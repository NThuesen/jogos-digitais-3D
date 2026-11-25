using System.Collections.Generic;
using SlipperyZlope.RingToss.Config;
using UnityEngine;

namespace SlipperyZlope.RingToss.Gameplay
{
    /// <summary>
    /// Builds the ring's compound collider: a circle of capsule colliders that leaves
    /// the hole genuinely open.
    ///
    /// A convex MeshCollider must never be used for a torus. The convex hull fills the
    /// hole, the peg can never pass through, and the game becomes unwinnable while
    /// looking correct in the editor. That failure is the reason this class exists.
    ///
    /// Each segment is a capsule primitive so the collider and the prototype's visual
    /// are the same object: what you see on screen is exactly what physics sees.
    /// </summary>
    [ExecuteAlways]
    public sealed class RingColliderBuilder : MonoBehaviour
    {
        private const string SegmentPrefix = "Segment_";

        [SerializeField] private PhysicsConfig config;
        [SerializeField] private PhysicsMaterial segmentMaterial;
        [SerializeField] private Material segmentRenderMaterial;

        [Tooltip("Draw the collider capsules. The prototype did, so that physics and " +
                 "what you saw were literally the same object. The finished ring has a " +
                 "torus mesh instead, and 14 extra renderers per ring would cost more " +
                 "draw calls than the whole rest of the scene.")]
        [SerializeField] private bool showSegments;

        public PhysicsConfig Config => config;

        /// <summary>Radius of the opening the peg has to fit through.</summary>
        public float HoleRadius => config == null ? 0f : config.RingOuterRadius - config.RingTubeRadius;

        public void SetConfig(PhysicsConfig value)
        {
            config = value;
        }

        public void SetMaterials(PhysicsMaterial physics, Material render)
        {
            segmentMaterial = physics;
            segmentRenderMaterial = render;
        }

        public void SetShowSegments(bool value)
        {
            showSegments = value;
        }

        public void Rebuild()
        {
            ClearSegments();

            if (config == null)
            {
                Debug.LogWarning($"{nameof(RingColliderBuilder)} on '{name}' has no PhysicsConfig assigned.", this);
                return;
            }

            int segments = Mathf.Max(3, config.ColliderSegments);
            float outerRadius = config.RingOuterRadius;
            float tubeRadius = config.RingTubeRadius;

            // Chord between neighbouring segment centres. Adding a full tube diameter on
            // top makes consecutive capsules overlap, so the ring has no gaps for a thin
            // fast-moving peg to slip through.
            float chord = 2f * outerRadius * Mathf.Sin(Mathf.PI / segments);
            float capsuleHeight = chord + 2f * tubeRadius;

            for (int i = 0; i < segments; i++)
            {
                float angle = 2f * Mathf.PI * i / segments;
                Vector3 radialOutward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));

                GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                segment.name = SegmentPrefix + i.ToString("D2");
                segment.transform.SetParent(transform, false);
                segment.transform.localPosition = radialOutward * outerRadius;

                // The capsule primitive's mesh and collider both run along local Y, so
                // aligning local Y with the tangent aligns both at once.
                segment.transform.localRotation = Quaternion.LookRotation(radialOutward, tangent);

                // A unit capsule is radius 0.5 and total height 2, so these scales give
                // exactly tubeRadius and capsuleHeight.
                segment.transform.localScale = new Vector3(
                    tubeRadius * 2f,
                    capsuleHeight * 0.5f,
                    tubeRadius * 2f);

                CapsuleCollider collider = segment.GetComponent<CapsuleCollider>();
                collider.material = segmentMaterial;

                MeshRenderer renderer = segment.GetComponent<MeshRenderer>();

                if (segmentRenderMaterial != null)
                {
                    renderer.sharedMaterial = segmentRenderMaterial;
                }

                // Disabled rather than destroyed: DestroyImmediate on a component is
                // discouraged at runtime, and a disabled renderer costs nothing to draw.
                renderer.enabled = showSegments;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        public void ClearSegments()
        {
            List<GameObject> doomed = new List<GameObject>();

            foreach (Transform child in transform)
            {
                if (child.name.StartsWith(SegmentPrefix))
                {
                    doomed.Add(child.gameObject);
                }
            }

            foreach (GameObject segment in doomed)
            {
                if (Application.isPlaying)
                {
                    Destroy(segment);
                }
                else
                {
                    DestroyImmediate(segment);
                }
            }
        }

        /// <summary>
        /// True when nothing blocks a peg of the given radius from passing through the
        /// ring along its axis. This is the property that a convex hull would silently
        /// destroy, so it is worth being able to assert on.
        /// </summary>
        public bool IsHoleClear(float pegRadius)
        {
            if (config == null)
            {
                return false;
            }

            return pegRadius < HoleRadius;
        }
    }
}
