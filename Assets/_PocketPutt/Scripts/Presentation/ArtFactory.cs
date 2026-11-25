using System.Collections.Generic;
using PocketPutt.Sim;
using UnityEngine;

namespace PocketPutt.Presentation
{
    public struct ThemePalette
    {
        public Color turfA, turfB, railSide, railTop, hedge, ground, groundFar, accent, sand, ice;
        public Color sky;
        public float railSmooth;
    }

    /// <summary>
    /// Procedural textures and materials. Shader variants are guaranteed in builds because every material is
    /// cloned from a template asset in Resources/Materials (created by the editor asset builder).
    /// </summary>
    public static class ArtFactory
    {
        static Material tplLit, tplUnlit, tplTransparent, tplParticle;
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        static List<Object> scope;

        /// <summary>Everything created between Begin/EndScope is added to 'sink' so a level can destroy it on unload.</summary>
        public static void BeginScope(List<Object> sink) { scope = sink; }
        public static void EndScope() { scope = null; }
        public static T Track<T>(T o) where T : Object { scope?.Add(o); return o; }

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly int Metallic = Shader.PropertyToID("_Metallic");

        public static ThemePalette Palette(Theme t)
        {
            switch (t)
            {
                case Theme.Dunes:
                    return new ThemePalette
                    {
                        turfA = H("5DAE55"), turfB = H("52A04C"), railSide = H("B07A4A"), railTop = H("D9A566"),
                        hedge = H("6E9B4B"), ground = H("E8CB8F"), groundFar = H("D9B06E"), accent = H("E56B3A"),
                        sand = H("F0D9A0"), ice = H("BFE3F0"), sky = H("F6E3B8"), railSmooth = 0.25f
                    };
                case Theme.Glacier:
                    return new ThemePalette
                    {
                        turfA = H("5FB3A3"), turfB = H("54A596"), railSide = H("7EA9C9"), railTop = H("C5E4F5"),
                        hedge = H("5C9A94"), ground = H("E4F0F7"), groundFar = H("BCD6E6"), accent = H("3FA7E0"),
                        sand = H("EBD6A0"), ice = H("B5E3F5"), sky = H("D8ECF7"), railSmooth = 0.55f
                    };
                case Theme.Workshop:
                    return new ThemePalette
                    {
                        turfA = H("4C9A57"), turfB = H("428D4E"), railSide = H("59616D"), railTop = H("F0B93A"),
                        hedge = H("3B4650"), ground = H("8D929A"), groundFar = H("6F747C"), accent = H("E8442E"),
                        sand = H("DDBF86"), ice = H("B5E3F5"), sky = H("A8AEB6"), railSmooth = 0.6f
                    };
                case Theme.Arena:
                    return new ThemePalette
                    {
                        turfA = H("3F9E58"), turfB = H("379150"), railSide = H("F1ECE0"), railTop = H("E7B94A"),
                        hedge = H("2E6F46"), ground = H("2E5C45"), groundFar = H("1F3F31"), accent = H("D8A524"),
                        sand = H("E6C98E"), ice = H("B5E3F5"), sky = H("22392F"), railSmooth = 0.5f
                    };
                default:
                    return new ThemePalette
                    {
                        turfA = H("63B14E"), turfB = H("58A546"), railSide = H("7E4F2E"), railTop = H("B07B47"),
                        hedge = H("2F7D3A"), ground = H("7DBE5A"), groundFar = H("5E9F46"), accent = H("F2B134"),
                        sand = H("EAD08F"), ice = H("B5E3F5"), sky = H("BFE3F2"), railSmooth = 0.2f
                    };
            }
        }

        public static Color H(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }

        static void LoadTemplates()
        {
            if (tplLit != null) return;
            tplLit = Resources.Load<Material>("Materials/_Lit");
            tplUnlit = Resources.Load<Material>("Materials/_Unlit");
            tplTransparent = Resources.Load<Material>("Materials/_UnlitTransparent");
            tplParticle = Resources.Load<Material>("Materials/_Particle");
            Debug.Log($"[PocketPutt] templates lit={tplLit?.shader?.name} supported={tplLit?.shader?.isSupported} unlit={tplUnlit?.shader?.name} pipeline={UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name}");
            if (tplLit == null || tplUnlit == null || tplTransparent == null || tplParticle == null)
                Debug.LogError("[PocketPutt] Material templates missing. Run Pocket Putt > Build Game Assets.");
        }

        public static Material Lit(Color c, Texture tex = null, float smooth = 0.25f, float metal = 0f)
        {
            LoadTemplates();
            var m = Track(new Material(tplLit));
            m.SetColor(BaseColor, c);
            if (tex != null) m.SetTexture(BaseMap, tex);
            m.SetFloat(Smoothness, smooth);
            m.SetFloat(Metallic, metal);
            return m;
        }

