using System;
using PocketPutt.Audio;
using PocketPutt.Ads;
using PocketPutt.Levels;
using PocketPutt.Presentation;
using PocketPutt.Save;
using PocketPutt.Sim;
using PocketPutt.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace PocketPutt.Game
{
    public enum Mode { Boot, Menu, Cups, Playing, Result, Trophies }

    /// <summary>Entry point: owns progression, audio, camera, the level runner and the UI, and routes between screens.</summary>
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp I { get; private set; }
        public Progress Progress { get; private set; }
        public Sfx Sfx { get; private set; }
        public LevelRunner Runner { get; private set; }
        public GameUI UI { get; private set; }
        public CupAdsHost Ads { get; private set; }
        static bool freshSaveRequested;
        Mode mode = Mode.Boot;
        public Mode Mode
        {
            get => mode;
            private set
            {
                mode = value;
                // Only gameplay keeps the screen awake: aiming can take a while without touches, menus can sleep normally.
                Screen.sleepTimeout = value == Mode.Playing ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
            }
        }
        public LevelData CurrentLevel { get; private set; }
        public Fx Effects { get; private set; }
        public CameraRig Rig { get; private set; }

        const float PlayTop = 0.19f, PlayBottom = 0.115f;   // keeps the whole field (and the tee) above the hint banner

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() => freshSaveRequested = false;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            string savePath = CommandLineValue("-pp-save");
            if (!string.IsNullOrEmpty(savePath))
            {
                SaveStore.PathOverride = savePath;
                if (!freshSaveRequested && CommandLineValue("-pp-fresh") == "1")
                {
                    freshSaveRequested = true;
                    foreach (string suffix in new[] { "", ".tmp", ".bak" })
                        System.IO.File.Delete(savePath + suffix);
                }
            }
            Progress = new Progress(SaveStore.Load());
            Loc.Init(Progress.data.language);
            Sfx = Sfx.Create(transform);
            Sfx.SoundOn = Progress.data.sound;
            Sfx.VibrationOn = Progress.data.vibration;
            Sfx.SetMusic(Progress.data.music);

            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            Rig = cam.GetComponent<CameraRig>() ?? cam.gameObject.AddComponent<CameraRig>();
            Rig.Setup();

            Effects = Fx.Create(transform);
            var runnerGo = new GameObject("Level Runner");
            runnerGo.transform.SetParent(transform, false);
            Runner = runnerGo.AddComponent<LevelRunner>();
            Runner.HoleSunk += OnHoleSunk;
            Runner.ShotsChanged += n => { if (CurrentLevel != null && UI != null) UI.UpdateShots(n, CurrentLevel); };
            Runner.FirstDrag += () => UI?.OnFirstDrag();

            var uiGo = new GameObject("UI");
            uiGo.transform.SetParent(transform, false);
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = Resources.Load<PanelSettings>("PocketPanelSettings");
            if (doc.panelSettings == null) Debug.LogError("[PocketPutt] PocketPanelSettings missing. Run Pocket Putt > Build Game Assets.");
            uiDoc = doc;
            UI = new GameUI(this, doc.rootVisualElement);
            Ads = gameObject.AddComponent<CupAdsHost>();
            Ads.Initialize(ShowCupOverview);
        }

        UIDocument uiDoc;

        /// <summary>Rebuilds every screen (used after a language change) and returns to the menu.</summary>
        public void RebuildUI(int languagePreference)
        {
            Progress.data.language = languagePreference;
            Progress.SaveNow();
            Loc.Init(languagePreference);
            UI.Dispose();
            uiDoc.rootVisualElement.Clear();
            UI = new GameUI(this, uiDoc.rootVisualElement);
            GoMenu();
        }

        void Start()
        {
            SelfTest.MaybeStart(this);
            string forced = CommandLineValue("-pp-level");
            if (!string.IsNullOrEmpty(forced) && LevelLibrary.Get(forced) != null) StartLevel(forced);
            else GoMenu();
            TrophyIcons.Create(transform);
        }

        public static string CommandLineValue(string name)
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < a.Length; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        void Update()
        {
            UI?.Tick();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
        }

        void OnApplicationPause(bool paused) { if (paused) Progress?.SaveNow(); }
        void OnApplicationQuit() => Progress?.SaveNow();

        void OnDestroy()
        {
            UI?.Dispose();
            if (I == this) I = null;
        }

        public void ReturnToGameHub()
        {
            Ads.Flow.Cancel();
            Progress.SaveNow();
            GameHub.HubNavigation.ReturnToMenu();
        }

        public void Back()
        {
            if (Ads.Flow.Pending) { ContinueCelebration(); return; }
            if (Ads.Flow.Busy) return;
            switch (Mode)
            {
                case Mode.Playing: case Mode.Result: ReturnToMap(); break;
                case Mode.Cups: if (!UI.HubBack()) GoMenu(); break;   // map -> destinations -> menu
                case Mode.Trophies: GoMenu(); break;
            }
        }

        // ---- navigation ------------------------------------------------------------------------

        public void GoMenu()
        {
            Ads.Flow.Cancel();
            Mode = Mode.Menu;
            // The Home is opaque art (HomeBackdrop): no course, ball or aim arrow is built behind it. The suggested level
            // comes from Progress.NextSuggested() (GameUI.RefreshMenu), so CurrentLevel stays null until a level starts.
            Runner.End();
            CurrentLevel = null;
            Runner.InputEnabled = false;
            UI.HideHud();
            UI.HideResult();
            UI.ShowMenu();
        }

        /// <summary>Destinations screen (the five cups). focusCup &lt; 0 = the cup that holds the suggested level.</summary>
        public void ShowCupOverview(int focusCup = -1)
        {
            EnterHub();
            UI.ShowCupOverview(focusCup);
        }

        /// <summary>Internal map of one cup. selectLevelId null = the first level still to play in that cup.</summary>
        public void ShowCupMap(int cupIndex, string selectLevelId = null)
        {
            EnterHub();
            UI.ShowCupMap(Mathf.Clamp(cupIndex, 0, Campaign.Cups.Length - 1), selectLevelId);
        }

        /// <summary>Compatibility adapter for the old grid entry point: &lt; 0 = destinations, otherwise that cup's map.</summary>
        public void ShowCups(int cupIndex)
        {
            if (cupIndex < 0) ShowCupOverview(); else ShowCupMap(cupIndex);
        }

        /// <summary>Leaves gameplay/result for the map of the cup that was being played, with the sensible node selected.</summary>
        public void ReturnToMap()
        {
            LevelData lv = CurrentLevel;
            if (lv == null) { ShowCupOverview(); return; }
            int cupIdx = Array.IndexOf(Campaign.Cups, Campaign.CupOf(lv.id));
            string select = returnSelection != null && Campaign.CupOf(returnSelection) == Campaign.CupOf(lv.id) ? returnSelection : lv.id;
            ShowCupMap(Mathf.Max(0, cupIdx), select);
        }

        string returnSelection;   // node to preselect when the map reopens after a result (null = the level that was played)

        void EnterHub()
        {
            Ads.Flow.Cancel();
            Mode = Mode.Cups;
            Runner.End();
            CurrentLevel = null;
            UI.HideHud();
            UI.HideResult();
        }

        public void ShowTrophies()
        {
            Ads.Flow.Cancel();
            Mode = Mode.Trophies;
            UI.ShowTrophies();
        }

        public void StartLevel(string id)
        {
            Ads.Flow.Cancel();
            LevelData lv = LevelLibrary.Get(id);
            if (lv == null) return;
            CurrentLevel = lv;
            returnSelection = null;
            Mode = Mode.Playing;
            Runner.Begin(lv, Rig, Effects, Sfx, PlayTop, PlayBottom);
            Runner.InputEnabled = true;
            UI.HideResult();
            UI.ShowHud(lv);
            UI.UpdateShots(0, lv);
            Progress.data.lastLevelId = id;
        }

        public void RestartLevel()
        {
            Ads.Flow.Cancel();
            if (CurrentLevel == null) return;
            Mode = Mode.Playing;
            Runner.InputEnabled = true;
            Runner.ResetAttempt();
            Rig.Frame(CurrentLevel.bounds, PlayTop, PlayBottom);
            UI.HideResult();
            UI.ShowHud(CurrentLevel);
            UI.UpdateShots(0, CurrentLevel);
        }

        public void ResetProgress()
        {
            Progress.ResetAll();
            GoMenu();
        }

        public Vector2 BallScreenPosition()
        {
            Vector2 p = Runner.Level != null ? Runner.BallPosition : Vector2.zero;
            Vector3 s = Rig.Cam.WorldToScreenPoint(new Vector3(p.x, Tuning.BallRadius, p.y));
            return new Vector2(s.x, s.y);
        }

        public void ContinueCelebration()
        {
            if (!Ads.Flow.Pending) return;
            UI.HideCelebration();
            Ads.Flow.Continue(Time.realtimeSinceStartupAsDouble);
        }

        // ---- results ---------------------------------------------------------------------------

        void OnHoleSunk(int shots)
        {
            if (CurrentLevel == null) return;
            LevelData lv = CurrentLevel;
            ResultInfo info = Progress.Record(lv, shots);
            returnSelection = HubLogic.SelectionAfterPlay(Progress, lv.id, info.firstCompletion);
            Mode = Mode.Result;
            Runner.InputEnabled = false;
            Rig.Frame(lv.bounds, 0.14f, 0.46f);
            UI.SetHandVisible(false);

            CupDef cup = Campaign.CupOf(lv.id);
            int cupIdx = Array.IndexOf(Campaign.Cups, cup);
            if (info.trophyWon)
            {
                Ads.Flow.TrophyWon(cupIdx, Campaign.Cups.Length);
                Sfx.Trophy();
                Effects.Confetti(lv.hole, 120);
                bool allDone = cupIdx == Campaign.Cups.Length - 1;
                UI.ShowResult(lv, info, null);
                UI.ShowCelebration(cup, cupIdx, info, allDone);
                return;
            }
            Sfx.Fanfare();
            string next = Campaign.NextLevelId(lv.id);
            if (next != null && (LevelLibrary.Get(next) == null || Progress.StateOf(next) == LevelState.Locked)) next = null;
            if (info.finalJustUnlocked) Sfx.Unlock();
            UI.ShowResult(lv, info, next);
        }
    }
}
