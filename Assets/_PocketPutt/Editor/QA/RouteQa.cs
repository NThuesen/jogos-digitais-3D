using System;
using System.Collections.Generic;
using PocketPutt.Sim;
using UnityEngine;

namespace PocketPutt.QA
{
    [Serializable]
    public sealed class RouteQaSettings
    {
        public float[] directOffsets = { -3f, -2f, -1f, 0f, 1f, 2f, 3f };
        public float[] directPowers = MakePowers();
        public float highPower = 0.8f;
        public int phaseSamples = 8;
        public int policyMaxShots = 4;
        public int searchDepth = 3;
        public int beamWidth = 12;
        public int searchShotBudget = 50000;
        public float searchAngleStep = 10f;
        public float routeSeparation = 0.65f;
        public int maxFamilies = 32;
        public float angleError = 1f;
        public float powerError = 0.03f;
        public float timingError = 0.12f;
        public int robustnessSamples = 32;
        public int seed = 20261020;

        static float[] MakePowers()
        {
            var p = new float[37];
            for (int i = 0; i < p.Length; i++) p[i] = (4 + i) / 40f;
            return p;
        }

        public void Validate()
        {
            if (directOffsets == null || directOffsets.Length == 0 || directPowers == null || directPowers.Length == 0)
                throw new ArgumentException("Direct angle and power grids must be nonempty.");
            foreach (float p in directPowers)
                if (!Finite(p) || p < 0.08f || p > 1f) throw new ArgumentException("Power outside [0.08, 1].");
            foreach (float a in directOffsets)
                if (!Finite(a) || Mathf.Abs(a) > 180f) throw new ArgumentException("Invalid angle offset.");
            if (phaseSamples < 1 || policyMaxShots < 1 || searchDepth < 1 || beamWidth < 1 || searchShotBudget < 1 || maxFamilies < 1 || robustnessSamples < 1 ||
                !Finite(highPower) || highPower < 0.08f || highPower > 1f ||
                !Finite(searchAngleStep) || searchAngleStep < 1f || searchAngleStep > 180f ||
                !Finite(routeSeparation) || routeSeparation <= 0f ||
                !Finite(angleError) || angleError < 0f || !Finite(powerError) || powerError < 0f || !Finite(timingError) || timingError < 0f)
                throw new ArgumentException("Invalid QA limits or tolerances.");
        }

        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    }

    [Serializable]
    public struct QaPoint
    {
        public float x, y;
        public QaPoint(Vector2 p) { x = p.x; y = p.y; }
        public Vector2 Vector() => new Vector2(x, y);
    }

    [Serializable]
    public struct QaCommand
    {
        public float angleDeg, power;
        public QaCommand(float a, float p) { angleDeg = a; power = p; }
    }

    [Serializable]
    public sealed class QaShot
    {
        public QaPoint from, rest;
        public float angleDeg, power, startTime, endTime, pathLength, sandSeconds, iceSeconds, minHole;
        public int steps, impactSteps, portalEvents, waterEvents;
        public QaPoint waterPoint;      // first contact centre (valid when waterEvents > 0)
        public bool sunk, timedOut, leftFloor;
        public List<int> portalIndices = new List<int>();
        public List<QaPoint> trace = new List<QaPoint>();
        public List<QaPoint> impacts = new List<QaPoint>();
    }

    [Serializable]
    public sealed class QaPlan
    {
        public string source;
        public QaPoint finalPosition;
        public float initialTime, finalTime;
        public bool sunk, timedOut, leftFloor, rejectedShot;
        public float pathLength, sandSeconds, iceSeconds;
        public int impactSteps, portalEvents, waterEvents;
        public List<QaShot> shots = new List<QaShot>();
        public int robustnessSuccesses, robustnessTrials;
        public int sandFreeSuccesses, impactFreeSuccesses, waterFreeSuccesses;
        public float meanShotsConsumed, meanRemainingDistance;
    }

    [Serializable]
    public sealed class QaScan
    {
        public int attempts, sunk, timedOut, leftFloor, water, highWater;   // water: attempts that fell in a lake
        public int exactAttempts, exactSunk, highAttempts, highSunk, exactHighAttempts, exactHighSunk;
        public List<QaShot> samples = new List<QaShot>();
    }

    [Serializable]
    public sealed class QaPolicies
    {
        public int attempts, completed, highAttempts, highCompleted, bestShots, bestHighShots;
        public int completedWithinThreeStar, highCompletedWithinThreeStar;
        public QaPlan best, bestHigh;
    }

