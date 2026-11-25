using NUnit.Framework;
using SlipperyZlope.RingToss.Audio;

namespace SlipperyZlope.RingToss.Tests
{
    /// <summary>
    /// The time attack tick: soft and low with time in hand, higher, brighter and only
    /// slightly distorted near the end — and never loud.
    /// </summary>
    public sealed class ClockTensionTests
    {
        [Test]
        public void Plenty_of_time_is_no_tension_and_the_last_seconds_are_all_of_it()
        {
            Assert.AreEqual(0f, ClockTension.TensionFor(30f));
            Assert.AreEqual(0f, ClockTension.TensionFor(ClockTension.CalmSeconds));
            Assert.AreEqual(1f, ClockTension.TensionFor(ClockTension.PanicSeconds));
            Assert.AreEqual(1f, ClockTension.TensionFor(0f));
        }

        [Test]
        public void Relaxed_is_an_octave_down_muffled_and_clean()
        {
            ClockVoice voice = ClockTension.VoiceFor(0f);

            Assert.AreEqual(0.5f, voice.Pitch, 1e-4f, "An octave below the clip.");
            Assert.AreEqual(0f, voice.Distortion);
            Assert.Less(voice.Cutoff, 2500f);
        }

        [Test]
        public void Close_to_losing_is_higher_brighter_and_only_slightly_distorted()
        {
            ClockVoice voice = ClockTension.VoiceFor(1f);

            Assert.Greater(voice.Pitch, 1.4f);
            Assert.Greater(voice.Distortion, 0f);
            Assert.LessOrEqual(voice.Distortion, 0.35f, "Slightly, not a fuzz pedal.");
            Assert.Greater(voice.Cutoff, 8000f);
        }

        [Test]
        public void Closer_to_losing_is_a_little_louder()
        {
            Assert.Greater(ClockTension.VoiceFor(1f).Volume, ClockTension.VoiceFor(0f).Volume,
                "Closer to losing is a little louder, not only higher.");
        }

        [Test]
        public void Tension_never_goes_backwards_and_never_gets_loud()
        {
            float previousPitch = 0f;

            for (float remaining = 25f; remaining >= 0f; remaining -= 1f)
            {
                ClockVoice voice = ClockTension.VoiceFor(ClockTension.TensionFor(remaining));

                Assert.GreaterOrEqual(voice.Pitch, previousPitch, $"at {remaining}s");

                // The ceiling moved from 0.55 to 0.75 when the owner heard the tick on a
                // handset and asked for it louder. The rule this guards has not changed
                // and the number is now tied to what it actually means: the catch chimes
                // peak at 0.8, and the clock must never reach the sound it is commenting
                // on. A tick as loud as a ringer is a tick that has stopped being a clock.
                Assert.LessOrEqual(voice.Volume, 0.75f + 1e-4f, $"at {remaining}s");
                previousPitch = voice.Pitch;
            }
        }
    }
}
