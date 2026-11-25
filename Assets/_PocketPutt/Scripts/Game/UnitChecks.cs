using System;
using System.Collections.Generic;
using System.IO;
using PocketPutt.Levels;
using PocketPutt.Save;
using PocketPutt.Sim;
using PocketPutt.UI;
using UnityEngine;

namespace PocketPutt.Game
{
    /// <summary>
    /// Fast rule/data checks with no Unity test framework: progression rules, save robustness and level data sanity.
    /// Run via the editor (LevelTools.UnitBatch) or the player self-test ("unit").
    /// </summary>
    public static class UnitChecks
    {
        public static List<string> RunAll()
        {
            var fails = new List<string>();
            string oldOverride = SaveStore.PathOverride;
            string tmp = Path.Combine(Path.GetTempPath(), "pp_unit_save_" + Guid.NewGuid().ToString("N") + ".json");
            SaveStore.PathOverride = tmp;
            try
            {
                Progression(fails);
                SaveRoundTrip(fails, tmp);
                LevelData_(fails);
                Gesture(fails);
                Hub(fails);
            }
            catch (Exception e) { fails.Add("exception: " + e); }
            finally
            {
                SaveStore.PathOverride = oldOverride;
                foreach (string f in new[] { tmp, tmp + ".bak", tmp + ".tmp" }) if (File.Exists(f)) File.Delete(f);
            }
            return fails;
        }

        static void Check(List<string> fails, bool ok, string what) { if (!ok) fails.Add(what); }

        // ---- slingshot gesture ---------------------------------------------------------------

        static void Gesture(List<string> f)
        {
            // the power mapping itself is a preserved invariant: 24% of min(height, 1.9 * width)
            Check(f, Mathf.Abs(GestureMath.FullDragPixels(1080f, 1920f) - 460.8f) < 0.01f, "gesture: full-drag length unchanged (1080x1920 = 460.8 px)");
            Check(f, Mathf.Abs(GestureMath.Power(new Vector2(500, 900), new Vector2(500, 900 - 230.4f), 1080f, 1920f) - 0.5f) < 1e-4f, "gesture: half pull = 50% power");
            Check(f, GestureMath.Power(new Vector2(500, 900), new Vector2(500, 0), 1080f, 1920f) == 1f, "gesture: power clamps at 1");

            // pressing mid-screen must always allow 100% in every direction, on phones and tablets in both orientations
            int[][] sizes = { new[] { 1080, 1920 }, new[] { 1080, 2160 }, new[] { 1080, 2340 }, new[] { 1080, 2400 }, new[] { 1440, 3200 },
                              new[] { 1200, 1920 }, new[] { 1536, 2048 }, new[] { 720, 1280 }, new[] { 1920, 1080 }, new[] { 2400, 1080 } };
            foreach (int[] sz in sizes)
            {
                float w = sz[0], h = sz[1], margin = 0.03f * Mathf.Min(w, h);
                float frac = GestureMath.ReachableFraction(new Vector2(w * 0.5f, h * 0.5f), w, h, margin);
                Check(f, frac >= 0.999f, $"gesture: mid-screen press reaches 100% in every direction at {sz[0]}x{sz[1]} (got {frac:0.00})");
            }

            // pressing right at a side cannot (that is why the guide says "drag anywhere" and warns at the edge)
            Check(f, GestureMath.ReachableFraction(new Vector2(60f, 960f), 1080f, 1920f, 0f) < 0.9f, "gesture: a press near the side loses directions at 100%");
            Check(f, GestureMath.BlockedByEdge(new Vector2(1f, 900f), 0.5f, 1080f, 1920f, 3f), "gesture: finger on the edge below full power is blocked");
            Check(f, !GestureMath.BlockedByEdge(new Vector2(1f, 900f), 1f, 1080f, 1920f, 3f), "gesture: finger on the edge at full power is not blocked");
            Check(f, !GestureMath.BlockedByEdge(new Vector2(540f, 900f), 0.5f, 1080f, 1920f, 3f), "gesture: finger mid-screen is not blocked");
            Vector2 anchor = new Vector2(300f, 1000f);
            Vector2 pointer = GestureMath.PointerFor(anchor, new Vector2(0.6f, 0.8f), 0.7f, 1080f, 1920f);
            Check(f, Mathf.Abs(GestureMath.Power(anchor, pointer, 1080f, 1920f) - 0.7f) < 1e-4f, "gesture: PointerFor inverts Power");
        }


