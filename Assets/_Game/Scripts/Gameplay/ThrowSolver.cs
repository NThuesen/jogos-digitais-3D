using SlipperyZlope.RingToss.Config;
using UnityEngine;

namespace SlipperyZlope.RingToss.Gameplay
{
    /// <summary>Result of turning a screen-space drag into a throw.</summary>
    public readonly struct ThrowSolution
    {
        public readonly bool IsValid;
        public readonly Vector3 Velocity;
        public readonly Vector3 AngularVelocity;

        /// <summary>Drag length as 0..1 of the configured maximum. Useful for previews and audio.</summary>
        public readonly float NormalisedForce;

        /// <summary>Ground distance this throw is aimed to land at, in metres.</summary>
        public readonly float Range;

        public ThrowSolution(
            bool isValid, Vector3 velocity, Vector3 angularVelocity, float normalisedForce, float range)
        {
            IsValid = isValid;
            Velocity = velocity;
            AngularVelocity = angularVelocity;
            NormalisedForce = normalisedForce;
            Range = range;
        }

        public static ThrowSolution Invalid => new ThrowSolution(false, Vector3.zero, Vector3.zero, 0f, 0f);
    }

    /// <summary>
    /// Pure function from a screen drag to a throw. Kept free of Unity scene state so
    /// the mapping can be unit tested without entering Play Mode, and so the same
    /// mapping can drive both the trajectory preview and the actual release.
    ///
    /// The drag is read as two independent axes, and the forward one picks a distance
    /// rather than a speed.
    ///
    /// Both of those are deliberate corrections to the obvious design. Deriving power
    /// from the drag's total length couples aim to power: a swipe wide enough to reach
    /// an off-centre peg is also a swipe long enough to throw at full strength, so
    /// every near peg that is not dead ahead becomes unreachable — not hard, actually
    /// impossible. And mapping the drag to a launch speed leaves the player solving a
    /// ballistic equation by feel, because the distance a throw covers depends on the
    /// pitch as much as the speed. Picking the distance and solving for the speed puts
    /// the one quantity the player is actually aiming with under their thumb.
    /// </summary>
    public static class ThrowSolver
    {
        /// <param name="dragPixels">Release position minus press position, in pixels.</param>
        /// <param name="screenHeightPixels">Used to normalise the drag so behaviour is resolution independent.</param>
        /// <param name="aimBasis">Rotation the throw is measured against, normally the throw origin's yaw.</param>
        /// <param name="launchHeight">World height the ring leaves the hand at.</param>
        public static ThrowSolution Solve(
            Vector2 dragPixels,
            float screenHeightPixels,
            Quaternion aimBasis,
            float launchHeight,
            PhysicsConfig config)
        {
            if (config == null || screenHeightPixels <= 0f)
            {
                return ThrowSolution.Invalid;
            }

            if (config.InvertDragDirection)
            {
                dragPixels = -dragPixels;
            }

            Vector2 dragScreenHeights = dragPixels / screenHeightPixels;

            // Power comes from the forward component alone. A backwards drag is not a
            // negative throw, it is no throw.
            float forward = dragScreenHeights.y;

            if (forward < config.DeadzoneScreenFraction)
            {
                return ThrowSolution.Invalid;
            }

            float normalisedForce = Mathf.Clamp01(
                forward / Mathf.Max(config.MaxDragScreenFraction, Mathf.Epsilon));

            float rangeBlend = EvaluateRamp(config.DragToRangeCurve, normalisedForce);
            float range = Mathf.Lerp(config.MinThrowRange, config.MaxThrowRange, rangeBlend);

            ShapeFor(range, launchHeight, config, out float pitch, out float speed);

            float yaw = Mathf.Clamp(
                dragScreenHeights.x / Mathf.Max(config.LateralDragScreenFraction, Mathf.Epsilon),
                -1f,
                1f) * config.MaxYawDegrees;

            // Negative pitch because a positive X rotation aims downwards in Unity.
            Quaternion aim = aimBasis * Quaternion.Euler(-pitch, yaw, 0f);
            Vector3 velocity = aim * Vector3.forward * speed;

            float spin = Mathf.Lerp(config.SpinAtMinForce, config.SpinAtMaxForce, normalisedForce);

            // Tumble is derived from how sideways the swipe was, so a curved swipe visibly
            // wobbles and a clean straight swipe flies clean. Deterministic on purpose:
            // the same drag must always produce the same throw.
            float lateralBias = Mathf.Clamp(
                dragScreenHeights.x / Mathf.Max(config.LateralDragScreenFraction, Mathf.Epsilon), -1f, 1f);
            float tumble = config.TumbleMagnitude * lateralBias;

            Vector3 angularVelocity =
                Vector3.up * spin
                + aim * Vector3.right * tumble
                + aim * Vector3.forward * (tumble * 0.35f);

            return new ThrowSolution(true, velocity, angularVelocity, normalisedForce, range);
        }

