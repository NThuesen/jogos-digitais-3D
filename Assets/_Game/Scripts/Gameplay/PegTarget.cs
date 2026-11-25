using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Juice;
using UnityEngine;

namespace SlipperyZlope.RingToss.Gameplay
{
    /// <summary>
    /// A single peg on the board: its geometry, its motion, and the test that decides
    /// whether a ring has been captured.
    ///
    /// The capture test is geometric rather than trigger based. A trigger cannot tell
    /// the difference between a ring resting against the peg and a ring that actually
    /// went over it, and getting that wrong is the difference between a game that
    /// feels fair and one that feels arbitrary.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class PegTarget : MonoBehaviour
    {
        [SerializeField] private PhysicsConfig config;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform capTransform;
        [SerializeField] private Transform groundGlow;
        [SerializeField] private Transform plinth;

        [Tooltip("How far below the peg top the ring has to descend before the catch " +
                 "is called. Enough that the peg is unambiguously through the hole.")]
        [SerializeField] private float captureDepth = 0.16f;

        [Tooltip("How long after descending past the peg top the ring may still be " +
                 "judged to have landed on it. Covers the ring that clips the rim and " +
                 "then drops into place, which is what most real catches look like.")]
        [SerializeField] private float captureGrace = 0.35f;

        [Tooltip("Fraction of the ring's hole radius the ring centre has to be within, " +
                 "measured from the peg axis. Below 1 so a ring hanging on the rim " +
                 "does not count.")]
        [Range(0.5f, 1f)]
        [SerializeField] private float captureTolerance = 0.92f;

        [Tooltip("How flat the ring has to be lying for the catch to count. This is " +
                 "the dot product between the ring's axis and world up.")]
        [Range(0f, 1f)]
        [SerializeField] private float captureFlatness = 0.5f;

        [Tooltip("How far out of the board a retracting peg has to be before a catch " +
                 "counts. A peg that is still sinking is not a target.")]
        [Range(0f, 1f)]
        [SerializeField] private float minCapturePresence = 0.6f;

        private CapsuleCollider body;
        private PegPlacement placement;
        private Vector3 anchorPosition;
        private float elapsed;
        private float presence = 1f;
        private float previousRingHeight;
        private bool hasPreviousRingHeight;

        /// <summary>Seconds since the ring descended past this peg's top, or -1.</summary>
        private float descendedFor = -1f;

        /// <summary>How far the ground glow floats above the board, to avoid z-fighting.</summary>
        private const float GlowLift = 0.008f;

        public bool IsClaimed { get; private set; }

        public int Points => placement.points;

        public PegPlacement Placement => placement;

        /// <summary>World position of the top of the peg, as it stands right now.</summary>
        public Vector3 TopPosition => transform.position + Vector3.up * (BaseHeight + CurrentHeight);

        /// <summary>The peg's height when fully standing, measured from its own base.</summary>
        public float Height { get; private set; }

        /// <summary>Metres the peg's base is raised off the board.</summary>
        public float BaseHeight { get; private set; }

        /// <summary>How far out of the board the peg is: 1 standing, 0 fully sunk.</summary>
        public float Presence => presence;

        /// <summary>Height of the part of the peg that is currently above the board.</summary>
        public float CurrentHeight => Height * presence;

        /// <summary>True while the peg is up far enough to be worth aiming at.</summary>
        public bool IsAvailable => !IsClaimed && presence >= minCapturePresence;

        public float Radius { get; private set; }

        public Transform VisualRoot => visualRoot;

        public Transform CapTransform => capTransform;

        public Transform GroundGlow => groundGlow;

        /// <summary>The peg's reactions. Null in a bare physics test scene.</summary>
        public PegPresenter Presenter { get; private set; }

        public void SetPresenter(PegPresenter value)
        {
            Presenter = value;
        }

        private void Awake()
        {
            body = GetComponent<CapsuleCollider>();
        }

        public void Bind(
            PhysicsConfig physicsConfig, Transform visual, Transform cap, Transform glow, Transform plinthRoot)
        {
            config = physicsConfig;
            visualRoot = visual;
            capTransform = cap;
            groundGlow = glow;
            plinth = plinthRoot;
        }

        /// <summary>Applies a level's placement. Also resets the claimed state.</summary>
        public void Apply(PegPlacement value, Vector3 boardOrigin)
        {
            if (body == null)
            {
                body = GetComponent<CapsuleCollider>();
            }

            placement = value;
            IsClaimed = false;
            elapsed = 0f;
            presence = value.PresenceAt(0f);
            hasPreviousRingHeight = false;

            float heightScale = value.heightScale <= 0f ? 1f : value.heightScale;
            float radiusScale = value.radiusScale <= 0f ? 1f : value.radiusScale;

            Height = (config == null ? 0.92f : config.PegHeight) * heightScale;
            Radius = (config == null ? 0.09f : config.PegRadius) * radiusScale;
            BaseHeight = Mathf.Max(0f, value.baseHeight);

            anchorPosition = boardOrigin + new Vector3(value.anchor.x, 0f, value.anchor.y);
            transform.position = anchorPosition;

            // The collider is sized directly rather than through transform scale so the
            // capsule stays a capsule: a non-uniform scale on the root would also skew
            // every child and the ring's contacts with it.
            body.direction = 1;
            body.radius = Radius;

            if (visualRoot != null)
            {
                visualRoot.localScale = new Vector3(radiusScale, heightScale, radiusScale);
            }

            ApplyPlinth();
            ApplyPresence();
            PinGlowToBoard();
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Advances the peg's motion. Driven by the level rather than by Update so
        /// every peg in a level shares one clock and a paused level really pauses.
        /// </summary>
        public void Tick(float deltaTime)
        {
            elapsed += deltaTime;

            if (placement.motion != PegMotion.Static && placement.motionAmplitude > 0f)
            {
                transform.position = anchorPosition + placement.OffsetAt(elapsed);
                PinGlowToBoard();
            }

            if (!placement.Retracts)
            {
                return;
            }

            float next = placement.PresenceAt(elapsed);

            if (!Mathf.Approximately(next, presence))
            {
                presence = next;
                ApplyPresence();
            }
        }

        /// <summary>
        /// Sinks the peg into the board by however much it has retracted.
        ///
        /// The collider follows the visual rather than being switched off wholesale:
        /// a peg that is half down is half a peg, and a ring should be able to clip
        /// the stub of it. Bouncing off geometry that is not drawn is the one outcome
        /// that has to be impossible here.
        /// </summary>
        private void ApplyPresence()
        {
            if (body == null)
            {
                return;
            }

            float height = Mathf.Max(0.001f, CurrentHeight);

            body.height = height;
            body.center = new Vector3(0f, BaseHeight + height * 0.5f, 0f);
            body.enabled = presence > 0.02f;

            if (visualRoot != null)
            {
                visualRoot.localPosition = new Vector3(0f, BaseHeight - Height * (1f - presence), 0f);
            }
        }

        /// <summary>
        /// Holds the ground glow on the board however the peg moves.
        ///
        /// The glow is a mark on the floor that says where this peg stands. It is a
        /// child of the peg root, which is what makes it follow a peg sliding across
        /// the board — correct, and the reason it was parented there. A bobbing peg
        /// moves that same root *up*, and the glow rode with it: a disc of light
        /// hanging in mid-air halfway up the screen, tracking a peg it is supposed to
        /// be lying underneath.
        ///
        /// Only the height is pinned. Sideways the glow still follows, because
        /// sideways it is still telling the truth.
        /// </summary>
        private void PinGlowToBoard()
        {
            if (groundGlow == null)
            {
                return;
            }

            Vector3 local = groundGlow.localPosition;
            local.y = GlowLift - (transform.position.y - anchorPosition.y);
            groundGlow.localPosition = local;
        }

        /// <summary>
        /// Draws the plinth a raised peg stands on. Without it the peg floats, and the
        /// player has no way to see why the throw that works everywhere else does not
        /// work here.
        /// </summary>
        private void ApplyPlinth()
        {
            if (plinth == null)
            {
                return;
            }

            bool raised = BaseHeight > 0.001f;
            plinth.gameObject.SetActive(raised);

            if (!raised)
            {
                return;
            }

            // The primitive cylinder is two units tall, so the scale halves into the
            // real height and doubles into the real radius.
            plinth.localPosition = new Vector3(0f, BaseHeight * 0.5f, 0f);
            plinth.localScale = new Vector3(Radius * 4.4f, BaseHeight * 0.5f, Radius * 4.4f);
        }

        /// <summary>
        /// True on the frame the ring is judged to have gone over this peg.
        ///
        /// Three things have to hold at once: the ring's centre has descended past the
        /// peg top, it is inside the hole's radius of the peg axis, and it is lying
        /// flat enough that it is around the peg rather than leaning on it.
        /// </summary>
        public bool TryCapture(RingInstance ring)
        {
            if (IsClaimed || ring == null || !ring.IsFlying)
            {
                return false;
            }

            // A peg on its way out of the board is not a target. Without this a ring
            // can be credited to a peg that is no longer drawn, which is the single
            // most unfair thing a retracting peg could do.
            if (presence < minCapturePresence)
            {
                return false;
            }

            // A peg thicker than the ring's hole physically cannot pass through it.
            // Until now that was left entirely to the collider, which is true but
            // implicit: the rule read as though a fat peg could be ringed and only
            // physics stopped it. Stating it here makes the tolerance a property of
            // the rule rather than an emergent one.
            if (Radius >= ring.HoleRadius)
            {
                return false;
            }

            Vector3 ringCentre = ring.Centre;
            float pegTopHeight = transform.position.y + BaseHeight + CurrentHeight;
            float currentHeight = ringCentre.y;

            if (!hasPreviousRingHeight)
            {
                previousRingHeight = currentHeight;
                hasPreviousRingHeight = true;
                return false;
            }

            float threshold = pegTopHeight - captureDepth;
            bool descendedPast = previousRingHeight > threshold && currentHeight <= threshold;
            previousRingHeight = currentHeight;

            // The descent opens a window rather than being the whole test.
            //
            // It used to be the whole test: the three conditions below were asked on
            // the single frame the ring's centre crossed the peg top, and a ring that
            // arrived a few centimetres out and then dropped into place answered "no"
            // on the only frame it was ever asked. It then sat there, around the peg,
            // scored as a miss — which is precisely what a player reported as "I hit
            // it and it didn't count", with the evidence still on screen.
            //
            // That was never the rule anybody wanted. The descent is what makes a
            // catch a catch rather than a ring lying on the floor; whether the peg is
            // through the hole is a question about where the ring *ends up*. So the
            // crossing latches, and the geometry is re-asked every step until the ring
            // settles, climbs back out, or the window closes.
            //
            // The window does not loosen what counts. A ring beside the peg fails the
            // offset test for every step of it, and one leaning on the peg fails the
            // flatness test — both are still misses, and both have tests saying so.
            if (descendedPast)
            {
                descendedFor = 0f;
            }
            else if (descendedFor >= 0f)
            {
                descendedFor += Time.fixedDeltaTime;

                // Climbed back out, or took too long. Either way it is not this
                // descent's catch any more; a genuine second attempt re-latches on
                // its own when it crosses again.
                if (currentHeight > threshold || descendedFor > captureGrace)
                {
                    descendedFor = -1f;
                }
            }

            if (descendedFor < 0f)
            {
                return false;
            }

            Vector3 axisOffset = ringCentre - transform.position;
            axisOffset.y = 0f;

            float allowed = Mathf.Max(0f, ring.HoleRadius * captureTolerance - Radius * 0.35f);

            if (axisOffset.sqrMagnitude > allowed * allowed)
            {
                return false;
            }

            float flatness = Mathf.Abs(Vector3.Dot(ring.Axis, Vector3.up));

            return flatness >= captureFlatness;
        }

        /// <summary>Called once a capture has been accepted.</summary>
        public void MarkClaimed()
        {
            IsClaimed = true;
        }

        public void ResetForThrow()
        {
            hasPreviousRingHeight = false;
            descendedFor = -1f;
        }

        /// <summary>
        /// Puts a retracting peg back at the top of its cycle. Used when a round
        /// starts, so the player never opens on a board of sunk pegs.
        /// </summary>
        public void ResetCycle()
        {
            elapsed = 0f;
            presence = placement.PresenceAt(0f);
            ApplyPresence();
        }

        /// <summary>
        /// Height a captured ring should come to rest at, in world space. Sits just
        /// above the peg's base collar so the ring reads as resting on the board.
        /// </summary>
        public Vector3 RestPositionFor(RingInstance ring)
        {
            float restHeight = ring == null ? 0.05f : ring.TubeRadius + 0.02f;
            return transform.position + Vector3.up * (BaseHeight + restHeight);
        }
    }
}
