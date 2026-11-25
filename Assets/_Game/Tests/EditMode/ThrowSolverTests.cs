using NUnit.Framework;
using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Gameplay;
using UnityEngine;

namespace SlipperyZlope.RingToss.Tests
{
    public sealed class ThrowSolverTests
    {
        private const float ScreenHeight = 1920f;

        /// <summary>Height the ring leaves the hand at, matching the scene's throw origin.</summary>
        private const float LaunchHeight = 1.15f;

        private PhysicsConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<PhysicsConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        private ThrowSolution Solve(Vector2 drag, Quaternion basis)
        {
            return ThrowSolver.Solve(drag, ScreenHeight, basis, LaunchHeight, config);
        }

        private ThrowSolution Solve(Vector2 drag) => Solve(drag, Quaternion.identity);

        /// <summary>The shortest drag, as a fraction of full power, that still throws.</summary>
        private float SmallestLegalFraction =>
            config.DeadzoneScreenFraction / config.MaxDragScreenFraction + 0.01f;

        /// <summary>A drag that is <paramref name="fraction"/> of the way to full power.</summary>
        private Vector2 ForwardDrag(float fraction)
        {
            return new Vector2(0f, ScreenHeight * config.MaxDragScreenFraction * fraction);
        }

        [Test]
        public void Drag_below_deadzone_is_rejected()
        {
            Vector2 tinyDrag = new Vector2(0f, ScreenHeight * config.DeadzoneScreenFraction * 0.5f);

            Assert.IsFalse(Solve(tinyDrag).IsValid, "A drag shorter than the deadzone must not throw.");
        }

        [Test]
        public void Backwards_drag_is_rejected()
        {
            // A backwards drag is not a negative throw. Reading it as one is how a
            // pull-back gesture ends up firing a ring into the counter.
            Assert.IsFalse(Solve(ForwardDrag(-0.8f)).IsValid);
        }

        [Test]
        public void Drag_above_deadzone_is_accepted()
        {
            ThrowSolution solution = Solve(ForwardDrag(0.5f));

            Assert.IsTrue(solution.IsValid);
            Assert.Greater(solution.Velocity.magnitude, 0f);
        }

        [Test]
        public void Longer_drag_throws_further()
        {
            float shortRange = Solve(ForwardDrag(0.35f)).Range;
            float longRange = Solve(ForwardDrag(0.9f)).Range;

            Assert.Greater(longRange, shortRange);
        }

        [Test]
        public void Range_is_clamped_at_maximum_drag()
        {
            ThrowSolution solution = Solve(ForwardDrag(8f));

            Assert.AreEqual(1f, solution.NormalisedForce, 1e-4f);
            Assert.AreEqual(config.MaxThrowRange, solution.Range, 1e-3f);
        }

        [Test]
        public void Weakest_legal_throw_reaches_the_minimum_range()
        {
            ThrowSolution solution = Solve(ForwardDrag(SmallestLegalFraction));

            Assert.IsTrue(solution.IsValid);
            Assert.Less(solution.Range, config.MinThrowRange + 0.35f,
                "The shortest legal throw has to actually land short, or near pegs are unreachable.");
        }

        /// <summary>
        /// The regression test for the bug that made near off-centre pegs impossible.
        ///
        /// Power used to come from the drag's total length, so aiming sideways added
        /// power. Reaching a peg 0.55 m off-axis at 2.3 m needed a swipe wide enough to
        /// throw at roughly twice the distance the peg was at, and no combination of
        /// drag could put a ring on it.
        /// </summary>
        [Test]
        public void Sideways_drag_does_not_change_the_power()
        {
            Vector2 straight = ForwardDrag(0.4f);
            Vector2 angled = straight + new Vector2(ScreenHeight * config.LateralDragScreenFraction, 0f);

            ThrowSolution straightSolution = Solve(straight);
            ThrowSolution angledSolution = Solve(angled);

            Assert.AreEqual(straightSolution.NormalisedForce, angledSolution.NormalisedForce, 1e-5f,
                "Aiming sideways must not also throw harder.");
            Assert.AreEqual(straightSolution.Range, angledSolution.Range, 1e-4f);
        }

