namespace SlipperyZlope.RingToss.Config
{
    /// <summary>
    /// How tall the board's layers are, in one place.
    ///
    /// These were three separate numbers in three files, and two of them disagreed with
    /// the deck by enough to hide an effect completely. The board's deck sits at 0.026
    /// and its plank seams at 0.042; the catch shockwave was drawn at 0.02 above the
    /// peg's anchor and the peg's own ground halo at 0.008 — both *under* the deck, on a
    /// board root that sits at zero. Neither had ever been seen by anybody, including in
    /// every capture this project has taken, because a flat mesh under an opaque floor
    /// does not look broken, it looks absent.
    ///
    /// So the heights are shared now, and anything flat that has to read as lying *on*
    /// the board uses <see cref="GroundFxY"/> rather than picking its own clearance.
    /// </summary>
    public static class BoardMetrics
    {
        /// <summary>Top of the board's deck, above the board root.</summary>
        public const float DeckSurfaceY = 0.026f;

        /// <summary>Top of the plank seams laid across the deck.</summary>
        public const float DeckSeamY = 0.042f;

        /// <summary>
        /// Where a flat effect has to sit to be seen on the board: clear of the deck and
        /// clear of its seams, close enough to still read as lying on the floor.
        /// </summary>
        public const float GroundFxY = 0.055f;

        /// <summary>Width of the lit border that marks where a ring can land.</summary>
        public const float GlowBorderWidth = 0.09f;

        /// <summary>
        /// Width of the soft band of light the border spills inwards.
        ///
        /// A line that merely gets paler is not a light, and the owner said so twice. The
        /// first attempt at this was a flat band, which simply read as a second line — a
        /// glow has to *fall off*, so the band now carries a gradient that is bright
        /// against the border and gone by its inner edge.
        ///
        /// It spills inwards only. Outwards at the back edge is the booth's floor, and
        /// "não é para estender para trás do booth" is the whole reason the glow moved
        /// off the trim in the first place.
        /// </summary>
        public const float GlowSkirtWidth = 1.15f;
    }
}
