using UnityEngine;

namespace SlipperyZlope.RingToss.Config
{
    /// <summary>
    /// Every tunable value behind the throw. Nothing in the gameplay code is allowed
    /// to hardcode any of this: the whole point is that the throw can be re-tuned
    /// from the inspector without a recompile.
    /// </summary>
    [CreateAssetMenu(menuName = "SlipperyZlope/Physics Config", fileName = "PhysicsConfig")]
    public sealed class PhysicsConfig : ScriptableObject
    {
        [Header("Ring shape")]
        [Tooltip("Distance from the ring centre to the centre of its tube.")]
        [SerializeField] private float ringOuterRadius = 0.32f;

        [Tooltip("Thickness of the ring's tube. Also the radius of each collider capsule.")]
        [SerializeField] private float ringTubeRadius = 0.05f;

        [Tooltip("How many capsule colliders form the ring. A convex MeshCollider cannot " +
                 "be used here: its hull closes the hole and the peg could never enter.")]
        [Range(8, 24)]
        [SerializeField] private int colliderSegments = 14;

        [Header("Rigidbody")]
        [SerializeField] private float mass = 0.12f;
        [SerializeField] private float linearDamping = 0.05f;
        [SerializeField] private float angularDamping = 0.35f;

        [Header("Gravity")]
        [Tooltip("Realistic gravity reads as floaty in a throwing game. Values above 1 " +
                 "make the arc snappier and the ring feel heavier.")]
        [Range(1f, 4f)]
        [SerializeField] private float gravityMultiplier = 1.9f;

