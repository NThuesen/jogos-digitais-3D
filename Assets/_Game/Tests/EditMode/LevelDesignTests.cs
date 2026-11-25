using NUnit.Framework;
using SlipperyZlope.RingToss.Config;
using UnityEngine;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// Rules the level data has to obey.
    ///
    /// The one that matters most is reachability: a peg placed outside the fan the
    /// throw can actually cover makes a level unwinnable, and nothing about the level
    /// asset looks wrong when that happens.
    /// </summary>
    public sealed class LevelDesignTests
    {
        private LevelConfig level;
        private PhysicsConfig physics;

        [SetUp]
        public void SetUp()
        {
            level = ScriptableObject.CreateInstance<LevelConfig>();
            physics = ScriptableObject.CreateInstance<PhysicsConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(level);
            Object.DestroyImmediate(physics);
        }

        [Test]
        public void Stars_reward_rings_left_over()
        {
            level.Configure("L", string.Empty, null,
                new[] { PegPlacement.Default(Vector2.zero) },
                rings: 5, scale: 1f, wobble: 0f,
                windForce: Vector3.zero, gust: 0f, gustHz: 0f,
                twoStars: 2, threeStars: 3);

            Assert.AreEqual(1, level.StarsFor(0), "Finishing with nothing spare is one star.");
            Assert.AreEqual(1, level.StarsFor(1));
            Assert.AreEqual(2, level.StarsFor(2));
            Assert.AreEqual(3, level.StarsFor(3));
            Assert.AreEqual(3, level.StarsFor(9), "More rings left cannot award fewer stars.");
        }

        [Test]
        public void Stars_never_decrease_as_rings_left_increases()
        {
            level.Configure("L", string.Empty, null,
                new[] { PegPlacement.Default(Vector2.zero) },
                rings: 8, scale: 1f, wobble: 0f,
                windForce: Vector3.zero, gust: 0f, gustHz: 0f,
                twoStars: 1, threeStars: 4);

            int previous = 0;

            for (int ringsLeft = 0; ringsLeft <= 8; ringsLeft++)
            {
                int stars = level.StarsFor(ringsLeft);
                Assert.GreaterOrEqual(stars, previous);
                previous = stars;
            }
        }

        [Test]
        public void Wind_without_a_gust_is_constant()
        {
            Vector3 wind = new Vector3(1.5f, 0f, 0f);

            level.Configure("L", string.Empty, null,
                new[] { PegPlacement.Default(Vector2.zero) },
                rings: 5, scale: 1f, wobble: 0f,
                windForce: wind, gust: 0f, gustHz: 0f,
                twoStars: 1, threeStars: 2);

            Assert.AreEqual(wind, level.WindAt(0f));
            Assert.AreEqual(wind, level.WindAt(3.7f));
        }

        [Test]
        public void Gust_stays_within_its_amplitude()
        {
            level.Configure("L", string.Empty, null,
                new[] { PegPlacement.Default(Vector2.zero) },
                rings: 5, scale: 1f, wobble: 0f,
                windForce: new Vector3(0.5f, 0f, 0f), gust: 2f, gustHz: 0.4f,
                twoStars: 1, threeStars: 2);

            for (float t = 0f; t < 12f; t += 0.05f)
            {
                float x = level.WindAt(t).x;

                Assert.LessOrEqual(x, 2.5f + 1e-3f);
                Assert.GreaterOrEqual(x, -1.5f - 1e-3f);
            }
        }

        [Test]
        public void Static_peg_never_moves()
        {
            PegPlacement placement = PegPlacement.Default(new Vector2(0.5f, 0.2f));

            Assert.AreEqual(Vector3.zero, placement.OffsetAt(0f));
            Assert.AreEqual(Vector3.zero, placement.OffsetAt(4.2f));
        }

        [Test]
        public void Sliding_peg_stays_within_its_amplitude()
        {
            PegPlacement placement = PegPlacement.Default(Vector2.zero);
            placement.motion = PegMotion.SlideX;
            placement.motionAmplitude = 0.7f;
            placement.motionSpeed = 0.4f;

            for (float t = 0f; t < 20f; t += 0.05f)
            {
                Vector3 offset = placement.OffsetAt(t);

                Assert.LessOrEqual(Mathf.Abs(offset.x), 0.7f + 1e-4f);
                Assert.AreEqual(0f, offset.y, 1e-5f);
                Assert.AreEqual(0f, offset.z, 1e-5f);
            }
        }

        [Test]
        public void Peg_motion_is_a_function_of_time_only()
        {
            PegPlacement placement = PegPlacement.Default(Vector2.zero);
            placement.motion = PegMotion.Orbit;
            placement.motionAmplitude = 0.5f;
            placement.motionSpeed = 0.3f;

            // Deterministic in time rather than integrated, so a peg can never drift
            // and a level always plays out the same way.
            Assert.AreEqual(placement.OffsetAt(3.3f), placement.OffsetAt(3.3f));
        }

        [Test]
        public void Phase_offsets_two_pegs_against_each_other()
        {
            PegPlacement first = PegPlacement.Default(Vector2.zero);
            first.motion = PegMotion.SlideX;
            first.motionAmplitude = 0.6f;
            first.motionSpeed = 0.5f;

            PegPlacement second = first;
            second.motionPhase = 0.5f;

            // Half a cycle apart means the two are always mirrored.
            Assert.AreEqual(-first.OffsetAt(1.4f).x, second.OffsetAt(1.4f).x, 1e-4f);
        }

        [Test]
        public void Ring_hole_stays_wider_than_the_peg_at_the_tightest_level_settings()
        {
            // The tightest combination the levels use: the smallest ring against the
            // fattest peg. If this inverts, that level simply cannot be completed, and
            // nothing about the asset would look wrong.
            const float smallestRingScale = 0.8f;
            const float largestPegScale = 1.5f;

            float hole = physics.RingHoleRadius * smallestRingScale;
            float peg = physics.PegRadius * largestPegScale;

            Assert.Greater(hole, peg * 1.4f,
                $"Hole {hole:F3} is not comfortably wider than peg {peg:F3}.");
        }
    }
}
