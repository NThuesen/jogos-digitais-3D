using System.Collections;
using NUnit.Framework;
using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.Juice;
using SlipperyZlope.RingToss.Save;
using SlipperyZlope.RingToss.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// The shop's catalogue and its pacing.
    ///
    /// The economy's arithmetic is covered by EconomyTests; this is about the twenty
    /// things it sells — that they exist, that they are ordered, that the paid skins
    /// are actually different objects rather than recoloured ones, and above all that
    /// the whole shop cannot be emptied by finishing the game once.
    ///
    /// Play Mode because the library is reached through the scene.
    /// </summary>
    public sealed class ShopTests
    {
        private GameDirector director;
        private CosmeticsLibrary library;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Main");

            yield return null;
            yield return null;
            yield return null;

            director = Object.FindFirstObjectByType<GameDirector>();
            Assert.IsNotNull(director, "The Main scene has no GameDirector.");

            CosmeticsDirector cosmetics = Object.FindFirstObjectByType<CosmeticsDirector>();
            Assert.IsNotNull(cosmetics, "The Main scene has no CosmeticsDirector.");

            library = cosmetics.Library;
            Assert.IsNotNull(library, "The CosmeticsDirector has no library.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (director != null && director.Progress != null)
            {
                director.Progress.ResetAll(director.LevelCount);
            }

            Scene main = SceneManager.GetSceneByName("Main");

            if (!main.IsValid() || !main.isLoaded)
            {
                yield break;
            }

            SceneManager.SetActiveScene(SceneManager.CreateScene("AfterShopTests"));
            yield return SceneManager.UnloadSceneAsync(main);
        }

        [UnityTest]
        public IEnumerator The_shop_stocks_ten_of_each()
        {
            Assert.AreEqual(10, library.SkinCount);
            Assert.AreEqual(10, library.TrailCount);

            for (int i = 0; i < library.SkinCount; i++)
            {
                Assert.IsNotNull(library.GetSkin(i), $"Skin {i} is missing.");
                Assert.IsNotEmpty(library.GetSkin(i).DisplayName, $"Skin {i} has no name.");
            }

            for (int i = 0; i < library.TrailCount; i++)
            {
                Assert.IsNotNull(library.GetTrail(i), $"Trail {i} is missing.");
                Assert.IsNotEmpty(library.GetTrail(i).DisplayName, $"Trail {i} has no name.");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Exactly_one_item_of_each_kind_is_free()
        {
            Assert.AreEqual(0, library.GetSkin(0).Price, "The first skin has to be the free one.");
            Assert.AreEqual(0, library.GetTrail(0).Price, "The first trail has to be the free one.");

            for (int i = 1; i < library.SkinCount; i++)
            {
                Assert.Greater(library.GetSkin(i).Price, library.GetSkin(i - 1).Price,
                    $"Skin '{library.GetSkin(i).DisplayName}' is not dearer than the one before it.");
            }

            for (int i = 1; i < library.TrailCount; i++)
            {
                Assert.Greater(library.GetTrail(i).Price, library.GetTrail(i - 1).Price,
                    $"Trail '{library.GetTrail(i).DisplayName}' is not dearer than the one before it.");
            }

            yield return null;
        }

        /// <summary>
        /// Every paid skin has to be a different *object*, not the same torus in a new
        /// colour. A shop of recolours is a shop nobody saves up for.
        /// </summary>
        [UnityTest]
        public IEnumerator Every_paid_skin_carries_a_pattern()
        {
            for (int i = 1; i < library.SkinCount; i++)
            {
                RingSkin skin = library.GetSkin(i);

                Assert.IsTrue(skin.IsPatterned,
                    $"Skin '{skin.DisplayName}' costs {skin.Price} tickets and is only a colour.");
                Assert.AreNotEqual(RingPattern.Solid, skin.Pattern);
            }

            // And no two of them share a pattern, or two paid skins are the same idea.
            for (int i = 1; i < library.SkinCount; i++)
            {
                for (int j = i + 1; j < library.SkinCount; j++)
                {
                    Assert.AreNotEqual(library.GetSkin(i).Pattern, library.GetSkin(j).Pattern,
                        $"'{library.GetSkin(i).DisplayName}' and '{library.GetSkin(j).DisplayName}' "
                        + "use the same pattern.");
                }
            }

            yield return null;
        }

        /// <summary>
        /// A trail has to be drawn by more than one point on the ring, or it reads as a
        /// scratch in one corner rather than as the ring travelling.
        /// </summary>
        [UnityTest]
        public IEnumerator Every_trail_is_drawn_across_the_whole_ring()
        {
            for (int i = 0; i < library.TrailCount; i++)
            {
                TrailStyle trail = library.GetTrail(i);

                Assert.GreaterOrEqual(trail.Emitters, 2,
                    $"Trail '{trail.DisplayName}' is drawn from too few points on the rim.");
                Assert.IsTrue(trail.HasTrail, $"Trail '{trail.DisplayName}' draws nothing at all.");
            }

            yield return null;
        }

        /// <summary>
        /// The pacing rule the whole shop exists for: finishing the campaign must buy
        /// the player something, and must come nowhere near buying everything.
        /// </summary>
        [UnityTest]
        public IEnumerator The_campaign_alone_cannot_empty_the_shop()
        {
            int total = 0;

            for (int i = 0; i < library.SkinCount; i++)
            {
                total += library.GetSkin(i).Price;
            }

            for (int i = 0; i < library.TrailCount; i++)
            {
                total += library.GetTrail(i).Price;
            }

            // A generous run through every level: three stars everywhere, three perfect
            // ringers a level. Better than most players will manage.
            int campaign = director.LevelCount * TicketRules.ForLevelClear(3, firstClear: true)
                           + director.LevelCount * 3 * TicketRules.ForPerfect();

            Assert.Greater(total, campaign * 4,
                $"The whole shop costs {total} and a perfect campaign pays {campaign}. "
                + "That is not progression, that is an afternoon.");

            // But the first paid item of each kind has to be within reach early, or the
            // shop is a locked door for the first hour.
            int earlyGame = 6 * TicketRules.ForLevelClear(2, firstClear: true);

            Assert.LessOrEqual(library.GetSkin(1).Price, earlyGame,
                "The first paid skin is out of reach for the whole of stage one.");
            Assert.LessOrEqual(library.GetTrail(1).Price, earlyGame,
                "The first paid trail is out of reach for the whole of stage one.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator Buying_spends_the_tickets_and_equips_the_item()
        {
            ProgressStore progress = director.Progress;
            progress.ResetAll(director.LevelCount);

            int price = library.GetSkin(1).Price;

            Assert.IsFalse(progress.BuySkin(1, price), "A skin was bought with no tickets.");

            progress.AddTickets(price);

            Assert.IsTrue(progress.BuySkin(1, price));
            Assert.AreEqual(0, progress.Tickets, "The purchase did not take the tickets.");
            Assert.IsTrue(progress.IsSkinOwned(1));

            Assert.IsTrue(progress.EquipSkin(1));
            Assert.AreEqual(1, progress.EquippedSkin);

            // And it cannot be bought twice.
            progress.AddTickets(price);
            Assert.IsFalse(progress.BuySkin(1, price), "An owned skin was bought again.");
            Assert.AreEqual(price, progress.Tickets, "A refused purchase still took the tickets.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator Nothing_unowned_can_be_equipped()
        {
            ProgressStore progress = director.Progress;
            progress.ResetAll(director.LevelCount);

            Assert.IsFalse(progress.EquipSkin(5), "An unowned skin was equipped.");
            Assert.IsFalse(progress.EquipTrail(5), "An unowned trail was equipped.");
            Assert.AreEqual(0, progress.EquippedSkin);
            Assert.AreEqual(0, progress.EquippedTrail);

            yield return null;
        }

        /// <summary>
        /// The answer to "does this device remember me": yes, and this is the check.
        /// A store that forgets is worse than no store.
        /// </summary>
        [UnityTest]
        public IEnumerator Purchases_and_progress_survive_a_fresh_store()
        {
            ProgressStore progress = director.Progress;
            progress.ResetAll(director.LevelCount);

            progress.AddTickets(5000);
            progress.BuySkin(2, library.GetSkin(2).Price);
            progress.BuyTrail(1, library.GetTrail(1).Price);
            progress.EquipSkin(2);
            progress.EquipTrail(1);
            progress.RecordCompletion(0, 3, 1234, director.LevelCount);

            int tickets = progress.Tickets;

            // A second store reading the same device-local save. This is what a restart
            // looks like from the game's point of view.
            GameObject host = new GameObject("Second Store");
            ProgressStore reopened = host.AddComponent<ProgressStore>();

            yield return null;

            Assert.AreEqual(tickets, reopened.Tickets, "Tickets did not persist.");
            Assert.IsTrue(reopened.IsSkinOwned(2), "A bought skin did not persist.");
            Assert.IsTrue(reopened.IsTrailOwned(1), "A bought trail did not persist.");
            Assert.AreEqual(2, reopened.EquippedSkin, "The equipped skin did not persist.");
            Assert.AreEqual(1, reopened.EquippedTrail, "The equipped trail did not persist.");
            Assert.AreEqual(3, reopened.StarsFor(0), "Stars did not persist.");
            Assert.AreEqual(1234, reopened.BestScoreFor(0), "The best score did not persist.");
            Assert.IsTrue(reopened.HasCleared(0), "The cleared flag did not persist.");

            Object.DestroyImmediate(host);
        }
    }
}
