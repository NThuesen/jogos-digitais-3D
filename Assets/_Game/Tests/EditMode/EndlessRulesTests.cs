using NUnit.Framework;
using SlipperyZlope.RingToss.Gameplay;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// The endless run's economy. All of it is arithmetic on four integers, so it is
    /// worth pinning down here rather than discovering by playing.
    /// </summary>
    public sealed class EndlessRulesTests
    {
        private EndlessRules rules;

        [SetUp]
        public void SetUp()
        {
            rules = new EndlessRules();
            rules.Reset();
        }

        [Test]
        public void A_run_starts_with_three_lives_and_no_streak()
        {
            Assert.AreEqual(3, rules.Lives);
            Assert.AreEqual(EndlessRules.StartingLives, rules.Lives);
            Assert.AreEqual(0, rules.PerfectStreak);
            Assert.AreEqual(0, rules.Round);
            Assert.IsFalse(rules.IsOver);
        }

        [Test]
        public void A_miss_costs_a_life()
        {
            rules.RegisterMiss();

            Assert.AreEqual(2, rules.Lives);
        }

        [Test]
        public void The_run_ends_when_the_last_life_is_spent()
        {
            rules.RegisterMiss();
            rules.RegisterMiss();

            Assert.IsFalse(rules.IsOver, "Two misses out of three is not the end.");

            rules.RegisterMiss();

            Assert.IsTrue(rules.IsOver);
            Assert.AreEqual(0, rules.Lives);
        }

        [Test]
        public void Lives_never_go_below_zero()
        {
            for (int i = 0; i < 10; i++)
            {
                rules.RegisterMiss();
            }

            Assert.AreEqual(0, rules.Lives);
        }

        [Test]
        public void Five_perfects_buy_a_lost_life_back()
        {
            rules.RegisterMiss();
            Assert.AreEqual(2, rules.Lives);

            for (int i = 0; i < EndlessRules.PerfectsPerLife - 1; i++)
            {
                PerfectReward partial = rules.RegisterPerfect();

                Assert.IsFalse(partial.StreakCompleted, $"Perfect {i + 1} should not complete the streak.");
                Assert.AreEqual(2, rules.Lives);
            }

            PerfectReward reward = rules.RegisterPerfect();

            Assert.IsTrue(reward.StreakCompleted);
            Assert.IsTrue(reward.LifeGranted);
            Assert.AreEqual(3, rules.Lives);
            Assert.AreEqual(0, rules.PerfectStreak, "The streak resets once it has paid out.");
        }

        [Test]
        public void A_completed_streak_at_full_lives_pays_out_without_a_life()
        {
            for (int i = 0; i < EndlessRules.PerfectsPerLife; i++)
            {
                PerfectReward reward = rules.RegisterPerfect();

                if (i < EndlessRules.PerfectsPerLife - 1)
                {
                    continue;
                }

                Assert.IsTrue(reward.StreakCompleted, "The streak still completes at full health.");
                Assert.IsFalse(reward.LifeGranted, "There is no life to give back.");
            }

            Assert.AreEqual(EndlessRules.MaxLives, rules.Lives);
        }

        [Test]
        public void Lives_never_exceed_the_cap()
        {
            for (int i = 0; i < EndlessRules.PerfectsPerLife * 6; i++)
            {
                rules.RegisterPerfect();
            }

            Assert.AreEqual(EndlessRules.MaxLives, rules.Lives);
        }

        [Test]
        public void A_miss_breaks_the_streak()
        {
            rules.RegisterPerfect();
            rules.RegisterPerfect();

            Assert.AreEqual(2, rules.PerfectStreak);

            rules.RegisterMiss();

            Assert.AreEqual(0, rules.PerfectStreak);
        }

        [Test]
        public void Rounds_and_perfects_are_counted_across_the_whole_run()
        {
            for (int i = 0; i < 4; i++)
            {
                rules.BeginRound();
                rules.RegisterPerfect();
                rules.CompleteRound();
            }

            Assert.AreEqual(4, rules.Round);
            Assert.AreEqual(4, rules.RoundsCleared);
            Assert.AreEqual(4, rules.TotalPerfects);
        }

        [Test]
        public void Resetting_clears_everything()
        {
            rules.BeginRound();
            rules.RegisterPerfect();
            rules.RegisterMiss();

            rules.Reset();

            Assert.AreEqual(EndlessRules.StartingLives, rules.Lives);
            Assert.AreEqual(0, rules.Round);
            Assert.AreEqual(0, rules.RoundsCleared);
            Assert.AreEqual(0, rules.TotalPerfects);
            Assert.AreEqual(0, rules.PerfectStreak);
        }

        /// <summary>
        /// The whole point of the mode: a player who never misses is never stopped,
        /// and a player who misses more than they recover always is.
        /// </summary>
        [Test]
        public void A_perfect_player_survives_indefinitely()
        {
            for (int round = 0; round < 200; round++)
            {
                rules.BeginRound();
                rules.RegisterPerfect();
                rules.CompleteRound();

                Assert.IsFalse(rules.IsOver);
            }
        }

        [Test]
        public void Missing_faster_than_the_streak_recovers_ends_the_run()
        {
            // Four perfects then a miss, forever: the streak never reaches five, so no
            // life ever comes back and the three the run started with run out.
            for (int cycle = 0; cycle < 3; cycle++)
            {
                for (int i = 0; i < EndlessRules.PerfectsPerLife - 1; i++)
                {
                    rules.RegisterPerfect();
                }

                rules.RegisterMiss();
            }

            Assert.IsTrue(rules.IsOver);
        }
    }
}
