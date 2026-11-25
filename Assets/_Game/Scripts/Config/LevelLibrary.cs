using UnityEngine;

namespace SlipperyZlope.RingToss.Config
{
    /// <summary>
    /// The ordered list of levels, and the stage palettes behind them. A single asset
    /// the scene can reference, so the game never has to look levels up by name or
    /// scan a folder at runtime.
    ///
    /// The palettes are held here rather than only on the levels because endless mode
    /// has no authored levels to read them from, and reaching into level 0's theme to
    /// find stage 0's palette would tie the generated mode to the authored one.
    /// </summary>
    [CreateAssetMenu(menuName = "SlipperyZlope/Level Library", fileName = "LevelLibrary")]
    public sealed class LevelLibrary : ScriptableObject
    {
        [SerializeField] private LevelConfig[] levels = new LevelConfig[0];

        [SerializeField] private StageTheme[] themes = new StageTheme[0];

        public int Count => levels == null ? 0 : levels.Length;

        public int ThemeCount => themes == null ? 0 : themes.Length;

        /// <summary>A stage palette. Wraps, so an endless run never runs out of stages.</summary>
        public StageTheme GetTheme(int index)
        {
            if (themes == null || themes.Length == 0)
            {
                return null;
            }

            int wrapped = ((index % themes.Length) + themes.Length) % themes.Length;
            return themes[wrapped];
        }

        public void SetThemes(StageTheme[] value)
        {
            themes = value;
        }

        public LevelConfig Get(int index)
        {
            if (levels == null || index < 0 || index >= levels.Length)
            {
                return null;
            }

            return levels[index];
        }

        public void SetLevels(LevelConfig[] value)
        {
            levels = value;
        }
    }
}
