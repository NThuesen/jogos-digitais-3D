using NUnit.Framework;
using SlipperyZlope.RingToss.Monetisation;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// The rules that decide whether the game feels like a game with ads in it or an
    /// advert with a game in it.
    ///
    /// Worth testing directly rather than by playing, because the failure is not a
    /// crash: it is a player who is shown two ads in ninety seconds and never comes
    /// back, which no amount of manual testing by somebody who already likes the game
    /// will ever surface.
    /// </summary>
    public sealed class AdPacingTests
    {
        private static AdPacingRules Rules(
            bool enabled = true,
            int grace = 5,
            int levels = 3,
            float seconds = 120f,
            float session = 60f,
            bool skipAfterFailure = true,
            int graceRuns = 1,
            float runSeconds = 60f)
        {
            return new AdPacingRules(
                enabled, grace, levels, seconds, session, skipAfterFailure, graceRuns, runSeconds);
        }

        /// <summary>A run-end state with every gate already satisfied, to break one at a time.</summary>
        private static AdRunEndState RunReady(
            float runSeconds = 300f,
            int runsThisSession = 4,
            float secondsSinceAd = 900f,
            float session = 900f,
            bool justWatchedRewarded = false)
        {
            return new AdRunEndState(
                runSeconds, runsThisSession, secondsSinceAd, session, justWatchedRewarded);
        }

        [Test]
        public void A_long_run_that_ends_may_carry_an_advert()
        {
            Assert.AreEqual(
                AdPacingVerdict.Allowed,
                AdPacing.EvaluateRunEnd(RunReady(), Rules()));
        }

        [Test]
        public void A_run_that_ended_almost_immediately_is_never_interrupted()
        {
            // The rule the whole placement rests on. A player losing in fifteen seconds
            // is having a bad time already, and that is the worst possible moment to
            // take the game away from them.
            Assert.AreEqual(
                AdPacingVerdict.RunTooShort,
                AdPacing.EvaluateRunEnd(RunReady(runSeconds: 15f), Rules(runSeconds: 60f)));
        }

        [Test]
        public void The_first_run_of_a_session_is_free_of_adverts()
        {
            Assert.AreEqual(
                AdPacingVerdict.WithinGracePeriod,
                AdPacing.EvaluateRunEnd(RunReady(runsThisSession: 0), Rules(graceRuns: 1)));
        }

        [Test]
        public void A_run_end_does_not_follow_a_rewarded_advert()
        {
            Assert.AreEqual(
                AdPacingVerdict.FollowsARewardedAd,
                AdPacing.EvaluateRunEnd(RunReady(justWatchedRewarded: true), Rules()));
        }

        [Test]
        public void Two_run_ends_in_quick_succession_only_carry_one_advert()
        {
            Assert.AreEqual(
                AdPacingVerdict.TooSoonInSeconds,
                AdPacing.EvaluateRunEnd(RunReady(secondsSinceAd: 10f), Rules(seconds: 120f)));
        }

        [Test]
        public void Run_end_adverts_stop_dead_when_interstitials_are_off()
        {
            Assert.AreEqual(
                AdPacingVerdict.DisabledEntirely,
                AdPacing.EvaluateRunEnd(RunReady(), Rules(enabled: false)));
        }

        private static AdPacingState Ready(
            int levelsSince = 9,
            float secondsSince = 900f,
            float session = 900f,
            int completedEver = 40,
            bool failed = false,
            bool rewarded = false)
        {
            return new AdPacingState(levelsSince, secondsSince, session, completedEver, failed, rewarded);
        }

        [Test]
        public void A_settled_session_past_every_gate_is_allowed()
        {
            // The control. Without this, every test below could be passing because
            // nothing is ever allowed.
            Assert.AreEqual(AdPacingVerdict.Allowed, AdPacing.Evaluate(Ready(), Rules()));
        }

        [Test]
        public void Ads_off_means_never()
        {
            Assert.AreEqual(
                AdPacingVerdict.DisabledEntirely,
                AdPacing.Evaluate(Ready(), Rules(enabled: false)));
        }

        [Test]
        public void A_new_player_is_never_interrupted()
        {
            // A player who has not yet decided whether they like the game is the last
            // person who should be shown an advert.
            Assert.AreEqual(
                AdPacingVerdict.WithinGracePeriod,
                AdPacing.Evaluate(Ready(completedEver: 2), Rules(grace: 5)));
        }

        [Test]
        public void The_grace_period_ends_exactly_where_it_says()
        {
            Assert.AreEqual(
                AdPacingVerdict.WithinGracePeriod,
                AdPacing.Evaluate(Ready(completedEver: 4), Rules(grace: 5)));

            Assert.AreEqual(
                AdPacingVerdict.Allowed,
                AdPacing.Evaluate(Ready(completedEver: 5), Rules(grace: 5)));
        }

        [Test]
        public void No_ad_in_the_opening_seconds_of_a_session()
        {
            // A veteran reopening the app has cleared the grace period long ago, and
            // would otherwise be met by an advert before the first throw.
            Assert.AreEqual(
                AdPacingVerdict.SessionTooYoung,
                AdPacing.Evaluate(Ready(session: 20f), Rules(session: 60f)));
        }

        [Test]
        public void Losing_is_not_answered_with_an_advert()
        {
            Assert.AreEqual(
                AdPacingVerdict.FollowsAFailure,
                AdPacing.Evaluate(Ready(failed: true), Rules()));
        }

        [Test]
        public void That_rule_can_be_turned_off_deliberately()
        {
            Assert.AreEqual(
                AdPacingVerdict.Allowed,
                AdPacing.Evaluate(Ready(failed: true), Rules(skipAfterFailure: false)));
        }

        [Test]
        public void A_rewarded_ad_is_not_followed_by_an_unasked_one()
        {
            // The player chose one advert. Handing them a second is a bait and switch,
            // and it is the fastest way to teach somebody never to tap a rewarded
            // button again.
            Assert.AreEqual(
                AdPacingVerdict.FollowsARewardedAd,
                AdPacing.Evaluate(Ready(rewarded: true), Rules()));
        }

        [Test]
        public void Both_gaps_are_enforced_independently()
        {
            // A fast player clears levels quickly and a slow one takes minutes over
            // each. Either gap alone gives one of them a very different game.
            Assert.AreEqual(
                AdPacingVerdict.TooSoonInLevels,
                AdPacing.Evaluate(Ready(levelsSince: 1), Rules(levels: 3)));

            Assert.AreEqual(
                AdPacingVerdict.TooSoonInSeconds,
                AdPacing.Evaluate(Ready(secondsSince: 30f), Rules(seconds: 120f)));
        }

        [Test]
        public void Allows_agrees_with_the_verdict()
        {
            Assert.IsTrue(AdPacing.Allows(AdPacingVerdict.Allowed));

            foreach (AdPacingVerdict verdict in System.Enum.GetValues(typeof(AdPacingVerdict)))
            {
                if (verdict != AdPacingVerdict.Allowed)
                {
                    Assert.IsFalse(AdPacing.Allows(verdict), $"{verdict} must not permit an ad.");
                }
            }
        }
    }

    /// <summary>
    /// What may lawfully be requested, given what the player has been asked.
    ///
    /// These are the assertions that would fail if somebody later "simplified" the
    /// consent path, which is exactly the change that turns a compliant build into a
    /// non-compliant one without any visible symptom.
    /// </summary>
    public sealed class ConsentPolicyTests
    {
        [Test]
        public void Nothing_is_requested_before_the_player_has_been_asked()
        {
            // Requesting an ad in order to work out whether to ask for consent is the
            // wrong way round, and is how an advertising identifier leaves a device
            // with no basis for sending it.
            Assert.AreEqual(
                AdPersonalisation.None,
                ConsentPolicy.Resolve(ConsentStatus.Unknown, playerIsUnderage: false, trackingAuthorised: true));
        }

        [Test]
        public void An_unknown_status_is_still_none_even_for_an_adult_who_allowed_tracking()
        {
            Assert.AreEqual(
                AdPersonalisation.None,
                ConsentPolicy.Resolve(ConsentStatus.Unknown, false, true));
        }

        [Test]
        public void Refusing_leaves_the_game_working_on_contextual_ads()
        {
            // Refusal must not be a dead end. Contextual advertising needs no consent
            // and no identifier, so a player who says no is not punished for it.
            Assert.AreEqual(
                AdPersonalisation.NonPersonalised,
                ConsentPolicy.Resolve(ConsentStatus.Denied, false, true));
        }

        [Test]
        public void A_child_never_gets_personalised_ads_whatever_the_dialog_recorded()
        {
            Assert.AreEqual(
                AdPersonalisation.NonPersonalised,
                ConsentPolicy.Resolve(ConsentStatus.Granted, playerIsUnderage: true, trackingAuthorised: true));
        }

        [Test]
        public void Consent_without_tracking_permission_is_still_not_personalised()
        {
            // iOS: agreeing to personalised advertising and refusing App Tracking
            // Transparency means no identifier, so the ad has to be contextual.
            Assert.AreEqual(
                AdPersonalisation.NonPersonalised,
                ConsentPolicy.Resolve(ConsentStatus.Granted, false, trackingAuthorised: false));
        }

        [Test]
        public void Everything_lined_up_gives_personalised_ads()
        {
            Assert.AreEqual(
                AdPersonalisation.Personalised,
                ConsentPolicy.Resolve(ConsentStatus.Granted, false, true));

            Assert.AreEqual(
                AdPersonalisation.Personalised,
                ConsentPolicy.Resolve(ConsentStatus.NotRequired, false, true));
        }

        [Test]
        public void The_dialog_is_shown_once_and_a_refusal_is_an_answer()
        {
            Assert.IsTrue(ConsentPolicy.MustAsk(ConsentStatus.Unknown));
            Assert.IsFalse(ConsentPolicy.MustAsk(ConsentStatus.Denied),
                "Re-asking a player who said no is the behaviour the rules exist to stop.");
            Assert.IsFalse(ConsentPolicy.MustAsk(ConsentStatus.Granted));
        }

        [Test]
        public void Anyone_who_answered_can_change_their_mind()
        {
            // Withdrawing consent has to be as easy as giving it, so the settings
            // entry point is required after a refusal as well as after agreement.
            Assert.IsTrue(ConsentPolicy.NeedsWithdrawalEntryPoint(ConsentStatus.Granted));
            Assert.IsTrue(ConsentPolicy.NeedsWithdrawalEntryPoint(ConsentStatus.Denied));
            Assert.IsFalse(ConsentPolicy.NeedsWithdrawalEntryPoint(ConsentStatus.Unknown));
        }
    }
}