    [Serializable]
    public sealed class QaFamily
    {
        public int id, observedSolutions;
        public bool usesSand, usesIce;
        public string portalSequence;
        public QaPlan representative;
    }

    [Serializable]
    public sealed class QaLevelReport
    {
        public string id, title;
        public int threeStar, twoStar;
        public bool tutorialException, highPowerReview, trivialPolicyReview;
        public QaScan direct = new QaScan();
        public QaPolicies policies = new QaPolicies();
        public int searchShots, searchedDepth, winningPlans;
        public bool searchBudgetExhausted, familyLimitReached;
        public List<QaFamily> families = new List<QaFamily>();
        public int bestSandFreeShots, bestSandShots;
        public float bestSandFreeRobustness, bestSandRobustness;
        public string hazardCoverage = "Water: contact returns the ball to the tee and the stroke stays counted; plans that drown are never reported as routes. Sand is a surface cost, not a reset hazard.";
        public string interpretation = "Finite sampled evidence, not exhaustive route count, safety proof or creator approval.";
    }

    /// <summary>QA only. Every flight is advanced by the production BallSim.Step, including events and clock.</summary>
    public static class RouteQa
    {
        internal sealed class Run
        {
            public BallState state;
            public QaShot shot;
        }

        internal static Run Shoot(LevelData level, BallState state, float angle, float power, bool trace)
        {
            var q = new QaShot { from = new QaPoint(state.pos), angleDeg = angle, power = power, startTime = state.time, minHole = (state.pos - level.hole).magnitude };
            if (trace) q.trace.Add(q.from);
            BallSim.Launch(ref state, new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)), power);
            int maxSteps = Mathf.RoundToInt(Tuning.MaxSimSeconds / Tuning.StepDt);
            StepEvents ev = default;
            bool moving = true;
            for (int i = 0; i < maxSteps; i++)
            {
                Vector2 previous = state.pos;
                Surface surface = level.SurfaceAt(previous);
                moving = BallSim.Step(ref state, level, ref ev);
                q.steps++;
                if (surface == Surface.Sand) q.sandSeconds += Tuning.StepDt;
                if (surface == Surface.Ice) q.iceSeconds += Tuning.StepDt;
                if (ev.wallImpact > 0f)
                {
                    q.impactSteps++;
                    if (trace) q.impacts.Add(new QaPoint(ev.wallPoint));
                }
                if (ev.water)
                {
                    // The return to the tee is a discontinuity, not distance rolled.
                    if (q.waterEvents++ == 0) q.waterPoint = new QaPoint(ev.waterPoint);
                    if (trace) q.trace.Add(new QaPoint(ev.waterPoint));
                    q.pathLength += (ev.waterPoint - previous).magnitude;
                }
                else if (ev.teleported)
                {
                    q.portalEvents++;
                    q.portalIndices.Add(ev.portalIndex);
                    // A tunnel jump has no traversed chord. Keep both ends as separate samples.
                    if (trace) q.trace.Add(new QaPoint(previous));
                }
                else q.pathLength += (state.pos - previous).magnitude;
                if (!level.InsideFloor(state.pos)) q.leftFloor = true;
                q.minHole = Mathf.Min(q.minHole, (state.pos - level.hole).magnitude);
                if (trace && (ev.teleported || ev.wallImpact > 0f || !moving ||
                    (q.trace[q.trace.Count - 1].Vector() - state.pos).sqrMagnitude >= 0.0225f))
                    q.trace.Add(new QaPoint(state.pos));
                if (!moving) break;
            }
            q.sunk = state.sunk;
            q.timedOut = moving;
            q.rest = new QaPoint(state.pos);
            q.endTime = state.time;
            return new Run { state = state, shot = q };
        }

        public static QaPlan Replay(LevelData level, IList<QaCommand> commands, float initialTime = 0f,
            string source = "replay", float[] angleErrors = null, float[] powerErrors = null, float[] timingErrors = null)
        {
            var plan = new QaPlan { source = source, initialTime = initialTime };
            var state = new BallState { pos = level.start, time = initialTime };
            for (int i = 0; i < commands.Count && !state.sunk; i++)
            {
                // Timing jitter models an actual nonnegative preparation delay; advance real simulation while waiting.
                float wait = timingErrors == null ? 0f : Mathf.Max(0f, timingErrors[i]);
                int waitSteps = Mathf.RoundToInt(wait / Tuning.StepDt);
                StepEvents ev = default;
                for (int w = 0; w < waitSteps; w++)
                {
                    BallSim.Step(ref state, level, ref ev);
                    if (!level.InsideFloor(state.pos)) plan.leftFloor = true;
                }
                if (state.sunk) break;
                // Waiting for a window requires a rest spot no mover ever sweeps (also checked without any delay).
                if (i > 0 && !level.RestIsSafe(state.pos)) { plan.rejectedShot = true; break; }
                // A mover can push a resting ball during a delay: input would be rejected by LevelRunner.
                if (state.vel.sqrMagnitude > 0f) { plan.rejectedShot = true; break; }
                QaCommand c = commands[i];
                float power = c.power + (powerErrors == null ? 0f : powerErrors[i]);
                if (power < 0.08f) { plan.rejectedShot = true; break; }
                Run run = Shoot(level, state, c.angleDeg + (angleErrors == null ? 0f : angleErrors[i]),
                    Mathf.Clamp01(power), true);
                state = run.state;
                plan.shots.Add(run.shot);
                plan.pathLength += run.shot.pathLength;
                plan.sandSeconds += run.shot.sandSeconds;
                plan.iceSeconds += run.shot.iceSeconds;
                plan.impactSteps += run.shot.impactSteps;
                plan.portalEvents += run.shot.portalEvents;
                plan.waterEvents += run.shot.waterEvents;
                plan.leftFloor |= run.shot.leftFloor;
                plan.timedOut |= run.shot.timedOut;
                if (plan.timedOut || plan.leftFloor) break;
            }
            plan.finalPosition = new QaPoint(state.pos);
            plan.finalTime = state.time;
            plan.sunk = state.sunk && !plan.timedOut && !plan.leftFloor && !plan.rejectedShot;
            return plan;
        }

        public static QaLevelReport Analyze(LevelData level, RouteQaSettings settings = null)
        {
            var cfg = settings ?? new RouteQaSettings();
            cfg.Validate();
            var r = new QaLevelReport { id = level.id, title = level.title, threeStar = level.threeStar, twoStar = level.twoStar,
                tutorialException = level.id == "C1-01" };
            float[] phases = Solver.PhasesFor(level, cfg.phaseSamples);
            foreach (float phase in phases)
            foreach (float offset in cfg.directOffsets)
            foreach (float power in cfg.directPowers)
            {
                float angle = Aim(level.start, level.hole) + offset;
                Run run = Shoot(level, new BallState { pos = level.start, time = phase }, angle, power, true);
                QaShot s = run.shot;
                bool win = s.sunk && !s.leftFloor && !s.timedOut;
                bool exact = Mathf.Abs(offset) < 0.0001f, high = power >= cfg.highPower;
                r.direct.attempts++;
                if (win) r.direct.sunk++;
                if (s.timedOut) r.direct.timedOut++;
                if (s.leftFloor) r.direct.leftFloor++;
                if (s.waterEvents > 0) { r.direct.water++; if (power >= cfg.highPower) r.direct.highWater++; }
                if (exact) { r.direct.exactAttempts++; if (win) r.direct.exactSunk++; }
                if (high) { r.direct.highAttempts++; if (win) r.direct.highSunk++; }
                if (high && exact) { r.direct.exactHighAttempts++; if (win) r.direct.exactHighSunk++; }
                r.direct.samples.Add(s);
                if (win) AddSolution(level, r, cfg, new List<QaCommand> { new QaCommand(angle, power) }, phase, "direct");

                // A deliberately simple policy: keep this power and angular offset, re-aim at the hole after each rest.
                var commands = new List<QaCommand> { new QaCommand(angle, power) };
                BallState state = run.state;
                for (int n = 1; !state.sunk && !run.shot.timedOut && !run.shot.leftFloor && n < cfg.policyMaxShots; n++)
                {
                    angle = Aim(state.pos, level.hole) + offset;
                    commands.Add(new QaCommand(angle, power));
                    run = Shoot(level, state, angle, power, false);
                    state = run.state;
                }
                var p = r.policies;
                p.attempts++;
                if (high) p.highAttempts++;
                if (!state.sunk || run.shot.timedOut || run.shot.leftFloor) continue;
                p.completed++;
                if (commands.Count <= level.threeStar) p.completedWithinThreeStar++;
                if (p.bestShots == 0 || commands.Count < p.bestShots)
                {
                    p.bestShots = commands.Count;
                    p.best = Replay(level, commands, phase, "reaim-policy");
                }
                if (high)
                {
                    p.highCompleted++;
                    if (commands.Count <= level.threeStar) p.highCompletedWithinThreeStar++;
                    if (p.bestHighShots == 0 || commands.Count < p.bestHighShots)
                    {
                        p.bestHighShots = commands.Count;
                        p.bestHigh = Replay(level, commands, phase, "high-reaim-policy");
                    }
                }
                if (commands.Count > 1) AddSolution(level, r, cfg, commands, phase, "reaim-policy");
            }
            Search(level, r, cfg, phases);
            if (r.policies.best != null) TestRobustness(level, r.policies.best, cfg);
            if (r.policies.bestHigh != null) TestRobustness(level, r.policies.bestHigh, cfg);
            foreach (QaFamily family in r.families)
            {
                TestRobustness(level, family.representative, cfg);
                family.representative = CenterTiming(level, family.representative, cfg);
                QaPlan plan = family.representative;
                int shots = plan.shots.Count;
                float rate = plan.robustnessSuccesses / (float)plan.robustnessTrials;
                if (family.usesSand)
                {
                    if (r.bestSandShots == 0 || shots < r.bestSandShots) { r.bestSandShots = shots; r.bestSandRobustness = rate; }
                    else if (shots == r.bestSandShots) r.bestSandRobustness = Mathf.Max(r.bestSandRobustness, rate);
                }
                else
                {
                    if (r.bestSandFreeShots == 0 || shots < r.bestSandFreeShots) { r.bestSandFreeShots = shots; r.bestSandFreeRobustness = rate; }
                    else if (shots == r.bestSandFreeShots) r.bestSandFreeRobustness = Mathf.Max(r.bestSandFreeRobustness, rate);
                }
            }
            r.highPowerReview = !r.tutorialException && r.direct.highSunk > 0;
            r.trivialPolicyReview = !r.tutorialException && r.policies.highCompletedWithinThreeStar > 0;
            return r;
        }

        /// <summary>
        /// Search phases are a coarse grid, so a found plan may sit at the edge of its timing window and look fragile.
        /// Slide the whole plan in time (+/-0.6 s, 0.1 s steps) and keep the most robust replay that still sinks.
        /// </summary>
        static QaPlan CenterTiming(LevelData level, QaPlan plan, RouteQaSettings cfg)
        {
            if (!level.HasMovers || level.Cycle <= 0f || plan.shots.Count == 0) return plan;
            List<QaCommand> commands = Commands(plan);
            QaPlan best = plan;
            for (int k = -6; k <= 6; k++)
            {
                if (k == 0) continue;
                QaPlan cand = Replay(level, commands, Mathf.Repeat(plan.initialTime + k * 0.1f, level.Cycle), plan.source);
                if (!cand.sunk || cand.waterEvents > 0) continue;
                TestRobustness(level, cand, cfg);
                if (cand.robustnessSuccesses > best.robustnessSuccesses ||
                    (cand.robustnessSuccesses == best.robustnessSuccesses && Mathf.Abs(k) < Mathf.Abs((best.initialTime - plan.initialTime) * 10f))) best = cand;
            }
            return best;
        }

        static float Aim(Vector2 from, Vector2 to) => Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;

        sealed class Node
        {
            public BallState state;
            public float initialTime;
            public List<QaCommand> commands;
            public bool sand;
        }

        static void Search(LevelData level, QaLevelReport report, RouteQaSettings cfg, float[] phases)
        {
            var frontier = new List<Node>();
            foreach (float phase in phases)
                frontier.Add(new Node { state = new BallState { pos = level.start, time = phase }, initialTime = phase, commands = new List<QaCommand>() });
            for (int depth = 1; depth <= cfg.searchDepth && frontier.Count > 0; depth++)
            {
                report.searchedDepth = depth;
                var next = new Dictionary<string, Node>();
                foreach (Node node in frontier)
                {
                    var angles = new List<float>();
                    foreach (float off in cfg.directOffsets) angles.Add(Aim(node.state.pos, level.hole) + off);
                    for (float a = 0f; a < 360f; a += cfg.searchAngleStep) angles.Add(a);
                    foreach (float a in angles)
                    foreach (float p in Solver.Powers)
                    {
                        if (report.searchShots >= cfg.searchShotBudget) { report.searchBudgetExhausted = true; return; }
                        report.searchShots++;
                        Run run = Shoot(level, node.state, a, p, false);
                        if (run.shot.timedOut || run.shot.leftFloor) continue;
                        var commands = new List<QaCommand>(node.commands) { new QaCommand(a, p) };
                        if (run.state.sunk) { AddSolution(level, report, cfg, commands, node.initialTime, "bounded-search"); continue; }
                        if (depth == cfg.searchDepth) continue;
                        if (!level.RestIsSafe(run.state.pos)) continue;   // cannot wait for a mover window from here
                        if ((run.state.pos - node.state.pos).sqrMagnitude < 0.01f) continue;
                        bool sand = node.sand || run.shot.sandSeconds > 0f;
                        int phaseBin = level.HasMovers ? Mathf.FloorToInt(Mathf.Repeat(run.state.time, level.Cycle) / level.Cycle * cfg.phaseSamples) : 0;
                        string key = Mathf.RoundToInt(run.state.pos.x / 0.4f) + ":" + Mathf.RoundToInt(run.state.pos.y / 0.4f) + ":" + phaseBin + ":" + sand;
                        if (!next.TryGetValue(key, out Node existing) || (run.state.pos - level.hole).sqrMagnitude < (existing.state.pos - level.hole).sqrMagnitude)
                            next[key] = new Node { state = run.state, initialTime = node.initialTime, commands = commands, sand = sand };
                    }
                }
                frontier = new List<Node>(next.Values);
                // Total ordering makes the bounded beam repeatable, including ties.
                frontier.Sort((a, b) => CompareNodes(a, b, level));
                if (frontier.Count > cfg.beamWidth) frontier.RemoveRange(cfg.beamWidth, frontier.Count - cfg.beamWidth);
            }
        }

        static int CompareNodes(Node a, Node b, LevelData level)
        {
            int c = (a.state.pos - level.hole).sqrMagnitude.CompareTo((b.state.pos - level.hole).sqrMagnitude);
            if (c != 0) return c;
            c = a.state.pos.x.CompareTo(b.state.pos.x); if (c != 0) return c;
            c = a.state.pos.y.CompareTo(b.state.pos.y); if (c != 0) return c;
            c = a.state.time.CompareTo(b.state.time); if (c != 0) return c;
            c = a.initialTime.CompareTo(b.initialTime); if (c != 0) return c;
            return a.sand.CompareTo(b.sand);
        }

        static List<QaCommand> Commands(QaPlan plan)
        {
            var list = new List<QaCommand>();
            foreach (QaShot s in plan.shots) list.Add(new QaCommand(s.angleDeg, s.power));
            return list;
        }

        static List<QaPoint> Points(QaPlan p)
        {
            var points = new List<QaPoint>();
            foreach (QaShot s in p.shots) points.AddRange(s.trace);
            return points;
        }

        static string Portals(QaPlan p)
        {
            var ids = new List<string>();
            foreach (QaShot s in p.shots) foreach (int id in s.portalIndices) ids.Add(id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return string.Join(",", ids);
        }

        internal static bool SimilarPaths(List<QaPoint> a, List<QaPoint> b, float tolerance)
            => Covered(a, b, tolerance) && Covered(b, a, tolerance);

        static bool Covered(List<QaPoint> a, List<QaPoint> b, float tolerance)
        {
            float t2 = tolerance * tolerance;
            foreach (QaPoint p in a)
            {
                bool near = false;
                foreach (QaPoint q in b) if ((p.Vector() - q.Vector()).sqrMagnitude <= t2) { near = true; break; }
                if (!near) return false;
            }
            return true;
        }

        static void AddSolution(LevelData level, QaLevelReport report, RouteQaSettings cfg, List<QaCommand> commands, float phase, string source)
        {
            QaPlan plan = Replay(level, commands, phase, source);
            if (!plan.sunk || plan.waterEvents > 0) return;   // a plan that drowns is a penalty, not a route
            report.winningPlans++;
            string portals = Portals(plan);
            bool sand = plan.sandSeconds > 0f, ice = plan.iceSeconds > 0f;
            List<QaPoint> points = Points(plan);
            foreach (QaFamily f in report.families)
            {
                if (f.usesSand != sand || f.usesIce != ice || f.portalSequence != portals ||
                    !SimilarPaths(points, Points(f.representative), cfg.routeSeparation)) continue;
                f.observedSolutions++;
                // Retain the cheapest representative; shorter distance breaks shot-count ties.
                if (plan.shots.Count < f.representative.shots.Count ||
                    (plan.shots.Count == f.representative.shots.Count && plan.pathLength < f.representative.pathLength)) f.representative = plan;
                return;
            }
            if (report.families.Count >= cfg.maxFamilies)
            {
                report.familyLimitReached = true;
                // A late cheap route must not be hidden by early four-shot policy detours.
                // Preserve the last representative of each observed sand/non-sand category when possible.
                int worst = -1;
                foreach (QaFamily f in report.families)
                {
                    int categoryCount = report.families.FindAll(x => x.usesSand == f.usesSand).Count;
                    if (categoryCount == 1 && f.usesSand != sand && cfg.maxFamilies > 1) continue;
                    if (worst < 0 || ComparePlans(f.representative, report.families[worst].representative) > 0) worst = f.id - 1;
                }
                bool newCategory = !report.families.Exists(f => f.usesSand == sand);
                if (worst >= 0 && (newCategory || ComparePlans(plan, report.families[worst].representative) < 0))
                    report.families[worst] = new QaFamily { id = worst + 1, observedSolutions = 1,
                        usesSand = sand, usesIce = ice, portalSequence = portals, representative = plan };
                return;
            }
            report.families.Add(new QaFamily { id = report.families.Count + 1, observedSolutions = 1,
                usesSand = sand, usesIce = ice, portalSequence = portals, representative = plan });
        }

        static int ComparePlans(QaPlan a, QaPlan b)
        {
            int c = a.shots.Count.CompareTo(b.shots.Count);
            return c != 0 ? c : a.pathLength.CompareTo(b.pathLength);
        }

        /// <summary>Replays whole plans from tee; never snaps the ball to nominal intermediate rests.</summary>
        public static void TestRobustness(LevelData level, QaPlan plan, RouteQaSettings cfg)
        {
            List<QaCommand> commands = Commands(plan);
            int count = commands.Count;
            var rng = new System.Random(cfg.seed);
            plan.robustnessTrials = plan.robustnessSuccesses = plan.sandFreeSuccesses = plan.impactFreeSuccesses = plan.waterFreeSuccesses = 0;
            plan.meanShotsConsumed = plan.meanRemainingDistance = 0f;
            // Nominal, each individual shot at both signed errors, then seeded independent errors on all shots.
            int axisTrials = count * (level.HasMovers ? 6 : 4);
            int total = 1 + axisTrials + cfg.robustnessSamples;
            for (int trial = 0; trial < total; trial++)
            {
                var ae = new float[count]; var pe = new float[count]; var te = new float[count];
                float initialTime = plan.initialTime;
                if (trial > 0 && trial <= axisTrials)
                {
                    int axes = level.HasMovers ? 6 : 4;
                    int shot = (trial - 1) / axes, axis = (trial - 1) % axes;
                    float sign = axis % 2 == 0 ? -1f : 1f;
                    if (axis < 2) ae[shot] = sign * cfg.angleError;
                    else if (axis < 4) pe[shot] = sign * cfg.powerError;
                    else if (shot == 0) initialTime += sign * cfg.timingError;
                    else te[shot] = axis % 2 == 0 ? 0f : 2f * cfg.timingError;
                }
                else if (trial > axisTrials)
                {
                    for (int i = 0; i < count; i++)
                    {
                        ae[i] = ((float)rng.NextDouble() * 2f - 1f) * cfg.angleError;
                        pe[i] = ((float)rng.NextDouble() * 2f - 1f) * cfg.powerError;
                        if (level.HasMovers && i > 0) te[i] = (float)rng.NextDouble() * 2f * cfg.timingError;
                    }
                    if (level.HasMovers) initialTime += ((float)rng.NextDouble() * 2f - 1f) * cfg.timingError;
                }
                QaPlan run = Replay(level, commands, initialTime, "perturbed-replay", ae, pe, te);
                plan.robustnessTrials++;
                if (run.sunk)
                {
                    plan.robustnessSuccesses++;
                    if (run.sandSeconds == 0f) plan.sandFreeSuccesses++;
                    if (run.impactSteps == 0) plan.impactFreeSuccesses++;
                    if (run.waterEvents == 0) plan.waterFreeSuccesses++;
                }
                plan.meanShotsConsumed += run.shots.Count;
                plan.meanRemainingDistance += run.sunk ? 0f : (run.finalPosition.Vector() - level.hole).magnitude;
            }
            plan.meanShotsConsumed /= total;
            plan.meanRemainingDistance /= total;
        }
    }
}
