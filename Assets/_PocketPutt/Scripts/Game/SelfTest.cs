using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PocketPutt.Levels;
using PocketPutt.Save;
using PocketPutt.Sim;
using PocketPutt.UI;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using MouseButton = UnityEngine.InputSystem.LowLevel.MouseButton;

namespace PocketPutt.Game
{
    /// <summary>
    /// Scripted autopilot for headless QA: -pp-script "level:C1-01;wait:1;solve;shot:name;quit" -pp-out dir.
    /// Only active when the argument is present; never part of normal play.
    /// </summary>
    static class VisualElementProxy
    {
        public static void Find(GameApp app, string name, out Vector2 pos, out bool ok)
        {
            pos = default;
            var e = app.UI.FindElement(name);
            // Both hub screens have a Back button. Query the active screen so a
            // hidden overview button cannot supply stale coordinates for map QA.
            if (name == "hub-back")
                e = app.UI.Hub.Current == HubController.View.Map
                    ? app.UI.Hub.Map.Q(name) : app.UI.Hub.Overview.Q(name);
            ok = e != null && app.UI.TryScreenCentre(e, out pos);
        }
    }

    public sealed class SelfTest : MonoBehaviour
    {
        string outDir;
        Vector2 mousePos;
        bool mouseDown;
        readonly List<string> log = new List<string>();
        int failures;

        public static void MaybeStart(GameApp app)
        {
            string script = GameApp.CommandLineValue("-pp-script");
            if (string.IsNullOrEmpty(script)) return;
            var go = new GameObject("SelfTest");
            go.transform.SetParent(app.transform, false);
            go.AddComponent<SelfTest>().Begin(app, script);
        }

        void Begin(GameApp app, string script)
        {
            Debug.Log("[Selftest] begin " + script);
            // scripted runs must not freeze when another window takes focus (the shipped setting stays runInBackground=false)
            Application.runInBackground = true;
            outDir = GameApp.CommandLineValue("-pp-out") ?? Path.Combine(Application.persistentDataPath, "selftest");
            Directory.CreateDirectory(outDir);
            StartCoroutine(Run(app, script.Split(';')));
        }

        void Log(string s)
        {
            log.Add(s);
            Debug.Log("[Selftest] " + s);
        }

