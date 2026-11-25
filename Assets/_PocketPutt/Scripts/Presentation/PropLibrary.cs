using System.Collections.Generic;
using PocketPutt.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketPutt.Presentation
{
    /// <summary>
    /// Scenery and trophy meshes authored in Blender (art-source/scripts/build_props.py), loaded from Resources.
    /// Materials are replaced at spawn time by themed URP materials, keyed by the material names in the FBX.
    /// </summary>
    public static class PropLibrary
    {
        static GameObject props, trophies;
        static bool loaded;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        // Largest dimension (metres) each prop should have at scale 1.
        static readonly Dictionary<string, float> TargetSize = new Dictionary<string, float>
        {
            ["pine"] = 1.2f, ["tree_round"] = 1.4f, ["bush"] = 0.62f, ["rock"] = 0.42f, ["cactus"] = 0.8f, ["crystal"] = 0.72f,
            ["crate"] = 0.5f, ["barrel"] = 0.6f, ["gear"] = 0.9f, ["lamp"] = 1.7f, ["flower"] = 0.30f, ["seat"] = 1.0f,
        };

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            props = Resources.Load<GameObject>("Models/PP_Props");
            trophies = Resources.Load<GameObject>("Models/PP_Trophies");
            if (props == null) Debug.LogWarning("[PocketPutt] PP_Props not found; using primitive scenery.");
        }

        public static bool HasProps { get { Load(); return props != null; } }
        public static bool HasTrophies { get { Load(); return trophies != null; } }

        static Transform Find(GameObject root, string objName)
        {
            if (root == null) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == objName) return t;
            return null;
        }

        /// <summary>Instantiates prop 'name' (pine, bush, ...) under parent; returns null if the library is missing.</summary>
        public static GameObject Spawn(string name, Transform parent, Theme theme, float scale = 1f)
        {
            Load();
            Transform src = Find(props, "prop_" + name);
            if (src == null) return null;
            var go = Object.Instantiate(src.gameObject, parent, false);
            go.name = name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            Normalize(go, TargetSize.TryGetValue(name, out float t) ? t : 1f, scale);
            ApplyMaterials(go, ArtFactory.Palette(theme), theme, name == "lamp" || name == "gear" ? false : true);
            return go;
        }

        public static GameObject SpawnTrophy(int index, Transform parent)
        {
            Load();
            Transform src = Find(trophies, "trophy_" + index);
            if (src == null) return null;
            var go = Object.Instantiate(src.gameObject, parent, false);
            go.name = "Trophy " + index;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            Normalize(go, 1.0f, 1f);
            ApplyMaterials(go, ArtFactory.Palette(Theme.Garden), Theme.Garden, false);
            return go;
        }

        static void Normalize(GameObject go, float targetMax, float scale)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            foreach (Renderer r in rends) b.Encapsulate(r.bounds);
            float max = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (max > 1e-4f) go.transform.localScale = Vector3.one * (targetMax / max * scale);
            // sit on the ground: lift so the lowest point is y = 0 in parent space
            b = rends[0].bounds;
            foreach (Renderer r in rends) b.Encapsulate(r.bounds);
            go.transform.localPosition += new Vector3(-(b.center.x - go.transform.position.x), -(b.min.y - go.transform.position.y), -(b.center.z - go.transform.position.z));
        }

        static void ApplyMaterials(GameObject go, ThemePalette pal, Theme theme, bool shadows)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                Material[] src = r.sharedMaterials;
                var dst = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    string n = src[i] != null ? src[i].name : "leaf";
                    int dot = n.IndexOf('.');
                    if (dot > 0) n = n.Substring(0, dot);
                    dst[i] = Resolve(n, pal, theme);
                }
                r.sharedMaterials = dst;
                r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
            }
        }

        // Prop materials are shared across courses, so they must not belong to a course's tracked scope:
        // that scope is destroyed when the course unloads, which left the next course holding dead (magenta) materials.
        static Material Resolve(string name, ThemePalette pal, Theme theme)
        {
            string key = name + "|" + theme;
            if (cache.TryGetValue(key, out Material m) && m != null) return m;
            m = ArtFactory.Cached("prop_" + key, () => CreateMaterial(name, pal, theme));
            cache[key] = m;
            return m;
        }

        static Material CreateMaterial(string name, ThemePalette pal, Theme theme)
        {
            Color c; float smooth = 0.25f, metal = 0f; bool unlit = false;
            switch (name)
            {
                case "leaf": c = pal.hedge * 1.05f; smooth = 0.08f; break;
                case "leaf_light": c = pal.hedge * 1.3f; smooth = 0.08f; break;
                case "trunk": c = ArtFactory.H("6E4A2C"); smooth = 0.1f; break;
                case "rock": c = theme == Theme.Dunes ? ArtFactory.H("B98D62") : theme == Theme.Glacier ? ArtFactory.H("E6F0F5") : theme == Theme.Workshop ? ArtFactory.H("767B84") : ArtFactory.H("A7A9A1"); smooth = 0.15f; break;
                case "cactus": c = ArtFactory.H("4F9D5B"); smooth = 0.1f; break;
                case "crystal": c = ArtFactory.H("8FD6F5"); smooth = 0.9f; metal = 0.1f; break;
                case "wood": c = ArtFactory.H("B98650"); smooth = 0.15f; break;
                case "wood_dark": c = ArtFactory.H("7C5530"); smooth = 0.15f; break;
                case "barrel": c = ArtFactory.H("D9533D"); smooth = 0.5f; metal = 0.2f; break;
                case "band": c = ArtFactory.H("3E4349"); smooth = 0.6f; metal = 0.6f; break;
                case "steel": c = ArtFactory.H("8B939D"); smooth = 0.65f; metal = 0.6f; break;
                case "yellow": c = ArtFactory.H("F2BB38"); smooth = 0.5f; metal = 0.3f; break;
                case "pole": c = ArtFactory.H("CBCFD2"); smooth = 0.5f; metal = 0.4f; break;
                case "glow": c = ArtFactory.H("FFF3C2"); unlit = true; break;
                case "petal": c = ArtFactory.H("F7C948"); smooth = 0.3f; break;
                case "flower_center": c = ArtFactory.H("8A4B22"); smooth = 0.2f; break;
                case "stem": c = ArtFactory.H("4C9A44"); smooth = 0.1f; break;
                case "seat": c = pal.accent; smooth = 0.3f; break;
                case "seat_dark": c = ArtFactory.H("3A4A42"); smooth = 0.2f; break;
                case "ball": c = ArtFactory.H("F8F8F2"); smooth = 0.7f; break;
                case "star": c = ArtFactory.H("FFD95A"); smooth = 0.7f; metal = 0.5f; break;
                default:
                    if (name.StartsWith("t") && name.Length > 2 && char.IsDigit(name[1]) && name[2] == '_')
                    {
                        int idx = name[1] - '0';
                        string part = name.Substring(3);
                        Color[] metals = { ArtFactory.H("D08C48"), ArtFactory.H("C6682F"), ArtFactory.H("D6DEE6"), ArtFactory.H("9AA5B1"), ArtFactory.H("FFC93A") };
                        Color[] accents = { ArtFactory.H("58C45B"), ArtFactory.H("F28C3C"), ArtFactory.H("6CC3F0"), ArtFactory.H("F2BC2E"), ArtFactory.H("D8233A") };
                        c = part == "accent" ? accents[idx] : part == "dark" ? metals[idx] * 0.55f : metals[idx];
                        smooth = part == "accent" ? 0.5f : 0.75f; metal = part == "accent" ? 0.1f : 0.55f;
                    }
                    else
                    {
                        Debug.LogWarning($"[PocketPutt] Prop material '{name}' has no theme mapping; using neutral grey.");
                        c = new Color(0.62f, 0.64f, 0.62f);
                    }
                    break;
            }
            return unlit ? ArtFactory.Unlit(c) : ArtFactory.Lit(c, null, smooth, metal);
        }
    }
}
