using System.Globalization;
using UnityEngine;

namespace SlipperyZlope.RingToss.Save
{
    /// <summary>
    /// What the player has unlocked and how well they did.
    ///
    /// PlayerPrefs rather than a save file: the entire save state is three integers
    /// per level, and a JSON file plus serialization plumbing would be more code than
    /// the thing it stores.
    /// </summary>
    public sealed class ProgressStore : MonoBehaviour
    {
        private const string UnlockedKey = "ringtoss.unlocked";
        private const string StarsKeyPrefix = "ringtoss.stars.";
        private const string ScoreKeyPrefix = "ringtoss.score.";
        private const string MutedKey = "ringtoss.muted";
        private const string EndlessScoreKey = "ringtoss.endless.score";
        private const string EndlessRoundKey = "ringtoss.endless.round";
        private const string TimeAttackScoreKey = "ringtoss.timeattack.score";
        private const string TimeAttackRoundKey = "ringtoss.timeattack.round";
        private const string TicketsKey = "ringtoss.tickets";
        private const string OwnedSkinsKey = "ringtoss.owned.skins";
        private const string OwnedTrailsKey = "ringtoss.owned.trails";
        private const string SkinKey = "ringtoss.equipped.skin";
        private const string TrailKey = "ringtoss.equipped.trail";
        private const string ClearedPrefix = "ringtoss.cleared.";
        private const string ThrowsKey = "ringtoss.throws";
        private const string ShareRewardKey = "ringtoss.share.rewarded";
        private const string ShareBoostKey = "ringtoss.share.boost";
        private const string DevUnlockKey = "ringtoss.dev.unlock";

        /// <summary>
        /// Ownership is a bitmask over the cosmetics library's index order, which caps
        /// each list at 32. That is a real limit and it is the right trade: an int in
        /// PlayerPrefs against a serialized list, for a shop that has twenty items in
        /// it. Item 33 is the day this becomes a string.
        /// </summary>
        public const int MaxCosmetics = 32;

        [Tooltip("modo_dev: every skin and trail counts as owned, for testing the shop and " +
                 "the cosmetics on a device. Nothing is written to the save — switch it off " +
                 "and the player's real purchases are exactly where they were. Must be off for " +
                 "a store build: the Android bundle build refuses to run with it on, and " +
                 "'Build Android APK (modo dev)' is the way to get a handset build with it.")]
        [SerializeField] private bool modoDev;

        /// <summary>
        /// Whether this build carries modo_dev at all — the checkbox as the build saw it.
        ///
        /// A build's own answer, and the reason there are two properties here: the switch
        /// is compiled in and cannot be turned *on* by a player, while a build that has it
        /// can turn it off and on from Settings, which is what makes it usable on a phone
        /// rather than only in the editor.
        /// </summary>
        public bool ModoDevAvailable => modoDev;

        /// <summary>
        /// modo_dev as it stands right now: in this build, and not switched off on this
        /// device. Settable so a test can flip it without a scene.
        /// </summary>
        public bool ModoDev
        {
            get => modoDev && PlayerPrefs.GetInt(DevUnlockKey, 1) != 0;
            set
            {
                modoDev = value;
                SetDevUnlock(value);
            }
        }

