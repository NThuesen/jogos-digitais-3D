using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SlipperyZlope.RingToss.EditorTools
{
    /// <summary>
    /// One-shot project configuration for this title. Kept in the repository so the
    /// setup is reproducible and reviewable instead of living as undocumented
    /// serialized state. Safe to re-run: every step is idempotent.
    /// </summary>
    public static class ProjectBootstrap
    {
        private const string CompanyName = "SlipperyZlope Games";
        private const string ProductName = "Jogos Digitais 3D";
        private const string ApplicationIdentifier = "com.slipperyzlope.jogosdigitais3d";

        private const string SettingsFolder = "Assets/_Game/Settings";
        private const string RendererAssetPath = SettingsFolder + "/RingToss_Renderer.asset";
        private const string PipelineAssetPath = SettingsFolder + "/RingToss_URP.asset";

        [MenuItem("SlipperyZlope/Bootstrap Project Settings")]
        public static void Run()
        {
            ApplyIdentity();
            ApplyOrientation();
            ApplyScriptingSettings();
            ApplyRenderPipeline();
            TidyGeneratedRenderPipelineAssets();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ProjectBootstrap] Completed.");
        }

        private static void ApplyIdentity()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationIdentifier);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, ApplicationIdentifier);
            Debug.Log($"[ProjectBootstrap] Identity: {CompanyName} / {ProductName} / {ApplicationIdentifier}");
        }

        private static void ApplyOrientation()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            Debug.Log("[ProjectBootstrap] Orientation locked to portrait.");
        }

        private static void ApplyScriptingSettings()
        {
            // Linear color space is required for the stylized lighting this game targets.
            PlayerSettings.colorSpace = ColorSpace.Linear;

            foreach (NamedBuildTarget target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetApiCompatibilityLevel(target, ApiCompatibilityLevel.NET_Standard);
                PlayerSettings.SetIl2CppCompilerConfiguration(target, Il2CppCompilerConfiguration.Release);
            }

            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            Debug.Log("[ProjectBootstrap] IL2CPP + ARM64 + .NET Standard 2.1 + Linear color space.");
        }

        private static void ApplyRenderPipeline()
        {
            EnsureFolder(SettingsFolder);

            UniversalRendererData rendererData =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);

            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererAssetPath);
            }

            UniversalRenderPipelineAsset pipelineAsset =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);

            if (pipelineAsset == null)
            {
                pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);
            }

            // Mobile-oriented defaults. Everything else stays at URP's own defaults
            // until the art direction in a later phase justifies changing it.
            pipelineAsset.msaaSampleCount = 4;
            // HDR on, and this is the one render setting in the project that is worth
            // the bandwidth. With it off the colour buffer clamps at white, so an
            // emissive surface brighter than 1 does not glow — it simply goes white and
            // stays the same size, and Bloom has nothing left to spread. That is exactly
            // what "it only changes colour, I want it to actually glow" turned out to
            // mean: two rounds of raising the floor border's emission produced a paler
            // line and no light, because the light had nowhere to go.
            //
            // The buffer is R11G11B10 rather than full float (HDRColorBufferPrecision
            // stays at its default), which is the cheap mobile HDR format.
            pipelineAsset.supportsHDR = true;
            pipelineAsset.shadowDistance = 25f;
            pipelineAsset.shadowCascadeCount = 1;
            pipelineAsset.supportsCameraDepthTexture = false;
            pipelineAsset.supportsCameraOpaqueTexture = false;
            EditorUtility.SetDirty(pipelineAsset);

            GraphicsSettings.defaultRenderPipeline = pipelineAsset;

            int originalQualityLevel = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.count; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipelineAsset;
            }

            QualitySettings.SetQualityLevel(originalQualityLevel, false);

            Debug.Log("[ProjectBootstrap] URP assigned as the render pipeline.");
        }

        /// <summary>
        /// Creates folders through the AssetDatabase rather than Directory.CreateDirectory
        /// plus AssetDatabase.Refresh. A Refresh here would compile any pending script
        /// changes and reload the domain in the middle of this method, invalidating every
        /// object reference it is holding — which silently produced a ring with no
        /// colliders on the first run after any edit.
        /// </summary>
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        /// <summary>
        /// URP drops its global settings and default volume profile at the root of
        /// Assets/ the first time it initializes. Move them next to the rest of this
        /// game's settings. AssetDatabase.MoveAsset preserves GUIDs, so the references
        /// in GraphicsSettings survive the move.
        /// </summary>
        private static void TidyGeneratedRenderPipelineAssets()
        {
            MoveIfAtAssetsRoot("UniversalRenderPipelineGlobalSettings.asset");
            MoveIfAtAssetsRoot("DefaultVolumeProfile.asset");
        }

        private static void MoveIfAtAssetsRoot(string fileName)
        {
            string sourcePath = "Assets/" + fileName;
            if (AssetDatabase.LoadAssetAtPath<Object>(sourcePath) == null)
            {
                return;
            }

            string destinationPath = SettingsFolder + "/" + fileName;
            string error = AssetDatabase.MoveAsset(sourcePath, destinationPath);

            if (string.IsNullOrEmpty(error))
            {
                Debug.Log($"[ProjectBootstrap] Moved {fileName} into {SettingsFolder}.");
            }
            else
            {
                Debug.LogWarning($"[ProjectBootstrap] Could not move {fileName}: {error}");
            }
        }
    }
}
