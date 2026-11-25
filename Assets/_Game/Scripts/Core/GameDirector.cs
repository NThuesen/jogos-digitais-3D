using System.Collections.Generic;
using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Gameplay;
using SlipperyZlope.RingToss.Save;
using UnityEngine;

namespace SlipperyZlope.RingToss.Core
{
    public enum SessionState
    {
        /// <summary>Nothing running: menus are up.</summary>
        Idle,

        /// <summary>Level laid out, camera settling, input still locked.</summary>
        Intro,

        /// <summary>Ring armed, waiting for the player.</summary>
        Ready,

        /// <summary>Ring in the air.</summary>
        Flying,

        /// <summary>Throw finished, feedback playing, next ring not armed yet.</summary>
        Resolving,

        /// <summary>Level over, result screen owns the flow.</summary>
        Finished
    }

    /// <summary>
    /// The rules of the game: session flow, the throw state machine, scoring and combo.
    ///
    /// A plain enum and a switch, not a state machine framework. The whole machine is
    /// six states and fits on a screen, and anything more would be scaffolding around
    /// a switch statement.
    ///
    /// Both modes run through this one machine. They diverge at exactly two points —
    /// what a failed throw costs, and what happens once a board is cleared — and
    /// everything else is shared. Splitting them into two directors would have meant
    /// two copies of the flight resolution, which is the part that took the longest to
    /// get right and the part that must not drift.
    ///
    /// This class never animates anything. It raises events and the juice, audio and
    /// UI layers decide what that looks like.
    /// </summary>
    [DefaultExecutionOrder(0)]
    public sealed class GameDirector : MonoBehaviour
    {
        /// <summary>Work that has to happen after a delay, without a coroutine per throw.</summary>
        private enum PendingStep
        {
            None,
            ArmRing,
            EndLevelCompleted,
            EndLevelFailed,
            NextEndlessRound,
            EndEndlessRun
        }

        /// <summary>A retired ring waiting for its exit animation to finish.</summary>
        private struct PendingRelease
        {
            public RingInstance Ring;
            public float Timer;
        }

        [Header("Configuration")]
        [SerializeField] private LevelLibrary levelLibrary;
        [SerializeField] private PhysicsConfig physicsConfig;
        [SerializeField] private JuiceConfig juiceConfig;

        /// <summary>The feedback tuning, for scenery that animates with play.</summary>
        public JuiceConfig Juice => juiceConfig;

        [Header("Systems")]
        [SerializeField] private RingPool ringPool;
        [SerializeField] private LevelRunner levelRunner;
        [SerializeField] private ThrowController throwController;
        [SerializeField] private ProgressStore progress;

        [Header("Scene")]
        [SerializeField] private Transform throwOrigin;

        [Header("Scoring")]
        [Tooltip("Extra multiplier added to the base peg score per consecutive hit.")]
        [SerializeField] private float comboBonusPerHit = 0.25f;

        [SerializeField] private float maxComboMultiplier = 3f;

        [Tooltip("Multiplier for landing the ring without ever touching the peg.")]
        [SerializeField] private float perfectMultiplier = 1.5f;

        [Header("Endless")]
        [Tooltip("Score for clearing a round, before the per-round growth below.")]
        [SerializeField] private int roundClearBonus = 120;

        [SerializeField] private int roundClearBonusPerRound = 25;

        [Tooltip("Paid when a run of perfects completes while the player is already " +
                 "at full lives, so a clean streak is never worth nothing.")]
        [SerializeField] private int fullHealthStreakBonus = 500;

        [Tooltip("Pause between clearing a round and the next board appearing.")]
        [SerializeField] private float roundBreakDuration = 1.1f;

        private readonly EndlessRules endless = new EndlessRules();
        private readonly TimeAttackRules timeAttack = new TimeAttackRules();
        private readonly List<PendingRelease> pendingReleases = new List<PendingRelease>(8);

