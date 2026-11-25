using System;
using System.Collections.Generic;
using PocketPutt.Game;
using PocketPutt.Levels;
using UnityEngine;
using UnityEngine.UIElements;

namespace PocketPutt.UI
{
    /// <summary>
    /// Internal map of one cup: prerendered diorama + route overlay + 10 level markers, scrolled vertically inside the cup only.
    /// Reads Progress for every state; selection and scroll are temporary and never persisted.
    /// </summary>
    public sealed class CupMapView : VisualElement
    {
        readonly GameApp app;
        readonly Func<Vector4> safeInsets;     // logical px: left, top, right, bottom

        readonly DragScroller scroller = new DragScroller();
        readonly VisualElement art = new VisualElement();
        readonly CupAmbientView ambient = new CupAmbientView();
        PlaceholderMapArt placeholder;
        readonly MapPathElement route = new MapPathElement();
        readonly VisualElement nodeLayer = new VisualElement();
        readonly VisualElement header = new VisualElement();
        readonly VisualElement scrim;
        readonly VisualElement lockedVeil = new VisualElement();
        readonly VisualElement lockBanner = new VisualElement();
        readonly Label lockBannerText;
        readonly Label titleLabel, progressLabel, starTotal;
        readonly SelectedLevelPanel panel = new SelectedLevelPanel();
        readonly List<MapNodeElement> nodes = new List<MapNodeElement>();

        int cupIndex;
        CupMapLayout layout;
        string selectedId;
        Texture2D texture;
        float pulseT;
        bool worldLoaded;
        Vector4 appliedInsets = new Vector4(-1, -1, -1, -1);

        Progress P => app.Progress;
        public int CupIndex => cupIndex;
        public string SelectedId => selectedId;
        public float ScrollY => scroller.ScrollY;
        public bool PanelVisible => panel.style.display == DisplayStyle.Flex;
        public bool MysteryLocked => layout != null && !P.CupUnlocked(cupIndex);
        public bool LockedVeilVisible => lockedVeil.style.display == DisplayStyle.Flex;
        public bool LockBannerVisible => lockBanner.style.display == DisplayStyle.Flex;
        public bool NodesVisible => nodeLayer.style.display == DisplayStyle.Flex;
        public bool RouteVisible => route.style.display == DisplayStyle.Flex;
        public bool AmbientVisible => ambient.style.display == DisplayStyle.Flex;
        public bool Placeholder => placeholder != null;
        public bool MapTextureLoaded => texture != null;
        public int NodeCount => nodes.Count;

