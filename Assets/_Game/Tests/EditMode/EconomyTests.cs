using NUnit.Framework;
using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Save;
using SlipperyZlope.RingToss.Utils;
using UnityEngine;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// The shop's economy and the cosmetics it sells.
    ///
    /// The rules are pure arithmetic, so the balance can be pinned here rather than
    /// discovered by playing fifty levels and counting.
    /// </summary>
    public sealed class EconomyTests
    {
        [Test]
        public void A_perfect_always_pays()
        {
            Assert.Greater(TicketRules.ForPerfect(), 0);
        }

        [Test]
        public void More_stars_pay_more()
        {
            int previous = -1;

            for (int stars = 0; stars <= 3; stars++)
            {
                int paid = TicketRules.ForLevelClear(stars, firstClear: true);

                Assert.Greater(paid, previous);
                previous = paid;
            }
        }

        /// <summary>
        /// The rule that keeps the shop from becoming a chore. If replaying level one
        /// paid the same as clearing level fifty, the fastest way to earn would be to
        /// never play the game again.
        /// </summary>
        [Test]
        public void A_replay_pays_a_fraction_of_a_first_clear()
        {
            for (int stars = 0; stars <= 3; stars++)
            {
                int first = TicketRules.ForLevelClear(stars, firstClear: true);
                int replay = TicketRules.ForLevelClear(stars, firstClear: false);

                Assert.Less(replay, first, $"A replay at {stars} stars pays as much as the first clear.");
                Assert.Greater(replay, 0, "A replay still has to pay something.");
                Assert.Less(replay * 3, first, $"A replay at {stars} stars pays too close to a first clear.");
            }
        }

        [Test]
        public void Deeper_endless_rounds_pay_more()
        {
            Assert.GreaterOrEqual(TicketRules.ForEndlessRound(30), TicketRules.ForEndlessRound(10));
            Assert.Greater(TicketRules.ForEndlessRound(30), TicketRules.ForEndlessRound(1));
        }

        [Test]
        public void Nothing_ever_pays_a_negative_amount()
        {
            Assert.GreaterOrEqual(TicketRules.ForEndlessRound(-5), 0);
            Assert.GreaterOrEqual(TicketRules.ForEndlessRun(-5), 0);
            Assert.GreaterOrEqual(TicketRules.ForLevelClear(-1, true), 0);
        }

        /// <summary>
        /// Finishing the game once should be enough to own a decent share of the shop
        /// without touching endless, and nowhere near enough to own all of it.
        /// </summary>
        [Test]
        public void Finishing_the_game_funds_part_of_the_shop()
        {
            const int levels = 50;
            const int averageStars = 2;
            const int perfectsPerLevel = 2;

            int earned = levels * TicketRules.ForLevelClear(averageStars, firstClear: true)
                         + levels * perfectsPerLevel * TicketRules.ForPerfect();

            Assert.Greater(earned, 1500, "A full playthrough should buy several items.");
            Assert.Less(earned, 6800, "A full playthrough should not buy the whole shop.");
        }

        private static RingSkin MakeSkin(
            SkinColorMode mode, RingPattern pattern, Color primary, float scroll = 0f)
        {
            RingSkin skin = ScriptableObject.CreateInstance<RingSkin>();
            skin.Configure("Test", 100, mode, pattern, primary, Color.white, 0.3f, 0f, 0.5f, scroll);
            return skin;
        }

        [Test]
        public void A_theme_skin_hands_the_stage_colour_straight_back()
        {
            RingSkin skin = MakeSkin(SkinColorMode.ThemeCycle, RingPattern.Solid, Color.red);

            // This is what keeps "no skin equipped" free of a special case anywhere in
            // the game: the default skin is a skin that agrees with the palette.
            Assert.AreEqual(Color.green, skin.TintFor(Color.green, 0f));
            Assert.AreEqual(Color.green, skin.ShaderColorFor(Color.green));

            Object.DestroyImmediate(skin);
        }

        [Test]
        public void A_fixed_skin_ignores_the_stage_colour()
        {
            RingSkin skin = MakeSkin(SkinColorMode.Fixed, RingPattern.Solid, Color.red);

            Assert.AreEqual(Color.red, skin.TintFor(Color.green, 0f));
            Assert.AreEqual(Color.red, skin.ShaderColorFor(Color.blue));

            Object.DestroyImmediate(skin);
        }

        /// <summary>
        /// A patterned skin must not tint the shader's base colour, because base colour
        /// multiplies the map — tinting it would wash the pattern's two colours into
        /// one and the skin would be a flat blob again.
        /// </summary>
        [Test]
        public void A_patterned_skin_leaves_the_shader_colour_white()
        {
            RingSkin skin = MakeSkin(SkinColorMode.Fixed, RingPattern.Candy, Color.red);

            Assert.IsTrue(skin.IsPatterned);
            Assert.AreEqual(Color.white, skin.ShaderColorFor(Color.green));

            // But everything reading the ring from outside still gets a real colour.
            Assert.AreEqual(Color.red, skin.TintFor(Color.green, 0f));

            Object.DestroyImmediate(skin);
        }

        [Test]
        public void A_scrolling_skin_keeps_moving_and_stays_in_gamut()
        {
            RingSkin skin = MakeSkin(SkinColorMode.Fixed, RingPattern.Spectrum, Color.white, scroll: 0.25f);

            Assert.AreNotEqual(skin.TintFor(Color.white, 0f), skin.TintFor(Color.white, 1.2f));
            Assert.AreNotEqual(skin.ScrollAt(0f), skin.ScrollAt(1.2f));

            for (float t = 0f; t < 12f; t += 0.13f)
            {
                Color color = skin.TintFor(Color.white, t);

                Assert.GreaterOrEqual(Mathf.Min(color.r, color.g, color.b), 0f);
                Assert.LessOrEqual(Mathf.Max(color.r, color.g, color.b), 1.001f);

                Vector2 offset = skin.ScrollAt(t);

                Assert.GreaterOrEqual(offset.x, 0f);
                Assert.Less(offset.x, 1f);
            }

            Object.DestroyImmediate(skin);
        }

        [Test]
        public void A_still_skin_never_scrolls()
        {
            RingSkin skin = MakeSkin(SkinColorMode.Fixed, RingPattern.Checks, Color.red);

            Assert.AreEqual(Vector2.zero, skin.ScrollAt(0f));
            Assert.AreEqual(Vector2.zero, skin.ScrollAt(9.4f));

            Object.DestroyImmediate(skin);
        }

        /// <summary>
        /// TrailRenderer accepts at most eight colour and eight alpha keys, and quietly
        /// truncates beyond that rather than complaining.
        /// </summary>
        [Test]
        public void Every_trail_gradient_fits_the_renderer()
        {
            foreach (TrailColorMode mode in System.Enum.GetValues(typeof(TrailColorMode)))
            {
                TrailStyle trail = ScriptableObject.CreateInstance<TrailStyle>();
                trail.Configure("T", 100, 0.4f, 0.12f, 0.02f, mode, Color.cyan, 0.8f, 0.1f, 0f, 0.6f, 4, false);

                Gradient gradient = trail.BuildGradient(Color.magenta);

                Assert.LessOrEqual(gradient.colorKeys.Length, 8, $"{mode} has too many colour keys.");
                Assert.LessOrEqual(gradient.alphaKeys.Length, 8, $"{mode} has too many alpha keys.");
                Assert.GreaterOrEqual(gradient.colorKeys.Length, 2);

                Object.DestroyImmediate(trail);
            }
        }

        [Test]
        public void A_ring_coloured_trail_follows_the_ring()
        {
            TrailStyle trail = ScriptableObject.CreateInstance<TrailStyle>();
            trail.Configure(
                "T", 100, 0.4f, 0.12f, 0f, TrailColorMode.Ring, Color.cyan, 0.8f, 0f, 0f, 0f, 4, false);

            Gradient gradient = trail.BuildGradient(Color.magenta);

            // The head is lifted towards white so an additive trail still registers
            // against the booth's pale backdrop — testers reported the trail as simply
            // not working, and it was being drawn into a background it could not brighten.
            // What has to survive that is the *identity* of the colour, not its exact
            // value: a magenta ring must not trail cyan.
            Color head = gradient.colorKeys[0].color;
            Color tail = gradient.colorKeys[gradient.colorKeys.Length - 1].color;

            Assert.Greater(head.r, 0.9f, "a magenta ring's trail has to stay red-heavy");
            Assert.Greater(head.b, 0.9f, "and blue-heavy");
            Assert.Less(head.g, head.r, "and never turn white outright");

            // The tail keeps the ring's own colour, which is what ties the streak to
            // the object that made it.
            Assert.Greater(tail.r, tail.g);
            Assert.Greater(tail.b, tail.g);

            Object.DestroyImmediate(trail);
        }

        [Test]
        public void Ownership_is_a_bitmask_and_the_shop_has_to_fit_in_it()
        {
            // The save file stores what the player owns as one int per list. The shop
            // ships ten of each; this is the wall it would hit.
            Assert.AreEqual(32, ProgressStore.MaxCosmetics);
        }
    }
}