        private SessionState state = SessionState.Idle;
        private GameMode mode = GameMode.Levels;
        private RingInstance activeRing;
        private LevelConfig level;
        private LevelConfig endlessLevel;
        private int levelIndex = -1;
        private int endlessSeed;
        private float flightTime;
        private PendingStep pending;
        private float pendingTimer;

        public SessionState State => state;

        public GameMode Mode => mode;

        public int LevelIndex => levelIndex;

        public LevelConfig CurrentLevel => level;

        public int Score { get; private set; }

        public int Combo { get; private set; }

        public int RingsRemaining { get; private set; }

        /// <summary>Rounds already paid for, so a continued run is not paid for twice.</summary>
        private int roundsPaidFor;

        public int PerfectCount { get; private set; }

        /// <summary>The endless run's bookkeeping. Meaningless in Levels mode.</summary>
        public EndlessRules Endless => endless;

        /// <summary>The time attack run's clock. Meaningless in any other mode.</summary>
        public TimeAttackRules TimeAttack => timeAttack;

        /// <summary>
        /// True for the two modes that play generated boards.
        ///
        /// They share the generator, the round bookkeeping, the round-clear bonus and
        /// the results screen; they differ only in what runs out. Asking this rather
        /// than listing both modes at each site is what keeps a third one from having
        /// to be added in eleven places.
        /// </summary>
        public bool IsGeneratedRun => mode == GameMode.Endless || mode == GameMode.TimeAttack;

        /// <summary>The round being played, whichever generated mode is running.</summary>
        public int CurrentRound => mode == GameMode.TimeAttack ? timeAttack.Round : endless.Round;

        /// <summary>Whether the running mode's resource has run out.</summary>
        private bool RunIsOver =>
            mode == GameMode.TimeAttack ? timeAttack.IsOver : endless.IsOver;

        /// <summary>The ring currently armed or in flight, or null between throws.</summary>
        public RingInstance ActiveRing => activeRing;

        public ThrowController Throwing => throwController;

        public LevelRunner Board => levelRunner;

        public int LevelCount => levelLibrary == null ? 0 : levelLibrary.Count;

        public ProgressStore Progress => progress;

        public LevelLibrary Library => levelLibrary;

        /// <summary>Lays out an authored level and starts its intro. Safe to call at any time.</summary>
        public void StartLevel(int index)
        {
            LevelConfig config = levelLibrary == null ? null : levelLibrary.Get(index);

            if (config == null)
            {
                Debug.LogError($"[GameDirector] No level at index {index}.", this);
                return;
            }

            AbortRun();

            mode = GameMode.Levels;
            levelIndex = index;
            Score = 0;
            Combo = 0;
            PerfectCount = 0;

            GameEvents.RaiseSessionStarted(mode);
            BeginBoard(config, config.RingCount);
        }

        public void RestartLevel()
        {
            if (mode == GameMode.TimeAttack)
            {
                StartTimeAttack();
                return;
            }

            if (mode == GameMode.Endless)
            {
                StartEndless();
                return;
            }

            if (levelIndex >= 0)
            {
                StartLevel(levelIndex);
            }
        }

        /// <summary>Moves to the next level, or back to idle when the last one is cleared.</summary>
        public bool StartNextLevel()
        {
            int next = levelIndex + 1;

            if (levelLibrary == null || next >= levelLibrary.Count)
            {
                ReturnToIdle();
                return false;
            }

            StartLevel(next);
            return true;
        }

        /// <summary>Starts a fresh endless run at round one with a full set of lives.</summary>
        public void StartEndless()
        {
            roundsPaidFor = 0;
            AbortRun();

            mode = GameMode.Endless;
            levelIndex = -1;
            Score = 0;
            Combo = 0;
            PerfectCount = 0;

            // Reseeded per run, so two runs that reach the same round meet the same
            // difficulty laid out differently. A fixed seed would make the mode a
            // memory test after one evening.
            endlessSeed = Random.Range(1, int.MaxValue - 1);
            endless.Reset();

            GameEvents.RaiseSessionStarted(mode);
            GameEvents.RaiseLivesChanged(endless.Lives, EndlessRules.MaxLives);
            GameEvents.RaisePerfectStreakChanged(0, EndlessRules.PerfectsPerLife);

            BeginGeneratedRound();
        }

