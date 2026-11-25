using System.Collections.Generic;
using PocketPutt.Sim;

namespace PocketPutt.Levels
{
    public static class LevelLibrary
    {
        static List<LevelData> all;
        static Dictionary<string, LevelData> byId;

        public static IReadOnlyList<LevelData> All()
        {
            if (all == null)
            {
                all = new List<LevelData>();
                all.AddRange(GardenCup.Build());
                all.AddRange(DunesCup.Build());
                all.AddRange(GlacierCup.Build());
                all.AddRange(WorkshopCup.Build());
                all.AddRange(MastersCup.Build());
                byId = new Dictionary<string, LevelData>();
                foreach (LevelData l in all) byId[l.id] = l;
            }
            return all;
        }

        public static LevelData Get(string id)
        {
            All();
            return byId.TryGetValue(id, out LevelData l) ? l : null;
        }
    }
}