        public CupMapView(GameApp app, Func<Vector4> safeInsets)
        {
            this.app = app; this.safeInsets = safeInsets;
            name = "hub-map";
            Ui.Fill(this);
            style.display = DisplayStyle.None;
            style.backgroundColor = Pal.Deep;

            Ui.Fill(scroller);
            Add(scroller);
            // Opaque neutral backdrop above the scroller. Locked worlds are never
            // loaded underneath it, including on Open and live Progress refresh.
            lockedVeil.name = "cup-locked-veil";
            Ui.Fill(lockedVeil);
            // Paths and stage platforms are also baked into the art. A translucent
            // tint still reveals them, so cover the locked viewport completely.
            lockedVeil.style.backgroundColor = Pal.H("101A1C");
            var atmosphere = new HubGradient(Pal.H("172326"), Pal.H("101A1C"));
            Ui.Fill(atmosphere);
            lockedVeil.Add(atmosphere);
            lockedVeil.pickingMode = PickingMode.Ignore;
            lockedVeil.style.display = DisplayStyle.None;
            Add(lockedVeil);
            art.pickingMode = PickingMode.Ignore;
            Ui.Fill(art);
            art.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            scroller.content.Add(art);
            scroller.content.Add(ambient);          // H6: above the picture, below route and markers
            scroller.content.Add(route);
            Ui.Fill(nodeLayer);
            nodeLayer.pickingMode = PickingMode.Ignore;
            scroller.content.Add(nodeLayer);
            scroller.RegisterCallback<GeometryChangedEvent>(e => LayoutContent());
            scroller.content.RegisterCallback<GeometryChangedEvent>(e => TryPendingFocus());
            scroller.Tapped += OnTapped;

            scrim = new HubGradient(Pal.A(Pal.Deep, 0.78f), Pal.A(Pal.Deep, 0f));
            scrim.style.position = Position.Absolute;
            scrim.style.left = 0; scrim.style.right = 0; scrim.style.top = 0; scrim.style.height = 330;
            Add(scrim);

            header.style.position = Position.Absolute;
            header.style.left = 0; header.style.right = 0; header.style.top = 0;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.pickingMode = PickingMode.Ignore;
            var back = Ui.IconButton(Icon.Back, 118, Pal.A(Pal.Cream, 0.97f), Pal.CreamDark, Pal.Ink, () => app.Back());
            back.name = "hub-back";
            header.Add(back);
            var mid = new VisualElement();
            mid.style.alignItems = Align.Center;
            mid.style.backgroundColor = Pal.A(Pal.Deep, 0.92f);
            mid.style.flexShrink = 1;
            Ui.Radius(mid, 44);
            Ui.Border(mid, 2, Pal.A(Pal.Cream, 0.18f));
            Ui.Pad(mid, 34, 10, 34, 12);
            mid.pickingMode = PickingMode.Ignore;
            titleLabel = Ui.Text("", 46, Pal.Cream);
            progressLabel = Ui.Text("", 32, Pal.A(Pal.Cream, 0.88f), false);
            mid.Add(titleLabel); mid.Add(progressLabel);
            header.Add(mid);
            var chip = new VisualElement();
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.Center;
            chip.style.backgroundColor = Pal.A(Pal.Deep, 0.88f);
            Ui.Radius(chip, 60);
            Ui.Pad(chip, 20, 10, 28, 10);
            chip.pickingMode = PickingMode.Ignore;
            var star = new IconElement(Icon.Star, 56, Pal.Gold); star.outline = Pal.GoldDark;
            chip.Add(star);
            starTotal = Ui.Text("0", 46, Pal.Cream);
            starTotal.style.marginLeft = 6;
            chip.Add(starTotal);
            header.Add(chip);
            Add(header);

            lockBanner.name = "cup-lock-banner";
            lockBanner.style.position = Position.Absolute;
            lockBanner.style.left = 40; lockBanner.style.right = 40;
            lockBanner.style.flexDirection = FlexDirection.Column;
            Ui.Center(lockBanner);
            lockBanner.pickingMode = PickingMode.Ignore;
            var medal = new VisualElement { pickingMode = PickingMode.Ignore };
            medal.style.width = 232; medal.style.height = 232;
            medal.style.flexShrink = 0;
            medal.style.marginBottom = 36;
            medal.style.backgroundColor = Pal.H("101719");
            Ui.Radius(medal, 116);
            Ui.Border(medal, 2, Pal.A(Pal.CreamDark, 0.22f));
            Ui.Center(medal);
            medal.Add(new IconElement(Icon.Lock, 176, Pal.Cream));
            lockBanner.Add(medal);
            var plaque = new VisualElement { pickingMode = PickingMode.Ignore };
            plaque.style.width = Length.Percent(100);
            plaque.style.maxWidth = 760;
            plaque.style.backgroundColor = Pal.H("191F21");
            plaque.style.alignItems = Align.Center;
            Ui.Radius(plaque, 36);
            Ui.Border(plaque, 2, Pal.A(Pal.CreamDark, 0.3f));
            Ui.Pad(plaque, 44, 36, 44, 42);
            var lockedTitle = Ui.Text(Loc.T("hub.mysteryLocked"), 48, Pal.Cream, true);
            lockedTitle.style.letterSpacing = 4;
            plaque.Add(lockedTitle);
            lockBannerText = Ui.Text("", 40, Pal.CreamDark, false, TextAnchor.MiddleCenter);
            lockBannerText.style.marginTop = 18;
            lockBannerText.style.whiteSpace = WhiteSpace.Normal;
            lockBannerText.style.width = Length.Percent(100);
            plaque.Add(lockBannerText);
            lockBanner.Add(plaque);
            lockBanner.style.display = DisplayStyle.None;
            Add(lockBanner);

            Add(panel);
            panel.PlayRequested += Play;
        }

        // ---- opening / closing ---------------------------------------------------------------

        public void Open(int cup, string selectId)
        {
            cupIndex = Mathf.Clamp(cup, 0, Campaign.Cups.Length - 1);
            layout = HubLayouts.For(cupIndex);
            CupDef def = Campaign.Cups[cupIndex];
            ClearWorld();
            selectedId = null; pendingFocus = false;
            panel.Hide();
            ApplyInsets(true);
            // Decide secrecy synchronously before this view can paint its first frame.
            Refresh();
            LayoutContent();
            style.display = DisplayStyle.Flex;

            string sel = selectId ?? HubLogic.InitialSelection(P, cupIndex);
            if (!P.CupUnlocked(cupIndex)) sel = null;
            else if (sel != null && Array.IndexOf(def.levelIds, sel) < 0) sel = HubLogic.InitialSelection(P, cupIndex);
            selectedId = null;
            panel.Hide();
            scroller.SetScroll(0f);
            // start from the selected node (or the entrance when nothing is selected); the geometry is only known after the first layout pass
            if (sel != null) Select(sel, instantFocus: false, focus: false);
            pendingFocus = true; pendingFocusId = sel;
            schedule.Execute(TryPendingFocus).ExecuteLater(60);
            TryPendingFocus();
        }

