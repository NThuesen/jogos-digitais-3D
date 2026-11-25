using UnityEngine;

namespace SlipperyZlope.RingToss.Config
{
    /// <summary>
    /// What the camera can actually see, as numbers the rest of the game can ask.
    ///
    /// This exists because of a bug that got past two rounds of review. The booth, its
    /// dressing and the peg layouts were all placed against screenshots captured at
    /// 9:16 — and no phone made in the last decade is 9:16. A real handset is 9:19.5 or
    /// taller, and because the camera's field of view is *vertical*, a taller screen
    /// shows **less** horizontally, not more. At 9:20 the visible width is 80% of what
    /// the captures showed.
    ///
    /// The result was that the booth's own frame posts, the balloons, the crates, the
    /// corner lanterns and the back shelf were all off the edge of a real screen — so
    /// the bunting and lights appeared to hang from nothing again — and, far worse,
    /// pegs on the deep rows sat outside the frame entirely. The player could be asked
    /// to hit something they could not see.
    ///
    /// Everything is now placed against <see cref="NarrowestAspect"/>. A wider screen
    /// simply shows a little more scenery at the edges, which is the correct way round.
    /// </summary>
    public static class CameraFraming
    {
        /// <summary>
        /// Mirrors the scene's camera rig. Kept in sync by CameraFramingTests.
        ///
        /// Moved back from -7.9 to -9.4. At -7.9 the ring in the player's hand was
        /// held 3.4 m from the lens, where the frame is only 1.14 m across, so a 0.64 m
        /// ring covered **56% of the width of the screen** — it was the biggest thing
        /// in the picture by a wide margin, and it sat on top of the board the player
        /// was aiming at. A metre and a half of distance takes it to 39% and costs the
        /// booth six points of frame, which is the right trade on a phone.
        ///
        /// Distance rather than a wider lens, deliberately. Both shrink everything;
        /// only one of them keeps the perspective. Opening the field of view to 50°
        /// gets the same 45% ring, and doubles the band of sky above the booth's beam,
        /// which is the hardest part of this composition to hold together.
        /// </summary>
        public const float CameraZ = -9.4f;

        /// <summary>
        /// The vertical field of view the whole layout is authored against.
        ///
        /// What a given device actually runs is <see cref="FieldOfViewFor"/>, which is
        /// this or wider, never narrower.
        /// </summary>
        public const float VerticalFieldOfView = 41f;

        /// <summary>
        /// The aspect the layout is composed against, as width / height. 0.45 is 9:20.
        ///
        /// It used to be a promise about the narrowest phone anybody would use, and a
        /// taller one simply lost scenery off both edges. It is now a *guarantee*:
        /// <see cref="FieldOfViewFor"/> widens the lens on anything taller so that this
        /// much width is always visible. Everything placed against the numbers below is
        /// therefore on screen on every device, not just on this one.
        /// </summary>
        public const float NarrowestAspect = 0.45f;

        /// <summary>
        /// The field of view to run at on a screen of this shape.
        ///
        /// The rule is: **never show less than the composed frame in either axis.**
        /// The lens is vertical, so a screen taller than the design aspect would crop
        /// the sides — the bug that cost this project two rounds of scenery — and the
        /// fix is to open the lens until the design width fits again, which shows a
        /// little extra height instead. A screen wider than the design aspect already
        /// has the width, so it keeps the authored angle and shows extra width.
        ///
        /// Both directions only ever *add*. Nothing composed against
        /// <see cref="NarrowestAspect"/> can be pushed off a screen by this.
        /// </summary>
        public static float FieldOfViewFor(float aspect)
        {
            if (aspect <= 0f || !float.IsFinite(aspect))
            {
                return VerticalFieldOfView;
            }

            float forWidth = 2f * Mathf.Atan(TanHalfFov * NarrowestAspect / aspect) * Mathf.Rad2Deg;

            return Mathf.Clamp(Mathf.Max(VerticalFieldOfView, forWidth), VerticalFieldOfView, 80f);
        }

        /// <summary>
        /// How much of the visible half-width anything important must stay inside.
        ///
        /// A peg exactly on the edge is technically visible and practically useless:
        /// the player cannot judge a throw against something clipped by the bezel.
        /// </summary>
        public const float SafeFraction = 0.88f;

