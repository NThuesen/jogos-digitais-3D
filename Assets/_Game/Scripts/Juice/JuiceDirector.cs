using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.Mobile;
using UnityEngine;

namespace SlipperyZlope.RingToss.Juice
{
    /// <summary>
    /// Listens to the game and decides what each event looks and feels like.
    ///
    /// This is the only class that knows the mapping from "a ring was caught" to
    /// "freeze, flash, burst, pop a number and buzz the phone". Gameplay raises the
    /// event and never learns what happened to it, which is what lets the whole
    /// feedback package be reworked without touching a rule.
    ///
    /// Every result gets its own package, including the failures. A miss that produces
    /// nothing reads as the game having not noticed.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class JuiceDirector : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private JuiceConfig juiceConfig;

        [Header("Systems")]
        [SerializeField] private GameDirector director;
        [SerializeField] private HitStop hitStop;
        [SerializeField] private FxPool fx;
        [SerializeField] private ScorePopupPool popups;

        [Header("Tuning")]
        [Tooltip("Impact speed below which a scrape along the ground is ignored.")]
        [SerializeField] private float groundImpactThreshold = 1.2f;

        [SerializeField] private float shockwaveRadius = 1.5f;
        [SerializeField] private float shockwaveDuration = 0.55f;

        private StageTheme theme;

        /// <summary>
        /// Combo labels, built once. Interpolating "x{combo}" would allocate a string
        /// on the frame of every catch, which is exactly the frame a collection would
        /// be felt on.
        /// </summary>
        private static readonly string[] ComboLabels =
        {
            "x1", "x2", "x3", "x4", "x5", "x6", "x7", "x8", "x9", "x10", "x11", "x12"
        };

        private void OnEnable()
        {
            GameEvents.Impact += OnImpact;
            GameEvents.Ringer += OnRinger;
            GameEvents.PegClaimed += OnPegClaimed;
            GameEvents.Resolved += OnResolved;
            GameEvents.LevelStarted += OnLevelStarted;
            GameEvents.LevelEnded += OnLevelEnded;
            GameEvents.RoundCleared += OnRoundCleared;
        }

        private void OnDisable()
        {
            GameEvents.Impact -= OnImpact;
            GameEvents.Ringer -= OnRinger;
            GameEvents.PegClaimed -= OnPegClaimed;
            GameEvents.Resolved -= OnResolved;
            GameEvents.LevelStarted -= OnLevelStarted;
            GameEvents.LevelEnded -= OnLevelEnded;
            GameEvents.RoundCleared -= OnRoundCleared;
        }

        public void Bind(
            JuiceConfig juice,
            GameDirector gameDirector,
            HitStop stop,
            FxPool effects,
            ScorePopupPool popupPool)
        {
            juiceConfig = juice;
            director = gameDirector;
            hitStop = stop;
            fx = effects;
            popups = popupPool;
        }

        private void OnLevelStarted(int levelIndex)
        {
            theme = director != null && director.CurrentLevel != null
                ? director.CurrentLevel.Theme
                : null;
        }

        private void OnImpact(RingImpact payload)
        {
            float strength = Mathf.Clamp01(payload.Speed / 6f);

            if (payload.HitPeg)
            {
                payload.Peg.Presenter?.PlayHit(strength);

                if (fx != null)
                {
                    Color sparkColor = theme == null ? Color.white : theme.PegGlowColor;
                    fx.PlaySparks(payload.Point, sparkColor, Mathf.RoundToInt(4f + strength * 10f),
                        1.6f + strength * 2.4f, 0.8f);
                }

                Haptics.Pulse(strength > 0.5f ? HapticStrength.Medium : HapticStrength.Light);
                return;
            }

            if (payload.Speed < groundImpactThreshold || fx == null)
            {
                return;
            }

            Color dustColor = theme == null ? new Color(1f, 1f, 1f, 0.5f) : theme.GroundColor;
            fx.PlayDust(payload.Point, dustColor, strength);
        }

