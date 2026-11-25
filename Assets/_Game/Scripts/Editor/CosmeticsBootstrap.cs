using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Utils;
using UnityEditor;
using UnityEngine;

namespace SlipperyZlope.RingToss.EditorTools
{
    /// <summary>
    /// Authors the shop: ten ring skins and ten trails.
    ///
    /// Both lists are ordered by price, and that order is also their identity — the
    /// save file records what the player owns as a bitmask over these indices. New
    /// items go on the end; reordering them re-labels somebody's purchases.
    ///
    /// The prices are a curve rather than a list. The first paid item is cheap enough
    /// to buy on the way out of stage one, so the shop proves it works early. From
    /// there they climb steeply: owning everything costs about twenty-seven thousand
    /// tickets against roughly two thousand for a full playthrough, which puts the
    /// last few items a long way past the end of the campaign and squarely in endless
    /// mode's territory. That gap is the point — a shop the player can empty in an
    /// evening stops being progression the moment they do.
    /// </summary>
    public static class CosmeticsBootstrap
    {
        private const string DataFolder = "Assets/_Game/Data";
        private const string SkinsFolder = DataFolder + "/Skins";
        private const string TrailsFolder = DataFolder + "/Trails";
        private const string LibraryPath = DataFolder + "/CosmeticsLibrary.asset";

