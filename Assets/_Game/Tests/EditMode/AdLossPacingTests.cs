using NUnit.Framework;
using SlipperyZlope.RingToss.Monetisation;
using UnityEditor;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// Adverts after a loss: where the owner asked for them, and still paced.
    ///
    /// Run against the shipped AdConfig asset rather than against numbers typed into the
    /// test, because in this project the asset is what the game runs on and a default in
    /// the source is not. The session these are built from is the playtest recording of
    /// 2026-09-10, in which the game was lost several times and no advert ever appeared.
    /// </summary>
    public sealed class AdLossPacingTests
    {
        private static AdPacingRules Shipped()
        {
            AdConfig config = AssetDatabase.LoadAssetAtPath<AdConfig>("Assets/_Game/Data/AdConfig.asset");
            Assert.IsNotNull(config, "AdConfig.asset is missing.");
            return config.PacingRules;
        }

        private static AdPacingState AfterLevel(bool failed, int levelsSince = 1, float secondsSince = 600f) =>
            new AdPacingState(levelsSince, secondsSince, 600f, 10, failed, false);

        [Test]
        public void A_lost_level_may_carry_an_advert()
        {
            Assert.AreEqual(AdPacingVerdict.Allowed, AdPacing.Evaluate(AfterLevel(failed: true), Shipped()));
        }

        [Test]
        public void A_loss_does_not_wait_out_the_level_gap_but_a_win_does()
        {
            AdPacingRules rules = Shipped();

            Assert.AreEqual(AdPacingVerdict.Allowed,
                AdPacing.Evaluate(AfterLevel(failed: true, levelsSince: 1), rules));
            Assert.AreEqual(AdPacingVerdict.TooSoonInLevels,
                AdPacing.Evaluate(AfterLevel(failed: false, levelsSince: 1), rules));
        }

        [Test]
        public void Two_losses_in_quick_succession_carry_one_advert()
        {
            Assert.AreEqual(AdPacingVerdict.TooSoonInSeconds,
                AdPacing.Evaluate(AfterLevel(failed: true, secondsSince: 20f), Shipped()));
        }

        [Test]
        public void The_run_from_the_recording_now_ends_with_an_advert()
        {
            // The first endless run of the session, over after 56 seconds.
            AdRunEndState run = new AdRunEndState(56f, 0, float.PositiveInfinity, 56f, false);

            Assert.AreEqual(AdPacingVerdict.Allowed, AdPacing.EvaluateRunEnd(run, Shipped()));

            // Under the numbers it shipped with it could not have had one: the first run
            // of a session was always free, and a run had to have lasted a minute.
            AdPacingRules before = new AdPacingRules(true, 5, 3, 120f, 60f, true, 1, 60f);
            Assert.AreNotEqual(AdPacingVerdict.Allowed, AdPacing.EvaluateRunEnd(run, before));
        }

        [Test]
        public void Still_nothing_in_the_opening_seconds_of_a_session()
        {
            AdRunEndState run = new AdRunEndState(25f, 0, float.PositiveInfinity, 25f, false);
            Assert.AreEqual(AdPacingVerdict.SessionTooYoung, AdPacing.EvaluateRunEnd(run, Shipped()));
        }

        [Test]
        public void Still_nothing_straight_after_a_rewarded_advert()
        {
            AdRunEndState run = new AdRunEndState(300f, 3, float.PositiveInfinity, 900f, true);
            Assert.AreEqual(AdPacingVerdict.FollowsARewardedAd, AdPacing.EvaluateRunEnd(run, Shipped()));
        }
    }
}
