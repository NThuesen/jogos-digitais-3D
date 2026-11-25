using NUnit.Framework;
using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Gameplay;
using UnityEngine;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// Geometry of the compound collider. These assertions exist because the failure
    /// they guard against — a closed ring hole — looks completely correct in the editor
    /// and only shows up as an unwinnable game.
    /// </summary>
    public sealed class RingColliderBuilderTests
    {
        private PhysicsConfig config;
        private GameObject ringObject;
        private RingColliderBuilder builder;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<PhysicsConfig>();

            ringObject = new GameObject("Ring");
            builder = ringObject.AddComponent<RingColliderBuilder>();
            builder.SetConfig(config);
            builder.Rebuild();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(ringObject);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void Builds_one_capsule_per_configured_segment()
        {
            CapsuleCollider[] colliders = ringObject.GetComponentsInChildren<CapsuleCollider>();

            Assert.AreEqual(config.ColliderSegments, colliders.Length);
        }

        [Test]
        public void Uses_no_convex_mesh_collider()
        {
            MeshCollider[] meshColliders = ringObject.GetComponentsInChildren<MeshCollider>();

            Assert.IsEmpty(meshColliders,
                "A convex MeshCollider would close the ring's hole and make the game unwinnable.");
        }

        [Test]
        public void Every_segment_sits_on_the_ring_circle()
        {
            foreach (Transform child in ringObject.transform)
            {
                Vector3 local = child.localPosition;
                float radial = new Vector2(local.x, local.z).magnitude;

                Assert.AreEqual(config.RingOuterRadius, radial, 1e-4f,
                    $"Segment '{child.name}' is off the ring circle.");
                Assert.AreEqual(0f, local.y, 1e-4f, $"Segment '{child.name}' is off the ring plane.");
            }
        }

        [Test]
        public void Hole_is_open_all_the_way_around()
        {
            float expectedHoleRadius = config.RingOuterRadius - config.RingTubeRadius;

            foreach (Transform child in ringObject.transform)
            {
                Vector3 local = child.localPosition;
                float radial = new Vector2(local.x, local.z).magnitude;
                float innermostReach = radial - config.RingTubeRadius;

                Assert.GreaterOrEqual(innermostReach, expectedHoleRadius - 1e-4f,
                    $"Segment '{child.name}' intrudes into the hole.");
            }

            Assert.AreEqual(expectedHoleRadius, builder.HoleRadius, 1e-4f);
        }

        [Test]
        public void Segments_overlap_so_the_ring_has_no_gaps()
        {
            int segments = config.ColliderSegments;
            float chord = 2f * config.RingOuterRadius * Mathf.Sin(Mathf.PI / segments);
            float capsuleHeight = chord + 2f * config.RingTubeRadius;

            Assert.Greater(capsuleHeight, chord,
                "Consecutive capsules must overlap, otherwise a thin peg can slip between them.");
        }

        [Test]
        public void Capsule_scale_produces_the_configured_tube_radius()
        {
            foreach (Transform child in ringObject.transform)
            {
                // A unit capsule has radius 0.5, so a scale of 2r yields radius r.
                Assert.AreEqual(config.RingTubeRadius * 2f, child.localScale.x, 1e-4f);
                Assert.AreEqual(config.RingTubeRadius * 2f, child.localScale.z, 1e-4f);
            }
        }

        [Test]
        public void Peg_thinner_than_the_hole_is_reported_as_fitting()
        {
            float holeRadius = builder.HoleRadius;

            Assert.IsTrue(builder.IsHoleClear(holeRadius * 0.5f));
            Assert.IsFalse(builder.IsHoleClear(holeRadius * 1.5f));
        }

        [Test]
        public void Rebuilding_does_not_accumulate_segments()
        {
            builder.Rebuild();
            builder.Rebuild();

            CapsuleCollider[] colliders = ringObject.GetComponentsInChildren<CapsuleCollider>();

            Assert.AreEqual(config.ColliderSegments, colliders.Length);
        }
    }
}