        IEnumerator Run(GameApp app, string[] steps)
        {
            yield return null;
            yield return null;
            foreach (string raw in steps)
            {
                string step = raw.Trim();
                if (step.Length == 0) continue;
                int c = step.IndexOf(':');
                string cmd = c < 0 ? step : step.Substring(0, c);
                string arg = c < 0 ? "" : step.Substring(c + 1);
                switch (cmd)
                {
                    case "wait":
                        yield return new WaitForSecondsRealtime(float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture));
                        break;
                    case "shot":
                    {
                        yield return new WaitForEndOfFrame();
                        string path = Path.Combine(outDir, arg + ".png").Replace('\\', '/');
                        ScreenCapture.CaptureScreenshot(path);
                        yield return null; yield return null; yield return null;
                        Log($"shot {path} ({Screen.width}x{Screen.height})");
                        break;
                    }
                    case "menu": app.GoMenu(); break;
                    case "cups": app.ShowCups(int.Parse(arg)); break;
                    case "overview": app.ShowCupOverview(arg.Length > 0 ? int.Parse(arg) : -1); break;
                    case "map": { string[] mp = arg.Split(','); app.ShowCupMap(int.Parse(mp[0]), mp.Length > 1 ? mp[1] : null); break; }
                    case "back": app.Back(); break;
                    case "seed": SeedScenario(app, arg); break;
                    case "tapnode": yield return TapNode(app, arg); break;
                    case "tapui": yield return TapUi(app, arg); break;
                    case "tapcup": yield return TapCup(app, arg); break;
                    case "tapcupat": yield return TapCupAt(app, arg); break;
                    case "tapplaque": yield return TapPlaque(app, arg); break;
                    case "swipecup": yield return SwipeCup(app, arg); break;
                    case "swipe": yield return Swipe(app, arg); break;
                    case "hubwait": { float tw = 0f; while (tw < 3f && app.UI.Hub.Current == HubController.View.Map && app.UI.Hub.Map.Moving) { tw += Time.unscaledDeltaTime; yield return null; } yield return null; break; }
                    case "hubexpect": HubExpect(app, arg); break;
                    case "lang": app.RebuildUI(int.Parse(arg)); break;
                    case "hubmem": yield return HubMemory(app, int.Parse(arg)); break;
                    case "homemem": yield return HomeMemory(app, int.Parse(arg)); break;
                    case "memseries": yield return MemorySeries(app, arg); break;
                    case "objstat": ObjStat(app, arg); break;
                    case "toggle":   // F2 isolation probe: toggle:app (GameApp.Update/UI.Tick), toggle:ui (UIDocument), toggle:runner
                    {
                        Behaviour b = arg == "app" ? app : arg == "ui" ? app.GetComponentInChildren<UnityEngine.UIElements.UIDocument>() : (Behaviour)app.Runner;
                        b.enabled = !b.enabled;
                        Log($"toggle {arg} enabled={b.enabled}");
                        break;
                    }
                    case "perf": yield return Perf(app, arg); break;
                    case "ambient":   // H6: ambient:off | ambient:on | ambient:fire[,index] (starts that cup event now)
                    {
                        if (arg == "off") CupAmbientView.Enabled = false;
                        else if (arg == "on") CupAmbientView.Enabled = true;
                        else if (arg == "reset") CupAmbientView.ResetStats();
                        else if (arg == "stat") Log(CupAmbientView.Stats());
                        else if (arg.StartsWith("mask,")) CupAmbientView.Mask = int.Parse(arg.Substring(5));
                        else if (arg.StartsWith("fire"))
                        {
                            int idx = arg.Length > 5 ? int.Parse(arg.Substring(5)) : 0;
                            bool fired = app.UI.Hub.Current == HubController.View.Map && app.UI.Hub.Map.Ambient.Fire(idx);
                            Log($"ambient fire index={idx} fired={fired} event={app.UI.Hub.Map.Ambient.CurrentEvent}");
                        }
                        break;
                    }
                    case "hubdebug": app.UI.Hub.Map.SetDebug(arg == "1"); break;
                    case "swipenode": yield return SwipeNode(app, arg); break;
                    case "trophies": app.ShowTrophies(); break;
                    case "settings": app.UI.ShowSettings(); break;
                    case "level": app.StartLevel(arg); break;
                    case "restart": app.RestartLevel(); break;
                    case "shoot":
                    {
                        string[] p = arg.Split(',');
                        float ang = float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture);
                        float pow = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
                        Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
                        bool ok = app.Runner.Shoot(dir, pow);
                        if (!ok) { failures++; Log("FAIL shoot rejected"); }
                        break;
                    }
                    case "press": case "move": case "release":
                    {
                        Mouse m = Mouse.current ?? InputSystem.AddDevice<Mouse>();
                        if (cmd != "release") { string[] q = arg.Split(','); mousePos = new Vector2(float.Parse(q[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(q[1], System.Globalization.CultureInfo.InvariantCulture)); }
                        mouseDown = cmd != "release";
                        var st = new MouseState { position = mousePos };
                        if (mouseDown) st = st.WithButton(MouseButton.Left);
                        InputSystem.QueueStateEvent(m, st);
                        yield return null; yield return null;
                        break;
                    }
                    case "rest":
                        yield return Settle(app);
                        break;
                    case "holepos":
                    {
                        // logs where the hole and the ball are on screen (px, origin bottom-left) so captures can be cropped around the cup
                        Vector2 hs = ScreenOf(app, app.CurrentLevel.hole), bs = ScreenOf(app, app.Runner.BallPosition);
                        Log($"holepos {app.CurrentLevel.id} hole={hs.x:0},{hs.y:0} ball={bs.x:0},{bs.y:0} screen={Screen.width}x{Screen.height}");
                        break;
                    }
                    case "drag":
                        yield return DragShot(app, arg);
                        break;
                    case "reach":
                        yield return Reach(app, arg);
                        break;
                    case "expect":
                    {
                        // expect:shots=1,tee=1,finished=0,rolling=0 — asserts the real runner state (tee=1: ball is on the tee).
                        LevelData lv = app.CurrentLevel;
                        foreach (string kv in arg.Split(','))
                        {
                            string[] pair = kv.Split('=');
                            int want = int.Parse(pair[1], System.Globalization.CultureInfo.InvariantCulture);
                            int got = pair[0] == "shots" ? app.Runner.Shots
                                : pair[0] == "finished" ? (app.Runner.Finished ? 1 : 0)
                                : pair[0] == "rolling" ? (app.Runner.IsRolling ? 1 : 0)
                                : pair[0] == "dragging" ? (app.Runner.Dragging ? 1 : 0)
                                : pair[0] == "blocked" ? (app.Runner.DragBlockedByEdge ? 1 : 0)
                                : pair[0] == "pow" ? Mathf.RoundToInt(app.Runner.LastShotPower * 100f)
                                : pair[0] == "dragpow" ? Mathf.RoundToInt(app.Runner.DragPower * 100f)
                                : pair[0] == "tee" ? ((app.Runner.BallPosition - lv.start).sqrMagnitude < 1e-6f ? 1 : 0) : -999;
                            if (got != want) { failures++; Log($"FAIL expect {kv}: got {got}"); }
                            else Log($"expect {kv} ok");
                        }
                        break;
                    }
                    case "solve":
                    {
                        // Solve from the current ball position and play the full plan through the real runner.
                        LevelData lv = app.CurrentLevel;
                        SolveResult r = Solver.Solve(lv, 3);
                        if (!r.solvable) { failures++; Log($"FAIL {lv.id} unsolved"); break; }
                        Log($"{lv.id} plan: {r.plan.Count} shots");
                        foreach (ShotPlan sp in r.plan)
                        {
                            yield return Settle(app);
                            if (lv.HasMovers && lv.Cycle > 0f)
                            {
                                float guard = 0f;
                                while (Mathf.Repeat(app.Runner.SimTime - sp.startTime, lv.Cycle) > 0.02f && guard < lv.Cycle * 2f) { guard += Time.unscaledDeltaTime; yield return null; }
                            }
                            Vector2 dir = new Vector2(Mathf.Cos(sp.angleDeg * Mathf.Deg2Rad), Mathf.Sin(sp.angleDeg * Mathf.Deg2Rad));
                            // the runner's position can differ from the plan node by tiny amounts; aim is robust to that
                            if (lv.HasMovers) app.Runner.TestSetTime(sp.startTime);
                            app.Runner.Shoot(dir, sp.power);
                        }
                        float t = 0f;
                        while (!app.Runner.Finished && t < 40f) { t += Time.unscaledDeltaTime; yield return null; }
                        Log($"{lv.id} finished={app.Runner.Finished} shots={app.Runner.Shots} (3*={lv.threeStar} 2*={lv.twoStar})");
                        if (!app.Runner.Finished) failures++;
                        break;
                    }
                    case "ads":
                    {
                        foreach (string failure in PocketPutt.Ads.CupAdsChecks.Run())
                        { failures++; Log("FAIL ads: " + failure); }
                        yield return AdsSmoke(app);
                        break;
                    }
                    case "unit":
                    {
                        List<string> bad = UnitChecks.RunAll();
                        foreach (string b in bad) { failures++; Log("FAIL unit: " + b); }
                        Log(bad.Count == 0 ? "unit checks passed" : $"{bad.Count} unit check(s) failed");
                        break;
                    }
                    case "campaign":
                        yield return Campaign(app, arg);
                        break;
                    case "waitresult":
                    {
                        float t = 0f;
                        while (app.Mode != Mode.Result && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
                        if (app.Mode != Mode.Result) { failures++; Log("FAIL result screen never appeared"); }
                        break;
                    }
                    case "quit":
                        Log(failures == 0 ? "PASS" : $"FAILED ({failures})");
                        File.WriteAllLines(Path.Combine(outDir, "selftest.log"), log);
                        Finish(failures == 0 ? 0 : 2);
                        yield break;
                }
            }
        }


        // Exercises the real result/save/UI path, injecting only the hole-sunk event (no solver campaign).
        IEnumerator AdsSmoke(GameApp app)
        {
            var mock = app.Ads.Provider as PocketPutt.Ads.MockCupInterstitial;
            if (mock == null || string.IsNullOrEmpty(SaveStore.PathOverride))
            { failures++; Log("FAIL ads requires -pp-ads-mock 1 and isolated -pp-save"); yield break; }
            bool originalAudio = AudioListener.pause;
            bool sound = app.Progress.data.sound, music = app.Progress.data.music;
            mock.CompleteImmediately = false;
            foreach (int c in new[] { 0, 4 })
            {
                app.ResetProgress();
                var cup = Game.Campaign.Cups[c];
                // Seed only QA progress so C5's overview is genuinely unlocked.
                for (int i = 0; i < c; i++) app.Progress.data.trophies.Add(Game.Campaign.Cups[i].id);
                foreach (string id in cup.levelIds)
                    if (id != cup.FinalId) app.Progress.Record(LevelLibrary.Get(id), 1);
                app.StartLevel(cup.FinalId);
                int before = mock.Shows;
                mock.Ready = true;
                app.SendMessage("OnHoleSunk", 1);
                yield return new WaitForSecondsRealtime(1.2f);
                AdsAssert(app.Ads.Flow.Pending && mock.Shows == before && app.Mode == Mode.Result,
                    "C" + (c + 1) + " celebration before ad");
                AdsAssert(new Progress(SaveStore.Load()).HasTrophy(cup.id), "trophy saved before ad");
                if (c == 0) yield return TapUi(app, "trophy-continue"); else app.Back();
                app.ContinueCelebration(); app.Back();
                AdsAssert(mock.Shows == before + 1 && app.Ads.Flow.Busy && AudioListener.pause,
                    "Continue/Back offers once and suspends audio");
                app.SendMessage("OnApplicationPause", true);
                app.Ads.SendMessage("OnApplicationPause", true);
                yield return null;
                AdsAssert(app.Ads.Flow.Busy && AudioListener.pause, "pause preserves in-flight transition");
                app.SendMessage("OnApplicationPause", false);
                app.Ads.SendMessage("OnApplicationPause", false);
                mock.Completion(); mock.Completion();
                yield return null;
                AdsAssert(app.Mode == Mode.Cups && app.UI.Hub.Current == HubController.View.Overview, "C1/C5 returns to intended overview");
                AdsAssert(AudioListener.pause == originalAudio && sound == app.Progress.data.sound && music == app.Progress.data.music,
                    "audio restored without changing preferences");
                var reloaded = new Progress(SaveStore.Load());
                AdsAssert(!reloaded.Record(LevelLibrary.Get(cup.FinalId), 1).trophyWon, "restart/reloaded trophy has no new opportunity");
                app.StartLevel(cup.FinalId); app.SendMessage("OnHoleSunk", 1);
                AdsAssert(!app.Ads.Flow.Pending && mock.Shows == before + 1, "replay has zero new ads");
                app.Back();
            }
            app.ResetProgress();
            app.StartLevel(Game.Campaign.Cups[0].levelIds[0]); app.SendMessage("OnHoleSunk", 1);
            AdsAssert(!app.Ads.Flow.Pending, "ordinary hole has no opportunity");
            app.GoMenu();
            Log("ads focused checks and Windows mock smoke finished");
        }

        void AdsAssert(bool ok, string name)
        {
            if (!ok) { failures++; Log("FAIL ads: " + name); }
            else Log("ads PASS: " + name);
        }

        // ---- hub: real clicks, scenarios and assertions ----------------------------------------------

        /// <summary>Progress fixtures written straight into the (scratch) save so each hub state can be reached without replaying levels.</summary>
        void SeedScenario(GameApp app, string name)
        {
            Progress p = app.Progress;
            p.data.levels.Clear(); p.data.trophies.Clear(); p.data.finalsUnlocked.Clear();
            void Done(string id, int stars) { p.data.levels.Add(new LevelRecord { id = id, stars = stars, bestShots = 5 - stars + 1, plays = 1 }); }
            CupDef c1 = Game.Campaign.Cups[0];
            switch (name)
            {
                case "fresh": break;
                case "partial": Done("C1-01", 3); Done("C1-02", 2); Done("C1-03", 3); Done("C1-04", 1); break;
                case "final-stars": for (int i = 0; i < 9; i++) Done(c1.levelIds[i], 1); break;                       // 9/9 but 9 < 13 stars
                case "final-qual": for (int i = 0; i < 6; i++) Done(c1.levelIds[i], 3); break;                          // 18 >= 13 stars but 6/9
                case "final-open": for (int i = 0; i < 9; i++) Done(c1.levelIds[i], 2); p.data.finalsUnlocked.Add("C1"); break;
                case "trophy": for (int i = 0; i < 10; i++) Done(c1.levelIds[i], i == 9 ? 2 : 3); p.data.trophies.Add("C1"); p.data.finalsUnlocked.Add("C1"); break;
                case "c2": for (int i = 0; i < 10; i++) Done(c1.levelIds[i], 3); p.data.trophies.Add("C1"); p.data.finalsUnlocked.Add("C1"); Done("C2-01", 2); Done("C2-02", 3); break;
                case "replay3": Done("C1-01", 3); Done("C1-02", 3); Done("C1-03", 2); break;
                default:
                    if (name.StartsWith("mid") && int.TryParse(name.Substring(3), out int upto) && upto >= 1 && upto <= 4)
                    {
                        // cups 1..upto won (trophies, finals unlocked); the next cup has its first four levels done
                        for (int c = 0; c < upto; c++)
                        {
                            CupDef cd = Game.Campaign.Cups[c];
                            for (int i = 0; i < 10; i++) Done(cd.levelIds[i], i == 9 ? 2 : 3);
                            p.data.trophies.Add(cd.id); p.data.finalsUnlocked.Add(cd.id);
                        }
                        CupDef nx = Game.Campaign.Cups[upto];
                        Done(nx.levelIds[0], 3); Done(nx.levelIds[1], 2); Done(nx.levelIds[2], 3); Done(nx.levelIds[3], 1);
                        break;
                    }
                    failures++; Log("FAIL unknown seed " + name); return;
            }
            p.data.lastLevelId = "";
            p.SaveNow();
            p.Changed?.Invoke();
            Log($"seed {name}: next={p.NextSuggested()} totalStars={p.TotalStars()} trophies={p.data.trophies.Count}");
        }

        static IEnumerator EnsureTouchscreen()
        {
            if (Touchscreen.current != null) yield break;
            InputSystem.AddDevice<Touchscreen>();
            yield return null; yield return null;
        }

        IEnumerator Click(Vector2 pos, bool touch)
        {
            if (touch) yield return EnsureTouchscreen();
            SendPointer(touch, "begin", pos); yield return null; yield return null;
            SendPointer(touch, "end", pos); yield return null; yield return null;
        }

        IEnumerator TapNode(GameApp app, string arg)
        {
            string[] a = arg.Split(',');
            bool touch = a.Length > 1 && a[1] == "touch";
            CupMapView map = app.UI.Hub.Map;
            if (app.UI.Hub.Current != HubController.View.Map) { failures++; Log($"FAIL tapnode {a[0]}: the cup map is not showing"); yield break; }
            if (!map.NodeOnScreen(a[0])) { map.FocusForTest(a[0]); yield return null; yield return null; }
            if (!map.TryGetNodeScreenPosition(a[0], out Vector2 pos)) { failures++; Log($"FAIL tapnode {a[0]}: unknown node"); yield break; }
            Log($"tapnode {a[0]} at ({pos.x:0},{pos.y:0}) {(touch ? "touch" : "mouse")}");
            yield return Click(pos, touch);
            yield return null;
        }

        IEnumerator TapUi(GameApp app, string arg)
        {
            string[] a = arg.Split(',');
            bool touch = a.Length > 1 && a[1] == "touch";
            VisualElementProxy.Find(app, a[0], out Vector2 pos, out bool ok);
            if (!ok) { failures++; Log($"FAIL tapui {a[0]}: not found or hidden"); yield break; }
            Log($"tapui {a[0]} at ({pos.x:0},{pos.y:0}) {(touch ? "touch" : "mouse")}");
            yield return Click(pos, touch);
            yield return null;
        }

        IEnumerator TapCup(GameApp app, string arg)
        {
            int cup = int.Parse(arg);
            if (app.UI.Hub.Current != HubController.View.Overview) { failures++; Log("FAIL tapcup: destinations not showing"); yield break; }
            if (!app.UI.Hub.Overview.TryGetTileScreenPosition(cup, out Vector2 pos)) { failures++; Log("FAIL tapcup: no tile"); yield break; }
            Log($"tapcup {cup} at ({pos.x:0},{pos.y:0})");
            yield return Click(pos, false);
            yield return null;
        }

        /// <summary>tapcupat:cup,fx,fy[,touch] - taps a normalised point of a destination maquette (fx,fy in 0..1 of its picture); empty ground must not open anything.</summary>
        IEnumerator TapCupAt(GameApp app, string arg)
        {
            string[] a = arg.Split(',');
            bool touch = a.Length > 3 && a[3] == "touch";
            if (app.UI.Hub.Current != HubController.View.Overview) { failures++; Log("FAIL tapcupat: destinations not showing"); yield break; }
            if (!app.UI.Hub.Overview.TryGetArtScreenPosition(int.Parse(a[0]), new Vector2(F(a[1]), F(a[2])), out Vector2 pos)) { failures++; Log("FAIL tapcupat: no destination"); yield break; }
            Log($"tapcupat {a[0]} ({a[1]},{a[2]}) at ({pos.x:0},{pos.y:0}) {(touch ? "touch" : "mouse")}");
            yield return Click(pos, touch);
            yield return null;
        }

        /// <summary>tapplaque:cup[,touch] - taps the name/progress/action plaque of a destination.</summary>
        IEnumerator TapPlaque(GameApp app, string arg)
        {
            string[] a = arg.Split(',');
            bool touch = a.Length > 1 && a[1] == "touch";
            if (app.UI.Hub.Current != HubController.View.Overview) { failures++; Log("FAIL tapplaque: destinations not showing"); yield break; }
            if (!app.UI.Hub.Overview.TryGetPlaqueScreenPosition(int.Parse(a[0]), out Vector2 pos)) { failures++; Log("FAIL tapplaque: no plaque"); yield break; }
            Log($"tapplaque {a[0]} at ({pos.x:0},{pos.y:0}) {(touch ? "touch" : "mouse")}");
            yield return Click(pos, touch);
            yield return null;
        }

        /// <summary>swipecup:cup,dyFraction[,steps[,touch]] - a real drag that STARTS on a destination maquette: must scroll, never open the cup.</summary>
        IEnumerator SwipeCup(GameApp app, string arg)
        {
            string[] a = arg.Split(',');
            if (app.UI.Hub.Current != HubController.View.Overview) { failures++; Log("FAIL swipecup: destinations not showing"); yield break; }
            if (!app.UI.Hub.Overview.TryGetTileScreenPosition(int.Parse(a[0]), out Vector2 from)) { failures++; Log("FAIL swipecup: no destination"); yield break; }
            float dy = F(a[1]) * Screen.height;
            int steps = a.Length > 2 ? int.Parse(a[2]) : 8;
            bool touch = a.Length > 3 && a[3] == "touch";
            Vector2 to = from + new Vector2(0f, dy);
            SendPointer(touch, "begin", from); yield return null; yield return null;
            for (int i = 1; i <= steps; i++) { SendPointer(touch, "move", Vector2.Lerp(from, to, i / (float)steps)); yield return null; }
            SendPointer(touch, "end", to); yield return null; yield return null;
            Log($"swipecup {a[0]} from ({from.x:0},{from.y:0}) dy={dy:0}px");
        }

        /// <summary>swipe:dyFraction[,steps[,mouse|touch[,startXFraction,startYFraction]]] - a real one-finger drag; dy is a fraction of the screen height (negative = up).</summary>
        IEnumerator Swipe(GameApp app, string arg)
        {
            string[] a = arg.Split(',');
            float dy = F(a[0]) * Screen.height;
            int steps = a.Length > 1 ? int.Parse(a[1]) : 8;
            bool touch = a.Length > 2 && a[2] == "touch";
            Vector2 from = new Vector2(Screen.width * (a.Length > 3 ? F(a[3]) : 0.5f), Screen.height * (a.Length > 4 ? F(a[4]) : 0.5f));
            Vector2 to = from + new Vector2(0f, dy);
            SendPointer(touch, "begin", from); yield return null; yield return null;
            for (int i = 1; i <= steps; i++) { SendPointer(touch, "move", Vector2.Lerp(from, to, i / (float)steps)); yield return null; }
            SendPointer(touch, "end", to); yield return null; yield return null;
            Log($"swipe dy={dy:0}px from ({from.x:0},{from.y:0})");
        }


        /// <summary>swipenode:levelId,dyFraction[,steps[,touch]] - a real drag that STARTS on a node marker: must scroll, never select or launch.</summary>
        IEnumerator SwipeNode(GameApp app, string arg)
        {
            string[] a = arg.Split(',');
            CupMapView map = app.UI.Hub.Map;
            if (app.UI.Hub.Current != HubController.View.Map) { failures++; Log("FAIL swipenode: map not showing"); yield break; }
            if (!map.NodeOnScreen(a[0])) { map.FocusForTest(a[0]); yield return null; yield return null; }
            if (!map.TryGetNodeScreenPosition(a[0], out Vector2 from)) { failures++; Log("FAIL swipenode: unknown node"); yield break; }
            float dy = F(a[1]) * Screen.height;
            int steps = a.Length > 2 ? int.Parse(a[2]) : 8;
            bool touch = a.Length > 3 && a[3] == "touch";
            Vector2 to = from + new Vector2(0f, dy);
            SendPointer(touch, "begin", from); yield return null; yield return null;
            for (int i = 1; i <= steps; i++) { SendPointer(touch, "move", Vector2.Lerp(from, to, i / (float)steps)); yield return null; }
            SendPointer(touch, "end", to); yield return null; yield return null;
            Log($"swipenode {a[0]} from ({from.x:0},{from.y:0}) dy={dy:0}px");
        }


        /// <summary>hubmem:N - opens/closes maps and destinations N times; no map texture may survive leaving the map.</summary>
        IEnumerator HubMemory(GameApp app, int rounds)
        {
            int Count(string n) { int c = 0; foreach (Texture2D t in Resources.FindObjectsOfTypeAll<Texture2D>()) if (t.name == n) c++; return c; }
            long before = 0;
            for (int i = 0; i < rounds; i++)
            {
                app.ShowCupMap(i % 5); yield return new WaitForSecondsRealtime(0.25f);
                if (i == 1) before = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                app.ShowCupOverview(i % 5); yield return new WaitForSecondsRealtime(0.25f);
            }
            app.ShowCupOverview(); yield return new WaitForSecondsRealtime(0.4f);
            yield return Resources.UnloadUnusedAssets();
            int maps = Count("garden_map");
            foreach (string mn in DestinationArt.Names) maps += Count(mn + "_map");
            int dests = 0;
            foreach (string dn in DestinationArt.Names) dests += Count(dn);
            long after = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            int gearPatches = 0;
            foreach (Texture2D t in Resources.FindObjectsOfTypeAll<Texture2D>()) if (t.name.StartsWith("c4_gear_")) gearPatches++;
            Log($"hubmem H6 overlays running after leaving the map={CupAmbientView.Running} gear patch textures alive={gearPatches}");
            if (CupAmbientView.Running != 0 || gearPatches != 0) { failures++; Log("FAIL an H6 overlay or patch texture outlived its map"); }
            Log($"hubmem rounds={rounds} cup map textures alive after leaving the map={maps} allocated before={before / 1048576f:0.0}MB after={after / 1048576f:0.0}MB delta={(after - before) / 1048576f:0.0}MB");
            if (maps != 0) { failures++; Log("FAIL a map texture is still loaded after leaving the map"); }
            // the overview holds exactly the five maquettes while it is open and nothing once it is closed
            int open = app.UI.Hub.Current == HubController.View.Overview ? app.UI.Hub.Overview.LoadedTextureCount : -1;
            Log($"hubmem destination textures alive with the overview open={open} (loaded by the view), unity objects named like a maquette={dests}");
            app.ShowCupMap(0); yield return new WaitForSecondsRealtime(0.3f);
            yield return Resources.UnloadUnusedAssets();
            int destsClosed = 0;
            foreach (string dn in DestinationArt.Names) destsClosed += Count(dn);
            Log($"hubmem destination textures alive after leaving the overview={destsClosed}");
            if (destsClosed != 0) { failures++; Log("FAIL a destination texture is still loaded after leaving the overview"); }
        }

        /// <summary>H5: Home -> Campeonatos -> map -> level -> back to the map -> Home, N rounds; the Home hero texture must be resident only while the Home is open.</summary>
        IEnumerator HomeMemory(GameApp app, int rounds)
        {
            int Count() { int c = 0; foreach (Texture2D t in Resources.FindObjectsOfTypeAll<Texture2D>()) if (t.name == "home_hero") c++; return c; }
            long before = 0;
            int bad = 0;
            for (int i = 0; i < rounds; i++)
            {
                app.GoMenu(); yield return new WaitForSecondsRealtime(0.3f);
                if (!app.UI.HomeHeroLoaded) { bad++; Log($"FAIL round {i}: hero not loaded on the Home"); }
                if (i == 1) before = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                app.ShowCupOverview(i % 5); yield return new WaitForSecondsRealtime(0.25f);
                if (app.UI.HomeHeroLoaded) { bad++; Log($"FAIL round {i}: hero still loaded in the overview"); }
                app.ShowCupMap(i % 5); yield return new WaitForSecondsRealtime(0.25f);
                app.StartLevel(Game.Campaign.Cups[i % 5].levelIds[0]); yield return new WaitForSecondsRealtime(0.25f);
                if (app.UI.HomeHeroLoaded) { bad++; Log($"FAIL round {i}: hero still loaded while playing"); }
                app.ReturnToMap(); yield return new WaitForSecondsRealtime(0.25f);
            }
            app.ShowCupOverview(); yield return new WaitForSecondsRealtime(0.3f);
            yield return Resources.UnloadUnusedAssets();
            int away = Count();
            app.GoMenu(); yield return new WaitForSecondsRealtime(0.3f);
            long after = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            Log($"homemem rounds={rounds} hero textures alive away from the Home={away} allocated before={before / 1048576f:0.0}MB after={after / 1048576f:0.0}MB delta={(after - before) / 1048576f:0.0}MB");
            if (away != 0) { failures++; Log("FAIL the Home hero texture is still loaded away from the Home"); }
            failures += bad;
        }

        /// <summary>F0/F1: memory snapshot that separates managed heap, Unity-allocated and reserved memory (MB).</summary>
        static string MemSnap()
        {
            const float MB = 1048576f;
            return $"alloc={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / MB:0.0} reserved={UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / MB:0.0} monoUsed={UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / MB:0.0} monoHeap={UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong() / MB:0.0} gc={GC.GetTotalMemory(false) / MB:0.0}";
        }

        /// <summary>objstat:label - live gameplay objects and renderers (what is built behind the current screen).</summary>
        void ObjStat(GameApp app, string label)
        {
            int rend = 0, meshes = 0;
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)) { if (!r.enabled) continue; rend++; if (r is MeshRenderer) meshes++; }
            Log($"objstat {label} mode={app.Mode} level={(app.CurrentLevel != null ? app.CurrentLevel.id : "none")} runner={(app.Runner.Level != null ? app.Runner.Level.id : "none")} " +
                $"courseviews={FindObjectsByType<Presentation.CourseView>(FindObjectsInactive.Include).Length} ballviews={FindObjectsByType<Presentation.BallView>(FindObjectsInactive.Include).Length} aimarrows={FindObjectsByType<Presentation.AimArrow>(FindObjectsInactive.Include).Length} " +
                $"activeRenderers={rend} meshRenderers={meshes} transforms={FindObjectsByType<Transform>(FindObjectsInactive.Include).Length} {MemSnap()}");
        }