        void LoadWorld(CupDef def, ThemeColors tc)
        {
            if (worldLoaded) return;
            if (!layout.placeholder && !string.IsNullOrEmpty(layout.textureKey))
            {
                texture = Resources.Load<Texture2D>(layout.textureKey);
                if (texture != null) art.style.backgroundImage = new StyleBackground(texture);
            }
            if (texture == null)
            {
                placeholder = new PlaceholderMapArt(tc);
                placeholder.SetPath(layout.path);
                art.Add(placeholder);
                var wip = Ui.Text(Loc.T("hub.wip"), 36, Pal.Cream);
                wip.style.position = Position.Absolute;
                wip.style.left = 80; wip.style.right = 80; wip.style.top = 360;
                wip.style.backgroundColor = Pal.A(Pal.Deep, 0.55f);
                Ui.Radius(wip, 30); Ui.Pad(wip, 20, 10, 20, 10);
                placeholder.Add(wip);
            }
            for (int i = 0; i < HubLayouts.NodeCount; i++)
            {
                var n = new MapNodeElement(def.levelIds[i], i, i == HubLayouts.NodeCount - 1, cupIndex);
                nodes.Add(n);
                nodeLayer.Add(n);
            }
            ambient.Open(cupIndex, layout);
            worldLoaded = true;
            LayoutContent();
        }

        void ClearWorld()
        {
            ambient.Close();
            art.style.backgroundImage = StyleKeyword.None;
            ReleaseTexture();
            if (placeholder != null) { placeholder.RemoveFromHierarchy(); placeholder = null; }
            nodeLayer.Clear(); nodes.Clear();
            SetDebug(false);
            worldLoaded = false;
        }

        bool pendingFocus;
        string pendingFocusId;

        void TryPendingFocus()
        {
            if (!pendingFocus || layout == null) return;
            if (scroller.layout.height <= 1f || scroller.content.layout.height <= 1f) return;
            pendingFocus = false;
            if (pendingFocusId != null) EnsureVisible(pendingFocusId, true);
            else scroller.SetScroll(scroller.MaxScroll);
            Debug.Log($"[Hub] initial focus {pendingFocusId ?? "bottom"}: scroll={scroller.ScrollY:0} max={scroller.MaxScroll:0} contentH={scroller.content.layout.height:0} viewportH={scroller.layout.height:0}");
        }

        public void Close()
        {
            style.display = DisplayStyle.None;
            panel.Hide();
            pendingFocus = false;
            ClearWorld();
        }

        void ReleaseTexture()
        {
            if (texture != null) { Resources.UnloadAsset(texture); texture = null; }
        }

        // ---- layout --------------------------------------------------------------------------

        void LayoutContent()
        {
            if (layout == null) return;
            float w = scroller.layout.width;
            if (w <= 0f) return;
            float h = w * layout.aspect;
            if (!Mathf.Approximately(scroller.content.style.height.value.value, h)) scroller.content.style.height = h;
            for (int i = 0; i < nodes.Count; i++)
            {
                MapNodeElement n = nodes[i];
                float size = n.style.width.value.value;
                n.style.left = layout.nodes[i].x * w - size * 0.5f;
                n.style.top = layout.nodes[i].y * h - size * 0.5f;
            }
            ApplyInsets(false);
            scroller.SetScroll(scroller.ScrollY);
        }

        void ApplyInsets(bool force)
        {
            Vector4 s = safeInsets();
            if (float.IsNaN(s.x + s.y + s.z + s.w)) s = Vector4.zero;
            if (!force && s == appliedInsets) return;
            appliedInsets = s;
            header.style.paddingLeft = 28 + s.x; header.style.paddingRight = 28 + s.z; header.style.paddingTop = 22 + s.y;
            panel.SetSafeBottom(s.w);
            scrim.style.height = 330 + s.y;
            lockBanner.style.top = 178 + s.y;
            lockBanner.style.bottom = 60 + s.w;
        }