        private void OnRinger(RingerCaught payload)
        {
            if (juiceConfig == null)
            {
                return;
            }

            // The freeze comes first and is the longest single beat in the game. It is
            // what gives the eye time to register that the ring went over.
            hitStop?.Freeze(payload.IsPerfect
                ? juiceConfig.PerfectHitStopDuration
                : juiceConfig.HitStopDuration);

            Color accent = theme == null
                ? Color.white
                : payload.IsPerfect ? theme.PerfectColor : theme.SuccessColor;

            Vector3 pegBase = payload.Peg.transform.position;

            if (fx != null)
            {
                fx.PlayShockwave(pegBase, accent, shockwaveRadius * (payload.IsPerfect ? 1.35f : 1f),
                    shockwaveDuration);

                fx.PlaySparks(
                    payload.Ring.Centre,
                    accent,
                    payload.IsPerfect ? 34 : 20,
                    payload.IsPerfect ? 4.2f : 3.1f);

                // Confetti is held back until a real streak. Firing it on every catch
                // would spend the biggest effect in the game on the smallest event.
                if (payload.Combo >= juiceConfig.ConfettiComboThreshold)
                {
                    Color secondary = theme == null ? Color.white : theme.PegGlowColor;
                    fx.PlayConfetti(pegBase + Vector3.up * 1.1f, accent, secondary,
                        Mathf.Min(26 + payload.Combo * 6, 90));
                }
            }

            if (popups != null)
            {
                Vector3 anchor = pegBase + Vector3.up * (payload.Peg.Height + 0.45f);

                popups.PlayScore(anchor, payload.Points, accent, payload.IsPerfect ? 1.2f : 1f);

                if (payload.IsPerfect)
                {
                    popups.PlayLabel(anchor + Vector3.up * 0.5f, "PERFECT", theme?.PerfectColor ?? accent, 0.8f);
                }
                else if (payload.Combo >= 2)
                {
                    popups.PlayLabel(anchor + Vector3.up * 0.5f, ComboLabel(payload.Combo), accent, 0.7f);
                }
            }

            Haptics.Pulse(payload.IsPerfect ? HapticStrength.Heavy : HapticStrength.Medium);
        }

        private static string ComboLabel(int combo)
        {
            return combo >= 1 && combo < ComboLabels.Length
                ? ComboLabels[combo]
                : ComboLabels[ComboLabels.Length - 1];
        }

        private void OnPegClaimed(SlipperyZlope.RingToss.Gameplay.PegTarget peg, int pegsLeft)
        {
            Color claimed = theme == null ? Color.white : theme.SuccessColor;
            peg.Presenter?.PlayClaimed(claimed);
        }

        private void OnResolved(ThrowResolved payload)
        {
            if (payload.Outcome.IsSuccess() || popups == null)
            {
                return;
            }

            Color failColor = theme == null ? Color.grey : theme.FailColor;

            // Failure gets a quiet acknowledgement rather than nothing and rather than
            // a scolding: the player already knows they missed.
            string label = payload.Outcome == ThrowOutcome.Bounce ? "SO CLOSE" : "MISS";
            popups.PlayLabel(payload.Position + Vector3.up * 0.6f, label, failColor, 0.55f);

            Haptics.Pulse(HapticStrength.Light);
        }

        private void OnLevelEnded(LevelResult result)
        {
            if (!result.Completed || fx == null)
            {
                return;
            }

            Celebrate(90);
        }

        /// <summary>
        /// A cleared endless round gets the same celebration a cleared level does, at
        /// about two thirds the size. It has to feel like an achievement — it is the
        /// only one the mode hands out — without becoming wallpaper by round thirty.
        /// </summary>
        private void OnRoundCleared(int round, int bonus)
        {
            Celebrate(60);
        }

        private void Celebrate(int confettiCount)
        {
            if (fx == null)
            {
                return;
            }

            Color primary = theme == null ? Color.white : theme.SuccessColor;
            Color secondary = theme == null ? Color.yellow : theme.PerfectColor;

            fx.PlayConfetti(new Vector3(0f, 2.6f, 0.4f), primary, secondary, confettiCount);
            Haptics.Pulse(HapticStrength.Heavy);
        }
    }
}
