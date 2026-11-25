using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
public static class ItchWebBuild {
 public static void Run() {
  PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
  PlayerSettings.WebGL.threadsSupport = false;
  var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(), "Builds/ItchWebGL", BuildTarget.WebGL, BuildOptions.None);
  Debug.Log("[ItchWebBuild] " + report.summary.result + " errors=" + report.summary.totalErrors);
  EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
 }
}
