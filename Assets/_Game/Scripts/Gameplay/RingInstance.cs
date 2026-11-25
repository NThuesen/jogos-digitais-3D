using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Core;
using UnityEngine;

namespace SlipperyZlope.RingToss.Gameplay
{
    public enum RingState
    {
        /// <summary>Parked in the pool, inactive.</summary>
        Pooled,

        /// <summary>Held at the throw origin, waiting to be aimed.</summary>
        Armed,

        /// <summary>In the air under physics.</summary>
        Flying,

        /// <summary>Landed over a peg and settling there.</summary>
        Captured,

        /// <summary>Failed throw, playing its exit.</summary>
        Retiring
    }

    /// <summary>
    /// One ring as the game sees it: geometry, presentation and lifecycle. The
    /// rigidbody itself belongs to <see cref="RingController"/>; this is the layer
    /// above it that the level and the throw talk to.
    ///
    /// Wind and aim assistance are deliberately not applied here. They are properties
    /// of the level, so the level applies them, and a ring stays something that can be
    /// dropped into a test scene on its own.
    /// </summary>
    [RequireComponent(typeof(RingController))]
    public sealed class RingInstance : MonoBehaviour
    {
        [SerializeField] private PhysicsConfig config;
        [SerializeField] private RingColliderBuilder colliderBuilder;
        [SerializeField] private Transform visual;
        [SerializeField] private MeshRenderer visualRenderer;
        [SerializeField] private TrailRenderer[] trails;
        [SerializeField] private Transform groundShadow;
        [SerializeField] private MeshRenderer groundShadowRenderer;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

        private RingController controller;
        private MaterialPropertyBlock block;
        private JuiceConfig juice;
        private float scaleMultiplier = 1f;
        private Color tint = Color.white;

        // Which emitters the equipped trail style actually uses. Sized once, on the
        // first Bind, so switching styles never allocates mid-game.
        private bool[] activeTrails;

        /// <summary>True once this ring has touched a peg during the current throw.</summary>
        public bool TouchedPeg { get; private set; }

        public RingState State { get; private set; } = RingState.Pooled;

        public PegTarget CapturedBy { get; private set; }

        public RingController Controller => controller;

        public Transform Visual => visual;

        public MeshRenderer VisualRenderer => visualRenderer;

        /// <summary>The rim emitters and the core, in that order. The last one is the core.</summary>
        public TrailRenderer[] Trails => trails;

        public Transform GroundShadow => groundShadow;

        public Color Tint => tint;

        public bool IsFlying => State == RingState.Flying;

        /// <summary>Centre of the ring in world space.</summary>
        public Vector3 Centre => transform.position;

        /// <summary>
        /// The middle of the ring as it is drawn: the centre of its renderer's bounds, the
        /// midpoint across and up of what the player sees. The visual can sit off the
        /// transform — it bobs while it waits — and the aim arc has to leave from the ring
        /// the player is looking at, not from the point its rigidbody hangs on.
        /// </summary>
        public Vector3 VisualCentre =>
            visualRenderer != null ? visualRenderer.bounds.center : transform.position;

        /// <summary>The ring's own axis, the direction the hole points along.</summary>
        public Vector3 Axis => transform.up;

        public float ScaleMultiplier => scaleMultiplier;

        public float HoleRadius =>
            config == null ? 0f : config.RingHoleRadius * scaleMultiplier;

        public float TubeRadius =>
            config == null ? 0f : config.RingTubeRadius * scaleMultiplier;

        private void Awake()
        {
            controller = GetComponent<RingController>();
        }

        /// <summary>
        /// The property block is created on demand rather than in Awake. A pooled ring
        /// spends most of its life inactive, and the director tints and sizes it before
        /// arming it — which is what activates it and runs Awake. Waiting for Awake
        /// would mean the first tint of every throw hit a null block.
        /// </summary>
        private MaterialPropertyBlock Block => block ??= new MaterialPropertyBlock();

        public void Bind(
            PhysicsConfig physicsConfig,
            JuiceConfig juiceConfig,
            RingColliderBuilder builder,
            Transform visualTransform,
            MeshRenderer visualMeshRenderer,
            TrailRenderer[] trailRenderers,
            Transform shadowTransform,
            MeshRenderer shadowRenderer)
        {
            config = physicsConfig;
            juice = juiceConfig;
            colliderBuilder = builder;
            visual = visualTransform;
            visualRenderer = visualMeshRenderer;
            trails = trailRenderers;
            activeTrails = new bool[trailRenderers == null ? 0 : trailRenderers.Length];

            for (int i = 0; i < activeTrails.Length; i++)
            {
                activeTrails[i] = true;
            }

            groundShadow = shadowTransform;
            groundShadowRenderer = shadowRenderer;
        }

