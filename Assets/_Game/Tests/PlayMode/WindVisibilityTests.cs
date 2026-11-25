using System.Collections;
using NUnit.Framework;
using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// Wind is drawn on windy levels and nowhere else.
    ///
    /// The playtest recording of 2026-09-10 shows the streaks from level 47 still
    /// drifting over the title screen and through the endless rounds after it. The
    /// drawing follows the last wind it was told about, and nothing ever told it the
    /// wind had stopped: a calm level simply never mentions wind. So this plays a windy
    /// level, and then leaves it each of the two ways a player can.
    /// </summary>
    public sealed class WindVisibilityTests
    {
        private GameDirector director;
        private Transform windRoot;
        private float originalCaptureDeltaTime;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalCaptureDeltaTime = Time.captureDeltaTime;
            Time.captureDeltaTime = Time.fixedDeltaTime;

            SceneManager.LoadScene("Main");

            yield return null;
            yield return null;
            yield return null;

            director = Object.FindFirstObjectByType<GameDirector>();
            Assert.IsNotNull(director, "The Main scene has no GameDirector.");

            WorldBuilder world = Object.FindFirstObjectByType<WorldBuilder>();
            Assert.IsNotNull(world, "The Main scene has no WorldBuilder.");

            windRoot = world.transform.Find("Wind");
            Assert.IsNotNull(windRoot, "The world built no wind streaks, so there is nothing to check.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (director != null && director.Progress != null)
            {
                director.Progress.ResetAll(director.LevelCount);
            }

            Time.timeScale = 1f;
            Time.captureDeltaTime = originalCaptureDeltaTime;

            Scene main = SceneManager.GetSceneByName("Main");

            if (main.IsValid() && main.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("Empty " + System.Guid.NewGuid());
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(main);
            }
        }

        [UnityTest]
        public IEnumerator Wind_is_only_drawn_while_a_windy_level_is_being_played()
        {
            int windy = -1;
            int calm = -1;

            for (int i = 0; i < director.LevelCount; i++)
            {
                LevelConfig level = director.Library.Get(i);

                if (level == null)
                {
                    continue;
                }

                if (level.HasWind && windy < 0) windy = i;
                if (!level.HasWind && calm < 0) calm = i;
            }

            Assert.GreaterOrEqual(windy, 0, "No level has wind.");
            Assert.GreaterOrEqual(calm, 0, "Every level has wind.");

            director.StartLevel(windy);
            yield return WaitUntilDrawn(true, 3f);
            Assert.IsTrue(windRoot.gameObject.activeSelf, $"Level {windy + 1} has wind and none was drawn.");

            // Straight on to a calm level, the way NEXT and the level grid both do it.
            director.StartLevel(calm);
            yield return Frames(3);
            Assert.IsFalse(windRoot.gameObject.activeSelf,
                $"Level {calm + 1} has no wind, and level {windy + 1}'s is still being drawn over it.");

            director.StartLevel(windy);
            yield return WaitUntilDrawn(true, 3f);
            Assert.IsTrue(windRoot.gameObject.activeSelf);

            // And back to the title with BACK, which is where the recording caught it.
            director.ReturnToIdle();
            yield return Frames(3);
            Assert.IsFalse(windRoot.gameObject.activeSelf, "The title screen is drawing wind.");
        }

        private IEnumerator WaitUntilDrawn(bool drawn, float seconds)
        {
            float deadline = Time.time + seconds;

            while (windRoot.gameObject.activeSelf != drawn && Time.time < deadline)
            {
                yield return null;
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }
    }
}
