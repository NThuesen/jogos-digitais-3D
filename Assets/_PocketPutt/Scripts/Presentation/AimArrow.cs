using PocketPutt.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketPutt.Presentation
{
    /// <summary>Slingshot aim indicator: arrow whose length and colour show the power, over a faint track that ends in a tick at 100%. No trajectory preview.</summary>
    public sealed class AimArrow : MonoBehaviour
    {
        Mesh mesh;
        MeshRenderer rend;
        readonly Vector3[] v = new Vector3[18];
        readonly Color32[] c = new Color32[18];
        const float Y = 0.36f;

        public static AimArrow Create(Transform parent)
        {
            var go = new GameObject("Aim Arrow");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AimArrow>();
            a.Build(go);
            return a;
        }

        void Build(GameObject go)
        {
            mesh = new Mesh { name = "AimArrow" };
            mesh.MarkDynamic();
            // shaft: 4 verts (0..3), head: 3 verts (4..6), tail dot: 3 more (7..9), full-power track (10..13) and its end tick (14..17)
            var tris = new int[] { 10, 11, 12, 10, 12, 13, 14, 15, 16, 14, 16, 17, 0, 1, 2, 0, 2, 3, 4, 5, 6, 7, 8, 9 };
            for (int i = 0; i < v.Length; i++) v[i] = Vector3.zero;
            mesh.vertices = v;
            mesh.colors32 = c;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            rend = go.AddComponent<MeshRenderer>();
            rend.sharedMaterial = ArtFactory.Cached("arrow", () => ArtFactory.Particle(Color.white, ArtFactory.Plain()));
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.enabled = false;
        }

        public void Hide() { if (rend != null) rend.enabled = false; }

        public void Show(Vector2 origin, Vector2 dir, float power)
        {
            if (dir.sqrMagnitude < 1e-6f) { Hide(); return; }
            rend.enabled = true;
            dir.Normalize();
            Vector2 n = new Vector2(-dir.y, dir.x);
            float start = Tuning.BallRadius + 0.14f;
            float len = Mathf.Lerp(0.45f, 3.0f, Mathf.Pow(power, 0.9f));
            float head = Mathf.Min(0.42f, len * 0.5f);
            float wTail = 0.055f, wHead = 0.24f;

            Vector2 p0 = origin + dir * start;
            Vector2 pNeck = origin + dir * (start + len - head);
            Vector2 pTip = origin + dir * (start + len);

            Color low = new Color(1f, 0.97f, 0.7f), mid = new Color(1f, 0.78f, 0.25f), high = new Color(1f, 0.38f, 0.22f);
            Color col = power < 0.5f ? Color.Lerp(low, mid, power * 2f) : Color.Lerp(mid, high, (power - 0.5f) * 2f);
            Color tail = col; tail.a = 0.25f;
            Color head0 = col; head0.a = 1f;

            Set(0, p0 - n * wTail, tail); Set(1, p0 + n * wTail, tail);
            Set(2, pNeck + n * wTail * 1.5f, col); Set(3, pNeck - n * wTail * 1.5f, col);
            Set(4, pNeck + n * wHead, head0); Set(5, pTip, head0); Set(6, pNeck - n * wHead, head0);
            // small pull-back cap behind the ball
            Vector2 back = origin - dir * (Tuning.BallRadius + 0.1f);
            Color capc = col; capc.a = 0.5f;
            Set(7, back + n * 0.12f, capc); Set(8, origin - dir * (Tuning.BallRadius + 0.01f), capc); Set(9, back - n * 0.12f, capc);

            // where 100% ends: a faint guide track plus a crossbar, so the player sees how much pull is left
            float maxLen = 3.0f;
            float tw = 0.03f;
            Color track = Color.white; track.a = 0.22f;
            Color bar = power >= 0.98f ? high : Color.white; bar.a = power >= 0.98f ? 0.95f : 0.5f;
            Vector2 pMax = origin + dir * (start + maxLen);
            Set(10, p0 - n * tw, track); Set(11, p0 + n * tw, track);
            Set(12, pMax + n * tw, track); Set(13, pMax - n * tw, track);
            Vector2 pBar = pMax + dir * 0.12f;
            Vector2 pBar0 = pMax + dir * 0.02f;
            Set(14, pBar0 - n * 0.3f, bar); Set(15, pBar0 + n * 0.3f, bar);
            Set(16, pBar + n * 0.3f, bar); Set(17, pBar - n * 0.3f, bar);

            mesh.vertices = v;
            mesh.colors32 = c;
        }

        void Set(int i, Vector2 p, Color col)
        {
            v[i] = new Vector3(p.x, Y, p.y);
            c[i] = col;
        }
    }
}
