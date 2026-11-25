using System;

namespace SlipperyZlope.RingToss.Save
{
    /// <summary>
    /// "Share with a friend": the link, the message, and when a share pays.
    ///
    /// A plain class with no Unity in it, for the reason every rule in this folder is:
    /// the part that decides whether the player gets paid is the part that has to be
    /// right, and it can be settled in a unit test.
    ///
    /// The link can be sent as often as the player likes; the tickets come at most once
    /// every two minutes. That split is the owner's and it is the right one — sharing is
    /// the thing worth encouraging, and a reward with no cooldown turns the button into
    /// a ticket tap that has nothing to do with anybody else.
    ///
    /// The time is wall-clock UTC, stored, rather than time since launch. A cooldown
    /// measured from launch resets every time the app is closed, which makes it a
    /// cooldown only for players who never close apps.
    /// </summary>
    public static class ShareRules
    {
        public const string PackageId = "com.slipperyzlope.ringtoss";

        public const string Subject = "Ring Toss";

        /// <summary>Seconds between two shares that pay.</summary>
        public const double CooldownSeconds = 120d;

        public static double NowUtc() => (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;

        /// <summary>
        /// The store page.
        ///
        /// It does not exist until the game is published, and it does not have to: a Play
        /// Store URL is fixed by the package name, so this is already the link that works
        /// on launch day. The referrer tags installs that came from a share, which the
        /// Play Console reports under acquisition — worth having from the first install.
        /// </summary>
        public static string StoreLink(string source)
        {
            // Only characters that need no escaping, because the referrer is itself an
            // escaped query string inside the URL and anything else would need escaping
            // twice. Anything unexpected is tagged as a plain share.
            string tag = IsPlain(source) ? source : "share";

            return "https://play.google.com/store/apps/details?id=" + PackageId
                   + "&referrer=utm_source%3D" + tag + "%26utm_medium%3Dshare";
        }

        /// <summary>
        /// What goes in the message. A number is a better invitation than an adjective,
        /// so a player with a best round to their name leads with it.
        /// </summary>
        public static string Message(int bestRound, string link)
        {
            return bestRound > 0
                ? $"I made it to round {bestRound} in Ring Toss. Think you can beat that?\n{link}"
                : $"Come and throw some rings with me in Ring Toss!\n{link}";
        }

        /// <summary>How long until a share pays again. Zero means now.</summary>
        public static double SecondsUntilReward(double nowUtc, double lastRewardUtc)
        {
            if (lastRewardUtc <= 0d)
            {
                return 0d;
            }

            double elapsed = nowUtc - lastRewardUtc;

            // A reward in the future means the device clock was wound back. Treated as
            // "just paid" rather than trusted — see IsFromTheFuture.
            if (elapsed < 0d)
            {
                return CooldownSeconds;
            }

            return Math.Max(0d, CooldownSeconds - elapsed);
        }

        public static bool CanReward(double nowUtc, double lastRewardUtc) =>
            SecondsUntilReward(nowUtc, lastRewardUtc) <= 0d;

        /// <summary>
        /// True when the stored reward time is later than now.
        ///
        /// Winding the clock back must not pay twice, and it must not lock the reward
        /// away until the clock catches up again either — which, after a year's jump,
        /// would be never. The store rewrites the time to now when this is true, so the
        /// cooldown ends two minutes from the moment it was noticed.
        /// </summary>
        public static bool IsFromTheFuture(double nowUtc, double lastRewardUtc) =>
            lastRewardUtc > nowUtc + 1d;

        private static bool IsPlain(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (char c in value)
            {
                bool plain = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';

                if (!plain)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
