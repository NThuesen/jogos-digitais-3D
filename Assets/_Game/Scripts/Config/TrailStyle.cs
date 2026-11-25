using UnityEngine;

namespace SlipperyZlope.RingToss.Config
{
    /// <summary>Where a trail takes its colour from.</summary>
    public enum TrailColorMode
    {
        /// <summary>Whatever colour the ring is. Keeps a skin and its trail together.</summary>
        Ring,

        /// <summary>Its own colour, independent of the ring.</summary>
        Fixed,

        /// <summary>Runs around the colour wheel along the trail's length.</summary>
        Rainbow
    }

    /// <summary>
    /// One purchasable look for the ring's trail.
    ///
    /// Everything here is a <see cref="TrailRenderer"/> setting plus an optional spark
    /// emission, so ten visibly different trails cost ten rows of numbers rather than
    /// ten particle systems. The width curve and the colour gradient are where nearly
    /// all of the character is: a short wide trail is a comet, a long thin one is a
    /// ribbon, and they are the same three fields.
    /// </summary>
    [CreateAssetMenu(menuName = "SlipperyZlope/Trail Style", fileName = "TrailStyle")]
    public sealed class TrailStyle : ScriptableObject
    {
        [SerializeField] private string displayName = "Trail";

        [Tooltip("Tickets. Zero means the player starts with it.")]
        [SerializeField] private int price;

        [Tooltip("Seconds of ring flight the trail remembers. This is its length.")]
        [Range(0.05f, 1.5f)]
        [SerializeField] private float time = 0.32f;

        [Range(0f, 0.6f)]
        [SerializeField] private float startWidth = 0.13f;

        [Range(0f, 0.6f)]
        [SerializeField] private float endWidth;

        [SerializeField] private TrailColorMode colorMode = TrailColorMode.Ring;

        [SerializeField] private Color color = Color.white;

        [Range(0f, 1f)]
        [SerializeField] private float startAlpha = 0.75f;

        [Range(0f, 1f)]
        [SerializeField] private float endAlpha;

        [Tooltip("Sparks shed along the flight, per second. Zero for none.")]
        [Range(0f, 120f)]
        [SerializeField] private float sparkRate;

        [Tooltip("How many points around the ring's rim shed a trail.\n\n" +
                 "One emitter draws a thin ribbon from a single point, which is what " +
                 "the game shipped with and which reads as a scratch in one corner of " +
                 "the ring rather than as the ring moving. Four turns the same style " +
                 "into a braid the width of the whole object.")]
        [Range(1, 4)]
        [SerializeField] private int emitters = 4;

        [Tooltip("A fifth trail from the ring's centre, for a comet core behind the hole.")]
        [SerializeField] private bool core;

        [Tooltip("Colour swept along the trail's length, for Rainbow trails.")]
        [Range(0f, 1f)]
        [SerializeField] private float rainbowSpan = 0.5f;

        public string DisplayName => displayName;
        public int Price => price;
        public float Time => time;
        public float StartWidth => startWidth;
        public float EndWidth => endWidth;
        public TrailColorMode ColorMode => colorMode;
        public Color Color => color;
        public float StartAlpha => startAlpha;
        public float EndAlpha => endAlpha;
        public float SparkRate => sparkRate;
        public float RainbowSpan => rainbowSpan;
        public int Emitters => Mathf.Clamp(emitters, 1, 4);
        public bool Core => core;

        public bool HasTrail => time > 0.01f && startWidth > 0.001f;

        /// <summary>
        /// The gradient to hand the TrailRenderer.
        ///
        /// Built fresh rather than cached because a Gradient is a reference type and a
        /// shared one would be mutated by whichever ring was configured last.
        /// </summary>
        public Gradient BuildGradient(Color ringColor)
        {
            Gradient gradient = new Gradient();

            Color head = colorMode == TrailColorMode.Ring ? ringColor : color;

            if (colorMode == TrailColorMode.Rainbow)
            {
                // Five stops around the wheel: enough to read as a sweep, few enough
                // that the TrailRenderer's eight-key limit is never in danger.
                GradientColorKey[] colors = new GradientColorKey[5];

                for (int i = 0; i < colors.Length; i++)
                {
                    float t = i / (float)(colors.Length - 1);

                    // Saturation eased off at the head for the same reason Head() lifts
                    // it: the first centimetres of the streak are what has to survive
                    // being drawn additively over a pale backdrop.
                    colors[i] = new GradientColorKey(
                        Color.HSVToRGB(Mathf.Repeat(t * rainbowSpan, 1f), Mathf.Lerp(0.45f, 0.85f, t), 1f), t);
                }

                gradient.SetKeys(colors, AlphaKeys());
                return gradient;
            }

            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Head(head), 0f),
                    new GradientColorKey(colorMode == TrailColorMode.Fixed ? color : head * 0.7f, 1f)
                },
                AlphaKeys());

            return gradient;
        }

        /// <summary>
        /// The colour the trail leaves the ring at, lifted towards white.
        ///
        /// The trail material is additive, which is why it glows against the board and
        /// why it disappears against the booth. Adding a mid-tone colour to a pale
        /// striped backdrop changes almost nothing, and the top of every throw's arc is
        /// spent in front of exactly that backdrop — so a trail tuned by watching it
        /// cross the dark floor is a trail nobody sees during the part of the flight
        /// they are actually looking at. Beta testers reported it as not working at all.
        ///
        /// Lifting the head towards white does not wash the colour out: the tail keeps
        /// the ring's own hue, so the streak still reads as belonging to this ring, and
        /// the bright end is the part that survives a bright background.
        /// </summary>
        private static Color Head(Color colour)
        {
            return Color.Lerp(colour, Color.white, 0.4f);
        }

        private GradientAlphaKey[] AlphaKeys()
        {
            return new[]
            {
                new GradientAlphaKey(startAlpha, 0f),
                new GradientAlphaKey(endAlpha, 1f)
            };
        }

        public void Configure(
            string label, int cost, float lifetime, float widthStart, float widthEnd,
            TrailColorMode mode, Color trailColor, float alphaStart, float alphaEnd,
            float sparks, float span, int emitterCount, bool hasCore)
        {
            displayName = label;
            price = cost;
            time = lifetime;
            startWidth = widthStart;
            endWidth = widthEnd;
            colorMode = mode;
            color = trailColor;
            startAlpha = alphaStart;
            endAlpha = alphaEnd;
            sparkRate = sparks;
            rainbowSpan = span;
            emitters = emitterCount;
            core = hasCore;
        }
    }
}