        /// <summary>
        /// Starts a fresh time attack run: the endless board, against a clock.
        ///
        /// Deliberately the same seed draw, the same generator and the same ramp as
        /// endless. The mode is a different question asked of the same board, and
        /// giving it its own generator would have made it a different game that
        /// happened to look similar.
        /// </summary>
        public void StartTimeAttack()
        {
            roundsPaidFor = 0;
            AbortRun();

            mode = GameMode.TimeAttack;
            levelIndex = -1;
            Score = 0;
            Combo = 0;
            PerfectCount = 0;

            endlessSeed = Random.Range(1, int.MaxValue - 1);
            timeAttack.Reset();

            GameEvents.RaiseSessionStarted(mode);
            GameEvents.RaiseTimeChanged(timeAttack.Remaining, TimeAttackRules.MaxSeconds);

            BeginGeneratedRound();
        }

        private void BeginGeneratedRound()
        {
            AbortRun();

            if (endlessLevel == null)
            {
                // One instance reconfigured per round rather than one asset per round.
                // DontSave keeps it out of the project and out of any scene save.
                endlessLevel = ScriptableObject.CreateInstance<LevelConfig>();
                endlessLevel.name = "Endless Round (runtime)";
                endlessLevel.hideFlags = HideFlags.DontSave;
            }

            if (mode == GameMode.TimeAttack)
            {
                timeAttack.BeginRound();
            }
            else
            {
                endless.BeginRound();
            }

            int round = CurrentRound;

            StageTheme theme = levelLibrary == null
                ? null
                : levelLibrary.GetTheme(EndlessRoundBuilder.StageFor(round));

            EndlessRoundBuilder.Build(endlessLevel, round, endlessSeed, theme, physicsConfig);

            BeginBoard(endlessLevel, endlessLevel.RingCount);

            GameEvents.RaiseRoundStarted(round);
        }

        /// <summary>
        /// The part of starting a board that both modes share: lay out the pegs,
        /// publish the opening numbers, and hand over to the intro.
        /// </summary>
        private void BeginBoard(LevelConfig config, int rings)
        {
            level = config;
            RingsRemaining = rings;

            levelRunner.StartLevel(config);

            GameEvents.RaiseLevelStarted(levelIndex);
            GameEvents.RaiseScoreChanged(Score, 0);
            GameEvents.RaiseComboChanged(Combo);
            GameEvents.RaiseRingsRemainingChanged(RingsRemaining);

            state = SessionState.Intro;
            Schedule(PendingStep.ArmRing, juiceConfig == null ? 0.9f : juiceConfig.LevelIntroDuration);
        }

        public void ReturnToIdle()
        {
            AbortRun();
            mode = GameMode.Levels;
            state = SessionState.Idle;
            levelRunner.ClearBoard();

            GameEvents.RaiseSessionClosed();
        }

        /// <summary>Stops whatever was running and clears the board of rings.</summary>
        private void AbortRun()
        {
            pending = PendingStep.None;
            pendingTimer = 0f;
            activeRing = null;
            flightTime = 0f;
            pendingReleases.Clear();

            throwController.Lock();
            ringPool.ReleaseAll();
        }

        private void OnEnable()
        {
            GameEvents.Thrown += OnThrown;
        }

        private void OnDisable()
        {
            GameEvents.Thrown -= OnThrown;
        }

        private void OnDestroy()
        {
            if (endlessLevel != null)
            {
                Destroy(endlessLevel);
                endlessLevel = null;
            }
        }