        /// <summary>Turns modo_dev off and on where the build has it. Ignored where it does not.</summary>
        public void SetDevUnlock(bool on)
        {
            PlayerPrefs.SetInt(DevUnlockKey, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Index of the highest level the player may enter. Level 0 is always open.</summary>
        public int HighestUnlockedLevel
        {
            get => PlayerPrefs.GetInt(UnlockedKey, 0);
            private set => PlayerPrefs.SetInt(UnlockedKey, value);
        }

        public bool IsMuted
        {
            get => PlayerPrefs.GetInt(MutedKey, 0) != 0;
            set
            {
                PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public int BestEndlessScore => PlayerPrefs.GetInt(EndlessScoreKey, 0);

        /// <summary>Deepest round ever cleared in endless mode.</summary>
        public int BestEndlessRound => PlayerPrefs.GetInt(EndlessRoundKey, 0);

        /// <summary>
        /// Throws ever made, capped once it stops being interesting.
        ///
        /// Only exists to answer "has this person played before", which decides whether
        /// the game explains how to throw. Counting rather than a flag because the
        /// answer is not binary — one throw is not learning the gesture, and a hint
        /// that vanishes after a single tap is a hint most people never read.
        /// </summary>
        public int ThrowsEver => PlayerPrefs.GetInt(ThrowsKey, 0);

        public void RecordThrow()
        {
            int thrown = ThrowsEver;

            // Stops writing once nothing above depends on it. This is called on every
            // throw for the rest of the player's life otherwise.
            if (thrown > 32)
            {
                return;
            }

            PlayerPrefs.SetInt(ThrowsKey, thrown + 1);
            PlayerPrefs.Save();
        }

        public int BestTimeAttackScore => PlayerPrefs.GetInt(TimeAttackScoreKey, 0);

        /// <summary>Deepest round ever cleared against the clock.</summary>
        public int BestTimeAttackRound => PlayerPrefs.GetInt(TimeAttackRoundKey, 0);

        /// <summary>The player's spending money.</summary>
        public int Tickets
        {
            get => PlayerPrefs.GetInt(TicketsKey, 0);
            private set => PlayerPrefs.SetInt(TicketsKey, Mathf.Max(0, value));
        }

        /// <summary>
        /// The equipped skin, and only ever one the player owns. Read back through the
        /// ownership check because modo_dev can equip anything and then be switched off:
        /// without the check a ring would go on wearing a skin nobody bought.
        /// </summary>
        public int EquippedSkin
        {
            get
            {
                int index = PlayerPrefs.GetInt(SkinKey, 0);
                return IsSkinOwned(index) ? index : 0;
            }
            private set => PlayerPrefs.SetInt(SkinKey, value);
        }

        public int EquippedTrail
        {
            get
            {
                int index = PlayerPrefs.GetInt(TrailKey, 0);
                return IsTrailOwned(index) ? index : 0;
            }
            private set => PlayerPrefs.SetInt(TrailKey, value);
        }

        public bool IsUnlocked(int levelIndex) => levelIndex <= HighestUnlockedLevel;

        /// <summary>True once this level has ever been finished, at any star count.</summary>
        public bool HasCleared(int levelIndex) => PlayerPrefs.GetInt(ClearedPrefix + levelIndex, 0) != 0;

        /// <summary>
        /// Adds tickets. Everything that pays the player goes through here, so the
        /// balance can only move in one place.
        /// </summary>
        public void AddTickets(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Tickets += amount;
            PlayerPrefs.Save();
        }

        /// <summary>
        /// When a share last paid, in UTC seconds; zero if it never has.
        ///
        /// Kept as a string because PlayerPrefs has no double, and a float of seconds
        /// since 1970 has no digits left for the seconds themselves.
        /// </summary>
        public double LastShareRewardUtc =>
            double.TryParse(PlayerPrefs.GetString(ShareRewardKey, "0"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double value) ? value : 0d;

        public void RecordShareReward(double nowUtc)
        {
            PlayerPrefs.SetString(ShareRewardKey, nowUtc.ToString("R", CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }

        /// <summary>Seconds until a share pays again, with a clock that was wound back repaired.</summary>
        public double SecondsUntilShareReward(double nowUtc)
        {
            double last = LastShareRewardUtc;

            if (ShareRules.IsFromTheFuture(nowUtc, last))
            {
                RecordShareReward(nowUtc);
                last = nowUtc;
            }

            return ShareRules.SecondsUntilReward(nowUtc, last);
        }

        /// <summary>
        /// True while the next share that pays is worth double: bought by watching a
        /// rewarded advert, spent by that share. Kept across sessions — it was earned,
        /// and closing the app is not a reason to lose it.
        /// </summary>
        public bool HasShareBoost => PlayerPrefs.GetInt(ShareBoostKey, 0) != 0;

        public void GrantShareBoost()
        {
            PlayerPrefs.SetInt(ShareBoostKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Spends the boost. True when there was one to spend.</summary>
        public bool ConsumeShareBoost()
        {
            if (!HasShareBoost)
            {
                return false;
            }

            PlayerPrefs.DeleteKey(ShareBoostKey);
            PlayerPrefs.Save();
            return true;
        }

        public bool IsSkinOwned(int index) =>
            index == 0 || IsUnlockedForDev(index) || IsOwned(OwnedSkinsKey, index);

        public bool IsTrailOwned(int index) =>
            index == 0 || IsUnlockedForDev(index) || IsOwned(OwnedTrailsKey, index);

        /// <summary>
        /// modo_dev: anything that could be in the library counts as owned.
        ///
        /// Through <see cref="ModoDev"/> rather than the serialized field, so switching it
        /// off in Settings puts the shop back. Reading the field here was the whole of the
        /// switch not working: the label said OFF and every item stayed unlocked.
        /// </summary>
        private bool IsUnlockedForDev(int index) => ModoDev && index >= 0 && index < MaxCosmetics;

        /// <summary>
        /// Buys a skin, if the player can afford it and does not already own it.
        /// </summary>
        /// <returns>True when tickets actually changed hands.</returns>
        public bool BuySkin(int index, int price) => Buy(OwnedSkinsKey, index, price, IsSkinOwned(index));

        public bool BuyTrail(int index, int price) => Buy(OwnedTrailsKey, index, price, IsTrailOwned(index));

        /// <summary>Equips an owned skin. Equipping something unowned is refused rather than allowed.</summary>
        public bool EquipSkin(int index)
        {
            if (!IsSkinOwned(index))
            {
                return false;
            }

            EquippedSkin = index;
            PlayerPrefs.Save();
            return true;
        }

        public bool EquipTrail(int index)
        {
            if (!IsTrailOwned(index))
            {
                return false;
            }

            EquippedTrail = index;
            PlayerPrefs.Save();
            return true;
        }

        private bool Buy(string key, int index, int price, bool alreadyOwned)
        {
            if (alreadyOwned || index < 0 || index >= MaxCosmetics || price > Tickets)
            {
                return false;
            }

            Tickets -= price;
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) | (1 << index));
            PlayerPrefs.Save();
            return true;
        }

        private static bool IsOwned(string key, int index)
        {
            if (index < 0 || index >= MaxCosmetics)
            {
                return false;
            }

            return (PlayerPrefs.GetInt(key, 0) & (1 << index)) != 0;
        }

        public int StarsFor(int levelIndex) => PlayerPrefs.GetInt(StarsKeyPrefix + levelIndex, 0);

        public int BestScoreFor(int levelIndex) => PlayerPrefs.GetInt(ScoreKeyPrefix + levelIndex, 0);

        public int TotalStars(int levelCount)
        {
            int total = 0;

            for (int i = 0; i < levelCount; i++)
            {
                total += StarsFor(i);
            }

            return total;
        }

        /// <summary>
        /// Records a completed level. Stars and score only ever go up, so replaying a
        /// level badly cannot cost the player anything they already earned.
        /// </summary>
        /// <returns>True when this run beat the stored score.</returns>
        public bool RecordCompletion(int levelIndex, int stars, int score, int levelCount)
        {
            if (stars > StarsFor(levelIndex))
            {
                PlayerPrefs.SetInt(StarsKeyPrefix + levelIndex, stars);
            }

            bool isNewBest = score > BestScoreFor(levelIndex);

            if (isNewBest)
            {
                PlayerPrefs.SetInt(ScoreKeyPrefix + levelIndex, score);
            }

            int nextLevel = Mathf.Min(levelIndex + 1, Mathf.Max(0, levelCount - 1));

            if (nextLevel > HighestUnlockedLevel)
            {
                HighestUnlockedLevel = nextLevel;
            }

            PlayerPrefs.SetInt(ClearedPrefix + levelIndex, 1);

            PlayerPrefs.Save();
            return isNewBest;
        }

        /// <summary>
        /// Records a finished endless run. Score and depth are tracked separately and
        /// each only goes up: a deep run that scored badly is still the deepest run,
        /// and both are things a player wants back.
        /// </summary>
        /// <returns>True when this run beat the stored score.</returns>
        public bool RecordEndlessRun(int score, int roundsCleared)
        {
            bool isNewBest = score > BestEndlessScore;

            if (isNewBest)
            {
                PlayerPrefs.SetInt(EndlessScoreKey, score);
            }

            if (roundsCleared > BestEndlessRound)
            {
                PlayerPrefs.SetInt(EndlessRoundKey, roundsCleared);
            }

            PlayerPrefs.Save();
            return isNewBest;
        }

        /// <summary>
        /// Records a finished time attack run, on its own records.
        ///
        /// Kept apart from the endless ones rather than merged into a single "best
        /// run". They are scored by the same points but they are not the same
        /// achievement, and a leaderboard that mixed them would be won permanently by
        /// whichever mode happened to pay better.
        /// </summary>
        /// <returns>True when this run beat the stored score.</returns>
        public bool RecordTimeAttackRun(int score, int roundsCleared)
        {
            bool isNewBest = score > BestTimeAttackScore;

            if (isNewBest)
            {
                PlayerPrefs.SetInt(TimeAttackScoreKey, score);
            }

            if (roundsCleared > BestTimeAttackRound)
            {
                PlayerPrefs.SetInt(TimeAttackRoundKey, roundsCleared);
            }

            PlayerPrefs.Save();
            return isNewBest;
        }

        public void ResetAll(int levelCount)
        {
            for (int i = 0; i < levelCount; i++)
            {
                PlayerPrefs.DeleteKey(StarsKeyPrefix + i);
                PlayerPrefs.DeleteKey(ScoreKeyPrefix + i);
                PlayerPrefs.DeleteKey(ClearedPrefix + i);
            }

            PlayerPrefs.DeleteKey(UnlockedKey);
            PlayerPrefs.DeleteKey(EndlessScoreKey);
            PlayerPrefs.DeleteKey(EndlessRoundKey);
            PlayerPrefs.DeleteKey(TimeAttackScoreKey);
            PlayerPrefs.DeleteKey(TimeAttackRoundKey);
            PlayerPrefs.DeleteKey(ThrowsKey);
            PlayerPrefs.DeleteKey(TicketsKey);
            PlayerPrefs.DeleteKey(OwnedSkinsKey);
            PlayerPrefs.DeleteKey(OwnedTrailsKey);
            PlayerPrefs.DeleteKey(SkinKey);
            PlayerPrefs.DeleteKey(TrailKey);
            PlayerPrefs.DeleteKey(ShareRewardKey);
            PlayerPrefs.DeleteKey(ShareBoostKey);
            PlayerPrefs.Save();
        }
    }
}
