using UnityEngine;

namespace SlipperyZlope.RingToss.Config
{
    /// <summary>
    /// Every duration, curve and magnitude in the feedback layer.
    ///
    /// Nothing in Scripts/Juice is allowed to hardcode a number: the whole project
    /// exists to make the feel tunable, and a feel that can only be changed by
    /// recompiling never actually gets tuned.
    /// </summary>
    [CreateAssetMenu(menuName = "SlipperyZlope/Juice Config", fileName = "JuiceConfig")]
    public sealed class JuiceConfig : ScriptableObject
    {
        [Header("Idle")]
        [Tooltip("How much the waiting ring breathes, as a fraction of its scale.")]
        [SerializeField] private float idleBreathAmount = 0.045f;

        [SerializeField] private float idleBreathDuration = 1.6f;

        [Tooltip("Vertical bob of the waiting ring, in metres.")]
        [SerializeField] private float idleBobAmount = 0.035f;

        [Tooltip("Slow spin of the waiting ring, degrees per second.")]
        [SerializeField] private float idleSpinSpeed = 22f;

        [Header("Arm")]
        [Tooltip("Duration of the pop the ring plays when a new one is handed over.")]
        [SerializeField] private float armPopDuration = 0.34f;

        [SerializeField] private float armPopOvershoot = 1.14f;

        [Header("Aim")]
        [Tooltip("Field of view change while dragging, as a fraction. The spec asks " +
                 "for a subtle 2-4% zoom out.")]
        [Range(0f, 0.15f)]
        [SerializeField] private float aimZoomAmount = 0.035f;

        [SerializeField] private float aimZoomDuration = 0.22f;

        [Tooltip("Roll applied across the same sideways drag, in degrees.")]
        [SerializeField] private float aimRollDegrees = 16f;

        [Tooltip("How far the ring leans into the throw while it is being aimed, in " +
                 "degrees. Rotation only: the ring never moves off the launch point, " +
                 "because the dotted arc starts there and has to start at its centre.")]
        [SerializeField] private float aimTiltDegrees = 18f;

        [Tooltip("Smoothing on the aim pose so the ring resists the drag instead of " +
                 "snapping to it.")]
        [SerializeField] private float aimDamping = 20f;

        [Header("Trajectory preview")]
        [SerializeField] private float previewDotSpeed = 2.4f;
        [SerializeField] private float previewPulseAmount = 0.35f;
        [SerializeField] private float previewFadeDuration = 0.12f;

        [Header("Flight")]
        [SerializeField] private float trailTime = 0.32f;
        [SerializeField] private float trailStartWidth = 0.13f;

        [Tooltip("Height at which the ground shadow has shrunk to its smallest.")]
        [SerializeField] private float shadowFadeHeight = 3.2f;

        [SerializeField] private float shadowMaxScale = 1.05f;
        [SerializeField] private float shadowMinScale = 0.42f;
        [SerializeField] private float shadowMaxAlpha = 0.42f;

