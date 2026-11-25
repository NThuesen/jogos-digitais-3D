using System.Collections;
using NUnit.Framework;
using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.Gameplay;
using SlipperyZlope.RingToss.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// Rules the fifty authored levels have to obey.
    ///
    /// The library is composed from layout helpers rather than hand-placed, and those
    /// helpers clamp every peg into the reachable fan on the way in — so this is
    /// checking that the clamp is doing its job and that the *other* levers, which it
    /// does not cover, have not put a level out of reach either. A peg on a plinth is
    /// the interesting case: it is inside the fan on the ground and can still be
    /// unreachable, because the throw has to arrive at it higher up the arc.
    ///
    /// These run in Play Mode only because the level assets are reached through the
    /// scene; nothing here needs physics.
    /// </summary>
    public sealed class LevelLibraryTests
    {
        private GameDirector director;
        private PhysicsConfig physics;
        private float launchHeight;
        private float originalCaptureDeltaTime;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // See GameLoopTests: batch mode frames vary wildly and Unity runs catch-up
            // fixed steps, which can drop a ring straight past a peg between two
            // observations.
            originalCaptureDeltaTime = Time.captureDeltaTime;
            Time.captureDeltaTime = Time.fixedDeltaTime;

            SceneManager.LoadScene("Main");

            yield return null;
            yield return null;
            yield return null;

            director = Object.FindFirstObjectByType<GameDirector>();
            Assert.IsNotNull(director, "The Main scene has no GameDirector.");

            PhysicsConfig[] loaded = Resources.FindObjectsOfTypeAll<PhysicsConfig>();
            Assert.Greater(loaded.Length, 0, "No PhysicsConfig is loaded.");
            physics = loaded[0];

            launchHeight = director.Throwing.Origin.position.y;
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

            if (!main.IsValid() || !main.isLoaded)
            {
                yield break;
            }

            SceneManager.SetActiveScene(SceneManager.CreateScene("AfterLevelLibraryTests"));
            yield return SceneManager.UnloadSceneAsync(main);
        }

        [UnityTest]
        public IEnumerator The_library_holds_fifty_levels_and_five_stages()
        {
            Assert.AreEqual(50, director.LevelCount);
            Assert.AreEqual(5, director.Library.ThemeCount);

            for (int i = 0; i < director.LevelCount; i++)
            {
                LevelConfig level = director.Library.Get(i);

                Assert.IsNotNull(level, $"Level {i + 1} is missing.");
                Assert.IsNotNull(level.Theme, $"Level {i + 1} has no theme.");
                Assert.IsNotEmpty(level.DisplayName, $"Level {i + 1} has no name.");
                Assert.Greater(level.PegCount, 0, $"Level {i + 1} has no pegs.");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Every_level_fits_the_pools_the_scene_built()
        {
            RingPool pool = Object.FindFirstObjectByType<RingPool>();
            LevelRunner runner = director.Board;

            Assert.IsNotNull(pool);
            Assert.IsNotNull(runner);

            for (int i = 0; i < director.LevelCount; i++)
            {
                LevelConfig level = director.Library.Get(i);

                Assert.LessOrEqual(level.RingCount, pool.Capacity,
                    $"'{level.DisplayName}' wants {level.RingCount} rings; the pool holds {pool.Capacity}.");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Every_peg_of_every_level_can_be_reached()
        {
            for (int i = 0; i < director.LevelCount; i++)
            {
                LevelConfig level = director.Library.Get(i);

                for (int p = 0; p < level.PegCount; p++)
                {
                    PegPlacement peg = level.GetPeg(p);

                    AssertReachable(level, i, p, peg, peg.anchor, "at rest");

                    if (peg.motion == PegMotion.Static || peg.motionAmplitude <= 0f)
                    {
                        continue;
                    }

                    // A peg that leaves the fan halfway through its cycle is a peg the
                    // player is punished for aiming at, at a moment they cannot predict.
                    for (float t = 0f; t < 10f; t += 0.1f)
                    {
                        Vector3 offset = peg.OffsetAt(t);
                        AssertReachable(level, i, p, peg,
                            peg.anchor + new Vector2(offset.x, offset.z), $"at t={t:F1}");
                    }
                }
            }

            yield return null;
        }

        private void AssertReachable(
            LevelConfig level, int index, int pegIndex, in PegPlacement peg, Vector2 anchor, string when)
        {
            float range = new Vector2(anchor.x, anchor.y - EndlessRoundBuilder.ThrowOriginZ).magnitude;
            float yaw = Mathf.Abs(
                Mathf.Atan2(anchor.x, anchor.y - EndlessRoundBuilder.ThrowOriginZ) * Mathf.Rad2Deg);

            string where = $"Level {index + 1} '{level.DisplayName}' peg {pegIndex} {when}";

            Assert.GreaterOrEqual(range, physics.MinThrowRange, $"{where}: {range:F2} m is too close.");
            Assert.LessOrEqual(range, physics.MaxThrowRange, $"{where}: {range:F2} m is too far.");
            Assert.LessOrEqual(yaw, physics.MaxYawDegrees, $"{where}: {yaw:F1} degrees is outside the fan.");

            // Reachable is not enough. A peg can sit comfortably inside the throw's fan
            // and still be off the side of a tall phone's screen, which is worse than
            // unreachable: the player cannot see what they are missing.
            Assert.IsTrue(CameraFraming.Fits(anchor),
                $"{where}: x={anchor.x:F2} at z={anchor.y:F2} is off the edge of a "
                + $"{CameraFraming.NarrowestAspect:F2} screen, where the frame is only "
                + $"{CameraFraming.SafeHalfWidthAt(anchor.y):F2} m wide.");

            // The height case. A peg on a plinth is met earlier on the arc than one on
            // the board, so the throw has to be aimed past it — and past the far end of
            // the drag is out of reach however legal its footprint is.
            float top = physics.PegHeight * Mathf.Max(0.01f, peg.heightScale) + peg.baseHeight;

            Assert.IsTrue(ThrowSolver.CanReach(range, top, launchHeight, physics),
                $"{where}: nothing reaches a {top:F2} m top at {range:F2} m.");
        }

        [UnityTest]
        public IEnumerator No_level_asks_for_a_peg_fatter_than_its_ring()
        {
            for (int i = 0; i < director.LevelCount; i++)
            {
                LevelConfig level = director.Library.Get(i);
                float hole = physics.RingHoleRadius * level.RingScale;

                for (int p = 0; p < level.PegCount; p++)
                {
                    float peg = physics.PegRadius * level.GetPeg(p).radiusScale;

                    Assert.Greater(hole, peg * 1.4f,
                        $"Level {i + 1} '{level.DisplayName}' peg {p}: hole {hole:F3} against peg {peg:F3}.");
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Difficulty_broadly_rises_across_the_stages()
        {
            // Not level by level — a curve that only ever goes up has no rhythm — but
            // the last stage has to be harder than the first by every lever that has a
            // number attached to it.
            float firstStageRing = 0f;
            float lastStageRing = 0f;
            int firstStagePegs = 0;
            int lastStagePegs = 0;

            for (int i = 0; i < 10; i++)
            {
                firstStageRing += director.Library.Get(i).RingScale;
                firstStagePegs += director.Library.Get(i).PegCount;

                lastStageRing += director.Library.Get(40 + i).RingScale;
                lastStagePegs += director.Library.Get(40 + i).PegCount;
            }

            Assert.Less(lastStageRing, firstStageRing, "The last stage does not use smaller rings.");
            Assert.Greater(lastStagePegs, firstStagePegs, "The last stage does not use more pegs.");

            yield return null;
        }

        /// <summary>
        /// The economy, end to end: a perfect ringer has to actually put tickets in the
        /// player's pocket. Everything else about the shop is arithmetic that
        /// EconomyTests already covers; this is the wire between the game and it.
        /// </summary>
        [UnityTest]
        public IEnumerator A_perfect_ringer_pays_tickets()
        {
            ProgressStore progress = director.Progress;
            Assert.IsNotNull(progress);

            progress.ResetAll(director.LevelCount);
            Assert.AreEqual(0, progress.Tickets);

            director.StartLevel(0);

            float deadline = Time.time + 6f;

            while (director.State != SessionState.Ready && Time.time < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(SessionState.Ready, director.State, "The level never armed a ring.");

            PegTarget peg = director.Board.ActivePegs[0];
            RingInstance ring = director.ActiveRing;
            Assert.IsNotNull(ring);

            ring.Arm(peg.TopPosition + Vector3.up * 0.55f, Quaternion.identity);
            director.Throwing.LaunchArmedRing(new Vector3(0f, -0.5f, 0f), Vector3.zero, 0.5f);

            deadline = Time.time + 8f;

            while (progress.Tickets == 0 && Time.time < deadline)
            {
                yield return null;
            }

            Assert.GreaterOrEqual(progress.Tickets, TicketRules.ForPerfect(),
                "A clean drop over the peg paid nothing.");
        }
    }
}
