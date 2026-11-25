using UnityEngine;

namespace SlipperyZlope.RingToss.Audio
{
    /// <summary>
    /// Synthesises the game's sound effects at load.
    ///
    /// The project ships no audio assets. Every sound here is a handful of oscillators
    /// and an envelope, which keeps the build tiny, keeps the sounds in code where
    /// they can be tuned alongside the animation they accompany, and means a colour
    /// palette change can be matched by a chime that moves with it.
    ///
    /// Two rules hold across all of it. Every clip is **normalised to a stated peak**,
    /// so the mix is a set of numbers that can be read and compared rather than an
    /// accident of how many partials a sound happens to sum. And anything that should
    /// sound like it happened in the booth rather than inside the phone gets a few
    /// taps of delay: three short reflections is not a reverb, but it is the
    /// difference between a sound with a place and a sound without one.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;

        /// <summary>
        /// The mix, in one place. These are peak amplitudes after normalisation, so
        /// the relative loudness of the whole game can be read down this list.
        /// </summary>
        private const float CatchPeak = 0.85f;
        private const float RattlePeak = 0.7f;
        private const float KnockPeak = 0.6f;
        private const float ThudPeak = 0.45f;
        private const float WhooshPeak = 0.4f;
        private const float FanfarePeak = 0.8f;
        private const float UiPeak = 0.42f;

        /// <summary>
        /// The time attack clock. Quieter than everything else on purpose: it fires on
        /// every throw rather than a few times a run, so it has to sit *under* the
        /// catch it is commenting on. Anything at UI level here would be the loudest
        /// thing in the mode by the second board.
        /// </summary>
        private const float ClockPeak = 0.3f;

        /// <summary>
        /// The time attack clock's running tick.
        ///
        /// It was 0.22 at first, on the reasoning that a sound heard sixty times a minute
        /// should sit under everything. The audio server measured what that meant: after
        /// the runtime volume the tick came out about four times quieter than the
        /// fairground's ambient bed — subtle to the point of not being there on a phone
        /// speaker. At 0.5 it sits a little over the bed and still well under a catch.
        /// </summary>
        private const float TickPeak = 0.5f;

