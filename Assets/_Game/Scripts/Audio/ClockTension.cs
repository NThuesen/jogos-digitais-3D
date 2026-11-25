using UnityEngine;

namespace SlipperyZlope.RingToss.Audio
{
    /// <summary>How one tick of the time attack clock is voiced.</summary>
    public readonly struct ClockVoice
    {
        public readonly float Pitch;
        public readonly float Volume;
        public readonly float Cutoff;
        public readonly float Distortion;

        public ClockVoice(float pitch, float volume, float cutoff, float distortion)
        {
            Pitch = pitch;
            Volume = volume;
            Cutoff = cutoff;
            Distortion = distortion;
        }
    }

    /// <summary>
    /// What the ticking clock sounds like for a given amount of time left.
    ///
    /// The owner's brief, nearly word for word: a constant, subtle tick; softer and an
    /// octave down while there is plenty of time; higher, brighter and very slightly
    /// distorted as the run gets close to losing. Kept as a pure mapping so the whole
    /// shape can be read down one test instead of heard by somebody who happens to be
    /// losing at the right moment.
    ///
    /// The rhythm does not change. A clock that speeds up is a bomb, and this is a
    /// fairground stall — the tension is carried by the voice, not the tempo.
    /// </summary>
    public static class ClockTension
    {
        /// <summary>At or above this many seconds the clock is relaxed.</summary>
        public const float CalmSeconds = 20f;

        /// <summary>At or below this it is as tense as it gets.</summary>
        public const float PanicSeconds = 4f;

        /// <summary>One tick a second, like a clock.</summary>
        public const float TickInterval = 1f;

        public static float TensionFor(float remainingSeconds) =>
            1f - Mathf.InverseLerp(PanicSeconds, CalmSeconds, remainingSeconds);

        public static ClockVoice VoiceFor(float tension)
        {
            float t = Mathf.Clamp01(tension);

            // An octave below the clip when relaxed, a little over half an octave above
            // it at the end.
            float pitch = Mathf.Pow(2f, Mathf.Lerp(-1f, 0.6f, t));

            // Quiet throughout, but above the fairground's ambient bed rather than under
            // it: at 0.09 to 0.2 it measured four times quieter than the bed and simply
            // was not there. Raised again after the owner heard it on a handset — a phone
            // speaker rolls off exactly the low end this tick lives in, so a level that
            // measured fine against the bed in the editor still read as too quiet in the
            // hand. About a third louder, which is the "UM POUCO" that was asked for, and
            // still comfortably under a catch.
            float volume = Mathf.Lerp(0.4f, 0.72f, t);

            // Muffled when relaxed, open when tense. This is most of what "softer" is.
            float cutoff = Mathf.Lerp(1600f, 9000f, t);

            // Slight, and only in the last stretch: squared, so it is nothing at all until
            // the clock is genuinely low. The brief said "LEVEMENTE", in capitals.
            float distortion = Mathf.Lerp(0f, 0.32f, t * t);

            return new ClockVoice(pitch, volume, cutoff, distortion);
        }
    }
}
