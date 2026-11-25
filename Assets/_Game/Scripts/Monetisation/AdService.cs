using System;
using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.Save;
using UnityEngine;

namespace SlipperyZlope.RingToss.Monetisation
{
    /// <summary>
    /// The game's single point of contact with advertising.
    ///
    /// Nothing else in the project references a placement or a consent status. That is
    /// worth one layer of indirection, because the rules about when an ad may be shown
    /// are rules about how the game *feels* — they belong in one place where they can
    /// be read, argued with and tested, not scattered across the screens that happen
    /// to trigger them.
    ///
    /// It is only one layer. This used to resolve an <c>IAdProvider</c> out of a
    /// serialized MonoBehaviour, with a null implementation, a simulator and a
    /// LevelPlay implementation behind it — four places to look to answer "what
    /// happens when I finish a level", which is one question. There is now exactly one
    /// thing that shows an advert and it is <see cref="UnityAds"/>.
    ///
    /// The whole thing is a no-op when <see cref="AdConfig.AdsEnabled"/> is off,
    /// which is the default and is how the game is developed.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public sealed class AdService : MonoBehaviour
    {
        [SerializeField] private AdConfig config;
        [SerializeField] private ConsentService consent;
        [SerializeField] private ProgressStore progress;

        [Tooltip("Unity's mediation. Optional: with none assigned nothing is ever " +
                 "shown, which is the correct behaviour for a build without it.")]
        [SerializeField] private UnityAds ads;

        [Tooltip("How long the game may stay frozen behind an advert before it gives " +
                 "up and carries on. Long enough for a full rewarded video, because " +
                 "cutting one short costs the player their reward; short enough that " +
                 "an SDK that has gone silent does not look like a crash. Zero " +
                 "disables the watchdog, which is not advisable.")]
        [Range(0f, 300f)] [SerializeField] private float showTimeoutSeconds = 90f;

        private float sessionStart;
        private float lastAdTime = float.NegativeInfinity;
        private int levelsSinceLastAd;
        private bool lastLevelWasFailed;
        private bool justWatchedRewarded;
        private int continuesUsedThisRun;
        private bool showing;
        private float runStarted;
        private float lastRunSeconds;
        private int runsThisSession;

        // The frame each end was last booked on. See RecordLevelEnd.
        private int levelRecordedFrame = -1;
        private int runRecordedFrame = -1;
        private float lastTicketAdTime = float.NegativeInfinity;

        public bool AdsAvailable => config != null && config.AdsEnabled;

        /// <summary>
        /// Whether this build offers rewarded adverts at all.
        ///
        /// A property about the *build*, not about the SDK's mood. It is what decides
        /// whether the "watch for tickets" control exists, and the whole point is that
        /// it does not change while the game is running — a button that comes and goes
        /// with the fill rate is a button nobody finds.
        /// </summary>
        public bool RewardedOffered => config != null && config.RewardedEnabled;

        /// <summary>Whether there is anything that could show an advert at all.</summary>
        private bool HasAds => ads != null;

        public AdPersonalisation Personalisation =>
            consent == null ? AdPersonalisation.None : consent.Personalisation;

        private float baseFixedDeltaTime;

        private void Awake()
        {
            sessionStart = Time.realtimeSinceStartup;
            baseFixedDeltaTime = Time.fixedDeltaTime;
        }

        /// <summary>
        /// Starts the SDK for a returning player.
        ///
        /// A player who answered on a previous run is never shown the dialog again, so
        /// without this the mediator would only ever initialise on the one launch
        /// where consent was first given — and every launch afterwards would silently
        /// have no ads at all.
        /// </summary>
        private void Start()
        {
            if (AdsAvailable && consent != null && !consent.MustAsk)
            {
                InitialiseAfterConsent();
            }
        }

        private void OnEnable()
        {
            GameEvents.LevelEnded += OnLevelEnded;
            GameEvents.RoundStarted += OnRoundStarted;
            GameEvents.RunEnded += OnRunEnded;
        }

        private void OnDisable()
        {
            GameEvents.LevelEnded -= OnLevelEnded;
            GameEvents.RoundStarted -= OnRoundStarted;
            GameEvents.RunEnded -= OnRunEnded;
        }

        public void Configure(
            AdConfig adConfig, ConsentService consentService, ProgressStore store, UnityAds mediation)
        {
            config = adConfig;
            consent = consentService;
            progress = store;
            ads = mediation;
        }

        /// <summary>
        /// Starts the mediator, once, after consent is known.
        ///
        /// Deliberately not called from Awake. Most mediation SDKs begin collecting an
        /// advertising identifier the moment they initialise, so initialising before
        /// the player has answered would leak the very thing the dialog exists to
        /// govern — and it would do it on the first run, before anybody could object.
        /// </summary>
        public void InitialiseAfterConsent()
        {
            if (!AdsAvailable || !HasAds || ads.IsInitialised)
            {
                return;
            }

            AdPersonalisation personalisation = Personalisation;

            if (personalisation == AdPersonalisation.None)
            {
                Debug.Log("[Ads] Consent not resolved; the SDK stays uninitialised.");
                return;
            }

            ads.Initialise(personalisation, ready =>
                Debug.Log($"[Ads] {ads.Name} initialised: {ready}, {personalisation}."));
        }

        /// <summary>Pushes a changed consent decision down to a running SDK.</summary>
        public void ApplyConsentChange()
        {
            if (!AdsAvailable || !HasAds)
            {
                return;
            }

            if (!ads.IsInitialised)
            {
                InitialiseAfterConsent();
                return;
            }

            ads.SetPersonalisation(Personalisation);
        }

        // -- Interstitials ---------------------------------------------------

        /// <summary>Why an interstitial would or would not be shown right now.</summary>
        public AdPacingVerdict InterstitialVerdict()
        {
            if (!AdsAvailable || Personalisation == AdPersonalisation.None)
            {
                return AdPacingVerdict.DisabledEntirely;
            }

            AdPacingState state = new AdPacingState(
                levelsSinceLastAd,
                Time.realtimeSinceStartup - lastAdTime,
                Time.realtimeSinceStartup - sessionStart,
                progress == null ? 0 : progress.HighestUnlockedLevel,
                lastLevelWasFailed,
                justWatchedRewarded);

            return AdPacing.Evaluate(state, config.PacingRules);
        }

        /// <summary>
        /// Shows an interstitial if every pacing rule allows it. The callback always
        /// runs, so a caller can use it as "carry on now" without caring whether an
        /// ad happened.
        /// </summary>
        public void TryShowInterstitial(Action<AdResult> onComplete)
        {
            AdPacingVerdict verdict = InterstitialVerdict();

            if (!AdPacing.Allows(verdict) || showing)
            {
                onComplete?.Invoke(AdResult.Suppressed);
                return;
            }

            if (!HasAds || !ads.IsReady(AdPlacement.Interstitial))
            {
                // Not an error and not the player's problem: carry on silently.
                onComplete?.Invoke(AdResult.NotReady);
                return;
            }

            Present(AdPlacement.Interstitial, result =>
            {
                if (result == AdResult.Watched || result == AdResult.Skipped)
                {
                    lastAdTime = Time.realtimeSinceStartup;
                    levelsSinceLastAd = 0;
                }

                onComplete?.Invoke(result);
            });
        }

        /// <summary>
        /// Shows an interstitial at the end of a level, if pacing allows one, and books
        /// the level first so the verdict is about *this* level. See RecordLevelEnd.
        /// </summary>
        public void TryShowLevelEndInterstitial(LevelResult result, Action<AdResult> onComplete)
        {
            RecordLevelEnd(result);
            TryShowInterstitial(onComplete);
        }

        // -- Run-end interstitial --------------------------------------------

        /// <summary>Why an advert would or would not follow the run that just ended.</summary>
        public AdPacingVerdict RunEndVerdict()
        {
            if (!AdsAvailable || Personalisation == AdPersonalisation.None)
            {
                return AdPacingVerdict.DisabledEntirely;
            }

            AdRunEndState state = new AdRunEndState(
                lastRunSeconds,
                runsThisSession - 1,
                Time.realtimeSinceStartup - lastAdTime,
                Time.realtimeSinceStartup - sessionStart,
                justWatchedRewarded);

            return AdPacing.EvaluateRunEnd(state, config.PacingRules);
        }

        /// <summary>
        /// Shows an advert after a run has ended, if the run was long enough to have
        /// earned the break and the rest of the pacing allows it.
        ///
        /// The callback always runs, so the caller can use it as "now show the results
        /// screen" without caring whether an advert happened. It has to be used that
        /// way round: putting the results up first and the advert on top of them means
        /// the player reads their score twice, once through an advert.
        /// </summary>
        public void TryShowRunEndInterstitial(Action<AdResult> onComplete)
        {
            RecordRunEnd();

            AdPacingVerdict verdict = RunEndVerdict();

            if (!AdPacing.Allows(verdict) || showing)
            {
                onComplete?.Invoke(AdResult.Suppressed);
                return;
            }

            if (!HasAds || !ads.IsReady(AdPlacement.Interstitial))
            {
                onComplete?.Invoke(AdResult.NotReady);
                return;
            }

            Present(AdPlacement.Interstitial, result =>
            {
                if (result == AdResult.Watched || result == AdResult.Skipped)
                {
                    lastAdTime = Time.realtimeSinceStartup;
                    levelsSinceLastAd = 0;
                }

                onComplete?.Invoke(result);
            });
        }

        // -- Rewarded --------------------------------------------------------

        /// <summary>Whether a rewarded ad can honestly be offered. Never offer one that cannot be filled.</summary>
        public bool CanOffer(AdPlacement placement)
        {
            if (!AdsAvailable || !config.RewardedEnabled || showing)
            {
                return false;
            }

            if (Personalisation == AdPersonalisation.None)
            {
                return false;
            }

            if (placement == AdPlacement.RewardedContinue &&
                continuesUsedThisRun >= config.MaxContinuesPerRun)
            {
                return false;
            }

            if (placement == AdPlacement.RewardedFreeTickets && TicketAdCooldownRemaining > 0f)
            {
                return false;
            }

            return HasAds && ads.IsReady(placement);
        }

        /// <summary>
        /// Seconds until the voluntary ticket advert can be offered again, or zero.
        ///
        /// Exposed rather than kept private because the button has to be able to say
        /// why it is not available. A control that is simply greyed out with no reason
        /// reads as broken, and a player who has just watched an advert and been told
        /// "not now" with no explanation reasonably concludes they were cheated.
        /// </summary>
        public float TicketAdCooldownRemaining
        {
            get
            {
                if (config == null)
                {
                    return 0f;
                }

                float elapsed = Time.realtimeSinceStartup - lastTicketAdTime;
                return Mathf.Max(0f, config.RewardedTicketCooldown - elapsed);
            }
        }

        /// <summary>
        /// Shows a rewarded ad the player asked for. The reward is granted only on
        /// <see cref="AdResult.Watched"/>; every other outcome, failure included, must
        /// leave the player no worse off than if they had never tapped.
        /// </summary>
        public void ShowRewarded(AdPlacement placement, Action<AdResult> onComplete)
        {
            if (!CanOffer(placement))
            {
                onComplete?.Invoke(AdResult.NotReady);
                return;
            }

            Present(placement, result =>
            {
                if (result == AdResult.Watched)
                {
                    justWatchedRewarded = true;

                    if (placement == AdPlacement.RewardedContinue)
                    {
                        continuesUsedThisRun++;
                    }

                    // The cooldown starts when one is actually watched, not when one is
                    // attempted. A no-fill must not cost the player their next offer.
                    if (placement == AdPlacement.RewardedFreeTickets)
                    {
                        lastTicketAdTime = Time.realtimeSinceStartup;
                    }
                }

                onComplete?.Invoke(result);
            });
        }

        // -- Presentation ----------------------------------------------------

        /// <summary>
        /// Silences and freezes the game around an ad, and guarantees it comes back.
        ///
        /// The guard against a provider calling back twice is not defensive
        /// programming for its own sake: a double callback would resume a game that is
        /// already running and, worse, grant a reward twice.
        /// </summary>
        private void Present(AdPlacement placement, Action<AdResult> onComplete)
        {
            showing = true;

            // Freezing and silencing is the whole of what the game has to do: a real
            // mediator puts a native view over everything, so there is nothing left to
            // hide. Deliberately no "an ad is showing" event — nothing needs one, and
            // an extension point with no consumer is a guess about the future.
            Time.timeScale = 0f;
            AudioListener.pause = true;

            bool finished = false;

            void Settle(AdResult result)
            {
                if (finished)
                {
                    Debug.LogWarning($"[Ads] {ads.Name} reported {placement} finished twice.");
                    return;
                }

                finished = true;
                showing = false;

                if (watchdog != null)
                {
                    StopCoroutine(watchdog);
                    watchdog = null;
                }

                // Resumed at 1, not at whatever the clock happened to read when the ad
                // started. Nothing in this game owns the time scale for longer than a
                // moment — there is no timescale pause — and the one thing that touches
                // it at all is the hit stop on a catch, which lasts a tenth of a second.
                // Capturing that and handing it back after the ad left the game running
                // at a twentieth speed with nothing left to restore it, which is a hang
                // the player can only escape by force quitting.
                Time.timeScale = 1f;
                Time.fixedDeltaTime = baseFixedDeltaTime;
                AudioListener.pause = false;

                onComplete?.Invoke(result);
            }

            // A provider that never calls back leaves the game frozen at timeScale 0
            // with no way out but force quitting. That is not a hypothetical: an
            // agent session hung exactly here for sixteen minutes, rendering at full
            // tilt with a stopped clock.
            //
            // The SDK is somebody else's code running over somebody else's network.
            // The "exactly once" guarantee above is worth nothing without an "at
            // least once" to go with it.
            //
            // Armed *before* the provider is called rather than after, because a
            // provider that throws on the way in would never reach a line that came
            // afterwards — and a throw here is the one failure that leaves the game
            // frozen with nothing at all watching.
            if (showTimeoutSeconds > 0f)
            {
                watchdog = StartCoroutine(GiveUpOn(placement, Settle));
            }

            try
            {
                ads.Show(placement, Settle);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Ads] {ads.Name} threw showing {placement}: {exception}");
                Settle(AdResult.Failed);
            }
        }