        public static Material Unlit(Color c, Texture tex = null)
        {
            LoadTemplates();
            var m = Track(new Material(tplUnlit));
            m.SetColor(BaseColor, c);
            if (tex != null) m.SetTexture(BaseMap, tex);
            return m;
        }

        public static Material Transparent(Color c, Texture tex = null)
        {
            LoadTemplates();
            var m = Track(new Material(tplTransparent));
            m.SetColor(BaseColor, c);
            if (tex != null) m.SetTexture(BaseMap, tex);
            return m;
        }

        public static Material Particle(Color c, Texture tex = null)
        {
            LoadTemplates();
            var m = Track(new Material(tplParticle));
            m.SetColor(BaseColor, c);
            if (tex != null) m.SetTexture(BaseMap, tex);
            return m;
        }

        // ---- Procedural textures --------------------------------------------------------------

        static Texture2D Make(string key, int w, int h, System.Func<float, float, Color> f, bool mips = true,
            TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            if (textures.TryGetValue(key, out Texture2D cached) && cached != null) return cached;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mips) { wrapMode = wrap, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            tex.SetPixels32(px);
            tex.Apply(mips, true);
            tex.name = key;
            textures[key] = tex;
            return tex;
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        /// <summary>Mowed turf: alternating stripes along Z (v). One texture tile = two stripes.</summary>
        public static Texture2D Turf(Color a, Color b)
        {
            string key = "turf2_" + ColorUtility.ToHtmlStringRGB(a) + ColorUtility.ToHtmlStringRGB(b);
            return Make(key, 256, 256, (u, v) =>
            {
                Color baseC = v < 0.5f ? a : b;
                float edge = Mathf.Min(Mathf.Abs(v - 0.5f), Mathf.Min(v, 1f - v));
                int ix = (int)(u * 256), iy = (int)(v * 256);
                float n = Hash(ix, iy) * 0.08f - 0.04f + (Hash(ix / 4, iy / 4) - 0.5f) * 0.05f + (ValueNoise(u * 4f, v * 4f, 4) - 0.5f) * 0.07f;
                float soft = Mathf.SmoothStep(0.97f, 1f, 0.97f + edge * 6f);
                Color c = baseC * (1f + n) * soft;
                c.a = 1f;
                return c;
            });
        }

        public static Texture2D Sand(Color tint)
        {
            return Make("sand2_" + ColorUtility.ToHtmlStringRGB(tint), 256, 256, (u, v) =>
            {
                int ix = (int)(u * 256), iy = (int)(v * 256);
                float grain = (Hash(ix, iy) - 0.5f) * 0.14f + (Hash(ix / 2, iy / 2) - 0.5f) * 0.05f;
                float dune = (ValueNoise(u * 3f, v * 3f, 3) - 0.5f) * 0.10f;
                float ripple = Mathf.Sin((v * 6f + Mathf.Sin(u * 8f * Mathf.PI) * 0.08f) * Mathf.PI * 2f) * 0.045f;
                Color c = tint * (1f + grain + ripple * 0.8f + dune);
                c.a = 1f;
                return c;
            });
        }

        public static Texture2D Ice(Color tint)
        {
            return Make("ice2_" + ColorUtility.ToHtmlStringRGB(tint), 256, 256, (u, v) =>
            {
                float cloud = ValueNoise(u * 4f, v * 4f, 4) * 0.5f + ValueNoise(u * 8f, v * 8f, 8) * 0.25f - 0.37f;
                // thin frost veins: where a warped sine crosses zero
                float warp = ValueNoise(u * 6f, v * 6f, 6) * 0.35f;
                float vein = Mathf.Abs(Mathf.Sin((u * 3f + v * 2f + warp) * Mathf.PI * 2f));
                float cracks = Mathf.Clamp01(1f - vein * 40f) * 0.22f;
                float streak = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin((u - v) * Mathf.PI * 2f * 2f + warp * 3f)) * 12f) * 0.10f;
                Color c = tint * (1f + cloud * 0.35f) + Color.white * (cracks + streak);
                c.a = 1f;
                return c;
            });
        }

