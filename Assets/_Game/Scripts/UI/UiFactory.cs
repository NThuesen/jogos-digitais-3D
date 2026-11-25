using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlipperyZlope.RingToss.UI
{
    /// <summary>
    /// Builders for the interface elements.
    ///
    /// The UI is assembled in code for the same reason the rest of the scene is: a
    /// prefab hierarchy's layout lives in serialized YAML where it cannot be read or
    /// diffed, and this interface is small enough that the code is shorter than the
    /// prefab would be.
    /// </summary>
    public static class UiFactory
    {
        /// <summary>
        /// How wide the canvas actually is, in the reference units everything below
        /// is written in.
        ///
        /// It is not the reference resolution, and getting that wrong is the single
        /// most expensive mistake available in a portrait layout. The canvas matches
        /// height — the right choice, because it keeps a layout stable as phones grow
        /// taller — which makes the width 1920 * aspect: 864 units at 9:20, 823 at
        /// 9:21. A panel laid out at the reference 1080 loses a quarter of itself off
        /// both edges of every modern handset, and it looks like a mysterious cropping
        /// bug rather than like arithmetic.
        ///
        /// This is the narrowest supported, so anything that fits here fits anywhere.
        /// </summary>
        public const float CanvasWidth = 823f;

        /// <summary>A full-bleed panel: the whole width, with a margin left at each edge.</summary>
        public const float PanelWidth = 792f;

        /// <summary>Anything sitting inside a panel.</summary>
        public const float ContentWidth = 720f;

        /// <summary>
        /// How far a label may shrink to stay inside its box, as a fraction of the
        /// size it asked for.
        /// </summary>
        private const float AutoFitFloor = 0.62f;

        public static RectTransform Rect(string name, Transform parent)
        {
            GameObject host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);
            return (RectTransform)host.transform;
        }

        /// <summary>A container stretched over its whole parent. The root of a screen.</summary>
        public static RectTransform Screen(string name, Transform parent)
        {
            RectTransform rect = Rect(name, parent);
            Stretch(rect);
            return rect;
        }

        /// <summary>
        /// A container fitted to the part of the screen the hardware is not using.
        ///
        /// Put anything anchored to an edge in one of these. A dimming overlay goes on
        /// the full-bleed parent instead — see <see cref="SafeArea"/>.
        /// </summary>
        public static RectTransform SafeScreen(string name, Transform parent)
        {
            RectTransform rect = Rect(name, parent);
            Stretch(rect);
            rect.gameObject.AddComponent<SafeArea>();
            return rect;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rect = Rect(name, parent);

            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0f
                ? UnityEngine.UI.Image.Type.Sliced
                : UnityEngine.UI.Image.Type.Simple;
            image.raycastTarget = false;

            return image;
        }

        public static TextMeshProUGUI Text(
            string name,
            Transform parent,
            string content,
            float size,
            Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center,
            FontStyles style = FontStyles.Normal,
            bool autoFit = true)
        {
            RectTransform rect = Rect(name, parent);

            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            // Every label shrinks rather than spills.
            //
            // Wrapping is off, which is right for single-line labels and is also a
            // loaded gun: with it off TMP will happily draw a string half again as
            // wide as the rect it was given, straight over whatever is beside it. A
            // five-digit score, a long cosmetic name and any translation at all all
            // do it, and none of them appear in the screenshot the layout was tuned
            // against. Auto-sizing makes the box authoritative, which is what a box
            // is for.
            if (autoFit)
            {
                text.fontSizeMax = size;
                text.fontSizeMin = size * AutoFitFloor;
                text.enableAutoSizing = true;
            }

            return text;
        }

        /// <summary>
        /// A rounded plate to put text on.
        ///
        /// Kept for panels that genuinely are surfaces. It is the wrong tool for a
        /// title screen: a plate over a rendered scene is a black rectangle, and a
        /// black rectangle in front of a fairground looks like a mistake even when it
        /// is doing its job. Use <see cref="Legible"/> there instead.
        /// </summary>
        public static Image Plate(
            string name, Transform parent, Sprite sprite, Color color,
            Vector2 position, Vector2 size, Vector2 anchor = default)
        {
            Image plate = Image(name, parent, sprite, color);
            Vector2 pin = anchor == default ? new Vector2(0.5f, 0.5f) : anchor;
            Place(plate.rectTransform, pin, new Vector2(0.5f, 0.5f), position, size);
            return plate;
        }

        /// <summary>
        /// Gives a label its own contrast, so it can sit on the scene without a plate
        /// behind it.
        ///
        /// A dark outline plus a soft drop shadow, both drawn by the text shader from
        /// the glyph's own distance field. This is the right answer for text over
        /// something that moves and changes colour: the plate approach makes the
        /// contrast a property of a rectangle, and the rectangle then has to be big
        /// enough for the longest string, which is how a title screen ends up looking
        /// like a form.
        ///
        /// The cost is one material instance per label, so this is for the handful of
        /// labels that actually sit on the world — not for anything already on a panel.
        /// </summary>
        public static TextMeshProUGUI Legible(
            TextMeshProUGUI text, float outline = 0.16f, float shadow = 0.8f)
        {
            text.outlineColor = new Color32(8, 6, 16, 255);
            text.outlineWidth = outline;

            // fontMaterial, not fontSharedMaterial: the shared one is the font asset's
            // and writing to it would put an outline on every label in the game.
            Material material = text.fontMaterial;

            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, shadow));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.5f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.5f);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.2f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.4f);

            return text;
        }

        /// <summary>
        /// A button with its own label. The pressed and disabled looks go through the
        /// Button's tint block, which costs nothing beyond the colours themselves.
        /// </summary>
        public static Button Button(
            string name,
            Transform parent,
            Sprite sprite,
            Color background,
            string label,
            float labelSize,
            Color labelColor)
        {
            Image image = Image(name, parent, sprite, background);
            image.raycastTarget = true;

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
            colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            if (!string.IsNullOrEmpty(label))
            {
                TextMeshProUGUI text = Text("Label", image.transform, label, labelSize, labelColor);
                Stretch(text.rectTransform, 10f);
            }

            return button;
        }

        /// <summary>
        /// A vertical scroll view: a masked viewport and a content rect that grows.
        ///
        /// The only scrolling surface in the game, and it exists for one screen — the
        /// legal documents, which are longer than a phone and must not be summarised
        /// into fitting. Everything else in this interface is laid out to fit on
        /// purpose, because a menu that scrolls is a menu whose author has not decided
        /// what matters.
        ///
        /// <see cref="RectMask2D"/> rather than a <c>Mask</c>: it needs no stencil
        /// buffer and no extra draw call, and it clips rectangles, which is all there
        /// is to clip here.
        /// </summary>
        public static ScrollRect Scroll(string name, Transform parent, out RectTransform content)
        {
            RectTransform root = Rect(name, parent);

            RectTransform viewport = Rect("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 40f;

            return scroll;
        }

        public static CanvasGroup Group(RectTransform rect)
        {
            CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            return group;
        }

        /// <summary>Stretches a rect over its parent, with an optional uniform inset.</summary>
        public static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        /// <summary>
        /// Places a rect against a single anchor point. Everything is measured from
        /// that anchor, which is what keeps a layout stable across phone aspect ratios.
        /// </summary>
        public static void Place(
            RectTransform rect,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Stretches across the full width at a fixed height, pinned to one edge.</summary>
        public static void Band(
            RectTransform rect,
            float anchorY,
            float pivotY,
            float y,
            float height,
            float inset = 0f)
        {
            rect.anchorMin = new Vector2(0f, anchorY);
            rect.anchorMax = new Vector2(1f, anchorY);
            rect.pivot = new Vector2(0.5f, pivotY);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(-inset * 2f, height);
        }
    }
}
