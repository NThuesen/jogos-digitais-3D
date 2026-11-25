using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SlipperyZlope.RingToss.EditorTools
{
    /// <summary>
    /// Renders the gameplay camera to a PNG at the game's portrait aspect ratio, so
    /// framing and readability can be checked without opening the editor.
    ///
    /// The render goes through URP's SubmitRenderRequest rather than Camera.Render:
    /// calling Camera.Render directly while a scriptable render pipeline is active
    /// bypasses the pipeline and produces an image lit by ambient only, with material
    /// base colours missing.
    /// </summary>
    public static class SceneCapture
    {
        private const string MainScenePath = "Assets/_Game/Scenes/Main.unity";
        private const int Width = 540;
        private const int Height = 960;

        public static void Run()
        {
            string outputPath = GetArgument("-capturePath") ?? "capture.png";

            EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            Camera camera = Object.FindFirstObjectByType<Camera>();

            if (camera == null)
            {
                Debug.LogError("[SceneCapture] No camera in the scene.");
                return;
            }

            RenderTexture target = new RenderTexture(
                Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                antiAliasing = 4
            };

            UniversalRenderPipeline.SingleCameraRequest request =
                new UniversalRenderPipeline.SingleCameraRequest { destination = target };

            if (!RenderPipeline.SupportsRenderRequest(camera, request))
            {
                Debug.LogError("[SceneCapture] URP single camera render requests are not supported here.");
                return;
            }

            RenderPipeline.SubmitRenderRequest(camera, request);

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = target;

            Texture2D image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();

            RenderTexture.active = previousActive;

            File.WriteAllBytes(outputPath, image.EncodeToPNG());

            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);

            Debug.Log($"[SceneCapture] Wrote {outputPath} ({Width}x{Height}).");
        }

        private static string GetArgument(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
