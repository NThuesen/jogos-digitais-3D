using System;
using UnityEngine;

namespace PocketPutt.UI
{
    /// <summary>
    /// Runtime layout of one prerendered macro destination (the maquette of a cup on the "Campeonatos" screen), exported next to its
    /// texture by art-source/scripts/postprocess_destinations.py. All numbers are normalised to the cropped texture (x right, y down).
    /// Presentation only: never saved, never read by gameplay.
    /// </summary>
    public sealed class DestinationArt
    {
        public static readonly string[] Names = { "c1_garden", "c2_dunes", "c3_glacial", "c4_workshop", "c5_masters" };

        public string textureKey;                       // Resources path, e.g. "Hub/Dest/c2_dunes"
        public float aspect = 0.75f;                    // texture height / width
        public Vector2 entrance, exit, hero;            // where the route meets the maquette / leaves it / the focal landmark
        public Vector4[] bands = Array.Empty<Vector4>(); // (y0, y1, x0, x1) silhouette spans, top to bottom

        [Serializable]
        sealed class Json
        {
            public int version;
            public string texture;
            public int width, height;
            public float aspect;
            public float[] entrance, exit, hero;
        }

        /// <summary>Loads the layout of a cup's destination; null when the art is not in the build (the overview then falls back to a vignette).</summary>
        public static DestinationArt Load(int cupIndex)
        {
            if (cupIndex < 0 || cupIndex >= Names.Length) return null;
            TextAsset ta = Resources.Load<TextAsset>("Hub/Dest/" + Names[cupIndex]);
            if (ta == null) return null;
            Json j;
            try { j = JsonUtility.FromJson<Json>(ta.text); }
            catch (Exception e) { Debug.LogWarning("[PocketPutt] destination layout unreadable: " + e.Message); return null; }
            if (j == null || j.entrance == null || j.entrance.Length < 2 || j.exit == null || j.exit.Length < 2 || j.hero == null || j.hero.Length < 2 || j.width <= 0) return null;
            var d = new DestinationArt
            {
                textureKey = "Hub/Dest/" + j.texture,
                aspect = j.aspect > 0f ? j.aspect : (float)j.height / j.width,
                entrance = new Vector2(j.entrance[0], j.entrance[1]),
                exit = new Vector2(j.exit[0], j.exit[1]),
                hero = new Vector2(j.hero[0], j.hero[1]),
                bands = ParseBands(ta.text),                   // JsonUtility cannot read nested arrays
            };
            return d;
        }

        /// <summary>Extracts "bands":[[y0,y1,x0,x1],...] by hand.</summary>
        static Vector4[] ParseBands(string text)
        {
            int k = text.IndexOf("\"bands\"", StringComparison.Ordinal);
            if (k < 0) return Array.Empty<Vector4>();
            int start = text.IndexOf('[', k);
            if (start < 0) return Array.Empty<Vector4>();
            var list = new System.Collections.Generic.List<Vector4>();
            int i = start + 1;
            while (i < text.Length)
            {
                int open = text.IndexOf('[', i);
                int close = text.IndexOf(']', i);
                if (close < 0) break;
                if (open < 0 || close < open) break;               // closing bracket of the outer array
                string[] n = text.Substring(open + 1, close - open - 1).Split(',');
                if (n.Length == 4)
                {
                    float F(string s) => float.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture);
                    list.Add(new Vector4(F(n[0]), F(n[1]), F(n[2]), F(n[3])));
                }
                i = close + 1;
            }
            return list.ToArray();
        }

        /// <summary>True when a point (normalised to the texture) is on the maquette; 'pad' widens each silhouette span (fraction of the width).</summary>
        public bool Hit(Vector2 n, float pad)
        {
            if (n.y < 0f || n.y > 1f) return false;
            for (int i = 0; i < bands.Length; i++)
            {
                Vector4 b = bands[i];
                if (n.y >= b.x - 0.004f && n.y <= b.y + 0.004f && n.x >= b.z - pad && n.x <= b.w + pad) return true;
            }
            return false;
        }
    }
}
