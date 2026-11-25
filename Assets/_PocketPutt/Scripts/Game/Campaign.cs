using System.Collections.Generic;
using PocketPutt.Levels;
using PocketPutt.Sim;

namespace PocketPutt.Game
{
    public sealed class CupDef
    {
        public string id;
        public string name;
        public string tagline;
        public Theme theme;
        public int starThreshold;
        public string trophyName;
        public string[] levelIds;     // 9 qualifiers + final
        public int Count => levelIds.Length;
        public string FinalId => levelIds[levelIds.Length - 1];
        public int QualifierCount => levelIds.Length - 1;
        public bool Playable
        {
            get
            {
                foreach (string l in levelIds) if (LevelLibrary.Get(l) == null) return false;
                return true;
            }
        }
    }

    public static class Campaign
    {
        public static readonly CupDef[] Cups =
        {
            Make("C1", "Copa do Jardim|Garden Cup", "Sua estreia no clube local|Your debut at the local club", Theme.Garden, 13, "Taça do Jardim|Garden Trophy"),
            Make("C2", "Copa das Dunas|Dunes Cup", "Areia, atrito e rotas seguras|Sand, friction and safe routes", Theme.Dunes, 15, "Taça das Dunas|Dunes Trophy"),
            Make("C3", "Copa Glacial|Glacier Cup", "Gelo, inércia e túneis|Ice, momentum and tunnels", Theme.Glacier, 17, "Taça Glacial|Glacier Trophy"),
            Make("C4", "Copa da Oficina|Workshop Cup", "Engrenagens e bom timing|Gears and good timing", Theme.Workshop, 18, "Taça da Oficina|Workshop Trophy"),
            Make("C5", "Copa dos Mestres|Masters Cup", "O mundial de minigolfe|The mini golf world championship", Theme.Arena, 20, "Grande Taça dos Mestres|Grand Masters Trophy"),
        };

        static CupDef Make(string id, string name, string tag, Theme theme, int threshold, string trophy)
        {
            var ids = new string[10];
            for (int i = 0; i < 10; i++) ids[i] = $"{id}-{i + 1:00}";
            return new CupDef { id = id, name = name, tagline = tag, theme = theme, starThreshold = threshold, trophyName = trophy, levelIds = ids };
        }

        public static CupDef CupOf(string levelId)
        {
            foreach (CupDef c in Cups) foreach (string l in c.levelIds) if (l == levelId) return c;
            return null;
        }

        public static int StarsFor(LevelData level, int shots) => shots <= level.threeStar ? 3 : shots <= level.twoStar ? 2 : 1;

        /// <summary>Next level to play after finishing 'levelId', or null at the end of a cup.</summary>
        public static string NextLevelId(string levelId)
        {
            CupDef c = CupOf(levelId);
            if (c == null) return null;
            int i = System.Array.IndexOf(c.levelIds, levelId);
            return i >= 0 && i + 1 < c.levelIds.Length ? c.levelIds[i + 1] : null;
        }
    }
}
