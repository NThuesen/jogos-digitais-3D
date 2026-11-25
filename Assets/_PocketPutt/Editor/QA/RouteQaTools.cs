#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using PocketPutt.Levels;
using UnityEditor;
using UnityEngine;

namespace PocketPutt.QA
{
    public static class RouteQaTools
    {
        public static void CheckBatch()
        {
            var failures = RouteQaChecks.RunAll();
            foreach (string f in failures) Debug.LogError("[RouteQA] " + f);
            Debug.Log("[RouteQA] " + (failures.Count == 0 ? ("ALL PASSED (" + RouteQaChecks.GroupCount + " groups)") : failures.Count + " FAILED"));
            EditorApplication.Exit(failures.Count == 0 ? 0 : 3);
        }

        public static void AnalyzeBatch()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                string filter = Arg(args, "-qaLevels", "C1-");
                string output = Arg(args, "-qaOut", "Builds/RouteQA/report");
                var report = new QaRunReport { revision = Arg(args, "-qaRevision", "UNSPECIFIED"),
                    sourceFingerprint = Arg(args, "-qaFingerprint", "UNSPECIFIED"), environment = "Unity Editor " + Application.unityVersion,
                    settings = new RouteQaSettings() };
                var selected = LevelLibrary.All().Where(l => filter == "all" || filter.Split(',').Any(f => l.id.StartsWith(f, StringComparison.Ordinal))).ToArray();
                if (selected.Length == 0) throw new ArgumentException("No levels match " + filter);
                Directory.CreateDirectory(output);
                foreach (var level in selected)
                {
                    QaLevelReport result = RouteQa.Analyze(level, report.settings);
                    report.levels.Add(result);
                    File.WriteAllText(Path.Combine(output, level.id + ".json"), JsonUtility.ToJson(result, true));
                    Debug.Log($"[RouteQA] {level.id}: high={result.direct.highSunk}/{result.direct.highAttempts} families={result.families.Count}");
                }
                File.WriteAllText(Path.Combine(output, "run.json"), JsonUtility.ToJson(report, true));
                File.WriteAllText(Path.Combine(output, "summary.csv"), RouteQaReport.Csv(report));
                File.WriteAllText(Path.Combine(output, "summary.md"), RouteQaReport.Markdown(report));
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(2); }
        }

        static string Arg(string[] args, string name, string fallback)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
    }
}
#endif