        // ---- world-map hub (presentation rules; Progress stays the only source of unlock truth) ---------

        static void Hub(List<string> f)
        {
            // layouts: exactly 10 ordered nodes per cup, inside the safe composition area and far enough apart for 48 dp touch targets
            var allIds = new HashSet<string>();
            for (int c = 0; c < Campaign.Cups.Length; c++)
            {
                CupMapLayout l = HubLayouts.For(c);
                foreach (string err in HubLayouts.Validate(l)) f.Add($"hub layout {Campaign.Cups[c].id}: {err}");
                Check(f, l.nodes.Length == Campaign.Cups[c].levelIds.Length, $"hub layout {Campaign.Cups[c].id}: one node per level");
                foreach (string id in Campaign.Cups[c].levelIds) Check(f, LevelLibrary.Get(id) != null && allIds.Add(id), $"hub: level {id} exists once");
            }
            Check(f, allIds.Count == 50, $"hub: 50 unique level ids reachable from the five maps (got {allIds.Count})");
            for (int c = 0; c < Campaign.Cups.Length; c++)
            {
                string cid = Campaign.Cups[c].id;
                Check(f, !HubLayouts.For(c).placeholder, $"hub: {cid} map layout comes from its authored art manifest");
                DestinationArt da = DestinationArt.Load(c);
                Check(f, da != null && da.bands.Length >= 8, $"hub macro: {cid} destination art + silhouette bands are in the build");
                if (da == null) continue;
                Check(f, da.Hit(da.hero, 0f) && da.Hit(da.entrance + new Vector2(0f, -0.04f), 0.03f), $"hub macro: {cid} hero and entrance lie on the silhouette");
                Check(f, !da.Hit(new Vector2(0.01f, 0.02f), 0f) && !da.Hit(new Vector2(0.99f, 0.02f), 0f), $"hub macro: {cid} empty corners do not hit");
                Check(f, da.entrance.y > 0.6f && da.exit.y < da.entrance.y, $"hub macro: {cid} route enters at the base and leaves from behind");
            }
            Check(f, MapNodeElement.Diameter * 0.5f + 14f >= 63f, "hub: node touch radius >= 48 dp (63 px at 1080 wide)");

            // string table: every hub key exists in both languages
            bool pt = Loc.Portuguese;
            foreach (string key in new[] { "hub.title", "hub.cupN", "hub.open", "hub.review", "hub.replay", "hub.locked", "hub.qual", "hub.qstars", "hub.wip" })
                foreach (bool lang in new[] { true, false })
                {
                    Loc.Portuguese = lang;
                    Check(f, Loc.T(key) != key && Loc.T(key).Length > 0, $"hub loc: {key} ({(lang ? "pt" : "en")})");
                }
            Loc.Portuguese = pt;

            CupDef c1 = Campaign.Cups[0];
            var p = new Progress(new SaveData());
            // fresh install
            Check(f, HubLogic.VisualOf(p, "C1-01") == NodeVisual.Next, "hub fresh: C1-01 is the suggested node");
            Check(f, HubLogic.VisualOf(p, "C1-02") == NodeVisual.Locked && HubLogic.VisualOf(p, "C2-01") == NodeVisual.Locked, "hub fresh: everything else locked");
            Check(f, HubLogic.CanStart(p, "C1-01") && !HubLogic.CanStart(p, "C1-02") && !HubLogic.CanStart(p, "C2-01") && !HubLogic.CanStart(p, c1.FinalId), "hub fresh: only C1-01 can launch");
            Check(f, HubLogic.InitialSelection(p, 0) == "C1-01" && HubLogic.InitialSelection(p, 1) == null, "hub fresh: preselects C1-01; locked cup preselects nothing");
            Check(f, HubLogic.FocusCup(p) == 0, "hub fresh: destinations focus the Garden");

            // final locked by stars: 9/9 qualifiers with 9 stars (< 13)
            for (int i = 0; i < 9; i++) p.data.levels.Add(new LevelRecord { id = c1.levelIds[i], stars = 1, bestShots = 9, plays = 1 });
            FinalGate g = HubLogic.GateOf(p, c1);
            Check(f, g.qualifiersDone == 9 && g.stars == 9 && !g.met && !g.needQualifiers && g.needStars, "hub gate: all qualifiers but too few stars");
            Check(f, !HubLogic.CanStart(p, c1.FinalId) && HubLogic.VisualOf(p, c1.FinalId) == NodeVisual.Locked, "hub gate: final locked by stars");
            // final locked by qualifiers: 6 levels with 3 stars = 18 >= 13 but 6/9
            p.data.levels.Clear();
            for (int i = 0; i < 6; i++) p.data.levels.Add(new LevelRecord { id = c1.levelIds[i], stars = 3, bestShots = 1, plays = 1 });
            g = HubLogic.GateOf(p, c1);
            Check(f, g.qualifiersDone == 6 && g.stars >= 13 && !g.met && g.needQualifiers && !g.needStars, "hub gate: enough stars but qualifiers missing");
            Check(f, !HubLogic.CanStart(p, c1.FinalId), "hub gate: final locked by qualifiers");
            Check(f, HubLogic.InitialSelection(p, 0) == c1.levelIds[6], "hub: preselects the first level still to play");
            // final open
            for (int i = 6; i < 9; i++) p.data.levels.Add(new LevelRecord { id = c1.levelIds[i], stars = 1, bestShots = 9, plays = 1 });
            g = HubLogic.GateOf(p, c1);
            Check(f, g.met && HubLogic.CanStart(p, c1.FinalId) && HubLogic.VisualOf(p, c1.FinalId) == NodeVisual.Next, "hub gate: final open and suggested");
            Check(f, HubLogic.InitialSelection(p, 0) == c1.FinalId, "hub gate: with every qualifier done the final is preselected");
            // trophy: cup 2 opens, the final stays replayable
            p.data.levels.Add(new LevelRecord { id = c1.FinalId, stars = 2, bestShots = 3, plays = 1 });
            p.data.trophies.Add("C1");
            Check(f, HubLogic.VisualOf(p, c1.FinalId) == NodeVisual.Completed && HubLogic.CanStart(p, c1.FinalId), "hub trophy: final completed and replayable");
            Check(f, HubLogic.VisualOf(p, "C2-01") == NodeVisual.Next && HubLogic.InitialSelection(p, 1) == "C2-01", "hub trophy: C2 opens with C2-01 suggested");
            Check(f, HubLogic.FocusCup(p) == 1, "hub trophy: destinations focus the next cup");
            Check(f, HubLogic.LockReason(p, "C3-01") != null && HubLogic.LockReason(p, "C3-01").Length > 0, "hub: locked cup explains what to win");

            // returning from a result
            Check(f, HubLogic.SelectionAfterPlay(p, "C2-01", true) == "C2-02" || !HubLogic.CanStart(p, "C2-02") && HubLogic.SelectionAfterPlay(p, "C2-01", true) == "C2-01", "hub result: first completion moves on only when the next level is open");
            Check(f, HubLogic.SelectionAfterPlay(p, "C1-03", false) == "C1-03", "hub result: a replay keeps the replayed level selected");
            Check(f, HubLogic.SelectionAfterPlay(p, c1.FinalId, true) == c1.FinalId, "hub result: the last level of a cup has no next node");
        }