        public void SetJuiceConfig(JuiceConfig value)
        {
            juice = value;
        }

        /// <summary>
        /// Ring size for the current level. Scaling the whole ring scales its capsule
        /// colliders with it, so the hole and the collision stay consistent; the peg
        /// tolerance really does shrink.
        /// </summary>
        public void SetScale(float multiplier)
        {
            scaleMultiplier = Mathf.Max(0.05f, multiplier);
            transform.localScale = Vector3.one * scaleMultiplier;
        }

        public void SetTint(Color color)
        {
            tint = color;

            if (visualRenderer == null)
            {
                return;
            }

            visualRenderer.GetPropertyBlock(Block);
            Block.SetColor(BaseColorId, color);

            // A gentle self-lit component so the ring stays readable against a dark
            // sky without needing a second light.
            Block.SetColor(EmissionColorId, color * 0.35f);
            visualRenderer.SetPropertyBlock(Block);

            if (trails == null)
            {
                return;
            }

            for (int i = 0; i < trails.Length; i++)
            {
                // The trail material reads vertex colour, which is what these drive.
                trails[i].startColor = new Color(color.r, color.g, color.b, 0.75f);
                trails[i].endColor = new Color(color.r, color.g, color.b, 0f);
            }
        }

        /// <summary>
        /// Dresses the ring in a purchased skin.
        ///
        /// Everything goes through the property block, texture included, so a ring
        /// wearing the most expensive skin in the shop still shares one material with
        /// the rest of the pool and costs no extra draw call.
        /// </summary>
        /// <param name="readColor">
        /// What the ring reads as from a distance, for the trail and the sparks. Not
        /// the same as the shader colour: a patterned ring's shader colour is white,
        /// because base colour multiplies the map and tinting it would wash the
        /// pattern's two colours into one.
        /// </param>
        public void ApplySkin(
            Color readColor,
            Color shaderColor,
            Texture map,
            Vector2 mapOffset,
            float emission,
            float metallic,
            float smoothness)
        {
            tint = readColor;

            if (visualRenderer == null)
            {
                return;
            }

            visualRenderer.GetPropertyBlock(Block);
            Block.SetColor(BaseColorId, shaderColor);
            Block.SetColor(EmissionColorId, readColor * emission);
            Block.SetFloat(MetallicId, metallic);
            Block.SetFloat(SmoothnessId, smoothness);

            if (map != null)
            {
                Block.SetTexture(BaseMapId, map);
                Block.SetVector(BaseMapStId, new Vector4(1f, 1f, mapOffset.x, mapOffset.y));
            }

            visualRenderer.SetPropertyBlock(Block);
        }

        /// <summary>
        /// Configures the trail from a purchased style.
        ///
        /// A null style leaves the trail exactly as the pool built it, which is what
        /// keeps the default free of any special case.
        /// </summary>
        public void ApplyTrail(TrailStyle style, Color ringColor)
        {
            if (trails == null || trails.Length == 0)
            {
                return;
            }

            if (style == null)
            {
                for (int i = 0; i < trails.Length; i++)
                {
                    trails[i].startColor = new Color(ringColor.r, ringColor.g, ringColor.b, 0.75f);
                    trails[i].endColor = new Color(ringColor.r, ringColor.g, ringColor.b, 0f);
                }

                return;
            }

            Gradient gradient = style.BuildGradient(ringColor);
            int core = trails.Length - 1;

            for (int i = 0; i < trails.Length; i++)
            {
                bool isCore = i == core;
                bool used = isCore ? style.Core : i < style.Emitters;

                activeTrails[i] = used;

                if (!used)
                {
                    trails[i].emitting = false;
                    trails[i].Clear();
                    continue;
                }

                trails[i].time = style.Time;

                // The core runs a little wider than the rim emitters. It is drawing
                // the hole rather than the edge, so at rim width it reads as a fifth
                // stripe instead of as a body behind them — but much wider than this
                // and five trails together cover the pegs the player is aiming at.
                float width = isCore ? style.StartWidth * 1.3f : style.StartWidth;

                trails[i].startWidth = width * scaleMultiplier;
                trails[i].endWidth = style.EndWidth * scaleMultiplier;

                // The gradient carries both colour and alpha, so start/endColor are
                // left alone: setting both makes the TrailRenderer multiply them
                // together and the trail comes out darker than either.
                trails[i].colorGradient = gradient;
            }
        }

