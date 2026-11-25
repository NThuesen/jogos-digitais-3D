using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Core;
using UnityEngine;

namespace SlipperyZlope.RingToss.Juice
{
    /// <summary>
    /// The deck lighting up as the player plays well.
    ///
    /// Third pass, and the shortest. A pool around the pegs answered the first note;
    /// lighting the whole deck and its rim answered the second; what the owner wanted,
    /// said plainly the third time, is **only the border**: "somente a borda dele, sem
    /// passar para o resto do chão", with the strength following the perfects in a row.
    ///
    /// So it is emission on the board's trim and on nothing else at all — not the deck
    /// inside it, not the booth floor, not the ground. A faint base while a board is in
    /// play, a step per perfect in a row up to a ceiling, a short flare on each one, and
    /// an ease back down that is slower than the rise, so losing a streak reads as the
    /// light going out rather than a switch being thrown.
    ///
    /// The streak belongs to the session, not to the board. A generated run lays a new
    /// board out every round, and resetting there kept the light from ever outgrowing
    /// one board's two or three pegs. It is put out — at once, not eased — when a mode
    /// starts or the player leaves for the menu, and eased out on a miss and at the end
    /// of a level or a run.
    ///
    /// Juice, so it only listens: a perfect, a miss, a session starting or closing.
    /// </summary>
    public sealed class FloorGlow : MonoBehaviour
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");

        /// <summary>Seconds a perfect's flare takes to die away.</summary>
        private const float FlashDecay = 0.2f;

        private Renderer rim;
        private Renderer skirt;
        private JuiceConfig config;
        private MaterialPropertyBlock block;
        private Color colour = new Color(1f, 0.8f, 0.45f);

        private int streak;
        private float level;
        private float flashAt = float.NegativeInfinity;
        private float applied = -1f;

        /// <summary>Perfects in a row that the floor is currently showing.</summary>
        public int Streak => streak;

        /// <summary>The deck's emission right now, flare included.</summary>
        public float Intensity => level + Flare(Time.unscaledTime);

        /// <summary>
        /// Hands over what glows: the board's border, and the soft band of light it
        /// spills inwards.
        ///
        /// Two renderers rather than one because they are different kinds of thing. The
        /// border is lit geometry and goes bright enough to bloom; the spill is additive,
        /// adds light to the floor without painting it, and is what makes the border read
        /// as a light source rather than as a paler line.
        /// </summary>
        public void Bind(Renderer trim, Renderer spill, JuiceConfig juice)
        {
            rim = trim;
            skirt = spill;
            config = juice;
            block ??= new MaterialPropertyBlock();
            level = Target;
            applied = -1f;
        }

        /// <summary>
        /// Shows the floor as a run of perfects would, without anybody throwing — for the
        /// capture tool, and for looking at the steps in the editor. The next miss, board
        /// or result puts it back as usual.
        /// </summary>
        public void ShowStreak(int perfectsInARow)
        {
            streak = Mathf.Max(0, perfectsInARow);
        }

        /// <summary>The theme's glow colour. Called whenever the stage changes.</summary>
        public void SetColour(Color glow)
        {
            colour = glow;
            applied = -1f;
        }

        private float Target => config == null ? 0.05f : config.FloorGlowFor(streak);

        private void OnEnable()
        {
            GameEvents.Ringer += OnRinger;
            GameEvents.Resolved += OnResolved;
            GameEvents.SessionStarted += OnSessionStarted;
            GameEvents.SessionClosed += PutOut;
            GameEvents.LevelEnded += OnLevelEnded;
            GameEvents.RunEnded += OnRunEnded;
        }

        private void OnDisable()
        {
            GameEvents.Ringer -= OnRinger;
            GameEvents.Resolved -= OnResolved;
            GameEvents.SessionStarted -= OnSessionStarted;
            GameEvents.SessionClosed -= PutOut;
            GameEvents.LevelEnded -= OnLevelEnded;
            GameEvents.RunEnded -= OnRunEnded;
        }

        private void OnRinger(RingerCaught payload)
        {
            // Only a perfect climbs. A scrappy catch is not an error, so it keeps what
            // the streak has built; it just does not add to it.
            if (!payload.IsPerfect)
            {
                return;
            }

            streak++;
            flashAt = Time.unscaledTime;
        }

        private void OnResolved(ThrowResolved payload)
        {
            bool caught = payload.Outcome == ThrowOutcome.PerfectRinger
                          || payload.Outcome == ThrowOutcome.Ringer;

            if (!caught)
            {
                streak = 0;
            }
        }

        private void OnSessionStarted(GameMode mode) => PutOut();

        /// <summary>
        /// Back to the resting glow on the spot. A new session, or the menu, must not
        /// open under the light the last run earned while it fades.
        /// </summary>
        private void PutOut()
        {
            streak = 0;
            level = Target;
            flashAt = float.NegativeInfinity;
            applied = -1f;
        }

        private void OnLevelEnded(LevelResult result) => streak = 0;

        private void OnRunEnded(EndlessResult result) => streak = 0;

        private void Update()
        {
            if (rim == null)
            {
                return;
            }

            float target = Target;
            float rise = config == null ? 0.25f : config.FloorGlowRise;
            float fall = config == null ? 0.7f : config.FloorGlowFall;

            // Unscaled, so a hit stop's slow motion does not stretch the answer to the
            // catch that caused it.
            float seconds = Mathf.Max(0.01f, target > level ? rise : fall);
            level += (target - level) * (1f - Mathf.Exp(-Time.unscaledDeltaTime / seconds));

            float intensity = level + Flare(Time.unscaledTime);

            if (Mathf.Abs(intensity - applied) < 0.0005f)
            {
                return;
            }

            applied = intensity;
            Apply(rim, intensity * (config == null ? 1.7f : config.FloorGlowRimBoost));

            // The spill is driven by what the streak earned, not by the whole level.
            //
            // The border keeps its faint resting glow, because it is the edge of the area
            // a ring can land in and is worth seeing at all times. The *light* is not: it
            // is the reward, and a reward that never fully goes out reads as not having
            // reset — which is what the owner saw once the spill got wide enough to
            // notice at rest. Subtracting the base is what makes a miss put the light
            // out rather than dim it.
            float resting = config == null ? 0.02f : config.FloorGlowFor(0);
            float earned = Mathf.Max(0f, intensity - resting);

            ApplySkirt(earned * (config == null ? 1.05f : config.FloorGlowSkirt));
        }

        private float Flare(float now)
        {
            float amount = config == null ? 0.05f : config.FloorGlowFlash;
            float age = now - flashAt;
            return age < 0f ? 0f : amount * Mathf.Exp(-age / FlashDecay);
        }

        /// <summary>
        /// The spill. Written as a base colour rather than an emission because the
        /// material is additive: what is written here is literally the light added to
        /// whatever is behind it, so black is off and there is nothing to fade.
        /// </summary>
        private void ApplySkirt(float intensity)
        {
            if (skirt == null)
            {
                return;
            }

            skirt.GetPropertyBlock(block);
            block.SetColor(BaseColourId, colour * Mathf.Max(0f, intensity));
            skirt.SetPropertyBlock(block);
        }

        private void Apply(Renderer target, float intensity)
        {
            if (target == null)
            {
                return;
            }

            target.GetPropertyBlock(block);
            block.SetColor(EmissionId, colour * intensity);
            target.SetPropertyBlock(block);
        }
    }
}