        // ---- progression ---------------------------------------------------------------------

        static void Progression(List<string> f)
        {
            var pr = new Progress(new SaveData());
            Check(f, pr.StateOf("C1-01") == LevelState.Available, "fresh: C1-01 available");
            Check(f, pr.StateOf("C1-02") == LevelState.Locked, "fresh: C1-02 locked");
            Check(f, pr.StateOf("C2-01") == LevelState.Locked, "fresh: C2-01 locked");
            Check(f, pr.StateOf("C1-10") == LevelState.Locked, "fresh: final locked");
            Check(f, pr.NextSuggested() == "C1-01", "fresh: next suggested is C1-01");

            LevelData l1 = LevelLibrary.Get("C1-01");
            Check(f, Campaign.StarsFor(l1, l1.threeStar) == 3, "stars: <= three = 3");
            Check(f, Campaign.StarsFor(l1, l1.threeStar + 1) == 2 || l1.twoStar > l1.threeStar + 1, "stars: just above three = 2");
            Check(f, Campaign.StarsFor(l1, l1.twoStar + 1) == 1, "stars: above two = 1");

            ResultInfo r = pr.Record(l1, 5);
            Check(f, r.stars == 1 && pr.Stars("C1-01") == 1, "record: slow attempt = 1 star");
            Check(f, pr.StateOf("C1-02") == LevelState.Available, "record: next level unlocks");
            r = pr.Record(l1, 1);
            Check(f, r.stars == 3 && pr.Stars("C1-01") == 3 && pr.BestShots("C1-01") == 1, "record: better attempt upgrades");
            pr.Record(l1, 9);
            Check(f, pr.Stars("C1-01") == 3 && pr.BestShots("C1-01") == 1, "record: worse attempt never lowers best");

            // complete 02..09 with 1 star each: 3 + 8 = 11 < 13 -> final stays locked
            CupDef c1 = Campaign.Cups[0];
            for (int i = 1; i < 9; i++) pr.Record(LevelLibrary.Get(c1.levelIds[i]), 99);
            Check(f, pr.QualifiersCompleted(c1) == 9, "cup: nine qualifiers done");
            Check(f, pr.QualifierStars(c1) == 11, "cup: star total counts best per level");
            Check(f, pr.StateOf(c1.FinalId) == LevelState.Locked, "cup: final locked below threshold");
            ResultInfo up = pr.Record(LevelLibrary.Get(c1.levelIds[1]), 1);   // 1 -> 3 stars: +2 => 13
            Check(f, pr.QualifierStars(c1) == 13, "cup: stars reach threshold");
            Check(f, up.finalJustUnlocked, "cup: final-unlocked flag raised once");
            Check(f, pr.StateOf(c1.FinalId) == LevelState.Available, "cup: final available at threshold");
            ResultInfo again = pr.Record(LevelLibrary.Get(c1.levelIds[2]), 99);
            Check(f, !again.finalJustUnlocked, "cup: final-unlocked flag not repeated");
            Check(f, pr.CupUnlocked(1) == false, "cup: next cup locked before trophy");

            ResultInfo fin = pr.Record(LevelLibrary.Get(c1.FinalId), 20);
            Check(f, fin.trophyWon && pr.HasTrophy("C1"), "final: any result wins the trophy");
            Check(f, pr.CupUnlocked(1) && pr.StateOf("C2-01") == LevelState.Available, "final: next cup unlocks");
            ResultInfo fin2 = pr.Record(LevelLibrary.Get(c1.FinalId), 3);
            Check(f, !fin2.trophyWon && pr.data.trophies.Count == 1, "final: trophy is granted only once");
            Check(f, pr.QualifierStars(c1) == 13, "final: final stars never count toward the requirement");

            // a final that was unlocked stays unlocked even if the data is later reduced
            pr.data.levels.RemoveAll(x => x.id == c1.levelIds[1]);
            Check(f, pr.StateOf(c1.FinalId) == LevelState.Completed, "final: completed state persists");
        }

