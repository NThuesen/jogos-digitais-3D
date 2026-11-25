using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.Gameplay;
using SlipperyZlope.RingToss.Save;
using SlipperyZlope.RingToss.Utils;
using UnityEngine;

namespace SlipperyZlope.RingToss.Juice
{
    /// <summary>
    /// Applies the player's chosen skin and trail to whatever ring they are holding.
    ///
    /// This sits in the juice layer and listens, like everything else here: gameplay
    /// arms a ring and tints it from the stage palette, and this overrides that if the
    /// player has bought something else. The ordering is not luck — <see
    /// cref="Core.GameDirector"/> tints the ring *before* it hands it to the throw
    /// controller, and handing it over is what raises ThrowArmed, so anything reacting
    /// to that event is guaranteed to run afterwards.
    ///
    /// Nothing here instantiates a material. Colour, emission, metallic and smoothness
    /// all go through a MaterialPropertyBlock, so a ring wearing the most expensive
    /// skin in the shop still shares one material with every other ring in the pool.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public sealed class CosmeticsDirector : MonoBehaviour
    {
        [SerializeField] private CosmeticsLibrary library;
        [SerializeField] private ProgressStore progress;
        [SerializeField] private FxPool fx;

        private RingInstance armedRing;
        private RingInstance flyingRing;
        private RingSkin skin;
        private TrailStyle trail;
        private Color themeColor = Color.white;

        // What the trail and its sparks are drawn in. Kept apart from the ring's own
        // tint because the two disagree on dark skins: see RingSkin.TrailTintFor.
        private Color trailTint = Color.white;
        private float sparkCredit;

        // Patterns are rasterised once and kept. Nine small textures for the whole
        // shop, built the first time each skin is worn rather than all at boot.
        private Texture2D[] patterns;
        private Texture2D plain;

        public CosmeticsLibrary Library => library;

        public void Bind(CosmeticsLibrary cosmetics, ProgressStore progressStore, FxPool effects)
        {
            library = cosmetics;
            progress = progressStore;
            fx = effects;
        }

        private void OnEnable()
        {
            GameEvents.ThrowArmed += OnThrowArmed;
            GameEvents.Thrown += OnThrown;
            GameEvents.Resolved += OnResolved;
            GameEvents.CosmeticsChanged += OnCosmeticsChanged;
        }

        private void OnDisable()
        {
            GameEvents.ThrowArmed -= OnThrowArmed;
            GameEvents.Thrown -= OnThrown;
            GameEvents.Resolved -= OnResolved;
            GameEvents.CosmeticsChanged -= OnCosmeticsChanged;
        }

        private void Start()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            // Runtime textures are owned by nobody else.
            if (plain != null)
            {
                Destroy(plain);
            }

            if (patterns == null)
            {
                return;
            }

            for (int i = 0; i < patterns.Length; i++)
            {
                if (patterns[i] != null)
                {
                    Destroy(patterns[i]);
                }
            }
        }

        /// <summary>
        /// The pattern for a skin, printed on first use.
        ///
        /// Lazily, because a player wears one skin at a time and rasterising ten of
        /// them at boot would be nine textures nobody asked for. The shop is the one
        /// place that wants them all, and it asks for them as it draws each card.
        /// </summary>
        public Texture2D PatternFor(int index)
        {
            if (library == null || index < 0 || index >= library.SkinCount)
            {
                return Plain;
            }

            RingSkin entry = library.GetSkin(index);

            if (entry == null || !entry.IsPatterned)
            {
                return Plain;
            }

            patterns ??= new Texture2D[library.SkinCount];

            if (patterns[index] == null)
            {
                patterns[index] = RingTextures.Create(entry.Pattern, entry.Primary, entry.Secondary);
            }

            return patterns[index];
        }

        private Texture2D Plain => plain ??= RingTextures.White();

        private void OnCosmeticsChanged(int skinIndex, int trailIndex)
        {
            Refresh();

            // The ring already in the player's hand changes immediately. Waiting for
            // the next throw makes the shop feel like it did not take.
            if (armedRing != null)
            {
                Apply(armedRing);
            }
        }

        private void Refresh()
        {
            if (library == null || progress == null)
            {
                return;
            }

            skin = library.GetSkin(progress.EquippedSkin);
            trail = library.GetTrail(progress.EquippedTrail);
        }

        private void OnThrowArmed(RingInstance ring)
        {
            armedRing = ring;

            // Whatever the stage palette wanted. A ThemeCycle skin hands it straight
            // back, which is how the default skin stays free of any special case.
            themeColor = ring == null ? Color.white : ring.Tint;

            Apply(ring);
        }

        private void OnThrown(ThrowLaunched payload)
        {
            flyingRing = armedRing;
            armedRing = null;
            sparkCredit = 0f;
        }

        private void OnResolved(ThrowResolved payload)
        {
            flyingRing = null;
        }

        private void Apply(RingInstance ring)
        {
            if (ring == null)
            {
                return;
            }

            if (skin == null)
            {
                ring.ApplySkin(themeColor, themeColor, Plain, Vector2.zero, 0.35f, 0f, 0.5f);
                trailTint = themeColor;
                ring.ApplyTrail(trail, trailTint);
                return;
            }

            float time = Time.unscaledTime;
            Color read = skin.TintFor(themeColor, time);

            ring.ApplySkin(
                read,
                skin.ShaderColorFor(themeColor),
                PatternFor(progress == null ? 0 : progress.EquippedSkin),
                skin.ScrollAt(time),
                skin.Emission,
                skin.Metallic,
                skin.Smoothness);

            trailTint = skin.TrailTintFor(themeColor, time);
            ring.ApplyTrail(trail, trailTint);
        }

        private void Update()
        {
            // A scrolling pattern is the only reason this runs every frame, so it
            // costs nothing at all for the nine skins that do not scroll.
            if (skin != null && skin.ScrollSpeed != 0f)
            {
                RingInstance live = armedRing != null ? armedRing : flyingRing;

                if (live != null)
                {
                    Apply(live);
                }
            }

            TickSparks();
        }

        /// <summary>
        /// Sheds sparks along the flight for the trails that ask for them.
        ///
        /// Rate based rather than per frame, and carried as a fractional credit, so a
        /// trail sheds the same number of sparks per second whatever the frame rate is
        /// doing.
        /// </summary>
        private void TickSparks()
        {
            if (flyingRing == null || trail == null || trail.SparkRate <= 0f || fx == null)
            {
                sparkCredit = 0f;
                return;
            }

            sparkCredit += trail.SparkRate * Time.deltaTime;

            while (sparkCredit >= 1f)
            {
                sparkCredit -= 1f;

                Color color = trail.ColorMode == TrailColorMode.Fixed
                    ? trail.Color
                    : trailTint;

                // Smaller than a catch's sparks. These are shed continuously along the
                // flight, and a trail is a detail on the throw, not a second effect.
                fx.PlaySparks(flyingRing.Centre, color, 1, 1.1f, 0.4f);
            }
        }
    }
}
