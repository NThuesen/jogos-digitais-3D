using UnityEngine;

namespace SlipperyZlope.RingToss.Gameplay
{
    /// <summary>
    /// The time attack run's bookkeeping: one clock, and everything that moves it.
    ///
    /// A plain class with no Unity dependency, like <see cref="EndlessRules"/> and for
    /// the same reason — the whole economy can be played out in a unit test without a
    /// scene, and the numbers that decide whether the mode is any fun can be read down
    /// one list.
    ///
    /// The mode is the same board and the same throw as endless. What changes is the
    /// resource. Endless asks "how many can you get right"; this asks "how fast can
    /// you get them right", and the difference is entirely in what runs out.
    ///
    /// Three forces act on the clock and they are deliberately unequal:
    ///
    /// - it drains **continuously**, so standing still is losing and aiming forever is
    ///   not a strategy;
    /// - a good throw pays time back, more for a perfect than for a scrappy catch,
    ///   because the mode still has to reward the skill the game is about;
    /// - a miss costs time on top of the time it already took.
    ///
    /// The cap is what stops the whole thing collapsing. Without one a strong player
    /// banks a minute in the first four rounds and the clock stops being a threat for
    /// the rest of the run — which is exactly the failure the endless mode's life cap
    /// exists to prevent, arrived at from the other direction.
    /// </summary>
    public sealed class TimeAttackRules
    {
        /// <summary>Seconds on the clock at the start of a run.</summary>
        public const float StartingSeconds = 30f;

        /// <summary>
        /// The most the clock may ever hold. Time earned past this is lost, on purpose:
        /// see the class note.
        /// </summary>
        public const float MaxSeconds = 45f;

        /// <summary>Paid for a ring that went over without ever touching the peg.</summary>
        public const float PerfectBonus = 2.5f;

        /// <summary>Paid for a catch that bounced its way on. Worth having, worth less.</summary>
        public const float RingerBonus = 1.2f;

        /// <summary>Charged for a failed throw, on top of the seconds it already cost.</summary>
        public const float MissPenalty = 3f;

        /// <summary>Paid for clearing a whole board.</summary>
        public const float RoundClearBonus = 4f;

        public const float BaseDrainPerSecond = 1f;

        /// <summary>
        /// Where the drain ends up.
        ///
        /// The boards get harder on their own — this runs the same generator the
        /// endless mode does — so the ramp here is gentle. It exists because board
        /// difficulty alone makes a run end *eventually*, and a mode about agility
        /// should be closing in on the player the whole time rather than only once the
        /// pegs start moving.
        /// </summary>
        public const float MaxDrainPerSecond = 1.6f;

        /// <summary>Rounds the drain takes to reach its maximum.</summary>
        public const int DrainRampRounds = 20;

        public float Remaining { get; private set; } = StartingSeconds;

        /// <summary>Rounds started, 1 while the first one is being played.</summary>
        public int Round { get; private set; }

        public int RoundsCleared { get; private set; }

        public int TotalPerfects { get; private set; }

        public bool IsOver => Remaining <= 0f;

        /// <summary>How full the clock is, for a bar to draw.</summary>
        public float Fraction => Mathf.Clamp01(Remaining / MaxSeconds);

        /// <summary>Seconds lost per second of play at the current round.</summary>
        public float DrainPerSecond => Mathf.Lerp(
            BaseDrainPerSecond,
            MaxDrainPerSecond,
            Mathf.Clamp01((Round - 1f) / DrainRampRounds));

        /// <summary>Seconds a continue is worth. Half a starting clock: a rescue, not a restart.</summary>
        public const float ContinueSeconds = 15f;

        /// <summary>
        /// Puts time back on the clock, bought with a rewarded advert. Clamped to
        /// <see cref="MaxSeconds"/> like every other bonus, so the ceiling on how much
        /// clock a player can be holding does not depend on how they got it.
        /// </summary>
        public void Extend(float seconds)
        {
            Remaining = Mathf.Min(Remaining + Mathf.Max(0f, seconds), MaxSeconds);
        }

        public void Reset()
        {
            Remaining = StartingSeconds;
            Round = 0;
            RoundsCleared = 0;
            TotalPerfects = 0;
        }

        public void BeginRound()
        {
            Round++;
        }

        public void CompleteRound()
        {
            RoundsCleared++;
            Add(RoundClearBonus);
        }

        /// <summary>
        /// Runs the clock down. Driven from the outside so it shares the session's
        /// clock and a board that is not being played does not cost the player time.
        /// </summary>
        public void Drain(float deltaTime)
        {
            Spend(deltaTime * DrainPerSecond);
        }

        public void RegisterPerfect()
        {
            TotalPerfects++;
            Add(PerfectBonus);
        }

        public void RegisterRinger()
        {
            Add(RingerBonus);
        }

        public void RegisterMiss()
        {
            Spend(MissPenalty);
        }

        private void Add(float seconds)
        {
            // A run that is already over cannot be rescued by a reward that lands in
            // the same frame. Otherwise the last throw of a run resolves after the
            // clock hits zero and quietly puts time back on it.
            if (IsOver)
            {
                return;
            }

            Remaining = Mathf.Min(MaxSeconds, Remaining + seconds);
        }

        private void Spend(float seconds)
        {
            Remaining = Mathf.Max(0f, Remaining - seconds);
        }
    }
}
