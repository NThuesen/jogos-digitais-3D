using System.Collections;
using System.IO;
using NUnit.Framework;
using PocketPutt.Audio;
using PocketPutt.Game;
using PocketPutt.Levels;
using PocketPutt.Presentation;
using PocketPutt.Save;
using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Button = UnityEngine.UI.Button;

namespace GameHub.Tests
{
    public sealed class HubNavigationTests
    {
        private const float ProjectFixedDeltaTime = 1f / 60f;
        private const string ThrowsKey = "ringtoss.throws";

        private string previousSavePath;
        private string saveDirectory;
        private int? previousThrows;

        [SetUp]
        public void IsolateProgress()
        {
            previousSavePath = SaveStore.PathOverride;
            saveDirectory = Path.Combine(Path.GetTempPath(), "GameHubTests-" + System.Guid.NewGuid());
            Directory.CreateDirectory(saveDirectory);
            SaveStore.PathOverride = Path.Combine(saveDirectory, "pocketputt_save.json");
            previousThrows = PlayerPrefs.HasKey(ThrowsKey) ? PlayerPrefs.GetInt(ThrowsKey) : (int?)null;
            PlayerPrefs.SetInt(ThrowsKey, 0);
        }

        [TearDown]
        public void RestoreProgress()
        {
            SaveStore.PathOverride = previousSavePath;
            Directory.Delete(saveDirectory, true);
            if (previousThrows.HasValue) PlayerPrefs.SetInt(ThrowsKey, previousThrows.Value);
            else PlayerPrefs.DeleteKey(ThrowsKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator MenuOpensRingTossAndItsMenuButtonReturns()
        {
            yield return LoadMenu();
            Press("Ring Toss");
            yield return WaitForScene("Main");
            Assert.AreEqual(ProjectFixedDeltaTime, Time.fixedDeltaTime, 1e-5f);

            yield return null;
            yield return null;
            Press("Hub Menu");
            yield return WaitForScene("Menu");
            Assert.AreEqual(ProjectFixedDeltaTime, Time.fixedDeltaTime, 1e-5f);
        }

        [UnityTest]
        public IEnumerator SwitchingGamesTwicePreservesIndependentProgress()
        {
            yield return LoadMenu();
            int initialThrows = PlayerPrefs.GetInt(ThrowsKey);

            for (int cycle = 0; cycle < 2; cycle++)
            {
                Press("Pocket Putt");
                yield return WaitForScene("PocketPutt");
                yield return null;
                Assert.AreEqual(HubNavigation.PocketPuttFixedDeltaTime, Time.fixedDeltaTime, 1e-5f);
                GameApp app = GameApp.I;
                Assert.IsNotNull(app);
                Assert.AreEqual(Mode.Menu, app.Mode);
                AssertSceneOwners(0);
                Assert.AreEqual(50, LevelLibrary.All().Count);
                Assert.AreEqual(cycle == 0 ? 0 : 3, app.Progress.TotalStars());

                app.StartLevel("C1-01");
                Assert.AreEqual("C1-01", app.Runner.Level.id);
                Assert.IsTrue(app.Runner.Shoot(Vector2.up, 0.5f));
                Assert.AreEqual(1, app.Runner.Shots);
                if (cycle == 0) app.Progress.Record(app.CurrentLevel, 1);
                app.GoMenu();

                VisualElement button = app.GetComponentInChildren<UIDocument>()
                    .rootVisualElement.Q("return-to-game-hub");
                Assert.IsNotNull(button);
                using (var down = PointerDownEvent.GetPooled())
                {
                    down.target = button;
                    button.SendEvent(down);
                }
                using (var up = PointerUpEvent.GetPooled())
                {
                    up.target = button;
                    button.SendEvent(up);
                }

                yield return WaitForScene("Menu");
                Assert.AreEqual(ProjectFixedDeltaTime, Time.fixedDeltaTime, 1e-5f);
                Assert.IsTrue(GameApp.I == null && Sfx.I == null && TrophyIcons.I == null);
                AssertSceneOwners(1);
                Assert.AreEqual(initialThrows + cycle, PlayerPrefs.GetInt(ThrowsKey));

                string savedPocketProgress = File.ReadAllText(SaveStore.PathOverride);
                Press("Ring Toss");
                yield return WaitForScene("Main");
                yield return null;
                yield return null;
                AssertSceneOwners(1);
                GameDirector director = Object.FindAnyObjectByType<GameDirector>();
                ProgressStore progress = Object.FindAnyObjectByType<ProgressStore>();
                Assert.IsNotNull(director);
                Assert.IsNotNull(progress);
                Assert.AreEqual(initialThrows + cycle, progress.ThrowsEver);
                director.StartLevel(0);
                float deadline = Time.realtimeSinceStartup + 10f;
                while (director.State != SessionState.Ready && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.IsNotNull(director.CurrentLevel);
                Assert.AreEqual(SessionState.Ready, director.State);
                director.Throwing.LaunchArmedRing(Vector3.forward * 5f + Vector3.up * 2f, Vector3.up);
                Assert.AreEqual(initialThrows + cycle + 1, progress.ThrowsEver);
                director.ReturnToIdle();
                yield return null;
                Press("Hub Menu");
                yield return WaitForScene("Menu");
                Assert.AreEqual(savedPocketProgress, File.ReadAllText(SaveStore.PathOverride));
            }
        }

        [UnityTest]
        public IEnumerator ReturningToMenuRestoresAStoppedClockAndAudio()
        {
            yield return LoadMenu();
            Time.timeScale = 0f;
            AudioListener.pause = true;
            HubNavigation.ReturnToMenu();
            yield return WaitForScene("Menu");
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(AudioListener.pause);
        }

        private static void AssertSceneOwners(int eventSystems)
        {
            Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>().Length);
            Assert.AreEqual(eventSystems, Object.FindObjectsByType<EventSystem>().Length);
            Camera camera = Camera.main;
            Assert.IsNotNull(camera);
            Assert.AreEqual(SceneManager.GetActiveScene().handle, camera.gameObject.scene.handle);
        }

        private static IEnumerator LoadMenu()
        {
            SceneManager.LoadScene(HubNavigation.MenuScenePath);
            yield return WaitForScene("Menu");
            yield return null;
        }

        private static void Press(string name)
        {
            GameObject target = GameObject.Find(name);
            Assert.IsNotNull(target, $"No button named {name}");
            Button button = target.GetComponent<Button>();
            Assert.IsNotNull(button, $"{name} is not a button");
            button.onClick.Invoke();
        }

        private static IEnumerator WaitForScene(string name)
        {
            for (int frame = 0; frame < 300 && SceneManager.GetActiveScene().name != name; frame++)
                yield return null;
            Assert.AreEqual(name, SceneManager.GetActiveScene().name);
        }
    }
}
