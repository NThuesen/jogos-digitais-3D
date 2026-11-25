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
    /// Checks that the lane either side of the booth is on screen and not sealed off.
    ///
    /// This exists because of a bug that survived four rounds of measurement, three
    /// screenshots and a debug-coloured capture. The booth's canvas wall was narrowed
    /// from 3.4 m to 1.65 m specifically so that neighbours could be seen past it, and
    /// every placement was then computed against that number — but the wall was a
    /// serialized field, and the scene still carried the old 3.4 from before the
    /// change. The C# default was never read at runtime. Every stall built itself
    /// against a window that did not exist, ended up a metre and a half off the edge
    /// of the frame, and no amount of staring at captures explained why.
    ///
    /// The lesson is what these tests encode: on this project, a tuned constant in
    /// source is not the value the game runs on. So nothing here reads
    /// <c>WorldBuilder</c>'s fields. Everything is measured off the geometry that was
    /// actually built, in the scene as it will actually ship.
    /// </summary>
    public sealed class FairwayFramingTests
    {
        private Camera camera;
        private Bounds wall;
        private readonly List<Transform> lane = new List<Transform>();
        private readonly List<Transform> floorProps = new List<Transform>();
        private readonly List<Transform> occluders = new List<Transform>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Main");

            yield return null;
            yield return null;
            yield return null;

            camera = Camera.main;
            Assert.IsNotNull(camera, "No main camera in the scene.");

            GameObject backdrop = GameObject.Find("Backdrop");
            Assert.IsNotNull(backdrop, "The scene has no 'Backdrop'.");

            Transform found = backdrop.transform.Find("Wall");
            Assert.IsNotNull(found, "The backdrop has no 'Wall'.");
            wall = found.GetComponent<Renderer>().bounds;

            lane.Clear();

            foreach (string rootName in new[] { "Neighbours", "Fairway" })
            {
                GameObject root = GameObject.Find(rootName);
                Assert.IsNotNull(root, $"The scene has no '{rootName}' root.");
                Collect(root.transform, lane);
            }

            Assert.Greater(lane.Count, 14, "The lane is nearly empty.");

            floorProps.Clear();
            GameObject boothFloor = GameObject.Find("Booth Floor");
            Assert.IsNotNull(boothFloor, "The scene has no 'Booth Floor' root.");
            Collect(boothFloor.transform, floorProps);

            // Two, not five. How much clutter stands on the booth floor is a
            // SceneryDensity decision — at the lowest setting only the two hoops lying
            // flat survive, because standing things in the corners are what a small
            // screen reads as noise. This guard is against building *nothing*; whether
            // each piece can be seen is the occlusion test's job, not a count's.
            Assert.GreaterOrEqual(floorProps.Count, 2, "The booth floor is empty.");

            // Painted into the occlusion check but never asserted on. Without them the
            // booth's own shelf and crates are invisible to the model, and a prop
            // parked squarely behind a crate would be scored as if the crate were not
            // there.
            occluders.Clear();

            foreach (string rootName in new[]
            {
                "Board", "Counter", "Backdrop", "Booth Frame", "Back Shelf",
                "Corner_L", "Corner_R", "Hanging Prizes", "Bunting", "String Lights",
                "Balloons", "Deck", "Booth Sides", "Apron"
            })
            {
                GameObject root = GameObject.Find(rootName);

                if (root != null)
                {
                    occluders.Add(root.transform);
                }
            }
        }

        /// <summary>
        /// The wall the player's own booth is backed by does not overhang the frame.
        ///
        /// This is the whole bug in one assertion. A wall wider than the camera can see
        /// at its own depth seals the booth: there is no angle at which anything beside
        /// it can be seen, so every other test here would pass on scenery placed in the
        /// dark, and the fairground could only ever be built above the roofline.
        ///
        /// It is deliberately asserted with a margin rather than at exactly the frame
        /// edge. A wall that just reaches the edge leaves a window of zero.
        /// </summary>
        [UnityTest]
        public IEnumerator The_booth_wall_leaves_a_window_either_side()
        {
            float frame = CameraFraming.HalfWidthAt(wall.center.z);
            float half = wall.extents.x;

            Assert.Less(half, frame,
                $"The booth's wall is {half:F2} m either side of centre but the camera " +
                $"only sees {frame:F2} m at that depth, so it overhangs the frame and " +
                "nothing can be seen beside it. This is what a serialized value left " +
                "over from before the wall was narrowed looks like.");

            // A tenth of the half-frame is about fifty pixels on a 540-wide portrait
            // screen: enough for a stall front, a lamp and a figure, which is what the
            // lane is made of.
            Assert.Less(half / frame, 0.9f,
                $"The wall fills {half / frame:P0} of the half-frame, leaving too " +
                "little beside it for anything to read.");

            yield return null;
        }

        /// <summary>
        /// Every piece of the lane is at least partly inside the frame a real handset
        /// shows.
        ///
        /// Partly, not wholly: most of these are meant to be cut by the wall on one
        /// side and by the screen edge on the other — that is what makes the lane read
        /// as continuing past the shot rather than as a diorama. What is not intended
        /// is a stall built entirely outside the frame.
        /// </summary>
        [UnityTest]
        public IEnumerator Every_lane_element_is_inside_the_frame()
        {
            Plane[] planes = NarrowFrustum();
            List<string> missing = new List<string>();

            for (int i = 0; i < lane.Count; i++)
            {
                Bounds bounds = ElementBounds(lane[i]);

                if (!GeometryUtility.TestPlanesAABB(planes, bounds))
                {
                    missing.Add(
                        $"{lane[i].name} centred at {bounds.center} " +
                        $"(the frame is {CameraFraming.HalfWidthAt(bounds.center.z):F2} m " +
                        "either side of centre at that depth)");
                }
            }

            Assert.IsEmpty(missing,
                "Built outside the frame, so no player will ever see it:\n" +
                string.Join("\n", missing));

            yield return null;
        }

        /// <summary>
        /// Every piece of the lane reaches past the booth's own wall.
        ///
        /// Being inside the frame is not enough on its own. The wall throws an angular
        /// shadow that widens with distance — a straight line from the lens over its
        /// edge — and anything wholly inside that shadow is hidden by the player's own
        /// booth however well framed it is. That failure and being off screen look
        /// identical from a screenshot, which is why both are checked separately.
        /// </summary>
        [UnityTest]
        public IEnumerator Every_lane_element_reaches_past_the_booth_wall()
        {
            List<string> hidden = new List<string>();

            for (int i = 0; i < lane.Count; i++)
            {
                Bounds bounds = ElementBounds(lane[i]);
                float shadow = ShadowAt(bounds.center.z);
                float reach = Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x));

                if (reach <= shadow)
                {
                    hidden.Add(
                        $"{lane[i].name} reaches {reach:F2} m from centre at z={bounds.center.z:F1}, " +
                        $"where the booth hides everything within {shadow:F2} m");
                }
            }

            Assert.IsEmpty(hidden,
                "Behind the player's own booth, so no player will ever see it:\n" +
                string.Join("\n", hidden));

            yield return null;
        }

        /// <summary>
        /// The lane has depth rather than being one row of props.
        ///
        /// The window beside the booth is a fixed fraction of the screen at every
        /// distance, so depth is the only dimension the lane can actually grow in — a
        /// second stall placed level with the first is drawn in the same slot at the
        /// same size and simply hides it. This asserts the content is spread down the
        /// lane instead.
        /// </summary>
        [UnityTest]
        public IEnumerator The_lane_recedes()
        {
            List<float> depths = new List<float>();

            for (int i = 0; i < lane.Count; i++)
            {
                depths.Add(ElementBounds(lane[i]).center.z);
            }

            depths.Sort();

            Assert.Less(depths[0], 8f, "Nothing in the lane is close enough to read as next door.");
            Assert.Greater(depths[depths.Count - 1], 16f, "The lane stops before it reaches the skyline.");

            yield return null;
        }

        /// <summary>
        /// Nothing in the lane stands where a ring is thrown.
        ///
        /// The window converges towards the lens, so a prop placed by fraction rather
        /// than by metres drifts inwards as it comes forward and ends up in the play
        /// area. Scenery there is not just wrong to look at: <c>CreatePrimitive</c>
        /// strips colliders, so a ring would pass straight through it.
        /// </summary>
        [UnityTest]
        public IEnumerator Nothing_in_the_lane_comes_forward_of_the_booth()
        {
            List<string> intruders = new List<string>();

            for (int i = 0; i < lane.Count; i++)
            {
                Bounds bounds = ElementBounds(lane[i]);

                if (bounds.min.z < wall.min.z)
                {
                    intruders.Add($"{lane[i].name} reaches forward to z={bounds.min.z:F1}");
                }
            }

            Assert.IsEmpty(intruders,
                "In front of the booth's back wall, where the rings are:\n" +
                string.Join("\n", intruders));

            yield return null;
        }

        /// <summary>
        /// Nothing in the lane is standing behind something else in the lane.
        ///
        /// This is the failure the other tests here do not catch, and it cost more than
        /// all of them put together. Every element can be inside the frame, past the
        /// wall, correctly lit and correctly coloured, and still be worth nothing —
        /// because the window is one column a tenth of the screen wide, and the nearest
        /// thing at any given height covers the whole of it. A first pass at filling
        /// this lane built two stalls, a kiosk, two lamp posts, two figures and six far
        /// silhouettes at exactly zero visible pixels, and the only reason anyone found
        /// out was a debug-coloured capture.
        ///
        /// So the check is a painter's-order rasterisation at the narrowest aspect:
        /// draw far to near into a coarse grid, then count the cells each
        /// element still owns. The lane and the booth's own floor clutter are both
        /// subjects; the rest of the booth is painted as an occluder and owns nothing.
        ///
        /// Per *renderer*, not per element. One box around a whole element is not a
        /// conservative approximation of it, it is a wrong one: a festoon is a cord and
        /// eight bulbs strung over ten metres, and its bounding box is a solid slab ten
        /// metres deep that swallows everything behind it. Painting each primitive
        /// separately keeps sparse things sparse. It is still boxes rather than
        /// geometry, so it can under-report a sliver showing past a corner — but it
        /// cannot invent one, and an element it scores at zero is genuinely not there.
        /// </summary>
        [UnityTest]
        public IEnumerator Nothing_in_the_lane_is_hidden_behind_the_rest_of_it()
        {
            const int columns = 108;
            const int rows = 240;

            Matrix4x4 viewProjection = NarrowViewProjection();
            int[] owner = new int[columns * rows];

            for (int i = 0; i < owner.Length; i++)
            {
                owner[i] = -1;
            }

            // The booth's own wall occludes the inner edge of the window and has to be
            // in the painting, or every element would appear to reach further across
            // the frame than it does. It is index -2: an occluder nobody asserts on.
            List<Transform> subjects = new List<Transform>(lane);
            subjects.AddRange(floorProps);

            List<KeyValuePair<int, Renderer>> painting = new List<KeyValuePair<int, Renderer>>();

            for (int i = 0; i < subjects.Count; i++)
            {
                foreach (Renderer part in subjects[i].GetComponentsInChildren<Renderer>())
                {
                    painting.Add(new KeyValuePair<int, Renderer>(i, part));
                }
            }

            // The booth's own geometry: painted, and owned by nobody.
            for (int i = 0; i < occluders.Count; i++)
            {
                foreach (Renderer part in occluders[i].GetComponentsInChildren<Renderer>())
                {
                    painting.Add(new KeyValuePair<int, Renderer>(-2, part));
                }
            }

            // Far to near, so nearer boxes overwrite.
            painting.Sort((a, b) => b.Value.bounds.center.z.CompareTo(a.Value.bounds.center.z));

            foreach (KeyValuePair<int, Renderer> entry in painting)
            {
                Rect rect = ScreenRect(viewProjection, entry.Value);

                int x0 = Mathf.Clamp(Mathf.FloorToInt(rect.xMin * columns), 0, columns);
                int x1 = Mathf.Clamp(Mathf.CeilToInt(rect.xMax * columns), 0, columns);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(rect.yMin * rows), 0, rows);
                int y1 = Mathf.Clamp(Mathf.CeilToInt(rect.yMax * rows), 0, rows);

                for (int y = y0; y < y1; y++)
                {
                    for (int x = x0; x < x1; x++)
                    {
                        owner[y * columns + x] = entry.Key;
                    }
                }
            }

            int[] cells = new int[subjects.Count];

            for (int i = 0; i < owner.Length; i++)
            {
                if (owner[i] >= 0)
                {
                    cells[owner[i]]++;
                }
            }

            // Twenty cells of 108x240 is about a tenth of a percent of the frame: a
            // lamp head, or a pole seen edge on. Below that an element is not scenery,
            // it is a rounding error someone spent an afternoon placing.
            const int visibleFloor = 20;
            List<string> buried = new List<string>();

            // Written out on every run, passing or not. The margins here are the only
            // way to tell a lane that is comfortably full from one that is one prop
            // away from burying something, and that difference is invisible in a
            // screenshot.
            for (int i = 0; i < subjects.Count; i++)
            {
                TestContext.Out.WriteLine($"{subjects[i].name,-22} {cells[i],6} cells");
            }

            for (int i = 0; i < subjects.Count; i++)
            {
                if (cells[i] < visibleFloor)
                {
                    buried.Add(
                        $"{subjects[i].name} at z={ElementBounds(subjects[i]).center.z:F1} " +
                        $"owns {cells[i]} of {columns * rows} cells");
                }
            }

            Assert.IsEmpty(buried,
                "Built behind something else in the lane, so no player will ever see it:\n" +
                string.Join("\n", buried));

            yield return null;
        }

        /// <summary>
        /// One entry per named element, skipping the grouping transforms the lane is
        /// organised under. A stall is one thing; its awning is not.
        /// </summary>
        private void Collect(Transform root, List<Transform> into)
        {
            foreach (Transform child in root)
            {
                if (child.GetComponentInChildren<Renderer>() == null)
                {
                    continue;
                }

                if (child.GetComponent<Renderer>() == null && child.childCount > 0
                    && IsGroup(child.name))
                {
                    Collect(child, into);
                    continue;
                }

                into.Add(child);
            }
        }

        private static bool IsGroup(string name)
        {
            return name == "Structures" || name == "Lights" || name == "Far Fairway";
        }

        /// <summary>
        /// How far from centre the booth's own wall hides, at a given depth.
        ///
        /// Measured off the built wall rather than off the field it was built from, so
        /// a scene carrying a stale serialized width is caught here too.
        /// </summary>
        private float ShadowAt(float z)
        {
            return wall.extents.x / (wall.center.z - CameraFraming.CameraZ)
                * (z - CameraFraming.CameraZ);
        }

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
        /// Where one renderer lands on screen, as a 0..1 rect.
        ///
        /// Projected from the renderer's *local* box through its own transform, not
        /// from its world-axis-aligned bounds. The difference is not academic here: the
        /// booth's side panels are thin slabs laid along the camera's sight line so they
        /// are seen edge on, and their world AABB is a box a third of a metre wide and
        /// over a metre deep. Painted from that, they cover half the window beside the
        /// booth and score the stalls out there at zero — a model artifact that reads
        /// exactly like a real bug.
        ///
        /// Every corner is projected rather than the centre: a box near the edge of a
        /// perspective frustum does not project to a rectangle around its own centre.
        /// </summary>
        private static Rect ScreenRect(Matrix4x4 viewProjection, Renderer renderer)
        {
            Bounds local = renderer.localBounds;
            Matrix4x4 model = renderer.localToWorldMatrix;

            float xMin = float.MaxValue, xMax = float.MinValue;
            float yMin = float.MaxValue, yMax = float.MinValue;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = model.MultiplyPoint3x4(new Vector3(
                    (i & 1) == 0 ? local.min.x : local.max.x,
                    (i & 2) == 0 ? local.min.y : local.max.y,
                    (i & 4) == 0 ? local.min.z : local.max.z));

                Vector4 clip = viewProjection * new Vector4(corner.x, corner.y, corner.z, 1f);

                if (clip.w <= 0.001f)
                {
                    continue;
                }

                float x = (clip.x / clip.w + 1f) * 0.5f;
                float y = (clip.y / clip.w + 1f) * 0.5f;

                xMin = Mathf.Min(xMin, x); xMax = Mathf.Max(xMax, x);
                yMin = Mathf.Min(yMin, y); yMax = Mathf.Max(yMax, y);
            }

            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private Matrix4x4 NarrowViewProjection()
        {
            Matrix4x4 projection = Matrix4x4.Perspective(
                camera.fieldOfView,
                CameraFraming.NarrowestAspect,
                camera.nearClipPlane,
                camera.farClipPlane);

            return projection * camera.worldToCameraMatrix;
        }

        /// <summary>
        /// The camera's frustum at the narrowest aspect the game supports, regardless
        /// of the window the test runner opened.
        /// </summary>
        private Plane[] NarrowFrustum()
        {
            return GeometryUtility.CalculateFrustumPlanes(NarrowViewProjection());
        }
    }
}