        [Test]
        public void A_near_off_centre_peg_is_reachable()
        {
            // The nearest peg the level library places, at the widest column it uses
            // for that row. Depth and range are not the same number: a peg 0.55 m off
            // the axis at 2.3 m of depth is 2.37 m away along the throw.
            const float pegDepth = 2.3f;
            const float pegOffAxis = 0.55f;

            float pegRange = Mathf.Sqrt(pegDepth * pegDepth + pegOffAxis * pegOffAxis);
            float wantedYaw = Mathf.Atan2(pegOffAxis, pegDepth) * Mathf.Rad2Deg;

            Assert.Less(wantedYaw, config.MaxYawDegrees, "The peg has to be inside the yaw limit.");
            Assert.Greater(pegRange, config.MinThrowRange, "The peg has to be inside the range limit.");

            float wantedForce = (pegRange - config.MinThrowRange)
                                / (config.MaxThrowRange - config.MinThrowRange);

            Vector2 drag = new Vector2(
                ScreenHeight * config.LateralDragScreenFraction * (wantedYaw / config.MaxYawDegrees),
                ScreenHeight * config.MaxDragScreenFraction * wantedForce);

            ThrowSolution solution = Solve(drag);

            Assert.IsTrue(solution.IsValid);

            Vector3 landing = LandingPoint(solution);

            Assert.AreEqual(pegOffAxis, landing.x, 0.06f);
            Assert.AreEqual(pegDepth, landing.z, 0.06f);
        }

        /// <summary>
        /// The throw has to actually go where the solver says it will. Everything else
        /// in the model is built on this: the preview draws it, the level design is
        /// laid out against it, and the player learns it.
        /// </summary>
        [Test]
        public void The_solved_throw_lands_at_the_solved_range()
        {
            // From just above the deadzone: below it there is deliberately no throw.
            for (float fraction = SmallestLegalFraction; fraction <= 1f; fraction += 0.05f)
            {
                ThrowSolution solution = Solve(ForwardDrag(fraction));

                Assert.IsTrue(solution.IsValid, $"Drag at {fraction:F2} should be a legal throw.");

                Vector3 landing = LandingPoint(solution);

                Assert.AreEqual(solution.Range, landing.z, 0.02f,
                    $"Drag at {fraction:F2} aimed at {solution.Range:F2} m but lands at {landing.z:F2} m.");
            }
        }

        [Test]
        public void Range_grows_smoothly_with_the_drag()
        {
            float previous = 0f;

            for (float fraction = 0.02f; fraction <= 1f; fraction += 0.02f)
            {
                ThrowSolution solution = Solve(ForwardDrag(fraction));

                if (!solution.IsValid)
                {
                    continue;
                }

                Assert.GreaterOrEqual(solution.Range, previous - 1e-4f,
                    "A longer drag must never land shorter.");

                previous = solution.Range;
            }
        }

        [Test]
        public void Throw_is_aimed_forward_and_upward()
        {
            ThrowSolution solution = Solve(ForwardDrag(0.6f));

            Assert.Greater(solution.Velocity.z, 0f, "A forward swipe must send the ring towards +Z.");
            Assert.Greater(solution.Velocity.y, 0f, "The throw must have upward pitch.");
        }

        [Test]
        public void Sideways_drag_yaws_the_throw_and_stays_clamped()
        {
            // Far more sideways drag than the configured maximum, to prove the clamp holds.
            Vector2 drag = new Vector2(
                ScreenHeight * config.LateralDragScreenFraction * 5f,
                ScreenHeight * config.MaxDragScreenFraction * 0.6f);

            ThrowSolution solution = Solve(drag);

            Vector3 flat = new Vector3(solution.Velocity.x, 0f, solution.Velocity.z);
            float yaw = Vector3.SignedAngle(Vector3.forward, flat, Vector3.up);

            Assert.Greater(yaw, 0f, "A rightward swipe must yaw the throw to the right.");
            Assert.AreEqual(config.MaxYawDegrees, yaw, 1e-3f);
        }

