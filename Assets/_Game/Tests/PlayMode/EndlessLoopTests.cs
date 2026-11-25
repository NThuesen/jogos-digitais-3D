using System.Collections;
using NUnit.Framework;
using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// The generated runs, in the real scene: clearing a round advances to the next
    /// one, a miss costs a life, running out of lives ends the run — and the same
    /// board played against a clock instead.
    ///
    /// Time attack shares this fixture rather than getting its own because it shares
    /// the machinery: the same generator, the same ramp, the same director. A separate
    /// fixture would have duplicated the scene lifecycle in order to test the one
    /// thing that differs, which is what runs out.
    ///
    /// Throws go through <see cref="ThrowController.LaunchArmedRing"/>, the same exit
    /// the player's drag uses. Synthesising touch would test the input layer instead
    /// of the loop, and far more fragilely.
    /// </summary>
    public sealed class EndlessLoopTests
    {
        private GameDirector director;
        private EndlessResult lastResult;
        private bool runEnded;
        private int roundsCleared;
        private float originalCaptureDeltaTime;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // See GameLoopTests: batch mode frames vary wildly and Unity runs catch-up
            // fixed steps, which can drop a ring straight past a peg between two
            // observations.
            originalCaptureDeltaTime = Time.captureDeltaTime;
            Time.captureDeltaTime = Time.fixedDeltaTime;

            SceneManager.LoadScene("Main");

            yield return null;
            yield return null;
            yield return null;

            director = Object.FindFirstObjectByType<GameDirector>();
            Assert.IsNotNull(director, "The Main scene has no GameDirector.");

            runEnded = false;
            roundsCleared = 0;

            GameEvents.RunEnded += OnRunEnded;
            GameEvents.RoundCleared += OnRoundCleared;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameEvents.RunEnded -= OnRunEnded;
            GameEvents.RoundCleared -= OnRoundCleared;

            if (director != null)
            {
                director.ReturnToIdle();

                if (director.Progress != null)
                {
                    director.Progress.ResetAll(director.LevelCount);
                }
            }

            Time.timeScale = 1f;
            Time.captureDeltaTime = originalCaptureDeltaTime;

            Scene main = SceneManager.GetSceneByName("Main");

            if (!main.IsValid() || !main.isLoaded)
            {
                yield break;
            }

            SceneManager.SetActiveScene(SceneManager.CreateScene("AfterEndlessLoopTests"));
            yield return SceneManager.UnloadSceneAsync(main);
        }

        private void OnRunEnded(EndlessResult result)
        {
            lastResult = result;
            runEnded = true;
        }

        private void OnRoundCleared(int round, int bonus)
        {
            roundsCleared++;
        }

        [UnityTest]
        public IEnumerator Starting_a_run_opens_round_one_with_three_lives()
        {
            director.StartEndless();

            yield return WaitForState(SessionState.Ready, 5f);

            Assert.AreEqual(GameMode.Endless, director.Mode);
            Assert.AreEqual(1, director.Endless.Round);
            Assert.AreEqual(EndlessRules.StartingLives, director.Endless.Lives);
            Assert.AreEqual(1, director.Board.PegsRemaining, "Round one is a single peg.");
            Assert.IsNotNull(director.ActiveRing, "No ring was handed to the player.");
        }

        [UnityTest]
        public IEnumerator Clearing_a_round_advances_to_the_next_one()
        {
            director.StartEndless();
            yield return WaitForState(SessionState.Ready, 5f);

            yield return DropRingOnPeg();
            yield return WaitForRound(2, 10f);

            Assert.AreEqual(2, director.Endless.Round);
            Assert.AreEqual(1, director.Endless.RoundsCleared);
            Assert.AreEqual(1, roundsCleared, "Clearing a round has to announce itself.");
            Assert.Greater(director.Score, 0, "A cleared round must have scored.");
            Assert.IsFalse(runEnded, "Clearing a round is not the end of the run.");
        }

        [UnityTest]
        public IEnumerator The_score_carries_across_rounds()
        {
            director.StartEndless();
            yield return WaitForState(SessionState.Ready, 5f);

            yield return DropRingOnPeg();
            yield return WaitForRound(2, 10f);

            int afterFirst = director.Score;

            // Round two is two pegs, not one. Clearing a round means clearing the
            // board, so the test has to keep throwing until the board is empty rather
            // than assume one ring is a round.
            yield return ClearCurrentRound(20f);
            yield return WaitForRound(3, 12f);

            Assert.Greater(director.Score, afterFirst,
                "Endless keeps one running total; a new round must not reset it.");
        }

        [UnityTest]
        public IEnumerator A_miss_costs_a_life_without_ending_the_round()
        {
            director.StartEndless();
            yield return WaitForState(SessionState.Ready, 5f);

            yield return ThrowAway();

            Assert.AreEqual(EndlessRules.StartingLives - 1, director.Endless.Lives);
            Assert.AreEqual(1, director.Endless.Round, "A miss does not advance the round.");
            Assert.IsFalse(runEnded);

            // And the player gets another ring rather than being left holding nothing.
            yield return WaitForState(SessionState.Ready, 5f);
            Assert.IsNotNull(director.ActiveRing);
        }

        [UnityTest]
        public IEnumerator Spending_every_life_ends_the_run()
        {
            director.StartEndless();
            yield return WaitForState(SessionState.Ready, 5f);

            for (int i = 0; i < EndlessRules.StartingLives && !runEnded; i++)
            {
                yield return ThrowAway();
            }

            yield return WaitForRunEnd(8f);

            Assert.AreEqual(0, director.Endless.Lives);
            Assert.AreEqual(0, lastResult.RoundsCleared, "No round was ever cleared.");
            Assert.AreEqual(SessionState.Finished, director.State);
        }

        [UnityTest]
        public IEnumerator Starting_a_time_attack_run_opens_round_one_with_a_full_clock()
        {
            director.StartTimeAttack();

            yield return WaitForState(SessionState.Ready, 5f);

            Assert.AreEqual(GameMode.TimeAttack, director.Mode);
            Assert.IsTrue(director.IsGeneratedRun);
            Assert.AreEqual(1, director.CurrentRound);
            Assert.AreEqual(1, director.Board.PegsRemaining, "Round one is a single peg.");
            Assert.IsNotNull(director.ActiveRing, "No ring was handed to the player.");

            Assert.AreEqual(
                TimeAttackRules.StartingSeconds, director.TimeAttack.Remaining, 0.5f,
                "A run has to start on a full clock.");
        }

        [UnityTest]
        public IEnumerator The_clock_runs_down_while_the_board_is_being_played()
        {
            director.StartTimeAttack();
            yield return WaitForState(SessionState.Ready, 5f);

            float start = director.TimeAttack.Remaining;

            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }

            Assert.Less(director.TimeAttack.Remaining, start,
                "Standing still has to cost time, or the mode is not against a clock.");
        }

        [UnityTest]
        public IEnumerator Clearing_a_round_against_the_clock_advances_and_pays_time()
        {
            director.StartTimeAttack();
            yield return WaitForState(SessionState.Ready, 5f);

            yield return DropRingOnPeg();
            yield return WaitForRound(2, 10f);

            Assert.AreEqual(2, director.CurrentRound);
            Assert.AreEqual(1, director.TimeAttack.RoundsCleared);
            Assert.Greater(director.Score, 0);
            Assert.IsFalse(runEnded);
        }

        [UnityTest]
        public IEnumerator Wasting_throws_empties_the_clock_and_ends_the_run()
        {
            // A miss costs three seconds and the clock is thirty, so ten wasted throws
            // cannot survive even before the drain is counted. Driven by throws rather
            // than by waiting out the clock because thirty seconds of pinned batch mode
            // frames is eighteen hundred of them.
            director.StartTimeAttack();
            yield return WaitForState(SessionState.Ready, 5f);

            // Stopped on the clock rather than on runEnded: the run is decided the
            // moment the clock empties, but RunEnded only fires after the outro, and
            // asking for another armed ring in that window waits for a Ready that is
            // never coming.
            for (int i = 0; i < 14 && !runEnded && !director.TimeAttack.IsOver; i++)
            {
                yield return ThrowAway();
            }

            yield return WaitForRunEnd(8f);

            Assert.IsTrue(runEnded, "An empty clock has to end the run.");
            Assert.AreEqual(0f, director.TimeAttack.Remaining, 0.001f);
            Assert.AreEqual(0, lastResult.RoundsCleared, "No round was ever cleared.");
            Assert.AreEqual(SessionState.Finished, director.State);
        }

        /// <summary>
        /// A run has no ring budget to cap how many throws it takes, so the pool has to
        /// keep handing rings out indefinitely. The failure this guards against is
        /// silent and total: an exhausted pool logs one error and then arms nothing
        /// ever again, which looks like the game freezing rather than running out of
        /// anything.
        /// </summary>
        [UnityTest]
        public IEnumerator A_long_run_never_exhausts_the_ring_pool()
        {
            RingPool pool = Object.FindFirstObjectByType<RingPool>();
            Assert.IsNotNull(pool);

            director.StartEndless();
            yield return WaitForState(SessionState.Ready, 5f);

            // More throws than the pool has rings, without ever losing a life.
            for (int i = 0; i < pool.Capacity + 3; i++)
            {
                Assert.IsNotNull(director.ActiveRing, $"The pool ran dry on throw {i + 1}.");

                yield return DropRingOnPeg();
                yield return WaitForState(SessionState.Ready, 10f);
            }

            Assert.IsFalse(runEnded, "Clean throws must not end the run.");
        }

        /// <summary>
        /// The owner's report: the clock runs out while a ring is still in the hand, an
        /// advert buys more time, and a ring hangs in front of the camera for good, in
        /// the way of every throw. Ending the run locked the input but left the armed
        /// ring where it was, and continuing armed a second one on top of it.
        /// </summary>
        [UnityTest]
        public IEnumerator Continuing_after_the_clock_ran_out_unthrown_leaves_one_ring_in_hand()
        {
            RingPool pool = Object.FindFirstObjectByType<RingPool>();
            Assert.IsNotNull(pool);

            director.StartTimeAttack();
            yield return WaitForState(SessionState.Ready, 5f);

            // The player never throws: the clock just runs out under the ring in hand.
            while (!director.TimeAttack.IsOver)
            {
                director.TimeAttack.Drain(1f);
            }

            yield return WaitForRunEnd(5f);

            director.ContinueRun();
            yield return WaitForState(SessionState.Ready, 5f);

            int inHand = 0;

            foreach (RingInstance ring in pool.All)
            {
                if (ring.State == RingState.Armed)
                {
                    inHand++;
                }
            }

            Assert.AreEqual(1, inHand, "A ring from before the continue is still hanging at the throw origin.");
        }

        /// <summary>
        /// Moves the armed ring above a peg and lets it fall down the axis. The
        /// ballistic path a player takes is the throw solver's job and has its own
        /// tests; what matters here is that the loop reacts to a capture.
        /// </summary>
        private IEnumerator DropRingOnPeg()
        {
            PegTarget peg = null;

            for (int i = 0; i < director.Board.ActivePegs.Count; i++)
            {
                if (director.Board.ActivePegs[i].IsAvailable)
                {
                    peg = director.Board.ActivePegs[i];
                    break;
                }
            }

            Assert.IsNotNull(peg, "The board has no peg available to aim at.");

            RingInstance ring = director.ActiveRing;
            Assert.IsNotNull(ring, "Nothing was armed to drop.");

            ring.Arm(peg.TopPosition + Vector3.up * 0.55f, Quaternion.identity);
            director.Throwing.LaunchArmedRing(new Vector3(0f, -0.5f, 0f), Vector3.zero, 0.5f);

            yield return null;
        }

        /// <summary>Throws backwards over the player's shoulder, which cannot reach anything.</summary>
        private IEnumerator ThrowAway()
        {
            yield return WaitForState(SessionState.Ready, 6f);

            director.Throwing.LaunchArmedRing(new Vector3(0f, 3f, -4f), Vector3.zero, 0.5f);

            yield return WaitForResolution(9f);
        }

        /// <summary>Rings every peg on the current board.</summary>
        private IEnumerator ClearCurrentRound(float timeout)
        {
            float deadline = Time.time + timeout;

            while (director.Board.PegsRemaining > 0 && !runEnded
                   && Time.time < deadline)
            {
                yield return WaitForState(SessionState.Ready, 8f);
                yield return DropRingOnPeg();
                yield return WaitForResolution(9f);
            }

            Assert.AreEqual(0, director.Board.PegsRemaining, "The board was never cleared.");
        }

        private IEnumerator WaitForState(SessionState state, float timeout)
        {
            float deadline = Time.time + timeout;

            while (director.State != state && Time.time < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(state, director.State, $"Timed out waiting for {state}.");
        }

        private IEnumerator WaitForResolution(float timeout)
        {
            float deadline = Time.time + timeout;

            while (director.State == SessionState.Flying && Time.time < deadline)
            {
                yield return null;
            }

            Assert.AreNotEqual(SessionState.Flying, director.State, "The throw never resolved.");
        }

        private IEnumerator WaitForRound(int round, float timeout)
        {
            float deadline = Time.time + timeout;

            // The director's round, not the endless bookkeeping's: time attack keeps
            // its own count, and reading the wrong one reports round zero forever.
            while (director.CurrentRound < round && !runEnded
                   && Time.time < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(round, director.CurrentRound, $"Never reached round {round}.");
        }

        private IEnumerator WaitForRunEnd(float timeout)
        {
            float deadline = Time.time + timeout;

            while (!runEnded && Time.time < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(runEnded, "The run never ended.");
        }
    }
}
