#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PocketPutt.Levels;
using PocketPutt.Sim;
using UnityEditor;
using UnityEngine;

namespace PocketPutt.EditorTools
{
    /// <summary>
    /// Headless level QA built on the runtime solver: reachability, shot tolerance, plans and top-down debug PNGs.
    /// Run with -executeMethod PocketPutt.EditorTools.LevelTools.AnalyzeBatch
    /// </summary>
    public static class LevelTools
    {
        // ---- Debug image --------------------------------------------------------------------

        public static void SaveDebugPng(LevelData level, string path, List<Vector2[]> traces)
        {
            const float ppm = 90f;
            Rect b = level.bounds;
            int w = Mathf.CeilToInt((b.width + 1f) * ppm), h = Mathf.CeilToInt((b.height + 1f) * ppm);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            Color32 bg = new Color32(40, 44, 40, 255), grass = new Color32(96, 168, 82, 255),
                sand = new Color32(226, 200, 140, 255), ice = new Color32(170, 220, 240, 255);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Vector2 wp = new Vector2(b.xMin - 0.5f + x / ppm, b.yMin - 0.5f + y / ppm);
                    Color32 c = bg;
                    if (level.InsideFloor(wp))
                    {
                        Surface sf = level.SurfaceAt(wp);
                        c = sf == Surface.Sand ? sand : sf == Surface.Ice ? ice : grass;
                    }
                    px[y * w + x] = c;
                }
            Func<Vector2, Vector2> toPx = v => new Vector2((v.x - b.xMin + 0.5f) * ppm, (v.y - b.yMin + 0.5f) * ppm);
            foreach (Obstacle o in level.obstacles)
            {
                Capsule c = o.PoseAt(0f);
                Color32 col = o.kind == ObstacleKind.Post ? new Color32(30, 110, 40, 255)
                    : o.kind == ObstacleKind.Mover ? new Color32(210, 70, 60, 255) : new Color32(110, 70, 40, 255);
                DrawCapsule(px, w, h, toPx(c.a), toPx(c.b), c.r * ppm, col);
            }
            foreach (var blk in level.blocks) FillPoly(px, w, h, blk.Select(toPx).ToArray(), new Color32(70, 130, 60, 255));
            DrawDisc(px, w, h, toPx(level.hole), Tuning.HoleRadius * ppm, new Color32(10, 10, 10, 255));
            DrawDisc(px, w, h, toPx(level.start), Tuning.BallRadius * ppm, new Color32(255, 255, 255, 255));
            foreach (Portal p in level.portals)
            {
                DrawDisc(px, w, h, toPx(p.from), p.radius * ppm, new Color32(90, 60, 160, 255));
                DrawDisc(px, w, h, toPx(p.to), p.radius * ppm, new Color32(160, 100, 220, 255));
            }
            foreach (var tr in traces)
                for (int i = 0; i + 1 < tr.Length; i += 2)
                    DrawDisc(px, w, h, toPx(tr[i]), 2f, new Color32(255, 40, 40, 255));
            tex.SetPixels32(px);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static void DrawDisc(Color32[] px, int w, int h, Vector2 c, float r, Color32 col)
        {
            int x0 = Mathf.Max(0, (int)(c.x - r - 1)), x1 = Mathf.Min(w - 1, (int)(c.x + r + 1));
            int y0 = Mathf.Max(0, (int)(c.y - r - 1)), y1 = Mathf.Min(h - 1, (int)(c.y + r + 1));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    if ((new Vector2(x, y) - c).sqrMagnitude <= r * r) px[y * w + x] = col;
        }

        static void DrawCapsule(Color32[] px, int w, int h, Vector2 a, Vector2 b, float r, Color32 col)
        {
            float minx = Mathf.Min(a.x, b.x) - r - 1, maxx = Mathf.Max(a.x, b.x) + r + 1;
            float miny = Mathf.Min(a.y, b.y) - r - 1, maxy = Mathf.Max(a.y, b.y) + r + 1;
            for (int y = Mathf.Max(0, (int)miny); y <= Mathf.Min(h - 1, (int)maxy); y++)
                for (int x = Mathf.Max(0, (int)minx); x <= Mathf.Min(w - 1, (int)maxx); x++)
                {
                    Vector2 p = new Vector2(x, y), ab = b - a;
                    float l2 = ab.sqrMagnitude;
                    float t = l2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2) : 0f;
                    if ((p - (a + ab * t)).sqrMagnitude <= r * r) px[y * w + x] = col;
                }
        }

