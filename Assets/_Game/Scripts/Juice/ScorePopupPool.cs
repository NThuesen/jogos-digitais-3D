using SlipperyZlope.RingToss.Config;
using PrimeTween;
using TMPro;
using UnityEngine;

namespace SlipperyZlope.RingToss.Juice
{
    /// <summary>
    /// The floating numbers and words that rise out of a result.
    ///
    /// World space rather than screen space, so a popup belongs to the peg it came
    /// from and the player's eye never has to leave the board to read it.
    ///
    /// Labels are written through TextMeshPro's SetText format overloads, which build
    /// the string into an internal char buffer instead of allocating a new one for
    /// every score.
    /// </summary>
    public sealed class ScorePopupPool : MonoBehaviour
    {
        [SerializeField] private JuiceConfig juiceConfig;

        [Range(2, 16)]
        [SerializeField] private int capacity = 8;

        [SerializeField] private float fontSize = 3.4f;

        [SerializeField] private Camera billboardCamera;

        private TextMeshPro[] labels;
        private Transform[] roots;
        private int cursor;

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            labels = new TextMeshPro[capacity];
            roots = new Transform[capacity];

            for (int i = 0; i < capacity; i++)
            {
                GameObject host = new GameObject($"Popup_{i:D2}");
                host.transform.SetParent(transform, false);

                TextMeshPro label = host.AddComponent<TextMeshPro>();
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = fontSize;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.raycastTarget = false;

                // Rect transforms default to a size that clips a short string oddly in
                // world space; a fixed generous box avoids per-popup layout surprises.
                label.rectTransform.sizeDelta = new Vector2(6f, 2f);

                MeshRenderer renderer = host.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                host.SetActive(false);

                labels[i] = label;
                roots[i] = host.transform;
            }
        }

        public void SetCamera(Camera camera)
        {
            billboardCamera = camera;
        }

        public void SetConfig(JuiceConfig value)
        {
            juiceConfig = value;
        }

        /// <summary>A numeric score popup, formatted without allocating a string.</summary>
        public void PlayScore(Vector3 worldPosition, int points, Color color, float scale = 1f)
        {
            TextMeshPro label = Next(out Transform root);

            if (label == null)
            {
                return;
            }

            label.SetText("+{0}", points);
            Animate(label, root, worldPosition, color, scale);
        }

        /// <summary>A worded popup: PERFECT, the combo count, a miss.</summary>
        public void PlayLabel(Vector3 worldPosition, string text, Color color, float scale = 1f)
        {
            TextMeshPro label = Next(out Transform root);

            if (label == null)
            {
                return;
            }

            label.SetText(text);
            Animate(label, root, worldPosition, color, scale);
        }

        private TextMeshPro Next(out Transform root)
        {
            if (labels == null || labels.Length == 0)
            {
                root = null;
                return null;
            }

            int index = cursor;
            cursor = (cursor + 1) % labels.Length;

            root = roots[index];
            return labels[index];
        }

        private void Animate(TextMeshPro label, Transform root, Vector3 worldPosition, Color color, float scale)
        {
            float duration = juiceConfig == null ? 0.95f : juiceConfig.PopupDuration;
            float rise = juiceConfig == null ? 1.05f : juiceConfig.PopupRise;
            float punch = juiceConfig == null ? 1.45f : juiceConfig.PopupPunchScale;

            root.gameObject.SetActive(true);
            root.position = worldPosition;
            root.localScale = Vector3.zero;
            label.color = color;

            Sequence.Create()
                // Overshoot in, so the number arrives with weight instead of appearing.
                .Group(Tween.Scale(root, 0f, scale * punch, duration * 0.22f, Ease.OutBack))
                .Chain(Tween.Scale(root, scale * punch, scale, duration * 0.18f, Ease.OutCubic))
                .Group(Tween.PositionY(root, worldPosition.y, worldPosition.y + rise, duration, Ease.OutCubic))
                .Group(Tween.Custom(label, 1f, 0f, duration, (target, alpha) =>
                {
                    Color current = target.color;
                    current.a = alpha;
                    target.color = current;
                }, Ease.InQuad))
                .ChainCallback(root, target => target.gameObject.SetActive(false));
        }

        /// <summary>
        /// Popups face the camera every frame rather than being parented to it, so a
        /// camera move does not drag them along with it.
        /// </summary>
        private void LateUpdate()
        {
            if (billboardCamera == null || roots == null)
            {
                return;
            }

            Vector3 forward = billboardCamera.transform.forward;

            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].gameObject.activeSelf)
                {
                    roots[i].rotation = Quaternion.LookRotation(forward, Vector3.up);
                }
            }
        }
    }
}
