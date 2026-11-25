using NUnit.Framework;
using SlipperyZlope.RingToss.Save;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// Share with a friend: the link has to be the store page, and the tickets have to
    /// come at most once every two minutes however the player — or the clock — behaves.
    /// </summary>
    public sealed class ShareRulesTests
    {
        [Test]
        public void The_link_is_the_store_page_for_this_package_tagged_as_a_share()
        {
            string link = ShareRules.StoreLink("title");

            StringAssert.StartsWith(
                "https://play.google.com/store/apps/details?id=com.slipperyzlope.ringtoss", link);
            StringAssert.Contains("referrer=utm_source%3Dtitle%26utm_medium%3Dshare", link);
        }

        [Test]
        public void A_source_that_would_break_the_url_is_not_used_as_one()
        {
            string link = ShareRules.StoreLink("a b&c=d");

            StringAssert.Contains("utm_source%3Dshare%26", link);
            StringAssert.DoesNotContain(" ", link);
        }

        [Test]
        public void The_message_leads_with_a_best_round_only_when_there_is_one()
        {
            StringAssert.Contains("round 11", ShareRules.Message(11, "LINK"));
            StringAssert.DoesNotContain("round", ShareRules.Message(0, "LINK"));
            StringAssert.EndsWith("LINK", ShareRules.Message(0, "LINK"));
        }

        [Test]
        public void A_player_who_has_never_shared_is_paid_straight_away()
        {
            Assert.AreEqual(0d, ShareRules.SecondsUntilReward(1_000_000d, 0d));
            Assert.IsTrue(ShareRules.CanReward(1_000_000d, 0d));
        }

        [Test]
        public void The_cooldown_is_two_minutes_and_ends_exactly_there()
        {
            const double paid = 1_000_000d;

            Assert.AreEqual(90d, ShareRules.SecondsUntilReward(paid + 30d, paid), 1e-9);
            Assert.IsFalse(ShareRules.CanReward(paid + 119.9d, paid));
            Assert.IsTrue(ShareRules.CanReward(paid + 120d, paid));
        }

        [Test]
        public void Winding_the_clock_back_cannot_pay_twice()
        {
            // Paid "in an hour's time" because the clock was moved back an hour.
            const double now = 1_000_000d;
            const double paid = now + 3600d;

            Assert.IsTrue(ShareRules.IsFromTheFuture(now, paid));
            Assert.IsFalse(ShareRules.CanReward(now, paid));
            Assert.AreEqual(ShareRules.CooldownSeconds, ShareRules.SecondsUntilReward(now, paid));
        }
    }
}