        /// <summary>Re-reads Progress: marker states, stars, header progress and the lock banner. Keeps scroll and selection.</summary>
        public void Refresh()
        {
            if (layout == null) return;
            CupDef def = Campaign.Cups[cupIndex];
            ThemeColors tc = ThemeColors.For(def.theme);
            bool unlocked = P.CupUnlocked(cupIndex);
            titleLabel.text = unlocked ? Loc.Pick(def.name) : Loc.T("hub.cupN", cupIndex + 1);
            progressLabel.text = unlocked ? HubLogic.ProgressLine(P, def) : "";
            progressLabel.style.display = unlocked ? DisplayStyle.Flex : DisplayStyle.None;
            style.backgroundColor = unlocked ? tc.bg : Pal.H("101A1C");
            art.style.display = unlocked ? DisplayStyle.Flex : DisplayStyle.None;
            starTotal.text = P.TotalStars().ToString();
            lockedVeil.style.display = unlocked ? DisplayStyle.None : DisplayStyle.Flex;
            lockBanner.style.display = unlocked ? DisplayStyle.None : DisplayStyle.Flex;
            // Do not let node/route detail or ambient effects disclose unreached stages.
            nodeLayer.style.display = unlocked ? DisplayStyle.Flex : DisplayStyle.None;
            route.style.display = unlocked ? DisplayStyle.Flex : DisplayStyle.None;
            if (!unlocked)
            {
                selectedId = null; panel.Hide();
                pendingFocusId = null;
                lockBannerText.text = HubLogic.CupRequirement(cupIndex, map: true);
                if (worldLoaded) ClearWorld();
                return;
            }
            LoadWorld(def, tc);

            int lit = -1;
            for (int i = 0; i < nodes.Count; i++)
            {
                MapNodeElement n = nodes[i];
                n.visual = HubLogic.VisualOf(P, n.levelId);
                n.selected = n.levelId == selectedId;
                n.Bind(tc, P.Stars(n.levelId), P.HasTrophy(def.id));
                if (n.visual != NodeVisual.Locked) lit = Mathf.Max(lit, layout.pathNodeIndex[i]);
            }
            route.Set(layout.path, lit, layout.aspect);
            if (unlocked && selectedId != null) panel.Show(P, def, cupIndex, selectedId, tc);
        }

        // ---- interaction ---------------------------------------------------------------------

        void OnTapped(Vector2 contentPos)
        {
            if (!P.CupUnlocked(cupIndex)) return; // locked content is only a teaser, never selectable
            // hit test: nearest marker centre within its (generous, >= 48 dp) touch radius; empty ground does nothing
            float w = scroller.layout.width, h = w * layout.aspect;
            MapNodeElement hit = null; float best = float.MaxValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                Vector2 c = new Vector2(layout.nodes[i].x * w, layout.nodes[i].y * h);
                float d = Vector2.Distance(c, contentPos);
                if (d <= nodes[i].HitRadius && d < best) { best = d; hit = nodes[i]; }
            }
            if (hit == null) return;
            Audio.Sfx.I?.Click();
            if (hit.levelId == selectedId && HubLogic.CanStart(P, hit.levelId)) { Play(hit.levelId); return; }
            Select(hit.levelId, instantFocus: false);
        }

        void Select(string id, bool instantFocus, bool focus = true)
        {
            selectedId = id;
            CupDef def = Campaign.Cups[cupIndex];
            foreach (MapNodeElement n in nodes) n.SetSelected(n.levelId == id);
            panel.Show(P, def, cupIndex, id, ThemeColors.For(def.theme));
            if (focus) EnsureVisible(id, instantFocus);
        }

        /// <summary>Scrolls only when the node would sit under the header or the sheet; never after every tap.</summary>
        void EnsureVisible(string id, bool instant)
        {
            int i = Array.IndexOf(Campaign.Cups[cupIndex].levelIds, id);
            if (i < 0) return;
            float w = scroller.layout.width, h = w * layout.aspect;
            float nodeY = layout.nodes[i].y * h;
            float vp = scroller.layout.height;
            float top = 150f + appliedInsets.y + 150f, bottom = vp - (SelectedLevelPanel.Height + appliedInsets.w) - 150f;
            float screenY = nodeY - scroller.ScrollY;
            if (!instant && screenY >= top && screenY <= bottom) return;
            float target = nodeY - (top + bottom) * 0.5f;
            scroller.ScrollTo(target, instant ? 0f : 0.42f);
        }

        void Play(string id)
        {
            // verify against Progress right before launching: a stale panel can never start a locked level
            if (!HubLogic.CanStart(P, id)) { Refresh(); return; }
            app.StartLevel(id);
        }