        // ---- save ----------------------------------------------------------------------------

        static void SaveRoundTrip(List<string> f, string path)
        {
            var d = new SaveData();
            d.levels.Add(new LevelRecord { id = "C1-01", stars = 2, bestShots = 3, plays = 4 });
            d.trophies.Add("C1");
            d.language = 2;
            SaveStore.Write(d);
            SaveData back = SaveStore.Load();
            Check(f, back.levels.Count == 1 && back.levels[0].stars == 2 && back.trophies.Contains("C1") && back.language == 2, "save: round trip");

            SaveStore.Write(d); // second write creates .bak
            File.WriteAllText(path, "{ this is not json");
            SaveData recovered = SaveStore.Load();
            Check(f, recovered != null && recovered.levels.Count == 1, "save: corrupt file falls back to backup");
            // crash between "delete main" and "move tmp": the complete tmp is the newest state
            var newer = new SaveData();
            newer.levels.Add(new LevelRecord { id = "C1-01", stars = 3, bestShots = 1, plays = 9 });
            File.Delete(path);
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(newer));
            SaveData fromTmp = SaveStore.Load();
            Check(f, fromTmp.levels.Count == 1 && fromTmp.levels[0].plays == 9, "save: complete .tmp recovers after a crash mid-write");
            // crash while writing tmp: truncated tmp must not hide the good backup
            File.WriteAllText(path + ".tmp", "{ \"version\":1, \"levels\":[ {");
            SaveData fromBak = SaveStore.Load();
            Check(f, fromBak != null && fromBak.levels.Count == 1 && fromBak.levels[0].plays == 4, "save: truncated .tmp falls back to .bak");
            File.Delete(path + ".tmp");
            File.Delete(path);
            File.Delete(path + ".bak");
            SaveData fresh = SaveStore.Load();
            Check(f, fresh != null && fresh.levels.Count == 0, "save: missing file gives a fresh save");
        }

