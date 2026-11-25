using PocketPutt.Game;
using PocketPutt.Levels;
using UnityEngine;

namespace PocketPutt.UI
{
    /// <summary>How a map node is drawn. Derived from Progress; the hub never re-implements unlock rules.</summary>
    public enum NodeVisual { Locked, Available, Next, Completed }

    public struct FinalGate
    {
        public int qualifiersDone, qualifiersTotal, stars, threshold;
        public bool met;            // Progress.FinalRequirementMet
        public bool needQualifiers; // fewer than all qualifiers completed
        public bool needStars;      // qualifier stars below the threshold
    }

    /// <summary>Pure presentation decisions for the world-map hub (selection, labels, gate text); no Unity UI types.</summary>
    public static class HubLogic
    {
        public static NodeVisual VisualOf(Progress p, string levelId)
        {
            LevelState st = p.StateOf(levelId);
            if (st == LevelState.Completed) return NodeVisual.Completed;
            if (st == LevelState.Locked) return NodeVisual.Locked;
            return levelId == p.NextSuggested() ? NodeVisual.Next : NodeVisual.Available;
        }

        /// <summary>The single launch rule of the hub: only levels Progress reports as Available/Completed may start.</summary>
        public static bool CanStart(Progress p, string levelId) => LevelLibrary.Get(levelId) != null && p.StateOf(levelId) != LevelState.Locked;

        public static FinalGate GateOf(Progress p, CupDef cup)
        {
            int done = p.QualifiersCompleted(cup), stars = p.QualifierStars(cup);
            return new FinalGate
            {
                qualifiersDone = done, qualifiersTotal = cup.QualifierCount, stars = stars, threshold = cup.starThreshold,
                met = p.FinalRequirementMet(cup), needQualifiers = done < cup.QualifierCount, needStars = stars < cup.starThreshold,
            };
        }

        /// <summary>Level to preselect when a cup map opens: the first level you can still improve on the path, otherwise the final.</summary>
        public static string InitialSelection(Progress p, int cupIndex)
        {
            CupDef cup = Campaign.Cups[Mathf.Clamp(cupIndex, 0, Campaign.Cups.Length - 1)];
            if (!p.CupUnlocked(cupIndex)) return null;
            foreach (string id in cup.levelIds) if (p.StateOf(id) == LevelState.Available) return id;
            return cup.FinalId;
        }

        /// <summary>After a result: a first-time completion moves on to the next level when it is open, anything else keeps the played level.</summary>
        public static string SelectionAfterPlay(Progress p, string playedId, bool firstCompletion)
        {
            if (!firstCompletion) return playedId;
            string next = Campaign.NextLevelId(playedId);
            return next != null && LevelLibrary.Get(next) != null && p.StateOf(next) != LevelState.Locked ? next : playedId;
        }

        /// <summary>Cup shown first on the destinations screen: the one that holds the suggested level.</summary>
        public static int FocusCup(Progress p)
        {
            int i = System.Array.IndexOf(Campaign.Cups, Campaign.CupOf(p.NextSuggested()));
            return Mathf.Max(0, i);
        }

        public static string LockReason(Progress p, string levelId)
        {
            CupDef cup = Campaign.CupOf(levelId);
            int ci = System.Array.IndexOf(Campaign.Cups, cup);
            if (!p.CupUnlocked(ci)) return Loc.T("cup.locked", Loc.Pick(Campaign.Cups[Mathf.Max(0, ci - 1)].name));
            if (levelId == cup.FinalId) return null;   // the final explains itself with two numeric conditions
            return Loc.T("level.locked");
        }

        public static string CupRequirement(int cupIndex, bool map = false) =>
            Loc.T(map ? "hub.mapRequirement" : "hub.cupRequirement",
                Loc.Pick(Campaign.Cups[Mathf.Max(0, cupIndex - 1)].name));

        public static string ProgressLine(Progress p, CupDef cup)
        {
            FinalGate g = GateOf(p, cup);
            return $"{g.qualifiersDone}/{g.qualifiersTotal}  ·  {g.stars}/{g.threshold} ★";
        }
    }
}