        private void Update()
        {
            TickPending();
            TickReleases();

            if (state == SessionState.Intro || state == SessionState.Ready
                || state == SessionState.Flying || state == SessionState.Resolving)
            {
                levelRunner.TickPegs(Time.deltaTime);
            }

            // The clock deliberately does not include the intro. The board is still
            // being laid out and input is locked, so those are seconds the player
            // could not have used — charging for a pause the game imposed is the one
            // way a timed mode can feel unfair before it has even started. The pegs
            // above do keep moving through it, because a board that snapped into
            // motion on the first throw would be worse.
            if (state == SessionState.Ready || state == SessionState.Flying
                || state == SessionState.Resolving)
            {
                TickClock(Time.deltaTime);
            }
        }

        /// <summary>
        /// Runs the time attack clock down, and ends the run when it empties.
        ///
        /// Ticked from the same four states the pegs are, which is what makes the
        /// clock stop for the intro of the next round and for the results screen. A
        /// clock that ran through the round break would charge the player for a pause
        /// the game imposed on them.
        ///
        /// The run is ended here rather than left to the next throw's resolution. In
        /// every other mode the resource can only run out *because* of a throw, so
        /// checking after one is enough; time runs out on its own, and a player who
        /// simply stops throwing has to lose.
        /// </summary>
        private void TickClock(float deltaTime)
        {
            if (mode != GameMode.TimeAttack)
            {
                return;
            }

            timeAttack.Drain(deltaTime);
            GameEvents.RaiseTimeChanged(timeAttack.Remaining, TimeAttackRules.MaxSeconds);

            if (!timeAttack.IsOver || pending == PendingStep.EndEndlessRun)
            {
                return;
            }

            // Whatever was queued is abandoned: there is no next ring and no next
            // round to go to. A ring already in the air is left to land, because
            // snatching it away at the buzzer reads as the game cheating.
            if (state != SessionState.Flying)
            {
                Schedule(PendingStep.EndEndlessRun,
                    juiceConfig == null ? 0.7f : juiceConfig.LevelOutroDelay);
            }
        }

        /// <summary>
        /// Returns retired rings to the pool once their exit has played.
        ///
        /// Levels mode got away without this: a level's throws are capped by its ring
        /// budget and the whole pool is reclaimed between levels. An endless run has no
        /// such cap, and while the current lives count keeps a single round's throws
        /// inside the pool's capacity anyway, that is an accident of two numbers rather
        /// than a property of the design. Recycling each ring as it retires makes it a
        /// property.
        /// </summary>
        private void TickReleases()
        {
            for (int i = pendingReleases.Count - 1; i >= 0; i--)
            {
                PendingRelease entry = pendingReleases[i];
                entry.Timer -= Time.deltaTime;

                if (entry.Timer > 0f)
                {
                    pendingReleases[i] = entry;
                    continue;
                }

                pendingReleases.RemoveAt(i);
                ringPool.Release(entry.Ring);
            }
        }

        private void QueueRelease(RingInstance ring)
        {
            if (ring == null)
            {
                return;
            }

            float linger = juiceConfig == null ? 0.28f : juiceConfig.MissLingerDuration;
            float fade = juiceConfig == null ? 0.3f : juiceConfig.MissFadeDuration;

            pendingReleases.Add(new PendingRelease
            {
                Ring = ring,
                Timer = linger + fade + 0.15f
            });
        }

        private void FixedUpdate()
        {
            // The ring's own state is checked as well as the session's. A ring that has
            // already been captured or retired is not in flight, and resolving the same
            // throw twice is what turns one failed throw into an endless run of them.
            if (state != SessionState.Flying || activeRing == null || !activeRing.IsFlying)
            {
                return;
            }

            flightTime += Time.fixedDeltaTime;

            levelRunner.ApplyFlightForces(activeRing, flightTime);

            PegTarget peg = levelRunner.EvaluateCapture(activeRing);

            if (peg != null)
            {
                ResolveCapture(peg);
                return;
            }

            if (activeRing.Centre.y < physicsConfig.OutOfBoundsHeight)
            {
                ResolveFailure();
                return;
            }

            bool stopped = flightTime >= physicsConfig.MinimumFlightTime
                           && activeRing.Controller.IsAtRest(
                               physicsConfig.RestLinearThreshold,
                               physicsConfig.RestAngularThreshold);

            if (stopped || flightTime >= physicsConfig.MaximumFlightTime)
            {
                ResolveFailure();
            }
        }

