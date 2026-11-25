using UnityEngine;

namespace SlipperyZlope.RingToss.Config
{
    /// <summary>
    /// The colour identity of a stage. The whole scene is built from untextured
    /// procedural geometry, so the palette is doing all of the art direction and is
    /// worth having as data rather than as constants scattered through a bootstrap.
    /// </summary>
    [CreateAssetMenu(menuName = "SlipperyZlope/Stage Theme", fileName = "StageTheme")]
    public sealed class StageTheme : ScriptableObject
    {
        [SerializeField] private string displayName = "Dusk";

        [Header("Sky")]
        [ColorUsage(false, true)] [SerializeField] private Color skyTop = new Color(0.09f, 0.08f, 0.22f);
        [ColorUsage(false, true)] [SerializeField] private Color skyMiddle = new Color(0.46f, 0.20f, 0.42f);
        [ColorUsage(false, true)] [SerializeField] private Color skyHorizon = new Color(0.98f, 0.52f, 0.36f);

        [Tooltip("Where the horizon colour sits, 0 at the bottom of the sky and 1 at the top.")]
        [Range(0f, 1f)] [SerializeField] private float horizonHeight = 0.34f;

        [Header("Sun / moon")]
        [ColorUsage(false, true)] [SerializeField] private Color sunColor = new Color(1f, 0.86f, 0.55f);
        [SerializeField] private Vector2 sunPosition = new Vector2(0.27f, 0.36f);
        [SerializeField] private float sunSize = 0.1f;

        [Header("Scenery")]
        [SerializeField] private Color silhouetteColor = new Color(0.12f, 0.09f, 0.22f);
        [SerializeField] private Color groundColor = new Color(0.24f, 0.17f, 0.32f);
        [SerializeField] private Color boardColor = new Color(0.34f, 0.20f, 0.38f);
        [SerializeField] private Color boardTrimColor = new Color(0.98f, 0.78f, 0.35f);

        [Header("Pegs")]
        [SerializeField] private Color pegColor = new Color(0.95f, 0.24f, 0.36f);
        [SerializeField] private Color pegStripeColor = new Color(0.98f, 0.95f, 0.9f);
        [ColorUsage(false, true)] [SerializeField] private Color pegGlowColor = new Color(1f, 0.83f, 0.4f);

        [Tooltip("Colour the cap sits at while the peg is still open.")]
        [ColorUsage(false, true)] [SerializeField] private Color pegCapRestColor = new Color(1f, 0.92f, 0.62f);

        [Header("Rings")]
        [Tooltip("Rings cycle through these, so consecutive throws are visually distinct.")]
        [SerializeField] private Color[] ringColors =
        {
            new Color(1f, 0.82f, 0.35f),
            new Color(0.36f, 0.86f, 0.78f),
            new Color(1f, 0.45f, 0.55f),
            new Color(0.62f, 0.72f, 1f)
        };

        [Header("Lighting")]
        [SerializeField] private Color keyLightColor = new Color(1f, 0.9f, 0.78f);
        [SerializeField] private float keyLightIntensity = 1.25f;
        [SerializeField] private Color ambientSky = new Color(0.35f, 0.3f, 0.5f);
        [SerializeField] private Color ambientEquator = new Color(0.28f, 0.2f, 0.34f);
        [SerializeField] private Color ambientGround = new Color(0.14f, 0.1f, 0.2f);

        [Header("Feedback")]
        [ColorUsage(false, true)] [SerializeField] private Color perfectColor = new Color(0.3f, 1f, 0.75f);
        [ColorUsage(false, true)] [SerializeField] private Color successColor = new Color(1f, 0.85f, 0.4f);
        [SerializeField] private Color failColor = new Color(0.75f, 0.72f, 0.82f);
        [SerializeField] private Color uiTextColor = new Color(0.98f, 0.96f, 0.94f);

        public string DisplayName => displayName;

        public Color SkyTop => skyTop;
        public Color SkyMiddle => skyMiddle;
        public Color SkyHorizon => skyHorizon;
        public float HorizonHeight => horizonHeight;

        public Color SunColor => sunColor;
        public Vector2 SunPosition => sunPosition;
        public float SunSize => sunSize;

        public Color SilhouetteColor => silhouetteColor;
        public Color GroundColor => groundColor;
        public Color BoardColor => boardColor;
        public Color BoardTrimColor => boardTrimColor;

        public Color PegColor => pegColor;
        public Color PegStripeColor => pegStripeColor;
        public Color PegGlowColor => pegGlowColor;
        public Color PegCapRestColor => pegCapRestColor;

        public Color KeyLightColor => keyLightColor;
        public float KeyLightIntensity => keyLightIntensity;
        public Color AmbientSky => ambientSky;
        public Color AmbientEquator => ambientEquator;
        public Color AmbientGround => ambientGround;

        public Color PerfectColor => perfectColor;
        public Color SuccessColor => successColor;
        public Color FailColor => failColor;
        public Color UiTextColor => uiTextColor;

        public int RingColorCount => ringColors == null ? 0 : ringColors.Length;

        public Color RingColor(int index)
        {
            if (ringColors == null || ringColors.Length == 0)
            {
                return Color.white;
            }

            // Positive modulo: a negative index must not throw.
            int wrapped = ((index % ringColors.Length) + ringColors.Length) % ringColors.Length;
            return ringColors[wrapped];
        }

        public void SetPalette(
            Color top, Color middle, Color horizon, float horizonAt,
            Color sun, Vector2 sunAt, float sunRadius,
            Color silhouette, Color ground, Color board, Color trim,
            Color peg, Color stripe, Color glow,
            Color[] rings,
            Color key, float keyIntensity, Color ambSky, Color ambEquator, Color ambGround,
            Color perfect, Color success, Color fail, Color text,
            string label)
        {
            skyTop = top;
            skyMiddle = middle;
            skyHorizon = horizon;
            horizonHeight = horizonAt;
            sunColor = sun;
            sunPosition = sunAt;
            sunSize = sunRadius;
            silhouetteColor = silhouette;
            groundColor = ground;
            boardColor = board;
            boardTrimColor = trim;
            pegColor = peg;
            pegStripeColor = stripe;
            pegGlowColor = glow;
            pegCapRestColor = Color.Lerp(glow, Color.white, 0.45f);
            ringColors = rings;
            keyLightColor = key;
            keyLightIntensity = keyIntensity;
            ambientSky = ambSky;
            ambientEquator = ambEquator;
            ambientGround = ambGround;
            perfectColor = perfect;
            successColor = success;
            failColor = fail;
            uiTextColor = text;
            displayName = label;
        }
    }
}
