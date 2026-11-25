using System;
using PocketPutt.Audio;
using PocketPutt.Game;
using PocketPutt.Levels;
using PocketPutt.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace PocketPutt.UI
{
    /// <summary>All screens (menu, cups, HUD, result, trophies, settings), built from code with UI Toolkit.</summary>
    public sealed class GameUI
    {
        readonly GameApp app;
        readonly VisualElement root;
        VisualElement safe;
        VisualElement menu, hud, result, trophies, settingsModal, celebrate;
        HubController hub;
        Label hudShots, hudTitle, hudSub, hudHint;
        StarRow hudStars;
        VisualElement hand;
        GestureGuide guide;
        Label guideLabel;
        bool tipActive, edgeShown;
        float safeLeft, safeTop, safeRight, safeBottom;
        Vector4 safeInsets;   // logical px: left, top, right, bottom (read by the hub, whose art is full-bleed)
        Rect lastSafe;
        Label menuStars;
        HomeBackdrop menuBackdrop;
        VisualElement menuHero, menuTitle;
        Texture2D heroTex;
        bool menuCompact;

        public GameUI(GameApp app, VisualElement root)
        {
            this.app = app;
            this.root = root;
            root.style.position = Position.Absolute;
            root.style.left = 0; root.style.right = 0; root.style.top = 0; root.style.bottom = 0;
            safe = new VisualElement();
            Ui.Fill(safe);
            safe.pickingMode = PickingMode.Ignore;
            root.Add(safe);
            BuildMenu();
            hub = new HubController(app, root, () => safeInsets);
            BuildHud();
            BuildTrophies();
            HideAll();
        }

        Progress P => app.Progress;

        // ---- safe area -------------------------------------------------------------------------

        public void Tick()
        {
            UpdateGestureGuide();
            hub?.Tick(Time.unscaledDeltaTime);
            Rect s = Screen.safeArea;
            if (s == lastSafe || !(root.layout.width > 0f) || Screen.width <= 0) return;   // NaN before the first layout pass must not latch lastSafe
            lastSafe = s;
            float k = root.layout.width / Screen.width;
            safe.style.paddingLeft = s.xMin * k;
            safe.style.paddingRight = (Screen.width - s.xMax) * k;
            safe.style.paddingTop = (Screen.height - s.yMax) * k;
            safe.style.paddingBottom = s.yMin * k;
            safeInsets = new Vector4(s.xMin * k, (Screen.height - s.yMax) * k, (Screen.width - s.xMax) * k, s.yMin * k);
        }

        public void Dispose()
        {
            ClearGesturePresentation();
            hub?.Hide();
            hub?.Dispose();
            ReleaseHero();
        }

        // The first-play tutorial uses a scheduled callback on the HUD hand. Hiding the HUD
        // alone does NOT stop that callback: it can repaint the root-level guide over a cup map.
        // Always pause the demo and clear its root overlays before leaving/rebuilding gameplay.
        void StopHandDemo()
        {
            handLoop?.Pause();
            if (hand != null) hand.style.display = DisplayStyle.None;
        }

        void ClearGesturePresentation()
        {
            StopHandDemo();
            if (guide != null) guide.style.display = DisplayStyle.None;
            if (guideLabel != null) guideLabel.style.display = DisplayStyle.None;
            tipActive = false;
            edgeShown = false;
            hintToken++; // invalidate delayed hint/fade work from the previous level
        }

        /// <summary>Visibility probes used by the real Player first-visit/Back regression.</summary>
        public bool GestureIndicatorVisible => (guide != null && guide.style.display != DisplayStyle.None)
            || (guideLabel != null && guideLabel.style.display != DisplayStyle.None);
        public bool TutorialHandVisible => hand != null && hand.style.display != DisplayStyle.None;

        void HideAll()
        {
            ClearGesturePresentation();
            foreach (VisualElement v in new[] { menu, menuBackdrop, hud, result, trophies, settingsModal, celebrate })
                if (v != null) v.style.display = DisplayStyle.None;
            hub?.Hide();
            ReleaseHero();
        }

        static void Show(VisualElement v, bool fade = true)
        {
            v.style.display = DisplayStyle.Flex;
            if (fade) Ui.Fade(v, 0f, 1f, 220);
        }

        // ---- Menu ------------------------------------------------------------------------------

        void BuildMenu()
        {
            menu = Ui.Fill(new VisualElement());
            menu.pickingMode = PickingMode.Ignore;
            safe.Add(menu);

            // H5-A: opaque studio backdrop (GameApp.GoMenu builds no 3D level behind it since F1).
            // It lives directly under `root` (below the safe-area container) so it also bleeds under the notch / navigation bar.
            menuBackdrop = new HomeBackdrop { name = "menu-backdrop" };
            Ui.Fill(menuBackdrop);
            root.Insert(0, menuBackdrop);

            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.justifyContent = Justify.SpaceBetween;
            top.style.alignItems = Align.Center;
            top.style.marginTop = 28; top.style.marginLeft = 36; top.style.marginRight = 36;
            top.pickingMode = PickingMode.Ignore;
            var chip = Chip(out menuStars);
            top.Add(chip);
            var gear = Ui.IconButton(Icon.Gear, 120, Pal.Cream, Pal.CreamDark, Pal.Ink, () => ShowSettings());
            gear.name = "menu-settings";
            top.Add(gear);
            menu.Add(top);

            menuTitle = new VisualElement();
            menuTitle.style.alignItems = Align.Center;
            menuTitle.pickingMode = PickingMode.Ignore;
            menu.Add(menuTitle);

            // the hero fills whatever the logo above and the buttons below leave free (ScaleToFit: never cropped, never stretched)
            menuHero = new VisualElement { name = "menu-hero" };
            menuHero.style.flexGrow = 1; menuHero.style.flexShrink = 1; menuHero.style.minHeight = 0;
            menuHero.style.marginLeft = 14; menuHero.style.marginRight = 14;
            menuHero.pickingMode = PickingMode.Ignore;
            menuHero.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            menu.Add(menuHero);

            var bottom = new VisualElement();
            bottom.style.alignItems = Align.Center;
            bottom.style.marginBottom = 70;
            bottom.name = "menuBottom";
            bottom.pickingMode = PickingMode.Ignore;
            menu.Add(bottom);

            BuildMenuTitle();
            menu.RegisterCallback<GeometryChangedEvent>(e => OnMenuGeometry());
        }

        void BuildMenuTitle()
        {
            menuTitle.Clear();
            float k = menuCompact ? 0.7f : 1f;
            menuTitle.style.marginTop = menuCompact ? 6 : 30;
            menuTitle.Add(ShadowText("POCKET", 78 * k, Pal.Cream, Pal.A(Pal.Deep, 0.85f), 5 * k, 14 * k));
            var putt = ShadowText("PUTT", 220 * k, Pal.Gold, Pal.A(Pal.GoldDark, 1f), 9 * k, 6 * k);
            putt.style.marginTop = -26 * k;
            menuTitle.Add(putt);
            var sub = Ui.Text(Loc.T("title.sub"), 40 * k, Pal.Cream);
            sub.style.marginTop = -10 * k;
            menuTitle.Add(sub);
        }

        /// <summary>Short screens (3:4 tablets) get a smaller logo and buttons so the hero keeps a readable size; the light pool follows the hero.</summary>
        void OnMenuGeometry()
        {
            float w = menu.layout.width, h = menu.layout.height;
            if (!(w > 0f) || !(h > 0f)) return;
            bool compact = h / w < 1.55f;
            if (compact != menuCompact)
            {
                menuCompact = compact;
                BuildMenuTitle();
                if (menu.resolvedStyle.display != DisplayStyle.None) RefreshMenu();
            }
            Rect r = menuHero.worldBound;
            if (r.height > 0f && root.layout.height > 0f) menuBackdrop.SetSpot(new Vector2(0.5f, r.center.y / root.layout.height));
        }

        void EnsureHero()
        {
            if (heroTex == null) heroTex = Resources.Load<Texture2D>("Hub/Home/home_hero");
            menuHero.style.backgroundImage = heroTex != null ? new StyleBackground(heroTex) : new StyleBackground();
        }

        void ReleaseHero()
        {
            if (heroTex == null) return;
            if (menuHero != null) menuHero.style.backgroundImage = new StyleBackground();
            Resources.UnloadAsset(heroTex);
            heroTex = null;
        }

        /// <summary>Test hook: is the Home hero texture resident right now (it must be gone once the menu is left).</summary>
        public bool HomeHeroLoaded => heroTex != null;

        void RefreshMenu()
        {
            var bottom = menu.Q("menuBottom");
            bottom.Clear();
            string next = P.NextSuggested();
            bool fresh = P.TotalStars() == 0;
            LevelData nl = LevelLibrary.Get(next);
            float kb = menuCompact ? 0.78f : 1f;
            var play = Ui.Button(Loc.T(fresh ? "menu.play" : "menu.continue"), Pal.Green, Pal.GreenDark, Color.white, 820, 190 * kb, 78 * kb, () => app.StartLevel(next));
            play.Insert(0, new IconElement(Icon.Play, 96 * kb, Color.white));
            play.style.marginBottom = 18;
            bottom.Add(play);
            if (nl != null)
            {
                var l = Ui.Text($"{nl.id}  ·  {Loc.Pick(nl.title)}", 38, Pal.Cream);
                l.style.marginBottom = menuCompact ? 24 : 40;
                bottom.Add(l);
            }
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.pickingMode = PickingMode.Ignore;
            var b1 = Ui.Button(Loc.T("menu.cups"), Pal.Cream, Pal.CreamDark, Pal.Ink, 400, 130 * kb, 40, () => app.ShowCupOverview());
            var b2 = Ui.Button(Loc.T("menu.trophies"), Pal.Cream, Pal.CreamDark, Pal.Ink, 400, 130 * kb, 40, () => app.ShowTrophies());
            b1.style.marginRight = 20;
            row.Add(b1); row.Add(b2);
            bottom.Add(row);
            var hubButton = Ui.Button(Loc.T("menu.gameHub"), Pal.Cream, Pal.CreamDark, Pal.Ink,
                400, 94 * kb, 38, app.ReturnToGameHub);
            hubButton.name = "return-to-game-hub";
            hubButton.style.marginTop = 18;
            bottom.Add(hubButton);
            menuStars.text = $"{P.TotalStars()}";
        }

        VisualElement Chip(out Label label)
        {
            var chip = new VisualElement();
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.Center;
            chip.style.backgroundColor = Pal.A(Pal.H("0E1A1C"), 0.5f);
            Ui.Radius(chip, 60);
            Ui.Border(chip, 2, Pal.A(Pal.Cream, 0.16f));
            Ui.Pad(chip, 28, 14, 36, 14);
            chip.pickingMode = PickingMode.Ignore;
            var star = new IconElement(Icon.Star, 70, Pal.Gold);
            star.outline = Pal.GoldDark;
            chip.Add(star);
            label = Ui.Text("0", 56, Pal.Cream);
            label.style.marginLeft = 10;
            chip.Add(label);
            return chip;
        }

        static VisualElement ShadowText(string text, float size, Color color, Color shadow, float offset, float spacing)
        {
            var holder = new VisualElement();
            holder.pickingMode = PickingMode.Ignore;
            var back = Ui.Text(text, size, shadow);
            back.style.letterSpacing = spacing;
            back.style.position = Position.Absolute;
            back.style.left = 0; back.style.right = 0; back.style.top = offset;
            var front = Ui.Text(text, size, color);
            front.style.letterSpacing = spacing;
            holder.Add(back);
            holder.Add(front);
            return holder;
        }

        public void ShowMenu()
        {
            HideAll();
            EnsureHero();
            RefreshMenu();
            Show(menu);
            Show(menuBackdrop);
        }

        // ---- Cups / level select (world-map hub, see UI/Hub) ----------------------------------------

        public void ShowCupOverview(int focusCup)
        {
            HideAll();
            hub.ShowOverview(focusCup < 0 ? HubLogic.FocusCup(P) : focusCup);
        }

        public void ShowCupMap(int cupIndex, string selectLevelId)
        {
            HideAll();
            hub.ShowMap(cupIndex, selectLevelId);
        }

        /// <summary>Back inside the hub (map -> destinations). False when the hub is on its first screen.</summary>
        public bool HubBack() => hub.HandleBack();
        public HubController Hub => hub;

        /// <summary>Test hook: a named element anywhere under the UI root (buttons are named for the self-test clicker).</summary>
        public VisualElement FindElement(string elementName) => root.Q(elementName);

        /// <summary>Test hook: Input System screen position (pixels, origin bottom-left) of the centre of an element.</summary>
        public bool TryScreenCentre(VisualElement e, out Vector2 screen)
        {
            screen = default;
            if (e == null || root.layout.width <= 0f || e.resolvedStyle.display == DisplayStyle.None) return false;
            Vector2 c = e.worldBound.center;
            screen = new Vector2(c.x * (Screen.width / root.layout.width), Screen.height - c.y * (Screen.height / root.layout.height));
            return true;
        }

        // ---- HUD -------------------------------------------------------------------------------

        void BuildHud()
        {
            hud = Ui.Fill(new VisualElement());
            hud.pickingMode = PickingMode.Ignore;
            safe.Add(hud);

            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.justifyContent = Justify.SpaceBetween;
            bar.style.marginTop = 22; bar.style.marginLeft = 28; bar.style.marginRight = 28;
            bar.pickingMode = PickingMode.Ignore;
            var hudBack = Ui.IconButton(Icon.Back, 118, Pal.A(Pal.Cream, 0.96f), Pal.CreamDark, Pal.Ink, () => app.ReturnToMap());
            hudBack.name = "hud-back";
            bar.Add(hudBack);
            var mid = new VisualElement();
            mid.style.alignItems = Align.Center;
            mid.style.backgroundColor = Pal.A(Pal.Deep, 0.93f);
            Ui.Radius(mid, 44);
            Ui.Border(mid, 2, Pal.A(Pal.Cream, 0.16f));
            Ui.Pad(mid, 40, 12, 40, 12);
            mid.pickingMode = PickingMode.Ignore;
            hudTitle = Ui.Text("", 44, Pal.Cream);
            hudSub = Ui.Text("", 28, Pal.A(Pal.Cream, 0.8f), false);
            mid.Add(hudTitle); mid.Add(hudSub);
            bar.Add(mid);
            bar.Add(Ui.IconButton(Icon.Restart, 118, Pal.A(Pal.Cream, 0.96f), Pal.CreamDark, Pal.Ink, () => app.RestartLevel()));
            hud.Add(bar);

            var counter = new VisualElement();
            counter.style.alignSelf = Align.Center;
            counter.style.flexDirection = FlexDirection.Row;
            counter.style.alignItems = Align.Center;
            counter.style.marginTop = 16;
            counter.style.backgroundColor = Pal.A(Pal.Deep, 0.92f);
            Ui.Radius(counter, 50);
            Ui.Border(counter, 2, Pal.A(Pal.Cream, 0.16f));
            Ui.Pad(counter, 36, 8, 36, 8);
            counter.pickingMode = PickingMode.Ignore;
            var cl = Ui.Text(Loc.T("hud.shots"), 28, Pal.A(Pal.Cream, 0.8f));
            cl.style.marginRight = 14;
            hudShots = Ui.Text("0", 62, Pal.Cream);
            hudStars = new StarRow(46, 6);
            hudStars.style.marginLeft = 24;
            counter.Add(cl); counter.Add(hudShots); counter.Add(hudStars);
            hud.Add(counter);

            hudHint = Ui.Text(Loc.T("hud.hint"), 36, Pal.Cream);
            hudHint.style.position = Position.Absolute;
            hudHint.style.left = 80; hudHint.style.right = 80; hudHint.style.bottom = 96;
            hudHint.style.backgroundColor = Pal.A(Pal.Deep, 0.9f);
            Ui.Radius(hudHint, 40);
            Ui.Border(hudHint, 2, Pal.A(Pal.Cream, 0.16f));
            Ui.Pad(hudHint, 28, 14, 28, 14);
            hud.Add(hudHint);

            hand = new VisualElement();
            hand.style.position = Position.Absolute;
            hand.style.width = 120; hand.style.height = 120;
            hand.style.backgroundColor = Pal.A(Color.white, 0.55f);
            Ui.Radius(hand, 60);
            Ui.Border(hand, 6, Pal.A(Color.white, 0.9f));
            hand.pickingMode = PickingMode.Ignore;
            hand.style.display = DisplayStyle.None;
            hud.Add(hand);

            // gesture guide lives on the root (not the safe-area wrapper) so it can follow the finger over the whole screen
            guide = new GestureGuide();
            Ui.Fill(guide);
            guide.style.display = DisplayStyle.None;
            root.Add(guide);
            guideLabel = Ui.Text("", 44, Pal.Cream);
            guideLabel.style.position = Position.Absolute;
            guideLabel.style.backgroundColor = Pal.A(Pal.Deep, 0.82f);
            Ui.Radius(guideLabel, 30);
            Ui.Pad(guideLabel, 18, 4, 18, 4);
            guideLabel.pickingMode = PickingMode.Ignore;
            guideLabel.style.display = DisplayStyle.None;
            root.Add(guideLabel);
        }

        public void ShowHud(LevelData level)
        {
            HideAll();
            hudTitle.text = Loc.Pick(level.title);
            hudSub.text = $"{level.id}" + (level.isFinal ? "  ·  " + Loc.T("cup.final") : "");
            hudShots.text = "0";
            hudStars.Set(3, Pal.A(Pal.Cream, 0.25f));
            // the "drag anywhere" tip is shown once (on whichever level is played first) until the player makes a real drag
            bool tutorial = !P.data.seenGestureTip;
            bool firstVisit = P.Stars(level.id) == 0;
            tipActive = tutorial;
            edgeShown = false;
            hudHint.style.display = tutorial || (firstVisit && !string.IsNullOrEmpty(level.hint)) ? DisplayStyle.Flex : DisplayStyle.None;
            hudHint.text = tutorial ? Loc.T("hud.hint") : Loc.Pick(level.hint);
            hudHint.style.opacity = 1f;
            hudHint.style.bottom = 96;
            hintToken++;
            int token = hintToken;
            if (!tutorial)
                hudHint.schedule.Execute(() =>
                {
                    if (token == hintToken && hudHint.style.display != DisplayStyle.None)
                        Ui.Fade(hudHint, 1f, 0f, 500, () => { if (token == hintToken) hudHint.style.display = DisplayStyle.None; });
                }).ExecuteLater(5500);
            Show(hud, false);
            if (tutorial) StartHand(); else hand.style.display = DisplayStyle.None;
        }

        public void HideHud()
        {
            ClearGesturePresentation();
            hud.style.display = DisplayStyle.None;
        }

        public void UpdateShots(int shots, LevelData level)
        {
            hudShots.text = shots.ToString();
            int stars = shots <= level.threeStar ? 3 : shots <= level.twoStar ? 2 : 1;
            hudStars.Set(stars, Pal.A(Pal.Cream, 0.25f));
        }

        public void OnFirstDrag()
        {
            bool changed = !P.data.seenIntro || !P.data.seenGestureTip;
            P.data.seenIntro = true;
            P.data.seenGestureTip = true;
            if (changed) P.SaveNow();
            tipActive = false;
            StopHandDemo(); // first real drag ends the looping fake hand, not the live aim indicator
            if (hudHint.style.display != DisplayStyle.None && !edgeShown)
                Ui.Fade(hudHint, 1f, 0f, 400, () =>
                {
                    // the edge warning may have taken over the banner while it was fading out
                    if (edgeShown) hudHint.style.opacity = 1f;
                    else hudHint.style.display = DisplayStyle.None;
                });
        }

        /// <summary>Per-frame: draws the gesture guide (anchor, rubber band, 100% target, percentage) and the screen-edge warning.</summary>
        void UpdateGestureGuide()
        {
            if (guide == null || root.layout.width <= 0 || Screen.width <= 0) return;
            // A paused/hidden tutorial must never leave a root-level guide above the hub.
            if (app.Mode != Mode.Playing || hud.style.display == DisplayStyle.None)
            {
                guide.style.display = DisplayStyle.None;
                guideLabel.style.display = DisplayStyle.None;
                return;
            }
            LevelRunner r = app.Runner;
            bool demo = hand != null && hand.style.display != DisplayStyle.None && hud.style.display != DisplayStyle.None;
            bool live = app.Mode == Mode.Playing && r != null && r.Level != null && r.Dragging && hud.style.display != DisplayStyle.None;
            if (!live)
            {
                if (!demo)
                {
                    guide.style.display = DisplayStyle.None;
                    guideLabel.style.display = DisplayStyle.None;
                }
                if (edgeShown)
                {
                    edgeShown = false;
                    hudHint.text = Loc.T("hud.hint");
                    if (!tipActive) hudHint.style.display = DisplayStyle.None;
                }
                return;
            }

            float k = root.layout.width / Screen.width;
            Vector2 a = new Vector2(r.DragAnchor.x * k, (Screen.height - r.DragAnchor.y) * k);
            Vector2 f = new Vector2(r.DragPointer.x * k, (Screen.height - r.DragPointer.y) * k);
            guide.Set(a, f, r.DragFullPixels * k, r.DragPower, r.DragBlockedByEdge);
            guide.style.display = DisplayStyle.Flex;
            guide.style.opacity = 1f;

            bool show = r.DragPower >= GestureMath.MinPower;
            guideLabel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show)
            {
                int pct = Mathf.RoundToInt(r.DragPower * 100f);
                guideLabel.text = pct >= 99 ? "MAX" : pct + "%";
                guideLabel.style.color = r.DragPower >= 0.98f ? Pal.Gold : Pal.Cream;
                // sit beside the finger, flipped to whichever side has room
                float lx = f.x + (f.x > root.layout.width * 0.5f ? -170 : 60);
                guideLabel.style.left = lx;
                guideLabel.style.top = f.y - 80;
            }

            // finger pinned to the screen edge short of full power: say how to recover (one clear, contextual message)
            if (r.DragBlockedByEdge && !edgeShown)
            {
                edgeShown = true;
                hintToken++;
                hudHint.text = Loc.T("hud.edge");
                hudHint.style.display = DisplayStyle.Flex;
                hudHint.style.opacity = 1f;
            }
            else if (!r.DragBlockedByEdge && edgeShown)
            {
                edgeShown = false;
                hudHint.text = Loc.T("hud.hint");
                if (!tipActive) hudHint.style.display = DisplayStyle.None;
            }
        }

        int hintToken;
        IVisualElementScheduledItem handLoop;
        float handStartedAt;

        void StartHand()
        {
            hand.style.display = DisplayStyle.Flex;
            handStartedAt = Time.realtimeSinceStartup;
            // Reuse the same scheduled item across retries; don't accumulate idle callbacks.
            if (handLoop != null) { handLoop.Resume(); return; }
            handLoop = hand.schedule.Execute(() =>
            {
                if (app.Mode != Mode.Playing || hud.style.display == DisplayStyle.None
                    || hand.style.display == DisplayStyle.None) return;
                float k = root.layout.width / Mathf.Max(1, Screen.width);
                float t = Mathf.Repeat(Time.realtimeSinceStartup - handStartedAt, 2.6f);
                float pull = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 0.3f) / 1.2f));
                float alpha = t < 0.3f ? t / 0.3f : t > 2.0f ? Mathf.Clamp01(1f - (t - 2.0f) / 0.4f) : 1f;
                // the demo gesture starts mid-screen, not on the ball: the whole play area accepts the drag
                float px = Screen.width * 0.5f * k - 60 - safeLeft;
                float py = Screen.height * 0.5f * k - 60 + pull * 380;
                // account for the safe-area wrapper's padding
                px -= safe.resolvedStyle.paddingLeft;
                py -= safe.resolvedStyle.paddingTop;
                hand.style.left = px; hand.style.top = py;
                hand.style.opacity = alpha;
                // mirror the demo drag in the real gesture guide so the tip shows the rubber band and the 100% target
                Vector2 da = new Vector2(Screen.width * 0.5f * k, Screen.height * 0.5f * k);
                float full = app.Runner.DragFullPixels * k;
                guide.Set(da, da + new Vector2(0, pull * full), full, pull, false);
                guide.style.display = DisplayStyle.Flex;
                guide.style.opacity = alpha;
                guideLabel.style.display = DisplayStyle.None;
                float s = 1f - pull * 0.08f;
                hand.style.scale = new Scale(new Vector3(s, s, 1));
            }).Every(16);
        }

        public void SetHandVisible(bool v)
        {
            if (!v) StopHandDemo();
        }

        // ---- Result ----------------------------------------------------------------------------

        public void ShowResult(LevelData level, ResultInfo info, string nextId)
        {
            result?.RemoveFromHierarchy();
            result = Ui.Fill(new VisualElement());
            result.pickingMode = PickingMode.Ignore;
            safe.Add(result);

            var sheet = new VisualElement();
            sheet.style.position = Position.Absolute;
            sheet.style.left = 0; sheet.style.right = 0; sheet.style.bottom = 0;
            sheet.style.backgroundColor = Pal.Cream;
            sheet.style.borderTopLeftRadius = 70; sheet.style.borderTopRightRadius = 70;
            sheet.style.alignItems = Align.Center;
            Ui.Pad(sheet, 50, 50, 50, 80);
            result.Add(sheet);

            bool ace = info.shots == 1;
            sheet.Add(Ui.Text(Loc.T(ace ? "res.ace" : "res.hole"), ace ? 96 : 104, Pal.GreenDark));
            var stars = new StarRow(190, 14);
            stars.style.marginTop = 6; stars.style.marginBottom = 4;
            sheet.Add(stars);
            for (int i = 0; i < 3; i++)
            {
                bool earned = i < info.stars;
                stars[i].Set(Pal.StarOff, new Color(0, 0, 0, 0.12f));
                if (earned)
                {
                    int idx = i;
                    stars[i].schedule.Execute(() =>
                    {
                        stars[idx].Set(Pal.Gold, Pal.GoldDark);
                        Ui.Pop(stars[idx], 0, 380);
                        Sfx.I?.Star(idx);
                    }).ExecuteLater(380 + 330 * i);
                }
            }
            string shotsText = info.shots == 1 ? Loc.T("res.shot1") : Loc.T("res.shots", info.shots);
            var line = Ui.Text(shotsText + "   ·   " + Loc.T("res.best", info.bestShots), 46, Pal.Ink);
            line.style.marginTop = 6;
            sheet.Add(line);
            if (info.newBestShots || info.newBestStars)
                sheet.Add(Pill(Loc.T("res.newBest"), Pal.Green, Color.white));
            if (info.finalJustUnlocked)
                sheet.Add(Pill(Loc.T("res.finalOpen"), Pal.Gold, Pal.Ink));
            if (info.stars < 3 && !info.finalJustUnlocked && !(info.newBestShots || info.newBestStars))
            {
                var hint = Ui.Text(Loc.T("hud.par", level.threeStar), 32, Pal.Slate, false);
                hint.style.marginTop = 10;
                sheet.Add(hint);
            }

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 36;
            row.style.alignItems = Align.Center;
            row.Add(Ui.IconButton(Icon.Home, 150, Pal.Cream, Pal.CreamDark, Pal.Ink, () => app.ReturnToMap()));
            row.ElementAt(0).name = "res-map";
            row.ElementAt(0).style.borderTopWidth = row.ElementAt(0).style.borderLeftWidth = row.ElementAt(0).style.borderRightWidth = 4;
            row.ElementAt(0).style.borderTopColor = row.ElementAt(0).style.borderLeftColor = row.ElementAt(0).style.borderRightColor = Pal.CreamDark;
            var retry = Ui.Button(Loc.T("res.retry"), Pal.Cream, Pal.CreamDark, Pal.Ink, 330, 150, 46, () => app.RestartLevel());
            Ui.Border(retry, 4, Pal.CreamDark); retry.style.borderBottomWidth = 14;
            retry.style.marginLeft = retry.style.marginRight = 20;
            row.Add(retry);
            if (nextId != null)
            {
                bool toFinal = Campaign.CupOf(nextId) != null && nextId == Campaign.CupOf(nextId).FinalId;
                var nxt = Ui.Button(Loc.T(toFinal ? "res.toFinal" : "res.next"), Pal.Green, Pal.GreenDark, Color.white, 400, 150, 48, () => app.StartLevel(nextId));
                row.Add(nxt);
            }
            else
            {
                var back = Ui.Button(Loc.T("res.cupDone"), Pal.Green, Pal.GreenDark, Color.white, 400, 150, 42, () => app.ReturnToMap());
                back.name = "res-map-done";
                row.Add(back);
            }
            sheet.Add(row);
            Show(result, false);

            sheet.style.translate = new Translate(0, Length.Percent(100));
            sheet.experimental.animation.Start(0f, 1f, 420, (e, t) =>
            {
                float s = 1f - Mathf.Pow(1f - t, 3f);
                e.style.translate = new Translate(0, Length.Percent((1f - s) * 100f));
            });
        }

        static VisualElement Pill(string text, Color bg, Color fg)
        {
            var p = new VisualElement();
            p.style.backgroundColor = bg;
            Ui.Radius(p, 40);
            Ui.Pad(p, 34, 10, 34, 10);
            p.style.marginTop = 12;
            p.Add(Ui.Text(text, 38, fg));
            return p;
        }

        public void HideResult() { if (result != null) result.style.display = DisplayStyle.None; }

        // ---- Celebration (trophy) --------------------------------------------------------------

        public void HideCelebration() { if (celebrate != null) celebrate.style.display = DisplayStyle.None; }

        public void ShowCelebration(CupDef cup, int cupIdx, ResultInfo info, bool allDone)
        {
            celebrate?.RemoveFromHierarchy();
            celebrate = Ui.Fill(new VisualElement());
            celebrate.style.backgroundColor = Pal.A(Pal.Deep, 0.82f);
            celebrate.style.alignItems = Align.Center;
            celebrate.style.justifyContent = Justify.Center;
            safe.Add(celebrate);

            var rays = new RaysElement();
            Ui.Fill(rays);
            celebrate.Add(rays);

            var stack = new VisualElement();
            stack.style.alignItems = Align.Center;
            stack.pickingMode = PickingMode.Ignore;
            celebrate.Add(stack);
            var tr = new TrophyElement(cupIdx, 640);
            stack.Add(tr);
            Ui.Pop(tr, 200, 650);
            var t1 = Ui.Text(Loc.T("trophy.won", Loc.Pick(cup.trophyName)), 70, Pal.Gold);
            t1.style.marginTop = 10; t1.style.maxWidth = 900;
            stack.Add(t1);
            var stars = new StarRow(110, 10);
            stars.Set(info.stars);
            stars.style.marginTop = 12;
            stack.Add(stars);
            string sub = allDone ? Loc.T("trophy.allDone")
                : info.cupUnlocked ? Loc.T("trophy.cupUnlocked", Loc.Pick(Campaign.Cups[cupIdx + 1].name)) : "";
            if (sub.Length > 0)
            {
                var s = Ui.Text(sub, 44, Pal.Cream);
                s.style.marginTop = 16; s.style.maxWidth = 860;
                stack.Add(s);
            }
            var go = Ui.Button(Loc.T("trophy.continue"), Pal.Green, Pal.GreenDark, Color.white, 600, 160, 56, app.ContinueCelebration);
            go.name = "trophy-continue";
            go.style.marginTop = 60;
            stack.Add(go);
            Show(celebrate);
            result.style.display = DisplayStyle.None;
        }

        // ---- Trophy room -----------------------------------------------------------------------

        void BuildTrophies()
        {
            trophies = Ui.Fill(new VisualElement());
            trophies.style.backgroundColor = Pal.H("2B2118");
            safe.Add(trophies);
        }

        public void ShowTrophies()
        {
            HideAll();
            trophies.Clear();
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginTop = 26; header.style.marginLeft = 30; header.style.marginRight = 30;
            header.Add(Ui.IconButton(Icon.Back, 120, Pal.Cream, Pal.CreamDark, Pal.Ink, () => app.GoMenu()));
            var title = Ui.Text(Loc.T("trophy.title"), 62, Pal.Gold);
            title.style.flexGrow = 1;
            header.Add(title);
            header.Add(Ui.Box(120, 120));
            trophies.Add(header);
            var total = Ui.Text(Loc.T("trophy.total", P.TotalStars(), Campaign.Cups.Length * 30), 40, Pal.Cream);
            total.style.marginTop = 10;
            trophies.Add(total);

            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.Center;
            grid.style.marginTop = 36;
            for (int i = 0; i < Campaign.Cups.Length; i++)
            {
                CupDef cup = Campaign.Cups[i];
                bool won = P.HasTrophy(cup.id);
                var shelf = new VisualElement();
                shelf.style.width = 470; shelf.style.height = 520;
                shelf.style.marginLeft = shelf.style.marginRight = 20; shelf.style.marginBottom = 26;
                shelf.style.backgroundColor = Pal.H("3A2C20");
                Ui.Radius(shelf, 36);
                shelf.style.borderBottomWidth = 24; shelf.style.borderBottomColor = Pal.H("7A5632");
                shelf.style.alignItems = Align.Center; shelf.style.justifyContent = Justify.FlexEnd;
                Ui.Pad(shelf, 10, 10, 10, 18);
                var tr = new TrophyElement(i, 330);
                tr.SetSilhouette(!won);
                shelf.Add(tr);
                shelf.Add(Ui.Text(Loc.Pick(cup.trophyName), 34, won ? Pal.Gold : Pal.A(Pal.Cream, 0.5f)));
                shelf.Add(Ui.Text(won ? $"{P.QualifierStars(cup) + P.Stars(cup.FinalId)} ★" : Loc.T("trophy.locked"), 30, Pal.A(Pal.Cream, 0.7f), false));
                grid.Add(shelf);
            }
            trophies.Add(grid);
            Show(trophies);
        }

        // ---- Settings --------------------------------------------------------------------------

        public void ShowSettings()
        {
            settingsModal?.RemoveFromHierarchy();
            settingsModal = Ui.Fill(new VisualElement());
            settingsModal.style.backgroundColor = Pal.A(Color.black, 0.6f);
            settingsModal.style.alignItems = Align.Center;
            settingsModal.style.justifyContent = Justify.Center;
            safe.Add(settingsModal);

            var card = new VisualElement();
            card.style.width = 900;
            card.style.backgroundColor = Pal.Cream;
            Ui.Radius(card, 60);
            card.style.borderBottomWidth = 16; card.style.borderBottomColor = Pal.CreamDark;
            Ui.Pad(card, 50, 46, 50, 50);
            card.style.alignItems = Align.Center;
            card.Add(Ui.Text(Loc.T("set.title"), 72, Pal.Ink));
            card.Add(ToggleRow(Loc.T("set.sound"), app.Sfx.SoundOn, v => { app.Sfx.SoundOn = v; P.data.sound = v; P.SaveNow(); }));
            card.Add(ToggleRow(Loc.T("set.music"), app.Sfx.MusicOn, v => { app.Sfx.SetMusic(v); P.data.music = v; P.SaveNow(); }));
            card.Add(ToggleRow(Loc.T("set.vibration"), app.Sfx.VibrationOn, v => { app.Sfx.VibrationOn = v; P.data.vibration = v; P.SaveNow(); }));

            var lang = Ui.Button(Loc.T("set.language"), Pal.Cream, Pal.CreamDark, Pal.Ink, 700, 110, 38, () => app.RebuildUI(Loc.Portuguese ? 2 : 1));
            Ui.Border(lang, 4, Pal.CreamDark); lang.style.borderBottomWidth = 12;
            lang.style.marginTop = 26;
            card.Add(lang);

            if (app.Ads.PrivacyOptionsRequired)
            {
                var privacy = Ui.Button(Loc.T("set.privacy"), Pal.Cream, Pal.CreamDark, Pal.Ink, 700, 110, 38,
                    app.Ads.ShowPrivacyOptions);
                privacy.style.marginTop = 20;
                card.Add(privacy);
            }

            bool armed = false;
            Label resetLabel = null;
            var reset = Ui.Button(Loc.T("set.reset"), Pal.Red, Pal.H("A93A25"), Color.white, 700, 120, 40, null);
            resetLabel = (Label)reset.ElementAt(0);
            reset.style.marginTop = 30;
            reset.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!armed) { armed = true; resetLabel.text = Loc.T("set.resetConfirm"); }
                else { app.ResetProgress(); armed = false; resetLabel.text = Loc.T("set.reset"); }
            });
            card.Add(reset);
            var close = Ui.Button(Loc.T("set.close"), Pal.Green, Pal.GreenDark, Color.white, 700, 130, 46, () => settingsModal.style.display = DisplayStyle.None);
            close.style.marginTop = 24;
            card.Add(close);
            settingsModal.Add(card);
            Show(settingsModal);
        }

        static VisualElement ToggleRow(string label, bool initial, Action<bool> onChange)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.width = Length.Percent(100);
            row.style.marginTop = 30;
            var l = Ui.Text(label, 46, Pal.Ink, true, TextAnchor.MiddleLeft);
            row.Add(l);
            var sw = new VisualElement();
            sw.style.width = 190; sw.style.height = 96;
            Ui.Radius(sw, 48);
            var knob = new VisualElement();
            knob.style.position = Position.Absolute;
            knob.style.width = 78; knob.style.height = 78; knob.style.top = 9;
            Ui.Radius(knob, 39);
            knob.style.backgroundColor = Color.white;
            sw.Add(knob);
            bool on = initial;
            Action paint = () =>
            {
                sw.style.backgroundColor = on ? Pal.Green : Pal.StarOff;
                knob.style.left = on ? 102 : 10;
            };
            paint();
            Ui.Press(sw, () => { on = !on; paint(); onChange(on); });
            row.Add(sw);
            return row;
        }
    }

    public struct ThemeColors
    {
        public Color bg, accent, accentDark;

        public static ThemeColors For(Theme t)
        {
            switch (t)
            {
                case Theme.Dunes: return new ThemeColors { bg = Pal.H("8A5A2B"), accent = Pal.H("E59A3F"), accentDark = Pal.H("A8641A") };
                case Theme.Glacier: return new ThemeColors { bg = Pal.H("2D5C7F"), accent = Pal.H("4FA7DC"), accentDark = Pal.H("2579AA") };
                case Theme.Workshop: return new ThemeColors { bg = Pal.H("3B4450"), accent = Pal.H("E8A92D"), accentDark = Pal.H("A9741A") };
                case Theme.Arena: return new ThemeColors { bg = Pal.H("23402F"), accent = Pal.H("D8A524"), accentDark = Pal.H("9D7612") };
                default: return new ThemeColors { bg = Pal.H("1F5A36"), accent = Pal.H("3DA35D"), accentDark = Pal.H("2A7C44") };
            }
        }
    }

    /// <summary>
    /// On-screen guide for the slingshot drag, drawn from the press point (which can be anywhere on the play area):
    /// a dashed ring marks how far a full pull is, a gold target marks where the finger has to go for 100%,
    /// and the rubber band to the finger changes colour with the power.
    /// </summary>
    public sealed class GestureGuide : VisualElement
    {
        Vector2 anchor, finger;
        float fullPx, power;
        bool blocked;

        public GestureGuide()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Set(Vector2 anchor, Vector2 finger, float fullPx, float power, bool blocked)
        {
            this.anchor = anchor; this.finger = finger; this.fullPx = fullPx; this.power = power; this.blocked = blocked;
            MarkDirtyRepaint();
        }

        static Color PowerColor(float pw)
        {
            Color low = new Color(1f, 0.97f, 0.7f), mid = new Color(1f, 0.78f, 0.25f), high = new Color(1f, 0.38f, 0.22f);
            return pw < 0.5f ? Color.Lerp(low, mid, pw * 2f) : Color.Lerp(mid, high, (pw - 0.5f) * 2f);
        }

        static void Circle(Painter2D p, Vector2 c, float r, int seg = 40)
        {
            p.BeginPath();
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.PI * 2f * i / seg;
                Vector2 q = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (i == 0) p.MoveTo(q); else p.LineTo(q);
            }
            p.ClosePath();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            Color col = PowerColor(power);
            Vector2 pull = anchor - finger;
            bool has = pull.sqrMagnitude > 1f;

            // dashed ring: how far a full pull reaches from where the drag began
            p.strokeColor = new Color(1f, 1f, 1f, 0.38f);
            p.lineWidth = 5;
            const int segs = 48;
            for (int i = 0; i < segs; i += 2)
            {
                float a0 = Mathf.PI * 2f * i / segs, a1 = Mathf.PI * 2f * (i + 1) / segs;
                p.BeginPath();
                p.MoveTo(anchor + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * fullPx);
                p.LineTo(anchor + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * fullPx);
                p.Stroke();
            }

            // rubber band from the anchor to the finger
            if (has)
            {
                p.strokeColor = new Color(col.r, col.g, col.b, 0.9f);
                p.lineWidth = 12;
                p.BeginPath(); p.MoveTo(anchor); p.LineTo(finger); p.Stroke();
            }

            // 100% target: on the ring, along the finger's own direction, so it is the point the pull has to reach
            Vector2 target = anchor + (has ? -pull.normalized : Vector2.down) * fullPx;
            bool full = power >= 0.98f;
            p.strokeColor = full ? Pal.Gold : new Color(1f, 1f, 1f, 0.85f);
            p.fillColor = full ? new Color(Pal.Gold.r, Pal.Gold.g, Pal.Gold.b, 0.95f) : new Color(1f, 0.85f, 0.35f, 0.18f);
            p.lineWidth = 7;
            Circle(p, target, full ? 34 : 28);
            p.Fill(); p.Stroke();

            // anchor dot and finger ring
            p.fillColor = new Color(1f, 1f, 1f, 0.9f);
            Circle(p, anchor, 13, 20); p.Fill();
            p.strokeColor = blocked ? new Color(Pal.Red.r, Pal.Red.g, Pal.Red.b, 0.95f) : new Color(col.r, col.g, col.b, 0.95f);
            p.fillColor = new Color(col.r, col.g, col.b, 0.28f);
            p.lineWidth = 8;
            Circle(p, finger, 40, 28);
            p.Fill(); p.Stroke();
        }
    }

    /// <summary>Vertical alpha gradient (top colour to bottom colour) drawn as thin strips.</summary>
    public sealed class GradientBand : VisualElement
    {
        readonly Color top, bottom;
        public GradientBand(Color top, Color bottom)
        {
            this.top = top; this.bottom = bottom;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            const int n = 28;
            float w = contentRect.width, h = contentRect.height;
            for (int i = 0; i < n; i++)
            {
                p.fillColor = Color.Lerp(top, bottom, (i + 0.5f) / n);
                float y0 = h * i / n, y1 = h * (i + 1) / n + 1f;
                p.BeginPath();
                p.MoveTo(new Vector2(0, y0)); p.LineTo(new Vector2(w, y0)); p.LineTo(new Vector2(w, y1)); p.LineTo(new Vector2(0, y1));
                p.ClosePath();
                p.Fill();
            }
        }
    }

    /// <summary>Slowly rotating sunburst behind the trophy celebration.</summary>
    public sealed class RaysElement : VisualElement
    {
        float angle;
        public RaysElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            schedule.Execute(() => { angle += 0.35f; MarkDirtyRepaint(); }).Every(33);
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            Vector2 c = new Vector2(contentRect.width * 0.5f, contentRect.height * 0.38f);
            float r = Mathf.Max(contentRect.width, contentRect.height);
            int n = 14;
            p.fillColor = new Color(1f, 0.85f, 0.35f, 0.13f);
            for (int i = 0; i < n; i++)
            {
                float a0 = (angle + i * 360f / n) * Mathf.Deg2Rad;
                float a1 = a0 + Mathf.PI / n * 0.9f;
                p.BeginPath();
                p.MoveTo(c);
                p.LineTo(c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r);
                p.LineTo(c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r);
                p.ClosePath();
                p.Fill();
            }
        }
    }
}