        private static float TanHalfFov => Mathf.Tan(VerticalFieldOfView * 0.5f * Mathf.Deg2Rad);

        /// <summary>Mirrors the scene's camera rig. Kept in sync by CameraFramingTests.</summary>
        public const float CameraY = 2.55f;

        /// <summary>How far the camera is tilted down, in degrees. Also from the rig.</summary>
        public const float CameraPitchDegrees = 12f;

        /// <summary>
        /// World height of the top edge of the frame at a given depth.
        ///
        /// The camera looks *down*, so the top edge of the frame still rises: it leaves
        /// the lens at (half the field of view minus the pitch) above horizontal. At 41
        /// degrees and a 12 degree tilt that is a shallow 8.5 degrees, which is why the
        /// band of sky above the booth is so unforgiving — everything in it has to be
        /// both far away and tall.
        /// </summary>
        public static float TopHeightAt(float z)
        {
            return CameraY + Mathf.Tan((VerticalFieldOfView * 0.5f - CameraPitchDegrees) * Mathf.Deg2Rad)
                * (z - CameraZ);
        }

        /// <summary>
        /// World height at a given depth of the sight line that grazes a point.
        ///
        /// This is what turns an occluder into a horizon: the booth's beam hides
        /// everything below the line from the lens over its top edge, so projecting
        /// that line out to a skyline's depth gives the height below which nothing
        /// there can ever be seen. Scenery placed against this number follows the beam
        /// automatically instead of being re-guessed every time the booth moves.
        /// </summary>
        public static float SightLineAt(float occluderY, float occluderZ, float z)
        {
            float run = occluderZ - CameraZ;

            if (run <= 1e-4f)
            {
                return occluderY;
            }

            return CameraY + (occluderY - CameraY) / run * (z - CameraZ);
        }

        /// <summary>Half the visible width, in metres, at a given board-local depth.</summary>
        public static float HalfWidthAt(float z)
        {
            return Mathf.Max(0f, (z - CameraZ) * TanHalfFov * NarrowestAspect);
        }

        /// <summary>The same, pulled in by the safety margin. This is the number to place against.</summary>
        public static float SafeHalfWidthAt(float z)
        {
            return HalfWidthAt(z) * SafeFraction;
        }

        /// <summary>
        /// The widest bearing a peg at this distance can take and still be comfortably
        /// on screen.
        ///
        /// Solved rather than approximated, because a peg's depth falls as its bearing
        /// grows — swinging wide brings it closer to the camera, where the frame is
        /// narrower, so the limit tightens on itself. Bisection over a monotone
        /// function is a few lines and cannot be got subtly wrong.
        /// </summary>
        public static float MaxYawAt(float range)
        {
            if (range <= 0f)
            {
                return 0f;
            }

            float low = 0f;
            float high = 45f;

            for (int i = 0; i < 24; i++)
            {
                float mid = (low + high) * 0.5f;

                if (FitsAt(range, mid))
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            return low;
        }

        /// <summary>True when a peg at this distance and bearing sits inside the safe frame.</summary>
        public static bool FitsAt(float range, float yawDegrees)
        {
            float yaw = yawDegrees * Mathf.Deg2Rad;
            float x = Mathf.Abs(Mathf.Sin(yaw)) * range;
            float z = ThrowOriginZ + Mathf.Cos(yaw) * range;

            return x <= SafeHalfWidthAt(z);
        }

        /// <summary>True when a point on the board is inside the safe frame.</summary>
        public static bool Fits(Vector2 anchor)
        {
            return Mathf.Abs(anchor.x) <= SafeHalfWidthAt(anchor.y);
        }

        /// <summary>
        /// The same, with room to spare. What a *generator* should ask.
        ///
        /// A generator that fits a moving peg by sampling its cycle and a test that
        /// checks it by sampling the same cycle will not sample the same points, and a
        /// peg accepted at exactly the boundary then fails by a thousandth of a metre.
        /// The producer has to be stricter than the checker, or the two argue forever
        /// over a rounding error.
        /// </summary>
        public static bool FitsWithMargin(Vector2 anchor)
        {
            return Mathf.Abs(anchor.x) <= SafeHalfWidthAt(anchor.y) * 0.96f;
        }

        /// <summary>Board-local Z of the throw origin, which every range is measured from.</summary>
        public const float ThrowOriginZ = -4.5f;
    }
}