        // ---- level data ----------------------------------------------------------------------

        static float DistToObstacles(LevelData lv, Vector2 p, float time)
        {
            float best = float.MaxValue;
            foreach (Obstacle o in lv.obstacles)
            {
                Capsule c = o.motion == MotionKind.None ? o.shape : o.PoseAt(time);
                Vector2 ab = c.b - c.a;
                float l2 = ab.sqrMagnitude;
                float t = l2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - c.a, ab) / l2) : 0f;
                best = Mathf.Min(best, (p - (c.a + ab * t)).magnitude - c.r);
            }
            return best;
        }

        static void LevelData_(List<string> f)
        {
            var seen = new HashSet<string>();
            foreach (CupDef cup in Campaign.Cups)
            {
                foreach (string id in cup.levelIds)
                {
                    LevelData lv = LevelLibrary.Get(id);
                    if (lv == null) { f.Add($"level {id}: missing"); continue; }
                    Check(f, seen.Add(id), $"level {id}: duplicate id");
                    Check(f, lv.theme == cup.theme, $"level {id}: theme matches its cup");
                    Check(f, lv.isFinal == (id == cup.FinalId), $"level {id}: isFinal flag");
                    Check(f, lv.threeStar >= 1 && lv.twoStar > lv.threeStar, $"level {id}: star targets 1 <= three < two");
                    Check(f, lv.InsideFloor(lv.start), $"level {id}: start inside floor");
                    Check(f, lv.InsideFloor(lv.hole), $"level {id}: hole inside floor");
                    Check(f, (lv.start - lv.hole).magnitude >= 1.5f, $"level {id}: start and hole far enough apart");
                    Check(f, lv.bounds.width <= 4.6f && lv.bounds.height <= 10f, $"level {id}: fits a portrait screen ({lv.bounds.width:0.0}x{lv.bounds.height:0.0})");
                    float cycle = lv.HasMovers ? Mathf.Max(lv.Cycle, 1f) : 0.1f;
                    for (float t = 0f; t <= cycle; t += 0.1f)
                    {
                        Check(f, DistToObstacles(lv, lv.start, t) >= Tuning.BallRadius, $"level {id}: ball start overlaps an obstacle at t={t:0.0}");
                        if (!f.Exists(x => x.StartsWith($"level {id}: ball start"))) { } else break;
                    }
                    Check(f, DistToObstacles(lv, lv.hole, 0f) >= 0.12f, $"level {id}: hole buried in a wall");
                    foreach (string issue in lv.WaterIssues()) f.Add($"level {id}: {issue}");
                    foreach (Portal p in lv.portals)
                    {
                        Check(f, lv.InsideFloor(p.from) && lv.InsideFloor(p.to), $"level {id}: tunnel mouths inside the floor");
                        Check(f, (p.to - lv.hole).magnitude > Tuning.HoleRadius + p.radius, $"level {id}: tunnel exit overlaps the hole");
                    }
                    foreach (Zone z in lv.zones)
                        foreach (Vector2 v in z.poly) Check(f, v.x >= lv.bounds.xMin - 0.01f && v.x <= lv.bounds.xMax + 0.01f && v.y >= lv.bounds.yMin - 0.01f && v.y <= lv.bounds.yMax + 0.01f, $"level {id}: zone inside course bounds");
                }
            }
        }
    }
}
