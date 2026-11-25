using System;
using System.Collections.Generic;
using UnityEngine;

namespace PocketPutt.Sim
{
    public enum Theme { Garden, Dunes, Glacier, Workshop, Arena }
    public enum MotionKind { None, Spin, Slide }
    public enum ObstacleKind { Boundary, Block, Rail, Post, Mover }

    /// <summary>Rounded segment: a line a-b with radius r. A post is a degenerate segment.</summary>
    public struct Capsule
    {
        public Vector2 a, b;
        public float r;
        public Capsule(Vector2 a, Vector2 b, float r) { this.a = a; this.b = b; this.r = r; }
    }

    public sealed class Obstacle
    {
        public Capsule shape;
        public ObstacleKind kind;
        public MotionKind motion;
        public Vector2 pivot;
        public float angularSpeed;   // Spin: degrees per second
        public Vector2 slideDir;     // Slide: unit direction
        public float slideTravel;    // Slide: max offset
        public float period = 4f;    // Slide: seconds for a full there-and-back cycle
        public float phase;          // Spin: start degrees; Slide: 0..1 cycle offset

        public Capsule PoseAt(float t)
        {
            switch (motion)
            {
                case MotionKind.Spin:
                {
                    float ang = (phase + angularSpeed * t) * Mathf.Deg2Rad;
                    float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
                    Vector2 a = shape.a - pivot, b = shape.b - pivot;
                    return new Capsule(
                        pivot + new Vector2(a.x * c - a.y * s, a.x * s + a.y * c),
                        pivot + new Vector2(b.x * c - b.y * s, b.x * s + b.y * c), shape.r);
                }
                case MotionKind.Slide:
                {
                    Vector2 off = slideDir * SlideOffset(t);
                    return new Capsule(shape.a + off, shape.b + off, shape.r);
                }
            }
            return shape;
        }

        public float SlideOffset(float t)
        {
            float u = Mathf.Repeat(t / period + phase, 1f);
            float tri = u < 0.5f ? u * 2f : (1f - u) * 2f;
            float eased = tri * tri * (3f - 2f * tri);
            return eased * slideTravel;
        }
    }

    public struct Zone
    {
        public Surface surface;
        public Vector2[] poly;
        public bool Contains(Vector2 p)
        {
            int n = poly.Length;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }
    }

    /// <summary>One-way tunnel: a ball reaching 'from' re-emerges at 'to' heading along exitDir.</summary>
    public struct Portal
    {
        public Vector2 from;
        public Vector2 entryOpen;   // direction from the mouth into the room (visual only)
        public Vector2 to;
        public Vector2 exitDir;
        public float radius;
    }

    public struct RectF
    {
        public float x0, z0, x1, z1;
        public RectF(float x0, float z0, float x1, float z1) { this.x0 = x0; this.z0 = z0; this.x1 = x1; this.z1 = z1; }
    }

    /// <summary>
    /// Authoring and simulation data for one course. Floors are a union of axis-aligned rects; boundary
    /// walls are generated automatically. Everything else is explicit.
    /// </summary>
    public sealed class LevelData
    {
        public string id;
        public string title;
        public string hint;
        public Theme theme;
        public int threeStar = 1;
        public int twoStar = 2;
        public bool isFinal;

        public Vector2 start;
        public Vector2 hole;

        public readonly List<RectF> rects = new List<RectF>();
        public readonly List<Vector2[]> blocks = new List<Vector2[]>();
        public readonly List<Vector2[]> rails = new List<Vector2[]>();
        public readonly List<KeyValuePair<Vector2, float>> posts = new List<KeyValuePair<Vector2, float>>();
        public readonly List<Zone> zones = new List<Zone>();
        public readonly List<Portal> portals = new List<Portal>();
        public readonly List<Obstacle> movers = new List<Obstacle>();
        /// <summary>Lakes: axis-aligned rects inside the floor. Touching one returns the ball to the tee (the stroke stays counted).</summary>
        public readonly List<RectF> water = new List<RectF>();

        public readonly List<Capsule> boundary = new List<Capsule>();
        public readonly List<RectF> cells = new List<RectF>(); // floor tiles (compressed grid), for rendering
        public Obstacle[] obstacles = new Obstacle[0];
        public bool HasMovers { get; private set; }
        /// <summary>Seconds after which every mover returns to its initial pose (LCM of their periods, 0.1 s resolution).</summary>
        public float Cycle { get; private set; } = 0f;
        public Rect bounds;

