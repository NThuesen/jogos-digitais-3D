using System;
using System.Collections.Generic;
using System.Linq;
using PocketPutt.Game;
using PocketPutt.Levels;
using PocketPutt.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace PocketPutt.UI
{
    /// <summary>
    /// "Campeonatos" (HUB-D6): the five cups as physical miniature destinations on one scrolling journey that climbs from the
    /// first cup (bottom) to the Masters (top), like the interior maps. Each destination is a prerendered transparent maquette;
    /// the route is drawn under the art and meets every maquette at its entrance; name, progress, state and one contextual action
    /// sit on a small plaque anchored to the base. Taps hit the silhouette of the art (not a rectangle) or the plaque.
    /// </summary>
    public sealed class CupOverviewView : VisualElement
    {
        const float ArtW = 900f, Gap = 150f, TopPad = 300f, BottomPad = 380f, PlaqueW = 380f, PlaqueLift = 120f;

        readonly GameApp app;
        readonly Func<Vector4> safeInsets;
        readonly DragScroller scroller = new DragScroller();
        readonly VisualElement header = new VisualElement();
        readonly VisualElement scrim;
        readonly VisualElement scrimSolid = new VisualElement();
        readonly VisualElement scrimFade;
        readonly Label starTotal;
        readonly List<Rect> artRects = new List<Rect>();
        readonly List<Rect> plaqueRects = new List<Rect>();
        readonly List<DestinationArt> arts = new List<DestinationArt>();
        readonly List<Texture2D> loaded = new List<Texture2D>();
        Vector4 appliedInsets = new Vector4(-1, -1, -1, -1);
        int focus;

        Progress P => app.Progress;
        public float ScrollY => scroller.ScrollY;
        public IReadOnlyList<Rect> TileRects => artRects;
        public IReadOnlyList<Rect> PlaqueRects => plaqueRects;

        public CupOverviewView(GameApp app, Func<Vector4> safeInsets)
        {
            this.app = app; this.safeInsets = safeInsets;
            name = "hub-overview";
            Ui.Fill(this);
            style.display = DisplayStyle.None;
            style.backgroundColor = AtlasBackdrop.Base;

            Ui.Fill(scroller);
            Add(scroller);
            scroller.Tapped += OnTapped;

            // header scrim: solid behind the title row (plaques and maquettes scroll under it), then a soft fade
            scrim = new VisualElement { pickingMode = PickingMode.Ignore };
            scrim.style.position = Position.Absolute;
            scrim.style.left = 0; scrim.style.right = 0; scrim.style.top = 0; scrim.style.height = 300;
            scrimSolid.style.backgroundColor = Pal.A(AtlasBackdrop.Base, 0.97f);
            scrimSolid.style.position = Position.Absolute;
            scrimSolid.style.left = 0; scrimSolid.style.right = 0; scrimSolid.style.top = 0; scrimSolid.style.height = 170;
            scrimSolid.pickingMode = PickingMode.Ignore;
            scrimFade = new HubGradient(Pal.A(AtlasBackdrop.Base, 0.97f), Pal.A(AtlasBackdrop.Base, 0f));
            scrimFade.style.position = Position.Absolute;
            scrimFade.style.left = 0; scrimFade.style.right = 0; scrimFade.style.top = 170; scrimFade.style.height = 130;
            scrim.Add(scrimSolid); scrim.Add(scrimFade);
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
            var title = Ui.Text(Loc.T("hub.title"), 60, Pal.Cream);
            title.style.flexGrow = 1;
            title.pickingMode = PickingMode.Ignore;
            header.Add(title);
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
        }

        public void Open(int focusCup)
        {
            focus = Mathf.Clamp(focusCup, 0, Campaign.Cups.Length - 1);
            style.display = DisplayStyle.Flex;
            Build();
            ApplyInsets(true);
            // bring the focused destination into view; layout is only known after the first geometry pass
            scroller.RegisterCallbackOnce<GeometryChangedEvent>(e => FocusOn(focus, true));
            schedule.Execute(() => FocusOn(focus, true)).ExecuteLater(30);
        }

        public void Close()
        {
            style.display = DisplayStyle.None;
            scroller.content.Clear();
            Unload();
        }

        void Unload()
        {
            foreach (Texture2D t in loaded) if (t != null) Resources.UnloadAsset(t);
            loaded.Clear();
        }

        void FocusOn(int cup, bool instant)
        {
            if (cup < 0 || cup >= artRects.Count) return;
            Rect r = artRects[cup];
            scroller.ScrollTo(r.center.y - scroller.layout.height * 0.44f, instant ? 0f : 0.4f);
        }

        void ApplyInsets(bool force)
        {
            Vector4 s = safeInsets();
            if (float.IsNaN(s.x + s.y + s.z + s.w)) s = Vector4.zero;
            if (!force && s == appliedInsets) return;
            appliedInsets = s;
            header.style.paddingLeft = 28 + s.x; header.style.paddingRight = 28 + s.z; header.style.paddingTop = 22 + s.y;
            scrim.style.height = 300 + s.y;
            scrimSolid.style.height = 170 + s.y;
            scrimFade.style.top = 170 + s.y;
        }

        public void Refresh()
        {
            if (style.display == DisplayStyle.None) return;
            float keep = scroller.ScrollY;
            Build();
            scroller.SetScroll(keep);
        }

        // ---- build ---------------------------------------------------------------------------

        void Build()
        {
            starTotal.text = P.TotalStars().ToString();
            scroller.content.Clear();
            Unload();
            artRects.Clear(); plaqueRects.Clear(); arts.Clear();
            int n = Campaign.Cups.Length;
            for (int i = 0; i < n; i++) { artRects.Add(default); plaqueRects.Add(default); arts.Add(DestinationArt.Load(i)); }

            // the journey climbs: the last cup is at the top of the content, the first one near the bottom
            float y = TopPad;
            for (int i = n - 1; i >= 0; i--)
            {
                float h = ArtW * (arts[i]?.aspect ?? 0.72f);
                float x = (HubLayouts.RefWidth - ArtW) * 0.5f + (i % 2 == 0 ? -46f : 46f);
                artRects[i] = new Rect(x, y, ArtW, h);
                float px = i % 2 == 0 ? Mathf.Min(x + ArtW - PlaqueW + 30f, HubLayouts.RefWidth - PlaqueW - 24f) : Mathf.Max(x - 30f, 24f);
                plaqueRects[i] = new Rect(px, y + h - PlaqueLift, PlaqueW, 0f);   // height is content-driven; only the origin matters for layout
                y += h + Gap;
            }
            float total = y - Gap + BottomPad;
            scroller.content.style.height = total;

            // H5-B: the atlas table under everything (non-interactive, scroll space, one element: no seams); light pools where the route rests
            var rests = new List<float> { TopPad * 0.5f, total - BottomPad * 0.5f };
            for (int i = 0; i + 1 < n; i++) rests.Add(artRects[i + 1].yMax + Gap * 0.5f);
            var backdrop = new AtlasBackdrop(rests, 20261023) { name = "hub-atlas" };
            backdrop.style.position = Position.Absolute;
            backdrop.style.left = 0; backdrop.style.right = 0; backdrop.style.top = 0; backdrop.style.height = total;
            scroller.content.Add(backdrop);

            // route layer sits under everything: it ends at each entrance and starts behind the previous maquette
            var link = new RouteLinks();
            Ui.Fill(link);
            scroller.content.Add(link);
            var segs = new List<RouteLinks.Seg>();
            string suggestedCup = Campaign.CupOf(P.NextSuggested())?.id;
            Vector2 Entrance(int i) => Point(i, arts[i]?.entrance ?? new Vector2(0.5f, 0.9f));
            Vector2 Exit(int i) => Point(i, arts[i]?.exit ?? new Vector2(0.5f, 0.4f));
            segs.Add(new RouteLinks.Seg { a = Entrance(0) + new Vector2(0f, BottomPad - 80f), b = Entrance(0), lit = false });
            for (int i = 0; i + 1 < n; i++) segs.Add(new RouteLinks.Seg { a = Exit(i), b = Entrance(i + 1), lit = P.HasTrophy(Campaign.Cups[i].id) });
            link.Set(segs);

            for (int i = 0; i < n; i++) BuildDestination(i, suggestedCup);
        }

        Vector2 Point(int i, Vector2 norm) => new Vector2(artRects[i].x + norm.x * artRects[i].width, artRects[i].y + norm.y * artRects[i].height);

        void BuildDestination(int i, string suggestedCup)
        {
            CupDef cup = Campaign.Cups[i];
            ThemeColors tc = ThemeColors.For(cup.theme);
            bool unlocked = P.CupUnlocked(i);
            bool won = P.HasTrophy(cup.id);
            bool current = cup.id == suggestedCup && unlocked && !won;
            Rect r = artRects[i];

            if (current)
            {
                var glow = new SoftGlow(Pal.Gold);
                glow.name = "glow-" + cup.id;
                glow.style.position = Position.Absolute;
                glow.style.left = r.x - 70; glow.style.top = r.y + r.height * 0.18f; glow.style.width = r.width + 140; glow.style.height = r.height * 0.9f;
                scroller.content.Add(glow);
            }

            // the maquette: transparent PNG, drawn at its natural aspect (no crop, no frame)
            Texture2D tex = unlocked && arts[i] != null ? Resources.Load<Texture2D>(arts[i].textureKey) : null;
            VisualElement art;
            if (tex != null)
            {
                loaded.Add(tex);
                art = new VisualElement();
                art.style.backgroundImage = new StyleBackground(tex);
                art.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            else art = unlocked ? (VisualElement)new VignetteArt(cup.theme, tc) : new MysteryIsland();
            art.name = "dest-" + cup.id;
            art.style.position = Position.Absolute;
            art.style.left = r.x; art.style.top = r.y; art.style.width = r.width; art.style.height = r.height;
            art.pickingMode = PickingMode.Ignore;
            // A locked destination uses only generic opaque geometry: no themed
            // texture, vignette, landmark or biome can leak through its silhouette.
            scroller.content.Add(art);

            if (!unlocked)
            {
                // A large padlock floats over the maquette. Keep silhouette hit-testing
                // independent of this purely decorative element.
                var seal = new VisualElement { name = "cup-lock-" + cup.id, pickingMode = PickingMode.Ignore };
                seal.style.position = Position.Absolute;
                seal.style.left = r.x + r.width * 0.5f - 76f;
                seal.style.top = r.y + r.height * 0.45f - 76f;
                seal.style.width = 152f; seal.style.height = 152f;
                seal.style.backgroundColor = Pal.A(Pal.Deep, 0.94f);
                Ui.Radius(seal, 76f);
                Ui.Border(seal, 2f, Pal.A(Pal.CreamDark, 0.28f));
                Ui.Center(seal);
                var lockIcon = new IconElement(Icon.Lock, 90f, Pal.Cream) { pickingMode = PickingMode.Ignore };
                seal.Add(lockIcon);
                scroller.content.Add(seal);
            }

            scroller.content.Add(BuildPlaque(i, cup, unlocked, won, current));
        }

        VisualElement BuildPlaque(int i, CupDef cup, bool unlocked, bool won, bool current)
        {
            Rect pr = plaqueRects[i];
            var plaque = new VisualElement { name = "plaque-" + cup.id };
            plaque.style.position = Position.Absolute;
            plaque.style.left = pr.x; plaque.style.top = pr.y; plaque.style.width = PlaqueW;
            plaque.pickingMode = PickingMode.Ignore;
            plaque.style.backgroundColor = Pal.A(Pal.Deep, unlocked ? 0.93f : 0.88f);
            Ui.Radius(plaque, 34);
            Ui.Border(plaque, current ? 6 : 3, current ? Pal.Gold : Pal.A(Pal.Cream, unlocked ? 0.5f : 0.25f));
            Ui.Pad(plaque, 26, 18, 22, 20);

            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.alignItems = Align.Center;
            top.style.justifyContent = Justify.SpaceBetween;
            top.pickingMode = PickingMode.Ignore;
            var tag = Ui.Text(Loc.T("hub.cupN", i + 1), 28, unlocked ? Pal.Gold : Pal.A(Pal.Cream, 0.7f), true, TextAnchor.MiddleLeft);
            tag.style.letterSpacing = 3;
            top.Add(tag);
            var seal = new VisualElement();
            seal.style.width = 66; seal.style.height = 66;
            Ui.Center(seal);
            seal.pickingMode = PickingMode.Ignore;
            if (!unlocked) seal.Add(new IconElement(Icon.Lock, 52, Pal.Cream));
            else
            {
                var tr = new TrophyElement(i, 62);
                tr.SetSilhouette(!won);
                seal.Add(tr);
            }
            top.Add(seal);
            plaque.Add(top);

            var nm = Ui.Text(unlocked ? Loc.Pick(cup.name) : "???", 42, Pal.Cream, true, TextAnchor.MiddleLeft);
            nm.style.marginTop = -6;
            plaque.Add(nm);
            if (unlocked)
            {
                var tx = Ui.Text(HubLogic.ProgressLine(P, cup), 31, Pal.A(Pal.Cream, 0.92f), false, TextAnchor.MiddleLeft);
                tx.style.marginTop = 2;
                plaque.Add(tx);
                string label = Loc.T(won ? "hub.review" : current ? "menu.continue" : "hub.open");
                var cta = Ui.Button(label, current ? Pal.Green : Pal.Cream, current ? Pal.GreenDark : Pal.CreamDark, current ? Color.white : Pal.Ink, PlaqueW - 60f, 78, 34, null);
                cta.name = "cta-" + cup.id;
                cta.pickingMode = PickingMode.Ignore;
                foreach (Label cl in cta.Children().OfType<Label>()) cl.style.whiteSpace = WhiteSpace.NoWrap;
                cta.style.marginTop = 12;
                plaque.Add(cta);
            }
            else
            {
                var state = Ui.Text(Loc.T("hub.mysteryLocked"), 26, Pal.CreamDark, true, TextAnchor.MiddleLeft);
                state.style.letterSpacing = 2;
                plaque.Add(state);
                var why = Ui.Text(HubLogic.CupRequirement(i), 28, Pal.A(Pal.Cream, 0.88f), false, TextAnchor.MiddleLeft);
                why.style.marginTop = 6;
                plaque.Add(why);
            }
            // measure once laid out so the tap area follows the plaque (content-space rect)
            plaque.RegisterCallback<GeometryChangedEvent>(e =>
            {
                if (i < plaqueRects.Count) plaqueRects[i] = new Rect(pr.x, pr.y, PlaqueW, e.newRect.height);
            });
            return plaque;
        }

        // ---- input ---------------------------------------------------------------------------

        /// <summary>Cup under a content-space point: the plaque (with its action) or the silhouette of a maquette; -1 on empty ground.</summary>
        public int HitCup(Vector2 contentPos)
        {
            for (int i = 0; i < plaqueRects.Count; i++)
                if (plaqueRects[i].height > 0f && plaqueRects[i].Contains(contentPos)) return i;
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < artRects.Count; i++)
            {
                Rect r = artRects[i];
                if (!r.Contains(contentPos)) continue;
                DestinationArt a = arts[i];
                var norm = new Vector2((contentPos.x - r.x) / r.width, (contentPos.y - r.y) / r.height);
                bool hit = !P.CupUnlocked(i) ? MysteryIsland.Hit(norm) : a == null || a.bands.Length == 0 || a.Hit(norm, 0.025f);
                if (!hit) continue;
                float d = (r.center - contentPos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        void OnTapped(Vector2 contentPos)
        {
            int i = HitCup(contentPos);
            if (i < 0) return;
            Audio.Sfx.I?.Click();
            app.ShowCupMap(i);
        }

        public void Tick(float dt)
        {
            scroller.Tick(dt);
            ApplyInsets(false);
        }

        // ---- test hooks ----------------------------------------------------------------------

        /// <summary>Screen position of a maquette's hero landmark, for real pointer tests.</summary>
        public bool TryGetTileScreenPosition(int cup, out Vector2 screen) => TryGetArtScreenPosition(cup, arts.Count > cup && arts[cup] != null ? arts[cup].hero : new Vector2(0.5f, 0.5f), out screen);

        /// <summary>Screen position of a normalised point of a maquette.</summary>
        public bool TryGetArtScreenPosition(int cup, Vector2 norm, out Vector2 screen)
        {
            screen = default;
            if (cup < 0 || cup >= artRects.Count || scroller.layout.width <= 0f) return false;
            Rect r = artRects[cup];
            return ToScreen(new Vector2(r.x + norm.x * r.width, r.y + norm.y * r.height), out screen);
        }

        public bool TryGetPlaqueScreenPosition(int cup, out Vector2 screen)
        {
            screen = default;
            if (cup < 0 || cup >= plaqueRects.Count || plaqueRects[cup].height <= 0f || scroller.layout.width <= 0f) return false;
            return ToScreen(plaqueRects[cup].center, out screen);
        }

        bool ToScreen(Vector2 contentPos, out Vector2 screen)
        {
            Vector2 world = scroller.LocalToWorld(new Vector2(contentPos.x, contentPos.y - scroller.ScrollY));
            VisualElement root = scroller.panel.visualTree;
            screen = new Vector2(world.x * (Screen.width / root.layout.width), Screen.height - world.y * (Screen.height / root.layout.height));
            return true;
        }

        public DestinationArt ArtOf(int cup) => cup >= 0 && cup < arts.Count ? arts[cup] : null;
        public int LoadedTextureCount => loaded.Count;
        public void SetScrollForTest(float y) => scroller.SetScroll(y);
        public float MaxScroll => scroller.MaxScroll;
    }

    /// <summary>Solid, anonymous island volume. The same contour is used for drawing and taps.</summary>
    sealed class MysteryIsland : VisualElement
    {
        static readonly Vector2[] Outline =
        {
            new Vector2(0.05f, 0.48f), new Vector2(0.20f, 0.30f), new Vector2(0.43f, 0.22f),
            new Vector2(0.69f, 0.27f), new Vector2(0.93f, 0.48f), new Vector2(0.90f, 0.64f),
            new Vector2(0.77f, 0.80f), new Vector2(0.53f, 0.90f), new Vector2(0.23f, 0.85f), new Vector2(0.08f, 0.69f)
        };
        static readonly Color Side = Pal.H("101719"), Face = Pal.H("191F21"), Rim = Pal.H("2B3334");

        public MysteryIsland()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public static bool Hit(Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = Outline.Length - 1; i < Outline.Length; j = i++)
            {
                Vector2 a = Outline[i], b = Outline[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float w = contentRect.width, h = contentRect.height;
            Vector2 At(float x, float y) => new Vector2(x * w, y * h);
            p.fillColor = Pal.A(Color.black, 0.15f);
            HubDraw.Ellipse(p, At(0.49f, 0.88f), w * 0.43f, h * 0.065f); p.Fill();
            p.fillColor = Side;
            p.BeginPath(); p.MoveTo(Vector2.Scale(Outline[0], new Vector2(w, h)));
            for (int i = 1; i < Outline.Length; i++) p.LineTo(Vector2.Scale(Outline[i], new Vector2(w, h)));
            p.ClosePath(); p.Fill();
            p.fillColor = Face;
            p.BeginPath(); p.MoveTo(At(0.05f, 0.48f));
            p.LineTo(At(0.20f, 0.30f)); p.LineTo(At(0.43f, 0.22f)); p.LineTo(At(0.69f, 0.27f));
            p.LineTo(At(0.93f, 0.48f)); p.LineTo(At(0.77f, 0.64f));
            p.LineTo(At(0.53f, 0.73f)); p.LineTo(At(0.23f, 0.68f)); p.ClosePath(); p.Fill();
            p.strokeColor = Rim; p.lineWidth = 2; p.lineJoin = LineJoin.Round;
            p.BeginPath(); p.MoveTo(At(0.05f, 0.48f)); p.LineTo(At(0.20f, 0.30f));
            p.LineTo(At(0.43f, 0.22f)); p.LineTo(At(0.69f, 0.27f)); p.Stroke();
        }
    }

    /// <summary>Soft warm light under the current destination (concentric low-alpha ellipses; no animation, no particles).</summary>
    sealed class SoftGlow : VisualElement
    {
        readonly Color tint;
        public SoftGlow(Color tint)
        {
            this.tint = tint;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float w = contentRect.width, h = contentRect.height;
            for (int k = 0; k < 9; k++)
            {
                float s = 1f - k * 0.075f;
                p.fillColor = Pal.A(tint, 0.045f);
                HubDraw.Ellipse(p, new Vector2(w * 0.5f, h * 0.5f), w * 0.5f * s, h * 0.5f * s, 48);
                p.Fill();
            }
        }
    }

    /// <summary>The route between maquettes: a worn track under the art. It leaves from behind one maquette and ends at the next entrance; walked links are gold.</summary>
    sealed class RouteLinks : VisualElement
    {
        public struct Seg { public Vector2 a, b; public bool lit; }
        List<Seg> segs = new List<Seg>();

        public RouteLinks() { pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }

        public void Set(List<Seg> s) { segs = s; MarkDirtyRepaint(); }

        static Vector2 Bez(Vector2 a, Vector2 c1, Vector2 c2, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * b;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            foreach (Seg s in segs)
            {
                // leaves upward from the lower maquette and arrives vertically at the upper entrance
                float dy = Mathf.Clamp(Mathf.Abs(s.a.y - s.b.y) * 0.45f, 60f, 260f);
                Vector2 c1 = s.a + new Vector2(0f, -dy), c2 = s.b + new Vector2(0f, dy);
                const int n = 40;
                var pts = new Vector2[n + 1];
                for (int k = 0; k <= n; k++) pts[k] = Bez(s.a, c1, c2, s.b, k / (float)n);
                void Pass(float width, Color col)
                {
                    p.strokeColor = col; p.lineWidth = width;
                    p.BeginPath(); p.MoveTo(pts[0]);
                    for (int k = 1; k <= n; k++) p.LineTo(pts[k]);
                    p.Stroke();
                }
                Pass(46f, new Color(0.02f, 0.06f, 0.08f, 0.5f));                                   // ground shadow (tinted like the atlas table)
                Pass(36f, s.lit ? Pal.H("E3B33F") : Pal.H("BFAE84"));                                // worn edge
                Pass(26f, s.lit ? Pal.H("F6D56E") : Pal.H("DCCCA2"));                                // track surface
                // stitching dashes down the middle
                p.strokeColor = s.lit ? Pal.A(Pal.H("FFF1B8"), 0.9f) : Pal.A(Pal.H("F3E9CB"), 0.6f); p.lineWidth = 5f;
                for (int k = 0; k + 1 <= n; k += 2) { p.BeginPath(); p.MoveTo(pts[k]); p.LineTo(pts[Mathf.Min(n, k + 1)]); p.Stroke(); }
            }
        }
    }

    /// <summary>Fallback vignette used only when a destination texture is missing from the build. Simple silhouettes on a theme gradient.</summary>
    sealed class VignetteArt : VisualElement
    {
        readonly Theme theme;
        readonly ThemeColors tc;

        public VignetteArt(Theme theme, ThemeColors tc)
        {
            this.theme = theme; this.tc = tc;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        static void Poly(Painter2D p, Color fill, params Vector2[] pts)
        {
            p.fillColor = fill; p.BeginPath(); p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.ClosePath(); p.Fill();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float w = contentRect.width, h = contentRect.height;
            Color skyTop, skyBottom, ground;
            switch (theme)
            {
                case Theme.Dunes: skyTop = Pal.H("F6C98B"); skyBottom = Pal.H("F9E3B8"); ground = Pal.H("E2B06A"); break;
                case Theme.Glacier: skyTop = Pal.H("9DD3EE"); skyBottom = Pal.H("DFF2FA"); ground = Pal.H("EAF5FA"); break;
                case Theme.Workshop: skyTop = Pal.H("4A5360"); skyBottom = Pal.H("78828F"); ground = Pal.H("3B4450"); break;
                case Theme.Arena: skyTop = Pal.H("1E3F30"); skyBottom = Pal.H("4B7A5E"); ground = Pal.H("2E5C45"); break;
                default: skyTop = Pal.H("8FD3F4"); skyBottom = Pal.H("D8F0D0"); ground = Pal.H("63B14E"); break;
            }
            HubDraw.Gradient(ctx, new Rect(0, 0, w, h * 0.72f), skyTop, skyBottom);
            Poly(p, ground, new Vector2(0, h * 0.62f), new Vector2(w * 0.3f, h * 0.55f), new Vector2(w * 0.7f, h * 0.6f), new Vector2(w, h * 0.54f), new Vector2(w, h), new Vector2(0, h));
            Color dark = HubDraw.Shade(ground, 0.78f), light = HubDraw.Shade(ground, 1.12f);
            switch (theme)
            {
                case Theme.Dunes:
                    p.fillColor = Pal.H("FFF1C9"); HubDraw.Circle(p, new Vector2(w * 0.78f, h * 0.22f), 70); p.Fill();
                    Poly(p, dark, new Vector2(w * 0.1f, h * 0.62f), new Vector2(w * 0.34f, h * 0.45f), new Vector2(w * 0.62f, h * 0.62f));
                    Poly(p, HubDraw.Shade(ground, 0.9f), new Vector2(w * 0.45f, h * 0.64f), new Vector2(w * 0.72f, h * 0.42f), new Vector2(w * 1.0f, h * 0.64f));
                    Poly(p, Pal.H("C9884A"), new Vector2(w * 0.3f, h * 0.6f), new Vector2(w * 0.38f, h * 0.38f), new Vector2(w * 0.46f, h * 0.6f));
                    break;
                case Theme.Glacier:
                    for (int i = 0; i < 5; i++)
                    {
                        float cx = w * (0.18f + 0.16f * i), ch = h * (0.22f + 0.07f * ((i * 3) % 4));
                        Poly(p, i % 2 == 0 ? Pal.H("6EC3EC") : Pal.H("A8E0F7"), new Vector2(cx - 50, h * 0.62f), new Vector2(cx, h * 0.62f - ch), new Vector2(cx + 50, h * 0.62f));
                    }
                    break;
                case Theme.Workshop:
                    p.fillColor = Pal.H("E8A92D");
                    for (int i = 0; i < 12; i++)
                    {
                        float a = Mathf.PI * 2f * i / 12f;
                        HubDraw.Circle(p, new Vector2(w * 0.5f + Mathf.Cos(a) * 118, h * 0.36f + Mathf.Sin(a) * 118), 24); p.Fill();
                    }
                    HubDraw.Circle(p, new Vector2(w * 0.5f, h * 0.36f), 112); p.Fill();
                    p.fillColor = Pal.H("3B4450"); HubDraw.Circle(p, new Vector2(w * 0.5f, h * 0.36f), 44); p.Fill();
                    break;
                case Theme.Arena:
                    p.fillColor = Pal.H("F1ECE0"); HubDraw.Ellipse(p, new Vector2(w * 0.5f, h * 0.5f), w * 0.34f, h * 0.18f, 48); p.Fill();
                    p.fillColor = Pal.H("2E5C45"); HubDraw.Ellipse(p, new Vector2(w * 0.5f, h * 0.51f), w * 0.29f, h * 0.14f, 48); p.Fill();
                    for (int i = 0; i < 5; i++) { p.fillColor = Pal.H("D8A524"); p.BeginPath(); p.MoveTo(new Vector2(w * (0.28f + 0.11f * i) - 10, h * 0.5f)); p.LineTo(new Vector2(w * (0.28f + 0.11f * i) + 10, h * 0.5f)); p.LineTo(new Vector2(w * (0.28f + 0.11f * i) + 10, h * 0.32f)); p.LineTo(new Vector2(w * (0.28f + 0.11f * i) - 10, h * 0.32f)); p.ClosePath(); p.Fill(); }
                    break;
                default:
                    p.fillColor = Pal.H("2A7C44"); HubDraw.Circle(p, new Vector2(w * 0.3f, h * 0.45f), 70); p.Fill();
                    break;
            }
        }
    }
}
