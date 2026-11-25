using System.Collections;
using NUnit.Framework;
using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// The whole machine, running in the real scene: boot, arm, throw, resolve, and
    /// end the level.
    ///
    /// Throws are driven through <see cref="ThrowController.LaunchArmedRing"/>, which
    /// is the same exit the player's drag goes through. Synthesising touch input would
    /// be testing the input layer instead of the loop, and would be far more fragile.
    ///
    /// These reset the locally stored progress when they finish, because clearing
    /// level one really does unlock level two.
    /// </summary>
    public sealed class GameLoopTests
    {
        private GameDirector director;
        private LevelResult lastResult;
        private bool levelEnded;
        private float originalCaptureDeltaTime;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Batch mode frames take wildly varying amounts of real time, and Unity
            // runs catch-up fixed steps to make up the difference. A dropped ring can
            // fall the whole way to the ground between two frames, sailing past the
            // peg's capture window entirely. Pinning the frame delta makes the loop
            // advance one fixed step at a time, the way it does on a device.
            originalCaptureDeltaTime = Time.captureDeltaTime;
            Time.captureDeltaTime = Time.fixedDeltaTime;

            SceneManager.LoadScene("Main");

            // One frame for the load, then a few more for the generated scene to build
            // itself: the world, the ring pool and the interface are all made in Awake.
            yield return null;
            yield return null;
            yield return null;

            director = Object.FindFirstObjectByType<GameDirector>();
            Assert.IsNotNull(director, "The Main scene has no GameDirector.");

            levelEnded = false;
            GameEvents.LevelEnded += OnLevelEnded;
        }

        /// <summary>
        /// Leaves nothing behind.
        ///
        /// This fixture loads the real scene, and NUnit runs it before the physics
        /// fixtures, which build their own two-object worlds. Without unloading, those
        /// worlds are assembled on top of a full carnival — complete with its own
        /// ground, board and pegs at the same coordinates — and they fail for reasons
        /// that have nothing to do with what they are testing.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameEvents.LevelEnded -= OnLevelEnded;

            if (director != null && director.Progress != null)
            {
                director.Progress.ResetAll(director.LevelCount);
            }

            // A hit stop caught mid-freeze would otherwise hand the next fixture a
            // stopped clock.
            Time.timeScale = 1f;
            Time.captureDeltaTime = originalCaptureDeltaTime;

            Scene main = SceneManager.GetSceneByName("Main");

            if (!main.IsValid() || !main.isLoaded)
            {
                yield break;
            }

            SceneManager.SetActiveScene(SceneManager.CreateScene("AfterGameLoopTests"));
            yield return SceneManager.UnloadSceneAsync(main);
        }

        private void OnLevelEnded(LevelResult result)
        {
            lastResult = result;
            levelEnded = true;
        }

        [UnityTest]
        public IEnumerator Scene_boots_with_a_level_library_and_a_ring_pool()
        {
            Assert.Greater(director.LevelCount, 0, "No levels are wired into the director.");

            RingPool pool = Object.FindFirstObjectByType<RingPool>();
            Assert.IsNotNull(pool, "The scene has no RingPool.");
            Assert.AreEqual(pool.Capacity, pool.All.Count, "The pool did not build its rings.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator Every_level_fits_inside_the_ring_pool()
        {
            // A level asking for more rings than the pool holds would run dry mid-level
            // and log an error instead of failing the level cleanly.
            RingPool pool = Object.FindFirstObjectByType<RingPool>();

            for (int i = 0; i < director.LevelCount; i++)
            {
                Config.LevelConfig level = director.Library.Get(i);

                Assert.IsNotNull(level, $"Level {i} is missing from the library.");
                Assert.LessOrEqual(level.RingCount, pool.Capacity,
                    $"'{level.DisplayName}' wants {level.RingCount} rings; the pool holds {pool.Capacity}.");
                Assert.Greater(level.PegCount, 0, $"'{level.DisplayName}' has no pegs.");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Starting_a_level_arms_a_ring()
        {
            director.StartLevel(0);

            yield return WaitForState(SessionState.Ready, 4f);

            Assert.IsNotNull(director.ActiveRing, "No ring was handed to the player.");
            Assert.AreEqual(RingState.Armed, director.ActiveRing.State);
            Assert.AreEqual(director.CurrentLevel.RingCount, director.RingsRemaining);
        }

        [UnityTest]
        public IEnumerator Ringing_the_only_peg_clears_level_one()
        {
            director.StartLevel(0);
            yield return WaitForState(SessionState.Ready, 4f);

            yield return DropRingOnPeg();
            yield return WaitForLevelEnd(8f);

            Assert.IsTrue(lastResult.Completed, "Claiming the only peg must clear the level.");
            Assert.Greater(lastResult.Score, 0, "A cleared level must have scored something.");
            Assert.GreaterOrEqual(lastResult.Stars, 1);
        }

        [UnityTest]
        public IEnumerator A_clean_drop_counts_as_perfect()
        {
            director.StartLevel(0);
            yield return WaitForState(SessionState.Ready, 4f);

            yield return DropRingOnPeg();
            yield return WaitForLevelEnd(8f);

            // The ring went straight down the peg's axis without ever touching it,
            // which is exactly what the perfect grade is for.
            Assert.AreEqual(1, lastResult.PerfectCount, "A clean descent should be graded perfect.");
        }

        [UnityTest]
        public IEnumerator Throwing_the_rings_away_fails_the_level()
        {
            director.StartLevel(0);
            yield return WaitForState(SessionState.Ready, 4f);

            int rings = director.CurrentLevel.RingCount;

            for (int i = 0; i < rings && !levelEnded; i++)
            {
                yield return WaitForState(SessionState.Ready, 5f);

                // Up and backwards: this cannot reach the peg.
                director.Throwing.LaunchArmedRing(new Vector3(0f, 3f, -4f), Vector3.zero, 0.5f);

                yield return WaitForResolution(9f);
            }

            yield return WaitForLevelEnd(8f);

            Assert.IsFalse(lastResult.Completed, "Missing with every ring must fail the level.");
            Assert.AreEqual(0, lastResult.Stars, "A failed level awards no stars.");
        }

        [UnityTest]
        public IEnumerator Re_arming_after_a_miss_stays_under_the_budget()
        {
            director.StartLevel(0);
            yield return WaitForState(SessionState.Ready, 4f);

            director.Throwing.LaunchArmedRing(new Vector3(0f, 2.5f, -3f), Vector3.zero, 0.4f);

            yield return WaitForResolution(9f);

            float startedWaiting = Time.time;
            yield return WaitForState(SessionState.Ready, 4f);
            float elapsed = Time.time - startedWaiting;

            // The spec calls the gap between failing and being able to throw again the
            // most important number in the game, and puts it under 400 ms.
            Assert.Less(elapsed, 0.4f, $"Re-arming took {elapsed * 1000f:F0} ms.");
        }

        /// <summary>
        /// Moves the armed ring above the peg and lets it fall down the axis. The
        /// ballistic path a player takes is the throw solver's job and has its own
        /// tests; what matters here is that the loop reacts to a capture.
        /// </summary>
        private IEnumerator DropRingOnPeg()
        {
            PegTarget peg = director.Board.ActivePegs[0];
            RingInstance ring = director.ActiveRing;

            Assert.IsNotNull(ring, "Nothing was armed to drop.");

            ring.Arm(peg.TopPosition + Vector3.up * 0.55f, Quaternion.identity);
            director.Throwing.LaunchArmedRing(new Vector3(0f, -0.5f, 0f), Vector3.zero, 0.5f);

            yield return null;
        }

        // Every deadline in this fixture is measured in *simulated* seconds, not on
        // the wall clock.
        //
        // Batchmode pins the frame delta to one fixed step so the physics is
        // deterministic — see the note on Time.captureDeltaTime — which means
        // simulated time advances per frame and a wall-clock timeout is really a
        // budget of *frames*, priced at whatever the machine happens to render at.
        // Adding scenery to the scene is enough to change that price: forty more
        // renderers slowed batchmode frames enough that a ten second budget stopped
        // covering a ring's flight, and the ring pool fixture failed in a full suite
        // while passing on its own. Nothing about the game had changed.
        private IEnumerator WaitForState(SessionState state, float timeout)
        {
            float deadline = Time.time + timeout;

            while (director.State != state && Time.time < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(state, director.State, $"Timed out waiting for {state}.");
        }

        private IEnumerator WaitForResolution(float timeout)
        {
            float deadline = Time.time + timeout;

            while (director.State == SessionState.Flying && Time.time < deadline)
            {
                yield return null;
            }

            Assert.AreNotEqual(SessionState.Flying, director.State, "The throw never resolved.");
        }

        private IEnumerator WaitForLevelEnd(float timeout)
        {
            float deadline = Time.time + timeout;

            while (!levelEnded && Time.time < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(levelEnded, "The level never ended.");
        }
    }
}
