using System.Collections;
using NUnit.Framework;
using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// Runs the actual physics. The headline test is that a dropped ring descends past
    /// the peg's top instead of resting on it: that is the behaviour a convex hull
    /// silently destroys, and no amount of inspector checking would reveal it.
    /// </summary>
    public sealed class RingPhysicsTests
    {
        private const float PegHeight = 0.9f;
        private const float PegRadius = 0.09f;

        private float originalMaximumDeltaTime;
        private float originalCaptureDeltaTime;
        private PhysicsConfig config;
        private GameObject ground;
        private GameObject peg;
        private GameObject ringObject;
        private RingController ring;

        [SetUp]
        public void SetUp()
        {
            // Unity runs catch-up fixed steps when a frame is slow, so a single
            // WaitForFixedUpdate can advance physics by far more than one step. Capping
            // the maximum delta at one fixed step makes these tests deterministic
            // instead of dependent on how fast the machine rendered the frame.
            // Batch mode frames take wildly different amounts of real time, and Unity
            // runs catch-up fixed steps to make up the difference. Between two
            // observations the ring could fall the entire way to the ground, which
            // makes a descent test look like the ring never descended.
            //
            // Time.maximumDeltaTime is not enough on its own: Unity clamps it to a
            // floor of about 0.03 s, which is still two steps. captureDeltaTime pins
            // every frame to exactly one fixed step, which is what makes this
            // deterministic.
            originalMaximumDeltaTime = Time.maximumDeltaTime;
            originalCaptureDeltaTime = Time.captureDeltaTime;

            Time.maximumDeltaTime = Time.fixedDeltaTime;
            Time.captureDeltaTime = Time.fixedDeltaTime;

            config = ScriptableObject.CreateInstance<PhysicsConfig>();

            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";

            peg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            peg.name = "Peg";
            peg.transform.position = new Vector3(0f, PegHeight * 0.5f, 0f);
            peg.transform.localScale = new Vector3(PegRadius * 2f, PegHeight * 0.5f, PegRadius * 2f);

            ringObject = new GameObject("Ring");
            ringObject.AddComponent<Rigidbody>();

            RingColliderBuilder builder = ringObject.AddComponent<RingColliderBuilder>();
            builder.SetConfig(config);
            builder.Rebuild();

            ring = ringObject.AddComponent<RingController>();
            ring.SetConfig(config);
        }

        [TearDown]
        public void TearDown()
        {
            Time.maximumDeltaTime = originalMaximumDeltaTime;
            Time.captureDeltaTime = originalCaptureDeltaTime;

            Object.Destroy(ringObject);
            Object.Destroy(peg);
            Object.Destroy(ground);
            Object.Destroy(config);
        }

        [Test]
        public void Ring_uses_settings_that_prevent_tunnelling()
        {
            Rigidbody body = ring.Body;

            Assert.AreEqual(CollisionDetectionMode.ContinuousDynamic, body.collisionDetectionMode,
                "Without continuous detection the thin ring passes straight through the peg.");
            Assert.AreEqual(RigidbodyInterpolation.Interpolate, body.interpolation);
            Assert.IsFalse(body.useGravity, "Gravity is applied manually so the multiplier means something.");
        }

        [Test]
        public void Peg_is_thinner_than_the_ring_hole()
        {
            RingColliderBuilder builder = ringObject.GetComponent<RingColliderBuilder>();

            Assert.Less(PegRadius, builder.HoleRadius,
                "The peg must physically fit through the hole for the game to be winnable.");
        }

        [UnityTest]
        public IEnumerator Dropped_ring_descends_past_the_peg_top()
        {
            // Directly above the peg, flat, with a tiny downward nudge to remove any
            // dependence on how long the fall takes to start.
            ring.Arm(new Vector3(0f, PegHeight + 0.8f, 0f), Quaternion.identity);
            ring.Launch(new Vector3(0f, -1.5f, 0f), Vector3.zero);

            yield return Simulate(2.5f);

            float finalHeight = ringObject.transform.position.y;
            Vector3 position = ringObject.transform.position;
            float horizontalOffset = new Vector2(position.x, position.z).magnitude;

            Debug.Log($"[RingPhysicsTests] Settled at y={finalHeight:F4}, " +
                      $"offset={horizontalOffset:F4}, peg top={PegHeight:F2}.");

            Assert.Less(finalHeight, PegHeight * 0.5f,
                $"The ring stopped at y={finalHeight:F3}, at or above the peg top ({PegHeight:F2}). " +
                "That is what happens when the ring's hole is closed.");

            Assert.Less(horizontalOffset, 0.2f,
                $"The ring drifted {horizontalOffset:F3} away from the peg axis instead of settling over it.");
        }

        [UnityTest]
        public IEnumerator Ring_is_stopped_by_the_peg_when_it_does_not_line_up()
        {
            // Offset so the peg meets the ring's tube rather than its hole.
            RingColliderBuilder builder = ringObject.GetComponent<RingColliderBuilder>();
            float offset = builder.HoleRadius + config.RingTubeRadius;

            ring.Arm(new Vector3(offset, PegHeight * 0.6f, -1.5f), Quaternion.identity);
            ring.Launch(new Vector3(0f, 0f, 6f), Vector3.zero);

            yield return Simulate(1.5f);

            Assert.Less(ringObject.transform.position.z, 1.5f,
                "The ring passed straight through the peg, which means it tunnelled.");
        }

        [UnityTest]
        public IEnumerator Custom_gravity_multiplier_is_actually_applied()
        {
            // Started far higher than the fall needs. One WaitForFixedUpdate can span
            // many catch-up physics steps in batch mode, so the sampled window
            // routinely overshoots the requested 0.4 s. The assertions below are all
            // computed from the elapsed time actually measured, so overshoot is
            // harmless — as long as the ring has not reached the ground, which is what
            // the height buys.
            const float startHeight = 120f;
            const float duration = 0.4f;

            ring.Arm(new Vector3(0f, startHeight, 0f), Quaternion.identity);
            ring.Launch(Vector3.zero, Vector3.zero);

            float startTime = Time.fixedTime;
            yield return Simulate(duration);
            float elapsed = Time.fixedTime - startTime;

            float drop = startHeight - ringObject.transform.position.y;
            float expectedDrop = 0.5f * config.Gravity.magnitude * elapsed * elapsed;
            float standardGravityDrop = 0.5f * Physics.gravity.magnitude * elapsed * elapsed;

            Assert.Less(drop, startHeight - 1f, "The ring reached the ground; the sample window is too long.");


            Assert.Greater(drop, standardGravityDrop * 1.1f,
                "The ring fell no faster than standard gravity, so the multiplier is not being applied.");

            // Linear damping bleeds a little speed, so the tolerance is deliberately loose.
            Assert.AreEqual(expectedDrop, drop, expectedDrop * 0.2f);
        }

        /// <summary>
        /// Advances physics by at least the requested simulated time, measured against
        /// Time.fixedTime rather than by counting iterations, so a slow frame cannot
        /// make the test think less time passed than actually did.
        /// </summary>
        private static IEnumerator Simulate(float seconds)
        {
            float target = Time.fixedTime + seconds;

            while (Time.fixedTime < target)
            {
                yield return new WaitForFixedUpdate();
            }
        }
    }
}
