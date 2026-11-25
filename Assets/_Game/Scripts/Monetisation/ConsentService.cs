using UnityEngine;

namespace SlipperyZlope.RingToss.Monetisation
{
    /// <summary>
    /// Remembers what the player was asked and what they answered, and hands the
    /// answer to the rest of the game as an <see cref="AdPersonalisation"/>.
    ///
    /// The choice is stored locally and nowhere else. It is not an account, it is not
    /// synced, and it does not leave the device — which is also the reason clearing
    /// the game's data legitimately resets it back to unasked.
    ///
    /// This deliberately does no region detection. Detecting the EEA reliably means
    /// an IP lookup, which is a network request carrying an IP address made *before*
    /// the player has agreed to anything, to decide whether to ask them — the wrong
    /// way round, and a processing act in its own right. Asking everyone costs one
    /// dialog and needs no justification. When a certified CMP is wired in, it can
    /// narrow this; until then the strict path is the correct default rather than a
    /// limitation.
    /// </summary>
    public sealed class ConsentService : MonoBehaviour
    {
        private const string StatusKey = "consent.status";
        private const string AgeBracketKey = "consent.ageBracket";
        private const string TrackingKey = "consent.tracking";

        [SerializeField] private AdConfig config;

        /// <summary>What the player last answered.</summary>
        public ConsentStatus Status
        {
            get => (ConsentStatus)PlayerPrefs.GetInt(StatusKey, (int)ConsentStatus.Unknown);
            private set
            {
                PlayerPrefs.SetInt(StatusKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Which side of the age of digital consent the player says they are on.
        /// Zero means they have not said.
        ///
        /// A bracket, not a birth date, and not even a birth year. The only thing the
        /// decision needs is which side of a threshold somebody is on, and a year of
        /// birth is a stable quasi-identifier that answers far more than that. Storing
        /// the answer to the question actually asked is the whole of data
        /// minimisation, and it costs nothing here.
        /// </summary>
        public int AgeBracket => PlayerPrefs.GetInt(AgeBracketKey, 0);

        private const int Underage = 1;
        private const int OfAge = 2;

        /// <summary>
        /// True when the player has been through Apple's tracking prompt and allowed
        /// it. Meaningless off iOS, where it stays true and the personalisation
        /// decision rests on consent alone.
        /// </summary>
        public bool TrackingAuthorised
        {
            get => PlayerPrefs.GetInt(TrackingKey, 1) != 0;
            private set
            {
                PlayerPrefs.SetInt(TrackingKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Unknown age counts as underage. It is the only assumption that cannot go
        /// wrong in the direction that matters.
        /// </summary>
        public bool PlayerIsUnderage => AgeBracket != OfAge;

        /// <summary>What may be requested right now.</summary>
        public AdPersonalisation Personalisation =>
            ConsentPolicy.Resolve(Status, PlayerIsUnderage, TrackingAuthorised);

        public bool MustAsk => ConsentPolicy.MustAsk(Status);

        public bool NeedsSettingsEntryPoint => ConsentPolicy.NeedsWithdrawalEntryPoint(Status);

        /// <summary>
        /// The threshold the age question is phrased against, so the dialog and the
        /// policy cannot drift apart.
        /// </summary>
        public int AgeOfDigitalConsent => config == null ? 16 : config.AgeOfDigitalConsent;

        public string PrivacyPolicyUrl => config == null ? string.Empty : config.PrivacyPolicyUrl;

        public string TermsUrl => config == null ? string.Empty : config.TermsUrl;

        public void Configure(AdConfig adConfig)
        {
            config = adConfig;
        }

        /// <summary>
        /// Records the player's answers.
        ///
        /// Both are recorded together because they are one decision: agreeing to
        /// personalised advertising while declining to confirm an age still produces
        /// non-personalised advertising, and that has to be a single consistent state
        /// rather than two settings that can disagree.
        /// </summary>
        public void Record(bool personalisedAllowed, bool isOfAge)
        {
            PlayerPrefs.SetInt(AgeBracketKey, isOfAge ? OfAge : Underage);
            Status = personalisedAllowed ? ConsentStatus.Granted : ConsentStatus.Denied;
        }

        public void RecordTrackingAuthorisation(bool authorised)
        {
            TrackingAuthorised = authorised;
        }

        /// <summary>
        /// Puts the player back to unasked, so the dialog appears again.
        ///
        /// Withdrawing consent has to be as easy as giving it, and the honest way to
        /// implement that is to forget the answer rather than to flip it to "denied"
        /// and never ask again.
        /// </summary>
        public void Reset()
        {
            PlayerPrefs.DeleteKey(StatusKey);
            PlayerPrefs.DeleteKey(AgeBracketKey);
            PlayerPrefs.Save();
        }
    }
}
