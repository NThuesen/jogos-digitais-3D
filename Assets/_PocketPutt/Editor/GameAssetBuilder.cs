#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PocketPutt.EditorTools
{
    /// <summary>Creates the small set of serialized assets the procedural game needs. Safe to run repeatedly.</summary>
    public static class GameAssetBuilder
    {
        const string ResDir = "Assets/_PocketPutt/Resources";
        const string MatDir = ResDir + "/Materials";
        const string ScenePath = GameHub.HubNavigation.PocketPuttScenePath;

        [MenuItem("Pocket Putt/Build Game Assets")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(MatDir);
            CreateMaterials();
            CreatePanelSettings();
            CreateScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PocketPutt] Game assets built.");
        }

        public static void BuildAllBatch()
        {
            BuildAll();
            EditorApplication.Exit(0);
        }

        static Material Template(string name, string shaderName, bool transparent)
        {
            string path = $"{MatDir}/{name}.mat";
            Shader shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogError("Shader not found: " + shaderName); return null; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            if (transparent) MakeTransparent(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void MakeTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.SetShaderPassEnabled("ShadowCaster", false);
        }

        static void CreateMaterials()
        {
            Template("_Lit", "Universal Render Pipeline/Lit", false);
            Template("_Unlit", "Universal Render Pipeline/Unlit", false);
            Template("_UnlitTransparent", "Universal Render Pipeline/Unlit", true);
            Template("_Particle", "Universal Render Pipeline/Particles/Unlit", true);
        }

        static void CreatePanelSettings()
        {
            string path = ResDir + "/PocketPanelSettings.asset";
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (ps == null)
            {
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(ps, path);
            }
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1080, 1920);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0f;
            ps.sortingOrder = 10;
            ps.clearColor = false;
            if (ps.themeStyleSheet == null)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:ThemeStyleSheet"))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    if (p.Contains("Runtime"))
                    {
                        ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(p);
                        Debug.Log("[PocketPutt] Panel theme: " + p);
                        break;
                    }
                }
            }
            EditorUtility.SetDirty(ps);
        }

        static void CreateScene()
        {
            Directory.CreateDirectory("Assets/_PocketPutt/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.49f, 0.75f, 0.35f);
            camGo.AddComponent<AudioListener>();
            var app = new GameObject("Pocket Putt");
            app.AddComponent<PocketPutt.Game.GameApp>();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
#endif
