using NUnit.Framework;
using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.Gameplay;
using UnityEngine;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// The capture rule, tested as the geometric predicate it is.
    ///
    /// Deciding whether a ring "went over" a peg is the single judgement the whole
    /// game rests on. Getting it slightly wrong does not crash anything — it produces
    /// a game that feels arbitrary, which is far harder to notice and far harder to
    /// diagnose than a crash. So each condition it depends on gets a case that fails
    /// when only that condition is violated.
    ///
    /// The ring is driven down by hand rather than dropped. Batch mode runs a variable
    /// number of catch-up physics steps between frames, so a dropped ring can pass the
    /// whole capture window between two observations, which looks identical to a
    /// broken rule. Whether the peg can physically fit through the hole is covered by
    /// <see cref="RingPhysicsTests"/>, which does use real physics.
    /// </summary>
    public sealed class PegCaptureTests
    {
        private const float StartHeight = 1.9f;
        private const float StepHeight = 0.02f;

        private PhysicsConfig config;
        private PegTarget peg;
        private RingInstance ring;

        [SetUp]
        public void SetUp()
        {
            // Everything here is built by hand into whatever scene is current, so the
            // fixture is only meaningful if nothing else is standing in the same place.
            Assert.IsNull(Object.FindFirstObjectByType<GameDirector>(),
                "A game scene is still loaded; this fixture needs an empty world.");

            config = ScriptableObject.CreateInstance<PhysicsConfig>();

            peg = PegFactory.Create("Peg", null, config, null, default);
            peg.Apply(PegPlacement.Default(Vector2.zero), Vector3.zero);

            ring = CreateRing();
        }

        [TearDown]
        public void TearDown()
        {
            if (ring != null)
            {
                Object.DestroyImmediate(ring.gameObject);
            }

            if (peg != null)
            {
                Object.DestroyImmediate(peg.gameObject);
            }

            Object.DestroyImmediate(config);
        }

        private RingInstance CreateRing()
        {
            GameObject root = new GameObject("Ring");

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;

            RingColliderBuilder builder = root.AddComponent<RingColliderBuilder>();
            builder.SetConfig(config);
            builder.SetShowSegments(false);
            builder.Rebuild();

            RingController controller = root.AddComponent<RingController>();
            controller.SetConfig(config);

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            RingInstance instance = root.AddComponent<RingInstance>();
            instance.Bind(config, null, builder, visual.transform, null, null, null, null);

            return instance;
        }

        /// <summary>
        /// Walks the ring straight down from <paramref name="start"/> and reports
        /// whether the peg ever claimed it.
        /// </summary>
        private bool LowerThroughPeg(Vector3 start, Quaternion rotation)
        {
            ring.Arm(start, rotation);

            // Launch to put the ring into the flying state the rule requires, then
            // take the rigidbody straight back out of the simulation: this fixture
            // moves the ring itself so every step is the same size.
            ring.Launch(Vector3.down, Vector3.zero);
            ring.Controller.Freeze();

            Vector3 position = start;

            for (int step = 0; step < 200; step++)
            {
                position.y -= StepHeight;
                ring.transform.position = position;

                if (peg.TryCapture(ring))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Lowers the ring past the peg top slightly off the axis, then slides it onto
        /// the axis while it keeps sinking — a ring that clipped the peg on the way in
        /// and dropped onto it, which is what most real ringers look like.
        /// </summary>
        private bool LowerThenSettleOn(float startOffset)
        {
            ring.Arm(new Vector3(startOffset, StartHeight, 0f), Quaternion.identity);
            ring.Launch(Vector3.down, Vector3.zero);
            ring.Controller.Freeze();

            Vector3 position = new Vector3(startOffset, StartHeight, 0f);

            for (int step = 0; step < 200; step++)
            {
                position.y -= StepHeight;

                // The settle starts *below* the capture threshold, which is a little
                // under the peg's top. That ordering is the whole point of the test:
                // the ring has to still be off-axis on the frame it crosses, and be
                // on-axis afterwards.
                if (position.y < peg.TopPosition.y - 0.2f)
                {
                    position.x = Mathf.MoveTowards(position.x, 0f, startOffset / 6f);
                }

                ring.transform.position = position;

                if (peg.TryCapture(ring))
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void A_ring_that_settles_onto_the_peg_after_clipping_it_still_counts()
        {
            // The bug a player reported as "I hit it and it didn't count".
            //
            // The capture rule fires on the frame the ring's centre crosses the peg
            // top, and asks on that one frame whether it is over the peg. A ring that
            // arrives a few centimetres out and then drops into place answers no on
            // the only frame it is ever asked — and then rests around the peg, scored
            // as a miss, with the evidence sitting on screen.
            Assert.IsTrue(LowerThenSettleOn(0.24f),
                "A ring that ends up around the peg has to be captured, whatever it " +
                "was doing on the frame it crossed the top.");
        }

        [Test]
        public void A_ring_that_falls_past_the_peg_still_does_not_count()
        {
            // The other side of the same change: widening the window must not turn a
            // near miss into a catch. This ring crosses off-axis and stays off-axis.
            ring.Arm(new Vector3(0.42f, StartHeight, 0f), Quaternion.identity);
            ring.Launch(Vector3.down, Vector3.zero);
            ring.Controller.Freeze();

            Vector3 position = new Vector3(0.42f, StartHeight, 0f);
            bool captured = false;

            for (int step = 0; step < 200 && !captured; step++)
            {
                position.y -= StepHeight;
                ring.transform.position = position;
                captured = peg.TryCapture(ring);
            }

            Assert.IsFalse(captured, "A ring that never comes over the peg must not count.");
        }

        [Test]
        public void Ring_lowered_over_the_peg_is_captured()
        {
            Assert.IsTrue(LowerThroughPeg(new Vector3(0f, StartHeight, 0f), Quaternion.identity),
                "A ring coming down the peg's axis must be captured.");
        }

        [Test]
        public void Ring_lowered_beside_the_peg_is_not_captured()
        {
            // Half a metre off the axis is nearly twice the hole's radius, so the peg
            // cannot possibly be inside the ring.
            Assert.IsFalse(LowerThroughPeg(new Vector3(0.5f, StartHeight, 0f), Quaternion.identity),
                "A ring landing beside the peg must not count.");
        }

        [Test]
        public void Ring_just_outside_the_hole_is_not_captured()
        {
            // Sitting on the rim rather than around the peg. This is the case a
            // trigger volume would get wrong, and the one that makes a game feel
            // arbitrary when it is wrong.
            float justOutside = config.RingHoleRadius * 0.95f;

            Assert.IsFalse(LowerThroughPeg(new Vector3(justOutside, StartHeight, 0f), Quaternion.identity),
                "A ring hanging on the rim has not been ringed onto the peg.");
        }

        [Test]
        public void Ring_on_its_edge_is_not_captured()
        {
            // Rolled onto its edge the hole faces sideways, so the ring can pass over
            // the peg's position without the peg ever being through it.
            Assert.IsFalse(LowerThroughPeg(new Vector3(0f, StartHeight, 0f), Quaternion.Euler(0f, 0f, 90f)),
                "A ring on its edge has not been ringed onto anything.");
        }

        [Test]
        public void Ring_that_never_descends_is_not_captured()
        {
            ring.Arm(new Vector3(0f, StartHeight, 0f), Quaternion.identity);
            ring.Launch(Vector3.down, Vector3.zero);
            ring.Controller.Freeze();

            // Held above the peg. The rule is about descending past the top, not about
            // being near it.
            for (int step = 0; step < 50; step++)
            {
                Assert.IsFalse(peg.TryCapture(ring));
            }
        }

        [Test]
        public void A_claimed_peg_cannot_be_claimed_twice()
        {
            Assert.IsTrue(LowerThroughPeg(new Vector3(0f, StartHeight, 0f), Quaternion.identity));

            peg.MarkClaimed();
            peg.ResetForThrow();

            Assert.IsFalse(LowerThroughPeg(new Vector3(0f, StartHeight, 0f), Quaternion.identity),
                "A peg that already has a ring on it is out of play.");
        }

        [Test]
        public void A_peg_fatter_than_the_hole_can_never_be_ringed()
        {
            // The mutation check for the whole mechanism. A peg wider than the hole
            // physically cannot pass through it, and the rule has to say so rather
            // than leaving it to the collider to be true by accident.
            PegPlacement fat = PegPlacement.Default(Vector2.zero);
            fat.radiusScale = config.RingHoleRadius / config.PegRadius * 1.4f;

            peg.Apply(fat, Vector3.zero);

            Assert.Greater(peg.Radius, config.RingHoleRadius, "The fattened peg is not actually too fat.");
            Assert.IsFalse(LowerThroughPeg(new Vector3(0f, StartHeight, 0f), Quaternion.identity),
                "A peg fatter than the hole cannot be ringed.");
        }

        [Test]
        public void A_taller_peg_moves_the_capture_window_up_with_it()
        {
            PegPlacement tall = PegPlacement.Default(Vector2.zero);
            tall.heightScale = 1.6f;

            peg.Apply(tall, Vector3.zero);

            Assert.AreEqual(config.PegHeight * 1.6f, peg.Height, 1e-4f);
            Assert.IsTrue(LowerThroughPeg(new Vector3(0f, StartHeight + 0.8f, 0f), Quaternion.identity),
                "A taller peg is still ringable; the window just sits higher.");
        }
    }
}
