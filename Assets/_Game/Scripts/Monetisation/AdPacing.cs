namespace SlipperyZlope.RingToss.Monetisation
{
    /// <summary>
    /// Decides whether an unasked-for ad may be shown.
    ///
    /// This is the whole of the difference between a game with ads in it and a game
    /// that feels like an ad with a game in it, so it is pure logic with no Unity in
    /// it and it is tested directly. Every rule below is a rule about how the game
    /// feels, not about revenue:
    ///
    /// - a player who has just arrived has not agreed to anything yet, so the first
    ///   couple of levels are free of interruptions entirely;
    /// - the end of a lost level is where an advert goes. It used to be the one place
    ///   one never went — "a loss answered with an advert is a punishment" — and with
    ///   the run-end rules stacked on top, a whole recorded session of losing passed
    ///   without a single advert. The owner asked for the opposite, which is also where
    ///   most games of this kind put them: a loss is the natural break in play, a win
    ///   is momentum worth keeping;
    /// - an ad immediately after a rewarded ad is two ads, and the player only chose
    ///   one of them;
    /// - and a minimum gap in seconds, because a fast player and a slow player would
    ///   otherwise get very different games — plus a gap in levels for *wins*, so a
    ///   run of quick clears is not interrupted every time.
    ///
    /// The state is a struct so a caller can ask "would this show?" without the
    /// asking changing the answer.
    /// </summary>
    public readonly struct AdPacingState
    {
        public readonly int LevelsSinceLastAd;
        public readonly float SecondsSinceLastAd;
        public readonly float SecondsSinceSessionStart;
        public readonly int LevelsCompletedEver;
        public readonly bool LastLevelWasFailed;
        public readonly bool JustWatchedRewarded;

        public AdPacingState(
            int levelsSinceLastAd,
            float secondsSinceLastAd,
            float secondsSinceSessionStart,
            int levelsCompletedEver,
            bool lastLevelWasFailed,
            bool justWatchedRewarded)
        {
            LevelsSinceLastAd = levelsSinceLastAd;
            SecondsSinceLastAd = secondsSinceLastAd;
            SecondsSinceSessionStart = secondsSinceSessionStart;
            LevelsCompletedEver = levelsCompletedEver;
            LastLevelWasFailed = lastLevelWasFailed;
            JustWatchedRewarded = justWatchedRewarded;
        }
    }

    /// <summary>
    /// What is known at the end of a generated run.
    ///
    /// A separate state from <see cref="AdPacingState"/> because a run is not a level
    /// and the questions that pace one do not pace the other. Two differences carry
    /// the whole thing:
    ///
    /// - <b>Every run ends in a loss.</b> The level rule "never straight after a
    ///   failure" would forbid this placement entirely rather than pace it, so it does
    ///   not apply here. What replaces it is <see cref="RunSeconds"/>: a run the
    ///   player survived for a while has earned a break, and a run that ended in
    ///   fifteen seconds has not — interrupting *that* one is kicking somebody who is
    ///   already having a bad time, and it is the single fastest way to make a game
    ///   feel predatory.
    /// - <b>Levels completed is the wrong grace period.</b> It counts authored levels,
    ///   and a player who only ever presses PLAY never completes one, so the level
    ///   grace would silently never expire. The grace here is runs *this session*, so
    ///   arriving and playing is always free of adverts.
    /// </summary>
    public readonly struct AdRunEndState
    {
        /// <summary>How long the run that just ended lasted.</summary>
        public readonly float RunSeconds;

        /// <summary>Runs finished this session, not counting the one that just ended.</summary>
        public readonly int RunsThisSession;

        public readonly float SecondsSinceLastAd;
        public readonly float SecondsSinceSessionStart;
        public readonly bool JustWatchedRewarded;

        public AdRunEndState(
            float runSeconds,
            int runsThisSession,
            float secondsSinceLastAd,
            float secondsSinceSessionStart,
            bool justWatchedRewarded)
        {
            RunSeconds = runSeconds;
            RunsThisSession = runsThisSession;
            SecondsSinceLastAd = secondsSinceLastAd;
            SecondsSinceSessionStart = secondsSinceSessionStart;
            JustWatchedRewarded = justWatchedRewarded;
        }
    }

    /// <summary>Why an interstitial was or was not allowed. Worth logging; never shown to the player.</summary>
    public enum AdPacingVerdict
    {
        Allowed,
        DisabledEntirely,
        WithinGracePeriod,
        SessionTooYoung,
        TooSoonInLevels,
        TooSoonInSeconds,
        FollowsAFailure,
        FollowsARewardedAd,

        /// <summary>The run was too short to interrupt. See <see cref="AdRunEndState"/>.</summary>
        RunTooShort
    }

    public static class AdPacing
    {
        public static bool Allows(AdPacingVerdict verdict) => verdict == AdPacingVerdict.Allowed;

        /// <summary>
        /// The rules, in the order a player would notice them being broken.
        ///
        /// Order matters only for the reason reported, not for the outcome: every
        /// rule is a veto.
        /// </summary>
        public static AdPacingVerdict Evaluate(in AdPacingState state, AdPacingRules rules)
        {
            if (!rules.InterstitialsEnabled)
            {
                return AdPacingVerdict.DisabledEntirely;
            }

            if (state.LevelsCompletedEver < rules.GraceLevels)
            {
                return AdPacingVerdict.WithinGracePeriod;
            }

            if (state.SecondsSinceSessionStart < rules.MinimumSessionSeconds)
            {
                return AdPacingVerdict.SessionTooYoung;
            }

            if (state.JustWatchedRewarded)
            {
                return AdPacingVerdict.FollowsARewardedAd;
            }

            if (rules.SkipAfterFailure && state.LastLevelWasFailed)
            {
                return AdPacingVerdict.FollowsAFailure;
            }

            // A loss does not wait out the level gap: it only has to clear the time gap
            // below, which still means two quick failures carry one advert, not two.
            if (!state.LastLevelWasFailed && state.LevelsSinceLastAd < rules.MinimumLevelsBetween)
            {
                return AdPacingVerdict.TooSoonInLevels;
            }

            if (state.SecondsSinceLastAd < rules.MinimumSecondsBetween)
            {
                return AdPacingVerdict.TooSoonInSeconds;
            }

            return AdPacingVerdict.Allowed;
        }

        /// <summary>
        /// The same question asked at the end of a generated run. Every rule is still
        /// a veto; see <see cref="AdRunEndState"/> for the two that differ.
        /// </summary>
        public static AdPacingVerdict EvaluateRunEnd(in AdRunEndState state, AdPacingRules rules)
        {
            if (!rules.InterstitialsEnabled)
            {
                return AdPacingVerdict.DisabledEntirely;
            }

            if (state.RunsThisSession < rules.GraceRuns)
            {
                return AdPacingVerdict.WithinGracePeriod;
            }

            if (state.SecondsSinceSessionStart < rules.MinimumSessionSeconds)
            {
                return AdPacingVerdict.SessionTooYoung;
            }

            if (state.JustWatchedRewarded)
            {
                return AdPacingVerdict.FollowsARewardedAd;
            }

            if (state.RunSeconds < rules.MinimumRunSeconds)
            {
                return AdPacingVerdict.RunTooShort;
            }

            if (state.SecondsSinceLastAd < rules.MinimumSecondsBetween)
            {
                return AdPacingVerdict.TooSoonInSeconds;
            }

            return AdPacingVerdict.Allowed;
        }
    }

    /// <summary>
    /// The numbers behind <see cref="AdPacing"/>, as a plain struct so the rules can
    /// be tested without loading an asset.
    /// </summary>
    public readonly struct AdPacingRules
    {
        public readonly bool InterstitialsEnabled;
        public readonly int GraceLevels;
        public readonly int MinimumLevelsBetween;
        public readonly float MinimumSecondsBetween;
        public readonly float MinimumSessionSeconds;
        public readonly bool SkipAfterFailure;

        /// <summary>Runs a session gets before the end of one may carry an advert.</summary>
        public readonly int GraceRuns;

        /// <summary>How long a run has to have lasted to be worth interrupting.</summary>
        public readonly float MinimumRunSeconds;

        public AdPacingRules(
            bool interstitialsEnabled,
            int graceLevels,
            int minimumLevelsBetween,
            float minimumSecondsBetween,
            float minimumSessionSeconds,
            bool skipAfterFailure,
            int graceRuns,
            float minimumRunSeconds)
        {
            InterstitialsEnabled = interstitialsEnabled;
            GraceLevels = graceLevels;
            MinimumLevelsBetween = minimumLevelsBetween;
            MinimumSecondsBetween = minimumSecondsBetween;
            MinimumSessionSeconds = minimumSessionSeconds;
            SkipAfterFailure = skipAfterFailure;
            GraceRuns = graceRuns;
            MinimumRunSeconds = minimumRunSeconds;
        }
    }
}
