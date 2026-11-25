using System;
using System.Collections.Generic;
using PocketPutt.Game;
using UnityEngine;

namespace PocketPutt.UI
{
    /// <summary>
    /// Presentation-only placement of one cup's 10 map nodes on its art, in normalised picture coordinates (x right, y down).
    /// Never serialised into the save and never read by gameplay: Progress/Campaign stay the source of truth for state.
    /// </summary>
    public sealed class CupMapLayout
    {
        public string cupId;
        public string textureKey;              // Resources path of the prerendered map, null = procedural placeholder (H3 pending)
        public string thumbKey;                // Resources path of the destination vignette, null = procedural placeholder
        public float aspect = 3.2f;            // picture height / width
        public Vector2[] nodes = new Vector2[10];
        public Vector2[] path = Array.Empty<Vector2>();   // dense centre line, gate -> final
        public int[] pathNodeIndex = new int[10];          // index in 'path' closest to each node
        public bool placeholder;
    }

    [Serializable] sealed class LayoutNodeJson { public string id; public float x; public float y; }

    [Serializable]
    sealed class LayoutJson
    {
        public int version;
        public string texture;
        public int width;
        public int height;
        public LayoutNodeJson[] nodes;
        public float[] path_xy;
        public int[] path_node_index;
    }

    public static class HubLayouts
    {
        public const int NodeCount = 10;
        /// <summary>Reference logical width of the UI (PanelSettings: 1080 wide, match width).</summary>
        public const float RefWidth = 1080f;
        public const float MinNodeSeparation = 210f;    // logical px between node centres, so 140 px markers never touch

        static readonly CupMapLayout[] cache = new CupMapLayout[5];

        public static CupMapLayout For(int cupIndex)
        {
            cupIndex = Mathf.Clamp(cupIndex, 0, 4);
            if (cache[cupIndex] != null) return cache[cupIndex];
            CupDef cup = Campaign.Cups[cupIndex];
            CupMapLayout layout = LoadCup(cup, cupIndex);
            if (layout == null) layout = Placeholder(cup, cupIndex);
            cache[cupIndex] = layout;
            return layout;
        }

        public static void ClearCache() { for (int i = 0; i < cache.Length; i++) cache[i] = null; }

        /// <summary>Resources name of a cup's authored layout: the Garden keeps its H2 name, the other cups use their destination id (c2_dunes ...).</summary>
        public static string LayoutKey(int cupIndex) => cupIndex == 0 ? "Hub/garden_layout" : "Hub/" + DestinationArt.Names[cupIndex] + "_layout";

        static CupMapLayout LoadCup(CupDef cup, int cupIndex)
        {
            TextAsset ta = Resources.Load<TextAsset>(LayoutKey(cupIndex));
            if (ta == null) return null;
            LayoutJson j;
            try { j = JsonUtility.FromJson<LayoutJson>(ta.text); }
            catch (Exception e) { Debug.LogWarning("[PocketPutt] " + LayoutKey(cupIndex) + " unreadable: " + e.Message); return null; }
            if (j == null || j.nodes == null || j.nodes.Length != NodeCount || j.path_xy == null || j.path_xy.Length < 8) return null;
            var l = new CupMapLayout { cupId = cup.id, textureKey = "Hub/" + System.IO.Path.GetFileNameWithoutExtension(j.texture ?? (cupIndex == 0 ? "garden_map" : DestinationArt.Names[cupIndex] + "_map")), thumbKey = null, aspect = j.width > 0 ? (float)j.height / j.width : 3.2f };
            for (int i = 0; i < NodeCount; i++)
            {
                // the layout is keyed by level id so a re-ordered export can never silently rewire the map
                int idx = Array.IndexOf(cup.levelIds, j.nodes[i].id);
                if (idx < 0) return null;
                l.nodes[idx] = new Vector2(j.nodes[i].x, j.nodes[i].y);
            }
            int n = j.path_xy.Length / 2;
            l.path = new Vector2[n];
            for (int i = 0; i < n; i++) l.path[i] = new Vector2(j.path_xy[i * 2], j.path_xy[i * 2 + 1]);
            if (j.path_node_index != null && j.path_node_index.Length == NodeCount) Array.Copy(j.path_node_index, l.pathNodeIndex, NodeCount);
            else l.pathNodeIndex = NearestPathIndices(l);
            return l;
        }

