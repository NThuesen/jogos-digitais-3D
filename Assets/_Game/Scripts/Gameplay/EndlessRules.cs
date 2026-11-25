using UnityEngine;

namespace SlipperyZlope.RingToss.Gameplay
{
    /// <summary>What a perfect ringer earned.</summary>
    public readonly struct PerfectReward
    {
        /// <summary>True when this perfect completed a run of them.</summary>
        public readonly bool StreakCompleted;

        /// <summary>True when completing the streak actually gave a life back.</summary>
        public readonly bool LifeGranted;

        public PerfectReward(bool streakCompleted, bool lifeGranted)
        {
            StreakCompleted = streakCompleted;
            LifeGranted = lifeGranted;
        }
    }

    /// <summary>
    /// The endless run's bookkeeping: lives, rounds, and the perfect streak that buys
    /// a life back.
    ///
    /// A plain class rather than a MonoBehaviour, so the whole economy can be played
    /// out in a unit test without a scene. Nothing here knows about pegs, rings or
    /// animation.
    /// </summary>
    public sealed class EndlessRules
    {
        public const int StartingLives = 3;

        /// <summary>Perfect ringers in a row that buy one life back.</summary>
        public const int PerfectsPerLife = 5;

        /// <summary>
        /// Lives never go above the number the run started with.
        ///
        /// A cap above the start would let a strong player bank a buffer and turn the
        /// back half of a run into a formality. Capping at the start means a clean
        /// streak restores what a mistake cost and nothing more, which keeps every
        /// round dangerous however well the last one went. Streaks completed at full
        /// health are not wasted: they pay out as score instead.
        /// </summary>
        public const int MaxLives = StartingLives;

        public int Lives { get; private set; } = StartingLives;

        /// <summary>Rounds started, 1 while the first one is being played.</summary>
        public int Round { get; private set; }

        /// <summary>Perfects since the last life was earned, 0..PerfectsPerLife - 1.</summary>
        public int PerfectStreak { get; private set; }

        public int TotalPerfects { get; private set; }

        public int RoundsCleared { get; private set; }

        public bool IsOver => Lives <= 0;

        public void Reset()
        {
            Lives = StartingLives;
            Round = 0;
            PerfectStreak = 0;
            TotalPerfects = 0;
            RoundsCleared = 0;
        }

        public void BeginRound()
        {
            Round++;
        }

        public void CompleteRound()
        {
            RoundsCleared++;
        }

        /// <summary>
        /// Books a ringer that never touched the peg.
        ///
        /// The streak is deliberately not broken by an ordinary ringer, only by a
        /// miss. Otherwise the safe play — nudge the ring on with a bounce — would
        /// cost the player their progress towards a life, and the mode would punish
        /// exactly the recovery it is supposed to reward.
        /// </summary>
        public PerfectReward RegisterPerfect()
        {
            TotalPerfects++;
            PerfectStreak++;

            if (PerfectStreak < PerfectsPerLife)
            {
                return new PerfectReward(false, false);
            }

            PerfectStreak = 0;

            if (Lives >= MaxLives)
            {
                return new PerfectReward(true, false);
            }

            Lives++;
            return new PerfectReward(true, true);
        }

        /// <summary>Books a failed throw. Costs a life and resets the streak.</summary>
        /// <summary>
        /// Puts one life back, bought with a rewarded advert the player chose to watch.
        ///
        /// Capped like every other life, so a continue can rescue a run but can never
        /// stack into a bigger safety net than playing well earns.
        /// </summary>
        public void GrantLife()
        {
            Lives = Mathf.Min(Lives + 1, MaxLives);
        }

        public void RegisterMiss()
        {
            Lives = Mathf.Max(0, Lives - 1);
            PerfectStreak = 0;
        }
    }
}
