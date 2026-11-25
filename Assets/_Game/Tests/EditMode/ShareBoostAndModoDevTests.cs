using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SlipperyZlope.RingToss.Save;
using UnityEngine;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// The 2x share bought with a rewarded advert, and modo_dev.
    ///
    /// Both live in ProgressStore over PlayerPrefs, and this runs inside the editor whose
    /// prefs the owner's own play sessions use — so every key either of them touches is
    /// read before and put back after, rather than wiped.
    /// </summary>
    public sealed class ShareBoostAndModoDevTests
    {
        private static readonly string[] Keys =
        {
            "ringtoss.share.boost",
            "ringtoss.dev.unlock",
            "ringtoss.owned.skins",
            "ringtoss.owned.trails",
            "ringtoss.equipped.skin",
            "ringtoss.equipped.trail",
            "ringtoss.tickets",
        };

        private readonly Dictionary<string, int?> saved = new Dictionary<string, int?>();
        private GameObject host;
        private ProgressStore store;

        [SetUp]
        public void SetUp()
        {
            saved.Clear();

            foreach (string key in Keys)
            {
                saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
                PlayerPrefs.DeleteKey(key);
            }

            host = new GameObject("Progress Under Test");
            store = host.AddComponent<ProgressStore>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);

            foreach (KeyValuePair<string, int?> entry in saved)
            {
                if (entry.Value.HasValue)
                {
                    PlayerPrefs.SetInt(entry.Key, entry.Value.Value);
                }
                else
                {
                    PlayerPrefs.DeleteKey(entry.Key);
                }
            }

            PlayerPrefs.Save();
        }

        [Test]
        public void A_boosted_share_pays_double_and_an_ordinary_one_does_not()
        {
            Assert.AreEqual(TicketRules.Share, TicketRules.ForShare(false));
            Assert.AreEqual(TicketRules.Share, TicketRules.ForShare());
            Assert.AreEqual(TicketRules.Share * 2, TicketRules.ForShare(true));
        }

        [Test]
        public void A_boosted_share_is_still_worth_less_than_two_adverts()
        {
            // The line that keeps sharing a thank-you rather than the way to earn.
            Assert.Less(TicketRules.ForShare(true), TicketRules.ForRewardedAd() * 2);
        }

        [Test]
        public void The_boost_is_spent_by_one_share_and_only_one()
        {
            Assert.IsFalse(store.HasShareBoost);

            store.GrantShareBoost();
            Assert.IsTrue(store.HasShareBoost);

            Assert.IsTrue(store.ConsumeShareBoost());
            Assert.IsFalse(store.HasShareBoost);
            Assert.IsFalse(store.ConsumeShareBoost(), "A spent boost paid a second time.");
        }

        [Test]
        public void Watching_twice_does_not_stack_the_boost_to_four_times()
        {
            store.GrantShareBoost();
            store.GrantShareBoost();

            Assert.IsTrue(store.ConsumeShareBoost());
            Assert.IsFalse(store.HasShareBoost);
        }

        [Test]
        public void Modo_dev_owns_everything_without_writing_to_the_save()
        {
            store.AddTickets(500);
            store.ModoDev = true;

            Assert.IsTrue(store.IsSkinOwned(5));
            Assert.IsTrue(store.IsTrailOwned(9));

            // Owned, so there is nothing to buy and nothing is charged.
            Assert.IsFalse(store.BuySkin(5, 150));
            Assert.AreEqual(500, store.Tickets);

            Assert.AreEqual(0, PlayerPrefs.GetInt("ringtoss.owned.skins", 0),
                "modo_dev wrote a purchase into the save.");
        }

        [Test]
        public void Switching_modo_dev_off_puts_the_real_purchases_back()
        {
            store.ModoDev = true;
            Assert.IsTrue(store.EquipSkin(7));
            Assert.IsTrue(store.EquipTrail(4));
            Assert.AreEqual(7, store.EquippedSkin);

            store.ModoDev = false;

            Assert.IsFalse(store.IsSkinOwned(7));
            Assert.AreEqual(0, store.EquippedSkin, "The ring kept a skin nobody bought.");
            Assert.AreEqual(0, store.EquippedTrail, "The ring kept a trail nobody bought.");
        }

        [Test]
        public void A_build_without_modo_dev_cannot_be_talked_into_it()
        {
            // The checkbox is compiled in. A player's build has it off, and nothing on the
            // device — including the Settings switch, which is not even built — can turn
            // it on: that is the whole reason there are two properties.
            Assert.IsFalse(store.ModoDevAvailable);

            store.SetDevUnlock(true);

            Assert.IsFalse(store.ModoDev, "A build without modo_dev unlocked the shop.");
            Assert.IsFalse(store.IsSkinOwned(5));
        }

        [Test]
        public void A_build_with_modo_dev_can_switch_it_off_on_the_device()
        {
            store.ModoDev = true;
            Assert.IsTrue(store.ModoDevAvailable);
            Assert.IsTrue(store.IsSkinOwned(5));

            // What the owner reaches from Settings: off, and back on, without a rebuild.
            store.SetDevUnlock(false);
            Assert.IsFalse(store.ModoDev);
            Assert.IsFalse(store.IsSkinOwned(5), "Switching it off did not put the shop back.");
            Assert.IsTrue(store.ModoDevAvailable, "The build still has it; only the device switch moved.");

            store.SetDevUnlock(true);
            Assert.IsTrue(store.ModoDev);
        }

        [Test]
        public void The_shipped_scene_has_modo_dev_off()
        {
            string scene = File.ReadAllText("Assets/_Game/Scenes/Main.unity");

            StringAssert.DoesNotContain("modoDev: 1", scene,
                "Main.unity has modo_dev switched on. Every skin and trail would be free in " +
                "any build made from it; switch it off on the ProgressStore component.");
        }
    }
}
