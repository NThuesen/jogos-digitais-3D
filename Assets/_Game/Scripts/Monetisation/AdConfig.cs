using UnityEngine;

namespace SlipperyZlope.RingToss.Monetisation
{
    /// <summary>
    /// Everything about advertising that a person might reasonably want to change
    /// without touching code, including whether there is any at all.
    /// </summary>
    [CreateAssetMenu(menuName = "SlipperyZlope/Ad Config", fileName = "AdConfig")]
    public sealed class AdConfig : ScriptableObject
    {
        [Header("Master switches")]
        [Tooltip("Off by default. A build with no mediator wired in must not pretend " +
                 "to have one, and the game has to be fully playable either way.")]
        [SerializeField] private bool adsEnabled;

        [SerializeField] private bool interstitialsEnabled = true;
        [SerializeField] private bool rewardedEnabled = true;

        [Header("Interstitial pacing")]
        // Loosened at the owner's request after a recorded session in which the game
        // was lost repeatedly and no advert ever appeared. Every number here was a
        // veto, and stacked together they vetoed everything: the first run of a
        // session, any run under a minute, and every failed level without exception.
        // What is left still protects the player — a new install, the first seconds
        // of a session, two adverts in a row — without protecting them from ever
        // seeing one.

        [Tooltip("Levels the player completes before the first interstitial. A player " +
                 "who has not yet decided whether they like the game gets no interruptions.")]
        [Range(0, 30)] [SerializeField] private int graceLevels = 2;

        [Tooltip("Levels between two interstitials after a *win*. A loss only waits out " +
                 "the time gap below: it is the natural break in play.")]
        [Range(1, 20)] [SerializeField] private int minimumLevelsBetween = 2;
        [Range(0f, 600f)] [SerializeField] private float minimumSecondsBetween = 50f;

        [Tooltip("No interstitial in the opening seconds of a session, however many " +
                 "levels are behind the player.")]
        [Range(0f, 300f)] [SerializeField] private float minimumSessionSeconds = 30f;

        [Header("Run-end interstitial")]
        [Tooltip("Runs a session gets before losing one may carry an advert. One means " +
                 "the first run after opening the game is always uninterrupted.")]
        [Range(0, 10)] [SerializeField] private int graceRuns;

        [Tooltip("How long a run has to have lasted for its ending to carry an advert. " +
                 "A run the player survived for a while has earned a break; a run that " +
                 "ended in fifteen seconds has not, and interrupting that one is how a " +
                 "game starts to feel predatory.")]
        [Range(0f, 600f)] [SerializeField] private float minimumRunSeconds = 20f;

        [Tooltip("Never straight after a failed level. Off: a loss is where the owner " +
                 "wants the advert, and with this on a whole session of losing could " +
                 "pass without a single one.")]
        [SerializeField] private bool skipAfterFailure;

        [Header("Rewards")]
        [Tooltip("Multiplier applied to a level's tickets when the player watches for it.")]
        [Range(2, 5)] [SerializeField] private int doubleTicketsMultiplier = 2;

        [Tooltip("Extra lives a single endless run may be continued with. Beyond a " +
                 "point a run stops being a run.")]
        [Range(0, 5)] [SerializeField] private int maxContinuesPerRun = 1;

        [Tooltip("Seconds between two voluntary ticket adverts. Not a punishment: it " +
                 "is what keeps the offer a small bonus rather than a second game " +
                 "made of watching adverts.")]
        [Range(0f, 900f)] [SerializeField] private float rewardedTicketCooldown = 120f;

        [Header("Compliance")]
        [Tooltip("Where the privacy policy is published. Shown in the game and " +
                 "required by both stores.")]
        [SerializeField] private string privacyPolicyUrl = "https://slipperyzlope.games/ring-toss/privacy";

        [SerializeField] private string termsUrl = "https://slipperyzlope.games/ring-toss/terms";

        [Tooltip("Under this age, personalised advertising is never requested no " +
                 "matter what any dialog recorded. 16 is the strictest figure any " +
                 "EEA member state sets, so it is the safe one to apply everywhere.")]
        [Range(13, 18)] [SerializeField] private int ageOfDigitalConsent = 16;

        public bool AdsEnabled => adsEnabled;
        public bool RewardedEnabled => rewardedEnabled && adsEnabled;
        public int DoubleTicketsMultiplier => doubleTicketsMultiplier;
        public int MaxContinuesPerRun => maxContinuesPerRun;
        public float RewardedTicketCooldown => rewardedTicketCooldown;
        public string PrivacyPolicyUrl => privacyPolicyUrl;
        public string TermsUrl => termsUrl;
        public int AgeOfDigitalConsent => ageOfDigitalConsent;

        public AdPacingRules PacingRules => new AdPacingRules(
            interstitialsEnabled && adsEnabled,
            graceLevels,
            minimumLevelsBetween,
            minimumSecondsBetween,
            minimumSessionSeconds,
            skipAfterFailure,
            graceRuns,
            minimumRunSeconds);
    }
}
