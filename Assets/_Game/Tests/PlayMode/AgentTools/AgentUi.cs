// Drop this into a Unity project's Tests (or Runtime) assembly to make a canvas
// legible to `ui_audit`.
//
// It writes one JSON file per screen state into the directory named by the
// AGENT_UI_DIR environment variable. Nothing happens when that variable is
// unset, so it is safe to leave in a shipping build's test assembly.
//
// What it dumps, and why each field is there:
//
//   rect   the element's box in screen pixels, origin bottom left. This is what
//          the layout says.
//   ink    for text, the box the glyphs actually cover. This is what the player
//          sees, and it is routinely wider than the rect: a label given a
//          900-wide box with wrapping off will happily draw 1300 wide and spill
//          over whatever is next to it. Comparing the two is the only way to
//          catch an overflow without reading pixels.
//   order  depth-first index, which is the order a canvas paints in. Two things
//          overlapping is only a defect if the later one is opaque, so the tool
//          needs to know which is later.
//   alpha  the product of every CanvasGroup up the chain. A screen that is
//          faded out is not a screen that is overlapping anything.
//   clip   the screen rect every RectMask2D and Mask above the element cuts it
//          to. A scroll view's content runs far past its viewport; without this
//          a long privacy policy reads as drawn over every button below it.
//
// Sample every screen the game can show, not just the ones that look busy. The
// defects live on the screens nobody re-opens after the first week.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AgentTools
{
    public static class AgentUi
    {
        public static bool Enabled =>
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AGENT_UI_DIR"));

        /// <summary>
        /// Dumps every canvas in the scene. Returns the file written, or null when
        /// the environment variable is unset, so callers never have to branch.
        /// </summary>
        public static string Capture(string name)
        {
            string directory = Environment.GetEnvironmentVariable("AGENT_UI_DIR");

            if (string.IsNullOrEmpty(directory))
            {
                return null;
            }

            Directory.CreateDirectory(directory);

            List<Canvas> roots = Roots();

            // The canvas's own pixel rect, not Screen.
            //
            // A ScreenSpaceCamera canvas is sized by its camera, so pointing that
            // camera at a 1080x2340 render texture lays the interface out for a
            // handset regardless of what window the editor happens to have open —
            // which is the only way to audit a phone layout from a batch run. Reading
            // Screen here would report the editor's window and quietly measure the
            // wrong aspect ratio, which is the most expensive mistake available in a
            // portrait layout.
            Vector2 view = roots.Count > 0 && roots[0].pixelRect.width > 0f
                ? roots[0].pixelRect.size
                : new Vector2(Screen.width, Screen.height);

            StringBuilder json = new StringBuilder();
            json.Append("{\"name\":").Append(Quote(name));
            json.Append(",\"screen\":{\"width\":").Append(Num(view.x))
                .Append(",\"height\":").Append(Num(view.y)).Append('}');

            // Scaled into the view, since the real device insets are a property of the
            // real screen and the view may be a stand-in for a different one.
            Rect safe = Screen.safeArea;
            float sx = Screen.width > 0 ? view.x / Screen.width : 1f;
            float sy = Screen.height > 0 ? view.y / Screen.height : 1f;

            json.Append(",\"safeArea\":{\"x\":").Append(Num(safe.x * sx))
                .Append(",\"y\":").Append(Num(safe.y * sy))
                .Append(",\"width\":").Append(Num(safe.width * sx))
                .Append(",\"height\":").Append(Num(safe.height * sy)).Append('}');

            json.Append(",\"elements\":[");

            int order = 0;
            bool first = true;

            foreach (Canvas canvas in roots)
            {
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera;

                Walk(canvas.transform, canvas.name, camera, 1f, json, ref order, ref first);
            }

            json.Append("]}");

            string path = Path.Combine(directory, name + ".json");
            File.WriteAllText(path, json.ToString());
            return path;
        }

        /// <summary>Root canvases only; a nested canvas is walked as part of its parent.</summary>
        private static List<Canvas> Roots()
        {
            List<Canvas> roots = new List<Canvas>();

            foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (canvas.isRootCanvas)
                {
                    roots.Add(canvas);
                }
            }

            // Sorting order decides which canvas paints over which, and a full-screen
            // advert on order 500 covering the HUD is not an overlap defect.
            roots.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
            return roots;
        }

        private static void Walk(
            Transform node,
            string path,
            Camera camera,
            float alpha,
            StringBuilder json,
            ref int order,
            ref bool first)
        {
            if (!node.gameObject.activeInHierarchy)
            {
                return;
            }

            if (node.TryGetComponent(out CanvasGroup group))
            {
                alpha *= group.alpha;
            }

            if (node is RectTransform rect && node.TryGetComponent(out Graphic graphic))
            {
                if (!first)
                {
                    json.Append(',');
                }

                first = false;
                Append(json, rect, graphic, path, camera, alpha, order);
            }

            order++;

            for (int i = 0; i < node.childCount; i++)
            {
                Transform child = node.GetChild(i);
                Walk(child, path + "/" + child.name, camera, alpha, json, ref order, ref first);
            }
        }

        private static void Append(
            StringBuilder json,
            RectTransform rect,
            Graphic graphic,
            string path,
            Camera camera,
            float alpha,
            int order)
        {
            TMP_Text text = graphic as TMP_Text;

            json.Append("{\"path\":").Append(Quote(path));
            json.Append(",\"order\":").Append(order);
            json.Append(",\"kind\":").Append(Quote(text != null ? "text" : "image"));

            float opacity = alpha * graphic.color.a;
            json.Append(",\"alpha\":").Append(Num(opacity));
            json.Append(",\"visible\":").Append(opacity > 0.02f && graphic.enabled ? "true" : "false");

            Bounds screenRect = ScreenBounds(Corners(rect), camera);
            json.Append(",\"rect\":").Append(RectJson(screenRect));

            Bounds? clip = ClipOf(rect, camera);

            if (clip.HasValue)
            {
                json.Append(",\"clip\":").Append(RectJson(clip.Value));
            }

            Color colour = graphic.color;
            json.Append(",\"color\":[")
                .Append(Num(colour.r)).Append(',').Append(Num(colour.g)).Append(',')
                .Append(Num(colour.b)).Append(',').Append(Num(colour.a)).Append(']');

            if (text != null)
            {
                // The glyph box, not the layout box. ForceMeshUpdate first, because a
                // label whose text was set this frame has not laid out yet and would
                // report the previous string's bounds.
                text.ForceMeshUpdate();

                json.Append(",\"text\":").Append(Quote(text.text));
                json.Append(",\"fontSize\":").Append(Num(text.fontSize));

                // Whether the label carries its own contrast.
                //
                // A label with a dark outline or a drop shadow does not need anything
                // behind it, and a tool that cannot see this reports every well-made
                // title screen as unreadable. Read from materialForRendering, which is
                // whatever is actually being drawn with — asking for `fontMaterial`
                // would *create* an instance and change what is being measured.
                Material drawn = text.materialForRendering;

                bool outlined = drawn != null
                                && drawn.HasProperty(ShaderUtilities.ID_OutlineWidth)
                                && drawn.GetFloat(ShaderUtilities.ID_OutlineWidth) > 0.01f;

                bool shadowed = drawn != null
                                && drawn.IsKeywordEnabled(ShaderUtilities.Keyword_Underlay);

                json.Append(",\"outline\":").Append(outlined ? "true" : "false");
                json.Append(",\"shadow\":").Append(shadowed ? "true" : "false");

                if (text.textInfo != null && text.textInfo.characterCount > 0)
                {
                    Bounds local = text.textBounds;
                    Vector3[] ink =
                    {
                        text.transform.TransformPoint(new Vector3(local.min.x, local.min.y, 0f)),
                        text.transform.TransformPoint(new Vector3(local.min.x, local.max.y, 0f)),
                        text.transform.TransformPoint(new Vector3(local.max.x, local.max.y, 0f)),
                        text.transform.TransformPoint(new Vector3(local.max.x, local.min.y, 0f)),
                    };

                    json.Append(",\"ink\":").Append(RectJson(ScreenBounds(ink, camera)));
                }
            }
            else
            {
                Image image = graphic as Image;

                if (image != null)
                {
                    json.Append(",\"sprite\":").Append(Quote(image.sprite == null ? "" : image.sprite.name));
                }
            }

            bool interactive = rect.TryGetComponent(out Selectable selectable)
                               && selectable.enabled
                               && selectable.interactable;

            json.Append(",\"interactive\":").Append(interactive ? "true" : "false");
            json.Append(",\"raycast\":").Append(graphic.raycastTarget ? "true" : "false");
            json.Append('}');
        }

        /// <summary>
        /// The screen rect every RectMask2D and Mask above this element clips it to, or
        /// null when nothing does. The intersection of all of them, since nested masks
        /// each cut what the one above let through.
        /// </summary>
        private static Bounds? ClipOf(RectTransform rect, Camera camera)
        {
            Bounds? clip = null;

            for (Transform node = rect.parent; node != null; node = node.parent)
            {
                bool clips = (node.TryGetComponent(out RectMask2D rectMask) && rectMask.isActiveAndEnabled)
                             || (node.TryGetComponent(out Mask mask) && mask.isActiveAndEnabled);

                if (!clips || !(node is RectTransform area))
                {
                    continue;
                }

                Bounds bounds = ScreenBounds(Corners(area), camera);

                if (clip.HasValue)
                {
                    Vector3 min = Vector3.Max(clip.Value.min, bounds.min);
                    Vector3 max = Vector3.Max(min, Vector3.Min(clip.Value.max, bounds.max));
                    Bounds both = new Bounds();
                    both.SetMinMax(min, max);
                    clip = both;
                }
                else
                {
                    clip = bounds;
                }
            }

            return clip;
        }

        private static Vector3[] Corners(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners;
        }

        /// <summary>
        /// Screen pixels, origin bottom left, for either canvas render mode.
        ///
        /// A ScreenSpaceOverlay canvas already has its world space in pixels, so the
        /// camera argument is null there and the points pass through untouched. Any
        /// other mode has to be projected, and projecting an overlay canvas through a
        /// camera produces coordinates in the thousands.
        /// </summary>
        private static Bounds ScreenBounds(Vector3[] corners, Camera camera)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            foreach (Vector3 corner in corners)
            {
                Vector2 point = camera == null
                    ? new Vector2(corner.x, corner.y)
                    : RectTransformUtility.WorldToScreenPoint(camera, corner);

                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            Bounds bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        private static string RectJson(Bounds bounds) =>
            "{\"x\":" + Num(bounds.min.x) +
            ",\"y\":" + Num(bounds.min.y) +
            ",\"width\":" + Num(bounds.size.x) +
            ",\"height\":" + Num(bounds.size.y) + "}";

        private static string Num(float value) =>
            float.IsFinite(value) ? value.ToString("0.##", CultureInfo.InvariantCulture) : "0";

        private static string Quote(string value)
        {
            StringBuilder quoted = new StringBuilder("\"");

            foreach (char c in value ?? string.Empty)
            {
                switch (c)
                {
                    case '"': quoted.Append("\\\""); break;
                    case '\\': quoted.Append("\\\\"); break;
                    case '\n': quoted.Append("\\n"); break;
                    case '\r': quoted.Append("\\r"); break;
                    case '\t': quoted.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            quoted.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            quoted.Append(c);
                        }

                        break;
                }
            }

            return quoted.Append('"').ToString();
        }
    }
}
