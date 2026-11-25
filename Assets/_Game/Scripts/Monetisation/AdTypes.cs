namespace SlipperyZlope.RingToss.Monetisation
{
    /// <summary>Where an ad is shown, which is also what the player is trading for it.</summary>
    public enum AdPlacement
    {
        /// <summary>Unasked-for, between levels. The only one that can annoy.</summary>
        Interstitial,

        /// <summary>Offered when an endless run ends, for one more life.</summary>
        RewardedContinue,

        /// <summary>Offered on the results screen, to double the tickets earned.</summary>
        RewardedDoubleTickets,

        /// <summary>
        /// Offered from the menu, for a handful of tickets.
        ///
        /// The only advert in the game that is not attached to a moment of play. It
        /// pays deliberately little: it is there so a player who wants a particular
        /// ring can choose to work towards it, not so that watching adverts becomes
        /// the efficient way to own the shop.
        /// </summary>
        RewardedFreeTickets
    }

    public enum AdResult
    {
        /// <summary>Watched far enough to earn the reward.</summary>
        Watched,

        /// <summary>Closed early. No reward, and not an error.</summary>
        Skipped,

        /// <summary>Nothing was filled, or the network failed. Never charge the player for this.</summary>
        Failed,

        /// <summary>Nothing to show yet.</summary>
        NotReady,

        /// <summary>Deliberately withheld: pacing, consent, or ads disabled.</summary>
        Suppressed
    }

    /// <summary>
    /// What the player has been asked, and what they said.
    ///
    /// Kept separate from any SDK's own consent object. The legal obligation is
    /// ours; the mediator is one way of meeting part of it, and mediators get
    /// swapped.
    /// </summary>
    public enum ConsentStatus
    {
        /// <summary>Not asked yet. No ads of any kind may be requested.</summary>
        Unknown,

        /// <summary>Not required here — outside the EEA, the UK and Switzerland.</summary>
        NotRequired,

        /// <summary>Asked and refused. Contextual, non-personalised ads only.</summary>
        Denied,

        /// <summary>Asked and accepted.</summary>
        Granted
    }

    /// <summary>What may be requested right now, given consent and age.</summary>
    public enum AdPersonalisation
    {
        /// <summary>No ad may be requested at all.</summary>
        None,

        /// <summary>Contextual only: no advertising identifier, no profile.</summary>
        NonPersonalised,

        Personalised
    }
}
