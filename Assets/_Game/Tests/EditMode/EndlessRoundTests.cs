using NUnit.Framework;
using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Gameplay;
using UnityEngine;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// Rules every generated round has to obey.
    ///
    /// An authored level that is impossible gets found the first time somebody plays
    /// it. A generated one might only appear at round 37 of a run nobody has reached
    /// yet, and when it does the player has no way to tell an unwinnable board from a
    /// hard one — they just lose their run. That asymmetry is why the generator is
    /// tested far harder than the level library is.
    /// </summary>
    public sealed class EndlessRoundTests
    {
        /// <summary>How deep to check. Well past anything a person is likely to reach.</summary>
        private const int RoundsToCheck = 120;

        /// <summary>Pegs the scene builds. A round asking for more would silently lose some.</summary>
        private const int ScenePegCapacity = 6;

        private PhysicsConfig physics;
        private LevelConfig level;

        [SetUp]
        public void SetUp()
        {
            physics = ScriptableObject.CreateInstance<PhysicsConfig>();
            level = ScriptableObject.CreateInstance<LevelConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(level);
            Object.DestroyImmediate(physics);
        }

        private void Build(int round, int seed = 12345)
        {
            EndlessRoundBuilder.Build(level, round, seed, null, physics);
        }

        [Test]
        public void Every_peg_of_every_round_is_inside_the_reachable_fan()
        {
            for (int round = 1; round <= RoundsToCheck; round++)
            {
                for (int seed = 1; seed <= 6; seed++)
                {
                    Build(round, seed);

                    for (int i = 0; i < level.PegCount; i++)
                    {
                        AssertReachable(level.GetPeg(i).anchor, round, seed, i, "at rest");
                    }
                }
            }
        }

        /// <summary>
        /// A moving peg has to stay reachable for its whole cycle. One that leaves the
        /// fan halfway through is not a harder peg — it is a peg the player is
        /// punished for aiming at, at a moment they cannot predict.
        /// </summary>
        [Test]
        public void Moving_pegs_stay_inside_the_fan_for_their_whole_cycle()
        {
            for (int round = 1; round <= RoundsToCheck; round++)
            {
                for (int seed = 1; seed <= 4; seed++)
                {
                    Build(round, seed);

                    for (int i = 0; i < level.PegCount; i++)
                    {
                        PegPlacement peg = level.GetPeg(i);

                        if (peg.motion == PegMotion.Static || peg.motionAmplitude <= 0f)
                        {
                            continue;
                        }

                        for (float t = 0f; t < 12f; t += 0.05f)
                        {
                            Vector3 offset = peg.OffsetAt(t);
                            Vector2 moved = peg.anchor + new Vector2(offset.x, offset.z);

                            AssertReachable(moved, round, seed, i, $"at t={t:F2}");
                        }
                    }
                }
            }
        }

        private void AssertReachable(Vector2 anchor, int round, int seed, int index, string when)
        {
            float range = EndlessRoundBuilder.RangeOf(anchor);
            float yaw = Mathf.Abs(EndlessRoundBuilder.YawOf(anchor));

            string where = $"round {round}, seed {seed}, peg {index} {when}";

            Assert.GreaterOrEqual(range, physics.MinThrowRange, $"{where}: {range:F2} m is too close to reach.");
            Assert.LessOrEqual(range, physics.MaxThrowRange, $"{where}: {range:F2} m is too far to reach.");
            Assert.LessOrEqual(yaw, physics.MaxYawDegrees,
                $"{where}: {yaw:F1} degrees is outside the throw's yaw limit.");

            // And on screen, which is usually the tighter of the two limits.
            Assert.IsTrue(CameraFraming.Fits(anchor),
                $"{where}: x={anchor.x:F2} at z={anchor.y:F2} is off the edge of a "
                + $"{CameraFraming.NarrowestAspect:F2} screen.");
        }

        /// <summary>
        /// A peg on a plinth is inside the fan on the ground and can still be out of
        /// reach, because the throw has to arrive at it higher up the arc — which means
        /// aiming past it, and past the far end of the drag is nowhere.
        /// </summary>
        [Test]
        public void Every_raised_peg_can_still_be_reached()
        {
            for (int round = 1; round <= RoundsToCheck; round++)
            {
                for (int seed = 1; seed <= 6; seed++)
                {
                    Build(round, seed);

                    for (int i = 0; i < level.PegCount; i++)
                    {
                        PegPlacement peg = level.GetPeg(i);

                        if (peg.baseHeight <= 0f)
                        {
                            continue;
                        }

                        float top = physics.PegHeight * Mathf.Max(0.01f, peg.heightScale) + peg.baseHeight;

                        // Across the whole cycle, not just at rest: a raised peg that
                        // also slides in depth carries its plinth with it, and the
                        // height it can be reached at depends on how far away it is.
                        for (float t = 0f; t < 12f; t += 0.1f)
                        {
                            Vector3 offset = peg.OffsetAt(t);
                            Vector2 moved = peg.anchor + new Vector2(offset.x, offset.z);
                            float range = EndlessRoundBuilder.RangeOf(moved);

                            Assert.IsTrue(
                                ThrowSolver.CanReach(range, top, EndlessRoundBuilder.LaunchHeight, physics),
                                $"Round {round} seed {seed} peg {i} at t={t:F1}: nothing reaches a " +
                                $"{top:F2} m top at {range:F2} m.");
                        }
                    }
                }
            }
        }

        [Test]
        public void The_ring_hole_always_stays_wider_than_the_fattest_peg()
        {
            for (int round = 1; round <= RoundsToCheck; round++)
            {
                for (int seed = 1; seed <= 6; seed++)
                {
                    Build(round, seed);

                    float hole = physics.RingHoleRadius * level.RingScale;

                    for (int i = 0; i < level.PegCount; i++)
                    {
                        float peg = physics.PegRadius * level.GetPeg(i).radiusScale;

                        Assert.Greater(hole, peg * 1.4f,
                            $"Round {round} seed {seed} peg {i}: hole {hole:F3} against peg {peg:F3}.");
                    }
                }
            }
        }

        /// <summary>
        /// A board of retracting pegs must never present nothing to aim at. The player
        /// would be left holding a ring with no legal target and no way to know how
        /// long that will last.
        /// </summary>
        [Test]
        public void At_least_one_peg_is_always_standing()
        {
            for (int round = 1; round <= RoundsToCheck; round++)
            {
                for (int seed = 1; seed <= 4; seed++)
                {
                    Build(round, seed);

                    for (float t = 0f; t < 20f; t += 0.05f)
                    {
                        bool anyUp = false;

                        for (int i = 0; i < level.PegCount; i++)
                        {
                            // 0.6 is the presence the capture rule requires.
                            if (level.GetPeg(i).PresenceAt(t) >= 0.6f)
                            {
                                anyUp = true;
                                break;
                            }
                        }

                        Assert.IsTrue(anyUp,
                            $"Round {round} seed {seed} has no peg standing at t={t:F2}.");
                    }
                }
            }
        }

        [Test]
        public void Rounds_never_ask_for_more_pegs_than_the_scene_builds()
        {
            for (int round = 1; round <= RoundsToCheck; round++)
            {
                Build(round);

                Assert.GreaterOrEqual(level.PegCount, 1);
                Assert.LessOrEqual(level.PegCount, ScenePegCapacity,
                    $"Round {round} asks for {level.PegCount} pegs.");
            }
        }

        [Test]
        public void Difficulty_never_goes_backwards()
        {
            int previousPegs = 0;
            float previousRingScale = float.MaxValue;

            for (int round = 1; round <= RoundsToCheck; round++)
            {
                Build(round);

                Assert.GreaterOrEqual(level.PegCount, previousPegs,
                    $"Round {round} has fewer pegs than round {round - 1}.");
                Assert.LessOrEqual(level.RingScale, previousRingScale + 1e-4f,
                    $"Round {round} has a larger ring than round {round - 1}.");

                previousPegs = level.PegCount;
                previousRingScale = level.RingScale;
            }
        }

        [Test]
        public void The_first_rounds_are_gentle()
        {
            Build(1);

            Assert.AreEqual(1, level.PegCount, "Round one is one peg.");
            Assert.AreEqual(1f, level.RingScale, 1e-4f, "Round one uses a full size ring.");
            Assert.AreEqual(Vector3.zero, level.Wind, "Round one has no wind.");
            Assert.AreEqual(PegMotion.Static, level.GetPeg(0).motion, "Round one does not move.");
            Assert.IsFalse(level.GetPeg(0).Retracts, "Round one does not disappear.");
        }

        [Test]
        public void A_round_is_the_same_board_every_time_it_is_built()
        {
            Build(17, seed: 4242);

            Vector2[] first = new Vector2[level.PegCount];

            for (int i = 0; i < level.PegCount; i++)
            {
                first[i] = level.GetPeg(i).anchor;
            }

            Build(17, seed: 4242);

            Assert.AreEqual(first.Length, level.PegCount);

            for (int i = 0; i < level.PegCount; i++)
            {
                Assert.AreEqual(first[i], level.GetPeg(i).anchor,
                    "The same round of the same run has to lay out identically.");
            }
        }

        [Test]
        public void Two_runs_lay_the_same_round_out_differently()
        {
            Build(9, seed: 1);
            Vector2 first = level.GetPeg(0).anchor;

            Build(9, seed: 2);
            Vector2 second = level.GetPeg(0).anchor;

            Assert.AreNotEqual(first, second,
                "A fixed layout would make the mode a memory test after one evening.");
        }

        [Test]
        public void Later_rounds_use_the_deep_end_of_the_board()
        {
            Build(1);
            float earlyDeepest = DeepestRange();

            Build(30);
            float lateDeepest = DeepestRange();

            Assert.Greater(lateDeepest, earlyDeepest + 0.5f,
                "The board has to open up as the run goes on.");
        }

        private float DeepestRange()
        {
            float deepest = 0f;

            for (int i = 0; i < level.PegCount; i++)
            {
                deepest = Mathf.Max(deepest, EndlessRoundBuilder.RangeOf(level.GetPeg(i).anchor));
            }

            return deepest;
        }
    }
}
