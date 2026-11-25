namespace SlipperyZlope.RingToss.Config
{
    /// <summary>One readable document in the in-game legal screen.</summary>
    public readonly struct LegalSection
    {
        /// <summary>What the tab says. Short: it has to fit four across a phone.</summary>
        public readonly string Tab;

        public readonly string Heading;
        public readonly string Body;

        public LegalSection(string tab, string heading, string body)
        {
            Tab = tab;
            Heading = heading;
            Body = body;
        }
    }

    /// <summary>
    /// The legal documents, in the game, in full.
    ///
    /// Both stores require a privacy policy to be reachable, and the usual way to meet
    /// that is a link to a web page. A link is not enough on its own and never was:
    /// it needs a site that exists, that stays up, and that the player has signal to
    /// reach — and a player on a plane who wants to know what this game is doing with
    /// their data is entitled to an answer. The URLs are still there, in Settings, and
    /// they are still what goes in the store listing. This is the copy that cannot 404.
    ///
    /// Written in code rather than as a TextAsset for the same reason the levels and
    /// the palette are: it is reviewable in a diff, it cannot go missing from a build,
    /// and there is no import step between writing it and it being what ships.
    ///
    /// **This has to stay true.** It is not boilerplate — every claim below is a claim
    /// about what the code actually does, and three of them are checked by
    /// <c>LegalTextTests</c>. If the game gains an analytics SDK, a leaderboard, a
    /// crash reporter or a second ad network, this file is wrong until it is updated,
    /// and being wrong here is a different order of problem from being wrong anywhere
    /// else in the project.
    ///
    /// The long form, with the full regulatory detail, is in <c>docs/legal/</c>. That
    /// is the version to publish; this is the version to read on a phone.
    /// </summary>
    public static class LegalText
    {
        /// <summary>The publisher, as it must appear to a player asking who holds their data.</summary>
        public const string Publisher = "SlipperyZlope Games";

        /// <summary>Where a data request has to arrive. A statutory deadline runs from it.</summary>
        public const string ContactEmail = "privacy@slipperyzlope.games";

        public static readonly LegalSection[] Sections =
        {
            new LegalSection(
                "DATA",
                "WHAT WE COLLECT",
                "The short answer: we collect nothing.\n\n" +
                "Ring Toss has no account, no login, no profile, no leaderboard and no " +
                "social feature. There is no SlipperyZlope server. Your progress is saved " +
                "on this device and is never sent to us. We could not look at your " +
                "scores if we wanted to.\n\n" +
                "There is no analytics SDK and no crash reporter in this build.\n\n" +
                "<b>STORED ON THIS DEVICE ONLY</b>\n" +
                "•  Levels cleared, stars and best scores\n" +
                "•  Best endless and time attack runs\n" +
                "•  Tickets, and which rings and trails you own\n" +
                "•  Whether sound is muted\n" +
                "•  Your advertising answer, and whether you said you are over 16\n\n" +
                "Deleting the app deletes all of it. So does DELETE MY DATA in " +
                "Settings. We keep no copy, which also means we cannot restore it.\n\n" +
                "<b>THE ONE EXCEPTION: ADVERTS</b>\n" +
                "If you allow adverts, our advertising partner — Unity Ads, run by " +
                "Unity Technologies — collects data from this device in order to " +
                "choose and measure them. That is the only routine flow of data off " +
                "your phone. Nothing is requested before you have answered, and you " +
                "can change your answer at any time from Settings.\n\n" +
                "See the PRIVACY tab for exactly what Unity collects."),

            new LegalSection(
                "PRIVACY",
                "PRIVACY POLICY",
                "<b>WHO WE ARE</b>\n" +
                Publisher + " is the controller for the little data this game " +
                "involves. Contact: " + ContactEmail + "\n\n" +
                "<b>WHAT THE ADVERTS COLLECT</b>\n" +
                "When an advert is requested, Unity Ads and the networks it works " +
                "with may collect:\n" +
                "•  your device's advertising ID, where you have allowed it\n" +
                "•  device and technical information — model, OS version, " +
                "language, country, screen size, network type\n" +
                "•  an approximate location, usually your country or region, " +
                "worked out from your IP address\n" +
                "•  which adverts were shown, and whether they were watched or " +
                "tapped\n\n" +
                "We do not receive any of that in a form that identifies you. We see " +
                "totals: how many adverts were shown. Unity is an independent " +
                "controller for its own use of the data, under its own privacy " +
                "policy, at unity.com/legal/privacy-policy\n\n" +
                "<b>YOUR CHOICE</b>\n" +
                "Before any advert is requested we ask whether you agree to " +
                "personalised adverts. Saying no is a complete answer: you still get " +
                "the whole game, and you get adverts chosen without your data " +
                "instead. Nothing in the game is locked behind saying yes.\n\n" +
                "You can change your mind at any time in Settings → Privacy " +
                "choices. Withdrawing is exactly as easy as agreeing was.\n\n" +
                "<b>CHILDREN</b>\n" +
                "If you have not told us you are over 16, or you told us you are not, " +
                "you never receive personalised adverts — whatever any dialog " +
                "recorded. That rule is in the code and cannot be talked round.\n\n" +
                "<b>YOUR RIGHTS</b>\n" +
                "Because everything is on your device, you already hold your data. " +
                "You can erase all of it from Settings, or by uninstalling. For " +
                "anything else — access, objection, a complaint — write to " +
                ContactEmail + ", or complain to your national data protection " +
                "authority.\n\n" +
                "The full policy, with the regulatory detail, is published online. " +
                "The link is in Settings."),

            new LegalSection(
                "TERMS",
                "TERMS OF USE",
                "By playing Ring Toss you agree to these terms. They are short " +
                "because the game is.\n\n" +
                "<b>THE LICENCE</b>\n" +
                "Ring Toss is free. You get a personal, non-exclusive, " +
                "non-transferable licence to play it on devices you own. You may not " +
                "sell it, rent it, take it apart or ship a modified copy.\n\n" +
                "<b>TICKETS AND RINGS</b>\n" +
                "Tickets, ring skins and trails are not property and have no cash " +
                "value. They cannot be bought with money, sold, transferred or " +
                "exchanged for anything outside the game. They live in this " +
                "installation and are lost with it — if you uninstall, clear the " +
                "app's storage, or lose the device, they are gone, and we have no " +
                "copy to give back.\n\n" +
                "<b>ADVERTS</b>\n" +
                "The game is paid for by adverts. Rewarded adverts are always " +
                "optional and always pay out when watched to the end. If an advert " +
                "fails to load or fails to play, you keep whatever you had — a " +
                "failure never costs you anything.\n\n" +
                "<b>NO WARRANTY</b>\n" +
                "The game is provided as it is, with no warranty of any kind. We do " +
                "not promise it is free of faults or that it will run on every " +
                "device. To the extent the law allows, we are not liable for loss " +
                "arising from playing it — including lost progress. Nothing here " +
                "removes rights your local consumer law gives you, and in some " +
                "countries that law gives you a good deal.\n\n" +
                "<b>CHANGES</b>\n" +
                "These terms can change with a new version of the game. Carrying on " +
                "playing after an update means accepting the version that shipped " +
                "with it.\n\n" +
                "Questions: " + ContactEmail),

            new LegalSection(
                "NOTICES",
                "THIRD PARTIES",
                "What is in this build that somebody else wrote.\n\n" +
                "<b>UNITY ENGINE</b>\n" +
                "The game runs on Unity, under the Unity Software Additional Terms.\n\n" +
                "<b>UNITY ADS</b>\n" +
                "Unity Technologies. The only component in this build that sends " +
                "anything off the device. What it sends is in the PRIVACY tab.\n" +
                "unity.com/legal/privacy-policy\n\n" +
                "<b>PRIMETWEEN</b>\n" +
                "Kyrylo Kuzyk, MIT licence. Drives every animation in the game.\n\n" +
                "<b>UNITY PACKAGES</b>\n" +
                "Universal Render Pipeline, Cinemachine, Input System, TextMesh Pro " +
                "and the Test Framework, all Unity Companion Licence.\n\n" +
                "<b>ART AND SOUND</b>\n" +
                "There is none to credit. Every shape in this game is generated by " +
                "code at load, and every sound is synthesised from oscillators when " +
                "the game starts. No texture, model, font or audio file was " +
                "imported, so there is nothing here that belongs to anyone else.\n\n" +
                "The full notices are in docs/legal/THIRD_PARTY_NOTICES.md in the " +
                "source repository.")
        };
    }
}
