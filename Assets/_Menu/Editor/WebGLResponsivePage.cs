using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace GameHub.EditorTools
{
    // The published page fills its iframe. PocketViewport.jslib temporarily narrows
    // the canvas while Pocket Putt is active; Unity then resizes its render target.
    public sealed class WebGLResponsivePage : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;
            string index = Path.Combine(report.summary.outputPath, "index.html");
            string html = File.ReadAllText(index);
            const string style = "<style>#unity-container.unity-desktop,#unity-container.unity-mobile{position:fixed;inset:0;width:100%;height:100%;transform:none;left:0;top:0}#unity-canvas{width:100%!important;height:100%!important;display:block}#unity-footer{display:none}</style>";
            if (!html.Contains("<head>")) throw new BuildFailedException("WebGL index.html has no head element.");
            html = html.Replace("<head>", "<head>\n    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n    " + style);
            // Match the published build: persist the games' WebGL saves automatically.
            if (!html.Contains("autoSyncPersistentDataPath: true"))
            {
                if (!html.Contains("var config = {")) throw new BuildFailedException("WebGL index.html has no Unity configuration.");
                html = html.Replace("var config = {", "var config = {\n        autoSyncPersistentDataPath: true,");
            }
            File.WriteAllText(index, html);
        }
    }
}
