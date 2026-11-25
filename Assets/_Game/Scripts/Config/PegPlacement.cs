using System;
using UnityEngine;

namespace SlipperyZlope.RingToss.Config
{
    /// <summary>How a peg moves. Motion is the main difficulty lever after distance.</summary>
    public enum PegMotion
    {
        Static,

        /// <summary>Slides left and right across the board.</summary>
        SlideX,

        /// <summary>Rises and falls in place.</summary>
        Bob,

        /// <summary>Circles around its anchor on the ground plane.</summary>
        Orbit,

        /// <summary>Moves towards and away from the player.</summary>
        SlideZ
    }

    /// <summary>
    /// One peg in a level. Serialized by value inside <see cref="LevelConfig"/> so a
    /// level is a single asset rather than a folder of them.
    /// </summary>
    [Serializable]
    public struct PegPlacement
    {
        [Tooltip("Anchor on the board. X is lateral, Y is depth away from the player.")]
        public Vector2 anchor;

        [Tooltip("Peg height as a multiple of the default. Taller pegs are easier to " +
                 "hit but harder to drop over cleanly.")]
        public float heightScale;

        [Tooltip("Peg thickness as a multiple of the default. Above 1 the peg eats " +
                 "into the ring's hole and the tolerance shrinks.")]
        public float radiusScale;

        [Tooltip("Metres the peg is raised off the board on a plinth.\n\n" +
                 "This is a real difficulty lever rather than decoration. The throw is " +
                 "aimed to arrive at the height of a peg standing on the board, so a " +
                 "raised peg has to be met earlier on the arc — which means aiming " +
                 "past it. The plinth is drawn so the player can see why.")]
        public float baseHeight;

        [Tooltip("Score for claiming this peg, before the combo multiplier.")]
        public int points;

        public PegMotion motion;

        [Tooltip("Metres of travel, or orbit radius.")]
        public float motionAmplitude;

        [Tooltip("Full cycles per second.")]
        public float motionSpeed;

        [Tooltip("Offsets the cycle so several moving pegs are not in lockstep.")]
        [Range(0f, 1f)]
        public float motionPhase;

        [Tooltip("Seconds for one full sink-and-rise cycle. Zero means the peg is " +
                 "always up.")]
        public float retractPeriod;

        [Tooltip("Fraction of that cycle the peg spends standing. Below about 0.5 the " +
                 "peg is gone more often than it is there, which stops being a timing " +
                 "problem and becomes a waiting one.")]
        [Range(0.1f, 0.95f)]
        public float retractDuty;

        [Tooltip("Offsets the retract cycle so a row of pegs does not sink as one.")]
        [Range(0f, 1f)]
        public float retractPhase;

        public static PegPlacement Default(Vector2 anchor)
        {
            return new PegPlacement
            {
                anchor = anchor,
                heightScale = 1f,
                radiusScale = 1f,
                points = 100,
                baseHeight = 0f,
                motion = PegMotion.Static,
                motionAmplitude = 0f,
                motionSpeed = 0f,
                motionPhase = 0f,
                retractPeriod = 0f,
                retractDuty = 0.65f,
                retractPhase = 0f
            };
        }

        /// <summary>
        /// Offset from the anchor at a given time. Deterministic in time rather than
        /// integrated, so a peg never drifts and a level always plays the same.
        /// </summary>
        public Vector3 OffsetAt(float time)
        {
            if (motion == PegMotion.Static || motionAmplitude <= 0f)
            {
                return Vector3.zero;
            }

            float phase = 2f * Mathf.PI * (time * motionSpeed + motionPhase);

            switch (motion)
            {
                case PegMotion.SlideX:
                    return new Vector3(Mathf.Sin(phase) * motionAmplitude, 0f, 0f);
                case PegMotion.SlideZ:
                    return new Vector3(0f, 0f, Mathf.Sin(phase) * motionAmplitude);
                case PegMotion.Bob:
                    return new Vector3(0f, Mathf.Sin(phase) * motionAmplitude, 0f);
                case PegMotion.Orbit:
                    return new Vector3(
                        Mathf.Cos(phase) * motionAmplitude,
                        0f,
                        Mathf.Sin(phase) * motionAmplitude * 0.55f);
                default:
                    return Vector3.zero;
            }
        }

        /// <summary>
        /// How far the peg is out of the board at a moment in time: 1 fully standing,
        /// 0 fully sunk.
        ///
        /// Deterministic in time for the same reason the motion is, so a retracting
        /// peg is a rhythm the player can learn rather than a coin flip.
        /// </summary>
        public float PresenceAt(float time)
        {
            if (retractPeriod <= 0f)
            {
                return 1f;
            }

            float duty = Mathf.Clamp(retractDuty, 0.1f, 0.95f);
            float u = Mathf.Repeat(time / retractPeriod + retractPhase, 1f);

            // The peg slides rather than blinks. A blink is unreadable at speed, and
            // the first time it costs a throw it reads as a bug rather than a rule.
            float slide = Mathf.Min(0.15f, (1f - duty) * 0.4f);

            if (u < slide)
            {
                return Mathf.SmoothStep(0f, 1f, u / slide);
            }

            if (u < duty)
            {
                return 1f;
            }

            if (u < duty + slide)
            {
                return Mathf.SmoothStep(1f, 0f, (u - duty) / slide);
            }

            return 0f;
        }

        /// <summary>True when this peg ever leaves the board.</summary>
        public bool Retracts => retractPeriod > 0f;
    }
}
