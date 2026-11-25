using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GameHub
{
    /// <summary>Builds the game selection screen in the menu scene.</summary>
    public sealed class MainMenu : MonoBehaviour
    {
        private static readonly Color Background = new Color32(12, 16, 23, 255);
        private static readonly Color TextColor = new Color32(245, 244, 238, 255);
        private static readonly Color MutedColor = new Color32(180, 192, 201, 255);
        private static readonly Color PocketAccent = new Color32(167, 239, 184, 255);
        private static readonly Color RingAccent = new Color32(255, 209, 138, 255);

        [SerializeField] private Camera menuCamera;

        private Canvas canvas;
        private RectTransform safeRoot;
        private RectTransform header;
        private Button pocketPutt;
        private Button ringToss;
        private Sprite roundedSprite;
        private Texture2D roundedTexture;
        private Texture2D backdropTexture;
        private Texture2D lightTexture;
        private Rect lastSafeArea;
        private Vector2Int lastResolution;
        private bool launching;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            if (menuCamera != null)
            {
                menuCamera.clearFlags = CameraClearFlags.SolidColor;
                menuCamera.backgroundColor = Background;
            }

            roundedTexture = CreateTexture(128, (x, y) =>
            {
                const float extent = 0.17f;
                float distance = new Vector2(Mathf.Max(Mathf.Abs(x - 0.5f) - extent, 0),
                    Mathf.Max(Mathf.Abs(y - 0.5f) - extent, 0)).magnitude;
                return new Color(1f, 1f, 1f, Mathf.Clamp01((0.33f - distance) * 128f + 0.5f));
            });
            roundedSprite = Sprite.Create(roundedTexture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(43, 43, 43, 43));

            backdropTexture = CreateTexture(128, (x, y) =>
            {
                Color bottom = new Color32(30, 20, 33, 255);
                Color top = new Color32(20, 46, 45, 255);
                float teal = 0.63f * Mathf.Exp(-5f * new Vector2(x - 0.88f, y - 1.05f).sqrMagnitude);
                float plum = 0.70f * Mathf.Exp(-5f * new Vector2(x - 0.06f, y + 0.06f).sqrMagnitude);
                return Color.Lerp(Color.Lerp(Background, top, teal), bottom, plum);
            });
            lightTexture = CreateTexture(64, (x, y) =>
            {
                float glow = Mathf.Max(0, 1f - new Vector2(x - 0.5f, y - 0.5f).sqrMagnitude * 4f);
                return new Color(1f, 1f, 1f, glow * glow);
            });

            EnsureEventSystem();
            BuildCanvas();
            Resize();
            EventSystem.current.SetSelectedGameObject(pocketPutt.gameObject);
        }

        private void Update()
        {
            Resize();
        }

        private void OnDestroy()
        {
            Destroy(roundedSprite);
            Destroy(roundedTexture);
            Destroy(backdropTexture);
            Destroy(lightTexture);
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var host = new GameObject("Event System");
            host.transform.SetParent(transform, false);
            host.AddComponent<EventSystem>();
            host.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private void BuildCanvas()
        {
            RectTransform root = Element(transform, "Menu Canvas");
            root.gameObject.layer = LayerMask.NameToLayer("UI");
            canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            root.gameObject.AddComponent<GraphicRaycaster>();

            Stretch(Picture(root, "Backdrop", backdropTexture, Color.white).rectTransform, Vector2.zero, Vector2.one);
            safeRoot = Element(root, "Safe Area");
            header = Element(safeRoot, "Heading");
            RectTransform rule = Solid(header, "Accent", PocketAccent);
            Place(rule, new Vector2(-427f, 92f), new Vector2(66f, 7f));
            Place(Text(header, "Title", "Jogos Digitais 3D", 100f, TextColor, FontStyles.Bold).rectTransform,
                new Vector2(0f, 18f), new Vector2(920f, 126f));
            Place(Text(header, "Subtitle", "Escolha um jogo", 40f, MutedColor).rectTransform,
                new Vector2(0f, -66f), new Vector2(920f, 55f));

            pocketPutt = Card("Pocket Putt", "Minigolfe em 50 fases", "Hub/Home/home_hero",
                PocketAccent, new Color32(17, 41, 38, 255), new Color32(153, 239, 178, 62),
                400f, HubNavigation.LaunchPocketPutt);
            ringToss = Card("Ring Toss", "Arremesso de argolas", "GameHub/ring-toss",
                RingAccent, new Color32(35, 24, 48, 255), new Color32(136, 71, 127, 53),
                459f, HubNavigation.LaunchRingToss);
        }

        private Button Card(string title, string description, string artwork, Color accent, Color surface,
            Color glow, float artworkHeight, Action launch)
        {
            Color outline = Color.Lerp(surface, accent, 0.28f);
            Image border = Rounded(safeRoot, title, Color.white);
            border.raycastTarget = true;
            Button button = border.gameObject.AddComponent<Button>();
            button.targetGraphic = border;
            ColorBlock colors = button.colors;
            colors.normalColor = outline;
            colors.highlightedColor = accent;
            colors.selectedColor = Color.Lerp(outline, accent, 0.70f);
            colors.pressedColor = Color.Lerp(accent, Color.white, 0.55f);
            colors.disabledColor = outline;
            colors.fadeDuration = 0.13f;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                if (launching) return;
                launching = true;
                pocketPutt.interactable = false;
                ringToss.interactable = false;
                launch();
            });

            Image panel = Rounded(border.rectTransform, "Surface", surface);
            RectTransform body = panel.rectTransform;
            Stretch(body, Vector2.zero, Vector2.one);
            body.offsetMin = new Vector2(3f, 3f);
            body.offsetMax = new Vector2(-3f, -3f);
            body.gameObject.AddComponent<Mask>();

            RawImage spotlight = Picture(body, "Artwork Light", lightTexture, glow);
            Stretch(spotlight.rectTransform, new Vector2(0f, 0.26f), new Vector2(1f, 1f));

            Texture2D texture = Resources.Load<Texture2D>(artwork);
            if (texture != null)
            {
                RawImage art = Picture(body, "Artwork", texture, Color.white);
                Place(art.rectTransform, new Vector2(0f, 98f),
                    new Vector2(artworkHeight * texture.width / texture.height, artworkHeight));
            }

            RectTransform footer = Solid(body, "Details", Color.Lerp(surface, Background, 0.37f));
            Stretch(footer, Vector2.zero, new Vector2(1f, 0f));
            footer.sizeDelta = new Vector2(0f, 175f);
            footer.anchoredPosition = new Vector2(0f, 87.5f);
            RectTransform rule = Solid(footer, "Divider", outline);
            Stretch(rule, Vector2.up, Vector2.one);
            rule.sizeDelta = new Vector2(0f, 2f);

            TextMeshProUGUI name = Text(footer, "Name", title, 68f, TextColor, FontStyles.Bold);
            FooterText(name, 43f, 81f);
            TextMeshProUGUI detail = Text(footer, "Description", description, 35f, MutedColor);
            FooterText(detail, -34f, 51f);

            RectTransform action = Rounded(footer, "Play", accent).rectTransform;
            action.anchorMin = action.anchorMax = new Vector2(1f, 0.5f);
            action.sizeDelta = new Vector2(178f, 84f);
            action.anchoredPosition = new Vector2(-127f, 0f);
            TextMeshProUGUI label = Text(action, "Play Label", "Jogar  ›", 40f, Background, FontStyles.Bold);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private void Resize()
        {
            Vector2Int resolution = new Vector2Int(Screen.width, Screen.height);
            Rect area = Screen.safeArea;
            if (resolution.x == 0 || resolution.y == 0) return;
            if (area.width <= 0 || area.height <= 0)
                area = new Rect(0, 0, resolution.x, resolution.y);
            if (resolution == lastResolution && area == lastSafeArea) return;

            lastResolution = resolution;
            lastSafeArea = area;
            Stretch(safeRoot, new Vector2(area.xMin / resolution.x, area.yMin / resolution.y),
                new Vector2(area.xMax / resolution.x, area.yMax / resolution.y));

            bool wide = area.width / area.height >= 1.28f;
            Vector2 reference = wide ? new Vector2(1800f, 1050f) : new Vector2(1080f, 1920f);
            canvas.scaleFactor = Mathf.Min(area.width / reference.x, area.height / reference.y);
            Place(header, wide ? new Vector2(-386f, 353f) : new Vector2(0f, 732f), new Vector2(920f, 200f));

            Vector2 cardSize = new Vector2(wide ? 828f : 920f, 610f);
            Place((RectTransform)pocketPutt.transform, wide ? new Vector2(-432f, -82f) : new Vector2(0f, 236f), cardSize);
            Place((RectTransform)ringToss.transform, wide ? new Vector2(432f, -82f) : new Vector2(0f, -426f), cardSize);
            SetNavigation(pocketPutt, ringToss, wide, true);
            SetNavigation(ringToss, pocketPutt, wide, false);
        }

        private static void SetNavigation(Button button, Button other, bool wide, bool first)
        {
            button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = wide && first ? other : null,
                selectOnLeft = wide && !first ? other : null,
                selectOnDown = !wide && first ? other : null,
                selectOnUp = !wide && !first ? other : null
            };
        }

        private Image Rounded(Transform parent, string name, Color color)
        {
            var image = Element(parent, name).gameObject.AddComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Solid(Transform parent, string name, Color color)
        {
            RectTransform rect = Element(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static RawImage Picture(Transform parent, string name, Texture texture, Color color)
        {
            var image = Element(parent, name).gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string value,
            float size, Color color, FontStyles style = FontStyles.Normal)
        {
            var text = Element(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static void FooterText(TextMeshProUGUI text, float y, float height)
        {
            RectTransform rect = text.rectTransform;
            Stretch(rect, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
            rect.offsetMin = new Vector2(40f, y - height * 0.5f);
            rect.offsetMax = new Vector2(-240f, y + height * 0.5f);
        }

        private static RectTransform Element(Transform parent, string name)
        {
            var host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);
            return (RectTransform)host.transform;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Texture2D CreateTexture(int size, Func<float, float, Color> sample)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = sample((x + 0.5f) / size, (y + 0.5f) / size);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
