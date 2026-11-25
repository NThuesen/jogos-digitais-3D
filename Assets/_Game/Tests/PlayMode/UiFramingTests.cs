using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SlipperyZlope.RingToss.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// The interface, measured against the screen it actually gets.
    ///
    /// The canvas is not as wide as the reference resolution and never was. It
    /// matches height — which is the right setting, because it keeps a layout stable
    /// as phones grow taller — so its width in reference units is 1920 * aspect: 864
    /// at 9:20, 823 at 9:21, and 1080 only at 9:16, which no phone has been for a
    /// decade. The interface was authored against 1080. The shop's panel was 980 wide
    /// and lost 58 units off each edge, its cards were 918 across a 823-unit canvas
    /// and were sliced down both outer columns, and nothing anywhere said so: it
    /// looks like a cropping bug and it is arithmetic.
    ///
    /// This is the same mistake the camera made in phase 10, one layer up, and it is
    /// held the same way — by measuring the built objects at the narrowest supported
    /// aspect rather than by reading the numbers in the source.
    /// </summary>
    public sealed class UiFramingTests
    {
        // 9:21. Narrower than the 9:20 the world is framed for, deliberately: the
        // camera's own limit is a design decision about how much fairground to show,
        // and a button off the edge of the screen is not a design decision.
        private const int Width = 900;
        private const int Height = 2100;

        private RenderTexture view;
        private Camera camera;
        private Canvas canvas;
        private RectTransform canvasRect;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Main");

            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            camera = Camera.main;
            Assert.IsNotNull(camera, "No main camera to size the canvas against.");

            // A ScreenSpaceCamera canvas takes its pixel rect from its camera, so
            // pointing the camera at a texture of the handset's shape is what lays the
            // interface out for that handset. Reading the editor's window instead
            // would measure whatever aspect the machine happens to have.
            view = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = view;

            yield return null;
            Canvas.ForceUpdateCanvases();

            canvas = Object.FindFirstObjectByType<Canvas>();
            Assert.IsNotNull(canvas, "No canvas in the scene.");
            canvasRect = (RectTransform)canvas.transform;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (camera != null)
            {
                camera.targetTexture = null;
            }

            if (view != null)
            {
                view.Release();
                Object.Destroy(view);
            }

            yield return SceneManager.UnloadSceneAsync("Main");
        }

        [UnityTest]
        public IEnumerator Nothing_is_laid_out_wider_than_the_canvas()
        {
            float half = canvasRect.rect.width * 0.5f;
            Assert.Less(half * 2f, 900f, "The canvas should be narrower than the reference at 9:21.");

            List<string> over = new List<string>();

            foreach (RectTransform rect in canvasRect.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect == canvasRect || !HasVisual(rect))
                {
                    continue;
                }

                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, rect);

                if (bounds.size.x <= 0f)
                {
                    continue;
                }

                float spill = Mathf.Max(-half - bounds.min.x, bounds.max.x - half);

                if (spill > 1f)
                {
                    over.Add($"{Path(rect)}: {bounds.size.x:F0} wide, {spill:F0} past the edge");
                }
            }

            Assert.IsEmpty(over,
                $"The canvas is {half * 2f:F0} units wide here. These are outside it:\n  " +
                string.Join("\n  ", over));

            yield return null;
        }

        /// <summary>
        /// Nothing can draw outside the box it was given.
        ///
        /// Stated as an invariant on the labels rather than as a measurement of
        /// today's strings, because that is the only version that survives a five
        /// digit score, a longer cosmetic name or a translation. Wrapping is off
        /// across this interface, which keeps single-line labels on one line and also
        /// means TMP will happily draw twice the width of the rect over whatever is
        /// beside it. Either the label shrinks or it wraps.
        /// </summary>
        [UnityTest]
        public IEnumerator Every_label_either_shrinks_or_wraps()
        {
            List<string> loose = new List<string>();

            foreach (TMP_Text label in canvasRect.GetComponentsInChildren<TMP_Text>(true))
            {
                bool contained = label.enableAutoSizing
                                 || label.textWrappingMode != TextWrappingModes.NoWrap;

                if (!contained)
                {
                    loose.Add(Path(label.rectTransform));
                }
            }

            Assert.IsEmpty(loose,
                "These labels have wrapping off and auto-sizing off, so nothing stops " +
                "them drawing outside their box:\n  " + string.Join("\n  ", loose));

            yield return null;
        }

        /// <summary>
        /// The payout line and the button under it, which used to overlap by 30 units.
        ///
        /// One is measured down from the top of the results panel and the other up
        /// from the bottom, so nothing in the source connects them, and every results
        /// screen in the game showed "+9 TICKETS" sliced through the middle.
        /// </summary>
        [UnityTest]
        public IEnumerator The_payout_line_clears_the_next_button()
        {
            RectTransform tickets = Find("Result/Panel/Tickets");
            RectTransform next = Find("Result/Panel/Next");

            Bounds above = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, tickets);
            Bounds below = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, next);

            Assert.Greater(above.min.y, below.max.y,
                $"The payout line ({above.min.y:F0}) is not above the NEXT button " +
                $"({below.max.y:F0}). They overlap by {below.max.y - above.min.y:F0} units.");

            yield return null;
        }

        /// <summary>
        /// Every button is big enough to hit. About 44pt, which is a tenth of the
        /// width of a phone, and is the number both platforms publish.
        /// </summary>
        [UnityTest]
        public IEnumerator Every_button_is_big_enough_for_a_thumb()
        {
            float minimum = canvasRect.rect.width * 0.09f;
            List<string> small = new List<string>();

            foreach (Selectable target in canvasRect.GetComponentsInChildren<Selectable>(true))
            {
                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    canvasRect, target.transform);

                float shortest = Mathf.Min(bounds.size.x, bounds.size.y);

                if (shortest < minimum - 1f)
                {
                    small.Add($"{Path((RectTransform)target.transform)}: " +
                              $"{bounds.size.x:F0}x{bounds.size.y:F0}");
                }
            }

            Assert.IsEmpty(small,
                $"A target should be at least {minimum:F0} units across. These are not:\n  " +
                string.Join("\n  ", small));

            yield return null;
        }

        /// <summary>Anything with a graphic on it. A bare layout rect can be any size.</summary>
        private static bool HasVisual(RectTransform rect) => rect.GetComponent<Graphic>() != null;

        private RectTransform Find(string path)
        {
            Transform found = canvasRect.Find(path);
            Assert.IsNotNull(found, $"No '{path}' under the canvas.");
            return (RectTransform)found;
        }

        private string Path(RectTransform rect)
        {
            string path = rect.name;

            for (Transform node = rect.parent; node != null && node != canvasRect; node = node.parent)
            {
                path = node.name + "/" + path;
            }

            return path;
        }
    }
}
