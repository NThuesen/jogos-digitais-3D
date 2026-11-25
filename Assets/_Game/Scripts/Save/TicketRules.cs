using UnityEngine;

namespace SlipperyZlope.RingToss.Save
{
    /// <summary>
    /// What the game pays for, and how much.
    ///
    /// The whole economy is here as pure arithmetic, so it can be balanced by reading
    /// eight numbers rather than by hunting for `AddTickets` calls, and so a change to
    /// it can be checked without playing fifty levels.
    ///
    /// The shape of it matters more than the values. A perfect ringer pays every time,
    /// in both modes, because that is the skill the game is about and it should always
    /// be worth reaching for. Clearing a level pays properly once and a fraction on
    /// every replay after that — enough that grinding an easy level is never the
    /// fastest way to earn, which is the failure mode that makes a shop feel like a
    /// chore instead of a reward.
    /// </summary>
    public static class TicketRules
    {
        /// <summary>Paid for every ringer that never touched the peg, in either mode.</summary>
        public const int PerPerfect = 3;

        public const int LevelClearBase = 15;
        public const int LevelClearPerStar = 10;

        /// <summary>A replay pays about a fifth, so the shop is never a grinding puzzle.</summary>
        public const int ReplayClearBase = 3;
        public const int ReplayClearPerStar = 2;

        public const int EndlessRoundBase = 2;

        /// <summary>Paid when a run ends, scaled by how deep it got.</summary>
        public const int EndlessRunPerRound = 4;

        public static int ForPerfect() => PerPerfect;

        public static int ForLevelClear(int stars, bool firstClear)
        {
            stars = Mathf.Clamp(stars, 0, 3);

            return firstClear
                ? LevelClearBase + LevelClearPerStar * stars
                : ReplayClearBase + ReplayClearPerStar * stars;
        }

        /// <summary>
        /// A cleared endless round. Grows with depth, so a long run pays for itself
        /// rather than paying the same for round forty as for round two.
        /// </summary>
        public static int ForEndlessRound(int round)
        {
            return EndlessRoundBase + Mathf.Max(0, round - 1) / 3;
        }

        public static int ForEndlessRun(int roundsCleared)
        {
            return EndlessRunPerRound * Mathf.Max(0, roundsCleared);
        }

        /// <summary>
        /// A voluntarily watched advert.
        ///
        /// Kept small on purpose, and the number is the whole argument: the cheapest
        /// paid ring costs 150, so it is seven or eight adverts, and the shop's back
        /// half runs into the thousands. A player who wants one particular ring can
        /// watch their way to it in an evening; a player trying to buy the catalogue
        /// this way would be at it for weeks, and would be right to conclude that
        /// playing the game is faster. That is the correct conclusion and it is what
        /// stops a rewarded placement quietly becoming the game.
        ///
        /// Cut from 30 after the owner played a build with it: between the advert and
        /// the boosted share, a player who never improved was out-earning one who was
        /// actually good at the game, which inverts what the shop is for.
        /// </summary>
        public const int RewardedAd = 20;

        public static int ForRewardedAd() => RewardedAd;

        /// <summary>
        /// Paid for sharing the game, at most once per ShareRules.CooldownSeconds. Below
        /// the voluntary advert on purpose: the advert pays for the game, and the share is
        /// a thank-you. Cut from 20 with the advert, and by more than the advert was, so
        /// the gap between "earning" and "being thanked" widened rather than closed.
        /// </summary>
        public const int Share = 12;

        public static int ForShare() => Share;

        /// <summary>
        /// What the next paying share is multiplied by once the player has chosen to watch
        /// a rewarded advert. Two: a share that pays 24 is worth a little over an advert,
        /// and an advert plus a boosted share is still less than a good few minutes of
        /// play — sharing stays a thank-you rather than becoming the way to earn.
        /// </summary>
        public const int ShareBoostMultiplier = 2;

        public static int ForShare(bool boosted) => boosted ? Share * ShareBoostMultiplier : Share;
    }
}