        public Surface SurfaceAt(Vector2 p)
        {
            Surface s = Surface.Grass;
            for (int i = 0; i < zones.Count; i++)
                if (zones[i].Contains(p)) s = zones[i].surface;
            return s;
        }

        /// <summary>
        /// True when a ball resting at p is never touched by a mover during a whole cycle, so the player can wait there
        /// for a timing window. Without this a plan could assume a rest spot that the sim shoves the ball away from.
        /// Read-only (solver-safe).
        /// </summary>
        public bool RestIsSafe(Vector2 p, float margin = 0.04f)
        {
            if (!HasMovers) return true;
            float clear = Tuning.BallRadius + margin;
            float end = Mathf.Max(Cycle, 0.1f);
            for (int i = 0; i < movers.Count; i++)
                for (float t = 0f; t <= end + 1e-4f; t += 0.05f)
                {
                    Capsule c = movers[i].PoseAt(t);
                    Vector2 ab = c.b - c.a;
                    float len2 = ab.sqrMagnitude;
                    float u = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - c.a, ab) / len2) : 0f;
                    if ((p - (c.a + ab * u)).magnitude < c.r + clear) return false;
                }
            return true;
        }

        public bool HasWater => water.Count > 0;

        /// <summary>
        /// True when the closed ball disc (centre p, Tuning.BallRadius) touches a lake; tangency counts.
        /// Read-only, so the solver may call it from several threads.
        /// </summary>
        public bool TouchesWater(Vector2 p) => TouchesWater(p, Tuning.BallRadius);

        public bool TouchesWater(Vector2 p, float radius)
        {
            float reach = radius + WaterEpsilon;
            for (int i = 0; i < water.Count; i++)
            {
                RectF r = water[i];
                float dx = p.x - Mathf.Clamp(p.x, r.x0, r.x1);
                float dz = p.y - Mathf.Clamp(p.y, r.z0, r.z1);
                if (dx * dx + dz * dz <= reach * reach) return true;
            }
            return false;
        }

        public const float WaterEpsilon = 0.00001f;
        /// <summary>Authoring margin kept dry around the tee, hole and tunnel mouths.</summary>
        public const float WaterMargin = 0.02f;

        /// <summary>
        /// Authoring problems that make a water level invalid. Sand/ice zones may lie under a lake: water wins in the sim and hides them. Empty when the level has no water or is consistent.
        /// Call after Finish().
        /// </summary>
        public List<string> WaterIssues()
        {
            var issues = new List<string>();
            for (int i = 0; i < water.Count; i++)
            {
                RectF w = water[i];
                if (float.IsNaN(w.x0 + w.x1 + w.z0 + w.z1) || float.IsInfinity(w.x0 + w.x1 + w.z0 + w.z1) || !(w.x0 < w.x1) || !(w.z0 < w.z1))
                { issues.Add($"water {i}: degenerate or non-finite rect"); continue; }
                // Every sample of the lake must lie on real floor (bounds are not enough for L/U layouts).
                const int n = 8;
                for (int a = 0; a <= n && issues.Count < 8; a++)
                for (int b = 0; b <= n; b++)
                {
                    var p = new Vector2(Mathf.Lerp(w.x0, w.x1, a / (float)n), Mathf.Lerp(w.z0, w.z1, b / (float)n));
                    if (!InsideFloor(p)) { issues.Add($"water {i}: not fully on the floor at ({p.x:0.00},{p.y:0.00})"); a = n + 1; break; }
                }
                foreach (Vector2[] poly in blocks)
                    if (BoundsOverlap(poly, w)) issues.Add($"water {i}: overlaps a block");
                foreach (KeyValuePair<Vector2, float> post in posts)
                    if (post.Key.x + post.Value > w.x0 && post.Key.x - post.Value < w.x1 && post.Key.y + post.Value > w.z0 && post.Key.y - post.Value < w.z1)
                        issues.Add($"water {i}: overlaps a post");
            }
            if (water.Count == 0) return issues;
            if (TouchesWater(start, Tuning.BallRadius + WaterMargin)) issues.Add("tee touches water");
            if (TouchesWater(hole, Tuning.HoleRadius + Tuning.BallRadius + WaterMargin)) issues.Add("hole region touches water");
            foreach (Portal p in portals)
            {
                if (TouchesWater(p.from, p.radius + Tuning.BallRadius + WaterMargin)) issues.Add("tunnel mouth touches water");
                if (TouchesWater(p.to, p.radius + Tuning.BallRadius + WaterMargin)) issues.Add("tunnel exit touches water");
            }
            return issues;
        }

        /// <summary>Conservative: the polygon's bounding box intersects the lake's interior.</summary>
        static bool BoundsOverlap(Vector2[] poly, RectF w)
        {
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (Vector2 v in poly) { minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x); minZ = Mathf.Min(minZ, v.y); maxZ = Mathf.Max(maxZ, v.y); }
            return maxX > w.x0 && minX < w.x1 && maxZ > w.z0 && minZ < w.z1;
        }

        public bool InsideFloor(Vector2 p)
        {
            for (int i = 0; i < rects.Count; i++)
            {
                RectF r = rects[i];
                if (p.x >= r.x0 && p.x <= r.x1 && p.y >= r.z0 && p.y <= r.z1) return true;
            }
            return false;
        }

        /// <summary>Generate boundary walls and flatten the obstacle list. Call once after authoring.</summary>
        public void Finish()
        {
            boundary.Clear();
            BuildBoundary();
            var list = new List<Obstacle>();
            foreach (Capsule c in boundary)
                list.Add(new Obstacle { shape = c, kind = ObstacleKind.Boundary });
            foreach (Vector2[] poly in blocks)
                for (int i = 0; i < poly.Length; i++)
                    list.Add(new Obstacle
                    {
                        shape = new Capsule(poly[i], poly[(i + 1) % poly.Length], Tuning.WallRadius),
                        kind = ObstacleKind.Block
                    });
            foreach (Vector2[] line in rails)
                for (int i = 0; i + 1 < line.Length; i++)
                    list.Add(new Obstacle { shape = new Capsule(line[i], line[i + 1], Tuning.WallRadius), kind = ObstacleKind.Rail });
            foreach (var p in posts)
                list.Add(new Obstacle { shape = new Capsule(p.Key, p.Key, p.Value), kind = ObstacleKind.Post });
            list.AddRange(movers);
            obstacles = list.ToArray();
            HasMovers = movers.Count > 0;
            Cycle = 0f;
            if (HasMovers)
            {
                long l = 1;
                foreach (Obstacle m in movers)
                {
                    float period = m.motion == MotionKind.Spin ? 360f / Mathf.Max(0.01f, Mathf.Abs(m.angularSpeed)) : m.period;
                    long p10 = Mathf.Max(1, Mathf.RoundToInt(period * 10f));
                    l = l / Gcd(l, p10) * p10;
                }
                Cycle = l / 10f;
            }

            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (RectF r in rects)
            {
                minX = Mathf.Min(minX, r.x0); minZ = Mathf.Min(minZ, r.z0);
                maxX = Mathf.Max(maxX, r.x1); maxZ = Mathf.Max(maxZ, r.z1);
            }
            bounds = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

        static long Gcd(long a, long b) { while (b != 0) { long t = a % b; a = b; b = t; } return a; }

        void BuildBoundary()
        {
            // Coordinate compression: mark inside cells, emit edges between inside/outside cells, merge collinear runs.
            var xs = new SortedSet<float>();
            var zs = new SortedSet<float>();
            foreach (RectF r in rects) { xs.Add(r.x0); xs.Add(r.x1); zs.Add(r.z0); zs.Add(r.z1); }
            float[] X = new float[xs.Count]; xs.CopyTo(X);
            float[] Z = new float[zs.Count]; zs.CopyTo(Z);
            int nx = X.Length - 1, nz = Z.Length - 1;
            bool[,] inside = new bool[nx + 2, nz + 2]; // padded by one cell
            cells.Clear();
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float cx = (X[i] + X[i + 1]) * 0.5f, cz = (Z[j] + Z[j + 1]) * 0.5f;
                    foreach (RectF r in rects)
                        if (cx > r.x0 && cx < r.x1 && cz > r.z0 && cz < r.z1)
                        {
                            inside[i + 1, j + 1] = true;
                            cells.Add(new RectF(X[i], Z[j], X[i + 1], Z[j + 1]));
                            break;
                        }
                }
            for (int i = 0; i <= nx; i++)
            {
                int start = -1;
                for (int j = 0; j <= nz; j++)
                {
                    bool edge = j < nz && inside[i, j + 1] != inside[i + 1, j + 1];
                    if (edge && start < 0) start = j;
                    if (!edge && start >= 0)
                    {
                        boundary.Add(new Capsule(new Vector2(X[i], Z[start]), new Vector2(X[i], Z[j]), Tuning.WallRadius));
                        start = -1;
                    }
                }
            }
            for (int j = 0; j <= nz; j++)
            {
                int start = -1;
                for (int i = 0; i <= nx; i++)
                {
                    bool edge = i < nx && inside[i + 1, j] != inside[i + 1, j + 1];
                    if (edge && start < 0) start = i;
                    if (!edge && start >= 0)
                    {
                        boundary.Add(new Capsule(new Vector2(X[start], Z[j]), new Vector2(X[i], Z[j]), Tuning.WallRadius));
                        start = -1;
                    }
                }
            }
        }

        // ---- Collision ------------------------------------------------------------------------

        public void Collide(ref BallState s, ref StepEvents ev)
        {
            float rb = Tuning.BallRadius;
            for (int pass = 0; pass < 2; pass++)
            {
                bool any = false;
                for (int i = 0; i < obstacles.Length; i++)
                {
                    Obstacle o = obstacles[i];
                    Capsule c = o.motion == MotionKind.None ? o.shape : o.PoseAt(s.time);
                    float reach = rb + c.r;
                    float minx = Mathf.Min(c.a.x, c.b.x) - reach, maxx = Mathf.Max(c.a.x, c.b.x) + reach;
                    if (s.pos.x < minx || s.pos.x > maxx) continue;
                    float minz = Mathf.Min(c.a.y, c.b.y) - reach, maxz = Mathf.Max(c.a.y, c.b.y) + reach;
                    if (s.pos.y < minz || s.pos.y > maxz) continue;

                    Vector2 ab = c.b - c.a;
                    float len2 = ab.sqrMagnitude;
                    float t = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(s.pos - c.a, ab) / len2) : 0f;
                    Vector2 closest = c.a + ab * t;
                    Vector2 d = s.pos - closest;
                    float dist2 = d.sqrMagnitude;
                    if (dist2 >= reach * reach) continue;

                    float dist = Mathf.Sqrt(dist2);
                    Vector2 n;
                    if (dist > 1e-5f) n = d / dist;
                    else n = len2 > 1e-8f ? new Vector2(-ab.y, ab.x).normalized : Vector2.up;
                    s.pos = closest + n * reach;

                    Vector2 surfVel = o.motion == MotionKind.None ? Vector2.zero : ObstacleVelocity(o, closest, s.time);
                    Vector2 rel = s.vel - surfVel;
                    float vn = Vector2.Dot(rel, n);
                    if (vn < 0f)
                    {
                        float e = -vn > 0.6f ? Tuning.WallRestitution : 0.25f;
                        Vector2 tangent = rel - vn * n;
                        rel = tangent * Tuning.WallTangentKeep - n * (vn * e);
                        s.vel = rel + surfVel;
                        if (-vn > ev.wallImpact) { ev.wallImpact = -vn; ev.wallPoint = closest; }
                    }
                    any = true;
                }
                if (!any) break;
            }
        }

        static Vector2 ObstacleVelocity(Obstacle o, Vector2 point, float t)
        {
            const float h = 1f / 240f;
            Capsule c0 = o.PoseAt(t), c1 = o.PoseAt(t + h);
            Vector2 ab = c0.b - c0.a;
            float len2 = ab.sqrMagnitude;
            float u = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(point - c0.a, ab) / len2) : 0f;
            Vector2 p0 = c0.a + (c0.b - c0.a) * u;
            Vector2 p1 = c1.a + (c1.b - c1.a) * u;
            return (p1 - p0) / h;
        }

        public bool TryPortal(ref BallState s, ref StepEvents ev)
        {
            for (int i = 0; i < portals.Count; i++)
            {
                Portal p = portals[i];
                if ((s.pos - p.from).sqrMagnitude <= p.radius * p.radius)
                {
                    float speed = Mathf.Max(s.vel.magnitude, 1.2f);
                    s.pos = p.to;
                    s.vel = p.exitDir.normalized * speed;
                    ev.teleported = true;
                    ev.portalIndex = i;
                    return true;
                }
            }
            return false;
        }
    }
}
