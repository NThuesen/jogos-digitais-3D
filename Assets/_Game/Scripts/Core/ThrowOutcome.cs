namespace SlipperyZlope.RingToss.Core
{
    /// <summary>
    /// How a single throw ended. Every value gets its own distinct feedback package
    /// in the juice layer, including the failures.
    /// </summary>
    public enum ThrowOutcome
    {
        /// <summary>Descended over the peg without ever touching it.</summary>
        PerfectRinger,

        /// <summary>Landed over the peg after bouncing off something first.</summary>
        Ringer,

        /// <summary>Touched the peg but did not settle over it.</summary>
        Bounce,

        /// <summary>Never reached the peg at all.</summary>
        Miss
    }

    public static class ThrowOutcomeExtensions
    {
        public static bool IsSuccess(this ThrowOutcome outcome)
        {
            return outcome == ThrowOutcome.PerfectRinger || outcome == ThrowOutcome.Ringer;
        }
    }
}
