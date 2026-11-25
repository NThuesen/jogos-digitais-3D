using NUnit.Framework;
using SlipperyZlope.RingToss.Config;
using UnityEngine;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// What the camera can see, and why anyone should care.
    ///
    /// This fixture exists because of a bug that survived two rounds of visual review.
    /// Everything in the scene was placed against screenshots captured at 9:16, and no
    /// phone is 9:16. The camera's field of view is vertical, so a taller screen shows
    /// *less* width — at 9:20 the frame is 80% of what those captures showed, and the
    /// booth's own frame posts, its balloons, its crates and several pegs were all off
    /// the edge of a real handset.
    ///
    /// A screenshot at the wrong aspect is not verification. These are the checks that
    /// do not depend on anybody looking.
    /// </summary>
    public sealed class CameraFramingTests
    {
        [Test]
        public void A_taller_screen_shows_less_width_not_more()
        {
            // The whole bug in one assertion. If this ever reads the other way round,
            // the field of view has been made horizontal and every placement in the
            // project is calibrated against the wrong axis.
            float atBoard = CameraFraming.HalfWidthAt(0f);

            Assert.Greater(atBoard, 0f);
            Assert.Less(CameraFraming.NarrowestAspect, 0.5625f,
                "The supported aspect has to be at least as tall as a modern phone.");
        }

        /// <summary>
        /// The responsive lens, and the one property that makes it safe.
        ///
        /// Everything in this game is placed against the composed aspect. If the lens
        /// ever showed *less* than that on some device, every one of those placements
        /// would be a guess again — which is exactly the failure this whole class
        /// exists because of. So the rule is not "fit the screen", it is "never show
        /// less than the composed frame in either axis", and that is what is asserted.
        /// </summary>
        [Test]
        public void The_lens_never_shows_less_than_the_composed_frame()
        {
            float composedTan = Mathf.Tan(CameraFraming.VerticalFieldOfView * 0.5f * Mathf.Deg2Rad);
            float composedHalfWidth = composedTan * CameraFraming.NarrowestAspect;

            // 9:21 through a 4:3 tablet, plus the two extremes a resize can pass through.
            float[] aspects = { 9f / 21f, 9f / 20f, 9f / 19.5f, 9f / 18f, 9f / 16f, 3f / 4f, 1f, 2f };

            foreach (float aspect in aspects)
            {
                float fov = CameraFraming.FieldOfViewFor(aspect);
                float tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

                Assert.GreaterOrEqual(tan * aspect, composedHalfWidth - 1e-4f,
                    $"At {aspect:F3} the frame is narrower than composed: the sides get cropped.");

                Assert.GreaterOrEqual(tan, composedTan - 1e-4f,
                    $"At {aspect:F3} the frame is shorter than composed: the top or bottom gets cropped.");
            }
        }

        [Test]
        public void A_screen_the_composed_shape_gets_the_composed_lens()
        {
            Assert.AreEqual(
                CameraFraming.VerticalFieldOfView,
                CameraFraming.FieldOfViewFor(CameraFraming.NarrowestAspect),
                0.01f);
        }

        [Test]
        public void A_nonsense_aspect_falls_back_rather_than_producing_a_nonsense_lens()
        {
            // Screen.width is 0 for a frame or two on some Android launches, and a
            // camera set to a NaN field of view renders nothing at all, forever.
            Assert.AreEqual(CameraFraming.VerticalFieldOfView, CameraFraming.FieldOfViewFor(0f), 0.01f);
            Assert.AreEqual(CameraFraming.VerticalFieldOfView, CameraFraming.FieldOfViewFor(-1f), 0.01f);
            Assert.AreEqual(
                CameraFraming.VerticalFieldOfView, CameraFraming.FieldOfViewFor(float.NaN), 0.01f);
        }

        [Test]
        public void The_frame_widens_with_distance()
        {
            float previous = 0f;

            for (float z = -3f; z <= 5f; z += 0.25f)
            {
                float half = CameraFraming.HalfWidthAt(z);

                Assert.Greater(half, previous, $"The frame narrowed going back to z={z:F2}.");
                previous = half;
            }
        }

        [Test]
        public void The_safe_frame_is_inside_the_visible_one()
        {
            for (float z = -3f; z <= 5f; z += 0.5f)
            {
                Assert.Less(CameraFraming.SafeHalfWidthAt(z), CameraFraming.HalfWidthAt(z));
            }
        }

        /// <summary>
        /// The limit tightens as a peg swings wide, because swinging wide also brings
        /// it closer to the camera, where the frame is narrower. A limit that did not
        /// account for that would pass the check at the anchor and fail on screen.
        /// </summary>
        [Test]
        public void The_yaw_limit_tightens_with_distance()
        {
            float previous = 90f;

            for (float range = 2f; range <= 6f; range += 0.25f)
            {
                float limit = CameraFraming.MaxYawAt(range);

                Assert.Greater(limit, 0f, $"Nothing at all fits at {range:F2} m.");
                Assert.LessOrEqual(limit, previous + 1e-3f,
                    $"The yaw limit widened going out to {range:F2} m.");

                previous = limit;
            }
        }

        [Test]
        public void A_peg_at_the_yaw_limit_fits_and_one_past_it_does_not()
        {
            for (float range = 2f; range <= 6f; range += 0.5f)
            {
                float limit = CameraFraming.MaxYawAt(range);

                Assert.IsTrue(CameraFraming.FitsAt(range, limit - 0.2f),
                    $"A peg just inside the limit at {range:F2} m does not fit.");
                Assert.IsFalse(CameraFraming.FitsAt(range, limit + 0.5f),
                    $"A peg past the limit at {range:F2} m still counts as fitting.");
            }
        }

        /// <summary>
        /// The number that actually bit: at the back of the board the throw allows a
        /// bearing the screen does not. Whichever is tighter has to win.
        /// </summary>
        [Test]
        public void The_screen_is_the_tighter_limit_at_the_back_of_the_board()
        {
            PhysicsConfig physics = ScriptableObject.CreateInstance<PhysicsConfig>();

            float deep = physics.MaxThrowRange - 0.5f;

            Assert.Less(CameraFraming.MaxYawAt(deep), physics.MaxYawDegrees,
                "If the throw were the tighter limit, the screen check would be redundant "
                + "— and the deep rows would still be off the edge.");

            Object.DestroyImmediate(physics);
        }

        [Test]
        public void The_throw_origin_sits_inside_the_frame()
        {
            // Sanity on the constants themselves: the ring leaves the hand somewhere the
            // player can see it.
            Assert.Greater(CameraFraming.HalfWidthAt(CameraFraming.ThrowOriginZ), 0.3f);
            Assert.Less(CameraFraming.ThrowOriginZ, 0f);
            Assert.Less(CameraFraming.CameraZ, CameraFraming.ThrowOriginZ);
        }
    }
}
