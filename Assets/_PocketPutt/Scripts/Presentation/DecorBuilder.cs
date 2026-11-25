using System.Collections.Generic;
using PocketPutt.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketPutt.Presentation
{
    /// <summary>Scatters themed scenery around a course (outside the floor), deterministic per level id.</summary>
    public static class DecorBuilder
    {
        struct Placed { public Vector2 p; public float r; }

        public static void Build(CourseView view)
        {
            LevelData level = view.level;
            ThemePalette pal = view.palette;
            var root = new GameObject("Decor").transform;
            root.SetParent(view.transform, false);

            // Prefer authored Blender props when present, otherwise primitives.
            var rng = new System.Random(level.id.GetHashCode());
            var placed = new List<Placed>();
            Rect b = level.bounds;
            float margin = 4.2f;
            float topMargin = 1.4f; // the band above the course sits under the HUD; keep it calm
            int target = 44;
            int guard = 0;
            while (placed.Count < target && guard++ < 2000)
            {
                Vector2 p = new Vector2(
                    Mathf.Lerp(b.xMin - margin, b.xMax + margin, (float)rng.NextDouble()),
                    Mathf.Lerp(b.yMin - margin, b.yMax + topMargin, (float)rng.NextDouble()));
                float dist = DistanceToFloor(level, p);
                if (dist < 0.75f) continue;
                float kind = (float)rng.NextDouble();
                float radius = 0.3f;
                // keep tall props away from the camera-facing side of the course
                bool near = dist < 1.8f;
                GameObject prop = MakeProp(level.theme, pal, kind, near, rng, out radius);
                if (prop == null) continue;
                bool clash = false;
                foreach (Placed o in placed) if ((o.p - p).magnitude < o.r + radius) { clash = true; break; }
                if (clash) { Object.Destroy(prop); continue; }
                prop.transform.SetParent(root, false);
                prop.transform.localPosition = new Vector3(p.x, CourseGround, p.y);
                prop.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                placed.Add(new Placed { p = p, r = radius });
            }
        }

        const float CourseGround = -0.12f;

        static float DistanceToFloor(LevelData level, Vector2 p)
        {
            float best = float.MaxValue;
            foreach (RectF r in level.rects)
            {
                float dx = Mathf.Max(Mathf.Max(r.x0 - p.x, 0), p.x - r.x1);
                float dz = Mathf.Max(Mathf.Max(r.z0 - p.y, 0), p.y - r.z1);
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dz * dz));
            }
            return best;
        }

        static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, bool shadow = false)
        {
            var g = GameObject.CreatePrimitive(t);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return g;
        }

        static Material Mat(string key, Color c, float smooth = 0.1f) =>
            ArtFactory.Cached("decor_" + key + ColorUtility.ToHtmlStringRGB(c), () => ArtFactory.Lit(c, ArtFactory.Plain(), smooth));

        static GameObject MakeProp(Theme theme, ThemePalette pal, float k, bool near, System.Random rng, out float radius)
        {
            var root = new GameObject("Prop").transform;
            float s = 0.8f + (float)rng.NextDouble() * 0.6f;
            if (PropLibrary.HasProps)
            {
                GameObject lib = LibraryProp(root, theme, k, near, s, rng, out radius);
                if (lib != null) return lib;
            }
            switch (theme)
            {
                case Theme.Dunes: return Dunes(root, pal, k, near, s, out radius);
                case Theme.Glacier: return Glacier(root, pal, k, near, s, out radius);
                case Theme.Workshop: return Workshop(root, pal, k, near, s, out radius);
                case Theme.Arena: return Arena(root, pal, k, near, s, rng, out radius);
                default: return Garden(root, pal, k, near, s, rng, out radius);
            }
        }

        /// <summary>Blender-authored scenery. Returns null for kinds that are better as simple shapes (mounds).</summary>
        static GameObject LibraryProp(Transform root, Theme theme, float k, bool near, float s, System.Random rng, out float radius)
        {
            string name; radius = 0.4f * s; float scale = s * 0.95f; int count = 1;
            switch (theme)
            {
                case Theme.Dunes:
                    if (k < 0.3f) { name = "cactus"; radius = 0.3f * s; }
                    else if (k < 0.55f && !near) { radius = 1f; return null; }
                    else if (k < 0.82f) { name = "rock"; radius = 0.35f * s; }
                    else { name = "bush"; radius = 0.35f * s; scale *= 0.8f; }
                    break;
                case Theme.Glacier:
                    if (k < 0.35f) { name = "crystal"; radius = 0.35f * s; }
                    else if (k < 0.65f && !near) { name = "pine"; radius = 0.6f * s; }
                    else if (k < 0.85f) { name = "rock"; radius = 0.35f * s; }
                    else { radius = 1f; return null; }
                    break;
                case Theme.Workshop:
                    if (k < 0.28f) { name = "crate"; radius = 0.4f * s; }
                    else if (k < 0.52f) { name = "barrel"; radius = 0.35f * s; }
                    else if (k < 0.78f) { name = "gear"; radius = 0.6f * s; }
                    else if (!near) { name = "lamp"; radius = 0.3f; scale = 1f; }
                    else { name = "crate"; radius = 0.4f * s; }
                    break;
                case Theme.Arena:
                    if (k < 0.5f) { name = "seat"; radius = 0.6f * s; }
                    else if (k < 0.8f && !near) { name = "lamp"; radius = 0.3f; scale = 1f; }
                    else { name = "bush"; radius = 0.4f * s; }
                    break;
                default:
                    if (k < 0.38f) { name = "bush"; radius = 0.4f * s; }
                    else if (k < 0.6f && !near) { name = rng.NextDouble() < 0.5 ? "pine" : "tree_round"; radius = 0.7f * s; scale = s; }
                    else if (k < 0.84f) { name = "flower"; radius = 0.28f; count = 3; scale = 1f; }
                    else { name = "rock"; radius = 0.35f * s; }
                    break;
            }
            for (int i = 0; i < count; i++)
            {
                GameObject g = PropLibrary.Spawn(name, root, theme, scale);
                if (g == null) return null;
                if (count > 1) g.transform.localPosition += new Vector3((float)rng.NextDouble() - 0.5f, 0, (float)rng.NextDouble() - 0.5f) * 0.35f;
                g.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
            }
            return root.gameObject;
        }

        static GameObject Garden(Transform root, ThemePalette pal, float k, bool near, float s, System.Random rng, out float radius)
        {
            Color bush = pal.hedge * (0.9f + (float)rng.NextDouble() * 0.3f);
            if (k < 0.42f)
            {
                radius = 0.45f * s;
                Prim(PrimitiveType.Sphere, root, new Vector3(0, 0.25f * s, 0), new Vector3(0.9f, 0.62f, 0.9f) * s, Mat("bush", bush), true);
                Prim(PrimitiveType.Sphere, root, new Vector3(0.28f * s, 0.18f * s, 0.1f * s), new Vector3(0.55f, 0.42f, 0.55f) * s, Mat("bush", bush * 1.08f));
            }
            else if (k < 0.62f && !near)
            {
                radius = 0.75f * s;
                Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.55f * s, 0), new Vector3(0.18f, 0.55f, 0.18f) * s, Mat("trunk", new Color(0.42f, 0.28f, 0.17f)), true);
                Prim(PrimitiveType.Sphere, root, new Vector3(0, 1.35f * s, 0), new Vector3(1.3f, 1.2f, 1.3f) * s, Mat("leaf", bush * 1.05f), true);
                Prim(PrimitiveType.Sphere, root, new Vector3(0.25f * s, 1.85f * s, 0.1f * s), new Vector3(0.85f, 0.8f, 0.85f) * s, Mat("leaf2", bush * 1.2f));
            }
            else if (k < 0.85f)
            {
                radius = 0.18f;
                Color[] cols = { H("F6D14A"), H("F28DA8"), H("FFFFFF"), H("F2784B"), H("B58CF0") };
                Color c = cols[rng.Next(cols.Length)];
                for (int i = 0; i < 3; i++)
                {
                    Vector3 o = new Vector3((float)rng.NextDouble() - 0.5f, 0, (float)rng.NextDouble() - 0.5f) * 0.28f;
                    Prim(PrimitiveType.Cylinder, root, o + new Vector3(0, 0.08f, 0), new Vector3(0.015f, 0.08f, 0.015f), Mat("stem", H("3E8A3A")));
                    Prim(PrimitiveType.Sphere, root, o + new Vector3(0, 0.17f, 0), Vector3.one * 0.1f, Mat("flower", c, 0.3f));
                }
            }
            else
            {
                radius = 0.35f * s;
                Prim(PrimitiveType.Sphere, root, new Vector3(0, 0.1f * s, 0), new Vector3(0.7f, 0.42f, 0.55f) * s, Mat("rock", H("A5A79F"), 0.2f), true);
            }
            return root.gameObject;
        }

        static GameObject Dunes(Transform root, ThemePalette pal, float k, bool near, float s, out float radius)
        {
            if (k < 0.3f)
            {
                radius = 0.4f;
                Material m = Mat("cactus", H("4C9A57"));
                Prim(PrimitiveType.Capsule, root, new Vector3(0, 0.4f * s, 0), new Vector3(0.22f, 0.4f, 0.22f) * s, m, true);
                Prim(PrimitiveType.Capsule, root, new Vector3(0.22f * s, 0.5f * s, 0), new Vector3(0.12f, 0.2f, 0.12f) * s, m);
                Prim(PrimitiveType.Capsule, root, new Vector3(-0.2f * s, 0.38f * s, 0), new Vector3(0.12f, 0.17f, 0.12f) * s, m);
            }
            else if (k < 0.6f && !near)
            {
                radius = 1.0f * s;
                Prim(PrimitiveType.Sphere, root, new Vector3(0, 0.0f, 0), new Vector3(2.2f, 0.7f, 1.6f) * s, Mat("dune", pal.sand * 0.98f, 0.02f));
            }
            else if (k < 0.8f)
            {
                radius = 0.4f * s;
                Prim(PrimitiveType.Sphere, root, new Vector3(0, 0.12f * s, 0), new Vector3(0.7f, 0.45f, 0.6f) * s, Mat("sandrock", H("B88A5E"), 0.15f), true);
            }
            else
            {
                radius = 0.3f;
                Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.07f, 0), new Vector3(0.25f, 0.07f, 0.25f), Mat("pot", H("C86A3A")), true);
            }
            return root.gameObject;
        }

        static GameObject Glacier(Transform root, ThemePalette pal, float k, bool near, float s, out float radius)
        {
            if (k < 0.4f)
            {
                radius = 0.35f * s;
                Material m = Mat("crystal", H("9FD8F2"), 0.9f);
                Prim(PrimitiveType.Cube, root, new Vector3(0, 0.35f * s, 0), new Vector3(0.22f, 0.9f, 0.22f) * s, m, true).transform.localRotation = Quaternion.Euler(8, 30, 6);
                Prim(PrimitiveType.Cube, root, new Vector3(0.22f * s, 0.2f * s, 0.05f), new Vector3(0.16f, 0.55f, 0.16f) * s, m).transform.localRotation = Quaternion.Euler(-10, 10, -18);
            }
            else if (k < 0.7f && !near)
            {
                radius = 0.6f * s;
                Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.3f * s, 0), new Vector3(0.12f, 0.3f, 0.12f) * s, Mat("trunk2", H("5B4636")));
                for (int i = 0; i < 3; i++)
                    Prim(PrimitiveType.Sphere, root, new Vector3(0, (0.7f + i * 0.45f) * s, 0), new Vector3(1.1f - i * 0.3f, 0.5f, 1.1f - i * 0.3f) * s, Mat("pine", H("3F7F66")), true);
            }
            else
            {
                radius = 0.7f * s;
                Prim(PrimitiveType.Sphere, root, new Vector3(0, 0.05f, 0), new Vector3(1.5f, 0.55f, 1.2f) * s, Mat("snow", H("F4FAFD"), 0.15f));
            }
            return root.gameObject;
        }

        static GameObject Workshop(Transform root, ThemePalette pal, float k, bool near, float s, out float radius)
        {
            if (k < 0.3f)
            {
                radius = 0.4f;
                Prim(PrimitiveType.Cube, root, new Vector3(0, 0.2f * s, 0), new Vector3(0.5f, 0.4f, 0.5f) * s, Mat("crate", H("B9854E")), true);
            }
            else if (k < 0.55f)
            {
                radius = 0.35f;
                Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.3f * s, 0), new Vector3(0.45f, 0.3f, 0.45f) * s, Mat("barrel", H("D9533D"), 0.4f), true);
            }
            else if (k < 0.8f)
            {
                radius = 0.55f * s;
                Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.05f, 0), new Vector3(1.0f, 0.05f, 1.0f) * s, Mat("gear", H("737B86"), 0.5f));
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f * Mathf.Deg2Rad;
                    Prim(PrimitiveType.Cube, root, new Vector3(Mathf.Cos(a) * 0.52f, 0.05f, Mathf.Sin(a) * 0.52f) * s, new Vector3(0.18f, 0.1f, 0.18f) * s, Mat("gear", H("737B86"), 0.5f)).transform.localRotation = Quaternion.Euler(0, -i * 45f, 0);
                }
            }
            else
            {
                radius = 0.3f;
                Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.45f * s, 0), new Vector3(0.12f, 0.45f, 0.12f) * s, Mat("pipe", H("F0B93A"), 0.5f), true);
            }
            return root.gameObject;
        }

        static GameObject Arena(Transform root, ThemePalette pal, float k, bool near, float s, System.Random rng, out float radius)
        {
            if (k < 0.5f)
            {
                radius = 0.5f;
                Color[] cols = { H("E7B94A"), H("F1ECE0"), H("3F7F66"), H("C6533C") };
                Prim(PrimitiveType.Cube, root, new Vector3(0, 0.3f * s, 0), new Vector3(0.9f, 0.6f, 0.5f) * s, Mat("seat", cols[rng.Next(cols.Length)]), true);
            }
            else if (k < 0.8f && !near)
            {
                radius = 0.4f;
                Prim(PrimitiveType.Cylinder, root, new Vector3(0, 1.2f * s, 0), new Vector3(0.1f, 1.2f, 0.1f) * s, Mat("lightpole", H("D8DAD6"), 0.6f), true);
                Prim(PrimitiveType.Sphere, root, new Vector3(0, 2.5f * s, 0), Vector3.one * 0.35f * s, ArtFactory.Cached("glow", () => ArtFactory.Unlit(H("FFF3C2"))));
            }
            else
            {
                radius = 0.3f;
                Prim(PrimitiveType.Sphere, root, new Vector3(0, 0.1f, 0), new Vector3(0.6f, 0.3f, 0.5f) * s, Mat("bushA", pal.hedge), true);
            }
            return root.gameObject;
        }

        static Color H(string hex) => ArtFactory.H(hex);
    }
}