        private Coroutine watchdog;

        /// <summary>Unfreezes the game if the advert never comes back.</summary>
        private System.Collections.IEnumerator GiveUpOn(
            AdPlacement placement, Action<AdResult> settle)
        {
            // Wall time, not unscaled time, and the distinction is the whole value of
            // this coroutine. `Time.unscaledTime` is still a clock the game owns: set
            // `Time.captureDeltaTime`, as every recording and capture session in this
            // project does, and it advances by a fixed step per frame rather than by
            // seconds. A watchdog that can be stopped by the same thing it is watching
            // is not a watchdog. `Time.realtimeSinceStartup` is the only clock in
            // Unity that nothing here can touch.
            float deadline = Time.realtimeSinceStartup + showTimeoutSeconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            watchdog = null;

            Debug.LogError(
                $"[Ads] {ads.Name} never finished {placement} after " +
                $"{showTimeoutSeconds:0}s. Resuming the game and treating it as a " +
                "failure. A frozen game is worse than a missed advert.");

            settle(AdResult.Failed);
        }

        // -- Bookkeeping -----------------------------------------------------

        private void OnLevelEnded(LevelResult result) => RecordLevelEnd(result);

        /// <summary>
        /// Books a finished level into the pacing state, once.
        ///
        /// Called from the event *and* from the interface's request for an advert,
        /// because both listen to the same event and C# runs a delegate's handlers in
        /// whatever order they happened to subscribe. If the interface asked first, it
        /// asked about the previous level — counter not yet moved, failure not yet
        /// recorded — and got the previous level's answer. The frame number makes the
        /// second call a no-op whichever comes first.
        /// </summary>
        private void RecordLevelEnd(LevelResult result)
        {
            if (levelRecordedFrame == Time.frameCount)
            {
                return;
            }

            levelRecordedFrame = Time.frameCount;
            levelsSinceLastAd++;
            lastLevelWasFailed = !result.Completed;

            // Cleared at the end of a level rather than when the ad finishes: the rule
            // is "no interstitial in the same breath as a rewarded ad", and the breath
            // ends when the next level does.
            justWatchedRewarded = false;
        }