        private void OnThrown(ThrowLaunched payload)
        {
            if (state != SessionState.Ready)
            {
                return;
            }

            state = SessionState.Flying;
            flightTime = 0f;

            RingsRemaining = Mathf.Max(0, RingsRemaining - 1);
            GameEvents.RaiseRingsRemainingChanged(RingsRemaining);

            // Counted so the game can stop explaining the gesture to somebody who has
            // clearly worked it out.
            progress?.RecordThrow();

            levelRunner.BeginThrow();
        }

        private void ResolveCapture(PegTarget peg)
        {
            RingInstance caught = activeRing;
            bool isPerfect = !caught.TouchedPeg;

            // The throw is given up before any of it is announced.
            //
            // Everything below the handover is presentation, and presentation is
            // foreign code: the juice, audio, UI and cosmetics layers all listen here,
            // and so does the platform, through the haptics. If any one of them throws
            // while the director still holds a ring in flight, the next FixedUpdate
            // finds that same ring still flying and resolves it again — and again,
            // every physics step, for as long as the app is open. The player watches
            // their lives drain in a single frame and then sits in a fail loop that
            // never reaches the results screen. Releasing the ring and committing the
            // state first makes that impossible: a broken effect can cost its own
            // effect and nothing else.
            activeRing = null;
            state = SessionState.Resolving;

            Combo++;

            float multiplier = Mathf.Min(1f + (Combo - 1) * comboBonusPerHit, maxComboMultiplier);

            if (isPerfect)
            {
                multiplier *= perfectMultiplier;
                PerfectCount++;
            }

            int points = Mathf.RoundToInt(peg.Points * multiplier);
            Score += points;

            caught.BeginCapture(peg);

            try
            {
                levelRunner.ClaimPeg(peg);

                GameEvents.RaiseRinger(new RingerCaught(peg, caught, isPerfect, points, Combo));
                GameEvents.RaiseComboChanged(Combo);
                GameEvents.RaiseScoreChanged(Score, points);
                GameEvents.RaiseResolved(new ThrowResolved(
                    isPerfect ? ThrowOutcome.PerfectRinger : ThrowOutcome.Ringer,
                    caught,
                    caught.Centre,
                    points,
                    Combo));

                if (isPerfect)
                {
                    // Paid in every mode: a perfect is the skill the game is about, and
                    // it should always be worth reaching for.
                    AwardTickets(TicketRules.ForPerfect());

                    if (mode == GameMode.Endless)
                    {
                        AwardPerfect();
                    }
                }

                if (mode == GameMode.TimeAttack)
                {
                    // Both catches pay, and the perfect pays double. The mode is about
                    // speed, but it must not be *only* about speed — a player who can
                    // only flail at the board has to run out of clock faster than one
                    // who is dropping rings cleanly.
                    // Measured across the call rather than read off the constant: the
                    // clock is capped, so a catch made at 44 seconds is worth less than
                    // the same catch made at 20, and the feedback should say so.
                    float before = timeAttack.Remaining;

                    if (isPerfect)
                    {
                        timeAttack.RegisterPerfect();
                    }
                    else
                    {
                        timeAttack.RegisterRinger();
                    }

                    GameEvents.RaiseTimeChanged(timeAttack.Remaining, TimeAttackRules.MaxSeconds);
                    GameEvents.RaiseTimeAwarded(timeAttack.Remaining - before);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
            }

            AdvanceAfterThrow();
        }

        /// <summary>Banks a perfect towards the next life, and pays out a completed run of them.</summary>
        private void AwardPerfect()
        {
            PerfectReward reward = endless.RegisterPerfect();

            GameEvents.RaisePerfectStreakChanged(endless.PerfectStreak, EndlessRules.PerfectsPerLife);

            if (!reward.StreakCompleted)
            {
                return;
            }

            if (reward.LifeGranted)
            {
                GameEvents.RaiseLivesChanged(endless.Lives, EndlessRules.MaxLives);
            }
            else
            {
                // Already at full lives. The streak still has to be worth chasing.
                Score += fullHealthStreakBonus;
                GameEvents.RaiseScoreChanged(Score, fullHealthStreakBonus);
            }

            GameEvents.RaiseStreakRewarded(reward.LifeGranted);
        }

        private void ResolveFailure()
        {
            RingInstance failed = activeRing;
            ThrowOutcome outcome = failed.TouchedPeg ? ThrowOutcome.Bounce : ThrowOutcome.Miss;

            // Handed over before anything is announced, for the reason set out in
            // ResolveCapture. This is the path that loops when it goes wrong, so it is
            // the path that most needs the throw to be finished before the news of it
            // goes out.
            activeRing = null;
            state = SessionState.Resolving;

            Combo = 0;
            levelRunner.RegisterMiss();

            failed.BeginRetire();
            QueueRelease(failed);

            // The life is spent outside the announcement. What a miss costs is a rule,
            // and a rule must not depend on whether the HUD that draws it succeeded.
            if (mode == GameMode.Endless)
            {
                endless.RegisterMiss();
            }
            else if (mode == GameMode.TimeAttack)
            {
                timeAttack.RegisterMiss();
            }

            try
            {
                if (mode == GameMode.Endless)
                {
                    GameEvents.RaiseLivesChanged(endless.Lives, EndlessRules.MaxLives);
                    GameEvents.RaisePerfectStreakChanged(0, EndlessRules.PerfectsPerLife);
                }
                else if (mode == GameMode.TimeAttack)
                {
                    GameEvents.RaiseTimeChanged(timeAttack.Remaining, TimeAttackRules.MaxSeconds);
                    GameEvents.RaiseTimeAwarded(-TimeAttackRules.MissPenalty);
                }

                GameEvents.RaiseComboChanged(0);
                GameEvents.RaiseResolved(new ThrowResolved(outcome, failed, failed.Centre, 0, 0));
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
            }

            AdvanceAfterThrow();
        }

        /// <summary>
        /// Decides what happens after a throw: another ring, a cleared board, or a
        /// finished run. Called from both resolution paths so the two can never drift.
        /// </summary>
        private void AdvanceAfterThrow()
        {
            state = SessionState.Resolving;

            float outroDelay = juiceConfig == null ? 0.7f : juiceConfig.LevelOutroDelay;
            bool boardCleared = levelRunner.PegsRemaining <= 0;

            if (IsGeneratedRun)
            {
                // Order matters. A last life — or a last second — spent on the throw
                // that clears the board is still a cleared board: the player earned the
                // round, and taking it away because the same throw was their last would
                // read as a cheat.
                if (boardCleared)
                {
                    AwardRoundClear();
                    Schedule(PendingStep.NextEndlessRound, outroDelay + roundBreakDuration);
                    return;
                }

                if (RunIsOver)
                {
                    Schedule(PendingStep.EndEndlessRun, outroDelay);
                    return;
                }

                Schedule(PendingStep.ArmRing, juiceConfig == null ? 0.22f : juiceConfig.ResetDelay);
                return;
            }

            if (boardCleared)
            {
                Schedule(PendingStep.EndLevelCompleted, outroDelay);
                return;
            }

            if (RingsRemaining <= 0)
            {
                Schedule(PendingStep.EndLevelFailed, outroDelay);
                return;
            }

            // Fewer rings left than pegs left is already a loss: a throw claims at most
            // one peg, so the board cannot be cleared however well the rest is played.
            // Handing the player three more throws at a level the game has already
            // decided against is worse than losing — it reads as the game not noticing.
            if (RingsRemaining < levelRunner.PegsRemaining)
            {
                Schedule(PendingStep.EndLevelFailed, outroDelay);
                return;
            }

            Schedule(PendingStep.ArmRing, juiceConfig == null ? 0.22f : juiceConfig.ResetDelay);
        }

        private void AwardRoundClear()
        {
            if (mode == GameMode.TimeAttack)
            {
                // Clearing a board is the only thing in this mode that buys a
                // meaningful amount of clock back, which is what makes finishing a
                // round the goal rather than farming easy pegs on it.
                float before = timeAttack.Remaining;

                timeAttack.CompleteRound();

                GameEvents.RaiseTimeChanged(timeAttack.Remaining, TimeAttackRules.MaxSeconds);
                GameEvents.RaiseTimeAwarded(timeAttack.Remaining - before);
            }
            else
            {
                endless.CompleteRound();
            }

            int round = CurrentRound;
            int bonus = roundClearBonus + roundClearBonusPerRound * round;
            Score += bonus;
            GameEvents.RaiseScoreChanged(Score, bonus);
            GameEvents.RaiseRoundCleared(round, bonus);

            AwardTickets(TicketRules.ForEndlessRound(round));
        }

        /// <summary>
        /// The one place tickets are handed out. Everything that pays the player goes
        /// through here so the balance can only move in a single, greppable spot.
        /// </summary>
        private void AwardTickets(int amount)
        {
            if (amount <= 0 || progress == null)
            {
                return;
            }

            progress.AddTickets(amount);
            GameEvents.RaiseTicketsAwarded(amount, progress.Tickets, TicketSource.Play);
        }

        private void ArmNextRing()
        {
            RingInstance ring = ringPool.Take();

            if (ring == null)
            {
                EndSession(false);
                return;
            }

            ring.SetJuiceConfig(juiceConfig);
            ring.SetScale(level.RingScale);

            StageTheme theme = level.Theme;

            if (theme != null)
            {
                ring.SetTint(theme.RingColor(level.RingCount - RingsRemaining));
            }

            activeRing = ring;
            state = SessionState.Ready;
            throwController.ArmRing(ring);
        }

        private void EndSession(bool completed)
        {
            if (IsGeneratedRun)
            {
                EndRun();
                return;
            }

            EndLevel(completed);
        }

        private void EndLevel(bool completed)
        {
            state = SessionState.Finished;
            throwController.Lock();

            int ringsLeft = RingsRemaining;
            int stars = completed && level != null ? level.StarsFor(ringsLeft) : 0;
            bool isNewBest = false;

            if (completed && progress != null)
            {
                // Asked before the record is written, because writing it is what marks
                // the level as cleared.
                bool firstClear = !progress.HasCleared(levelIndex);

                isNewBest = progress.RecordCompletion(levelIndex, stars, Score, LevelCount);
                AwardTickets(TicketRules.ForLevelClear(stars, firstClear));
            }

            GameEvents.RaiseLevelEnded(new LevelResult(
                levelIndex, completed, Score, stars, ringsLeft, PerfectCount, isNewBest));
        }

        /// <summary>Ends a generated run — endless or time attack — and banks it.</summary>
        private void EndRun()
        {
            state = SessionState.Finished;
            throwController.Lock();

            // A run can end with a ring still in the hand: the clock runs out on its own,
            // whether the player throws or not. Locking the input does not take that ring
            // away, and a continue arms a fresh one, so a ring left here hangs at the
            // throw origin for good — in front of the camera and in the way of every throw.
            if (activeRing != null && activeRing.State == RingState.Armed)
            {
                ringPool.Release(activeRing);
                activeRing = null;
            }

            bool timed = mode == GameMode.TimeAttack;
            int roundsCleared = timed ? timeAttack.RoundsCleared : endless.RoundsCleared;
            int round = CurrentRound;
            int perfects = timed ? timeAttack.TotalPerfects : endless.TotalPerfects;

            bool isNewBest = false;

            if (progress != null)
            {
                isNewBest = timed
                    ? progress.RecordTimeAttackRun(Score, roundsCleared)
                    : progress.RecordEndlessRun(Score, roundsCleared);

                // The delta, not the total. A continued run reaches this twice, and
                // paying the full amount both times would make watching an advert the
                // best-paying move in the game.
                AwardTickets(TicketRules.ForEndlessRun(roundsCleared)
                             - TicketRules.ForEndlessRun(roundsPaidFor));
                roundsPaidFor = Mathf.Max(roundsPaidFor, roundsCleared);
            }

            int best = progress == null
                ? Score
                : timed ? progress.BestTimeAttackScore : progress.BestEndlessScore;

            GameEvents.RaiseRunEnded(new EndlessResult(
                roundsCleared, round, Score, perfects, best, isNewBest));
        }

        /// <summary>
        /// Whether the run that just ended can be picked up again. Generated modes only:
        /// a level has a ring budget and a retry, which is a different offer.
        /// </summary>
        public bool CanContinueRun => IsGeneratedRun && state == SessionState.Finished;

        /// <summary>
        /// Resumes a finished run, paid for elsewhere — the caller grants this only
        /// after a rewarded advert has actually been watched to the end.
        /// </summary>
        ///
        /// <remarks>
        /// This is possible at all because ending a run is a small thing: it locks
        /// input, records the score and announces itself. The board, its pegs and the
        /// round counter are all still standing, so continuing is a life or a clock
        /// back and a ring in the hand — not a rebuild.
        /// </remarks>
        public void ContinueRun()
        {
            if (!CanContinueRun)
            {
                return;
            }

            if (mode == GameMode.TimeAttack)
            {
                timeAttack.Extend(TimeAttackRules.ContinueSeconds);
                GameEvents.RaiseTimeChanged(timeAttack.Remaining, TimeAttackRules.MaxSeconds);
            }
            else
            {
                endless.GrantLife();
                GameEvents.RaiseLivesChanged(endless.Lives, EndlessRules.MaxLives);
            }

            state = SessionState.Resolving;
            Schedule(PendingStep.ArmRing, juiceConfig == null ? 0.22f : juiceConfig.ResetDelay);
        }

        private void Schedule(PendingStep step, float delay)
        {
            pending = step;
            pendingTimer = Mathf.Max(0f, delay);
        }

        private void TickPending()
        {
            if (pending == PendingStep.None)
            {
                return;
            }

            pendingTimer -= Time.deltaTime;

            if (pendingTimer > 0f)
            {
                return;
            }

            PendingStep step = pending;
            pending = PendingStep.None;

            switch (step)
            {
                case PendingStep.ArmRing:
                    ArmNextRing();
                    break;
                case PendingStep.EndLevelCompleted:
                    EndLevel(true);
                    break;
                case PendingStep.EndLevelFailed:
                    EndLevel(false);
                    break;
                case PendingStep.NextEndlessRound:
                    AdvanceGeneratedRound();
                    break;
                case PendingStep.EndEndlessRun:
                    EndRun();
                    break;
            }
        }

        private void AdvanceGeneratedRound()
        {
            // A run that cleared its last board on its last life — or its last second —
            // still ends here, but it ends having banked the round.
            if (RunIsOver)
            {
                EndRun();
                return;
            }

            BeginGeneratedRound();
        }

        public void Configure(
            LevelLibrary library,
            PhysicsConfig physics,
            JuiceConfig juice,
            RingPool pool,
            LevelRunner runner,
            ThrowController throwing,
            ProgressStore progressStore,
            Transform origin)
        {
            levelLibrary = library;
            physicsConfig = physics;
            juiceConfig = juice;
            ringPool = pool;
            levelRunner = runner;
            throwController = throwing;
            progress = progressStore;
            throwOrigin = origin;
        }
    }
}