        public static Texture2D Wood(Color tint)
        {
            // grain runs along the rail: u = along the wall, v = across the profile
            return Make("wood2_" + ColorUtility.ToHtmlStringRGB(tint), 128, 128, (u, v) =>
            {
                float rings = Mathf.Sin((v * 5f + ValueNoise(u * 3f, v * 3f, 3) * 0.7f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                float fine = (Hash((int)(u * 128), (int)(v * 128)) - 0.5f) * 0.05f;
                Color c = tint * (0.93f + rings * 0.1f + fine);
                c.a = 1f;
                return c;
            });
        }

        public static Texture2D Noise(string key, Color a, Color b, float cells)
        {
            return Make(key, 256, 256, (u, v) =>
            {
                float n = 0f, amp = 0.5f, f = cells;
                for (int o = 0; o < 4; o++)
                {
                    n += amp * ValueNoise(u * f, v * f, (int)f);
                    amp *= 0.5f; f *= 2f;
                }
                Color c = Color.Lerp(a, b, Mathf.Clamp01(n * 1.3f));
                c.a = 1f;
                return c;
            });
        }

        static float ValueNoise(float x, float y, int period)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            xf = xf * xf * (3f - 2f * xf); yf = yf * yf * (3f - 2f * yf);
            int p = Mathf.Max(1, period);
            float a = Hash(Mod(xi, p), Mod(yi, p)), b = Hash(Mod(xi + 1, p), Mod(yi, p));
            float c = Hash(Mod(xi, p), Mod(yi + 1, p)), d = Hash(Mod(xi + 1, p), Mod(yi + 1, p));
            return Mathf.Lerp(Mathf.Lerp(a, b, xf), Mathf.Lerp(c, d, xf), yf);
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        /// <summary>Radial soft blob (white, alpha falloff) for shadows and glows.</summary>
        public static Texture2D SoftDisc()
        {
            return Make("softdisc", 64, 64, (u, v) =>
            {
                float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                return new Color(1f, 1f, 1f, a);
            }, true, TextureWrapMode.Clamp);
        }

        /// <summary>Vertical alpha gradient used for contact shadows along walls (v: 0 at wall, 1 far).</summary>
        public static Texture2D EdgeShade()
        {
            return Make("edgeshade", 4, 64, (u, v) =>
            {
                float a = Mathf.Clamp01(1f - v);
                a = a * a;
                return new Color(0f, 0f, 0f, a);
            }, false, TextureWrapMode.Clamp);
        }

        public static Texture2D Ball()
        {
            return Make("ball2", 256, 128, (u, v) =>
            {
                Color c = new Color(0.97f, 0.97f, 0.95f);
                // dimples
                float gx = u * 36f, gy = v * 18f;
                float dx = gx - Mathf.Round(gx), dy = gy - Mathf.Round(gy);
                if ((gy * 0.5f - Mathf.Floor(gy * 0.5f)) > 0.5f) dx = (gx + 0.5f) - Mathf.Round(gx + 0.5f);
                float dimple = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.2f);
                c *= 1f - dimple * 0.12f;
                // orange band + small dot so the roll is readable
                if (Mathf.Abs(v - 0.5f) < 0.07f) c = Color.Lerp(c, H("F26B3A"), 0.95f);
                if ((u - 0.25f) * (u - 0.25f) * 4f + (v - 0.72f) * (v - 0.72f) < 0.012f || (u - 0.75f) * (u - 0.75f) * 4f + (v - 0.28f) * (v - 0.28f) < 0.012f) c = H("F26B3A");
                c.a = 1f;
                return c;
            });
        }

        /// <summary>Cup interior seen from above: dark floor, lit far wall crescent at the top (v high), soft falloff to the lip.</summary>
        public static Texture2D HoleInterior()
        {
            return Make("hole_interior", 128, 128, (u, v) =>
            {
                float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
                float r = Mathf.Sqrt(x * x + y * y);
                float floorTone = Mathf.Lerp(0.05f, 0.24f, Mathf.SmoothStep(0.25f, 1f, r));
                Color c = new Color(0.55f, 0.65f, 0.5f) * floorTone;
                // far (top) wall catches light; fades toward the sides and the near wall is hidden by the lip
                float facing = Mathf.Clamp01(y / Mathf.Max(r, 1e-3f) * 0.9f + 0.1f);
                float band = Mathf.SmoothStep(0.55f, 0.95f, r) * (1f - Mathf.SmoothStep(0.97f, 1f, r));
                float lit = band * facing * (0.8f + 0.2f * Mathf.Clamp01(-x));
                c += new Color(0.42f, 0.50f, 0.32f) * lit;
                c.a = 1f;
                return c;
            }, true, TextureWrapMode.Clamp);
        }

        /// <summary>Soft inward falloff for zone edges: opaque at v=0 (the border), clear at v=1, with slight noise so the edge is not a ruler line.</summary>
        public static Texture2D ZoneEdge()
        {
            return Make("zoneedge", 32, 64, (u, v) =>
            {
                float a = Mathf.Clamp01(1f - v);
                a = a * a * (3f - 2f * a);
                a *= 0.85f + (ValueNoise(u * 8f, v * 2f, 8) - 0.5f) * 0.5f;
                return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }, false, TextureWrapMode.Clamp);
        }

        public static Texture2D Plain() => Make("white", 4, 4, (u, v) => Color.white, false);

        public static Material Cached(string key, System.Func<Material> factory)
        {
            if (materials.TryGetValue(key, out Material m) && m != null) return m;
            List<Object> prev = scope;
            scope = null; // cached materials outlive any level
            m = factory();
            scope = prev;
            materials[key] = m;
            return m;
        }
    }
}