        [Header("Catch — hit stop")]
        [Tooltip("The freeze on the moment of the catch. The spec asks for 50-80 ms.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float hitStopDuration = 0.07f;

        [Range(0f, 1f)]
        [SerializeField] private float hitStopTimeScale = 0.04f;

        [Tooltip("Hit stop on a perfect catch, which deserves a longer beat.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float perfectHitStopDuration = 0.11f;

        [Header("Catch — settle")]
        [Tooltip("Starting amplitude of the damped spring the ring settles with, " +
                 "in metres. The settle is evaluated, never a recorded clip.")]
        [SerializeField] private float settleAmplitude = 0.13f;

        [Tooltip("Exponential decay rate of the settle. Higher stops sooner.")]
        [SerializeField] private float settleDecay = 7.5f;

        [Tooltip("Oscillations per second of the settle.")]
        [SerializeField] private float settleFrequency = 6.2f;

        [SerializeField] private float settleDuration = 0.85f;

        [Tooltip("How much of the settle also shows up as a tilt wobble, in degrees " +
                 "per metre of vertical amplitude.")]
        [SerializeField] private float settleTiltPerMetre = 62f;

        [Header("Catch — flourish")]
        [SerializeField] private float catchPopScale = 1.22f;
        [SerializeField] private float catchPopDuration = 0.28f;

        [Header("Peg reaction")]
        [SerializeField] private float pegPulseDuration = 0.45f;
        [SerializeField] private float pegPulseEmission = 3.4f;
        [SerializeField] private float pegSquashAmount = 0.18f;
        [SerializeField] private float pegSquashDuration = 0.36f;

        [Tooltip("Emission the peg holds once it has been claimed, so a solved peg " +
                 "reads differently from an open one at a glance.")]
        [SerializeField] private float pegClaimedEmission = 1.15f;

        [Header("Camera shake")]
        [Tooltip("Deliberately small, and smaller again after the first play test. " +
                 "Over-shaking is the classic failure of this kind of feedback and it " +
                 "is far harder to notice while tuning than it is on a phone.\n\n" +
                 "Note what is missing: there is no shake for a perfect catch. Shake " +
                 "reads as impact, and a perfect ringer is the one throw in the game " +
                 "that never touched anything. Letting the screen sit perfectly still " +
                 "is what makes it feel clean, and it distinguishes a perfect from a " +
                 "scrappy catch better than adding more noise to it ever could.")]
        [SerializeField] private float shakeOnThrow = 0.03f;

        [SerializeField] private float shakeOnBounce = 0.05f;

        [Tooltip("A catch that clipped the peg on the way in. The only catch that shakes.")]
        [SerializeField] private float shakeOnRinger = 0.1f;

        [SerializeField] private float shakeDuration = 0.28f;
        [SerializeField] private float shakeFrequency = 22f;

        [Header("Score")]
        [SerializeField] private float scoreCountUpDuration = 0.42f;
        [SerializeField] private float scorePopScale = 1.28f;
        [SerializeField] private float scorePopDuration = 0.3f;

        [Header("Floating text")]
        [SerializeField] private float popupRise = 1.05f;
        [SerializeField] private float popupDuration = 0.95f;
        [SerializeField] private float popupPunchScale = 1.45f;

        [Header("Failure")]
        [Tooltip("How long a failed ring lies on the ground before it fades away.")]
        [SerializeField] private float missLingerDuration = 0.28f;

        [SerializeField] private float missFadeDuration = 0.3f;

        [Header("Combo")]
        [Tooltip("Pitch multiplier added per consecutive hit.\n\n" +
                 "Deliberately small. The rising pitch is doing real work — it is the " +
                 "part of a streak the player hears without looking at a counter — but " +
                 "at the old step and ceiling a long run climbed most of a fifth above " +
                 "the root and turned thin and shrill exactly when it should have felt " +
                 "best. A third is plenty to hear.")]
        [SerializeField] private float comboPitchStep = 0.028f;

        [SerializeField] private float comboPitchCeiling = 1.26f;

        [Tooltip("Combo at which confetti starts firing. Below this it would be noise.")]
        [SerializeField] private int confettiComboThreshold = 3;

        [Header("Flow")]
        [Tooltip("Delay between a throw resolving and the next ring being armed. " +
                 "The spec target for the whole reset is under 400 ms.")]
        [SerializeField] private float resetDelay = 0.22f;

        [SerializeField] private float levelIntroDuration = 0.9f;
        [SerializeField] private float levelOutroDelay = 0.7f;
        [SerializeField] private float screenFadeDuration = 0.32f;

        [Header("Floor glow")]
        [Tooltip("Emission on the deck and its rim while a board is in play, before any " +
                 "streak. Faint: it says the floor is the lit place and nothing more.")]
        [SerializeField] private float floorGlowBase = 0.07f;

        [Tooltip("Added for every perfect in a row. Measured from captures: at 0.03 six " +
                 "perfects lifted the deck by seven percent, which nobody would notice; " +
                 "at 0.1 it took six to peak, which the owner found far too slow.")]
        [SerializeField] private float floorGlowPerPerfect = 0.15f;

        [Tooltip("Perfects in a row past which the floor stops getting brighter. The " +
                 "owner's rule for this one was 'sem ser muito exagerado', and then 'mais " +
                 "imediato': the same peak, reached in four rather than six.")]
        [SerializeField] private int floorGlowMaxStreak = 4;

        [Tooltip("The rim glows this much more than the deck, so the border reads as a border.")]
        [SerializeField] private float floorGlowRimBoost = 1.7f;

        [Tooltip("The soft band of light the lit border spills inwards, against the " +
                 "border's own brightness. A line that only gets paler is not a glow.")]
        [Range(0f, 2f)]
        [SerializeField] private float floorGlowSkirt = 1.05f;

        [Tooltip("Seconds to rise to a new step. Quick: it answers the catch.")]
        [SerializeField] private float floorGlowRise = 0.12f;

        [Tooltip("Seconds to fall back after a miss. Slower than the rise, so losing a " +
                 "streak reads as the light going out rather than a switch — but quick " +
                 "enough that a loss visibly zeroes it.")]
        [SerializeField] private float floorGlowFall = 0.4f;

        [Tooltip("A short extra flare on every perfect, on top of the step it earns.")]
        [SerializeField] private float floorGlowFlash = 0.08f;

        public float IdleBreathAmount => idleBreathAmount;
        public float IdleBreathDuration => idleBreathDuration;
        public float IdleBobAmount => idleBobAmount;
        public float IdleSpinSpeed => idleSpinSpeed;

        public float ArmPopDuration => armPopDuration;
        public float ArmPopOvershoot => armPopOvershoot;

        public float AimZoomAmount => aimZoomAmount;
        public float AimZoomDuration => aimZoomDuration;
        public float AimRollDegrees => aimRollDegrees;
        public float AimTiltDegrees => aimTiltDegrees;
        public float AimDamping => aimDamping;

        public float PreviewDotSpeed => previewDotSpeed;
        public float PreviewPulseAmount => previewPulseAmount;
        public float PreviewFadeDuration => previewFadeDuration;

        public float TrailTime => trailTime;
        public float TrailStartWidth => trailStartWidth;
        public float ShadowFadeHeight => shadowFadeHeight;
        public float ShadowMaxScale => shadowMaxScale;
        public float ShadowMinScale => shadowMinScale;
        public float ShadowMaxAlpha => shadowMaxAlpha;

        public float HitStopDuration => hitStopDuration;
        public float HitStopTimeScale => hitStopTimeScale;
        public float PerfectHitStopDuration => perfectHitStopDuration;

        public float SettleAmplitude => settleAmplitude;
        public float SettleDecay => settleDecay;
        public float SettleFrequency => settleFrequency;
        public float SettleDuration => settleDuration;
        public float SettleTiltPerMetre => settleTiltPerMetre;

        public float CatchPopScale => catchPopScale;
        public float CatchPopDuration => catchPopDuration;

        public float PegPulseDuration => pegPulseDuration;
        public float PegPulseEmission => pegPulseEmission;
        public float PegSquashAmount => pegSquashAmount;
        public float PegSquashDuration => pegSquashDuration;
        public float PegClaimedEmission => pegClaimedEmission;

        public float ShakeOnThrow => shakeOnThrow;
        public float ShakeOnBounce => shakeOnBounce;
        public float ShakeOnRinger => shakeOnRinger;
        public float ShakeDuration => shakeDuration;
        public float ShakeFrequency => shakeFrequency;

        public float ScoreCountUpDuration => scoreCountUpDuration;
        public float ScorePopScale => scorePopScale;
        public float ScorePopDuration => scorePopDuration;

        public float PopupRise => popupRise;
        public float PopupDuration => popupDuration;
        public float PopupPunchScale => popupPunchScale;

        public float MissLingerDuration => missLingerDuration;
        public float MissFadeDuration => missFadeDuration;

        public float ComboPitchStep => comboPitchStep;
        public float ComboPitchCeiling => comboPitchCeiling;
        public int ConfettiComboThreshold => confettiComboThreshold;

        public float ResetDelay => resetDelay;
        public float LevelIntroDuration => levelIntroDuration;
        public float LevelOutroDelay => levelOutroDelay;
        public float ScreenFadeDuration => screenFadeDuration;

        /// <summary>How brightly the border's spill of light reads, against the border itself.</summary>
        public float FloorGlowSkirt => floorGlowSkirt;

        public float FloorGlowRimBoost => floorGlowRimBoost;
        public float FloorGlowRise => floorGlowRise;
        public float FloorGlowFall => floorGlowFall;
        public float FloorGlowFlash => floorGlowFlash;

        /// <summary>The deck's emission for a run of perfects, capped where it stops being subtle.</summary>
        public float FloorGlowFor(int perfectsInARow) =>
            floorGlowBase + floorGlowPerPerfect * Mathf.Clamp(perfectsInARow, 0, floorGlowMaxStreak);

        /// <summary>
        /// Damped spring displacement used for the settle: amplitude * e^(-decay*t) *
        /// sin(2*pi*frequency*t). Evaluated every frame rather than baked, so the
        /// settle can start from whatever the impact actually was.
        /// </summary>
        public float EvaluateSettle(float time, float amplitudeScale)
        {
            return settleAmplitude * amplitudeScale
                   * Mathf.Exp(-settleDecay * time)
                   * Mathf.Sin(2f * Mathf.PI * settleFrequency * time);
        }

        /// <summary>Audio pitch for a given combo, capped so it never turns shrill.</summary>
        public float ComboPitch(int combo)
        {
            return Mathf.Min(1f + Mathf.Max(0, combo - 1) * comboPitchStep, comboPitchCeiling);
        }
    }
}