        public void SetShadowColor(Color color)
        {
            if (groundShadowRenderer == null)
            {
                return;
            }

            groundShadowRenderer.GetPropertyBlock(Block);
            Block.SetColor(BaseColorId, color);
            groundShadowRenderer.SetPropertyBlock(Block);
        }

        /// <summary>Takes the ring out of the pool and puts it at the throw origin.</summary>
        public void Arm(Vector3 position, Quaternion rotation)
        {
            gameObject.SetActive(true);
            TouchedPeg = false;
            CapturedBy = null;
            State = RingState.Armed;

            transform.localScale = Vector3.one * scaleMultiplier;
            controller.Arm(position, rotation);

            if (visual != null)
            {
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;
            }

            SetTrailEnabled(false);
            SetShadowVisible(false);
        }

        public void Launch(Vector3 velocity, Vector3 angularVelocity)
        {
            State = RingState.Flying;
            controller.Launch(velocity, angularVelocity);
            SetTrailEnabled(true);
            SetShadowVisible(true);
        }

        /// <summary>
        /// Hands the ring over to a peg. The rigidbody stops here; from this moment
        /// the settle is animated, because a physical settle is never as satisfying
        /// twice in a row and cannot be tuned.
        /// </summary>
        public void BeginCapture(PegTarget peg)
        {
            CapturedBy = peg;
            State = RingState.Captured;

            controller.Freeze();
            SetTrailEnabled(false);
            SetShadowVisible(false);
        }

        public void BeginRetire()
        {
            State = RingState.Retiring;
            controller.Freeze();
            SetTrailEnabled(false);
            SetShadowVisible(false);
        }

        public void ReturnToPool(Transform poolParent)
        {
            State = RingState.Pooled;
            CapturedBy = null;
            TouchedPeg = false;

            transform.SetParent(poolParent, false);
            transform.localScale = Vector3.one * scaleMultiplier;

            if (visual != null)
            {
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;
            }

            SetTrailEnabled(false);
            SetShadowVisible(false);
            gameObject.SetActive(false);
        }

        public void SetTrailEnabled(bool value)
        {
            if (trails == null)
            {
                return;
            }

            for (int i = 0; i < trails.Length; i++)
            {
                // A style that does not use an emitter keeps it off even in flight.
                bool on = value && (activeTrails == null || i >= activeTrails.Length || activeTrails[i]);

                if (on)
                {
                    trails[i].Clear();
                }

                trails[i].emitting = on;
            }
        }

        public void SetShadowVisible(bool value)
        {
            if (groundShadow != null)
            {
                groundShadow.gameObject.SetActive(value);
            }
        }

        /// <summary>
        /// Ground shadow follows in LateUpdate so it uses the interpolated transform
        /// the camera will actually see, not the raw physics pose.
        /// </summary>
        private void LateUpdate()
        {
            if (groundShadow == null || !groundShadow.gameObject.activeSelf || juice == null)
            {
                return;
            }

            Vector3 position = transform.position;
            groundShadow.position = new Vector3(position.x, 0.012f, position.z);
            groundShadow.rotation = Quaternion.identity;

            float height = Mathf.Clamp01(position.y / Mathf.Max(0.01f, juice.ShadowFadeHeight));
            float scale = Mathf.Lerp(juice.ShadowMaxScale, juice.ShadowMinScale, height) * scaleMultiplier;
            groundShadow.localScale = new Vector3(scale, 1f, scale);

            if (groundShadowRenderer != null)
            {
                float alpha = Mathf.Lerp(juice.ShadowMaxAlpha, 0f, height);
                groundShadowRenderer.GetPropertyBlock(Block);
                Block.SetColor(BaseColorId, new Color(0f, 0f, 0f, alpha));
                groundShadowRenderer.SetPropertyBlock(Block);
            }
        }

        /// <summary>
        /// Compound colliders report contacts on the rigidbody's GameObject, so every
        /// segment's collision arrives here.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (State != RingState.Flying)
            {
                return;
            }

            PegTarget peg = collision.collider.TryGetComponent(out PegPart part) ? part.Owner : null;

            if (peg != null)
            {
                TouchedPeg = true;
            }

            ContactPoint contact = collision.GetContact(0);

            GameEvents.RaiseImpact(new RingImpact(
                contact.point,
                contact.normal,
                collision.relativeVelocity.magnitude,
                peg,
                this));
        }
    }
}