        /// <summary>
        /// perf:label,seconds - frame cost of whatever screen is open. The 60 fps cap is lifted while measuring (otherwise every frame
        /// sleeps to 16.7 ms and hides the real cost); FrameTimingManager CPU/GPU times are logged when the platform supports them.
        /// </summary>
        IEnumerator Perf(GameApp app, string arg)
        {
            string[] a = arg.Split(',');
            string label = a[0];
            float seconds = a.Length > 1 ? float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture) : 5f;
            int capBefore = Application.targetFrameRate;
            Application.targetFrameRate = -1;
            yield return null; yield return null;
            var dt = new List<float>(4096);
            var cpu = new List<double>(4096);
            var gpu = new List<double>(4096);
            var timings = new FrameTiming[16];
            long gc0 = GC.CollectionCount(0);
            using ProfilerRecorder gcFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            long gcBytes = 0, gcFrames = 0;
            using ProfilerRecorder drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            using ProfilerRecorder setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            using ProfilerRecorder tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            long mono0 = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            float t = 0f;
            while (t < seconds)
            {
                float d = Time.unscaledDeltaTime;
                dt.Add(d); t += d;
                if (gcFrame.Valid) { gcBytes += gcFrame.LastValue; gcFrames++; }
                FrameTimingManager.CaptureFrameTimings();
                uint n = FrameTimingManager.GetLatestTimings((uint)timings.Length, timings);
                for (uint i = 0; i < n; i++) { cpu.Add(timings[i].cpuFrameTime); if (timings[i].gpuFrameTime > 0) gpu.Add(timings[i].gpuFrameTime); }
                yield return null;
            }
            long gcRuns = GC.CollectionCount(0) - gc0;
            string allocPerFrame = gcFrames == 0 ? "n/a" : (gcBytes / gcFrames).ToString();
            string render = $"batches={(drawCalls.Valid ? drawCalls.LastValue : -1)} setPass={(setPass.Valid ? setPass.LastValue : -1)} tris={(tris.Valid ? tris.LastValue : -1)}";
            long monoDelta = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - mono0;
            Application.targetFrameRate = capBefore;
            dt.Sort();
            float mean = 0f; foreach (float d in dt) mean += d; mean /= Mathf.Max(1, dt.Count);
            float P(float q) => dt.Count == 0 ? 0f : dt[Mathf.Min(dt.Count - 1, (int)(q * dt.Count))] * 1000f;
            string ft = cpu.Count == 0 ? "frameTiming=n/a" : $"cpuFrame(avg)={Avg(cpu):0.00}ms gpuFrame(avg)={(gpu.Count == 0 ? "n/a" : Avg(gpu).ToString("0.00") + "ms")}";
            Log($"perf {label} mode={app.Mode} res={Screen.width}x{Screen.height} frames={dt.Count} uncapped: mean={mean * 1000f:0.00}ms ({1f / Mathf.Max(mean, 1e-6f):0} fps) p50={P(0.5f):0.00} p95={P(0.95f):0.00} p99={P(0.99f):0.00} max={P(1f):0.00}ms {ft} gcAlloc={allocPerFrame}B/frame {render} gc0runs={gcRuns} monoUsedDelta={monoDelta / 1024f:0}KB");
        }

        static double Avg(List<double> v) { double s = 0; foreach (double x in v) s += x; return s / v.Count; }

        /// <summary>
        /// memseries:rounds,series - F2: repeats the Home loop (Home -> destinations -> map -> level -> map) in `series` blocks of `rounds` cycles.
        /// After every block the unused assets are unloaded and a full GC runs, so growth that survives collection shows as a trend across blocks.
        /// </summary>
        IEnumerator MemorySeries(GameApp app, string arg)
        {
            string[] a = arg.Split(',');
            int rounds = int.Parse(a[0]), series = a.Length > 1 ? int.Parse(a[1]) : 3;
            app.GoMenu(); yield return new WaitForSecondsRealtime(0.3f);
            yield return Resources.UnloadUnusedAssets(); GC.Collect();
            yield return null;
            Log($"memseries start {MemSnap()}");
            for (int s = 0; s < series; s++)
            {
                for (int i = 0; i < rounds; i++)
                {
                    app.GoMenu(); yield return new WaitForSecondsRealtime(0.2f);
                    app.ShowCupOverview(i % 5); yield return new WaitForSecondsRealtime(0.2f);
                    app.ShowCupMap(i % 5); yield return new WaitForSecondsRealtime(0.2f);
                    app.StartLevel(Game.Campaign.Cups[i % 5].levelIds[0]); yield return new WaitForSecondsRealtime(0.2f);
                    app.ReturnToMap(); yield return new WaitForSecondsRealtime(0.2f);
                }
                app.GoMenu(); yield return new WaitForSecondsRealtime(0.3f);
                string beforeGc = MemSnap();
                yield return Resources.UnloadUnusedAssets(); GC.Collect();
                yield return null;
                Log($"memseries series={s + 1} rounds={rounds} afterLoopRaw[{beforeGc}] afterCollect[{MemSnap()}]");
            }
        }

        /// <summary>hubexpect:view=map,cup=0,sel=C1-03,panel=1,play=1,scrolled=0,mode=Cups,node.C1-02=locked,... asserts the real hub state.</summary>
        void HubExpect(GameApp app, string arg)
        {
            HubController hub = app.UI.Hub;
            foreach (string kv in arg.Split(','))
            {
                string[] pair = kv.Split('=');
                string key = pair[0], want = pair.Length > 1 ? pair[1] : "";
                string got;
                if (key == "view") got = hub.Current.ToString().ToLowerInvariant();
                else if (key == "mode") got = app.Mode.ToString().ToLowerInvariant();
                else if (key == "cup") got = hub.Map.CupIndex.ToString();
                else if (key == "sel") got = hub.Map.SelectedId ?? "none";
                else if (key == "panel") got = hub.Current == HubController.View.Map && hub.Map.PanelVisible ? "1" : "0";
                else if (key == "play") got = hub.Current == HubController.View.Map && hub.Map.PanelVisible && hub.Map.PlayButton != null ? "1" : "0";
                else if (key == "scrolled") got = (hub.Current == HubController.View.Map ? hub.Map.ScrollY : hub.Overview.ScrollY) > 1f ? "1" : "0";
                else if (key == "scrollge") got = (hub.Current == HubController.View.Map ? hub.Map.ScrollY : hub.Overview.ScrollY) >= float.Parse(want, System.Globalization.CultureInfo.InvariantCulture) ? want : "below";
                else if (key == "scrolly") { got = (hub.Current == HubController.View.Map ? hub.Map.ScrollY : hub.Overview.ScrollY).ToString("0"); Log($"hubexpect scrolly={got}"); continue; }
                else if (key == "texalive") got = hub.Overview.LoadedTextureCount.ToString();
                else if (key == "homehero") got = app.UI.HomeHeroLoaded ? "1" : "0";
                else if (key == "ambient") got = CupAmbientView.Running.ToString();
                else if (key == "gesture") got = app.UI.GestureIndicatorVisible ? "1" : "0";
                else if (key == "hand") got = app.UI.TutorialHandVisible ? "1" : "0";
                else if (key == "ambientevent") got = hub.Map.Ambient.CurrentEvent.ToLowerInvariant();
                else if (key == "placeholder") got = hub.Map.Placeholder ? "1" : "0";
                else if (key == "mystery") got = hub.Map.MysteryLocked ? "1" : "0";
                else if (key == "veil") got = hub.Map.LockedVeilVisible ? "1" : "0";
                else if (key == "lockbanner") got = hub.Map.LockBannerVisible ? "1" : "0";
                else if (key == "nodes") got = hub.Map.NodesVisible ? "1" : "0";
                else if (key == "route") got = hub.Map.RouteVisible ? "1" : "0";
                else if (key == "mapambient") got = hub.Map.AmbientVisible ? "1" : "0";
                else if (key == "maptex") got = hub.Map.MapTextureLoaded ? "1" : "0";
                else if (key == "nodecount") got = hub.Map.NodeCount.ToString();
                else if (key.StartsWith("unlocked.")) got = app.Progress.CupUnlocked(int.Parse(key.Substring(9))) ? "1" : "0";
                else if (key.StartsWith("trophy.")) got = app.Progress.HasTrophy(key.Substring(7)) ? "1" : "0";
                else if (key == "level") got = app.CurrentLevel != null ? app.CurrentLevel.id : "none";
                else if (key == "runner") got = app.Runner.Level != null ? app.Runner.Level.id : "none";
                else if (key == "courseviews") got = FindObjectsByType<Presentation.CourseView>(FindObjectsInactive.Include).Length.ToString();
                else if (key == "ballviews") got = FindObjectsByType<Presentation.BallView>(FindObjectsInactive.Include).Length.ToString();
                else if (key == "aimarrows") got = FindObjectsByType<Presentation.AimArrow>(FindObjectsInactive.Include).Length.ToString();
                else if (key.StartsWith("node.")) got = hub.Map.VisualOfNode(key.Substring(5)).ToString().ToLowerInvariant();
                else { failures++; Log("FAIL hubexpect unknown key " + key); continue; }
                if (!string.Equals(got, want, StringComparison.OrdinalIgnoreCase)) { failures++; Log($"FAIL hubexpect {key}: want {want}, got {got}"); }
                else Log($"hubexpect {kv} ok");
            }
        }

        /// <summary>
        /// Plays levels end to end through the real app using precomputed plans (-pp-plans file):
        /// progression, stars, final unlock, trophies and cup unlocks are all asserted.
        /// arg = comma separated cup ids (C1,C2...) or "all".
        /// </summary>
        IEnumerator Campaign(GameApp app, string arg)
        {
            string planFile = GameApp.CommandLineValue("-pp-plans");
            var plans = new Dictionary<string, List<ShotPlan>>();
            foreach (string line in File.ReadAllLines(planFile))
            {
                string[] tok = line.Split(' ');
                if (tok.Length < 2) continue;
                var list = new List<ShotPlan>();
                for (int i = 1; i < tok.Length; i++)
                {
                    string[] ap = tok[i].Split('@');
                    string[] a = ap[0].Split(',');
                    list.Add(new ShotPlan
                    {
                        angleDeg = float.Parse(a[0], System.Globalization.CultureInfo.InvariantCulture),
                        power = float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture),
                        startTime = ap.Length > 1 ? float.Parse(ap[1], System.Globalization.CultureInfo.InvariantCulture) : 0f
                    });
                }
                plans[tok[0]] = list;
            }

            var cupFilter = new HashSet<string>(arg == "all" || arg == "" ? new[] { "C1", "C2", "C3", "C4", "C5" } : arg.Split(','));
            foreach (CupDef cup in Game.Campaign.Cups)
            {
                if (!cupFilter.Contains(cup.id)) continue;
                for (int i = 0; i < cup.levelIds.Length; i++)
                {
                    string id = cup.levelIds[i];
                    LevelState before = app.Progress.StateOf(id);
                    if (before == LevelState.Locked) { failures++; Log($"FAIL {id} is locked when it should be playable (stars so far {app.Progress.QualifierStars(cup)}/{cup.starThreshold})"); yield break; }
                    if (!plans.TryGetValue(id, out List<ShotPlan> plan)) { failures++; Log($"FAIL no plan for {id}"); yield break; }
                    app.StartLevel(id);
                    yield return new WaitForSecondsRealtime(0.6f);
                    LevelData lv = app.CurrentLevel;
                    foreach (ShotPlan sp in plan)
                    {
                        yield return Settle(app);
                        if (lv.HasMovers && lv.Cycle > 0f)
                        {
                            float g = 0f;
                            while (Mathf.Repeat(app.Runner.SimTime - sp.startTime, lv.Cycle) > 0.02f && g < lv.Cycle * 2f) { g += Time.unscaledDeltaTime; yield return null; }
                        }
                        Vector2 dir = new Vector2(Mathf.Cos(sp.angleDeg * Mathf.Deg2Rad), Mathf.Sin(sp.angleDeg * Mathf.Deg2Rad));
                        if (lv.HasMovers) app.Runner.TestSetTime(sp.startTime);
                        app.Runner.Shoot(dir, sp.power);
                    }
                    float t = 0f;
                    while (app.Mode != Mode.Result && t < 60f) { t += Time.unscaledDeltaTime; yield return null; }
                    if (app.Mode != Mode.Result) { failures++; Log($"FAIL {id} did not finish (shots={app.Runner.Shots}, plan={plan.Count})"); yield break; }
                    int stars = app.Progress.Stars(id);
                    Log($"{id} done: shots={app.Runner.Shots} stars={stars} (3*<= {lv.threeStar}, 2*<= {lv.twoStar}) cupStars={app.Progress.QualifierStars(cup)}/{cup.starThreshold} trophy={app.Progress.HasTrophy(cup.id)}");
                    if (stars < 2) { failures++; Log($"FAIL {id} only {stars} star(s) with the planned solution"); }
                    if (i == cup.levelIds.Length - 1)
                    {
                        if (!app.Progress.HasTrophy(cup.id)) { failures++; Log($"FAIL trophy for {cup.id} not granted"); }
                        int idx = System.Array.IndexOf(Game.Campaign.Cups, cup);
                        if (idx + 1 < Game.Campaign.Cups.Length && !app.Progress.CupUnlocked(idx + 1)) { failures++; Log($"FAIL cup {idx + 2} not unlocked after the trophy"); }
                    }
                    yield return new WaitForSecondsRealtime(0.3f);
                }
            }
        }

        // ---- real pointer gestures ---------------------------------------------------------------

        static float F(string v) => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture);

        void SendPointer(bool touch, string phase, Vector2 pos)
        {
            if (touch)
            {
                Touchscreen ts = Touchscreen.current ?? InputSystem.AddDevice<Touchscreen>();
                var tp = phase == "begin" ? UnityEngine.InputSystem.TouchPhase.Began : phase == "end" ? UnityEngine.InputSystem.TouchPhase.Ended : UnityEngine.InputSystem.TouchPhase.Moved;
                InputSystem.QueueStateEvent(ts, new TouchState { touchId = 1, phase = tp, position = pos, pressure = phase == "end" ? 0f : 1f });
            }
            else
            {
                Mouse m = Mouse.current ?? InputSystem.AddDevice<Mouse>();
                var st = new MouseState { position = pos };
                if (phase != "end") st = st.WithButton(MouseButton.Left);
                InputSystem.QueueStateEvent(m, st);
            }
        }

        static Vector2 ScreenOf(GameApp app, Vector2 field)
        {
            Vector3 w = app.Rig.Cam.WorldToScreenPoint(new Vector3(field.x, 0f, field.y));
            return new Vector2(w.x, w.y);
        }

        /// <summary>
        /// drag:angleDeg,power,start[,mouse|touch[,info]] - performs press/move/release through the real input path.
        /// start = ball (press on the ball) or center (press mid-screen). The finger travels along the screen direction that maps to the
        /// requested field angle, with the pull length for the requested power. The delivered LastShotPower/Angle are asserted.
        /// An unreachable pointer (off screen) is clamped like a real device and reported; "info" logs it without failing.
        /// </summary>
        IEnumerator DragShot(GameApp app, string arg)
        {
            string[] a = arg.Split(',');
            float ang = F(a[0]), pow = F(a[1]);
            bool fromBall = a.Length > 2 && a[2] == "ball";
            bool touch = a.Length > 3 && a[3] == "touch";
            bool info = a.Length > 4 && a[4] == "info";
            float W = Screen.width, H = Screen.height;
            LevelRunner r = app.Runner;
            Vector2 ball = r.BallPosition;
            Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
            Vector2 anchor = fromBall ? ScreenOf(app, ball) : new Vector2(W * 0.5f, H * 0.5f);
            Vector2 shotScreen = ScreenOf(app, ball + dir) - ScreenOf(app, ball);   // field direction -> screen direction
            Vector2 want = GestureMath.PointerFor(anchor, shotScreen, pow, W, H);
            Vector2 pointer = new Vector2(Mathf.Clamp(want.x, 0f, W), Mathf.Clamp(want.y, 0f, H));
            bool reachable = GestureMath.Inside(want, W, H);
            float expectPow = GestureMath.Power(anchor, pointer, W, H);
            string tag = $"drag {(fromBall ? "ball" : "center")} {(touch ? "touch" : "mouse")} ang={ang:0} pow={pow:0.00} anchor=({anchor.x:0},{anchor.y:0}) pointer=({pointer.x:0},{pointer.y:0})";
            if (!reachable)
            {
                Log($"{tag} UNREACHABLE on screen (would need ({want.x:0},{want.y:0}); clamped power {expectPow:0.00})");
                if (!info) failures++;
            }

            int before = r.Shots;
            // A desktop Player has no Touchscreen: the virtual one must exist (and be current) before its first event, otherwise that event is racy.
            if (touch) yield return EnsureTouchscreen();
            SendPointer(touch, "begin", anchor); yield return null; yield return null;
            for (int i = 1; i <= 6; i++) { SendPointer(touch, "move", Vector2.Lerp(anchor, pointer, i / 6f)); yield return null; }
            yield return null; yield return null;
            float live = r.DragPower;
            SendPointer(touch, "end", pointer); yield return null; yield return null;

            if (r.Shots != before + 1)
            {
                Log($"{tag} -> no shot fired (power {expectPow:0.00})");
                Log($"  diag pointer={Pointer.current?.GetType().Name} touchPressed={Touchscreen.current?.primaryTouch.press.isPressed} dragging={r.Dragging} inputEnabled={r.InputEnabled} rolling={r.IsRolling} finished={r.Finished} dragPower={r.DragPower:0.00} devices={InputSystem.devices.Count} level={r.Level?.id}");
                if (expectPow >= GestureMath.MinPower && !info) failures++;
                yield break;
            }
            float dPow = Mathf.Abs(r.LastShotPower - expectPow);
            float dAng = Mathf.Abs(Mathf.DeltaAngle(r.LastShotAngle, ang));
            bool ok = dPow <= 0.02f && (dAng <= 1.5f || !reachable);
            Log($"{tag} -> shot power {r.LastShotPower:0.00} angle {r.LastShotAngle:0.0} (live {live:0.00}) {(ok ? "ok" : "FAIL")}");
            if (!ok && !info) failures++;
        }

        /// <summary>
        /// reach:all|current - no input; for each level reports the share of directions that reach 100% on screen when the drag starts on
        /// the ball versus mid-screen (screen margin 0), plus the ball's distance to the nearest side.
        /// </summary>
        IEnumerator Reach(GameApp app, string arg)
        {
            float W = Screen.width, H = Screen.height;
            var levels = arg == "current" ? new List<LevelData> { app.CurrentLevel } : new List<LevelData>(LevelLibrary.All());
            int worse = 0; float minBall = 1f, minCenter = 1f;
            foreach (LevelData lv in levels)
            {
                if (arg != "current") { app.StartLevel(lv.id); yield return null; yield return null; }
                Vector2 b = ScreenOf(app, app.Runner.BallPosition);
                float fb = GestureMath.ReachableFraction(b, W, H, 0f);
                float fc = GestureMath.ReachableFraction(new Vector2(W * 0.5f, H * 0.5f), W, H, 0f);
                float edge = Mathf.Min(b.x, W - b.x) / W;
                if (fb < 0.999f) worse++;
                minBall = Mathf.Min(minBall, fb); minCenter = Mathf.Min(minCenter, fc);
                Log($"reach {lv.id} {W:0}x{H:0} ball=({b.x:0},{b.y:0}) sideDist={edge:0.00}W fullPowerDirs: fromBall={fb:P0} fromCenter={fc:P0}");
            }
            Log($"reach summary: levels where a drag started ON the ball cannot reach 100% in every direction: {worse}/{levels.Count}; worst fromBall={minBall:P0}, worst fromCenter={minCenter:P0}");
            if (minCenter < 0.999f) { failures++; Log("FAIL a mid-screen drag cannot reach 100% in every direction"); }
        }

        IEnumerator Settle(GameApp app)
        {
            float t = 0f;
            while ((app.Runner.IsRolling) && !app.Runner.Finished && t < 40f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return null;
        }

        static void Finish(int code)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }
    }
}
