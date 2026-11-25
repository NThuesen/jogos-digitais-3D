using System;
using System.Collections;
using UnityEngine;

#if SLIPPERYZLOPE_LEVELPLAY
using com.unity3d.mediation;
#elif SLIPPERYZLOPE_UNITY_ADS
using UnityEngine.Advertisements;
#endif

namespace SlipperyZlope.RingToss.Monetisation
{
    /// <summary>
    /// The ad SDK. There is one of these and it is Unity's.
    ///
    /// This file replaces four: an interface, a null implementation, a simulator and a
    /// LevelPlay implementation. That arrangement was defensible — swap the mediator,
    /// keep the game — and it was four places to look to answer "what happens when I
    /// finish a level", which is one question. A game with one mediator does not need
    /// a mediator abstraction; it needs the mediator.
    ///
    /// There are three ways it can be compiled, and only one of them is ever active:
    ///
    /// 1. <b>Unity Ads</b> — the <c>com.unity.ads</c> package, which is in this
    ///    project's manifest, so this is what a normal build gets. It needs a game ID
    ///    from the Unity dashboard and nothing else, and it has a <b>test mode</b>
    ///    that serves Unity's own demo adverts. That combination is why it is the
    ///    default: the whole loop can be exercised on a real handset, on real network
    ///    conditions, before there is an account to monetise or a listing to be live
    ///    on. <c>SLIPPERYZLOPE_UNITY_ADS</c> is set automatically by a version define on
    ///    the runtime assembly, so installing the package is the whole of the setup.
    /// 2. <b>LevelPlay</b> — the mediation layer Unity Ads and ironSource became.
    ///    Worth having when there are several networks to fill from, and it takes an
    ///    account and an app key. Add the package, define
    ///    <c>SLIPPERYZLOPE_LEVELPLAY</c>, and it wins over Unity Ads.
    /// 3. <b>Neither</b> — a placeholder that takes real time and covers the screen.
    ///    A checkout with no SDK still has to build, still has to be playable, and —
    ///    more usefully — still has to let somebody feel how long the game disappears
    ///    for.
    ///
    /// The mediator abstraction this replaced was four files for one question. Three
    /// branches of one file is not that: they are three spellings of the same four
    /// calls, and they sit next to each other where they can be compared.
    ///
    /// What is deliberately *not* in here, and cannot be:
    ///
    /// - <b>Consent.</b> LevelPlay ships no dialog. The GDPR obligation is the
    ///   publisher's, and the age question governs the personalisation question, so
    ///   <see cref="ConsentService"/> owns both and this is told the answer.
    /// - <b>Pacing.</b> When an ad may interrupt is a decision about how the game
    ///   feels, not a setting on an SDK. <see cref="AdPacing"/> owns it.
    ///
    /// Two guarantees this makes to its callers, because mediation SDKs break both
    /// often enough to be worth defending against rather than trusting:
    ///
    /// - the completion callback fires <b>exactly once</b> per <see cref="Show"/>,
    ///   including on a failure to fill, so a caller waiting on a reward cannot hang;
    /// - it runs on the main thread, because callers touch the scene.
    ///
    /// Written against Unity Ads 4.x and LevelPlay Unity SDK 8.x. The type names are
    /// the part that changes between major versions — check them against the package
    /// actually installed.
    /// </summary>
    [DefaultExecutionOrder(-390)]
    public sealed class UnityAds : MonoBehaviour
#if SLIPPERYZLOPE_UNITY_ADS && !SLIPPERYZLOPE_LEVELPLAY
        , IUnityAdsInitializationListener, IUnityAdsLoadListener, IUnityAdsShowListener
#endif
    {
        [Header("Unity Ads")]
        [Tooltip("Game ID for Android, from the Unity Dashboard: Monetization > " +
                 "Get Started. Android and iOS are separate projects with separate IDs. " +
                 "Not a secret — it ships in the binary either way.")]
        [SerializeField] private string androidGameId;

        [SerializeField] private string iosGameId;

        [Tooltip("Test mode serves Unity's own demo adverts instead of live ones and " +
                 "does not pay. Leave it ON until the store listing is live: showing " +
                 "live adverts to yourself is what gets an account banned for invalid " +
                 "traffic, and the placeholder cannot tell you whether the SDK is " +
                 "actually wired up on the device.")]
        [SerializeField] private bool testMode = true;

        [Tooltip("Placement IDs from the dashboard. A project created through the " +
                 "current Unity flow gets Automatic_Interstitial and Automatic_Rewarded; " +
                 "older ones got Interstitial_Android and Rewarded_Android. They have " +
                 "to match exactly, and the SDK will initialise happily with the wrong " +
                 "ones and then never fill.")]
        [SerializeField] private string androidInterstitialPlacement = "Automatic_Interstitial";

        [SerializeField] private string androidRewardedPlacement = "Automatic_Rewarded";
        [SerializeField] private string iosInterstitialPlacement = "Automatic_Interstitial";
        [SerializeField] private string iosRewardedPlacement = "Automatic_Rewarded";

        [Header("LevelPlay")]
        [Tooltip("From the LevelPlay dashboard. Android and iOS are separate apps and " +
                 "have separate keys. Not a secret — it ships in the binary either way.")]
        [SerializeField] private string appKey;

        [Tooltip("The interstitial ad unit's ID. Leave empty to run rewarded-only.")]
        [SerializeField] private string interstitialUnit;

        [Tooltip("The rewarded ad unit's ID. Both rewarded placements share it: they " +
                 "differ in what the game pays out, which is not the mediator's business.")]
        [SerializeField] private string rewardedUnit;

        [Header("Placeholder")]
        [Tooltip("Show the placeholder whenever a real advert is not there to show: " +
                 "no SDK compiled in, the SDK never initialised, or — the ordinary " +
                 "case before launch — the network had nothing to fill with. Keeps " +
                 "the interruption visible and playable on a handset while the real " +
                 "path is wired but not yet earning. TURN THIS OFF BEFORE RELEASE: " +
                 "with it on, a player who gets no fill is shown a card that says " +
                 "'Your advert here'.")]
        [SerializeField] private bool placeholderWhenUnfilled = true;

        [Tooltip("Show the placeholder even when a real advert IS available. For a " +
                 "pre-launch build you want to demonstrate: everything up to the last " +
                 "step still runs — the SDK initialises with the real game ID, " +
                 "requests the real placements, and the log says when one filled — " +
                 "but what appears on screen is the stand-in. Also the only way to " +
                 "reliably look at the interruption in the editor, where the SDK " +
                 "reports a placement loaded and then never shows it. OFF before " +
                 "release, or the game earns nothing.")]
        [SerializeField] private bool alwaysPlaceholder;

        [Tooltip("How long a placeholder ad occupies the screen.")]
        [SerializeField] private float durationSeconds = 2.5f;

        [Tooltip("Seconds before an interstitial can be closed. Real mediators land " +
                 "between three and five, and the game has to be designed for the " +
                 "longer one because it is not the game's decision.")]
        [SerializeField] private float skipAfterSeconds = 3f;

        [Tooltip("Fraction of placeholder requests that fail to fill, so the failure " +
                 "path is exercised during ordinary play rather than only when it breaks.")]
        [Range(0f, 1f)] [SerializeField] private float failureRate = 0.15f;

        [Tooltip("Draw the placeholder. Off leaves the timing intact and the screen " +
                 "untouched, which is what an automated test wants.")]
        [SerializeField] private bool showPlaceholder = true;

        private Action<AdResult> pending;
        private AdPlacement showing;
        private MockAdView view;
        private bool busy;

        public bool IsInitialised { get; private set; }

        /// <summary>
        /// Whether an unfilled request is shown as a placeholder instead of refused.
        ///
        /// Settable so a test can turn it off and see the real fill, which is the
        /// whole point of the flag existing: with it on, <see cref="IsReady"/> is
        /// always true and cannot be used as evidence that an advert arrived.
        /// </summary>
        public bool PlaceholderWhenUnfilled
        {
            get => placeholderWhenUnfilled;
            set => placeholderWhenUnfilled = value;
        }

        /// <summary>Whether the stand-in is shown even when a real advert is there.</summary>
        public bool AlwaysPlaceholder
        {
            get => alwaysPlaceholder;
            set => alwaysPlaceholder = value;
        }

        /// <summary>Which implementation is compiled in. Shown in logs and in settings.</summary>
        public string Name =>
#if SLIPPERYZLOPE_LEVELPLAY
            "levelplay";
#elif SLIPPERYZLOPE_UNITY_ADS
            "unity-ads";
#else
            "placeholder";
#endif

        /// <summary>
        /// The share of placeholder requests that fail to fill.
        ///
        /// Settable so a capture can film the no-fill path on purpose rather than
        /// waiting for it to come up, which is the only way that screen ever gets
        /// looked at — and the first time it was, it turned out to be showing a
        /// complete advert with "No advert available" written over it.
        /// </summary>
        public float FailureRate
        {
            get => failureRate;
            set => failureRate = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Reports one result and makes sure it is the only one.
        ///
        /// The callback is taken out of the field before it is invoked, so a second
        /// event for the same show — and with a rewarded ad there is always a second
        /// event, because paying out and closing are both events — finds nothing left
        /// to call.
        /// </summary>
        private void Finish(AdResult result)
        {
            Action<AdResult> callback = pending;

            if (callback == null)
            {
                return;
            }

            pending = null;
            busy = false;

            Debug.Log($"[Ads] {Name} {showing} -> {result}.");
            callback(result);
        }

        /// <summary>
        /// Stands in for an advert that is not there.
        ///
        /// Three different situations end up here and they are worth naming, because
        /// they look identical from the game's side and are not the same problem: no
        /// SDK compiled into this build, an SDK that never initialised, and — the one
        /// that will happen every time before launch — an SDK that initialised fine
        /// and had nothing to fill with. A game that is not live on a store gets no
        /// fill, and that is the network working correctly.
        ///
        /// It takes real time and covers the screen, because the thing worth judging
        /// before shipping is not the advert — the mediator picks that — it is the
        /// shape of the interruption: how long the game disappears for, where the
        /// close button is and when it appears, and what the player comes back to.
        /// None of that can be judged against a stub that returns instantly.
        ///
        /// It says so in the log every single time. A placeholder that is quiet about
        /// being a placeholder is how a build ships showing players a blue card that
        /// reads "Your advert here".
        /// </summary>
        private void ShowPlaceholder(AdPlacement placement, string why)
        {
            Debug.LogWarning(
                $"[Ads] {why}: showing the PLACEHOLDER for {placement}, not a real " +
                "advert. Turn off " +
                (alwaysPlaceholder ? "Always Placeholder" : "Placeholder When Unfilled") +
                " on the Unity Ads component before release.");

            StartCoroutine(RunPlaceholder(placement));
        }

        private IEnumerator RunPlaceholder(AdPlacement placement)
        {
            // Decided before anything is drawn, so a failure looks like a failure from
            // the first frame rather than like an advert that gave up halfway.
            bool fails = UnityEngine.Random.value < failureRate;
            AdResult result = AdResult.Watched;

            if (showPlaceholder)
            {
                view ??= gameObject.AddComponent<MockAdView>();

                yield return fails
                    ? view.RunFailure(placement, outcome => result = outcome)
                    : view.Run(placement, durationSeconds, skipAfterSeconds,
                               outcome => result = outcome);
            }
            else
            {
                // A real ad runs while the game is paused, so this has to as well:
                // unscaled time, or the wait never ends.
                float deadline = Time.realtimeSinceStartup + durationSeconds;

                while (Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                result = fails ? AdResult.Failed : AdResult.Watched;
            }

            Finish(result);
        }

#if SLIPPERYZLOPE_LEVELPLAY

        private LevelPlayInterstitialAd interstitial;
        private LevelPlayRewardedAd rewarded;
        private Action<bool> readyCallback;
        private bool earned;

        /// <summary>
        /// Starts the SDK.
        ///
        /// <see cref="AdService"/> guarantees this is not reached until the player has
        /// answered, and that matters more here than anywhere else in the file:
        /// LevelPlay begins collecting an advertising identifier at init, so
        /// initialising early leaks the exact thing the dialog governs. Consent is
        /// applied before Init for the same reason.
        /// </summary>
        public void Initialise(AdPersonalisation personalisation, Action<bool> onReady)
        {
            if (personalisation == AdPersonalisation.None)
            {
                Debug.LogError("[Ads] Refusing to initialise without an answer on consent.");
                onReady?.Invoke(false);
                return;
            }

            if (IsInitialised)
            {
                onReady?.Invoke(true);
                return;
            }

            if (string.IsNullOrEmpty(appKey))
            {
                // A warning for the same reason the missing Unity Ads game ID is one:
                // it is a build that has not been configured, not a build that is broken.
                Debug.LogWarning(
                    "[Ads] No LevelPlay app key, so no adverts will be requested. " +
                    "Set it on the Unity Ads component.");
                onReady?.Invoke(false);
                return;
            }

            readyCallback = onReady;
            SetPersonalisation(personalisation);

            LevelPlay.OnInitSuccess += OnInitSuccess;
            LevelPlay.OnInitFailed += OnInitFailed;
            LevelPlay.Init(appKey);
        }

        private void OnInitSuccess(LevelPlayConfiguration configuration)
        {
            IsInitialised = true;

            if (!string.IsNullOrEmpty(interstitialUnit))
            {
                interstitial = new LevelPlayInterstitialAd(interstitialUnit);

                // Displaying an interstitial is the whole interaction: there is nothing
                // to earn, so being shown one counts as having watched it.
                interstitial.OnAdDisplayed += _ => earned = true;
                interstitial.OnAdDisplayFailed += (_, __) => Finish(AdResult.Failed);
                interstitial.OnAdClosed += _ =>
                {
                    Finish(earned ? AdResult.Watched : AdResult.Skipped);
                    interstitial.LoadAd();
                };
                interstitial.OnAdLoadFailed += error =>
                    Debug.LogWarning($"[Ads] Interstitial did not load: {error}");

                interstitial.LoadAd();
            }

            if (!string.IsNullOrEmpty(rewardedUnit))
            {
                rewarded = new LevelPlayRewardedAd(rewardedUnit);

                rewarded.OnAdRewarded += (_, __) => earned = true;
                rewarded.OnAdDisplayFailed += (_, __) => Finish(AdResult.Failed);
                rewarded.OnAdClosed += _ =>
                {
                    Finish(earned ? AdResult.Watched : AdResult.Skipped);

                    // Reloaded the moment it closes. A rewarded placement that is
                    // offered and then not ready is worse than one never offered.
                    rewarded.LoadAd();
                };
                rewarded.OnAdLoadFailed += error =>
                    Debug.LogWarning($"[Ads] Rewarded did not load: {error}");

                rewarded.LoadAd();
            }

            Hand(true);
        }

        private void OnInitFailed(LevelPlayInitError error)
        {
            Debug.LogWarning($"[Ads] LevelPlay init failed: {error}");
            IsInitialised = false;
            Hand(false);
        }

        private void Hand(bool ready)
        {
            Action<bool> callback = readyCallback;
            readyCallback = null;
            callback?.Invoke(ready);
        }

        /// <summary>Whether a real advert is loaded and waiting. See the Unity Ads note.</summary>
        public bool IsFilled(AdPlacement placement) =>
            IsInitialised && (placement == AdPlacement.Interstitial
                ? interstitial != null && interstitial.IsAdReady()
                : rewarded != null && rewarded.IsAdReady());

        public bool IsReady(AdPlacement placement)
        {
            if (busy)
            {
                return false;
            }

            return alwaysPlaceholder || IsFilled(placement) || placeholderWhenUnfilled;
        }

        public void Show(AdPlacement placement, Action<AdResult> onComplete)
        {
            if (!IsReady(placement))
            {
                onComplete?.Invoke(AdResult.NotReady);
                return;
            }

            pending = onComplete;
            showing = placement;
            busy = true;
            earned = false;

            if (alwaysPlaceholder || !IsFilled(placement))
            {
                ShowPlaceholder(
                    placement,
                    alwaysPlaceholder
                        ? $"Always Placeholder is on, so this {placement} is a stand-in" +
                          (IsFilled(placement) ? " and a real advert was available" : string.Empty)
                        : IsInitialised
                            ? $"LevelPlay had no fill for {placement}"
                            : "LevelPlay is not initialised");

                return;
            }

            if (placement == AdPlacement.Interstitial)
            {
                interstitial.ShowAd();
            }
            else
            {
                rewarded.ShowAd();
            }
        }

        /// <summary>
        /// Hands the player's answer to the SDK.
        ///
        /// <see cref="AdPersonalisation.None"/> has no representation here because it
        /// is not a mode the SDK can be in — it means "do not ask for an ad at all",
        /// and that is enforced above by never calling <see cref="Show"/>.
        /// </summary>
        public void SetPersonalisation(AdPersonalisation personalisation)
        {
            bool personalised = personalisation == AdPersonalisation.Personalised;

            IronSource.Agent.setConsent(personalised);

            // CCPA. "do_not_sell = true" is the restrictive setting, so it is the one
            // a player who declined gets.
            IronSource.Agent.setMetaData("do_not_sell", personalised ? "false" : "true");
        }

#elif SLIPPERYZLOPE_UNITY_ADS

        // Which placements have a filled advert waiting. Unity Ads 4 has no "is this
        // ready" call: a load either calls back or it does not, and the only honest
        // answer to IsReady is the one this remembers.
        private bool interstitialLoaded;
        private bool rewardedLoaded;
        private Action<bool> readyCallback;

        private bool OnIos => Application.platform == RuntimePlatform.IPhonePlayer;

        private string GameId => OnIos ? iosGameId : androidGameId;

        private string InterstitialPlacement =>
            OnIos ? iosInterstitialPlacement : androidInterstitialPlacement;

        private string RewardedPlacement =>
            OnIos ? iosRewardedPlacement : androidRewardedPlacement;

        private string PlacementFor(AdPlacement placement) =>
            placement == AdPlacement.Interstitial ? InterstitialPlacement : RewardedPlacement;

        /// <summary>
        /// Starts the SDK.
        ///
        /// <see cref="AdService"/> guarantees this is not reached until the player has
        /// answered, and consent is pushed down *before* Initialize rather than after.
        /// Unity Ads begins building its request the moment it initialises, so an
        /// answer applied afterwards arrives too late for the first advert of the
        /// session — which is the one most likely to be seen.
        /// </summary>
        public void Initialise(AdPersonalisation personalisation, Action<bool> onReady)
        {
            if (personalisation == AdPersonalisation.None)
            {
                Debug.LogError("[Ads] Refusing to initialise without an answer on consent.");
                onReady?.Invoke(false);
                return;
            }

            if (IsInitialised)
            {
                onReady?.Invoke(true);
                return;
            }

            if (string.IsNullOrEmpty(GameId))
            {
                // A warning, not an error. An unset game ID is the ordinary state of a
                // checkout that has not been pointed at a dashboard project yet, and
                // the game is required to be completely playable in it — the same
                // contract a build with no SDK at all has. Raising it to an error
                // would also fail every play mode test in the project, which is how
                // this severity got noticed.
                Debug.LogWarning(
                    "[Ads] No Unity Ads game ID for this platform, so no adverts will be " +
                    "requested. Set it on the Unity Ads component in the scene, from " +
                    "Dashboard > Monetization > Get Started.");
                onReady?.Invoke(false);
                return;
            }

            if (!Advertisement.isSupported)
            {
                Debug.LogWarning("[Ads] Unity Ads is not supported on this platform.");
                onReady?.Invoke(false);
                return;
            }

            readyCallback = onReady;
            SetPersonalisation(personalisation);

            // Game Hub: this scene can be re-entered from the menu while the SDK is
            // still initialised from the previous visit in the same app session.
            if (Advertisement.isInitialized)
            {
                OnInitializationComplete();
                return;
            }

            Advertisement.Initialize(GameId, testMode, this);
        }

        public void OnInitializationComplete()
        {
            IsInitialised = true;

            Debug.Log($"[Ads] Unity Ads ready, testMode={testMode}.");

            LoadPlacement(InterstitialPlacement);
            LoadPlacement(RewardedPlacement);

            Hand(true);
        }

        public void OnInitializationFailed(UnityAdsInitializationError error, string message)
        {
            Debug.LogWarning($"[Ads] Unity Ads init failed: {error} — {message}");
            IsInitialised = false;
            Hand(false);
        }

        private void Hand(bool ready)
        {
            Action<bool> callback = readyCallback;
            readyCallback = null;
            callback?.Invoke(ready);
        }

        private void LoadPlacement(string placement)
        {
            if (string.IsNullOrEmpty(placement))
            {
                return;
            }

            Advertisement.Load(placement, this);
        }

        public void OnUnityAdsAdLoaded(string placementId)
        {
            if (placementId == InterstitialPlacement)
            {
                interstitialLoaded = true;
            }

            if (placementId == RewardedPlacement)
            {
                rewardedLoaded = true;
            }
        }

        public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
        {
            // Not an error the player should ever hear about: no fill is ordinary, and
            // every caller treats "not ready" as "carry on".
            Debug.LogWarning($"[Ads] {placementId} did not load: {error} — {message}");

            if (placementId == InterstitialPlacement)
            {
                interstitialLoaded = false;
            }

            if (placementId == RewardedPlacement)
            {
                rewardedLoaded = false;
            }
        }

        /// <summary>
        /// Whether a real advert is loaded and waiting.
        ///
        /// Separate from <see cref="IsReady"/> on purpose. With the placeholder
        /// fallback on, IsReady is always true and therefore proves nothing — so the
        /// fill test asks this instead. A diagnostic that cannot fail is not a
        /// diagnostic.
        /// </summary>
        public bool IsFilled(AdPlacement placement) =>
            IsInitialised
            && (placement == AdPlacement.Interstitial ? interstitialLoaded : rewardedLoaded);

        public bool IsReady(AdPlacement placement)
        {
            if (busy)
            {
                return false;
            }

            return alwaysPlaceholder || IsFilled(placement) || placeholderWhenUnfilled;
        }

        public void Show(AdPlacement placement, Action<AdResult> onComplete)
        {
            if (!IsReady(placement))
            {
                onComplete?.Invoke(AdResult.NotReady);
                return;
            }

            pending = onComplete;
            showing = placement;
            busy = true;

            if (alwaysPlaceholder || !IsFilled(placement))
            {
                ShowPlaceholder(
                    placement,
                    alwaysPlaceholder
                        ? $"Always Placeholder is on, so this {placement} is a stand-in" +
                          (IsFilled(placement)
                              ? $" and {PlacementFor(placement)} had a real advert ready"
                              : string.Empty)
                        : IsInitialised
                            ? $"Unity Ads had no fill for {PlacementFor(placement)}"
                            : "Unity Ads is not initialised");

                return;
            }

            // Marked spent before it is shown. The advert is consumed either way, and
            // a stale "loaded" left behind by a failed show is what makes a placement
            // offer itself and then do nothing.
            if (placement == AdPlacement.Interstitial)
            {
                interstitialLoaded = false;
            }
            else
            {
                rewardedLoaded = false;
            }

            Advertisement.Show(PlacementFor(placement), this);
        }

        public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
        {
            Debug.LogWarning($"[Ads] {placementId} failed to show: {error} — {message}");

            Finish(AdResult.Failed);
            LoadPlacement(placementId);
        }

        public void OnUnityAdsShowStart(string placementId)
        {
        }

        public void OnUnityAdsShowClick(string placementId)
        {
        }

        public void OnUnityAdsShowComplete(
            string placementId, UnityAdsShowCompletionState state)
        {
            // COMPLETED is the only state that earns a reward. UNKNOWN is reported when
            // the SDK cannot tell, and treating "cannot tell" as "watched" is how a
            // rewarded placement turns into a free tap.
            Finish(state == UnityAdsShowCompletionState.COMPLETED
                ? AdResult.Watched
                : AdResult.Skipped);

            // Reloaded the moment it closes. A rewarded placement that is offered and
            // then not ready is worse than one never offered.
            LoadPlacement(placementId);
        }

        /// <summary>
        /// Hands the player's answer to the SDK, as the metadata Unity Ads reads.
        ///
        /// Three separate keys, because they answer to three separate regimes and a
        /// player can be covered by more than one at a time. The restrictive value is
        /// sent for anything short of full consent — including for an unconfirmed age,
        /// which <see cref="ConsentPolicy"/> has already collapsed into
        /// non-personalised by this point.
        /// </summary>
        public void SetPersonalisation(AdPersonalisation personalisation)
        {
            bool personalised = personalisation == AdPersonalisation.Personalised;

            MetaData gdpr = new MetaData("gdpr");
            gdpr.Set("consent", personalised ? "true" : "false");
            Advertisement.SetMetaData(gdpr);

            // CCPA. Unity reads this one the other way round from GDPR: "consent"
            // true means the sale of data is permitted.
            MetaData privacy = new MetaData("privacy");
            privacy.Set("consent", personalised ? "true" : "false");
            Advertisement.SetMetaData(privacy);

            // The switch that makes the advert itself contextual rather than merely
            // stopping the identifier being used to pick it.
            MetaData user = new MetaData("user");
            user.Set("nonbehavioral", personalised ? "false" : "true");
            Advertisement.SetMetaData(user);

            Debug.Log($"[Ads] Unity Ads personalisation set to {personalisation}.");
        }

#else

        /// <summary>No SDK, so nothing is requested and no network is touched.</summary>
        public void Initialise(AdPersonalisation personalisation, Action<bool> onReady)
        {
            IsInitialised = true;
            Debug.Log($"[Ads] Placeholder ready, personalisation={personalisation}. " +
                      "Install com.unity.ads, or define SLIPPERYZLOPE_LEVELPLAY, for the " +
                      "real thing.");
            onReady?.Invoke(true);
        }

        /// <summary>Never. There is no SDK in this build to have filled anything.</summary>
        public bool IsFilled(AdPlacement placement) => false;

        public bool IsReady(AdPlacement placement) =>
            !busy && (placeholderWhenUnfilled || alwaysPlaceholder);

        public void Show(AdPlacement placement, Action<AdResult> onComplete)
        {
            if (!IsReady(placement))
            {
                onComplete?.Invoke(AdResult.NotReady);
                return;
            }

            pending = onComplete;
            showing = placement;
            busy = true;

            ShowPlaceholder(placement, "No ad SDK is compiled into this build");
        }

        public void SetPersonalisation(AdPersonalisation personalisation)
        {
            Debug.Log($"[Ads] Placeholder personalisation set to {personalisation}.");
        }

#endif
    }
}
