using System;
using System.Collections.Generic;
using PocketPutt.Levels;
using PocketPutt.Sim;
using UnityEngine;

namespace PocketPutt.QA
{
    /// <summary>Synthetic QA fixtures and two Garden design regressions. No campaign content, saves, scenes or packages are modified.</summary>
    public static class RouteQaChecks
    {
        /// <summary>Number of check groups run by the last RunAll call.</summary>
        public static int GroupCount { get; private set; }

        public static List<string> RunAll()
        {
            GroupCount = 0;
            var failures = new List<string>();
            Check(failures, "runtime parity", RuntimeParity);
            Check(failures, "direct grids and tutorial exemption", DirectScan);
            Check(failures, "re-aim after rest", ReaimPolicy);
            Check(failures, "full replay carries position and clock", ReplayCarriesState);
            Check(failures, "sand and tunnel measurements", SurfacesAndPortals);
            Check(failures, "spatial route grouping", RouteGrouping);
            Check(failures, "bounded deterministic search", BoundedSearch);
            Check(failures, "whole-plan robustness", Robustness);
            Check(failures, "invalid settings", InvalidSettings);
            Check(failures, "water: contact geometry", WaterGeometry);
            Check(failures, "water: return to tee, stroke counted", WaterReturnsToTee);
            Check(failures, "water: lake cannot be crossed at any speed", WaterNeverCrossed);
            Check(failures, "water: movers, portals and precedence", WaterMoversAndPortals);
            Check(failures, "water: repeated falls never softlock", WaterRepeated);
            Check(failures, "water: authoring validation", WaterValidation);
            Check(failures, "water: solver agreement", WaterSolver);
            Check(failures, "mover-safe rest spots", MoverRestSpots);
            Check(failures, "garden: planned routes and double bank", GardenRoutes);
            return failures;
        }

        static void Check(List<string> failures, string name, Action test)
        {
            GroupCount++;
            try { test(); }
            catch (Exception e) { failures.Add(name + ": " + e.Message); }
        }
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Near(float a, float b, string message) => Require(Mathf.Abs(a - b) < 0.00001f, message + $" ({a} != {b})");
        static LevelBuilder Lane(string id = "QA-LANE") => new LevelBuilder(id, "QA", "QA", Theme.Garden, 1, 2)
            .Floor(-2f, -4f, 2f, 4f).Start(0f, -2.45f).Hole(0f, 2.45f);
        static RouteQaSettings Tiny() => new RouteQaSettings { directOffsets = new[] { 0f }, directPowers = new[] { 0.8f, 1f },
            searchShotBudget = 40, searchDepth = 1, phaseSamples = 2, robustnessSamples = 2, maxFamilies = 8 };

        static void GardenRoutes()
        {
            var shortcut = LevelLibrary.Get("C1-05");
            var banks = LevelLibrary.Get("C1-08");
            foreach (var level in new[] { shortcut, banks })
            {
                float aim = Mathf.Atan2(level.hole.y - level.start.y, level.hole.x - level.start.x) * Mathf.Rad2Deg;
                for (int offset = -3; offset <= 3; offset++)
                for (int p = 0; p <= 8; p++)
                    Require(!Solver.Shoot(level, level.start, aim + offset, 0.8f + p * 0.025f).sunk,
                        level.id + ": near-direct high power bypasses the route decision");
            }
            var route = RouteQa.Replay(shortcut, new[] { new QaCommand(184.5f, 0.855f), new QaCommand(58.25741f, 0.565804362f) });
            Require(route.sunk && route.waterEvents == 0 && route.impactSteps == 1 && route.shots.Count == 2,
                "shortcut must finish in two putts with one deliberate bank");
            Require(route.shots[0].impacts[0].x < -1f && route.shots[0].impacts[0].y < -2f,
                "shortcut must use the visible lower-left reflector");
            RouteQa.TestRobustness(shortcut, route, new RouteQaSettings());
            Require(route.robustnessSuccesses >= 35, "shortcut lost its whole-plan tolerance (at least 35/41)");
            var safe = RouteQa.Replay(shortcut, new[] { new QaCommand(36.65611f, 0.584128f),
                new QaCommand(90.00002f, 0.683797359f), new QaCommand(173.290176f, 0.5688302f) });
            Require(safe.sunk && safe.shots.Count == 3 && safe.waterEvents == 0 && safe.impactSteps == 0,
                "right flank must remain a dry three-putt alternative without banks");
            RouteQa.TestRobustness(shortcut, safe, new RouteQaSettings());
            Require(safe.robustnessSuccesses >= 30, "dry route lost its measured whole-plan margin (at least 30/45)");
            var bank = RouteQa.Replay(banks, new[] { new QaCommand(98.75f, 0.95809525f) });
            Require(bank.sunk && bank.impactSteps == 2 && bank.shots[0].impacts[0].x < 0 && bank.shots[0].impacts[1].x > 0,
                "double bank must hit the left and right faces, once each");
            RouteQa.TestRobustness(banks, bank, new RouteQaSettings());
            Require(bank.robustnessSuccesses >= 35, "double bank lost its whole-plan tolerance (at least 35/37)");
        }