        /// <summary>
        /// A curve that is meant to be a 0..1 ramp, with a straight ramp as the answer
        /// when it is not one.
        ///
        /// A curve field added to a config type after its asset was last saved can
        /// come back empty, and an empty AnimationCurve evaluates to zero everywhere.
        /// Without this guard that turns every throw in the game into the shortest
        /// possible one, with nothing on screen to say why.
        /// </summary>
        private static float EvaluateRamp(AnimationCurve curve, float t)
        {
            if (curve == null || curve.length < 2)
            {
                return Mathf.Clamp01(t);
            }

            return Mathf.Clamp01(curve.Evaluate(t));
        }

        /// <summary>
        /// The pitch and speed a throw aimed at a given ground range is made of.
        ///
        /// Shared by the solver and by the reachability check below, so a peg can never
        /// be judged reachable by a differently shaped arc than the one the player will
        /// actually throw.
        /// </summary>
        public static void ShapeFor(
            float aimRange, float launchHeight, PhysicsConfig config, out float pitch, out float speed)
        {
            float blend = Mathf.InverseLerp(config.MinThrowRange, config.MaxThrowRange, aimRange);

            pitch = Mathf.Lerp(config.PitchAtMinRange, config.PitchAtMaxRange, blend);

            float heightDrop = launchHeight - config.LandingHeight;
            speed = Mathf.Clamp(
                SpeedForRange(aimRange, pitch, heightDrop, config.GravityMagnitude),
                config.MinThrowSpeed,
                config.MaxThrowSpeed);
        }

        /// <summary>
        /// How far a throw aimed at <paramref name="aimRange"/> has travelled by the
        /// time it is coming *down* through <paramref name="height"/>.
        ///
        /// This is what makes a raised peg a fair target rather than a trap. The throw
        /// is aimed to arrive at the height of a peg standing on the board, so a peg on
        /// a plinth has to be met earlier on the arc — the player aims past it. This
        /// answers by how much, and a level or a generated round can be checked against
        /// it before it is ever played.
        ///
        /// Zero means the arc never climbs to that height at all, however it is aimed.
        /// </summary>
        public static float DescendingReach(
            float aimRange, float height, float launchHeight, PhysicsConfig config)
        {
            if (config == null)
            {
                return 0f;
            }

            ShapeFor(aimRange, launchHeight, config, out float pitchDegrees, out float speed);

            float pitch = pitchDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(pitch);

            if (speed <= 0f || cos <= 1e-4f)
            {
                return 0f;
            }

            // y(x) = launch + x tan(t) - a x^2, with a = g / (2 v^2 cos^2(t)).
            // Setting y(x) = height and taking the larger root gives the descending
            // crossing; the smaller one is where the arc passed the same height going up.
            float a = config.GravityMagnitude / (2f * speed * speed * cos * cos);
            float b = Mathf.Tan(pitch);
            float c = launchHeight - height;

            float discriminant = b * b + 4f * a * c;

            if (discriminant < 0f)
            {
                return 0f;
            }

            return (b + Mathf.Sqrt(discriminant)) / (2f * a);
        }

        /// <summary>
        /// True when some legal throw arrives descending onto a target of the given
        /// height at the given distance.
        ///
        /// Reach grows with the aim range, so testing the two extremes is enough.
        /// </summary>
        public static bool CanReach(float range, float height, float launchHeight, PhysicsConfig config)
        {
            float nearest = DescendingReach(config.MinThrowRange, height, launchHeight, config);
            float furthest = DescendingReach(config.MaxThrowRange, height, launchHeight, config);

            if (furthest <= 0f)
            {
                return false;
            }

            return range >= nearest && range <= furthest;
        }

        /// <summary>
        /// Launch speed that lands a projectile <paramref name="range"/> metres away,
        /// <paramref name="heightDrop"/> metres below where it started, thrown at
        /// <paramref name="pitchDegrees"/> above horizontal.
        ///
        /// Standard ballistic inversion:
        ///     v^2 = g d^2 / (2 cos^2(t) (d tan(t) + drop))
        ///
        /// The denominator goes to zero for a throw that would have to travel further
        /// than its pitch allows at any speed. That is clamped rather than special
        /// cased: the caller pins the result to the configured speed limits anyway, and
        /// a pinned throw is a recoverable miss where a NaN is a ring that vanishes.
        /// </summary>
        public static float SpeedForRange(float range, float pitchDegrees, float heightDrop, float gravity)
        {
            if (range <= 0f || gravity <= 0f)
            {
                return 0f;
            }

            float pitch = pitchDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(pitch);
            float tan = Mathf.Tan(pitch);

            float denominator = 2f * cos * cos * (range * tan + heightDrop);

            if (denominator <= 1e-4f)
            {
                return float.MaxValue;
            }

            return Mathf.Sqrt(gravity * range * range / denominator);
        }

        /// <summary>
        /// True when the finger has come back close enough to the press point that the
        /// player is clearly abandoning the throw.
        /// </summary>
        public static bool ShouldCancel(Vector2 dragPixels, float screenHeightPixels, PhysicsConfig config)
        {
            if (config == null || screenHeightPixels <= 0f)
            {
                return false;
            }

            if (config.InvertDragDirection)
            {
                dragPixels = -dragPixels;
            }

            // A drag that never went forward is a cancel however long it is sideways:
            // there is no throw to release, so letting go has to put the ring back.
            return dragPixels.y / screenHeightPixels < config.CancelScreenFraction;
        }
    }
}
