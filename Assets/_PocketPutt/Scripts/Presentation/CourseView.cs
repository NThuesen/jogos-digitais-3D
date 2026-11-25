using System.Collections.Generic;
using PocketPutt.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketPutt.Presentation
{
    /// <summary>
    /// Builds the visible miniature course (turf, rails, hedges, hole, flag, decor) from LevelData.
    /// Pure presentation: gameplay uses LevelData/BallSim only.
    /// </summary>
    public sealed class CourseView : MonoBehaviour
    {
        public const float RailHeight = 0.30f;
        const float RailBottom = -0.12f;
        const float BlockTop = 0.20f;

        public LevelData level;
        [System.NonSerialized] public ThemePalette palette;
        public Transform flagPole;
        public Transform flagCloth;
        public Transform holeRoot;
        readonly List<MoverView> movers = new List<MoverView>();
        readonly List<Object> owned = new List<Object>();   // meshes/materials created for this course
        float flagRetract;

        sealed class MoverView { public Obstacle obstacle; public Transform t; public Vector3 baseCenter; }

        public static CourseView Build(LevelData level, Transform parent)
        {
            var go = new GameObject("Course " + level.id);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<CourseView>();
            view.level = level;
            view.palette = ArtFactory.Palette(level.theme);
            ArtFactory.BeginScope(view.owned);
            view.BuildAll();
            ArtFactory.EndScope();
            return view;
        }

        void OnDestroy()
        {
            foreach (Object o in owned) if (o != null) Destroy(o);
            owned.Clear();
        }

        void BuildAll()
        {
            BuildGround();
            BuildFloor();
            BuildZones();
            BuildWater();
            BuildMoverTracks();
            BuildContactShade();
            BuildRails();
            BuildPosts();
            BuildBlocks();
            BuildTunnels();
            BuildMovers();
            BuildHole();
            BuildStartMark();
            DecorBuilder.Build(this);
        }

        GameObject Part(string name, Mesh mesh, Material[] mats, bool shadows, bool receive = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = receive;
            return go;
        }

        // ---- ground & floor --------------------------------------------------------------------

        void BuildGround()
        {
            Rect b = level.bounds;
            Vector2 c = b.center;
            float half = 30f;
            var mb = new MeshBuilder();
            Vector3 up = Vector3.up;
            float y = RailBottom;
            mb.Quad(0, new Vector3(c.x - half, y, c.y - half), new Vector3(c.x - half, y, c.y + half),
                new Vector3(c.x + half, y, c.y + half), new Vector3(c.x + half, y, c.y - half), up,
                new Vector2(0, 0), new Vector2(0, 10), new Vector2(10, 10), new Vector2(10, 0));
            Texture2D tex = ArtFactory.Noise("ground2_" + level.theme, palette.ground, Color.Lerp(palette.ground, palette.groundFar, 0.5f), 6f);
            Part("Ground", mb.ToMesh("Ground"), new[] { ArtFactory.Lit(Color.white, tex, 0.05f) }, false);
        }

        void BuildFloor()
        {
            var mb = new MeshBuilder();
            const float inv = 1f / 1.2f;
            foreach (RectF r in level.cells)
                mb.Quad(0, new Vector3(r.x0, 0, r.z0), new Vector3(r.x0, 0, r.z1), new Vector3(r.x1, 0, r.z1), new Vector3(r.x1, 0, r.z0),
                    Vector3.up, new Vector2(r.x0 * inv, r.z0 * inv), new Vector2(r.x0 * inv, r.z1 * inv),
                    new Vector2(r.x1 * inv, r.z1 * inv), new Vector2(r.x1 * inv, r.z0 * inv));
            Texture2D turf = ArtFactory.Turf(palette.turfA, palette.turfB);
            Part("Floor", mb.ToMesh("Floor"), new[] { ArtFactory.Lit(Color.white, turf, 0.12f) }, false);
        }

        void BuildZones()
        {
            foreach (Zone z in level.zones)
            {
                var mb = new MeshBuilder();
                Vector2[] p = z.poly;
                for (int i = 1; i + 1 < p.Length; i++)
                {
                    int a = mb.AddVertex(new Vector3(p[0].x, 0.004f, p[0].y), Vector3.up, p[0] * 0.8f);
                    int b = mb.AddVertex(new Vector3(p[i].x, 0.004f, p[i].y), Vector3.up, p[i] * 0.8f);
                    int c = mb.AddVertex(new Vector3(p[i + 1].x, 0.004f, p[i + 1].y), Vector3.up, p[i + 1] * 0.8f);
                    mb.AddTri(0, a, b, c);
                }
                Material m = z.surface == Surface.Sand
                    ? ArtFactory.Lit(Color.white, ArtFactory.Sand(palette.sand), 0.05f)
                    : ArtFactory.Lit(Color.white, ArtFactory.Ice(palette.ice), 0.92f);
                Part("Zone " + z.surface, mb.ToMesh("Zone"), new[] { m }, false);

                // lowered-bed look: soft dark gradient running inward from every border, plus a faint crisp line
                Vector2 cen = Vector2.zero;
                foreach (Vector2 q in p) cen += q;
                cen /= p.Length;
                var soft = new MeshBuilder();
                var line = new MeshBuilder();
                const float inset = 0.16f;
                for (int i = 0; i < p.Length; i++)
                {
                    Vector2 a = p[i], b = p[(i + 1) % p.Length];
                    Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x);
                    if (Vector2.Dot(n, cen - (a + b) * 0.5f) < 0f) n = -n; // n points into the zone
                    soft.Quad(0, V(a.x, 0.006f, a.y), V(b.x, 0.006f, b.y), V(b.x + n.x * inset, 0.006f, b.y + n.y * inset), V(a.x + n.x * inset, 0.006f, a.y + n.y * inset),
                        Vector3.up, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                    Vector2 a0 = a - d * 0.02f, b0 = b + d * 0.02f, w = n * 0.022f;
                    line.Quad(0, V(a0.x - w.x, 0.0065f, a0.y - w.y), V(b0.x - w.x, 0.0065f, b0.y - w.y), V(b0.x + w.x, 0.0065f, b0.y + w.y), V(a0.x + w.x, 0.0065f, a0.y + w.y),
                        Vector3.up, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                }
                bool sand = z.surface == Surface.Sand;
                Color shade = sand ? new Color(0.50f, 0.34f, 0.12f, 0.55f) : new Color(0.45f, 0.65f, 0.80f, 0.50f);
                Part("Zone Shade", soft.ToMesh("ZoneShade"), new[] { ArtFactory.Transparent(shade, ArtFactory.ZoneEdge()) }, false, false);
                Color edge = sand ? new Color(0.55f, 0.40f, 0.18f, 0.35f) : new Color(1f, 1f, 1f, 0.55f);
                Part("Zone Edge", line.ToMesh("ZoneEdge"), new[] { ArtFactory.Transparent(edge, ArtFactory.Plain()) }, false, false);
            }
        }

        /// <summary>
        /// Lakes drawn from the same RectF data the sim uses (no collider, no trigger). Overlapping rects are merged on a
        /// compressed grid so every cell is emitted once: no seams, no z-fighting, no doubled transparency.
        /// </summary>
        void BuildWater()
        {
            if (level.water.Count == 0) return;
            var xs = new SortedSet<float>(); var zs = new SortedSet<float>();
            foreach (RectF r in level.water) { xs.Add(r.x0); xs.Add(r.x1); zs.Add(r.z0); zs.Add(r.z1); }
            float[] X = new float[xs.Count]; xs.CopyTo(X);
            float[] Z = new float[zs.Count]; zs.CopyTo(Z);
            int nx = X.Length - 1, nz = Z.Length - 1;
            var wet = new bool[nx + 2, nz + 2];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float cx = (X[i] + X[i + 1]) * 0.5f, cz = (Z[j] + Z[j + 1]) * 0.5f;
                    foreach (RectF r in level.water)
                        if (cx > r.x0 && cx < r.x1 && cz > r.z0 && cz < r.z1) { wet[i + 1, j + 1] = true; break; }
                }

            const float y = 0.009f, inset = 0.17f, foam = 0.05f;   // above zone layers (<= 0.0065): a lake hides ice/sand under it
            var surface = new MeshBuilder(); var shade = new MeshBuilder(); var rim = new MeshBuilder();
            const float inv = 1f / 1.6f;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    if (!wet[i + 1, j + 1]) continue;
                    float x0 = X[i], x1 = X[i + 1], z0 = Z[j], z1 = Z[j + 1];
                    surface.Quad(0, V(x0, y, z0), V(x0, y, z1), V(x1, y, z1), V(x1, y, z0), Vector3.up,
                        new Vector2(x0 * inv, z0 * inv), new Vector2(x0 * inv, z1 * inv), new Vector2(x1 * inv, z1 * inv), new Vector2(x1 * inv, z0 * inv));
                    // shore strips on every side that borders dry ground; inner gradient + crisp foam line
                    for (int side = 0; side < 4; side++)
                    {
                        int di = side == 0 ? -1 : side == 1 ? 1 : 0, dj = side == 2 ? -1 : side == 3 ? 1 : 0;
                        if (wet[i + 1 + di, j + 1 + dj]) continue;
                        Vector2 a, b, n;
                        if (side == 0) { a = new Vector2(x0, z0); b = new Vector2(x0, z1); n = Vector2.right; }
                        else if (side == 1) { a = new Vector2(x1, z1); b = new Vector2(x1, z0); n = Vector2.left; }
                        else if (side == 2) { a = new Vector2(x1, z0); b = new Vector2(x0, z0); n = Vector2.up; }
                        else { a = new Vector2(x0, z1); b = new Vector2(x1, z1); n = Vector2.down; }
                        shade.Quad(0, V(a.x, y + 0.001f, a.y), V(b.x, y + 0.001f, b.y), V(b.x + n.x * inset, y + 0.001f, b.y + n.y * inset), V(a.x + n.x * inset, y + 0.001f, a.y + n.y * inset),
                            Vector3.up, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                        rim.Quad(0, V(a.x, y + 0.002f, a.y), V(b.x, y + 0.002f, b.y), V(b.x + n.x * foam, y + 0.002f, b.y + n.y * foam), V(a.x + n.x * foam, y + 0.002f, a.y + n.y * foam),
                            Vector3.up, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                    }
                }
            Texture2D tex = ArtFactory.Noise("water", new Color(0.16f, 0.50f, 0.86f), new Color(0.26f, 0.64f, 0.95f), 5f);
            Part("Water", surface.ToMesh("Water"), new[] { ArtFactory.Lit(Color.white, tex, 0.9f) }, false, false);
            Part("Water Shade", shade.ToMesh("WaterShade"), new[] { ArtFactory.Transparent(new Color(0.05f, 0.22f, 0.5f, 0.55f), ArtFactory.ZoneEdge()) }, false, false);
            Part("Water Rim", rim.ToMesh("WaterRim"), new[] { ArtFactory.Transparent(new Color(0.9f, 0.97f, 1f, 0.8f), ArtFactory.Plain()) }, false, false);
        }

        void BuildContactShade()
        {
            var mb = new MeshBuilder();
            const float w = 0.38f;
            foreach (Obstacle o in level.obstacles)
            {
                if (o.kind == ObstacleKind.Mover) continue;
                Capsule c = o.shape;
                float reach = c.r + w;
                Vector2 d = c.b - c.a;
                if (d.sqrMagnitude < 1e-6f) continue; // posts get their own radial decal in BuildPosts
                Vector2 dir = d.normalized, n = new Vector2(-dir.y, dir.x);
                const int segs = 6;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 off = n * side;
                    for (int k = 0; k < segs; k++)
                    {
                        float t0 = (float)k / segs, t1 = (float)(k + 1) / segs;
                        Vector2 s0 = Vector2.Lerp(c.a, c.b, t0), s1 = Vector2.Lerp(c.a, c.b, t1);
                        // shade only the playable side, and never let it leak onto the lawn outside the floor
                        if (!level.InsideFloor((s0 + s1) * 0.5f + off * 0.35f)) continue;
                        Vector2 p0 = s0 + off * c.r * 0.6f, p1 = s1 + off * c.r * 0.6f;
                        Vector2 q0 = s0 + off * reach, q1 = s1 + off * reach;
                        mb.Quad(0, V(p0.x, 0.003f, p0.y), V(p1.x, 0.003f, p1.y), V(q1.x, 0.003f, q1.y), V(q0.x, 0.003f, q0.y),
                            Vector3.up, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                    }
                }
            }
            Material m = ArtFactory.Transparent(new Color(0.05f, 0.1f, 0.03f, 0.38f), ArtFactory.EdgeShade());
            Part("Contact Shade", mb.ToMesh("Shade"), new[] { m }, false, false);
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        // ---- rails -----------------------------------------------------------------------------

        void BuildRails()
        {
            var mb = new MeshBuilder(2);
            foreach (Obstacle o in level.obstacles)
                if (o.kind == ObstacleKind.Boundary || o.kind == ObstacleKind.Block || o.kind == ObstacleKind.Rail)
                    AddRail(mb, o.shape.a, o.shape.b, RailHeight, o.shape.r);
            Part("Rails", mb.ToMesh("Rails"), RailMaterials(), true);
        }

        Material[] RailMaterials()
        {
            return new[]
            {
                ArtFactory.Lit(Color.white, ArtFactory.Wood(palette.railSide), palette.railSmooth * 0.6f, level.theme == Theme.Workshop ? 0.5f : 0f),
                ArtFactory.Lit(Color.white, ArtFactory.Wood(palette.railTop), palette.railSmooth, level.theme == Theme.Workshop ? 0.3f : 0f)
            };
        }

        /// <summary>Rounded-top wall segment between a and b plus a dome at each end.</summary>
        public static void AddRail(MeshBuilder mb, Vector2 a, Vector2 b, float height, float halfWidth)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            Vector2 dir = d / len, nrm = new Vector2(-dir.y, dir.x);
            Vector3 side = new Vector3(nrm.x, 0, nrm.y);
            Vector3 pa = new Vector3(a.x, 0, a.y), pb = new Vector3(b.x, 0, b.y);
            float w = halfWidth, hc = height - w;
            const int arc = 6;
            var prof = new List<(float o, float y, Vector2 n, int sub)>();
            prof.Add((-w, RailBottom, new Vector2(-1, 0), 0));
            for (int i = 0; i <= arc; i++)
            {
                float th = Mathf.PI * i / arc;
                float cos = Mathf.Cos(th), sin = Mathf.Sin(th);
                prof.Add((-w * cos, hc + w * sin, new Vector2(-cos, sin), i == 0 ? 0 : 1));
            }
            prof.Add((w, RailBottom, new Vector2(1, 0), 0));
            float uvLen = len * 1.4f;
            for (int i = 0; i + 1 < prof.Count; i++)
            {
                var p0 = prof[i]; var p1 = prof[i + 1];
                Vector3 n0 = side * p0.n.x + Vector3.up * p0.n.y, n1 = side * p1.n.x + Vector3.up * p1.n.y;
                int sub = i == 0 || i == prof.Count - 2 ? 0 : 1;
                int v0 = mb.AddVertex(pa + side * p0.o + Vector3.up * p0.y, n0, new Vector2(0, i * 0.15f));
                int v1 = mb.AddVertex(pb + side * p0.o + Vector3.up * p0.y, n0, new Vector2(uvLen, i * 0.15f));
                int v2 = mb.AddVertex(pb + side * p1.o + Vector3.up * p1.y, n1, new Vector2(uvLen, (i + 1) * 0.15f));
                int v3 = mb.AddVertex(pa + side * p1.o + Vector3.up * p1.y, n1, new Vector2(0, (i + 1) * 0.15f));
                mb.AddQuad(sub, v0, v1, v2, v3);
            }
            AddDome(mb, pa, w, height, 0);
            AddDome(mb, pb, w, height, 0);
        }

        /// <summary>Cylinder from the bottom to (height - r) capped with a hemisphere. topSub is the cap submesh.</summary>
        public static void AddDome(MeshBuilder mb, Vector3 center, float r, float height, int sideSub, int capSub = 1, float uvScale = 1f)
        {
            const int seg = 14, rings = 5;
            float hc = height - r;
            // cylinder wall
            for (int s = 0; s < seg; s++)
            {
                float a0 = Mathf.PI * 2f * s / seg, a1 = Mathf.PI * 2f * (s + 1) / seg;
                Vector3 n0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), n1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                int v0 = mb.AddVertex(center + n0 * r + Vector3.up * RailBottom, n0, new Vector2(s * uvScale / seg, 0));
                int v1 = mb.AddVertex(center + n1 * r + Vector3.up * RailBottom, n1, new Vector2((s + 1) * uvScale / seg, 0));
                int v2 = mb.AddVertex(center + n1 * r + Vector3.up * hc, n1, new Vector2((s + 1) * uvScale / seg, 0.15f));
                int v3 = mb.AddVertex(center + n0 * r + Vector3.up * hc, n0, new Vector2(s * uvScale / seg, 0.15f));
                mb.AddQuad(sideSub, v0, v1, v2, v3);
            }
            for (int ring = 0; ring < rings; ring++)
            {
                float p0 = Mathf.PI * 0.5f * ring / rings, p1 = Mathf.PI * 0.5f * (ring + 1) / rings;
                for (int s = 0; s < seg; s++)
                {
                    float a0 = Mathf.PI * 2f * s / seg, a1 = Mathf.PI * 2f * (s + 1) / seg;
                    int v0 = DomeV(mb, center, r, hc, a0, p0, uvScale), v1 = DomeV(mb, center, r, hc, a1, p0, uvScale);
                    int v2 = DomeV(mb, center, r, hc, a1, p1, uvScale), v3 = DomeV(mb, center, r, hc, a0, p1, uvScale);
                    mb.AddQuad(capSub, v0, v1, v2, v3);
                }
            }
        }

        static int DomeV(MeshBuilder mb, Vector3 c, float r, float hc, float az, float el, float uvScale)
        {
            Vector3 n = new Vector3(Mathf.Cos(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(az) * Mathf.Cos(el));
            return mb.AddVertex(c + n * r + Vector3.up * hc, n, new Vector2(az / (Mathf.PI * 2f) * uvScale, 0.15f + el * 0.1f));
        }

        // ---- posts, blocks ---------------------------------------------------------------------

        void BuildPosts()
        {
            if (level.posts.Count == 0) return;
            var mb = new MeshBuilder(2);
            foreach (var p in level.posts)
            {
                float r = p.Value;
                float h = r * 1.9f;
                AddDome(mb, new Vector3(p.Key.x, 0, p.Key.y), r, h, 0, 1, 2f);
            }
            var shade = new MeshBuilder();
            foreach (var p in level.posts) AddDisc(shade, p.Key, p.Value + 0.34f, 0.003f, 24);
            Part("Post Shade", shade.ToMesh("PostShade"), new[] { ArtFactory.Transparent(new Color(0.03f, 0.08f, 0.02f, 0.42f), ArtFactory.SoftDisc()) }, false, false);
            Texture2D tex = ArtFactory.Noise("hedge_" + level.theme, palette.hedge * 0.82f, palette.hedge * 1.18f, 10f);
            Material m = ArtFactory.Lit(Color.white, tex, 0.1f);
            Part("Posts", mb.ToMesh("Posts"), new[] { m, m }, true);
        }

        void BuildBlocks()
        {
            if (level.blocks.Count == 0) return;
            var mb = new MeshBuilder();
            foreach (Vector2[] poly in level.blocks)
            {
                // centroid fan keeps non-convex-but-star-shaped hedges valid
                Vector2 c = Vector2.zero;
                foreach (Vector2 v in poly) c += v;
                c /= poly.Length;
                int hub = mb.AddVertex(V(c.x, BlockTop, c.y), Vector3.up, c * 0.6f);
                for (int i = 0; i < poly.Length; i++)
                {
                    Vector2 p0 = poly[i], p1 = poly[(i + 1) % poly.Length];
                    int a = mb.AddVertex(V(p0.x, BlockTop, p0.y), Vector3.up, p0 * 0.6f);
                    int b = mb.AddVertex(V(p1.x, BlockTop, p1.y), Vector3.up, p1 * 0.6f);
                    mb.AddTri(0, hub, a, b);
                }
            }
            Texture2D tex = ArtFactory.Noise("hedgetop_" + level.theme, palette.hedge * 0.8f, palette.hedge * 1.2f, 14f);
            Part("Blocks", mb.ToMesh("Blocks"), new[] { ArtFactory.Lit(Color.white, tex, 0.08f) }, true);
        }

        // ---- tunnels, movers -------------------------------------------------------------------

        void BuildTunnels()
        {
            foreach (Portal p in level.portals)
            {
                MakeMouth(p.from, p.entryOpen, p.radius, true);
                MakeMouth(p.to, p.exitDir, p.radius, false);
            }
        }

        /// <summary>Stone arch whose open side faces 'open'; the ball enters/leaves through the open side.</summary>
        void MakeMouth(Vector2 pos, Vector2 open, float radius, bool entry)
        {
            var mb = new MeshBuilder(2);
            Vector2 f = open.sqrMagnitude > 0 ? open.normalized : Vector2.up;
            Vector2 side = new Vector2(-f.y, f.x);
            float half = radius + 0.16f;
            float depth = 0.62f;
            Vector2 backCenter = pos - f * depth;
            AddRail(mb, pos + side * half, backCenter + side * half, 0.52f, 0.12f);
            AddRail(mb, pos - side * half, backCenter - side * half, 0.52f, 0.12f);
            AddRail(mb, backCenter + side * half, backCenter - side * half, 0.52f, 0.12f);
            Color body = entry ? palette.accent * 0.85f : palette.accent;
            Material[] mats =
            {
                ArtFactory.Lit(body * 0.8f, ArtFactory.Wood(palette.railSide), 0.35f),
                ArtFactory.Lit(body, ArtFactory.Wood(palette.railTop), 0.45f)
            };
            Part(entry ? "Tunnel In" : "Tunnel Out", mb.ToMesh("Mouth"), mats, true);

            var disc = new MeshBuilder();
            AddDisc(disc, pos - f * (depth * 0.5f), radius * 0.95f, 0.012f, 24);
            Color floor = entry ? new Color(0.06f, 0.04f, 0.1f) : new Color(0.22f, 0.18f, 0.3f);
            Part("Tunnel Floor", disc.ToMesh("Disc"), new[] { ArtFactory.Unlit(floor) }, false);

            // chevrons on the floor show the direction of travel through the mouth
            var chev = new MeshBuilder();
            Vector2 c0 = pos + (entry ? -f : f) * 0.0f;
            AddChevron(chev, pos + f * 0.42f, entry ? -f : f, 0.17f, 0.012f);
            AddChevron(chev, pos + f * 0.66f, entry ? -f : f, 0.17f, 0.012f);
            Part("Tunnel Arrows", chev.ToMesh("Arrows"), new[] { ArtFactory.Transparent(new Color(1f, 1f, 1f, 0.55f), ArtFactory.Plain()) }, false, false);
        }

        static void AddChevron(MeshBuilder mb, Vector2 c, Vector2 dir, float size, float y)
        {
            Vector2 n = new Vector2(-dir.y, dir.x);
            Vector2 tip = c + dir * size, l = c - dir * size * 0.2f + n * size, r = c - dir * size * 0.2f - n * size;
            Vector2 il = c - dir * size * 0.55f + n * size * 0.55f, ir = c - dir * size * 0.55f - n * size * 0.55f, itip = c + dir * size * 0.3f;
            int a = mb.AddVertex(V(tip.x, y, tip.y), Vector3.up, Vector2.zero), b = mb.AddVertex(V(l.x, y, l.y), Vector3.up, Vector2.zero);
            int c1 = mb.AddVertex(V(il.x, y, il.y), Vector3.up, Vector2.zero), d = mb.AddVertex(V(itip.x, y, itip.y), Vector3.up, Vector2.zero);
            mb.AddQuad(0, a, b, c1, d);
            int e = mb.AddVertex(V(r.x, y, r.y), Vector3.up, Vector2.zero), f2 = mb.AddVertex(V(ir.x, y, ir.y), Vector3.up, Vector2.zero);
            mb.AddQuad(0, a, d, f2, e);
        }

        /// <summary>
        /// Floor decals for the swept area of every mover (slider travel hull, spinner circle) so the cycle can be read at a
        /// glance before it arrives. Purely visual: the sim never reads it.
        /// </summary>
        void BuildMoverTracks()
        {
            foreach (Obstacle o in level.movers)
            {
                var fill = new MeshBuilder(); var edge = new MeshBuilder();
                const float y = 0.0045f;
                if (o.motion == MotionKind.Slide)
                {
                    Vector2 axis = o.shape.b - o.shape.a;
                    Vector2 n = axis.sqrMagnitude > 1e-6f ? new Vector2(-axis.y, axis.x).normalized : Vector2.up;
                    Vector2 w = n * (o.shape.r + 0.03f), d = o.slideDir.normalized * o.slideTravel;
                    var pts = new List<Vector2> { o.shape.a - w, o.shape.a + w, o.shape.b - w, o.shape.b + w,
                        o.shape.a - w + d, o.shape.a + w + d, o.shape.b - w + d, o.shape.b + w + d };
                    List<Vector2> hull = ConvexHull(pts);
                    Vector2 cen = Vector2.zero; foreach (Vector2 p in hull) cen += p; cen /= hull.Count;
                    for (int i = 0; i < hull.Count; i++)
                    {
                        Vector2 p = hull[i], q = hull[(i + 1) % hull.Count];
                        int a = fill.AddVertex(V(cen.x, y, cen.y), Vector3.up, Vector2.zero);
                        int b = fill.AddVertex(V(p.x, y, p.y), Vector3.up, Vector2.zero);
                        int c = fill.AddVertex(V(q.x, y, q.y), Vector3.up, Vector2.zero);
                        fill.AddTri(0, a, b, c);
                        Vector2 e = (q - p).normalized, en = new Vector2(-e.y, e.x) * 0.025f;
                        edge.Quad(0, V(p.x - en.x, y + 0.0005f, p.y - en.y), V(q.x - en.x, y + 0.0005f, q.y - en.y), V(q.x + en.x, y + 0.0005f, q.y + en.y), V(p.x + en.x, y + 0.0005f, p.y + en.y),
                            Vector3.up, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
                    }
                }
                else if (o.motion == MotionKind.Spin)
                {
                    float rr = Mathf.Max((o.shape.a - o.pivot).magnitude, (o.shape.b - o.pivot).magnitude) + o.shape.r;
                    AddDisc(fill, o.pivot, rr, y, 40);
                    AddDisc(edge, o.pivot, rr, y + 0.0005f, 40, rr - 0.05f);
                }
                else continue;
                Color tint = palette.accent;
                Part("Mover Track", fill.ToMesh("MoverTrack"), new[] { ArtFactory.Transparent(new Color(tint.r, tint.g, tint.b, 0.16f), ArtFactory.Plain()) }, false, false);
                Part("Mover Track Edge", edge.ToMesh("MoverTrackEdge"), new[] { ArtFactory.Transparent(new Color(tint.r, tint.g, tint.b, 0.55f), ArtFactory.Plain()) }, false, false);
            }
        }

        static List<Vector2> ConvexHull(List<Vector2> pts)
        {
            var p = new List<Vector2>(pts);
            p.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var h = new List<Vector2>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = h.Count;
                foreach (Vector2 q in p)
                {
                    while (h.Count >= start + 2 && Cross(h[h.Count - 2], h[h.Count - 1], q) <= 1e-6f) h.RemoveAt(h.Count - 1);
                    h.Add(q);
                }
                h.RemoveAt(h.Count - 1);
                p.Reverse();
            }
            return h;
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        void BuildMovers()
        {
            foreach (Obstacle o in level.movers)
            {
                var mb = new MeshBuilder(2);
                // built around origin so the transform follows the pose (capsule a..b relative to its midpoint)
                Vector2 mid = (o.shape.a + o.shape.b) * 0.5f;
                AddRail(mb, o.shape.a - mid, o.shape.b - mid, 0.34f, o.shape.r);
                Material[] mats =
                {
                    ArtFactory.Lit(palette.accent * 0.85f, ArtFactory.Plain(), 0.4f, 0.2f),
                    ArtFactory.Lit(palette.accent, ArtFactory.Plain(), 0.5f, 0.2f)
                };
                GameObject g = Part("Mover", mb.ToMesh("Mover"), mats, true);
                movers.Add(new MoverView { obstacle = o, t = g.transform, baseCenter = new Vector3(mid.x, 0, mid.y) });
                if (o.motion == MotionKind.Spin)
                {
                    var hub = new MeshBuilder(2);
                    AddDome(hub, new Vector3(o.pivot.x, 0, o.pivot.y), 0.22f, 0.4f, 0, 1);
                    Part("Hub", hub.ToMesh("Hub"), new[] { mats[0], mats[1] }, true);
                }
            }
        }

        public void SyncMovers(float time)
        {
            foreach (MoverView m in movers)
            {
                Capsule c = m.obstacle.PoseAt(time);
                Vector2 mid = (c.a + c.b) * 0.5f;
                Vector2 d = c.b - c.a;
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                Vector2 baseDir = m.obstacle.shape.b - m.obstacle.shape.a;
                float baseAng = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
                m.t.position = new Vector3(mid.x, 0, mid.y);
                m.t.rotation = Quaternion.Euler(0, -(ang - baseAng), 0);
            }
        }

        // ---- hole, flag, start ----------------------------------------------------------------

        static void AddDisc(MeshBuilder mb, Vector2 c, float r, float y, int seg, float inner = 0f)
        {
            for (int i = 0; i < seg; i++)
            {
                float a0 = Mathf.PI * 2f * i / seg, a1 = Mathf.PI * 2f * (i + 1) / seg;
                Vector3 o0 = V(c.x + Mathf.Cos(a0) * r, y, c.y + Mathf.Sin(a0) * r), o1 = V(c.x + Mathf.Cos(a1) * r, y, c.y + Mathf.Sin(a1) * r);
                if (inner <= 0f)
                {
                    int h = mb.AddVertex(V(c.x, y, c.y), Vector3.up, new Vector2(0.5f, 0.5f));
                    int a = mb.AddVertex(o0, Vector3.up, new Vector2(0.5f + Mathf.Cos(a0) * 0.5f, 0.5f + Mathf.Sin(a0) * 0.5f));
                    int b = mb.AddVertex(o1, Vector3.up, new Vector2(0.5f + Mathf.Cos(a1) * 0.5f, 0.5f + Mathf.Sin(a1) * 0.5f));
                    mb.AddTri(0, h, a, b);
                }
                else
                {
                    Vector3 i0 = V(c.x + Mathf.Cos(a0) * inner, y, c.y + Mathf.Sin(a0) * inner), i1 = V(c.x + Mathf.Cos(a1) * inner, y, c.y + Mathf.Sin(a1) * inner);
                    int v0 = mb.AddVertex(i0, Vector3.up, Vector2.zero), v1 = mb.AddVertex(i1, Vector3.up, Vector2.zero);
                    int v2 = mb.AddVertex(o1, Vector3.up, Vector2.one), v3 = mb.AddVertex(o0, Vector3.up, Vector2.one);
                    mb.AddQuad(0, v0, v1, v2, v3);
                }
            }
        }

        void BuildHole()
        {
            holeRoot = new GameObject("Hole").transform;
            holeRoot.SetParent(transform, false);
            holeRoot.localPosition = new Vector3(level.hole.x, 0, level.hole.y);

            // soft contact shadow so the cup sits in the turf instead of floating on it
            var halo = new MeshBuilder();
            AddDisc(halo, Vector2.zero, 0.55f, 0.003f, 28);
            Part("Hole Halo", halo.ToMesh("HoleHalo"), new[] { ArtFactory.Transparent(new Color(0.02f, 0.07f, 0.02f, 0.30f), ArtFactory.SoftDisc()) }, false, false).transform.SetParent(holeRoot, false);

            // interior: gradient disc with a lit far wall, so it reads as a shaft rather than a black sticker
            var dark = new MeshBuilder();
            AddDisc(dark, Vector2.zero, 0.272f, 0.006f, 32);
            var darkGo = Part("Hole Dark", dark.ToMesh("HoleDark"), new[] { ArtFactory.Unlit(Color.white, ArtFactory.HoleInterior()) }, false);
            darkGo.transform.SetParent(holeRoot, false);

            // bevelled lip (outer slope, flat crest at the capture radius, inner slope)
            var rim = new MeshBuilder();
            (float r, float y, Vector2 n)[] prof =
            {
                (0.352f, 0.000f, new Vector2(0.75f, 0.66f)), (0.326f, 0.019f, new Vector2(0.40f, 0.92f)),
                (0.300f, 0.025f, new Vector2(0f, 1f)), (0.278f, 0.016f, new Vector2(-0.55f, 0.83f)), (0.264f, 0.002f, new Vector2(-0.93f, 0.37f)),
            };
            const int seg = 32;
            for (int i = 0; i + 1 < prof.Length; i++)
                for (int s = 0; s < seg; s++)
                {
                    float a0 = Mathf.PI * 2f * s / seg, a1 = Mathf.PI * 2f * (s + 1) / seg;
                    int v0 = RimV(rim, prof[i], a0), v1 = RimV(rim, prof[i], a1), v2 = RimV(rim, prof[i + 1], a1), v3 = RimV(rim, prof[i + 1], a0);
                    rim.AddQuad(0, v0, v1, v2, v3);
                }
            var rimGo = Part("Hole Rim", rim.ToMesh("HoleRim"), new[] { ArtFactory.Lit(new Color(0.95f, 0.88f, 0.66f), ArtFactory.Plain(), 0.5f, 0.15f) }, false);
            rimGo.transform.SetParent(holeRoot, false);

            // flag: planted exactly on the capture centre (like a real flagstick) so it always reads as centred on the cup, on every
            // course and wall layout; the pole is thin enough to leave the opening readable and it retracts when the ball nears
            flagPole = new GameObject("Flag").transform;
            flagPole.SetParent(holeRoot, false);
            flagPole.localPosition = Vector3.zero;
            const float poleH = 0.85f;
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(pole.GetComponent<Collider>());
            pole.transform.SetParent(flagPole, false);
            pole.transform.localScale = new Vector3(0.03f, poleH * 0.5f, 0.03f);
            pole.transform.localPosition = new Vector3(0, poleH * 0.5f, 0);
            pole.GetComponent<MeshRenderer>().sharedMaterial = ArtFactory.Lit(new Color(0.97f, 0.96f, 0.92f), ArtFactory.Plain(), 0.5f);
            pole.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;

            // wavy swallow-less pennant, double sided; widest at the pole, tapering to a tip
            var tri = new MeshBuilder();
            const int cols = 6;
            const float len = 0.46f, halfH = 0.17f;
            float cy = poleH - 0.04f - halfH;
            for (int face = 0; face < 2; face++)
            {
                float zs = face == 0 ? -1f : 1f;
                int[] top = new int[cols + 1], bot = new int[cols + 1];
                for (int i = 0; i <= cols; i++)
                {
                    float t = (float)i / cols;
                    float x = 0.015f + t * len;
                    float h = halfH * (1f - t);
                    float wave = Mathf.Sin(t * 7f) * 0.035f * t;
                    float slope = Mathf.Cos(t * 7f) * 0.035f * 7f / len;
                    Vector3 nrm = new Vector3(-slope * zs, 0f, zs).normalized;
                    top[i] = tri.AddVertex(V(x, cy + h, wave), nrm, new Vector2(t, 1));
                    bot[i] = tri.AddVertex(V(x, cy - h, wave), nrm, new Vector2(t, 0));
                }
                for (int i = 0; i < cols; i++) { tri.AddTri(0, top[i], bot[i], bot[i + 1]); tri.AddTri(0, top[i], bot[i + 1], top[i + 1]); }
            }
            GameObject clothGo = Part("Flag Cloth", tri.ToMesh("Pennant"), new[] { ArtFactory.Lit(palette.accent, ArtFactory.Plain(), 0.3f) }, true);
            clothGo.transform.SetParent(flagPole, false);
            flagCloth = clothGo.transform;

            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(cap.GetComponent<Collider>());
            cap.transform.SetParent(flagPole, false);
            cap.transform.localScale = Vector3.one * 0.065f;
            cap.transform.localPosition = new Vector3(0, poleH + 0.005f, 0);
            cap.GetComponent<MeshRenderer>().sharedMaterial = ArtFactory.Lit(new Color(0.95f, 0.8f, 0.4f), ArtFactory.Plain(), 0.7f, 0.5f);
        }

        static int RimV(MeshBuilder mb, (float r, float y, Vector2 n) p, float az)
        {
            float c = Mathf.Cos(az), s = Mathf.Sin(az);
            return mb.AddVertex(V(c * p.r, p.y, s * p.r), new Vector3(c * p.n.x, p.n.y, s * p.n.x).normalized, new Vector2(az / (Mathf.PI * 2f), p.r));
        }

        void BuildStartMark()
        {
            var mb = new MeshBuilder();
            AddDisc(mb, level.start, 0.3f, 0.005f, 28, 0.255f);
            Part("Start Mark", mb.ToMesh("Start"), new[] { ArtFactory.Transparent(new Color(1, 1, 1, 0.55f), ArtFactory.Plain()) }, false, false);
        }

        public void UpdateFlag(Vector2 ballPos, float dt)
        {
            if (flagPole == null) return;
            float dist = (ballPos - level.hole).magnitude;
            float target = dist < 1.1f ? 1f : 0f;
            flagRetract = Mathf.MoveTowards(flagRetract, target, dt * 4f);
            float s = Mathf.SmoothStep(1f, 0.02f, flagRetract);
            flagPole.localScale = new Vector3(1, s, 1);
            if (flagCloth != null)
                flagCloth.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 2.1f) * 7f + Mathf.Sin(Time.time * 3.7f) * 2f, 0);
        }
    }
}