        public void Tick(float dt)
        {
            scroller.Tick(dt);
            ApplyInsets(false);
            if (!P.CupUnlocked(cupIndex)) return;
            pulseT += dt / 2.0f;
            if (pulseT > 1f) pulseT -= 1f;
            foreach (MapNodeElement n in nodes)
                if (n.visual == NodeVisual.Next) { n.pulse = pulseT; n.MarkDirtyRepaint(); }
            // H6: events may only start in the band between the header and the sheet; the overlay itself never touches input
            float vp = scroller.layout.height, sy = scroller.ScrollY;
            ambient.Tick(dt, sy, sy + vp, sy + 150f + appliedInsets.y + 120f, sy + vp - (SelectedLevelPanel.Height + appliedInsets.w) - 20f);
        }

        // ---- test hooks ----------------------------------------------------------------------

        /// <summary>Screen position (Input System space: pixels, origin bottom-left) of a node centre, for real-click tests.</summary>
        public bool TryGetNodeScreenPosition(string levelId, out Vector2 screen)
        {
            screen = default;
            int i = Array.IndexOf(Campaign.Cups[cupIndex].levelIds, levelId);
            if (i < 0 || scroller.layout.width <= 0f) return false;
            float w = scroller.layout.width, h = w * layout.aspect;
            Vector2 local = new Vector2(layout.nodes[i].x * w, layout.nodes[i].y * h - scroller.ScrollY);
            Vector2 world = scroller.LocalToWorld(local);
            VisualElement root = panel.panel.visualTree;
            float kx = Screen.width / root.layout.width, ky = Screen.height / root.layout.height;
            screen = new Vector2(world.x * kx, Screen.height - world.y * ky);
            return true;
        }

        public bool NodeOnScreen(string levelId)
        {
            int i = Array.IndexOf(Campaign.Cups[cupIndex].levelIds, levelId);
            if (i < 0) return false;
            float w = scroller.layout.width, h = w * layout.aspect;
            float y = layout.nodes[i].y * h - scroller.ScrollY;
            return y > 0f && y < scroller.layout.height;
        }

        public NodeVisual VisualOfNode(string levelId)
        {
            foreach (MapNodeElement n in nodes) if (n.levelId == levelId) return n.visual;
            return NodeVisual.Locked;
        }

        public VisualElement PlayButton => panel.PlayButton;
        public CupAmbientView Ambient => ambient;

        DebugCircles debugOverlay;

        /// <summary>QA overlay: draws every marker's real touch radius (red) next to the 48 dp minimum (white).</summary>
        public void SetDebug(bool on)
        {
            if (!on || !P.CupUnlocked(cupIndex)) { debugOverlay?.RemoveFromHierarchy(); debugOverlay = null; return; }
            if (debugOverlay == null) { debugOverlay = new DebugCircles(); Ui.Fill(debugOverlay); scroller.content.Add(debugOverlay); }
            float w = scroller.layout.width, h = w * layout.aspect;
            var list = new List<Vector3>();
            for (int i = 0; i < nodes.Count; i++) list.Add(new Vector3(layout.nodes[i].x * w, layout.nodes[i].y * h, nodes[i].HitRadius));
            debugOverlay.Set(list);
        }

        sealed class DebugCircles : VisualElement
        {
            List<Vector3> circles = new List<Vector3>();
            public DebugCircles() { pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }
            public void Set(List<Vector3> c) { circles = c; MarkDirtyRepaint(); }
            void Draw(MeshGenerationContext ctx)
            {
                var p = ctx.painter2D;
                foreach (Vector3 c in circles)
                {
                    p.lineWidth = 4; p.strokeColor = new Color(1f, 0.15f, 0.15f, 0.95f); HubDraw.Circle(p, new Vector2(c.x, c.y), c.z); p.Stroke();
                    p.lineWidth = 3; p.strokeColor = new Color(1f, 1f, 1f, 0.95f); HubDraw.Circle(p, new Vector2(c.x, c.y), 63f); p.Stroke();
                }
            }
        }
        public void SetScrollForTest(float y) => scroller.SetScroll(y);

        /// <summary>Test convenience: brings a node into the free zone without animation (the click itself is still a real pointer event).</summary>
        public void FocusForTest(string levelId)
        {
            int i = Array.IndexOf(Campaign.Cups[cupIndex].levelIds, levelId);
            if (i < 0) return;
            float h = scroller.layout.width * layout.aspect;
            float vp = scroller.layout.height;
            float top = 300f + appliedInsets.y, bottom = vp - (SelectedLevelPanel.Height + appliedInsets.w) - 150f;
            scroller.SetScroll(layout.nodes[i].y * h - (top + bottom) * 0.5f);
        }
        public float MaxScroll => scroller.MaxScroll;
        public bool Moving => scroller.IsMoving;
    }
}