        static void RuntimeParity()
        {
            var fixtures = new[] {
                Lane().Done(), Lane().Zone(Surface.Sand, -2, -1, 2, 1).Done(),
                Lane().Zone(Surface.Ice, -2, -4, 2, 4).Done(),
                Lane().Tunnel(0, -1, 0, -1, 0, 1, 0, 1).Done(),
                Lane().Rail(-2, 0, 0.4f, 0).Done(),
                Lane().Spinner(0, 0, 0.9f, 60).Done(),
                Lane().Slider(-2, 0, -0.2f, 0, 1, 0, 1.5f, 6).Done()
            };
            foreach (LevelData lv in fixtures)
            foreach (float angle in new[] { 45f, 90f, 120f })
            foreach (float power in new[] { 0.4f, 0.8f, 1f })
            foreach (float phase in Solver.PhasesFor(lv, 3))
            {
                var state = new BallState { pos = lv.start, time = phase };
                RouteQa.Run run = RouteQa.Shoot(lv, state, angle, power, true);
                Solver.ShotOutcome expected = Solver.Shoot(lv, lv.start, angle, power, null, phase);
                Require(run.shot.sunk == expected.sunk, "sunk diverged from Solver.Shoot");
                Near(run.state.pos.x, expected.rest.x, "rest x"); Near(run.state.pos.y, expected.rest.y, "rest y");
                Require((run.state.vel.sqrMagnitude == 0f) == expected.wasStopped, "rest status diverged");
                BallSim.Launch(ref state, new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)), power);
                int steps = BallSim.RunToRest(ref state, lv);
                Require(run.shot.steps == steps, "step count differs from RunToRest");
                Near(run.state.time, state.time, "simulation clock");
                Require(run.shot.timedOut == (!state.sunk && state.vel.sqrMagnitude > 0), "timeout status");
            }
        }

        static void DirectScan()
        {
            var cfg = Tiny(); cfg.directOffsets = new[] { -3f, 0f, 3f }; cfg.directPowers = new[] { 0.7f, 0.8f, 1f };
            QaLevelReport r = RouteQa.Analyze(Lane().Done(), cfg);
            Require(r.direct.attempts == 9 && r.direct.exactAttempts == 3 && r.direct.highAttempts == 6 && r.direct.exactHighAttempts == 2,
                "scan denominators or inclusive 0.8 threshold");
            Require(r.direct.highSunk > 0 && r.highPowerReview, "straight high-power shortcut was missed");
            QaLevelReport tutorial = RouteQa.Analyze(Lane("C1-01").Done(), cfg);
            Require(tutorial.direct.highSunk == r.direct.highSunk && !tutorial.highPowerReview && tutorial.tutorialException,
                "tutorial must retain evidence and suppress the design-review flag");
            Require(new RouteQaSettings().directPowers.Length == 37, "default power coverage");
        }

        static LevelData LongLane() => new LevelBuilder("QA-LONG", "QA", "QA", Theme.Garden, 2, 3)
            .Floor(-3, -2, 3, 22).Start(0, 0).Hole(0, 19).Done();

        static void ReaimPolicy()
        {
            var cfg = Tiny(); cfg.directPowers = new[] { 1f }; cfg.policyMaxShots = 3;
            QaLevelReport r = RouteQa.Analyze(LongLane(), cfg);
            Require(r.direct.sunk == 0, "long lane should not complete in one shot");
            Require(r.policies.bestHighShots == 2 && r.policies.highCompletedWithinThreeStar == 1 && r.trivialPolicyReview,
                "high-power re-aim should find two-shot trivial policy");
            Require(r.policies.bestHigh.shots[1].from.y > 9f, "second policy shot did not start at actual rest");
        }

        static void ReplayCarriesState()
        {
            LevelData lv = LongLane();
            var commands = new[] { new QaCommand(90f, 1f), new QaCommand(90f, 1f) };
            QaPlan plan = RouteQa.Replay(lv, commands, 1.25f, angleErrors: new[] { 3f, 0f });
            Require(plan.shots.Count == 2, "expected two shots");
            Near(plan.shots[1].from.x, plan.shots[0].rest.x, "intermediate x must propagate");
            Near(plan.shots[1].from.y, plan.shots[0].rest.y, "intermediate y must propagate");
            Require(Mathf.Abs(plan.shots[1].from.x) > 0.1f, "perturbation lost by snapping to nominal rest");
            Near(plan.shots[1].startTime, plan.shots[0].endTime, "clock reset between shots");
            QaPlan cancelled = RouteQa.Replay(lv, new[] { new QaCommand(90, 0.1f) }, powerErrors: new[] { -0.03f });
            Require(cancelled.rejectedShot && cancelled.shots.Count == 0 && !cancelled.sunk, "touch power below dead zone should cancel");
            LevelData mover = new LevelBuilder("QA-WAIT", "QA", "QA", Theme.Workshop, 2, 3)
                .Floor(-3, -2, 3, 22).Start(0, 0).Hole(0, 19).Spinner(2.4f, 10, 0.2f, 60).Done();
            QaPlan waited = RouteQa.Replay(mover, commands, timingErrors: new[] { 0f, 0.24f });
            Require(waited.shots.Count == 2, "off-path mover should permit the second shot");
            float expectedWait = Mathf.RoundToInt(0.24f / Tuning.StepDt) * Tuning.StepDt;
            Near(waited.shots[1].startTime, waited.shots[0].endTime + expectedWait, "preparation wait must advance real mover time");
        }

        static void SurfacesAndPortals()
        {
            LevelData sand = Lane().Zone(Surface.Sand, -2, -4, 2, 4).Done();
            QaShot s = RouteQa.Shoot(sand, new BallState { pos = sand.start }, 90, 1, true).shot;
            Require(s.sandSeconds > 0 && s.iceSeconds == 0 && s.portalEvents == 0, "sand exposure missing");
            LevelData tunnel = new LevelBuilder("QA-PORTAL", "QA", "QA", Theme.Glacier, 1, 2)
                .Floor(-2, -3, 2, 3).Floor(-2, 9, 2, 15).Start(0, -2).Hole(0, 13)
                .Tunnel(0, -1, 0, -1, 0, 11, 0, 1).Done();
            QaShot p = RouteQa.Shoot(tunnel, new BallState { pos = tunnel.start }, 90, 0.65f, true).shot;
            Require(p.portalEvents == 1 && p.portalIndices[0] == 0, "portal event missing");
            Require(p.pathLength < 10f, "portal chord incorrectly counted as distance rolled");
            Require(!p.leftFloor, "portal gap incorrectly classified as escaped floor");
        }

        static void RouteGrouping()
        {
            var line = new List<QaPoint> { new QaPoint(new Vector2(0, 0)), new QaPoint(new Vector2(0, 1)), new QaPoint(new Vector2(0, 2)) };
            var jitter = new List<QaPoint> { new QaPoint(new Vector2(0.1f, 0)), new QaPoint(new Vector2(0.1f, 1)), new QaPoint(new Vector2(0.1f, 2)) };
            var detour = new List<QaPoint>(line) { new QaPoint(new Vector2(2, 1)) };
            Require(RouteQa.SimilarPaths(line, jitter, 0.65f), "tiny aim variations counted as distinct route");
            Require(!RouteQa.SimilarPaths(line, detour, 0.65f) && !RouteQa.SimilarPaths(detour, line, 0.65f), "detour merged; comparison must be symmetric");
        }

        static void BoundedSearch()
        {
            var cfg = Tiny(); cfg.searchShotBudget = 17; cfg.searchDepth = 3;
            LevelData lv = Lane().Rail(-2, 0, 2, 0).Done();
            QaLevelReport a = RouteQa.Analyze(lv, cfg), b = RouteQa.Analyze(lv, cfg);
            Require(a.searchShots == 17 && a.searchBudgetExhausted, "hard shot budget ignored");
            Require(a.families.Count == 0, "fully divided lane should not have a solution");
            Require(a.direct.sunk == b.direct.sunk && a.searchShots == b.searchShots && a.winningPlans == b.winningPlans, "run is nondeterministic");
            cfg.phaseSamples = 3;
            QaLevelReport mover = RouteQa.Analyze(Lane().Spinner(0, 0, 0.9f, 60).Done(), cfg);
            Require(mover.direct.attempts == cfg.directPowers.Length * 3, "mover phases missing from scan");
        }

        static void Robustness()
        {
            LevelData lv = LongLane();
            var cfg = Tiny(); cfg.angleError = 3; cfg.powerError = 0.03f; cfg.robustnessSamples = 12;
            QaPlan plan = RouteQa.Replay(lv, new[] { new QaCommand(90, 1), new QaCommand(90, 1) });
            Require(plan.sunk, "nominal two-shot fixture must complete");
            RouteQa.TestRobustness(lv, plan, cfg);
            Require(plan.robustnessTrials == 21, "must perturb both shots, not just the final shot");
            Require(plan.robustnessSuccesses > 0 && plan.robustnessSuccesses < plan.robustnessTrials, "whole-plan errors did not affect completion");
            int first = plan.robustnessSuccesses;
            RouteQa.TestRobustness(lv, plan, cfg);
            Require(first == plan.robustnessSuccesses, "seeded robustness is not reproducible");
        }

        // ---- water ---------------------------------------------------------------------------

        static LevelData Pond() => Lane("QA-POND").Water(-0.5f, -0.5f, 0.5f, 0.5f).Done();
        static Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

        static void WaterGeometry()
        {
            LevelData lv = Lane("QA-EDGE").Water(0.2f, -0.5f, 1f, 0.5f).Done();
            float r = Tuning.BallRadius;
            Require(!lv.TouchesWater(new Vector2(0.2f - r - 0.001f, 0f)), "just outside the face counted as wet");
            Require(lv.TouchesWater(new Vector2(0.2f - r + 0.001f, 0f)), "just inside the face missed");
            Require(lv.TouchesWater(new Vector2(0.2f - r, 0f)), "tangency must count as contact");
            Require(lv.TouchesWater(new Vector2(0.5f, 0f)), "centre inside missed");
            // Corner: the swept disc is round there, so a diagonal near-miss that an expanded AABB would flag stays dry.
            float c45 = r * 0.7071f;
            Require(lv.TouchesWater(new Vector2(0.2f - c45 * 0.9f, 0.5f + c45 * 0.9f)), "corner contact missed");
            Require(!lv.TouchesWater(new Vector2(0.2f - c45 * 1.1f, 0.5f + c45 * 1.1f)), "corner miss flagged wet (AABB false positive)");
            LevelData none = Lane("QA-DRY").Done();
            Require(!none.HasWater && !none.TouchesWater(Vector2.zero), "level without water must never be wet");
            LevelData two = Lane("QA-TWO").Water(-1.5f, -0.5f, -0.5f, 0.5f).Water(0.5f, -0.5f, 1.5f, 0.5f).Done();
            Require(two.TouchesWater(new Vector2(-1f, 0f)) && two.TouchesWater(new Vector2(1f, 0f)) && !two.TouchesWater(Vector2.zero), "two lakes");
        }

        static void WaterReturnsToTee()
        {
            LevelData lv = Pond();
            QaShot s = RouteQa.Shoot(lv, new BallState { pos = lv.start }, 90f, 1f, true).shot;
            Require(s.waterEvents == 1 && !s.sunk && !s.timedOut, "straight high-power shot over the pond must drown exactly once");
            Near(s.rest.x, lv.start.x, "rest x is the tee"); Near(s.rest.y, lv.start.y, "rest y is the tee");
            Require(s.waterPoint.y < -0.5f + 0.001f && s.waterPoint.y > -0.5f - 0.2f, "contact point should be on the near shore");
            RouteQa.Run run = RouteQa.Shoot(lv, new BallState { pos = lv.start }, 90f, 1f, false);
            Require(run.state.vel == Vector2.zero && run.state.drowned && !run.state.sunk, "ball must be at rest and flagged drowned");
            Require(run.state.time > 0f, "clock must keep what elapsed");
            // A fresh launch clears the flag; a later dry shot is a normal rest.
            BallState again = run.state;
            BallSim.Launch(ref again, Dir(90f), 0.3f);
            Require(!again.drowned, "launch must clear the drowned flag");
            Solver.ShotOutcome o = Solver.Shoot(lv, lv.start, 90f, 1f);
            Require(o.water && !o.wasStopped && !o.sunk, "solver outcome must say water and not a rest");

            // Stroke accounting: drown, then play around the lake and sink; two strokes, one fall.
            LevelData side = Lane("QA-SIDE").Water(0.3f, -1f, 1.2f, 1f).Done();
            float power = -1f;
            foreach (float p in new[] { 0.7f, 0.65f, 0.6f, 0.75f, 0.55f, 0.8f, 0.5f })
                if (Solver.Shoot(side, side.start, 90f, p).sunk) { power = p; break; }
            Require(power > 0f, "fixture has no straight dry shot");
            float toLake = Mathf.Atan2(2.45f, 0.75f) * Mathf.Rad2Deg;
            QaPlan plan = RouteQa.Replay(side, new[] { new QaCommand(toLake, 1f), new QaCommand(90f, power) });
            Require(plan.shots.Count == 2 && plan.waterEvents == 1 && plan.sunk, "fall then sink must be two strokes and one fall");
            Near(plan.shots[1].from.x, side.start.x, "second stroke starts at the tee"); Near(plan.shots[1].from.y, side.start.y, "second stroke starts at the tee (z)");
            QaPlan twin = RouteQa.Replay(side, new[] { new QaCommand(toLake, 1f), new QaCommand(90f, power) });
            Require(twin.finalTime == plan.finalTime && twin.shots[0].steps == plan.shots[0].steps, "replay is not deterministic");
        }

        static void WaterNeverCrossed()
        {
            // A lake spanning the whole lane: nothing may end beyond it unless it fell in.
            LevelData lv = Lane("QA-BARRIER").Water(-2f, 0f, 2f, 0.5f).Done();
            int drowned = 0, shots = 0;
            for (float a = 0f; a < 360f; a += 1.5f)
            foreach (float p in new[] { 0.3f, 0.6f, 0.9f, 1f })
            {
                RouteQa.Run run = RouteQa.Shoot(lv, new BallState { pos = lv.start }, a, p, false);
                shots++;
                if (run.shot.waterEvents > 0) drowned++;
                Require(!(run.state.pos.y > 0.5f), $"ball crossed the lake without falling (angle {a}, power {p})");
                Require(!run.shot.timedOut, "timeout");
            }
            Require(drowned > shots / 10, "sweep never reached the water; fixture is vacuous");
        }

        static void WaterMoversAndPortals()
        {
            // A slider shoves a resting ball into the lake: the fall happens without any new stroke.
            LevelData pusher = Lane("QA-PUSH").Water(0.5f, -0.3f, 1.6f, 0.3f)
                .Slider(-0.6f, -1f, -0.6f, 1f, 1f, 0f, 2.2f, 4f).Done();
            var s = new BallState { pos = new Vector2(-0.3f, 0f) };
            StepEvents ev = default;
            bool fell = false;
            for (int i = 0; i < 600 && !fell; i++)
            {
                BallSim.Step(ref s, pusher, ref ev);
                fell = ev.water;
            }
            Require(fell, "mover push into the lake was not detected");
            Require(s.pos == pusher.start && s.vel == Vector2.zero && s.drowned, "pushed ball must be back on the tee at rest");
            Require(s.time > 0.5f, "clock must run on through a fall at rest");

            // Portal exit placed in water (illegal authoring, but must resolve as a fall in the same tick).
            LevelData tunnel = Lane("QA-WETEXIT").Water(-0.6f, 0.6f, 0.6f, 1.4f).Tunnel(0f, -1f, 0f, -1f, 0f, 1f, 0f, 1f).Done();
            RouteQa.Run r = RouteQa.Shoot(tunnel, new BallState { pos = tunnel.start }, 90f, 0.65f, true);
            Require(r.shot.waterEvents == 1 && r.shot.portalEvents == 0 && !r.shot.sunk, "portal exit into water must be a fall, not a teleport");
            Require(tunnel.WaterIssues().Exists(x => x.Contains("tunnel exit")), "validation must flag a wet tunnel exit");

            // Water beats the hole: a lake touching the cup radius cannot award a sink.
            LevelData cup = Lane("QA-CUP").Water(-0.6f, 2.3f, 0.6f, 2.7f).Done();
            Require(cup.WaterIssues().Exists(x => x.Contains("hole")), "validation must flag a wet hole");
            RouteQa.Run h = RouteQa.Shoot(cup, new BallState { pos = cup.start }, 90f, 0.62f, false);
            Require(!h.state.sunk, "ball reached the cup through the lake");
        }

        static void WaterRepeated()
        {
            LevelData lv = Pond();
            var commands = new List<QaCommand>();
            for (int i = 0; i < 25; i++) commands.Add(new QaCommand(90f, 0.9f + (i % 3) * 0.05f));
            QaPlan plan = RouteQa.Replay(lv, commands);
            Require(plan.shots.Count == 25 && plan.waterEvents == 25 && !plan.sunk && !plan.timedOut, "every stroke should count and fall once");
            foreach (QaShot s in plan.shots) Require(s.from.Vector() == lv.start && s.steps < 600, "each fall must end its stroke promptly from the tee");
        }

        static void WaterValidation()
        {
            Require(Pond().WaterIssues().Count == 0, "valid pond reported issues: " + string.Join(";", Pond().WaterIssues()));
            Require(Lane("QA-TEE").Water(-0.5f, -2.8f, 0.5f, -2.3f).Done().WaterIssues().Exists(x => x.Contains("tee")), "wet tee not flagged");
            LevelData u = new LevelBuilder("QA-U", "QA", "QA", Theme.Garden, 1, 2)
                .Floor(-2f, -3f, -0.5f, 3f).Floor(-2f, 2f, 2f, 3f).Floor(0.5f, -3f, 2f, 3f)
                .Start(-1.2f, -2.4f).Hole(1.2f, -2.4f).Water(-1f, -1f, 1f, 1f).Done();
            Require(u.WaterIssues().Exists(x => x.Contains("not fully on the floor")), "lake over the notch of a U must be flagged");
            var bad = Lane("QA-BAD");
            bad.data.water.Add(new RectF(1f, 0f, 0.5f, 1f));
            Require(bad.Done().WaterIssues().Exists(x => x.Contains("degenerate")), "inverted rect not flagged");
            Require(Lane("QA-SAND").Zone(Surface.Sand, -1f, -1f, 1f, 1f).Water(-0.5f, -0.5f, 0.5f, 0.5f).Done().WaterIssues().Count == 0,
                "a sand/ice zone under a lake is allowed (water wins)");
            LevelData wetZone = Lane("QA-ICEPOND").Zone(Surface.Ice, -2f, -4f, 2f, 4f).Water(-0.5f, -0.5f, 0.5f, 0.5f).Done();
            RouteQa.Run fall = RouteQa.Shoot(wetZone, new BallState { pos = wetZone.start }, 90f, 0.6f, false);
            Require(fall.shot.waterEvents == 1, "ice under the lake must not stop the fall");
            Require(Lane("QA-BLOCK").BlockRect(-1f, -1f, 1f, 1f).Water(-0.5f, -0.5f, 0.5f, 0.5f).Done().WaterIssues().Exists(x => x.Contains("block")),
                "water over a block must be flagged");
        }

        static void WaterSolver()
        {
            // Lake covers the lane except a gap on the right: the solver must route through the gap, never through water.
            LevelData lv = Lane("QA-GAP").Water(-2f, -0.4f, 0.9f, 0.4f).Done();
            SolveResult res = Solver.Solve(lv, 3);
            Require(res.solvable, "gap level should be solvable");
            foreach (ShotPlan sp in res.plan)
                Require(!Solver.Shoot(lv, sp.from, sp.angleDeg, sp.power, null, sp.startTime).water, "solver plan falls in the lake");
            QaPlan replay = RouteQa.Replay(lv, res.plan.ConvertAll(sp => new QaCommand(sp.angleDeg, sp.power)));
            Require(replay.sunk && replay.waterEvents == 0, "runtime replay of the solver plan disagrees");
            Require(replay.shots.Count == res.minShots, "stroke count differs between solver and runtime");
        }

        static void MoverRestSpots()
        {
            LevelData spin = Lane("QA-ROT").Spinner(0f, 0f, 0.9f, 60f).Done();
            Require(!spin.RestIsSafe(new Vector2(0.6f, 0f)), "a spot inside the rotor disc is not a safe rest");
            Require(spin.RestIsSafe(spin.start), "the tee outside the rotor disc is safe");
            LevelData slide = Lane("QA-SLIDE").Slider(-2f, 0f, -0.2f, 0f, 1f, 0f, 1.5f, 6f).Done();
            Require(!slide.RestIsSafe(new Vector2(1.0f, 0f)), "the slider reaches x=1.0 at the end of its travel");
            Require(slide.RestIsSafe(new Vector2(1.8f, 0f)), "x=1.8 is never touched by the slider");
            Require(Lane("QA-STATIC").Done().RestIsSafe(Vector2.zero), "no movers: always safe");
            // The solver must not plan a second stroke from a spot the rotor sweeps.
            SolveResult res = Solver.Solve(Lane("QA-ROT2").Spinner(0f, 0f, 0.9f, 60f).Done(), 2);
            foreach (ShotPlan sp in res.plan) Require(Lane("QA-ROT3").Spinner(0f, 0f, 0.9f, 60f).Done().RestIsSafe(sp.from), "solver plan starts from an unsafe rest");
        }

        static void InvalidSettings()
        {
            var cfg = Tiny(); cfg.directPowers = new[] { float.NaN };
            bool failed = false;
            try { cfg.Validate(); } catch (ArgumentException) { failed = true; }
            Require(failed, "NaN input accepted");
        }
    }
}