        /// <summary>Procedural zig-zag used until the themed art of a cup exists (H3). Clearly flagged as placeholder.</summary>
        static CupMapLayout Placeholder(CupDef cup, int cupIndex)
        {
            Vector2[] guide =
            {
                new Vector2(0.46f, 0.890f), new Vector2(0.70f, 0.820f), new Vector2(0.30f, 0.750f), new Vector2(0.66f, 0.680f), new Vector2(0.36f, 0.605f),
                new Vector2(0.70f, 0.525f), new Vector2(0.32f, 0.445f), new Vector2(0.69f, 0.365f), new Vector2(0.27f, 0.285f), new Vector2(0.50f, 0.208f),
            };
            var l = new CupMapLayout { cupId = cup.id, aspect = 3.2f, placeholder = true };
            Array.Copy(guide, l.nodes, NodeCount);
            var ctrl = new List<Vector2> { new Vector2(0.5f, 0.972f) };
            ctrl.AddRange(guide);
            l.path = Sample(ctrl, l.aspect);
            l.pathNodeIndex = NearestPathIndices(l);
            return l;
        }

        public static int[] NearestPathIndices(CupMapLayout l)
        {
            var idx = new int[NodeCount];
            for (int i = 0; i < NodeCount; i++)
            {
                float best = float.MaxValue;
                for (int k = 0; k < l.path.Length; k++)
                {
                    Vector2 d = l.path[k] - l.nodes[i];
                    d.y *= l.aspect;
                    float m = d.sqrMagnitude;
                    if (m < best) { best = m; idx[i] = k; }
                }
            }
            return idx;
        }

        /// <summary>Uniform-ish centripetal-free Catmull-Rom through the control points (isotropic: y scaled by aspect).</summary>
        static Vector2[] Sample(List<Vector2> pts, float aspect)
        {
            var iso = new List<Vector2>();
            foreach (Vector2 p in pts) iso.Add(new Vector2(p.x, p.y * aspect));
            iso.Insert(0, 2 * iso[0] - iso[1]);
            iso.Add(2 * iso[iso.Count - 1] - iso[iso.Count - 2]);
            var outp = new List<Vector2>();
            for (int i = 1; i < iso.Count - 2; i++)
            {
                Vector2 p0 = iso[i - 1], p1 = iso[i], p2 = iso[i + 1], p3 = iso[i + 2];
                for (int s = 0; s < 24; s++)
                {
                    float t = s / 24f, t2 = t * t, t3 = t2 * t;
                    Vector2 q = 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
                    outp.Add(new Vector2(q.x, q.y / aspect));
                }
            }
            outp.Add(pts[pts.Count - 1]);
            return outp.ToArray();
        }

        /// <summary>Structural checks used by the unit checks: ids/order, bounds, separation and hit-box clearance in logical px.</summary>
        public static List<string> Validate(CupMapLayout l)
        {
            var errs = new List<string>();
            if (l.nodes == null || l.nodes.Length != NodeCount) { errs.Add("needs exactly 10 nodes"); return errs; }
            float h = RefWidth * l.aspect;
            for (int i = 0; i < NodeCount; i++)
            {
                Vector2 p = l.nodes[i];
                if (p.x < 0.12f || p.x > 0.88f) errs.Add($"node {i + 1} too close to the side ({p.x:0.00})");
                if (p.y < 0.10f || p.y > 0.90f) errs.Add($"node {i + 1} too close to the top/bottom ({p.y:0.00})");
                for (int j = i + 1; j < NodeCount; j++)
                {
                    float d = Vector2.Distance(new Vector2(p.x * RefWidth, p.y * h), new Vector2(l.nodes[j].x * RefWidth, l.nodes[j].y * h));
                    if (d < MinNodeSeparation) errs.Add($"nodes {i + 1} and {j + 1} are {d:0} px apart (< {MinNodeSeparation})");
                }
            }
            if (l.path == null || l.path.Length < 8) errs.Add("path polyline missing");
            else if (l.pathNodeIndex.Length != NodeCount) errs.Add("pathNodeIndex size");
            else for (int i = 1; i < NodeCount; i++) if (l.pathNodeIndex[i] <= l.pathNodeIndex[i - 1]) errs.Add($"path order broken at node {i + 1}");
            return errs;
        }
    }
}