        [Header("Input mapping")]
        [Tooltip("Forward drag, as a fraction of screen height, that maps to the longest " +
                 "throw. Only the component along the aim axis counts: see the note on " +
                 "lateralDragScreenFraction.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float maxDragScreenFraction = 0.30f;

        [Tooltip("Sideways drag, as a fraction of screen height, that maps to full yaw.\n\n" +
                 "This is a separate axis from the forward drag on purpose. Deriving power " +
                 "from the drag's total length instead couples aim to power: swiping wide " +
                 "enough to reach an off-centre peg also throws at full strength, which " +
                 "puts every near peg that is not dead ahead out of reach entirely.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float lateralDragScreenFraction = 0.18f;

        [Tooltip("Below this fraction of screen height the release is ignored, so a " +
                 "stray tap never throws.")]
        [Range(0.001f, 0.1f)]
        [SerializeField] private float deadzoneScreenFraction = 0.016f;

        [Tooltip("If the finger comes back inside this fraction of screen height from " +
                 "where it started, the throw is cancelled instead of released.")]
        [Range(0.001f, 0.1f)]
        [SerializeField] private float cancelScreenFraction = 0.012f;

        [Tooltip("Swipe direction is throw direction. Enable to invert into a slingshot: " +
                 "drag back and down, release, the ring goes the opposite way.")]
        [SerializeField] private bool invertDragDirection;

        [Header("Throw range")]
        [Tooltip("Ground distance the weakest throw lands at, measured from the throw " +
                 "origin. Has to be shorter than the nearest peg any level places or " +
                 "that peg cannot be reached at all.")]
        [SerializeField] private float minThrowRange = 1.1f;

        [Tooltip("Ground distance the strongest throw lands at.")]
        [SerializeField] private float maxThrowRange = 6.4f;

        [Tooltip("Maps normalised drag length (0..1) to the 0..1 blend between the " +
                 "shortest and longest throw. Linear by default, because a linear map is " +
                 "the one the player can learn: half the drag lands halfway across the " +
                 "board.")]
        [SerializeField] private AnimationCurve dragToRangeCurve =
            AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Throw arc")]
        [Tooltip("Launch pitch used for the shortest throw. Steep, so a short throw is a " +
                 "lob that drops rather than a weak flat one that never gets there.")]
        [Range(10f, 80f)]
        [SerializeField] private float pitchAtMinRange = 56f;

        [Tooltip("Launch pitch used for the longest throw. Still steep enough that the " +
                 "ring arrives descending and can drop over a peg instead of clipping " +
                 "its side.")]
        [Range(10f, 80f)]
        [SerializeField] private float pitchAtMaxRange = 42f;

        [Tooltip("Safety clamps on the solved speed. A degenerate solve is pinned here " +
                 "rather than allowed to produce an absurd throw.")]
        [SerializeField] private float minThrowSpeed = 3f;

        [SerializeField] private float maxThrowSpeed = 13f;

        [Tooltip("Maximum yaw the player can apply, to keep wild swipes playable. Also " +
                 "fixes how wide the board can be: the widest legal throw has to still " +
                 "reach the outermost peg column.")]
        [Range(0f, 90f)]
        [SerializeField] private float maxYawDegrees = 20f;

        [Header("Spin")]
        [Tooltip("Spin around the ring's own axis, through the hole. This is the " +
                 "frisbee-like rotation and it carries most of the visual satisfaction.")]
        [SerializeField] private float spinAtMinForce = 6f;
        [SerializeField] private float spinAtMaxForce = 20f;

        [Tooltip("Small rotation across the other two axes so the flight never looks " +
                 "mechanically perfect.")]
        [SerializeField] private float tumbleMagnitude = 1.4f;

        [Header("Peg geometry")]
        [Tooltip("Peg radius at a level's default thickness. The ring's hole has to " +
                 "stay comfortably wider than this or the game becomes unwinnable.")]
        [SerializeField] private float pegRadius = 0.09f;

        [SerializeField] private float pegHeight = 0.92f;

        [Header("Flight resolution")]
        [Tooltip("Speed below which the ring counts as stopped.")]
        [SerializeField] private float restLinearThreshold = 0.14f;

        [Tooltip("Angular speed below which the ring counts as stopped.")]
        [SerializeField] private float restAngularThreshold = 0.7f;

        [Tooltip("Grace period after launch before the ring is allowed to count as stopped.")]
        [SerializeField] private float minimumFlightTime = 0.3f;

        [Tooltip("Hard cap so a ring resting against geometry can never stall the loop.")]
        [SerializeField] private float maximumFlightTime = 5f;

        [Tooltip("Below this height the ring has fallen out of the world and the throw " +
                 "is resolved immediately rather than waiting for it to stop.")]
        [SerializeField] private float outOfBoundsHeight = -3f;

        public float RingOuterRadius => ringOuterRadius;
        public float RingTubeRadius => ringTubeRadius;
        public int ColliderSegments => colliderSegments;

        public float Mass => mass;
        public float LinearDamping => linearDamping;
        public float AngularDamping => angularDamping;

        public float GravityMultiplier => gravityMultiplier;

        public float MaxDragScreenFraction => maxDragScreenFraction;
        public float LateralDragScreenFraction => lateralDragScreenFraction;
        public float DeadzoneScreenFraction => deadzoneScreenFraction;
        public float CancelScreenFraction => cancelScreenFraction;
        public bool InvertDragDirection => invertDragDirection;

        public float MinThrowRange => minThrowRange;
        public float MaxThrowRange => maxThrowRange;
        public AnimationCurve DragToRangeCurve => dragToRangeCurve;

        public float PitchAtMinRange => pitchAtMinRange;
        public float PitchAtMaxRange => pitchAtMaxRange;
        public float MinThrowSpeed => minThrowSpeed;
        public float MaxThrowSpeed => maxThrowSpeed;
        public float MaxYawDegrees => maxYawDegrees;

        public float SpinAtMinForce => spinAtMinForce;
        public float SpinAtMaxForce => spinAtMaxForce;
        public float TumbleMagnitude => tumbleMagnitude;

        public float PegRadius => pegRadius;
        public float PegHeight => pegHeight;

        public float RestLinearThreshold => restLinearThreshold;
        public float RestAngularThreshold => restAngularThreshold;
        public float MinimumFlightTime => minimumFlightTime;
        public float MaximumFlightTime => maximumFlightTime;
        public float OutOfBoundsHeight => outOfBoundsHeight;

        /// <summary>Radius of the opening the peg has to fit through, at ring scale 1.</summary>
        public float RingHoleRadius => ringOuterRadius - ringTubeRadius;

        /// <summary>
        /// Height the throw aims to arrive at. Derived from the peg rather than stored,
        /// so a peg height change cannot leave the aim pointing at the old one.
        /// </summary>
        public float LandingHeight => pegHeight;

        /// <summary>Custom gravity vector actually applied to the ring in flight.</summary>
        public Vector3 Gravity => Physics.gravity * gravityMultiplier;

        /// <summary>Magnitude of that gravity, which is what the ballistic solve needs.</summary>
        public float GravityMagnitude => Mathf.Abs(Physics.gravity.y) * gravityMultiplier;
    }
}