        [Test]
        public void Yaw_does_not_change_how_far_the_throw_travels()
        {
            ThrowSolution straight = Solve(ForwardDrag(0.7f));

            Vector2 angled = ForwardDrag(0.7f)
                             + new Vector2(ScreenHeight * config.LateralDragScreenFraction * 0.8f, 0f);

            ThrowSolution turned = Solve(angled);

            Vector3 straightLanding = LandingPoint(straight);
            Vector3 turnedLanding = LandingPoint(turned);

            // Yaw swings the landing point around an arc rather than shortening it, so
            // the player only has to learn one relationship between drag and distance.
            Assert.AreEqual(straightLanding.magnitude, turnedLanding.magnitude, 0.02f);
        }

        [Test]
        public void Aim_basis_rotates_the_throw()
        {
            ThrowSolution solution = Solve(ForwardDrag(0.6f), Quaternion.Euler(0f, 90f, 0f));

            Assert.Greater(solution.Velocity.x, 0f, "With a 90 degree basis the throw must go towards +X.");
            Assert.Less(Mathf.Abs(solution.Velocity.z), 1e-3f);
        }

        [Test]
        public void Solving_is_deterministic()
        {
            Vector2 drag = new Vector2(60f, ScreenHeight * config.MaxDragScreenFraction * 0.7f);

            ThrowSolution first = Solve(drag);
            ThrowSolution second = Solve(drag);

            Assert.AreEqual(first.Velocity, second.Velocity);
            Assert.AreEqual(first.AngularVelocity, second.AngularVelocity);
        }

        [Test]
        public void Spin_is_applied_around_the_ring_axis()
        {
            ThrowSolution solution = Solve(ForwardDrag(0.8f));

            Assert.Greater(solution.AngularVelocity.y, 0f, "The ring must spin around its own axis.");
        }

        [Test]
        public void Solved_speed_stays_inside_the_configured_limits()
        {
            for (float fraction = SmallestLegalFraction; fraction <= 1f; fraction += 0.05f)
            {
                float speed = Solve(ForwardDrag(fraction)).Velocity.magnitude;

                Assert.GreaterOrEqual(speed, config.MinThrowSpeed - 1e-3f);
                Assert.LessOrEqual(speed, config.MaxThrowSpeed + 1e-3f);
            }
        }

        [Test]
        public void Cancel_triggers_only_when_the_drag_never_went_forward()
        {
            Vector2 nearPress = new Vector2(0f, ScreenHeight * config.CancelScreenFraction * 0.5f);
            Vector2 forward = new Vector2(0f, ScreenHeight * config.CancelScreenFraction * 4f);
            Vector2 sidewaysOnly = new Vector2(ScreenHeight * 0.3f, 0f);

            Assert.IsTrue(ThrowSolver.ShouldCancel(nearPress, ScreenHeight, config));
            Assert.IsFalse(ThrowSolver.ShouldCancel(forward, ScreenHeight, config));
            Assert.IsTrue(ThrowSolver.ShouldCancel(sidewaysOnly, ScreenHeight, config),
                "A drag that only ever went sideways is not a throw.");
        }

        /// <summary>
        /// Where a solved throw actually arrives at the peg-top plane, worked out from
        /// the velocity rather than from the solver's own answer, so the two can be
        /// compared against each other.
        /// </summary>
        private Vector3 LandingPoint(in ThrowSolution solution)
        {
            float gravity = config.GravityMagnitude;
            float rise = solution.Velocity.y;
            float drop = LaunchHeight - config.LandingHeight;

            // Positive root of: launch + rise*t - g/2*t^2 = landing
            float discriminant = rise * rise + 2f * gravity * drop;

            Assert.Greater(discriminant, 0f, "The throw never reaches the landing height.");

            float time = (rise + Mathf.Sqrt(discriminant)) / gravity;

            return new Vector3(solution.Velocity.x, 0f, solution.Velocity.z) * time;
        }
    }
}
