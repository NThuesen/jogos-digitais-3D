namespace SlipperyZlope.RingToss.Core
{
    /// <summary>
    /// Which set of rules a session is running under.
    ///
    /// The modes share every system below this line — the same board, the same throw,
    /// the same feedback. They differ only in what the resource is and what ends a
    /// run: a ring budget against an authored board, lives against a board that keeps
    /// being rebuilt, or a clock against the same rebuilt board.
    /// </summary>
    public enum GameMode
    {
        /// <summary>Authored levels, a ring budget, stars and unlocks.</summary>
        Levels,

        /// <summary>Generated rounds, three lives, no end.</summary>
        Endless,

        /// <summary>
        /// The endless board against a clock instead of lives. Same generator, same
        /// ramp; the resource is time, and it is always running out.
        /// </summary>
        TimeAttack
    }
}
