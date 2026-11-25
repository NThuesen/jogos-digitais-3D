using System;
using SlipperyZlope.RingToss.Gameplay;
using UnityEngine;

namespace SlipperyZlope.RingToss.Core
{
    /// <summary>Payload for a released throw.</summary>
    public readonly struct ThrowLaunched
    {
        public readonly Vector3 Origin;
        public readonly Vector3 Velocity;

        /// <summary>Drag length as 0..1 of the configured maximum.</summary>
        public readonly float NormalisedForce;

        public ThrowLaunched(Vector3 origin, Vector3 velocity, float normalisedForce)
        {
            Origin = origin;
            Velocity = velocity;
            NormalisedForce = normalisedForce;
        }
    }

    /// <summary>
    /// Payload for a drag in progress.
    ///
    /// Carries the direction as well as the power because the two are independent
    /// axes of the same gesture, and the presentation has to show both: a ring that
    /// only pulls straight back tells the player nothing about where they are aiming.
    /// </summary>
    public readonly struct AimState
    {
        /// <summary>Drag length along the aim axis, 0..1 of the configured maximum.</summary>
        public readonly float NormalisedForce;

        /// <summary>Sideways drag, -1 hard left to +1 hard right.</summary>
        public readonly float NormalisedYaw;

        /// <summary>Ground distance this throw is currently aimed to land at, in metres.</summary>
        public readonly float Range;

        public AimState(float normalisedForce, float normalisedYaw, float range)
        {
            NormalisedForce = normalisedForce;
            NormalisedYaw = normalisedYaw;
            Range = range;
        }
    }

    /// <summary>Payload for any physical contact worth reacting to.</summary>
    public readonly struct RingImpact
    {
        public readonly Vector3 Point;
        public readonly Vector3 Normal;

        /// <summary>Relative speed at the contact, used to scale the reaction.</summary>
        public readonly float Speed;

        /// <summary>The peg that was hit, or null when the ring hit scenery.</summary>
        public readonly PegTarget Peg;

        public readonly RingInstance Ring;

        public bool HitPeg => Peg != null;

        public RingImpact(Vector3 point, Vector3 normal, float speed, PegTarget peg, RingInstance ring)
        {
            Point = point;
            Normal = normal;
            Speed = speed;
            Peg = peg;
            Ring = ring;
        }
    }

    /// <summary>Payload for the moment a ring is captured by a peg.</summary>
    public readonly struct RingerCaught
    {
        public readonly PegTarget Peg;
        public readonly RingInstance Ring;
        public readonly bool IsPerfect;
        public readonly int Points;
        public readonly int Combo;

        public RingerCaught(PegTarget peg, RingInstance ring, bool isPerfect, int points, int combo)
        {
            Peg = peg;
            Ring = ring;
            IsPerfect = isPerfect;
            Points = points;
            Combo = combo;
        }
    }

    /// <summary>Payload for the end of a throw, whatever the result was.</summary>
    public readonly struct ThrowResolved
    {
        public readonly ThrowOutcome Outcome;

        /// <summary>The ring that was thrown. The juice layer animates its exit.</summary>
        public readonly RingInstance Ring;

        public readonly Vector3 Position;
        public readonly int Points;
        public readonly int Combo;

        public ThrowResolved(ThrowOutcome outcome, RingInstance ring, Vector3 position, int points, int combo)
        {
            Outcome = outcome;
            Ring = ring;
            Position = position;
            Points = points;
            Combo = combo;
        }
    }

    /// <summary>Payload for the end of a level.</summary>
    public readonly struct LevelResult
    {
        public readonly int LevelIndex;
        public readonly bool Completed;
        public readonly int Score;
        public readonly int Stars;
        public readonly int RingsLeft;
        public readonly int PerfectCount;
        public readonly bool IsNewBest;

        public LevelResult(
            int levelIndex,
            bool completed,
            int score,
            int stars,
            int ringsLeft,
            int perfectCount,
            bool isNewBest)
        {
            LevelIndex = levelIndex;
            Completed = completed;
            Score = score;
            Stars = stars;
            RingsLeft = ringsLeft;
            PerfectCount = perfectCount;
            IsNewBest = isNewBest;
        }
    }

    /// <summary>Payload for the end of an endless run.</summary>
    public readonly struct EndlessResult
    {
        /// <summary>Rounds fully cleared. The headline number of the mode.</summary>
        public readonly int RoundsCleared;

        /// <summary>The round the run died on.</summary>
        public readonly int Round;

        public readonly int Score;
        public readonly int Perfects;
        public readonly int Best;
        public readonly bool IsNewBest;

        public EndlessResult(int roundsCleared, int round, int score, int perfects, int best, bool isNewBest)
        {
            RoundsCleared = roundsCleared;
            Round = round;
            Score = score;
            Perfects = perfects;
            Best = best;
            IsNewBest = isNewBest;
        }
    }