        static void FillPoly(Color32[] px, int w, int h, Vector2[] poly, Color32 col)
        {
            float miny = poly.Min(p => p.y), maxy = poly.Max(p => p.y), minx = poly.Min(p => p.x), maxx = poly.Max(p => p.x);
            var z = new Zone { poly = poly };
            for (int y = Mathf.Max(0, (int)miny); y <= Mathf.Min(h - 1, (int)maxy); y++)
                for (int x = Mathf.Max(0, (int)minx); x <= Mathf.Min(w - 1, (int)maxx); x++)
                    if (z.Contains(new Vector2(x, y))) px[y * w + x] = col;
        }

        // ---- Entry points -------------------------------------------------------------------

        public static void AnalyzeBatch()
        {
            string[] args = Environment.GetCommandLineArgs();
            string only = ArgValue(args, "-levels", "all");
            string outDir = ArgValue(args, "-out", Path.Combine(Path.GetTempPath(), "pp_levels"));
            int depth = int.Parse(ArgValue(args, "-depth", "2"));
            bool deep = ArgValue(args, "-deep", "1") == "1";
            bool writePlans = ArgValue(args, "-writePlans", "0") == "1";
            var sb = new StringBuilder();
            var plans = new StringBuilder();
            foreach (LevelData lv in LevelLibrary.All())
            {
                if (only != "all" && !only.Split(',').Contains(lv.id)) continue;
                var t0 = DateTime.Now;
                SolveResult st = Solver.Solve(lv, depth, float.Parse(ArgValue(args, "-minw1", "2"), System.Globalization.CultureInfo.InvariantCulture), float.Parse(ArgValue(args, "-minwN", "4"), System.Globalization.CultureInfo.InvariantCulture), deep);
                string verdict = !st.solvable ? "UNSOLVED" : st.minShots <= lv.threeStar ? "ok" : $"par-mismatch(solver {st.minShots} > 3*={lv.threeStar})";
                string line = $"{lv.id} {lv.title}: {verdict} minShots={st.minShots} (3*={lv.threeStar} 2*={lv.twoStar}) " +
                              $"finalWidth={st.finalAngleWidth:0.0}deg powerLevels={st.finalPowerLevels}/22 " +
                              (st.closestMiss >= 0 ? $"closestMiss={st.closestMiss:0.00}m " : "") +
                              $"[{(DateTime.Now - t0).TotalSeconds:0.0}s]";
                Debug.Log("[LevelQA] " + line);
                sb.AppendLine(line);
                if (st.solvable)
                {
                    plans.Append(lv.id);
                    foreach (ShotPlan sp in st.plan) plans.Append($" {sp.angleDeg.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)},{sp.power.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)}@{sp.startTime.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}");
                    plans.AppendLine();
                }
                SaveDebugPng(lv, Path.Combine(outDir, lv.id + ".png"), st.traces);
            }
            Directory.CreateDirectory(outDir);
            // merge by level id so partial runs never discard other levels' results
            MergeLines(Path.Combine(outDir, "report.txt"), sb.ToString());
            MergeLines(Path.Combine(outDir, "plans.txt"), plans.ToString());
            EditorApplication.Exit(0);
        }

        /// <summary>Runs the pure-C# unit checks headlessly (-executeMethod PocketPutt.EditorTools.LevelTools.UnitBatch).</summary>
        public static void UnitBatch()
        {
            var bad = PocketPutt.Game.UnitChecks.RunAll();
            bad.AddRange(PocketPutt.QA.RouteQaChecks.RunAll());
            foreach (string b in bad) Debug.LogError("[UnitChecks] FAIL " + b);
            Debug.Log("[LevelQA] unit checks: " + (bad.Count == 0 ? "ALL PASSED" : bad.Count + " FAILED"));
            EditorApplication.Exit(bad.Count == 0 ? 0 : 3);
        }

        static void MergeLines(string path, string newText)
        {
            var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (File.Exists(path))
                foreach (string l in File.ReadAllLines(path)) if (l.Length >= 5) map[l.Substring(0, 5)] = l;
            foreach (string l in newText.Split((char)10)) { string t = l.TrimEnd((char)13); if (t.Length >= 5) map[t.Substring(0, 5)] = t; }
            File.WriteAllLines(path, map.Values);
        }

        static string ArgValue(string[] args, string name, string def)
        {
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return def;
        }
    }
}
#endif
