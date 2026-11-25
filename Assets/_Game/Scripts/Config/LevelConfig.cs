using UnityEngine;

namespace SlipperyZlope.RingToss.Config
{
    /// <summary>
    /// One level: the board layout, how many rings the player gets, what the rings
    /// are like, and what counts as a good run.
    ///
    /// A level is cleared by claiming every peg. Rings are the resource, so the
    /// difficulty curve is mostly the gap between how many pegs there are and how
    /// many rings you get to claim them with.
    /// </summary>
    [CreateAssetMenu(menuName = "SlipperyZlope/Level Config", fileName = "Level")]
    public sealed class LevelConfig : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string displayName = "Level";
        [SerializeField] private string subtitle = string.Empty;
        [SerializeField] private StageTheme theme;

        [Header("Board")]
        [SerializeField] private PegPlacement[] pegs = { PegPlacement.Default(Vector2.zero) };

        [Header("Rings")]
        [Tooltip("How many throws the player gets.")]
        [Range(1, 12)]
        [SerializeField] private int ringCount = 5;

        [Tooltip("Ring size as a multiple of the default. Below 1 the hole shrinks and " +
                 "the peg has to be hit more precisely.")]
        [Range(0.55f, 1.3f)]
        [SerializeField] private float ringScale = 1f;

        [Tooltip("Extra tumble applied at launch. Makes the ring wobble in flight and " +
                 "land less predictably.")]
        [Range(0f, 3f)]
        [SerializeField] private float ringWobble = 0f;

        [Header("Air")]
        [Tooltip("Constant sideways acceleration in m/s^2. The player has to aim off-axis.")]
        [SerializeField] private Vector3 wind = Vector3.zero;

        [Tooltip("Amplitude of the gust that rides on top of the constant wind.")]
        [SerializeField] private float gustAmplitude = 0f;

        [Tooltip("Gust cycles per second.")]
        [SerializeField] private float gustFrequency = 0.35f;

        [Header("Scoring")]
        [Tooltip("Rings still in hand at the end needed for the second star.")]
        [SerializeField] private int ringsLeftForTwoStars = 1;

        [Tooltip("Rings still in hand at the end needed for the third star.")]
        [SerializeField] private int ringsLeftForThreeStars = 2;

        public string DisplayName => displayName;
        public string Subtitle => subtitle;
        public StageTheme Theme => theme;

        public int PegCount => pegs == null ? 0 : pegs.Length;
        public PegPlacement GetPeg(int index) => pegs[index];

        public int RingCount => ringCount;
        public float RingScale => ringScale;
        public float RingWobble => ringWobble;

        public Vector3 Wind => wind;
        public float GustAmplitude => gustAmplitude;
        public float GustFrequency => gustFrequency;

        public int RingsLeftForTwoStars => ringsLeftForTwoStars;
        public int RingsLeftForThreeStars => ringsLeftForThreeStars;

        /// <summary>True when this level has any air moving at all.</summary>
        public bool HasWind => wind.sqrMagnitude > 0.0001f || gustAmplitude > 0.0001f;

        /// <summary>Wind acceleration at a moment in time, gust included.</summary>
        public Vector3 WindAt(float time)
        {
            if (gustAmplitude <= 0f)
            {
                return wind;
            }

            float gust = Mathf.Sin(2f * Mathf.PI * gustFrequency * time);
            return wind + new Vector3(gust * gustAmplitude, 0f, 0f);
        }

        /// <summary>
        /// Stars for a completed run. One for finishing, more for finishing with rings
        /// to spare, so the reward is efficiency rather than a separate score chase.
        /// </summary>
        public int StarsFor(int ringsLeft)
        {
            if (ringsLeft >= ringsLeftForThreeStars)
            {
                return 3;
            }

            return ringsLeft >= ringsLeftForTwoStars ? 2 : 1;
        }

        public void Configure(
            string label,
            string sub,
            StageTheme stageTheme,
            PegPlacement[] placements,
            int rings,
            float scale,
            float wobble,
            Vector3 windForce,
            float gust,
            float gustHz,
            int twoStars,
            int threeStars)
        {
            displayName = label;
            subtitle = sub;
            theme = stageTheme;
            pegs = placements;
            ringCount = rings;
            ringScale = scale;
            ringWobble = wobble;
            wind = windForce;
            gustAmplitude = gust;
            gustFrequency = gustHz;
            ringsLeftForTwoStars = twoStars;
            ringsLeftForThreeStars = threeStars;
        }
    }
}
