using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Gameplay;
using UnityEditor;
using UnityEngine;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// Wind a player can beat.
    ///
    /// This fixture exists because level 44 shipped unwinnable and every other test in
    /// the project passed it. The reachability fixtures ask whether a peg can be hit by
    /// *some* throw, which is a question about geometry, and wind is not geometry — so
    /// a level could be authored with a crosswind twice the width of the ring's hole
    /// and nothing anywhere said a word.
    ///
    /// The distinction that matters is between the two halves of the wind:
    ///
    /// - The **constant** part is learnable. It pushes every throw the same way, the
    ///   player sees it happen once and aims into it thereafter. It is a skill, and a
    ///   large one is a legitimate difficulty lever.
    /// - The **gust** is not. It is a sine wave the player has no way to read, sampled
    ///   at whatever phase they happened to release on, so it is a random number added
    ///   to every throw. No amount of skill removes it.
    ///
    /// So the rule is about the gust alone: aiming perfectly into the *average* wind
    /// has to still land the ring, whatever phase the gust is caught at. Anything
    /// stricter would ban wind; anything looser is a coin flip wearing a level's
    /// clothes, which is what level 44 was.
    ///
    /// Level 44 failed this at 2.2x the ring's hole. It took a person playing to find
    /// that, which is the expensive way.
    /// </summary>
    public sealed class WindFairnessTests
    {
        /// <summary>
        /// How much of the ring's hole the unpredictable half of the wind may eat.
        ///
        /// Not 1.0. At exactly 1.0 a throw aimed perfectly at the mean lands on the
        /// rim at the worst phase, and the player has no error budget of their own
        /// left — which is another way of writing "unwinnable for humans". Two thirds
        /// leaves a third of the hole for the thumb.
        /// </summary>
        private const float FairFraction = 0.67f;

        private const string LibraryPath = "Assets/_Game/Data/LevelLibrary.asset";

        [Test]
        public void No_level_asks_the_player_to_guess_a_gust()
        {
            LevelLibrary library = AssetDatabase.LoadAssetAtPath<LevelLibrary>(LibraryPath);
            Assert.IsNotNull(library, $"No level library at {LibraryPath}.");

            PhysicsConfig physics = LoadPhysics();
            StringBuilder offenders = new StringBuilder();
            int windy = 0;

            for (int i = 0; i < library.Count; i++)
            {
                LevelConfig level = library.Get(i);

                if (level == null || level.GustAmplitude <= 0f)
                {
                    continue;
                }

                windy++;

                float hole = physics.RingHoleRadius * level.RingScale;
                float spread = WorstGustDrift(level, physics);
                float allowed = hole * FairFraction;

                if (spread <= allowed)
                {
                    continue;
                }

                offenders.AppendLine(
                    $"  {level.name}: gust drifts {spread:0.000} m, " +
                    $"hole is {hole:0.000} m, allowed {allowed:0.000} m " +
                    $"({spread / hole:0.0}x the hole)");
            }

            Assert.Greater(windy, 0, "No level uses gusting wind; this fixture is testing nothing.");

            Assert.IsEmpty(offenders.ToString(),
                "These levels ask the player to guess where an unreadable gust will " +
                "push the ring:\n" + offenders);
        }

        [Test]
        public void The_constant_wind_can_still_be_aimed_into()
        {
            // The other half of the rule. A steady crosswind is fair *if* it can be
            // compensated for, and the compensation is yaw — which is capped. A wind
            // that pushes the ring further sideways than the throw can be turned is
            // unwinnable for a different reason, and would otherwise pass the gust
            // test with a gust of zero.
            LevelLibrary library = AssetDatabase.LoadAssetAtPath<LevelLibrary>(LibraryPath);
            PhysicsConfig physics = LoadPhysics();

            StringBuilder offenders = new StringBuilder();

            for (int i = 0; i < library.Count; i++)
            {
                LevelConfig level = library.Get(i);

                if (level == null || Mathf.Abs(level.Wind.x) <= 0f)
                {
                    continue;
                }

                foreach (float range in Ranges(level))
                {
                    float drift = Drift(Mathf.Abs(level.Wind.x), range, physics);
                    float reach = range * Mathf.Sin(physics.MaxYawDegrees * Mathf.Deg2Rad);

                    if (drift <= reach * 0.8f)
                    {
                        continue;
                    }

                    offenders.AppendLine(
                        $"  {level.name}: wind pushes {drift:0.00} m at {range:0.00} m, " +
                        $"but the throw can only aim {reach:0.00} m off centre");
                    break;
                }
            }

            Assert.IsEmpty(offenders.ToString(),
                "These levels blow the ring further than it can be aimed:\n" + offenders);
        }

        /// <summary>
        /// The worst unpredictable sideways drift a level's gust can add, over every
        /// peg on it.
        ///
        /// The gust cycles far more slowly than a throw lasts — a third of a hertz
        /// against two thirds of a second — so within one flight it is very nearly a
        /// constant, held at whatever value it had on release. That is what makes it
        /// unfair and it is also what makes it easy to bound: the drift is the usual
        /// half a t squared, and the spread either side of the mean is the amplitude.
        /// </summary>
        private static float WorstGustDrift(LevelConfig level, PhysicsConfig physics)
        {
            float worst = 0f;

            foreach (float range in Ranges(level))
            {
                worst = Mathf.Max(worst, Drift(level.GustAmplitude, range, physics));
            }

            return worst;
        }

        private static float Drift(float acceleration, float range, PhysicsConfig physics)
        {
            float time = FlightTime(range, physics);
            return 0.5f * acceleration * time * time;
        }

        private static float FlightTime(float range, PhysicsConfig physics)
        {
            ThrowSolver.ShapeFor(range, LaunchHeight, physics, out float pitch, out float speed);

            float gravity = physics.GravityMagnitude;
            float rise = speed * Mathf.Sin(pitch * Mathf.Deg2Rad);
            float drop = LaunchHeight - physics.LandingHeight;

            // The positive root of the usual quadratic: when the arc comes back down
            // through the height a peg's top sits at.
            return (rise + Mathf.Sqrt(Mathf.Max(0f, rise * rise + 2f * gravity * drop))) / gravity;
        }

        private static IEnumerable<float> Ranges(LevelConfig level)
        {
            for (int i = 0; i < level.PegCount; i++)
            {
                Vector2 anchor = level.GetPeg(i).anchor;
                yield return new Vector2(anchor.x, anchor.y - EndlessRoundBuilder.ThrowOriginZ).magnitude;
            }
        }

        private static float LaunchHeight => EndlessRoundBuilder.LaunchHeight;

        private static PhysicsConfig LoadPhysics()
        {
            PhysicsConfig physics =
                AssetDatabase.LoadAssetAtPath<PhysicsConfig>("Assets/_Game/Data/PhysicsConfig.asset");

            Assert.IsNotNull(physics, "No PhysicsConfig asset.");
            return physics;
        }
    }
}