        [MenuItem("SlipperyZlope/Bootstrap Cosmetics")]
        public static void Run()
        {
            EditorAssets.EnsureFolder(SkinsFolder);
            EditorAssets.EnsureFolder(TrailsFolder);

            RingSkin[] skins =
            {
                // The default. ThemeCycle hands the stage palette straight back, which
                // is how "no skin equipped" needs no special case anywhere in the game.
                Skin("Skin_00_Classic", "Classic", 0, SkinColorMode.ThemeCycle,
                    RingPattern.Solid, "#FFFFFF", "#FFFFFF", 0.35f, 0f, 0.5f),

                // Every paid skin carries a pattern. A skin that is only a recoloured
                // torus is the same ring in a different mood, and nobody saves up for
                // a mood.
                Skin("Skin_01_Candy", "Candy Cane", 150, SkinColorMode.Fixed,
                    RingPattern.Candy, "#E8384F", "#FFF6EC", 0.22f, 0f, 0.42f),

                Skin("Skin_02_Chequer", "Chequer", 320, SkinColorMode.Fixed,
                    RingPattern.Checks, "#232634", "#F2F4FF", 0.16f, 0.2f, 0.55f),

                Skin("Skin_03_Bubblegum", "Bubblegum", 560, SkinColorMode.Fixed,
                    RingPattern.Dots, "#FF87C3", "#FFF3F8", 0.3f, 0f, 0.66f),

                Skin("Skin_04_Brass", "Brass Band", 880, SkinColorMode.Fixed,
                    RingPattern.Bands, "#C9922A", "#F6DE9A", 0.2f, 0.92f, 0.8f),

                Skin("Skin_05_Serpent", "Serpent", 1300, SkinColorMode.Fixed,
                    RingPattern.Scales, "#1E6B4F", "#7FE3A8", 0.28f, 0.35f, 0.7f),

                Skin("Skin_06_Circuit", "Circuit", 1800, SkinColorMode.Fixed,
                    RingPattern.Chevron, "#101828", "#3BE8FF", 1.5f, 0.5f, 0.72f),

                Skin("Skin_07_Marble", "Marble", 2400, SkinColorMode.Fixed,
                    RingPattern.Marble, "#EDEAE3", "#5B5F73", 0.14f, 0.1f, 0.88f),

                Skin("Skin_08_Nebula", "Nebula", 3200, SkinColorMode.Fixed,
                    RingPattern.Starfield, "#1A1140", "#FFF0B8", 1.25f, 0.2f, 0.78f),

                // The only skin that moves: the pattern itself scrolls around the ring.
                Skin("Skin_09_Prism", "Prism", 4200, SkinColorMode.Fixed,
                    RingPattern.Spectrum, "#FFFFFF", "#FFFFFF", 1.1f, 0.3f, 0.82f, scroll: 0.22f)
            };

            TrailStyle[] trails =
            {
                // A trail is a detail on the throw, not a second effect. The playtest
                // recording that prompted this set had Aurora drawing a rainbow sheet a
                // quarter of the screen tall over the pegs the player was aiming at, and
                // the owner's note was simply that trails should be a detail.
                //
                // So every one of these is cut on three axes at once: about half the
                // width (still stated against the ring's 0.32 m outer radius — 0.05 is a
                // sixth of it), about two thirds of the length, and two rim emitters
                // instead of four. Four emitters on a tumbling ring sweep out a surface;
                // two draw a pair of lines, which is what reads as a ring travelling.
                //
                // That cut went too far, and the owner said so after playing it: the
                // trails stopped being worth buying, which is the one thing a cosmetic
                // has to be. Widths are half again what the cut left, lifetimes a
                // quarter longer, and the paid ones get a third rim emitter — landing
                // between the sheet that covered the board and the scratch nobody could
                // see. The widest is now about a third of the ring's radius against the
                // half it was when it was in the way.
                Trail("Trail_00_Classic", "Classic", 0, 0.30f, 0.07f, 0f,
                    TrailColorMode.Ring, "#FFFFFF", 0.6f, 0f, 0f, 0f, emitters: 2, core: false),

                Trail("Trail_01_Whisper", "Whisper", 120, 0.24f, 0.05f, 0f,
                    TrailColorMode.Ring, "#FFFFFF", 0.48f, 0f, 0f, 0f, emitters: 2, core: false),

                Trail("Trail_02_Comet", "Comet", 260, 0.32f, 0.09f, 0f,
                    TrailColorMode.Ring, "#FFFFFF", 0.68f, 0f, 0f, 0f, emitters: 3, core: true),

                Trail("Trail_03_Ribbon", "Ribbon", 460, 0.6f, 0.07f, 0.03f,
                    TrailColorMode.Ring, "#FFFFFF", 0.68f, 0.12f, 0f, 0f, emitters: 3, core: false),

                // Half the sparks it had. They are shed continuously, and at 55 a second
                // they read as a spray rather than as a trail with a spark in it.
                Trail("Trail_04_Sparks", "Sparks", 720, 0.32f, 0.068f, 0f,
                    TrailColorMode.Ring, "#FFD166", 0.63f, 0f, 38f, 0f, emitters: 3, core: false),

                Trail("Trail_05_Smoke", "Smoke", 1060, 0.55f, 0.105f, 0.13f,
                    TrailColorMode.Fixed, "#A6ADC4", 0.36f, 0f, 0f, 0f, emitters: 3, core: true),

                Trail("Trail_06_Neon", "Neon", 1500, 0.5f, 0.07f, 0.055f,
                    TrailColorMode.Fixed, "#38F5FF", 0.78f, 0.3f, 0f, 0f, emitters: 3, core: false),

                // The one in the recording. No core any more: a rainbow body behind two
                // rainbow rims is exactly the sheet that covered the board.
                Trail("Trail_07_Aurora", "Aurora", 2050, 0.55f, 0.09f, 0.045f,
                    TrailColorMode.Rainbow, "#FFFFFF", 0.63f, 0.08f, 0f, 0.65f, emitters: 3, core: false),

                Trail("Trail_08_Starfall", "Starfall", 2750, 0.48f, 0.082f, 0.03f,
                    TrailColorMode.Rainbow, "#FFFFFF", 0.63f, 0f, 44f, 0.9f, emitters: 3, core: false),

                // Dark band, bright tail: the ring appears to punch a hole in the air
                // and leave light behind it.
                Trail("Trail_09_Eclipse", "Eclipse", 3600, 0.52f, 0.105f, 0.055f,
                    TrailColorMode.Fixed, "#1B1030", 0.78f, 0.5f, 20f, 0f, emitters: 3, core: true)
            };

            CosmeticsLibrary library = EditorAssets.LoadOrCreate<CosmeticsLibrary>(LibraryPath);
            library.SetSkins(skins);
            library.SetTrails(trails);
            EditorUtility.SetDirty(library);

            AssetDatabase.SaveAssets();

            Verify(skins, trails);

            int total = 0;

            for (int i = 0; i < skins.Length; i++)
            {
                total += skins[i].Price;
            }

            for (int i = 0; i < trails.Length; i++)
            {
                total += trails[i].Price;
            }

            Debug.Log(
                $"[CosmeticsBootstrap] Wrote {skins.Length} skins and {trails.Length} trails, " +
                $"{total} tickets to own everything.");
        }