    /// <summary>
    /// Where a ticket award came from. The amount says how many; this says how loudly
    /// the interface celebrates, because a payout the player went out of their way for
    /// — an advert they chose, a friend they told — deserves more than a trickle does.
    /// </summary>
    public enum TicketSource
    {
        /// <summary>Earned by playing: perfects, cleared levels and rounds, finished runs.</summary>
        Play,

        /// <summary>A rewarded advert the player chose to watch.</summary>
        RewardedAd,

        /// <summary>Shared the game with a friend.</summary>
        Share,

        /// <summary>Shared while the 2x tag bought with a rewarded advert was on the button.</summary>
        BoostedShare,

        /// <summary>
        /// Tickets going the other way: spent on a skin or a trail. Not an award, so it
        /// never raises <see cref="TicketsAwarded"/> — it exists so the celebration and
        /// the mix can tell paying from being paid.
        /// </summary>
        Purchase
    }

    /// <summary>
    /// The seam between gameplay and presentation. Gameplay raises; the juice, audio
    /// and UI layers listen. Gameplay never calls animation directly, which is what
    /// makes the feedback layer replaceable without touching the rules.
    ///
    /// These are plain typed C# events, deliberately not a generic event bus: no
    /// registry, no reflection, no subscription framework. Payloads are readonly
    /// structs so raising one during gameplay allocates nothing.
    ///
    /// Every subscriber must unsubscribe in OnDisable. These are static and outlive
    /// scene loads, so a missed unsubscribe leaks a destroyed object forever.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Ring is at the throw origin and can be aimed.</summary>
        public static event Action<RingInstance> ThrowArmed;

        /// <summary>Fired every frame the player is dragging.</summary>
        public static event Action<AimState> AimChanged;

        /// <summary>Player let go without throwing.</summary>
        public static event Action AimCancelled;

        public static event Action<ThrowLaunched> Thrown;

        public static event Action<RingImpact> Impact;

        public static event Action<RingerCaught> Ringer;

        public static event Action<ThrowResolved> Resolved;

        /// <summary>Consecutive successes. Zero means the streak just broke.</summary>
        public static event Action<int> ComboChanged;

        /// <summary>Total score, then how much was just added.</summary>
        public static event Action<int, int> ScoreChanged;

        public static event Action<int> RingsRemainingChanged;

        /// <summary>A peg has been claimed; carries how many are still open.</summary>
        public static event Action<PegTarget, int> PegClaimed;

        /// <summary>
        /// A board has been laid out. Every board: a generated run raises this once per
        /// round, so it is not where anything that belongs to the whole run should reset.
        /// </summary>
        public static event Action<int> LevelStarted;

        public static event Action<LevelResult> LevelEnded;

        /// <summary>
        /// A mode has been started from scratch — a level, a run, or either one again.
        /// The place for what lasts across a run's boards to go back to zero.
        /// </summary>
        public static event Action<GameMode> SessionStarted;

        /// <summary>The player has left whatever was in play and is back at the menu.</summary>
        public static event Action SessionClosed;

        /// <summary>Endless: lives left, and the most the run can hold.</summary>
        public static event Action<int, int> LivesChanged;

        /// <summary>Endless: perfects banked towards the next life, and how many it takes.</summary>
        public static event Action<int, int> PerfectStreakChanged;

        /// <summary>
        /// Time attack: seconds left, and the most the clock can hold.
        ///
        /// Raised every frame of a run rather than only when something changes, because
        /// in this mode the clock is always changing — draining is the resting state.
        /// </summary>
        public static event Action<float, float> TimeChanged;

        /// <summary>
        /// Time attack: seconds just won or lost, signed, and only for the discrete
        /// awards — a catch, a miss, a cleared board.
        ///
        /// Separate from <see cref="TimeChanged"/>, which is state and fires every
        /// frame because draining is the resting condition of the mode. This is the
        /// *event*, and the feedback layer needs the difference: a clock that flashed
        /// green every frame it was merely running would be a strobe, and one that
        /// tried to infer an award by watching the state jump would be guessing at a
        /// threshold that the drain rate moves under it.
        /// </summary>
        public static event Action<float> TimeAwarded;

        /// <summary>
        /// The wind acting on the board right now, gust included.
        ///
        /// Raised every frame of a level that has any, because the gust is a sine wave
        /// and the whole point of drawing it is that the player can watch it change.
        /// A level with no wind never raises it at all.
        /// </summary>
        public static event Action<Vector3> WindChanged;

        /// <summary>Endless: a streak just paid out. True when it bought a life back.</summary>
        public static event Action<bool> StreakRewarded;

        /// <summary>Endless: a round's board has been laid out.</summary>
        public static event Action<int> RoundStarted;

        /// <summary>Endless: a round's last peg has been claimed. Carries the round and its bonus.</summary>
        public static event Action<int, int> RoundCleared;

        /// <summary>Endless: the run is over.</summary>
        public static event Action<EndlessResult> RunEnded;

