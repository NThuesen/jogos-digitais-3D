using NUnit.Framework;
using SlipperyZlope.RingToss.Gameplay;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// The time attack economy.
    ///
    /// Tested for the same reason <see cref="EndlessRulesTests"/> exists: the mode is
    /// eight numbers and the numbers decide whether it is a game or a stopwatch. The
    /// two that matter most are the cap and the drain, and both have a failure that is
    /// invisible from a single playthrough — a cap that does not hold turns the back
    /// half of a good run into a formality, and a drain that stops ramping means a
    /// strong player never loses at all.
    /// </summary>
    public sealed class TimeAttackRulesTests
    {
        private static TimeAttackRules Fresh()
        {
            TimeAttackRules rules = new TimeAttackRules();
            rules.Reset();
            return rules;
        }

        [Test]
        public void A_run_starts_with_time_on_the_clock_and_is_not_over()
        {
            TimeAttackRules rules = Fresh();

            Assert.AreEqual(TimeAttackRules.StartingSeconds, rules.Remaining, 0.001f);
            Assert.IsFalse(rules.IsOver);
        }

        [Test]
        public void Standing_still_loses()
        {
            TimeAttackRules rules = Fresh();
            rules.BeginRound();

            for (int i = 0; i < 60 * 60; i++)
            {
                rules.Drain(1f / 60f);
            }

            Assert.IsTrue(rules.IsOver, "A minute of doing nothing has to end the run.");
        }

        [Test]
        public void A_perfect_is_worth_more_time_than_a_scrappy_catch()
        {
            // The mode is about speed, but it must not be *only* about speed, or the
            // right strategy becomes throwing as fast as possible at nothing.
            Assert.Greater(TimeAttackRules.PerfectBonus, TimeAttackRules.RingerBonus);
        }

        [Test]
        public void A_miss_costs_more_than_a_catch_pays()
        {
            // Otherwise a player could farm time by missing on purpose next to a peg
            // they then ring, which is the sort of thing that only shows up after
            // release and is impossible to take back.
            Assert.Greater(TimeAttackRules.MissPenalty, TimeAttackRules.PerfectBonus);
        }

        [Test]
        public void The_clock_cannot_be_banked_past_its_cap()
        {
            TimeAttackRules rules = Fresh();

            for (int i = 0; i < 200; i++)
            {
                rules.RegisterPerfect();
            }

            Assert.AreEqual(TimeAttackRules.MaxSeconds, rules.Remaining, 0.001f);
        }

        [Test]
        public void The_clock_never_goes_below_zero()
        {
            TimeAttackRules rules = Fresh();

            for (int i = 0; i < 100; i++)
            {
                rules.RegisterMiss();
            }

            Assert.AreEqual(0f, rules.Remaining, 0.001f);
            Assert.IsTrue(rules.IsOver);
        }

        [Test]
        public void A_reward_landing_after_the_clock_empties_cannot_rescue_the_run()
        {
            // The last throw of a run resolves a frame or two after the clock hits
            // zero. Without this the run would quietly come back to life.
            TimeAttackRules rules = Fresh();

            for (int i = 0; i < 100; i++)
            {
                rules.RegisterMiss();
            }

            rules.RegisterPerfect();
            rules.CompleteRound();

            Assert.IsTrue(rules.IsOver);
            Assert.AreEqual(0f, rules.Remaining, 0.001f);
        }

        [Test]
        public void Clearing_a_round_banks_it_and_pays_time()
        {
            TimeAttackRules rules = Fresh();
            rules.BeginRound();
            rules.Drain(10f);

            float before = rules.Remaining;
            rules.CompleteRound();

            Assert.AreEqual(1, rules.RoundsCleared);
            Assert.Greater(rules.Remaining, before);
        }

        [Test]
        public void The_drain_gets_harsher_as_the_run_goes_on_and_then_stops()
        {
            TimeAttackRules early = Fresh();
            early.BeginRound();

            TimeAttackRules late = Fresh();

            for (int i = 0; i < TimeAttackRules.DrainRampRounds + 1; i++)
            {
                late.BeginRound();
            }

            Assert.Greater(late.DrainPerSecond, early.DrainPerSecond);
            Assert.AreEqual(TimeAttackRules.MaxDrainPerSecond, late.DrainPerSecond, 0.001f);

            // And it is a ramp, not a cliff: a player must never lose a run to a
            // difficulty step they could not see coming.
            TimeAttackRules middle = Fresh();

            for (int i = 0; i < TimeAttackRules.DrainRampRounds / 2; i++)
            {
                middle.BeginRound();
            }

            Assert.Greater(middle.DrainPerSecond, early.DrainPerSecond);
            Assert.Less(middle.DrainPerSecond, late.DrainPerSecond);
        }

        [Test]
        public void A_reset_puts_everything_back()
        {
            TimeAttackRules rules = Fresh();

            rules.BeginRound();
            rules.RegisterPerfect();
            rules.CompleteRound();
            rules.Drain(5f);
            rules.Reset();

            Assert.AreEqual(TimeAttackRules.StartingSeconds, rules.Remaining, 0.001f);
            Assert.AreEqual(0, rules.Round);
            Assert.AreEqual(0, rules.RoundsCleared);
            Assert.AreEqual(0, rules.TotalPerfects);
        }
    }
}
