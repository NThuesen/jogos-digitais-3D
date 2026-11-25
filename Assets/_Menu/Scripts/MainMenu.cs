using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GameHub
{
    /// <summary>
    /// The start screen: one card per game. Built in code at load, the same way both
    /// games build their own scenes, so the menu scene holds only a camera and this.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        private static readonly Color Background = new Color(0.10f, 0.12f, 0.18f);
        private static readonly Color TitleColor = new Color(1f, 0.83f, 0.35f);
        private static readonly Color TextColor = new Color(0.96f, 0.96f, 0.98f);
        private static readonly Color MutedColor = new Color(0.70f, 0.74f, 0.82f);
        private static readonly Color RingTossColor = new Color(0.80f, 0.26f, 0.30f);
        private static readonly Color PocketPuttColor = new Color(0.22f, 0.56f, 0.36f);

        [SerializeField] private Camera menuCamera;

        private Sprite cardSprite;
        private Texture2D cardTexture;

        private void Awake()
        {
            // Both games are portrait and aim for 60 FPS; Android would otherwise
            // default to 30 for whichever game does not set it itself.
            Application.targetFrameRate = 60;

            if (menuCamera != null)
            {
                menuCamera.clearFlags = CameraClearFlags.SolidColor;
                menuCamera.backgroundColor = Background;
            }

            cardTexture = RoundedTexture(64, 24);
            cardSprite = Sprite.Create(cardTexture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));

            EnsureEventSystem();
            BuildCanvas();
        }

        private void OnDestroy()
        {
            if (cardSprite != null) Destroy(cardSprite);
            if (cardTexture != null) Destroy(cardTexture);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject host = new GameObject("Event System");
            host.AddComponent<EventSystem>();
            host.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private void BuildCanvas()
        {
            GameObject host = new GameObject("Menu Canvas", typeof(RectTransform));
            host.transform.SetParent(transform, false);
            host.layer = LayerMask.NameToLayer("UI");

            Canvas canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            host.AddComponent<GraphicRaycaster>();

            RectTransform root = (RectTransform)host.transform;

            Text(root, "Title", "JOGOS", 140f, TitleColor, FontStyles.Bold,
                new Vector2(0f, 600f), new Vector2(760f, 180f));
            Text(root, "Subtitle", "Escolha um jogo", 44f, MutedColor, FontStyles.Normal,
                new Vector2(0f, 470f), new Vector2(760f, 70f));

            Card(root, "Ring Toss", "RING TOSS", "Arremesse argolas nos pinos",
                RingTossColor, new Vector2(0f, 160f), HubNavigation.LaunchRingToss);
            Card(root, "Pocket Putt", "POCKET PUTT", "Minigolfe de tacadas rápidas",
                PocketPuttColor, new Vector2(0f, -220f), HubNavigation.LaunchPocketPutt);
        }

        private void Card(RectTransform parent, string name, string title, string description,
            Color color, Vector2 position, Action onPressed)
        {
            GameObject host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)host.transform;
            Place(rect, position, new Vector2(720f, 320f));

            Image image = host.AddComponent<Image>();
            image.sprite = cardSprite;
            image.type = Image.Type.Sliced;
            image.color = color;

            Button button = host.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                button.interactable = false;
                onPressed();
            });

            Text(rect, "Title", title, 76f, TextColor, FontStyles.Bold,
                new Vector2(0f, 40f), new Vector2(660f, 110f));
            Text(rect, "Description", description, 36f, TextColor, FontStyles.Normal,
                new Vector2(0f, -60f), new Vector2(660f, 60f));
        }

        private static TextMeshProUGUI Text(RectTransform parent, string name, string value,
            float size, Color color, FontStyles style, Vector2 position, Vector2 boxSize)
        {
            GameObject host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);
            Place((RectTransform)host.transform, position, boxSize);

            TextMeshProUGUI text = host.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = size * 0.6f;
            text.fontSizeMax = size;
            text.raycastTarget = false;
            return text;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>A white rounded square, nine-sliced by the card images.</summary>
        private static Texture2D RoundedTexture(int size, int radius)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    byte alpha = (byte)(Mathf.Clamp01(radius - distance + 0.5f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
