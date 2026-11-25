using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SlipperyZlope.RingToss.Config;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// Checks that the fairground behind the booth is actually on screen.
    ///
    /// This test exists because "the scenery looks empty" turned out to have a
    /// measurable cause rather than an aesthetic one: an audit of the skyline found
    /// two of its nine pieces built entirely outside the camera's frame and four more
    /// clipped by the edge. Work had been going into scenery nobody could ever see,
    /// and the only way anyone noticed was by looking at a screenshot and disagreeing
    /// about it.
    ///
    /// The frame is checked at <see cref="CameraFraming.NarrowestAspect"/> rather than
    /// at whatever aspect the test runner's window happens to be, for the same reason
    /// the capture tool is: the field of view is vertical, so a taller screen shows
    /// less width, and a check that passes at 9:16 proves nothing about a real phone.
    /// </summary>
    public sealed class SkylineFramingTests
    {
        private Camera camera;
        private readonly List<Renderer> skyline = new List<Renderer>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Main");

            yield return null;
            yield return null;
            yield return null;

            camera = Camera.main;
            Assert.IsNotNull(camera, "No main camera in the scene.");

            skyline.Clear();

            foreach (string rootName in new[] { "Skyline", "Skyline Moving" })
            {
                GameObject root = GameObject.Find(rootName);
                Assert.IsNotNull(root, $"The scene has no '{rootName}' root.");

                foreach (Transform element in root.transform)
                {
                    // One entry per named element — a tent, a wheel, a stall row — not
                    // per primitive. A single spoke off the edge of a wheel that is
                    // otherwise in shot is not a bug.
                    Renderer[] parts = element.GetComponentsInChildren<Renderer>();

                    if (parts.Length > 0)
                    {
                        skyline.Add(parts[0]);
                    }
                }
            }

            Assert.Greater(skyline.Count, 8, "The skyline is nearly empty.");
        }

        /// <summary>
        /// Every element is at least partly inside the frame a real handset shows.
        ///
        /// Partly, not wholly: a ferris wheel sliced by the booth's roofline is the
        /// intended look, and a big top running off the edge reads as a fair that
        /// continues past the shot. What is not intended is a shape that is outside
        /// the frame altogether.
        /// </summary>
        [UnityTest]
        public IEnumerator Every_skyline_element_is_inside_the_frame()
        {
            Plane[] planes = NarrowFrustum();
            List<string> missing = new List<string>();

            for (int i = 0; i < skyline.Count; i++)
            {
                Transform element = skyline[i].transform.parent;
                Bounds bounds = ElementBounds(element);

                if (!GeometryUtility.TestPlanesAABB(planes, bounds))
                {
                    missing.Add(
                        $"{Path(element)} centred at {bounds.center} " +
                        $"(the frame is {CameraFraming.HalfWidthAt(bounds.center.z):F2} m " +
                        $"either side of centre at that depth)");
                }
            }

            Assert.IsEmpty(missing,
                "Built outside the frame, so no player will ever see it:\n" +
                string.Join("\n", missing));

            yield return null;
        }

        /// <summary>
        /// Nothing in the skyline pokes out below the booth's roofline into the play
        /// area, which would put fairground scenery on top of the board.
        /// </summary>
        [UnityTest]
        public IEnumerator No_skyline_element_hangs_below_the_booth_roofline()
        {
            List<string> intruders = new List<string>();

            for (int i = 0; i < skyline.Count; i++)
            {
                Transform element = skyline[i].transform.parent;
                Bounds bounds = ElementBounds(element);

                // The sight line over the beam, at this element's depth. Anything whose
                // *top* is under it is hidden, which is fine and expected — the layers
                // are built down to the ground. What must not happen is an element
                // whose top is high enough to be seen appearing where the booth is.
                float roofline = CameraFraming.SightLineAt(3.3f, 3.98f, bounds.center.z);

                if (bounds.max.y > roofline && bounds.center.z < 8f)
                {
                    intruders.Add($"{Path(element)} at z={bounds.center.z:F1}");
                }
            }

            Assert.IsEmpty(intruders,
                "Skyline scenery in front of the booth's backdrop:\n" + string.Join("\n", intruders));

            yield return null;
        }

        /// <summary>
        /// The three layers are at genuinely different distances.
        ///
        /// Two layers a metre apart are one layer drawn twice, and the parallax that
        /// makes a background read as distance disappears.
        /// </summary>
        [UnityTest]
        public IEnumerator The_layers_sit_at_distinct_depths()
        {
            List<float> depths = new List<float>();

            for (int i = 0; i < skyline.Count; i++)
            {
                depths.Add(skyline[i].transform.parent.position.z);
            }

            depths.Sort();

            Assert.Greater(depths[depths.Count - 1] - depths[0], 20f,
                "The whole skyline is within twenty metres of itself, so it is one layer.");

            yield return null;
        }

        /// <summary>
        /// The bounds of a whole element, in world space.
        ///
        /// Encapsulating the children rather than asking for the root's own renderer:
        /// most of these are empty transforms with the geometry hung underneath.
        /// </summary>
        private static Bounds ElementBounds(Transform element)
        {
            Renderer[] parts = element.GetComponentsInChildren<Renderer>();
            Bounds bounds = parts[0].bounds;

            for (int i = 1; i < parts.Length; i++)
            {
                bounds.Encapsulate(parts[i].bounds);
            }

            return bounds;
        }

        /// <summary>
        /// The camera's frustum at the narrowest aspect the game supports, regardless
        /// of the window the test runner opened.
        /// </summary>
        private Plane[] NarrowFrustum()
        {
            Matrix4x4 projection = Matrix4x4.Perspective(
                camera.fieldOfView,
                CameraFraming.NarrowestAspect,
                camera.nearClipPlane,
                camera.farClipPlane);

            return GeometryUtility.CalculateFrustumPlanes(projection * camera.worldToCameraMatrix);
        }

        private static string Path(Transform element)
        {
            return element.parent == null
                ? element.name
                : $"{element.parent.name}/{element.name}";
        }
    }
}