        /// <summary>A soft airy whoosh, pitched down as it goes. The sound of a throw.</summary>
        public static AudioClip Whoosh(string name, float duration = 0.34f)
        {
            int count = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[count];

            float phase = 0f;
            float previous = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;

                // Band-limited noise: white noise fed through a one-pole low pass whose
                // cutoff falls over the sound, which is what gives the doppler feel.
                float noise = Random.value * 2f - 1f;
                float cutoff = Mathf.Lerp(0.42f, 0.06f, t);
                previous += (noise - previous) * cutoff;

                // A quiet sine underneath gives the whoosh a body so it does not read
                // as static on a phone speaker.
                phase += 2f * Mathf.PI * Mathf.Lerp(420f, 180f, t) / SampleRate;

                float envelope = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.7f));
                samples[i] = (previous * 0.75f + Mathf.Sin(phase) * 0.16f) * envelope;
            }

            Normalise(samples, WhooshPeak);
            return FromSamples(name, samples);
        }

        /// <summary>
        /// The ring dropping down the peg and settling against the collar.
        ///
        /// This is the sound the whole game is named after and it was missing: a catch
        /// played a chime and nothing else, so the most physical moment in the game was
        /// the least physical thing in the mix. It is a run of woody taps that speed up
        /// and fall in pitch as the ring loses height, ending in a short scrape as it
        /// beds down.
        /// </summary>
        public static AudioClip Rattle(string name, float duration = 0.62f)
        {
            int count = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[count];

            // Taps accelerate the way a dropped ring does: each gap is a fixed
            // fraction of the last. Linear spacing reads as a machine.
            const int taps = 7;
            float gap = 0.115f;
            float at = 0.008f;

            for (int tap = 0; tap < taps; tap++)
            {
                int offset = Mathf.RoundToInt(SampleRate * at);

                // Lower than it was. The old range topped out at 320 Hz, which sat
                // right where the ear is most sensitive and made a wooden ring sound
                // like a plastic one.
                float pitch = Mathf.Lerp(248f, 132f, tap / (float)(taps - 1));
                float level = Mathf.Lerp(1f, 0.42f, tap / (float)(taps - 1));

                for (int i = 0; i < SampleRate / 14 && offset + i < count; i++)
                {
                    float t = (float)i / SampleRate;
                    float sweep = pitch * (1f + 0.55f * Mathf.Exp(-90f * t));
                    float body = Mathf.Sin(2f * Mathf.PI * sweep * t) * Mathf.Exp(-26f * t);
                    float click = (Random.value * 2f - 1f) * Mathf.Exp(-180f * t) * 0.18f;

                    // Each tap swells rather than snapping on. Wood dropped on wood is
                    // a knock; a sample starting at full amplitude is a tick.
                    samples[offset + i] += (body + click) * level * Mathf.Clamp01(t / 0.0025f);
                }

                at += gap;
                gap *= 0.72f;
            }

            AddSpace(samples, 0.22f);
            Normalise(samples, RattlePeak);
            return FromSamples(name, samples);
        }

        /// <summary>
        /// A bell-like chime built from a fundamental and two partials. Used for a
        /// catch; the caller pitches it up with the combo.
        /// </summary>
        public static AudioClip Chime(string name, float frequency, float duration = 1.5f, bool bright = false)
        {
            int count = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[count];

            // Fewer partials, and the highest ones much quieter. The old set carried a
            // 2.98 and a 4.2 at nearly a third of the fundamental, which is where the
            // "ping" came from: a bell struck hard rather than a bell rung.
            float[] partials = bright
                ? new[] { 1f, 2f, 3f }
                : new[] { 1f, 2f };

            float[] weights = bright
                ? new[] { 1f, 0.3f, 0.12f }
                : new[] { 1f, 0.24f };

            // Higher partials decay faster, which is what separates a bell from an
            // organ — but all of them decay far slower than they used to. At a decay of
            // 5 the fundamental was two thirds gone in a fifth of a second, which is
            // what made every sound arrive and leave before it had been heard.
            float[] decays = bright
                ? new[] { 1.9f, 3.2f, 5f }
                : new[] { 2.1f, 3.6f };

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float value = 0f;

                for (int p = 0; p < partials.Length; p++)
                {
                    value += Mathf.Sin(2f * Mathf.PI * frequency * partials[p] * t)
                             * weights[p] * Mathf.Exp(-decays[p] * t);
                }

                // A slow swell in rather than a couple of milliseconds of anti-click.
                // Four milliseconds is a transient, and a transient is the single thing
                // that makes a soft sound read as an aggressive one; thirty-five is a
                // sound that arrives.
                float attack = Mathf.Clamp01(t / 0.035f);
                attack *= attack;

                samples[i] = value * attack;
            }

            AddSpace(samples, 0.26f);
            Normalise(samples, CatchPeak);
            return FromSamples(name, samples);
        }

        /// <summary>A short woody knock. The ring clipping the peg.</summary>
        public static AudioClip Knock(string name, float frequency = 190f, float duration = 0.18f)
        {
            int count = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;

                // The pitch drop over the first few milliseconds is what makes this read
                // as something solid being struck rather than as a tone. Gentler than
                // it was: a 1.6x sweep over 90 per second is a crack, not a knock.
                float sweep = frequency * (1f + 0.9f * Mathf.Exp(-60f * t));
                float body = Mathf.Sin(2f * Mathf.PI * sweep * t) * Mathf.Exp(-14f * t);

                // The click is what made this sting. Quieter, and given a couple of
                // milliseconds to arrive rather than starting at full amplitude.
                float click = (Random.value * 2f - 1f) * Mathf.Exp(-110f * t) * 0.16f;

                samples[i] = (body + click) * Mathf.Clamp01(t / 0.003f);
            }

            Normalise(samples, KnockPeak);
            return FromSamples(name, samples);
        }

        /// <summary>A dry muffled tap. A ring landing on the board with nothing to show for it.</summary>
        public static AudioClip Thud(string name, float duration = 0.22f)
        {
            int count = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[count];

            float previous = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;

                float noise = Random.value * 2f - 1f;
                previous += (noise - previous) * 0.08f;

                float body = Mathf.Sin(2f * Mathf.PI * 96f * t) * Mathf.Exp(-19f * t);
                samples[i] = (previous * 1.6f + body * 0.7f) * Mathf.Exp(-13f * t);
            }

            Normalise(samples, ThudPeak);
            return FromSamples(name, samples);
        }

        /// <summary>A short rising arpeggio. Played when a level is cleared.</summary>
        public static AudioClip Fanfare(string name, float rootFrequency = 523.25f)
        {
            // A major triad plus the octave, each note overlapping the last.
            float[] intervals = { 1f, 1.25f, 1.5f, 2f };

            // Slower, and each note allowed to ring on under the next. The old spacing
            // fired four notes inside half a second, which reads as an alarm rather
            // than as a flourish.
            const float noteSpacing = 0.15f;
            const float noteDuration = 1.1f;

            float total = noteSpacing * (intervals.Length - 1) + noteDuration;
            int count = Mathf.RoundToInt(SampleRate * total);
            float[] samples = new float[count];

            for (int note = 0; note < intervals.Length; note++)
            {
                int offset = Mathf.RoundToInt(SampleRate * noteSpacing * note);
                float frequency = rootFrequency * intervals[note];

                for (int i = 0; i < Mathf.RoundToInt(SampleRate * noteDuration); i++)
                {
                    int index = offset + i;

                    if (index >= count)
                    {
                        break;
                    }

                    float t = (float)i / SampleRate;
                    float value = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-2.2f * t)
                                  + Mathf.Sin(4f * Mathf.PI * frequency * t) * Mathf.Exp(-3.8f * t) * 0.22f;

                    float attack = Mathf.Clamp01(t / 0.02f);
                    samples[index] += value * attack * attack * 0.16f;
                }
            }

            AddSpace(samples, 0.3f);
            Normalise(samples, FanfarePeak);
            return FromSamples(name, samples);
        }

        /// <summary>
        /// A two-tone tick for buttons. Two pitches a fifth apart, the upper one a few
        /// milliseconds behind: a single sine reads as a test tone, and two read as a
        /// mechanism.
        /// </summary>
        public static AudioClip Blip(string name, float frequency = 660f, float duration = 0.11f)
        {
            int count = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[count];
            int lag = SampleRate / 220;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = Mathf.Clamp01(t / 0.003f) * Mathf.Exp(-28f * t);

                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope;

                if (i >= lag)
                {
                    float lagged = (float)(i - lag) / SampleRate;
                    samples[i] += Mathf.Sin(2f * Mathf.PI * frequency * 1.5f * lagged)
                                  * Mathf.Clamp01(lagged / 0.003f) * Mathf.Exp(-34f * lagged) * 0.6f;
                }
            }

            Normalise(samples, UiPeak);
            return FromSamples(name, samples);
        }

        /// <summary>
        /// A quick rising twinkle. Tickets landing in the player's pocket.
        /// </summary>
        public static AudioClip Sparkle(string name, float rootFrequency = 1046.5f)
        {
            float[] intervals = { 1f, 1.335f, 1.682f, 2f };
            const float spacing = 0.045f;
            const float noteDuration = 0.28f;

            int count = Mathf.RoundToInt(SampleRate * (spacing * intervals.Length + noteDuration));
            float[] samples = new float[count];

            for (int note = 0; note < intervals.Length; note++)
            {
                int offset = Mathf.RoundToInt(SampleRate * spacing * note);
                float frequency = rootFrequency * intervals[note];

                for (int i = 0; i < Mathf.RoundToInt(SampleRate * noteDuration); i++)
                {
                    int index = offset + i;

                    if (index >= count)
                    {
                        break;
                    }

                    float t = (float)i / SampleRate;

                    // A sine plus a quiet octave, decaying fast. Glassy rather than belly.
                    float value = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-11f * t)
                                  + Mathf.Sin(4f * Mathf.PI * frequency * t) * Mathf.Exp(-18f * t) * 0.3f;

                    samples[index] += value * Mathf.Clamp01(t / 0.003f);
                }
            }

            AddSpace(samples, 0.24f);
            Normalise(samples, UiPeak * 1.5f);
            return FromSamples(name, samples);
        }

        /// <summary>
        /// A firm two-note confirm for a purchase. Falls rather than rises, which is
        /// what makes it read as a transaction completing instead of a reward arriving.
        /// </summary>
        public static AudioClip Confirm(string name, float rootFrequency = 392f)
        {
            const float noteDuration = 0.34f;
            int count = Mathf.RoundToInt(SampleRate * (noteDuration + 0.1f));
            float[] samples = new float[count];

            float[] frequencies = { rootFrequency * 1.5f, rootFrequency };

            for (int note = 0; note < frequencies.Length; note++)
            {
                int offset = Mathf.RoundToInt(SampleRate * 0.085f * note);

                for (int i = 0; i < Mathf.RoundToInt(SampleRate * noteDuration); i++)
                {
                    int index = offset + i;

                    if (index >= count)
                    {
                        break;
                    }

                    float t = (float)i / SampleRate;
                    float value = Mathf.Sin(2f * Mathf.PI * frequencies[note] * t) * Mathf.Exp(-6.5f * t)
                                  + Mathf.Sin(2f * Mathf.PI * frequencies[note] * 2f * t)
                                    * Mathf.Exp(-11f * t) * 0.35f;

                    samples[index] += value * Mathf.Clamp01(t / 0.004f);
                }
            }

            AddSpace(samples, 0.26f);
            Normalise(samples, UiPeak * 1.6f);
            return FromSamples(name, samples);
        }

        /// <summary>
        /// Winding the clock: a short ratchet of clicks that accelerate and rise, then
        /// a small bell.
        ///
        /// A real sound rather than a nice one. Time attack pays the player in
        /// *seconds*, and a generic reward chime says "you got something" where this
        /// says "you got more clock" — the mechanism and the feedback are the same
        /// object, which is the whole reason the mode drew a dial instead of printing
        /// a number.
        ///
        /// The clicks accelerate because a winding key does. Evenly spaced they read
        /// as a stutter or a buffer underrun; speeding up reads as effort, and it is
        /// what makes the bell at the end land as a release rather than as a seventh
        /// click.
        /// </summary>
        public static AudioClip ClockWind(string name)
        {
            const int clicks = 6;
            const float duration = 0.44f;

            int count = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[count];

            float at = 0f;
            float gap = 0.032f;

            for (int click = 0; click < clicks; click++)
            {
                int offset = Mathf.RoundToInt(SampleRate * at);

                // Each click is a hard, tiny burst: a square edge through a fast
                // decay. Pitch climbs across the run, which is what turns six clicks
                // into one gesture instead of six events.
                float frequency = Mathf.Lerp(880f, 1560f, (float)click / (clicks - 1));
                int length = Mathf.RoundToInt(SampleRate * 0.018f);

                for (int i = 0; i < length; i++)
                {
                    int index = offset + i;

                    if (index >= count)
                    {
                        break;
                    }

                    float t = (float)i / SampleRate;
                    float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * frequency * t));

                    samples[index] += square * Mathf.Exp(-160f * t) * 0.5f;
                }

                at += gap;
                gap *= 0.82f;
            }

            // The bell the winding arrives at. Two partials a fifth apart, which is
            // enough to read as a small brass bell and not enough to read as a chord.
            int bellAt = Mathf.RoundToInt(SampleRate * (at + 0.01f));

            for (int i = 0; bellAt + i < count; i++)
            {
                float t = (float)i / SampleRate;

                float value = Mathf.Sin(2f * Mathf.PI * 1568f * t) * Mathf.Exp(-9f * t)
                              + Mathf.Sin(2f * Mathf.PI * 2349f * t) * Mathf.Exp(-14f * t) * 0.4f;

                // A short attack rather than an instant one, for the reason the whole
                // mix was softened in phase 10: a four millisecond edge is a transient,
                // and a transient is what makes a gentle sound read as a sharp one.
                samples[bellAt + i] += value * Mathf.Clamp01(t / 0.012f);
            }

            AddSpace(samples, 0.2f);
            Normalise(samples, ClockPeak);
            return FromSamples(name, samples);
        }

        /// <summary>
        /// The clock losing time: a spring letting go — a dull strike with the pitch
        /// sagging under it.
        ///
        /// Deliberately the same instrument as <see cref="ClockWind"/> heard from the
        /// wrong end, rather than a separate "you failed" noise. The player has not
        /// done anything wrong that needs announcing; the clock has simply gone
        /// backwards, and it should sound like the same mechanism running down.
        ///
        /// Short, and with no bell on it. The reward gets the bright tail because a
        /// reward is worth dwelling on; a loss that rang out for the same length would
        /// be the game tutting at somebody who already knows they missed.
        /// </summary>
        public static AudioClip ClockUnwind(string name)
        {
            const float duration = 0.36f;

            int count = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[count];

            // The strike: a low, slightly inharmonic body. The 2.7 ratio is what stops
            // it reading as a musical note — bells are not harmonic and a clock's
            // insides are less harmonic still.
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;

                float value = Mathf.Sin(2f * Mathf.PI * 196f * t) * Mathf.Exp(-11f * t)
                              + Mathf.Sin(2f * Mathf.PI * 196f * 2.7f * t) * Mathf.Exp(-20f * t) * 0.3f;

                samples[i] += value * Mathf.Clamp01(t / 0.01f);
            }

            // And the sag: a quiet tone sliding down a fourth over the first eighth of
            // a second. Phase is integrated rather than computed from the instantaneous
            // frequency, because multiplying a falling frequency by t sweeps the wrong
            // way and clicks at the end.
            float phase = 0f;
            int slide = Mathf.RoundToInt(SampleRate * 0.13f);

            for (int i = 0; i < slide && i < count; i++)
            {
                float t = (float)i / SampleRate;
                float frequency = Mathf.Lerp(430f, 245f, (float)i / slide);

                phase += 2f * Mathf.PI * frequency / SampleRate;

                samples[i] += Mathf.Sin(phase) * Mathf.Exp(-9f * t) * 0.34f;
            }

            AddSpace(samples, 0.18f);
            Normalise(samples, ClockPeak * 0.92f);
            return FromSamples(name, samples);
        }

        /// <summary>
        /// One beat of the time attack clock: a wooden tick, or the lower tock after it.
        ///
        /// Built to be heard once a second for as long as a run lasts, which is a
        /// different brief from every other sound here: it has to disappear while
        /// nothing is wrong and still be there when the ear goes looking for it. So it
        /// is very short, with no tail and no bell, and it carries no reverb — a tick
        /// that rang would smear into the next one.
        ///
        /// The tock sits a fourth below the tick. That interval is what makes a pair of
        /// clicks read as a clock rather than as a metronome.
        /// </summary>
        public static AudioClip ClockTick(string name, bool tock)
        {
            const float duration = 0.05f;

            int count = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[count];

            float body = tock ? 1320f : 1760f;
            float filtered = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;

                // The escapement: a click of noise through a one-pole low pass, gone in
                // a few milliseconds.
                float noise = Random.value * 2f - 1f;
                filtered = Mathf.Lerp(filtered, noise, 0.35f);
                float click = filtered * Mathf.Exp(-260f * t);

                // And the case answering it: a small, inharmonic resonance.
                float ring = Mathf.Sin(2f * Mathf.PI * body * t) * Mathf.Exp(-120f * t) * 0.6f
                             + Mathf.Sin(2f * Mathf.PI * body * 2.4f * t) * Mathf.Exp(-190f * t) * 0.2f;

                samples[i] = (click + ring) * Mathf.Clamp01(t / 0.0015f);
            }

            Normalise(samples, TickPeak);
            return FromSamples(name, samples);
        }

        /// <summary>
        /// A seamless bed of fairground air: filtered noise breathing slowly, with a
        /// low hum under it. Quiet enough to be felt rather than heard.
        ///
        /// Looped, so the two ends have to meet. The last quarter of a second is
        /// crossfaded into the head, which is enough to hide the seam in noise this
        /// heavily filtered.
        /// </summary>
        public static AudioClip Ambience(string name, float duration = 6f)
        {
            int count = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[count];

            float low = 0f;
            float lower = 0f;

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;

                float noise = Random.value * 2f - 1f;

                // Two cascaded one-pole filters: one is a hiss, two is air.
                low += (noise - low) * 0.035f;
                lower += (low - lower) * 0.035f;

                // Two detuned slow swells, so the bed never repeats audibly inside the
                // loop even though the loop itself is short.
                float breath = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.07f * t)
                                    + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.113f * t);

                float hum = Mathf.Sin(2f * Mathf.PI * 58f * t) * 0.05f
                            + Mathf.Sin(2f * Mathf.PI * 87f * t) * 0.03f;

                samples[i] = lower * 22f * breath + hum;
            }

            samples = CrossfadeLoop(samples, 0.25f);
            Normalise(samples, 0.5f);
            return FromSamples(name, samples);
        }

        /// <summary>
        /// Three short reflections mixed back in. Not a reverb — a reverb needs a
        /// convolution and a tail this project has no budget for — but enough for a
        /// sound to belong to the booth rather than to the phone.
        /// </summary>
        private static void AddSpace(float[] samples, float mix)
        {
            if (mix <= 0f)
            {
                return;
            }

            // Prime-ish spacings so the taps never line up into an audible pitch.
            int[] taps = { SampleRate / 27, SampleRate / 14, SampleRate / 9 };
            float[] gains = { 0.6f, 0.38f, 0.22f };

            float[] source = (float[])samples.Clone();

            for (int tap = 0; tap < taps.Length; tap++)
            {
                int delay = taps[tap];
                float gain = gains[tap] * mix;

                for (int i = delay; i < samples.Length; i++)
                {
                    samples[i] += source[i - delay] * gain;
                }
            }
        }

        /// <summary>
        /// Folds the tail of a buffer into its head and returns the shortened buffer,
        /// so the clip can loop without a click.
        ///
        /// The overlap is equal-power crossfaded, which holds the noise floor steady
        /// across the seam where a linear fade would dip. The tail has to be *removed*
        /// rather than silenced: a clip that keeps its full length ends in a quarter
        /// second of silence, and the loop then ticks once per cycle — which is worse
        /// than the click this is fixing.
        /// </summary>
        private static float[] CrossfadeLoop(float[] samples, float seconds)
        {
            int fade = Mathf.Min(Mathf.RoundToInt(SampleRate * seconds), samples.Length / 4);

            if (fade <= 1)
            {
                return samples;
            }

            int length = samples.Length - fade;
            float[] looped = new float[length];

            for (int i = 0; i < length; i++)
            {
                looped[i] = samples[i];
            }

            for (int i = 0; i < fade; i++)
            {
                float t = (float)i / fade;
                looped[i] = samples[i] * Mathf.Sin(t * Mathf.PI * 0.5f)
                            + samples[length + i] * Mathf.Cos(t * Mathf.PI * 0.5f);
            }

            return looped;
        }

        private static void Normalise(float[] samples, float peak)
        {
            float loudest = 0f;

            for (int i = 0; i < samples.Length; i++)
            {
                loudest = Mathf.Max(loudest, Mathf.Abs(samples[i]));
            }

            if (loudest <= 1e-5f)
            {
                return;
            }

            float gain = peak / loudest;

            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= gain;
            }
        }

        private static AudioClip FromSamples(string name, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
