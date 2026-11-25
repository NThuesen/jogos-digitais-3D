using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace PocketPutt.Sim
{
    public struct ShotPlan
    {
        public float angleDeg, power;
        public Vector2 from;
        public float startTime;   // moving obstacles: when (mod cycle) the shot must be fired
    }

    public sealed class SolveResult
    {
        public bool solvable;
        public int minShots;
        public List<ShotPlan> plan = new List<ShotPlan>();
        public float finalAngleWidth;      // contiguous angle tolerance (degrees) of the last shot
        public int finalPowerLevels;       // how many of the power samples work at the best angle (depth 1 only)
        public float closestMiss = -1f;    // depth-1 failure diagnostics
        public float missAngle, missPower;
        public List<Vector2[]> traces = new List<Vector2[]>();
    }

    /// <summary>
    /// Brute-force shot search using the exact runtime simulation. Used by level QA and the in-game self-test;
    /// it never ships as a player-facing hint.
    /// </summary>
    public static class Solver
    {
        public static readonly float[] Powers = MakePowers();
        static float[] MakePowers()
        {
            var l = new float[22];
            for (int i = 0; i < l.Length; i++) l[i] = 0.12f + i * (0.88f / (l.Length - 1));
            return l;
        }

        /// <summary>wasStopped means a genuine rest; a shot that ended in water is returned to the tee and is never a rest node.</summary>
        public struct ShotOutcome { public bool sunk; public Vector2 rest; public float minHole; public bool wasStopped; public bool water; }

        public static ShotOutcome Shoot(LevelData level, Vector2 from, float angleDeg, float power, List<Vector2> trace = null, float startTime = 0f)
        {
            var s = new BallState { pos = from, time = startTime };
            Vector2 dir = new Vector2(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad));
            BallSim.Launch(ref s, dir, power);
            float minHole = float.MaxValue;
            BallSim.RunToRest(ref s, level, p =>
            {
                trace?.Add(p);
                float d = (p - level.hole).sqrMagnitude;
                if (d < minHole) minHole = d;
            });
            return new ShotOutcome { sunk = s.sunk, rest = s.pos, minHole = Mathf.Sqrt(minHole), wasStopped = s.vel.sqrMagnitude == 0f && !s.drowned, water = s.drowned };
        }

        static readonly Vector2[] Offsets = { Vector2.zero, new Vector2(0.25f, 0), new Vector2(-0.25f, 0), new Vector2(0, 0.25f), new Vector2(0, -0.25f) };

        static bool RobustFrom(LevelData level, Vector2 pos, float angle, float power, float width, float phase, bool positions)
        {
            float[] dts = level.HasMovers ? new[] { -0.12f, 0f, 0.12f } : new[] { 0f };
            foreach (float dt in dts)
            foreach (Vector2 off in Offsets)
            {
                if (!positions && off != Vector2.zero) continue;
                Vector2 p = pos + off;
                if (level.TouchesWater(p)) return false;   // a wet offset is a real failure, not a sample to skip
                if (!level.InsideFloor(p)) continue;
                for (int i = -2; i <= 2; i++)
                    if (!Shoot(level, p, angle + i * width * 0.25f, power, null, phase + dt).sunk) return false;
            }
            return true;
        }

        /// <summary>Shot start times to try: a few evenly spaced points of the mover cycle.</summary>
        public static float[] PhasesFor(LevelData level, int n = 8)
        {
            if (!level.HasMovers || level.Cycle <= 0f) return new[] { 0f };
            var ph = new float[n];
            for (int i = 0; i < n; i++) ph[i] = level.Cycle * i / n;
            return ph;
        }

        static long Key(Vector2 v) => ((long)Mathf.RoundToInt(v.x / 0.25f) << 20) ^ (uint)Mathf.RoundToInt(v.y / 0.25f);

        sealed class Node
        {
            public Vector2 pos;
            public int parent = -1;
            public float angle, power, phase;
        }

        /// <summary>Finds the shortest robust plan of at most maxDepth shots.</summary>
        public static SolveResult Solve(LevelData level, int maxDepth, float minFinalWidth1 = 2f, float minFinalWidthN = 4f, bool deep = true)
        {
            var res = new SolveResult();
            var nodes = new List<Node> { new Node { pos = level.start, parent = -1 } };
            var visited = new HashSet<long> { Key(level.start) };
            var frontier = new List<int> { 0 };

            for (int depth = 1; depth <= maxDepth; depth++)
            {
                float minWidth = depth == 1 ? minFinalWidth1 : minFinalWidthN;
                var found = new ConcurrentBag<(int node, float width, float angle, float power, int powers, float phase)>();
                var next = new ConcurrentDictionary<long, (Vector2 pos, int parent, float angle, float power, float phase)>();
                var miss = new ConcurrentBag<(float d, float a, float p)>();
                int[] fr = frontier.ToArray();
                float angStep = depth == 1 ? 0.5f : 1f;
                if (level.HasMovers) angStep = depth == 1 ? 1f : 2f;
                int na = Mathf.RoundToInt(360f / angStep);
                float[] phases = PhasesFor(level, depth == 1 ? 20 : 8);

                Parallel.For(0, fr.Length, fi =>
                {
                    int ni = fr[fi];
                    Vector2 from = nodes[ni].pos;
                    float bestWidth = 0f, bestAng = 0f, bestPow = 0f, bestPhase = 0f;
                    int bestPowers = 0;
                    foreach (float phase in phases)
                    for (int p = 0; p < Powers.Length; p++)
                    {
                        int run = 0, runStart = 0;
                        for (int a = 0; a < na; a++)
                        {
                            float ang = a * angStep;
                            ShotOutcome o = Shoot(level, from, ang, Powers[p], null, phase);
                            if (depth == 1 && o.minHole < 5f) miss.Add((o.minHole, ang, Powers[p]));
                            if (o.sunk)
                            {
                                if (run == 0) runStart = a;
                                run++;
                                float w = run * angStep;
                                if (w > bestWidth) { bestWidth = w; bestAng = (runStart + run * 0.5f - 0.5f) * angStep; bestPow = Powers[p]; bestPhase = phase; }
                            }
                            else
                            {
                                run = 0;
                                if (depth < maxDepth && deep && o.wasStopped && (a & 1) == 0 && level.RestIsSafe(o.rest))
                                {
                                    long k = Key(o.rest);
                                    next.TryAdd(k, (o.rest, ni, ang, Powers[p], phase));
                                }
                            }
                        }
                    }
                    if (bestWidth >= minWidth)
                    {
                        if (depth == 1)
                        {
                            for (int p = 0; p < Powers.Length; p++)
                                if (Shoot(level, from, bestAng, Powers[p], null, bestPhase).sunk) bestPowers++;
                        }
                        found.Add((ni, bestWidth, bestAng, bestPow, bestPowers, bestPhase));
                    }
                });

                if (!found.IsEmpty)
                {
                    // Humans cannot land on the exact planned spot, so a candidate final shot must also work
                    // from positions around it (depth > 1) before it counts.
                    var cands = new List<(int node, float width, float angle, float power, int powers, float phase)>(found);
                    cands.Sort((a, b) => b.width.CompareTo(a.width));
                    (int node, float width, float angle, float power, int powers, float phase) best = default;
                    int checkedCount = 0;
                    foreach (var cnd0 in cands)
                    {
                        if (++checkedCount > 80) break;
                        var cnd = cnd0;
                        if (!level.HasMovers)
                        {
                            if (depth == 1 || RobustFrom(level, nodes[cnd.node].pos, cnd.angle, cnd.power, minWidth, cnd.phase, true)) { best = cnd; break; }
                            continue;
                        }
                        // Movers: slide the shot time around the sampled phase to find the middle of the open window.
                        bool ok = false;
                        for (int k = 0; k <= 12 && !ok; k++)
                        {
                            float dp = ((k + 1) / 2) * 0.1f * ((k % 2 == 1) ? 1f : -1f);
                            if (RobustFrom(level, nodes[cnd.node].pos, cnd.angle, cnd.power, minWidth, cnd.phase + dp, depth > 1))
                            {
                                cnd.phase = Mathf.Repeat(cnd.phase + dp, level.Cycle);
                                ok = true;
                            }
                        }
                        if (ok) { best = cnd; break; }
                    }
                    if (best.width > 0f)
                    {
                        res.solvable = true;
                        res.minShots = depth;
                        res.finalAngleWidth = best.width;
                        res.finalPowerLevels = best.powers;
                        var chain = new List<ShotPlan>();
                        int cur = best.node;
                        while (nodes[cur].parent >= 0)
                        {
                            Node n = nodes[cur];
                            chain.Add(new ShotPlan { angleDeg = n.angle, power = n.power, from = nodes[n.parent].pos, startTime = n.phase });
                            cur = n.parent;
                        }
                        chain.Reverse();
                        chain.Add(new ShotPlan { angleDeg = best.angle, power = best.power, from = nodes[best.node].pos, startTime = best.phase });
                        res.plan = chain;
                        foreach (ShotPlan sp in chain)
                        {
                            var tr = new List<Vector2> { sp.from };
                            Shoot(level, sp.from, sp.angleDeg, sp.power, tr, sp.startTime);
                            res.traces.Add(tr.ToArray());
                        }
                        return res;
                    }
                }

                if (depth == 1 && !miss.IsEmpty)
                {
                    (float d, float a, float p) m = (float.MaxValue, 0, 0);
                    foreach (var x in miss) if (x.d < m.d) m = x;
                    res.closestMiss = m.d; res.missAngle = m.a; res.missPower = m.p;
                    var tr = new List<Vector2> { level.start };
                    Shoot(level, level.start, m.a, m.p, tr);
                    res.traces.Add(tr.ToArray());
                }
                if (!deep) break;

                // build next frontier (deduplicated, capped)
                var newFrontier = new List<int>();
                var entries = new List<KeyValuePair<long, (Vector2 pos, int parent, float angle, float power, float phase)>>(next);
                entries.Sort((x, y) => x.Key.CompareTo(y.Key));
                int stride = Mathf.Max(1, entries.Count / 500);
                for (int i = 0; i < entries.Count; i += stride)
                {
                    var e = entries[i];
                    if (!visited.Add(e.Key)) continue;
                    nodes.Add(new Node { pos = e.Value.pos, parent = e.Value.parent, angle = e.Value.angle, power = e.Value.power, phase = e.Value.phase });
                    newFrontier.Add(nodes.Count - 1);
                }
                frontier = newFrontier;
                if (frontier.Count == 0) break;
            }
            return res;
        }
    }
}
