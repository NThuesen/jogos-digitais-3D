namespace SlipperyZlope.RingToss.Monetisation
{
    /// <summary>
    /// Turns what is known about the player into what may legally be requested.
    ///
    /// Pure, and separate from any SDK, because the obligation belongs to the game
    /// rather than to whichever mediator is wired in this month. A mediator's own
    /// consent object answers "did the CMP record a yes"; this answers "may this
    /// build ask for an ad at all", which is a different and stricter question.
    ///
    /// The rules encoded here:
    ///
    /// - Nothing may be requested before the player has been asked. Requesting an ad
    ///   in order to decide whether to ask for consent is the wrong way round, and it
    ///   is the most common way a game ends up transmitting an advertising identifier
    ///   it had no basis to transmit.
    /// - A player under the age of digital consent never gets personalised ads,
    ///   whatever any dialog said. Consent given by a child is not consent, and in a
    ///   game a child might plausibly play this is the rule that matters most.
    /// - Refusal is not a dead end. Contextual ads need no consent and no identifier,
    ///   so the game keeps working and the player is not punished for saying no.
    /// </summary>
    public static class ConsentPolicy
    {
        public static AdPersonalisation Resolve(
            ConsentStatus status,
            bool playerIsUnderage,
            bool trackingAuthorised)
        {
            if (status == ConsentStatus.Unknown)
            {
                return AdPersonalisation.None;
            }

            if (playerIsUnderage)
            {
                // Under the age of digital consent, and under Play's Families policy
                // and the App Store's Kids rules, the only lawful ad is a contextual
                // one. There is no dialog that changes this.
                return AdPersonalisation.NonPersonalised;
            }

            if (status == ConsentStatus.Denied)
            {
                return AdPersonalisation.NonPersonalised;
            }

            // On iOS, App Tracking Transparency is a second gate in front of the
            // identifier. Consent to personalised advertising without ATT permission
            // still means no IDFA, so the ad has to be contextual.
            if (!trackingAuthorised)
            {
                return AdPersonalisation.NonPersonalised;
            }

            return AdPersonalisation.Personalised;
        }

        /// <summary>
        /// Whether the consent dialog has to be shown before any ad is requested.
        ///
        /// True whenever consent is required and has not been recorded. Note that
        /// this is not the same as "the player has not consented": a recorded refusal
        /// is an answer, and re-asking a player who has already said no is the
        /// behaviour the rules exist to stop.
        /// </summary>
        public static bool MustAsk(ConsentStatus status) => status == ConsentStatus.Unknown;

        /// <summary>
        /// Whether the player must be able to change their mind from inside the game.
        ///
        /// Consent that cannot be withdrawn as easily as it was given is not consent,
        /// so any build that ever asked has to carry the settings entry point. That
        /// includes builds where the player refused.
        /// </summary>
        public static bool NeedsWithdrawalEntryPoint(ConsentStatus status) =>
            status == ConsentStatus.Granted || status == ConsentStatus.Denied;
    }
}