        /// <summary>
        /// Checks what was written rather than what was meant. Both of these are silent
        /// failures: a second free item makes the shop look broken, and a list longer
        /// than the save file's bitmask loses purchases without an error anywhere.
        /// </summary>
        private static void Verify(RingSkin[] skins, TrailStyle[] trails)
        {
            if (skins.Length > Save.ProgressStore.MaxCosmetics
                || trails.Length > Save.ProgressStore.MaxCosmetics)
            {
                Debug.LogError(
                    $"[CosmeticsBootstrap] More than {Save.ProgressStore.MaxCosmetics} items in a list. " +
                    "Ownership is stored as a bitmask and anything past that cannot be saved.");
            }

            for (int i = 1; i < skins.Length; i++)
            {
                if (skins[i].Price <= 0)
                {
                    Debug.LogError($"[CosmeticsBootstrap] Skin '{skins[i].DisplayName}' is free but is not the default.");
                }

                if (skins[i].Price <= skins[i - 1].Price)
                {
                    Debug.LogError($"[CosmeticsBootstrap] Skin '{skins[i].DisplayName}' is not dearer than the one before it.");
                }
            }

            for (int i = 1; i < trails.Length; i++)
            {
                if (trails[i].Price <= 0)
                {
                    Debug.LogError($"[CosmeticsBootstrap] Trail '{trails[i].DisplayName}' is free but is not the default.");
                }

                if (trails[i].Price <= trails[i - 1].Price)
                {
                    Debug.LogError($"[CosmeticsBootstrap] Trail '{trails[i].DisplayName}' is not dearer than the one before it.");
                }
            }
        }

        private static RingSkin Skin(
            string assetName, string label, int price, SkinColorMode mode, RingPattern pattern,
            string primary, string secondary, float emission, float metallic, float smoothness,
            float scroll = 0f)
        {
            RingSkin skin = EditorAssets.LoadOrCreate<RingSkin>($"{SkinsFolder}/{assetName}.asset");
            skin.Configure(label, price, mode, pattern, Hex(primary), Hex(secondary),
                emission, metallic, smoothness, scroll);
            EditorUtility.SetDirty(skin);
            return skin;
        }

        private static TrailStyle Trail(
            string assetName, string label, int price, float time, float startWidth, float endWidth,
            TrailColorMode mode, string hex, float startAlpha, float endAlpha, float sparks, float span,
            int emitters, bool core)
        {
            TrailStyle trail = EditorAssets.LoadOrCreate<TrailStyle>($"{TrailsFolder}/{assetName}.asset");
            trail.Configure(label, price, time, startWidth, endWidth, mode, Hex(hex),
                startAlpha, endAlpha, sparks, span, emitters, core);
            EditorUtility.SetDirty(trail);
            return trail;
        }

        private static Color Hex(string value)
        {
            if (!ColorUtility.TryParseHtmlString(value, out Color color))
            {
                Debug.LogError($"[CosmeticsBootstrap] Bad colour '{value}'.");
                return Color.magenta;
            }

            return color;
        }
    }
}