        /// <summary>
        /// Resets the continue budget at the start of a run, not at the end of one.
        ///
        /// The continue is offered *because* the run ended, so clearing the counter
        /// on RunEnded would clear it moments before the player spends it and the cap
        /// would never apply to anybody.
        /// </summary>
        private void OnRoundStarted(int round)
        {
            if (round > 1)
            {
                return;
            }

            continuesUsedThisRun = 0;
            runStarted = Time.realtimeSinceStartup;
            runsThisSession++;

            // Cleared when the next run *starts*, not when the last one ended. The
            // rule is "no interstitial in the same breath as a rewarded ad", and the
            // end of the run is still that breath — clearing it there would let the
            // run-end advert fire immediately after a rewarded continue, which is the
            // exact pair the rule exists to prevent.
            justWatchedRewarded = false;
        }

        /// <summary>
        /// Times the run that just ended.
        ///
        /// Real time rather than game time, deliberately. What is being paced is the
        /// player's patience, and that is spent in seconds off a wall clock however
        /// much of it the game spent frozen for a hit stop or an intro.
        /// </summary>
        private void OnRunEnded(EndlessResult result) => RecordRunEnd();

        /// <summary>
        /// Times the run that just ended, once, for the same reason as RecordLevelEnd.
        ///
        /// This one was not hypothetical. The interface asks for the run-end advert
        /// from its own RunEnded handler, and when that ran first the run was judged on
        /// the *previous* run's length — which on the first run of a session is zero,
        /// and "shorter than the minimum" is a veto.
        /// </summary>
        private void RecordRunEnd()
        {
            if (runRecordedFrame == Time.frameCount)
            {
                return;
            }

            runRecordedFrame = Time.frameCount;
            lastRunSeconds = Time.realtimeSinceStartup - runStarted;
        }
    }
}
