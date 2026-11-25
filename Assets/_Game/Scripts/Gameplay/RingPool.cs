using System.Collections.Generic;
using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Utils;
using UnityEngine;

namespace SlipperyZlope.RingToss.Gameplay
{
    /// <summary>
    /// Builds every ring the game will ever need at load, and hands them out during
    /// play.
    ///
    /// Nothing is instantiated once gameplay starts. A ring is a rigidbody with
    /// fourteen colliders, a torus mesh and a trail; creating one mid-throw is exactly
    /// the kind of hitch this project exists to avoid.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public sealed class RingPool : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private PhysicsConfig physicsConfig;
        [SerializeField] private JuiceConfig juiceConfig;

        [Tooltip("Rings built at load. Has to cover the largest level plus the ones " +
                 "still sitting on claimed pegs.")]
        [Range(1, 32)]
        [SerializeField] private int capacity = 14;

        [Header("Assets")]
        [SerializeField] private Mesh ringMesh;
        [SerializeField] private Material ringMaterial;
        [SerializeField] private Material trailMaterial;
        [SerializeField] private Mesh shadowMesh;
        [SerializeField] private Material shadowMaterial;
        [SerializeField] private PhysicsMaterial ringPhysicsMaterial;

        [Header("Scene")]
        [SerializeField] private Transform ringContainer;
        [SerializeField] private Transform shadowContainer;

        private readonly List<RingInstance> all = new List<RingInstance>();
        private readonly Stack<RingInstance> available = new Stack<RingInstance>();

        public int Capacity => capacity;

        public IReadOnlyList<RingInstance> All => all;

        private void Awake()
        {
            if (ringContainer == null)
            {
                ringContainer = transform;
            }

            if (shadowContainer == null)
            {
                shadowContainer = transform;
            }

            Build();
        }

        private void Build()
        {
            for (int i = 0; i < capacity; i++)
            {
                RingInstance ring = CreateRing(i);
                all.Add(ring);
                ring.ReturnToPool(ringContainer);
                available.Push(ring);
            }
        }

        private RingInstance CreateRing(int index)
        {
            GameObject root = new GameObject($"Ring_{index:D2}");
            root.transform.SetParent(ringContainer, false);

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;

            RingColliderBuilder builder = root.AddComponent<RingColliderBuilder>();
            builder.SetConfig(physicsConfig);
            builder.SetMaterials(ringPhysicsMaterial, null);
            builder.SetShowSegments(false);
            builder.Rebuild();

            RingController controller = root.AddComponent<RingController>();
            controller.SetConfig(physicsConfig);

            GameObject visual = GeometryFactory.CreateMeshObject(
                "Visual", root.transform, ringMesh, ringMaterial, castShadows: true);

            TrailRenderer[] trails = CreateTrails(visual.transform);

            GameObject shadow = GeometryFactory.CreateMeshObject(
                "Shadow", shadowContainer, shadowMesh, shadowMaterial);
            shadow.SetActive(false);

            RingInstance ring = root.AddComponent<RingInstance>();
            ring.Bind(
                physicsConfig,
                juiceConfig,
                builder,
                visual.transform,
                visual.GetComponent<MeshRenderer>(),
                trails,
                shadow.transform,
                shadow.GetComponent<MeshRenderer>());

            return ring;
        }

        /// <summary>
        /// Builds the ring's trail emitters: four spaced around the rim and one at the
        /// centre.
        ///
        /// One emitter was not enough. A single point on the rim draws a thin ribbon
        /// that reads as a scratch in one corner of the ring rather than as the ring
        /// travelling, and no amount of width or colour fixes that — the problem is
        /// that only one part of the object leaves a mark. Four points braid into a
        /// band the width of the whole ring, and the centre one gives a style the
        /// option of a comet core behind the hole.
        ///
        /// All five exist on every ring and a trail style decides how many are used, so
        /// switching styles never allocates. Only one ring is in flight at a time, so
        /// this is at most five active trail renderers.
        /// </summary>
        private TrailRenderer[] CreateTrails(Transform parent)
        {
            float radius = physicsConfig == null ? 0.32f : physicsConfig.RingOuterRadius;

            Vector3[] offsets =
            {
                new Vector3(radius, 0f, 0f),
                new Vector3(0f, 0f, radius),
                new Vector3(-radius, 0f, 0f),
                new Vector3(0f, 0f, -radius),
                Vector3.zero
            };

            TrailRenderer[] trails = new TrailRenderer[offsets.Length];

            for (int i = 0; i < offsets.Length; i++)
            {
                trails[i] = CreateTrail(parent, i == offsets.Length - 1 ? "Trail Core" : $"Trail_{i}",
                    offsets[i]);
            }

            return trails;
        }

        private TrailRenderer CreateTrail(Transform parent, string name, Vector3 offset)
        {
            GameObject trailObject = new GameObject(name);
            trailObject.transform.SetParent(parent, false);
            trailObject.transform.localPosition = offset;

            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMaterial;
            trail.time = juiceConfig == null ? 0.32f : juiceConfig.TrailTime;
            trail.startWidth = juiceConfig == null ? 0.13f : juiceConfig.TrailStartWidth;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.02f;
            trail.numCapVertices = 3;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            trail.autodestruct = false;

            return trail;
        }

        /// <summary>
        /// Hands out a ring. Returns null when the pool is empty rather than growing:
        /// running out means a level asked for more rings than the pool was sized for,
        /// which is a configuration error worth seeing rather than hiding.
        /// </summary>
        public RingInstance Take()
        {
            if (available.Count == 0)
            {
                Debug.LogError($"[RingPool] Exhausted at capacity {capacity}.", this);
                return null;
            }

            return available.Pop();
        }

        public void Release(RingInstance ring)
        {
            if (ring == null || ring.State == RingState.Pooled)
            {
                return;
            }

            ring.ReturnToPool(ringContainer);
            available.Push(ring);
        }

        /// <summary>Returns every ring, including ones resting on claimed pegs.</summary>
        public void ReleaseAll()
        {
            available.Clear();

            for (int i = 0; i < all.Count; i++)
            {
                all[i].ReturnToPool(ringContainer);
                available.Push(all[i]);
            }
        }

        public void Configure(
            PhysicsConfig physics,
            JuiceConfig juice,
            Mesh mesh,
            Material material,
            Material trail,
            Mesh shadow,
            Material shadowMat,
            PhysicsMaterial physicsMaterial,
            Transform rings,
            Transform shadows,
            int poolCapacity)
        {
            physicsConfig = physics;
            juiceConfig = juice;
            ringMesh = mesh;
            ringMaterial = material;
            trailMaterial = trail;
            shadowMesh = shadow;
            shadowMaterial = shadowMat;
            ringPhysicsMaterial = physicsMaterial;
            ringContainer = rings;
            shadowContainer = shadows;
            capacity = poolCapacity;
        }
    }
}
