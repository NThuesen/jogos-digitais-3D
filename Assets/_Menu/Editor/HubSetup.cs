using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameHub.EditorTools
{
    /// <summary>
    /// Generates the menu scene and puts the three scenes in the build, menu first.
    /// </summary>
    public static class HubSetup
    {
        [MenuItem("Game Hub/Build Menu Scene")]
        public static void BuildMenuScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Menu Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.orthographic = true;
            cameraObject.AddComponent<AudioListener>();

            GameObject menuObject = new GameObject("Main Menu");
            MainMenu menu = menuObject.AddComponent<MainMenu>();
            SerializedObject serialized = new SerializedObject(menu);
            serialized.FindProperty("menuCamera").objectReferenceValue = camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(HubNavigation.MenuScenePath));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, HubNavigation.MenuScenePath);
            ConfigureBuildSettings();
            Debug.Log($"[Game Hub] Built {HubNavigation.MenuScenePath}.");
        }

        [MenuItem("Game Hub/Configure Build Settings")]
        public static void ConfigureBuildSettings()
        {
            string[] paths =
            {
                HubNavigation.MenuScenePath,
                HubNavigation.RingTossScenePath,
                HubNavigation.PocketPuttScenePath
            };

            foreach (string path in paths.Where(p => !File.Exists(p)))
            {
                Debug.LogWarning($"[Game Hub] Scene missing, generate it first: {path}");
            }

            EditorBuildSettings.scenes = paths
                .Where(File.Exists)
                .Select(p => new EditorBuildSettingsScene(p, true))
                .ToArray();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Everything a fresh copy needs: the Pocket Putt scene and assets, the menu, and the
        /// build list. Ring Toss's scene is committed and is not regenerated here.
        /// </summary>
        [MenuItem("Game Hub/Build All Generated Scenes")]
        public static void BuildAll()
        {
            PocketPutt.EditorTools.GameAssetBuilder.BuildAll();
            BuildMenuScene();
            EditorSceneManager.OpenScene(HubNavigation.MenuScenePath);
        }
    }
}