        /// <summary>Tickets just earned, the balance afterwards, and what paid them.</summary>
        public static event Action<int, int, TicketSource> TicketsAwarded;

        /// <summary>
        /// The interface has started showing an award arrive: its source and amount.
        ///
        /// Separate from <see cref="TicketsAwarded"/> because the two are not always the
        /// same moment. A share pays on the tap, while Android's share sheet is covering
        /// the game — and a celebration nobody can see or hear is not one, so the
        /// interface holds it until the player is back, and the sound follows the picture.
        /// </summary>
        public static event Action<TicketSource, int> TicketShowerStarted;

        /// <summary>One ticket of an award reached the balance: its index, and how many are flying.</summary>
        public static event Action<int, int> TicketLanded;

        /// <summary>The equipped skin or trail changed. Carries the new indices.</summary>
        public static event Action<int, int> CosmeticsChanged;

        public static void RaiseThrowArmed(RingInstance ring) => ThrowArmed?.Invoke(ring);

        public static void RaiseAimChanged(in AimState state) => AimChanged?.Invoke(state);

        public static void RaiseAimCancelled() => AimCancelled?.Invoke();

        public static void RaiseThrown(in ThrowLaunched payload) => Thrown?.Invoke(payload);

        public static void RaiseImpact(in RingImpact payload) => Impact?.Invoke(payload);

        public static void RaiseRinger(in RingerCaught payload) => Ringer?.Invoke(payload);

        public static void RaiseResolved(in ThrowResolved payload) => Resolved?.Invoke(payload);

        public static void RaiseComboChanged(int combo) => ComboChanged?.Invoke(combo);

        public static void RaiseScoreChanged(int total, int delta) => ScoreChanged?.Invoke(total, delta);

        public static void RaiseRingsRemainingChanged(int remaining) => RingsRemainingChanged?.Invoke(remaining);

        public static void RaisePegClaimed(PegTarget peg, int pegsLeft) => PegClaimed?.Invoke(peg, pegsLeft);

        public static void RaiseLevelStarted(int levelIndex) => LevelStarted?.Invoke(levelIndex);

        public static void RaiseLevelEnded(in LevelResult result) => LevelEnded?.Invoke(result);

        public static void RaiseSessionStarted(GameMode mode) => SessionStarted?.Invoke(mode);

        public static void RaiseSessionClosed() => SessionClosed?.Invoke();

        public static void RaiseLivesChanged(int lives, int max) => LivesChanged?.Invoke(lives, max);

        public static void RaisePerfectStreakChanged(int streak, int needed) =>
            PerfectStreakChanged?.Invoke(streak, needed);

        public static void RaiseTimeChanged(float remaining, float max) =>
            TimeChanged?.Invoke(remaining, max);

        public static void RaiseTimeAwarded(float seconds) => TimeAwarded?.Invoke(seconds);

        public static void RaiseWindChanged(Vector3 wind) => WindChanged?.Invoke(wind);

        public static void RaiseStreakRewarded(bool lifeGranted) => StreakRewarded?.Invoke(lifeGranted);

        public static void RaiseRoundStarted(int round) => RoundStarted?.Invoke(round);

        public static void RaiseRoundCleared(int round, int bonus) => RoundCleared?.Invoke(round, bonus);

        public static void RaiseRunEnded(in EndlessResult result) => RunEnded?.Invoke(result);

        public static void RaiseTicketsAwarded(int amount, int total, TicketSource source) =>
            TicketsAwarded?.Invoke(amount, total, source);

        public static void RaiseTicketShowerStarted(TicketSource source, int amount) =>
            TicketShowerStarted?.Invoke(source, amount);

        public static void RaiseTicketLanded(int index, int count) => TicketLanded?.Invoke(index, count);

        public static void RaiseCosmeticsChanged(int skin, int trail) => CosmeticsChanged?.Invoke(skin, trail);

        /// <summary>
        /// Drops every subscriber. Called when a session tears down so a domain reload
        /// disabled in the editor cannot carry stale delegates into the next Play Mode
        /// run, which shows up as an object being animated after it was destroyed.
        /// </summary>
        public static void Clear()
        {
            ThrowArmed = null;
            AimChanged = null;
            AimCancelled = null;
            Thrown = null;
            Impact = null;
            Ringer = null;
            Resolved = null;
            ComboChanged = null;
            ScoreChanged = null;
            RingsRemainingChanged = null;
            PegClaimed = null;
            LevelStarted = null;
            LevelEnded = null;
            LivesChanged = null;
            PerfectStreakChanged = null;
            TimeChanged = null;
            TimeAwarded = null;
            WindChanged = null;
            StreakRewarded = null;
            RoundStarted = null;
            RoundCleared = null;
            RunEnded = null;
            TicketsAwarded = null;
            TicketShowerStarted = null;
            TicketLanded = null;
            CosmeticsChanged = null;
        }
    }
}
