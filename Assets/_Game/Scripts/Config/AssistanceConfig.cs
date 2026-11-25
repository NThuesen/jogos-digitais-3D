using UnityEngine;

namespace SlipperyZlope.RingToss.Config
{
    /// <summary>
    /// Aim assistance. Ships disabled: tuning the physics while magnetism is on tunes
    /// the assist rather than the throw, and leaves a bad foundation permanently.
    ///
    /// The assist is a gentle steering acceleration applied only while the ring is
    /// descending towards a peg it is already close to. It never overrides the
    /// player's throw, so a badly aimed throw still misses.
    /// </summary>
    [CreateAssetMenu(menuName = "SlipperyZlope/Assistance Config", fileName = "AssistanceConfig")]
    public sealed class AssistanceConfig : ScriptableObject
    {
        [Tooltip("Off until the raw throw has been judged by a person. Tuning the " +
                 "physics with magnetism on tunes the assist, not the throw.")]
        [SerializeField] private bool enabled;

        [Header("Magnetism")]
        [Tooltip("Horizontal distance from the peg axis inside which the ring starts " +
                 "being steered. Outside this the throw is untouched.")]
        [SerializeField] private float radius = 0.42f;

        [Tooltip("Steering acceleration at the centre of the radius, in m/s^2. Kept " +
                 "low on purpose: assistance the player can feel reads as the game " +
                 "playing itself.")]
        [SerializeField] private float strength = 5.5f;

        [Tooltip("Assist only applies below this height above the peg top, so it can " +
                 "never bend a throw that was going to sail over.")]
        [SerializeField] private float activationHeight = 1.3f;

        [Tooltip("Assist is off while the ring is still rising. It only helps a ring " +
                 "that is already coming down onto the peg.")]
        [SerializeField] private bool descendingOnly = true;

        [Tooltip("Falloff from the centre of the radius to its edge.")]
        [SerializeField] private AnimationCurve falloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("Anti-frustration")]
        [Tooltip("Consecutive misses after which the assist radius starts widening, " +
                 "so a stuck player is quietly helped rather than walled.")]
        [SerializeField] private int missesBeforeHelp = 3;

        [Tooltip("Extra radius granted per miss beyond the threshold.")]
        [SerializeField] private float radiusPerMiss = 0.06f;

        [Tooltip("Ceiling on the widened radius.")]
        [SerializeField] private float maxRadius = 0.68f;

        public bool Enabled => enabled;
        public float Radius => radius;
        public float Strength => strength;
        public float ActivationHeight => activationHeight;
        public bool DescendingOnly => descendingOnly;

        /// <summary>Assist radius after the anti-frustration allowance for a miss streak.</summary>
        public float RadiusForMissStreak(int consecutiveMisses)
        {
            int excess = Mathf.Max(0, consecutiveMisses - missesBeforeHelp);
            return Mathf.Min(radius + excess * radiusPerMiss, maxRadius);
        }

        /// <summary>
        /// Steering acceleration for a ring at <paramref name="horizontalDistance"/>
        /// from the peg axis. Zero outside the radius so there is no discontinuity at
        /// the boundary.
        /// </summary>
        public float StrengthAtDistance(float horizontalDistance, float effectiveRadius)
        {
            if (!enabled || horizontalDistance >= effectiveRadius || effectiveRadius <= 0f)
            {
                return 0f;
            }

            float normalised = Mathf.Clamp01(horizontalDistance / effectiveRadius);
            return strength * Mathf.Max(0f, falloff.Evaluate(normalised));
        }
    }
}
