using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GameHub.Tests
{
    /// <summary>Menu to each game and back, through the real buttons.</summary>
    public sealed class HubNavigationTests
    {
        private const float ProjectFixedDeltaTime = 1f / 60f;

        [UnityTest]
        public IEnumerator MenuOpensRingTossAndItsMenuButtonReturns()
        {
            yield return LoadMenu();
            Press("Ring Toss");
            yield return WaitForScene("Main");
            Assert.AreEqual(ProjectFixedDeltaTime, Time.fixedDeltaTime, 1e-5f);

            // Let the title screen finish building.
            yield return null;
            yield return null;
            Press("Hub Menu");
            yield return WaitForScene("Menu");
            Assert.AreEqual(ProjectFixedDeltaTime, Time.fixedDeltaTime, 1e-5f);
        }

        [UnityTest]
        public IEnumerator MenuOpensPocketPuttAtItsOwnPhysicsStepAndReturns()
        {
            yield return LoadMenu();
            Press("Pocket Putt");
            yield return WaitForScene("C1-01_Prototype");
            Assert.AreEqual(HubNavigation.PocketPuttFixedDeltaTime, Time.fixedDeltaTime, 1e-5f);
            Assert.IsNotNull(GameObject.Find("Player Ball"));

            HubNavigation.ReturnToMenu();
            yield return WaitForScene("Menu");
            Assert.AreEqual(ProjectFixedDeltaTime, Time.fixedDeltaTime, 1e-5f);
        }

        [UnityTest]
        public IEnumerator ReturningToMenuRestoresAStoppedClock()
        {
            yield return LoadMenu();
            Time.timeScale = 0f;
            HubNavigation.ReturnToMenu();
            yield return WaitForScene("Menu");
            Assert.AreEqual(1f, Time.timeScale);
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
            {
                yield return null;
            }

            Assert.AreEqual(name, SceneManager.GetActiveScene().name);
        }
    }
}
