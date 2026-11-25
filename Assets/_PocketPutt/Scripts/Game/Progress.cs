using PocketPutt.Levels;
using PocketPutt.Save;
using PocketPutt.Sim;

namespace PocketPutt.Game
{
    public enum LevelState { Locked, Available, Completed }

    public struct ResultInfo
    {
        public int shots, stars, bestStars, bestShots;
        public bool newBestStars, newBestShots, firstCompletion;
        public bool trophyWon;          // this was the final and the trophy is new
        public bool finalJustUnlocked;  // completing this level opened the final
        public bool cupUnlocked;        // next cup just opened
    }

    /// <summary>Pure progression rules on top of SaveData (see docs/PROGRESSION.md).</summary>
    public sealed class Progress
    {
        public readonly SaveData data;
        public System.Action Changed;

        public Progress(SaveData data) { this.data = data; }

        LevelRecord Rec(string id, bool create)
        {
            foreach (LevelRecord r in data.levels) if (r.id == id) return r;
            if (!create) return null;
            var n = new LevelRecord { id = id };
            data.levels.Add(n);
            return n;
        }

        public int Stars(string id) => Rec(id, false)?.stars ?? 0;
        public int BestShots(string id) => Rec(id, false)?.bestShots ?? 0;
        public bool Completed(string id) => Stars(id) > 0;
        public bool HasTrophy(string cupId) => data.trophies.Contains(cupId);

        public int QualifierStars(CupDef cup)
        {
            int total = 0;
            for (int i = 0; i < cup.QualifierCount; i++) total += Stars(cup.levelIds[i]);
            return total;
        }

        public int QualifiersCompleted(CupDef cup)
        {
            int n = 0;
            for (int i = 0; i < cup.QualifierCount; i++) if (Completed(cup.levelIds[i])) n++;
            return n;
        }

        public int TotalStars()
        {
            int t = 0;
            foreach (LevelRecord r in data.levels) t += r.stars;
            return t;
        }

        public bool CupUnlocked(int cupIndex)
        {
            if (cupIndex <= 0) return true;
            return HasTrophy(Campaign.Cups[cupIndex - 1].id);
        }

        public bool FinalRequirementMet(CupDef cup) =>
            data.finalsUnlocked.Contains(cup.id) ||
            (QualifiersCompleted(cup) == cup.QualifierCount && QualifierStars(cup) >= cup.starThreshold);

        public LevelState StateOf(string levelId)
        {
            CupDef cup = Campaign.CupOf(levelId);
            if (cup == null) return LevelState.Locked;
            int cupIndex = System.Array.IndexOf(Campaign.Cups, cup);
            if (Completed(levelId)) return LevelState.Completed;
            if (!CupUnlocked(cupIndex)) return LevelState.Locked;
            int i = System.Array.IndexOf(cup.levelIds, levelId);
            if (i == 0) return LevelState.Available;
            if (i == cup.levelIds.Length - 1) return FinalRequirementMet(cup) ? LevelState.Available : LevelState.Locked;
            return Completed(cup.levelIds[i - 1]) ? LevelState.Available : LevelState.Locked;
        }

        /// <summary>Highest level the player should be nudged towards (first non-completed available level).</summary>
        public string NextSuggested()
        {
            foreach (CupDef cup in Campaign.Cups)
            {
                if (!cup.Playable) continue;
                foreach (string id in cup.levelIds)
                    if (StateOf(id) == LevelState.Available) return id;
            }
            return Campaign.Cups[0].levelIds[0];
        }

        public ResultInfo Record(LevelData level, int shots)
        {
            CupDef cup = Campaign.CupOf(level.id);
            int stars = Campaign.StarsFor(level, shots);
            LevelRecord r = Rec(level.id, true);
            var info = new ResultInfo { shots = shots, stars = stars };
            bool finalWasOpen = cup != null && FinalRequirementMet(cup);
            info.firstCompletion = r.stars == 0;
            r.plays++;
            if (stars > r.stars) { r.stars = stars; info.newBestStars = !info.firstCompletion; }
            if (r.bestShots == 0 || shots < r.bestShots) { info.newBestShots = r.bestShots != 0; r.bestShots = shots; }
            info.bestStars = r.stars;
            info.bestShots = r.bestShots;
            data.lastLevelId = level.id;

            if (cup != null)
            {
                if (!finalWasOpen && FinalRequirementMet(cup) && level.id != cup.FinalId)
                {
                    data.finalsUnlocked.Add(cup.id);
                    info.finalJustUnlocked = true;
                }
                if (level.id == cup.FinalId && !HasTrophy(cup.id))
                {
                    data.trophies.Add(cup.id);
                    info.trophyWon = true;
                    int idx = System.Array.IndexOf(Campaign.Cups, cup);
                    info.cupUnlocked = idx + 1 < Campaign.Cups.Length;
                }
            }
            SaveStore.Write(data);
            Changed?.Invoke();
            return info;
        }

        public void SaveNow() => SaveStore.Write(data);

        public void ResetAll()
        {
            data.levels.Clear(); data.trophies.Clear(); data.finalsUnlocked.Clear(); data.lastLevelId = "";
            SaveStore.Write(data);
            Changed?.Invoke();
        }
    }
}
