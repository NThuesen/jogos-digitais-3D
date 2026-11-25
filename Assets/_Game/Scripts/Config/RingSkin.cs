using SlipperyZlope.RingToss.Utils;
using UnityEngine;

namespace SlipperyZlope.RingToss.Config
{
    /// <summary>How a skin decides what colour the ring is.</summary>
    public enum SkinColorMode
    {
        /// <summary>Takes the stage palette, cycling per throw. What the game shipped with.</summary>
        ThemeCycle,

        /// <summary>Its own colours, whatever the stage is.</summary>
        Fixed
    }

    /// <summary>
    /// One purchasable look for the ring.
    ///
    /// A skin is a pattern, two colours and a material finish — not a mesh and not an
    /// imported texture. That is only possible because the whole project is untextured
    /// procedural geometry, and it is the biggest dividend that choice has paid: ten
    /// visibly different rings cost ten rows of numbers, and every one of them is
    /// applied through a <see cref="MaterialPropertyBlock"/>, so the ring pool still
    /// shares a single material and the draw call count does not move.
    ///
    /// The pattern is the point. A skin that is only a recoloured torus is not a
    /// different skin, it is the same ring in a different mood.
    /// </summary>
    [CreateAssetMenu(menuName = "SlipperyZlope/Ring Skin", fileName = "RingSkin")]
    public sealed class RingSkin : ScriptableObject
    {
        [SerializeField] private string displayName = "Skin";

        [Tooltip("Tickets. Zero means the player starts with it.")]
        [SerializeField] private int price;

        [SerializeField] private SkinColorMode colorMode = SkinColorMode.Fixed;

        [SerializeField] private RingPattern pattern = RingPattern.Solid;

        [Tooltip("The ring's own colour, and the ground the pattern is printed on.")]
        [SerializeField] private Color primary = Color.white;

        [Tooltip("What the pattern is printed in.")]
        [SerializeField] private Color secondary = Color.white;

        [Tooltip("How much the ring lights itself. Above 1 it glows against a dark sky.")]
        [Range(0f, 3f)]
        [SerializeField] private float emission = 0.35f;

        [Range(0f, 1f)]
        [SerializeField] private float metallic;

        [Range(0f, 1f)]
        [SerializeField] private float smoothness = 0.5f;

        [Tooltip("Turns of the pattern around the ring per second. Only the spectrum " +
                 "skin uses it, and it is what makes that one the single skin that " +
                 "visibly moves.")]
        [SerializeField] private float scrollSpeed;

        public string DisplayName => displayName;
        public int Price => price;
        public SkinColorMode ColorMode => colorMode;
        public RingPattern Pattern => pattern;
        public Color Primary => primary;
        public Color Secondary => secondary;
        public float Emission => emission;
        public float Metallic => metallic;
        public float Smoothness => smoothness;
        public float ScrollSpeed => scrollSpeed;

        /// <summary>True when this skin needs a generated texture rather than a flat colour.</summary>
        public bool IsPatterned => pattern != RingPattern.Solid;

        /// <summary>
        /// The colour everything *else* should take from this skin: the trail, the
        /// sparks, the shop swatch. A patterned ring has no single colour, so this is
        /// the one it reads as from a distance.
        /// </summary>
        public Color TintFor(Color themeColor, float time)
        {
            if (colorMode == SkinColorMode.ThemeCycle)
            {
                return themeColor;
            }

            if (pattern == RingPattern.Spectrum)
            {
                return Color.HSVToRGB(Mathf.Repeat(time * scrollSpeed, 1f), 0.78f, 1f);
            }

            return primary;
        }

        /// <summary>
        /// The colour the ring's trail and its sparks take.
        ///
        /// Not <see cref="TintFor"/>, which is the colour the ring *reads as* and is
        /// right for a swatch. Trails and sparks are drawn additively, and adding a
        /// near-black to the frame adds nothing: Circuit's own colour is #101828, so
        /// every spark trail on that ring was invisible, which a playtest recording
        /// shows plainly. Three skins are printed on a dark ground — Chequer, Circuit
        /// and Nebula — and each has a bright print, so the print is what they trail.
        /// </summary>
        public Color TrailTintFor(Color themeColor, float time)
        {
            Color read = TintFor(themeColor, time);

            if (colorMode != SkinColorMode.Fixed || pattern == RingPattern.Spectrum)
            {
                return read;
            }

            return Luma(primary) < DarkGround && Luma(secondary) > Luma(primary) ? secondary : primary;
        }

        /// <summary>Below this a colour adds too little light to read as a trail.</summary>
        private const float DarkGround = 0.25f;

        private static float Luma(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>
        /// What goes into the shader's base colour.
        ///
        /// White for a patterned skin, because the base colour *multiplies* the
        /// texture — tinting it would wash the pattern's two colours into one.
        /// </summary>
        public Color ShaderColorFor(Color themeColor)
        {
            if (IsPatterned)
            {
                return Color.white;
            }

            return colorMode == SkinColorMode.ThemeCycle ? themeColor : primary;
        }

        /// <summary>Texture offset for a scrolling pattern, in UV space.</summary>
        public Vector2 ScrollAt(float time)
        {
            return scrollSpeed == 0f ? Vector2.zero : new Vector2(Mathf.Repeat(time * scrollSpeed, 1f), 0f);
        }

        public void Configure(
            string label, int cost, SkinColorMode mode, RingPattern ringPattern,
            Color primaryColor, Color secondaryColor,
            float emissionStrength, float metallicAmount, float smoothnessAmount, float scroll)
        {
            displayName = label;
            price = cost;
            colorMode = mode;
            pattern = ringPattern;
            primary = primaryColor;
            secondary = secondaryColor;
            emission = emissionStrength;
            metallic = metallicAmount;
            smoothness = smoothnessAmount;
            scrollSpeed = scroll;
        }
    }
}
