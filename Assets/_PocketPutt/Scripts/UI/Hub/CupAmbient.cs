using PocketPutt.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace PocketPutt.UI
{
    /// <summary>
    /// H6 / HUB-D10 "Ambient Life": one non-interactive overlay between the map art and the route that adds small themed movement
    /// to a cup map. Procedural (Painter2D), fixed-size state, no allocation per frame, alive only while its map is open.
    /// It lives in <c>scroller.content</c>, so everything it draws scrolls with the picture; anchors are authored in the same
    /// normalised picture space as <see cref="CupMapLayout"/> (x of width, y of height). Never reads or writes Progress.
    /// </summary>
    public sealed class CupAmbientView : VisualElement
    {
        /// <summary>Technical off switch (A/B tests, weak devices): <c>-pp-h6 0</c> on the command line or <c>ambient:off</c> in the self-test.</summary>
        public static bool Enabled = true;
        /// <summary>Overlays currently running; must be 0 whenever no cup map is open.</summary>
        public static int Running { get; private set; }
        /// <summary>QA/profiling: bit mask of the effect groups that are allowed to run (1 gears, 2 floor patches, 4 steam, 8 particles, 16 flags/fountain, 32 glints/critters/events). Default all.</summary>
        public static int Mask = 63;
        /// <summary>QA cost counters (managed time inside Tick and inside the paint callback since the last reset).</summary>
        public static long TickTicks, DrawTicks; public static int TickCount, DrawCount;
        public static void ResetStats() { TickTicks = DrawTicks = 0; TickCount = DrawCount = 0; }
        public static string Stats() => $"ambient cost: tick avg={(TickCount == 0 ? 0 : TickTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / TickCount):0.000}ms over {TickCount} ticks; paint avg={(DrawCount == 0 ? 0 : DrawTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency / DrawCount):0.000}ms over {DrawCount} repaints";

        const float Ref = 1080f;
        const float RepaintInterval = 1f / 30f;
        const int MaxParticles = 64;

        enum Kind { Leaf, Sand, Snow, Mote }
        enum Ev { None, Birds, Tumbleweed, SandGust, SnowGust, Steam, FountainJet, GoldGlint }

        sealed class Profile
        {
            public Kind kind;
            public int count;                  // always-on particles; the rest of the pool only exists during a gust
            public Vector2 wind;               // px/s at the 1080 reference width
            public float sizeMin, sizeMax;
            public Color colA, colB;
            public Vector4[] glintAreas = System.Array.Empty<Vector4>();   // cx (of width), cy (of height), rx, ry (both of width)
            public Color glint = Color.white;
            public Vector2[] critters = System.Array.Empty<Vector2>();     // butterfly anchors
            public Ev[] events = System.Array.Empty<Ev>();
            // anchors below are in art pixels (the 1080 x 3726 pictures), measured on the prerendered maps
            public Vector2[] flagsRed = System.Array.Empty<Vector2>(), flagsGreen = System.Array.Empty<Vector2>();   // top-left of the baked cloth
            public Vector2[] vents = System.Array.Empty<Vector2>();        // top of each baked steam stack
            public Vector2[] sparkles = System.Array.Empty<Vector2>();     // gold pedestal tops and the trophy
            public bool fountain;
            public Gear[] gears = System.Array.Empty<Gear>();
        }

        /// <summary>A baked gear that is replaced by a turning copy: same centre, tip radius, tooth phase, face colour and visible region.</summary>
        struct Gear
        {
            public Vector2 c; public float rt, phase, dps; public Color face; public Vector4 clip;   // clip: x0, y0, x1, y1 (art px)
            public Vector4 patch; public string patchKey;                                           // patch: x, y, w, h (art px)
        }

        static bool cliRead;
        static int entries;

        Profile profile;
        float aspect = 3.45f;
        int seed;
        bool running, needSeed;
        float t, repaintAcc, nextEvent;
        int evIndex;

        float viewTop, viewBottom, safeTop, safeBottom;

        // particle pool (struct-of-arrays, content px)
        readonly float[] pX = new float[MaxParticles], pY = new float[MaxParticles], pK = new float[MaxParticles], pPh = new float[MaxParticles], pSz = new float[MaxParticles];
        int poolSize;
        readonly float[] gearAng = new float[4];

        // the single running event
        Ev cur; float evT, evDur, evSeed, evY, evDir;

        public bool IsRunning => running;
        public string CurrentEvent => cur.ToString();

        readonly VisualElement patchLayer = new VisualElement();    // reconstructed floor under the animated gears (Workshop only)
        readonly VisualElement canvas = new VisualElement();        // everything procedural
        readonly System.Collections.Generic.List<Texture2D> patchTextures = new System.Collections.Generic.List<Texture2D>(4);
        readonly System.Collections.Generic.List<VisualElement> patchElements = new System.Collections.Generic.List<VisualElement>(4);
        readonly System.Collections.Generic.List<int> patchGear = new System.Collections.Generic.List<int>(4);

        public CupAmbientView()
        {
            pickingMode = PickingMode.Ignore;
            Fill(this);
            style.display = DisplayStyle.None;
            Fill(patchLayer); Fill(canvas);
            Add(patchLayer); Add(canvas);
            canvas.generateVisualContent += Draw;
            RegisterCallback<DetachFromPanelEvent>(e => Close());
        }

        static void Fill(VisualElement e)
        {
            e.pickingMode = PickingMode.Ignore;
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.right = 0; e.style.top = 0; e.style.bottom = 0;
        }

        // ---- lifetime ------------------------------------------------------------------------

        public void Open(int cupIndex, CupMapLayout layout)
        {
            Close();
            if (!cliRead) { cliRead = true; if (GameApp.CommandLineValue("-pp-h6") == "0") Enabled = false; }
            if (!Enabled || layout == null || layout.placeholder) return;
            profile = ProfileFor(cupIndex);
            if (profile == null) return;
            aspect = layout.aspect;
            seed = cupIndex * 977 + (++entries) * 131;
            t = 0f; repaintAcc = 0f; cur = Ev.None; evIndex = (int)(H(seed, 7) * profile.events.Length);
            nextEvent = 6f + H(seed, 1) * 4f;          // the first event waits a few seconds so entering a map never feels like a reward
            poolSize = Mathf.Min(MaxParticles, profile.count + (profile.kind == Kind.Sand || profile.kind == Kind.Snow ? 28 : 0));
            needSeed = true;
            System.Array.Clear(gearAng, 0, gearAng.Length);
            BuildPatches();
            running = true; Running++;
            style.display = DisplayStyle.Flex;
        }

        public void Close()
        {
            if (running) { running = false; Running--; }
            profile = null; cur = Ev.None;
            style.display = DisplayStyle.None;
            patchLayer.Clear(); patchElements.Clear(); patchGear.Clear();
            foreach (Texture2D tex in patchTextures) if (tex != null) Resources.UnloadAsset(tex);
            patchTextures.Clear();
        }

        /// <summary>Floor patches (made offline by tools/hub/h6_gear_patches.py) that hide the baked gears while the overlay gears turn; unloaded on Close.</summary>
        void BuildPatches()
        {
            Gear[] gears = profile.gears;
            for (int i = 0; i < gears.Length; i++)
            {
                if ((Mask & 2) == 0) continue;
                Texture2D tex = Resources.Load<Texture2D>(gears[i].patchKey);
                if (tex == null) continue;
                patchTextures.Add(tex);
                Vector4 r = gears[i].patch;
                var e = new VisualElement();
                e.pickingMode = PickingMode.Ignore;
                e.style.position = Position.Absolute;
                e.style.left = Length.Percent(r.x / Ref * 100f); e.style.top = Length.Percent(r.y / (Ref * aspect) * 100f);
                e.style.width = Length.Percent(r.z / Ref * 100f); e.style.height = Length.Percent(r.w / (Ref * aspect) * 100f);
                e.style.backgroundImage = new StyleBackground(tex);
                e.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
                e.style.display = DisplayStyle.None;      // shown by Tick only while its gear is on screen (hidden textured quads still cost CPU every frame)
                patchLayer.Add(e);
                patchElements.Add(e); patchGear.Add(i);
            }
        }

        /// <summary>QA: starts an event now (index into the cup's event list, wrapping); returns false when nothing is visible to host it.</summary>
        public bool Fire(int index)
        {
            if (!running || profile.events.Length == 0) return false;
            return Begin(profile.events[((index % profile.events.Length) + profile.events.Length) % profile.events.Length]);
        }

        // ---- per-cup authoring ---------------------------------------------------------------

        static Profile ProfileFor(int cupIndex)
        {
            switch (cupIndex)
            {
                case 0:   // Garden: drifting leaves, butterflies on the flower beds, glints on the pond; a small flock crosses now and then
                    return new Profile
                    {
                        kind = Kind.Leaf, count = 6, wind = new Vector2(46f, 30f), sizeMin = 11f, sizeMax = 16f,
                        colA = new Color(0.60f, 0.80f, 0.27f, 0.9f), colB = new Color(0.86f, 0.80f, 0.30f, 0.85f),
                        glintAreas = new[] { new Vector4(0.75f, 0.567f, 0.13f, 0.06f), new Vector4(0.474f, 0.6275f, 0.06f, 0.025f) },
                        glint = new Color(1f, 1f, 0.96f, 1f),
                        critters = new[] { new Vector2(0.259f, 0.179f), new Vector2(0.716f, 0.1775f), new Vector2(0.56f, 0.286f), new Vector2(0.44f, 0.4165f), new Vector2(0.397f, 0.7375f), new Vector2(0.534f, 0.775f), new Vector2(0.612f, 0.975f) },
                        events = new[] { Ev.Birds },
                    };
                case 1:   // Dunes: fine wind-blown sand, shimmer on the oasis; a tumbleweed rolls past and a short gust crosses
                    return new Profile
                    {
                        kind = Kind.Sand, count = 20, wind = new Vector2(210f, -38f), sizeMin = 1f, sizeMax = 1f,
                        colA = new Color(1f, 0.97f, 0.86f, 0.85f), colB = new Color(0.62f, 0.40f, 0.18f, 0.45f),
                        glintAreas = new[] { new Vector4(0.81f, 0.4925f, 0.14f, 0.07f), new Vector4(0.13f, 0.73f, 0.07f, 0.03f) },
                        glint = new Color(1f, 1f, 0.94f, 1f),
                        events = new[] { Ev.Tumbleweed, Ev.SandGust },
                    };
                case 2:   // Glacial: sparse two-depth snowfall, glints in the crystals; an occasional soft gust turns the snow sideways
                    return new Profile
                    {
                        kind = Kind.Snow, count = 18, wind = new Vector2(-8f, 55f), sizeMin = 2.8f, sizeMax = 6.8f,
                        colA = new Color(1f, 1f, 1f, 0.95f), colB = new Color(0.86f, 0.94f, 1f, 0.85f),
                        glintAreas = new[]
                        {
                            new Vector4(0.767f, 0.205f, 0.03f, 0.012f), new Vector4(0.198f, 0.3125f, 0.03f, 0.012f), new Vector4(0.897f, 0.3825f, 0.02f, 0.01f),
                            new Vector4(0.172f, 0.57f, 0.02f, 0.01f), new Vector4(0.215f, 0.71f, 0.02f, 0.01f), new Vector4(0.819f, 0.74f, 0.03f, 0.012f), new Vector4(0.81f, 0.84f, 0.025f, 0.01f),
                        },
                        glint = new Color(0.90f, 0.98f, 1f, 1f),
                        events = new[] { Ev.SnowGust },
                    };
                case 3:   // Workshop: steam stacks breathing on the vents; a hard puff now and then
                    return new Profile
                    {
                        kind = Kind.Mote, count = 0,
                        vents = new[] { new Vector2(752f, 771f), new Vector2(811f, 1595f), new Vector2(453f, 1880f), new Vector2(153f, 2172f), new Vector2(646f, 2438f) },
                        gears = new[]
                        {
                            new Gear { c = new Vector2(161f, 1236f), rt = 121f, phase = 0f,   dps = 11f,  face = new Color32(168, 110, 21, 255),  clip = new Vector4(115f, -1e4f, 1e4f, 1e4f),  patch = new Vector4(0f, 1074f, 354f, 315f),  patchKey = "Hub/H6/c4_gear_0" },
                            new Gear { c = new Vector2(173f, 2068f), rt = 113f, phase = 0f,   dps = -11f, face = new Color32(214, 159, 33, 255),  clip = new Vector4(115f, -1e4f, 1e4f, 1e4f),  patch = new Vector4(0f, 1912f, 358f, 303f),  patchKey = "Hub/H6/c4_gear_1" },
                            new Gear { c = new Vector2(897f, 923f),  rt = 94f,  phase = 11f,  dps = 7f,   face = new Color32(139, 153, 166, 255), clip = new Vector4(-1e4f, -1e4f, 967f, 981f), patch = new Vector4(743f, 782f, 320f, 273f), patchKey = "Hub/H6/c4_gear_2" },
                            new Gear { c = new Vector2(927f, 2378f), rt = 89f,  phase = 3.7f, dps = -7f,  face = new Color32(143, 158, 172, 255), clip = new Vector4(-1e4f, -1e4f, 967f, 2447f), patch = new Vector4(778f, 2241f, 302f, 265f), patchKey = "Hub/H6/c4_gear_3" },
                        },
                        events = new[] { Ev.Steam },
                    };
                case 4:   // Masters: pennants rippling in the breeze, the fountain playing, glints on the gold; the monument shines now and then
                    return new Profile
                    {
                        kind = Kind.Mote, count = 8, wind = new Vector2(7f, -16f), sizeMin = 2.6f, sizeMax = 4.6f,
                        colA = new Color(1f, 0.86f, 0.42f, 0.85f), colB = new Color(1f, 0.95f, 0.70f, 0.8f),
                        flagsRed = new[] { new Vector2(850f, 725f), new Vector2(474f, 1162f), new Vector2(295f, 1569f), new Vector2(358f, 2151f) },
                        flagsGreen = new[] { new Vector2(753f, 725f), new Vector2(377f, 1158f), new Vector2(198f, 1569f), new Vector2(261f, 2151f) },
                        fountain = true,
                        sparkles = new[] { new Vector2(540f, 355f), new Vector2(880f, 1005f), new Vector2(885f, 1708f), new Vector2(196f, 1965f), new Vector2(196f, 2730f) },
                        events = new[] { Ev.FountainJet, Ev.GoldGlint },
                    };
            }
            return null;
        }

        // ---- simulation ----------------------------------------------------------------------

        /// <summary>view* = visible part of the content (px); safe* = the band between header and sheet where events may start.</summary>
        public void Tick(float dt, float viewTop, float viewBottom, float safeTop, float safeBottom)
        {
            if (!running) return;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            TickInner(dt, viewTop, viewBottom, safeTop, safeBottom);
            TickTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; TickCount++;
        }

        void TickInner(float dt, float viewTop, float viewBottom, float safeTop, float safeBottom)
        {
            dt = Mathf.Min(dt, 0.1f);                  // a stall or a returning focus never fast-forwards the scene
            this.viewTop = viewTop; this.viewBottom = viewBottom; this.safeTop = safeTop; this.safeBottom = safeBottom;
            float W = canvas.contentRect.width;
            if (W <= 1f) return;
            float s = W / Ref;
            t += dt;

            if (needSeed) { SeedParticles(W, s); needSeed = false; }

            if (cur != Ev.None)
            {
                evT += dt;
                if (evT >= evDur) { cur = Ev.None; nextEvent = 15f + H(seed, 3, evIndex) * 25f; }
            }
            else
            {
                nextEvent -= dt;
                if (nextEvent <= 0f && profile.events.Length > 0)
                {
                    // the next event in rotation that has something visible to happen to; otherwise look again in a moment
                    bool started = false;
                    for (int k = 0; k < profile.events.Length && !started; k++) started = Begin(profile.events[(evIndex + k) % profile.events.Length]);
                    if (started) evIndex++; else nextEvent = 3f;
                }
            }

            for (int i = 0; i < patchElements.Count; i++)
            {
                Gear g = profile.gears[patchGear[i]];
                bool on = Visible(g.c.y * s, (g.rt + 40f) * s);
                if (on != (patchElements[i].style.display.value == DisplayStyle.Flex)) patchElements[i].style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (profile.gears.Length > 0)
            {
                // the steam event also spins the machinery up for a few seconds
                float boost = cur == Ev.Steam ? 1f + Env(evT / evDur) : 1f;
                for (int i = 0; i < profile.gears.Length; i++) gearAng[i] += profile.gears[i].dps * boost * dt;
            }
            float gust = cur == Ev.SandGust || cur == Ev.SnowGust ? Env(evT / evDur) : 0f;
            float top = viewTop - 60f, band = viewBottom - viewTop + 120f;
            for (int i = 0; i < poolSize; i++)
            {
                float k = pK[i], ph = pPh[i], vx, vy;
                switch (profile.kind)
                {
                    case Kind.Leaf:
                        vx = profile.wind.x * (0.6f + k * 0.8f) + Mathf.Sin(t * 1.3f + ph) * 38f;
                        vy = profile.wind.y * (0.6f + k * 0.8f) + Mathf.Cos(t * 1.7f + ph) * 22f;
                        break;
                    case Kind.Sand:
                    {
                        float boost = i >= profile.count ? 1.7f : 1f + gust * 1.3f;
                        vx = profile.wind.x * (0.7f + k * 0.6f) * boost;
                        vy = profile.wind.y * (0.7f + k * 0.6f) * boost + Mathf.Sin(t * 2.1f + ph) * 10f;
                        break;
                    }
                    case Kind.Snow:
                    {
                        float sway = Mathf.Sin(t * 0.9f + ph) * 24f * (0.4f + k);
                        float wind = i >= profile.count ? 0f : gust;
                        vx = profile.wind.x + sway + evDir * 340f * (0.6f + 0.8f * k) * (i >= profile.count ? 1f : wind);
                        vy = (40f + 95f * k) * (1f + 0.35f * gust) * (i >= profile.count ? 1.2f : 1f);
                        break;
                    }
                    case Kind.Mote:
                        vx = profile.wind.x + Mathf.Sin(t * 0.7f + ph) * 9f; vy = profile.wind.y * (0.5f + k);
                        break;
                    default: vx = vy = 0f; break;
                }
                pX[i] += vx * s * dt; pY[i] += vy * s * dt;
                if (pX[i] > W + 60f) pX[i] -= W + 120f; else if (pX[i] < -60f) pX[i] += W + 120f;
                pY[i] = top + Mathf.Repeat(pY[i] - top, band);
            }

            repaintAcc += dt;
            if (repaintAcc >= RepaintInterval) { repaintAcc = 0f; canvas.MarkDirtyRepaint(); }
        }

        void SeedParticles(float W, float s)
        {
            float top = viewTop - 60f, band = viewBottom - viewTop + 120f;
            for (int i = 0; i < poolSize; i++)
            {
                pX[i] = H(seed, 11, i) * (W + 120f) - 60f;
                pY[i] = top + H(seed, 12, i) * band;
                pK[i] = H(seed, 13, i);
                pPh[i] = H(seed, 14, i) * 6.2832f;
                pSz[i] = Mathf.Lerp(profile.sizeMin, profile.sizeMax, profile.kind == Kind.Snow ? pK[i] : H(seed, 15, i));
            }
        }

        Vector2 evAnchor;

        bool Begin(Ev e)
        {
            float W = canvas.contentRect.width, s = W / Ref;
            // events that belong to a place (a vent, the fountain, a golden pedestal) only start when that place is on screen
            Vector2 anchor = Vector2.zero;
            if (e == Ev.Steam || e == Ev.GoldGlint)
            {
                Vector2[] list = e == Ev.Steam ? profile.vents : profile.sparkles;
                int n = 0, pick = Mathf.FloorToInt(H(seed, 24, evIndex) * 8f);
                for (int i = 0; i < list.Length; i++) if (InSafe(list[i].y * s)) n++;
                if (n == 0) return false;
                pick %= n;
                for (int i = 0; i < list.Length; i++) if (InSafe(list[i].y * s) && pick-- == 0) { anchor = list[i]; break; }
            }
            else if (e == Ev.FountainJet)
            {
                if (!profile.fountain || !InSafe(2040f * s)) return false;
                anchor = new Vector2(907f, 1985f);
            }
            cur = e; evT = 0f; evSeed = H(seed, 21, evIndex); evAnchor = anchor;
            evDir = H(seed, 22, evIndex) < 0.5f ? 1f : -1f;
            // the path lives in content space: it scrolls with the picture and starts inside the band the player can see
            evY = Mathf.Lerp(safeTop, Mathf.Max(safeTop + 1f, safeBottom), 0.25f + 0.5f * H(seed, 23, evIndex));
            switch (e)
            {
                case Ev.Birds: evDur = 7f; break;
                case Ev.Tumbleweed: evDur = 8f; break;
                case Ev.SandGust: evDur = 4.2f; break;
                case Ev.SnowGust: evDur = 5f; break;
                case Ev.Steam: evDur = 4f; break;
                case Ev.FountainJet: evDur = 3.6f; break;
                case Ev.GoldGlint: evDur = 3.4f; break;
                default: evDur = 3f; break;
            }
            return true;
        }

        bool InSafe(float y) => y > safeTop && y < safeBottom;

        static float Env(float u) { float v = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u)); return v * v * (3f - 2f * v); }

        /// <summary>Stateless 0..1 hash so glints, wing phases and spawn jitter need no RNG state and replay identically for a given seed.</summary>
        static float H(int a, int b = 0, int c = 0)
        {
            unchecked
            {
                uint x = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(c * 83492791);
                x ^= x >> 13; x *= 0x5bd1e995u; x ^= x >> 15;
                return (x & 0xFFFFFFu) / (float)0x1000000;
            }
        }

        // ---- drawing -------------------------------------------------------------------------

        void Draw(MeshGenerationContext ctx)
        {
            if (!running || profile == null) return;
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            DrawInner(ctx);
            DrawTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0; DrawCount++;
        }

        void DrawInner(MeshGenerationContext ctx)
        {
            float W = canvas.contentRect.width;
            if (W <= 1f) return;
            float s = W / Ref, Hh = W * aspect;
            Painter2D p = ctx.painter2D;
            p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;

            if ((Mask & 32) != 0) { DrawGlints(p, W, Hh, s); DrawCritters(p, W, Hh, s); }
            if ((Mask & 8) != 0) { if (cur == Ev.SandGust) DrawHaze(p, W, s); DrawParticles(p, s); }
            if ((Mask & 1) != 0) DrawGears(p, s);
            if ((Mask & 16) != 0) { DrawFlags(p, s); DrawFountain(p, s); }
            if ((Mask & 4) != 0) DrawVents(p, s);
            if ((Mask & 32) != 0) switch (cur)
            {
                case Ev.Birds: DrawBirds(p, W, s); break;
                case Ev.Tumbleweed: DrawTumbleweed(p, W, s); break;
                case Ev.GoldGlint: DrawGoldGlint(p, s); break;
            }
        }

        bool Visible(float y, float margin) => y > viewTop - margin && y < viewBottom + margin;

        void DrawGlints(Painter2D p, float W, float Hh, float s)
        {
            Vector4[] areas = profile.glintAreas;
            for (int a = 0; a < areas.Length; a++)
            {
                Vector4 ar = areas[a];
                float cy = ar.y * Hh;
                if (!Visible(cy, ar.w * W + 40f)) continue;
                for (int slot = 0; slot < 3; slot++)
                {
                    float period = 2.6f + 1.6f * H(a, slot, 5);
                    float u = t / period + H(a, slot, 6);
                    int cycle = Mathf.FloorToInt(u);
                    float f = u - cycle, al = Mathf.Pow(Mathf.Sin(Mathf.PI * f), 3f);
                    if (al < 0.03f) continue;
                    // a different spot of the water each cycle, inside the ellipse
                    float ang = H(a * 3 + slot, cycle, 8) * 6.2832f, rad = Mathf.Sqrt(H(a * 3 + slot, cycle, 9));
                    Vector2 c = new Vector2(ar.x * W + Mathf.Cos(ang) * rad * ar.z * W, cy + Mathf.Sin(ang) * rad * ar.w * W);
                    Star(p, c, (9f + 7f * H(a, slot, 4)) * s * (0.6f + 0.4f * al), WithA(profile.glint, 0.85f * al));
                }
            }
        }

        /// <summary>A few long, thin, translucent dust banks drifting with the wind during a gust (never opaque, never covers the route).</summary>
        void DrawHaze(Painter2D p, float W, float s)
        {
            float env = Env(evT / evDur);
            for (int i = 0; i < 5; i++)
            {
                float y = Mathf.Lerp(viewTop, viewBottom, 0.12f + 0.76f * H(seed, 41, i + evIndex * 7));
                float x = Mathf.Repeat(H(seed, 42, i) * (W + 700f * s) + t * 420f * s * (0.8f + 0.4f * H(seed, 43, i)), W + 700f * s) - 350f * s;
                float rx = (230f + 150f * H(seed, 44, i)) * s, ry = (34f + 26f * H(seed, 45, i)) * s;
                p.fillColor = new Color(0.98f, 0.90f, 0.72f, 0.20f * env);
                HubDraw.Ellipse(p, new Vector2(x, y), rx, ry, 18); p.Fill();
                p.fillColor = new Color(0.98f, 0.93f, 0.78f, 0.16f * env);
                HubDraw.Ellipse(p, new Vector2(x + rx * 0.25f, y - ry * 0.2f), rx * 0.6f, ry * 0.55f, 14); p.Fill();
            }
        }

        void DrawParticles(Painter2D p, float s)
        {
            float gust = cur == Ev.SandGust || cur == Ev.SnowGust ? Env(evT / evDur) : 0f;
            for (int i = 0; i < poolSize; i++)
            {
                float y = pY[i];
                if (!Visible(y, 40f)) continue;
                float a = 1f;
                if (i >= profile.count) { a = gust; if (a < 0.02f) continue; }
                Vector2 c = new Vector2(pX[i], y);
                Color col = Color.Lerp(profile.colA, profile.colB, pK[i]);
                col.a *= a;
                switch (profile.kind)
                {
                    case Kind.Leaf:
                    {
                        float len = pSz[i] * s, flip = 0.35f + 0.65f * Mathf.Abs(Mathf.Cos(t * 2.2f + pPh[i]));
                        Leaf(p, c, t * 0.9f + pPh[i], len, len * 0.5f * flip, col);
                        break;
                    }
                    case Kind.Snow:
                    {
                        float r = pSz[i] * s;
                        if (i >= profile.count)
                        {
                            // gust flakes are drawn as short streaks along their drift
                            Vector2 v = new Vector2(evDir * 340f * (0.6f + 0.8f * pK[i]), 50f + 95f * pK[i]) * s;
                            p.lineWidth = r * 1.5f; p.strokeColor = new Color(0.96f, 0.99f, 1f, Mathf.Min(1f, col.a * 1.15f));
                            p.BeginPath(); p.MoveTo(c - v * 0.15f); p.LineTo(c); p.Stroke();
                        }
                        else
                        {
                            // a faint cool halo keeps the flake readable on white snow
                            p.fillColor = new Color(0.62f, 0.78f, 0.95f, 0.30f * col.a);
                            HubDraw.Circle(p, c, r * 1.9f); p.Fill();
                            p.fillColor = col;
                            HubDraw.Circle(p, c, r); p.Fill();
                        }
                        break;
                    }
                    case Kind.Mote:
                    {
                        float tw = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * 1.6f + pPh[i]));
                        p.fillColor = WithA(col, col.a * tw);
                        HubDraw.Circle(p, c, pSz[i] * s); p.Fill();
                        break;
                    }
                    case Kind.Sand:
                    {
                        float boost = i >= profile.count ? 1.7f : 1f + gust * 1.3f;
                        Vector2 v = new Vector2(profile.wind.x, profile.wind.y) * (0.7f + pK[i] * 0.6f) * boost;
                        Vector2 tail = c - v * (0.26f * s);
                        p.lineWidth = (3.2f + 2.6f * pK[i]) * s * (i >= profile.count ? 1.5f : 1f); p.strokeColor = col;
                        p.BeginPath(); p.MoveTo(tail); p.LineTo(c); p.Stroke();
                        break;
                    }
                }
            }
        }

        void DrawCritters(Painter2D p, float W, float Hh, float s)
        {
            Vector2[] a = profile.critters;
            for (int i = 0; i < a.Length; i++)
            {
                Vector2 anchor = new Vector2(a[i].x * W, a[i].y * Hh);
                if (!Visible(anchor.y, 80f)) continue;
                float ph = H(seed, 31, i) * 6.2832f, tt = t * (0.55f + 0.2f * H(seed, 32, i)) + ph;
                // a lazy figure of eight around its flower bed
                Vector2 c = anchor + new Vector2(Mathf.Sin(tt) * 78f, Mathf.Sin(tt * 2f) * 34f + Mathf.Cos(tt * 0.5f) * 14f) * s;
                Vector2 dir = new Vector2(Mathf.Cos(tt) * 78f, Mathf.Cos(tt * 2f) * 68f).normalized;
                float flap = 0.25f + 0.75f * Mathf.Abs(Mathf.Sin(t * 13f + ph));
                Color wing = i % 3 == 0 ? new Color(1f, 0.83f, 0.27f, 0.95f) : i % 3 == 1 ? new Color(1f, 0.97f, 0.88f, 0.95f) : new Color(1f, 0.55f, 0.25f, 0.95f);
                Vector2 n = new Vector2(-dir.y, dir.x);
                float w = 18f * s;
                // soft ground shadow, then two wing pairs that scissor in and out, then the body
                Wing(p, c + new Vector2(10f, 22f) * s, dir, n, w, flap, new Color(0f, 0f, 0f, 0.12f));
                Wing(p, c, dir, n, w, flap, wing);
                p.lineWidth = 4.5f * s; p.strokeColor = new Color(0.22f, 0.16f, 0.12f, 0.95f);
                p.BeginPath(); p.MoveTo(c + dir * 8f * s); p.LineTo(c - dir * 8f * s); p.Stroke();
            }
        }

        static void Wing(Painter2D p, Vector2 c, Vector2 dir, Vector2 n, float w, float flap, Color col)
        {
            p.fillColor = col;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 tip = c + n * (side * w * flap);
                Vector2 a = c + dir * w * 0.1f, b = tip + dir * w * 0.1f, e = c - dir * w * 0.2f;
                p.BeginPath();
                p.MoveTo(a);
                QuadTo(p, a, tip + dir * w * 0.95f, b);
                QuadTo(p, b, tip - dir * w * 0.8f, e);
                p.ClosePath(); p.Fill();
            }
        }

        void DrawBirds(Painter2D p, float W, float s)
        {
            float u = evT / evDur;
            float x0 = evDir > 0f ? -140f * s : W + 140f * s, x1 = evDir > 0f ? W + 140f * s : -140f * s;
            Vector2 lead = new Vector2(Mathf.Lerp(x0, x1, u), evY + (u - 0.5f) * 160f * s * (evSeed - 0.5f) * 2f);
            Vector2 dir = new Vector2(evDir, ((evSeed - 0.5f) * 2f) * 0.12f).normalized, n = new Vector2(-dir.y, dir.x);
            Vector2[] off = { new Vector2(0f, 0f), new Vector2(-54f, 30f), new Vector2(-58f, -26f), new Vector2(-112f, 58f), new Vector2(-120f, -4f) };
            for (int i = 0; i < off.Length; i++)
            {
                Vector2 c = lead + (dir * off[i].x + n * off[i].y) * s + new Vector2(0f, Mathf.Sin(t * 1.9f + i * 1.7f) * 9f * s);
                if (!Visible(c.y, 60f)) continue;
                float flap = Mathf.Sin(t * (7.5f + i * 0.35f) + i * 2.3f), span = 36f * s;
                // top-down bird: swept wings that beat, drawn twice (shadow on the ground, then the body)
                Bird(p, c + new Vector2(30f, 70f) * s, dir, n, span * 0.9f, flap, new Color(0f, 0f, 0f, 0.13f));
                Bird(p, c, dir, n, span, flap, new Color(0.13f, 0.17f, 0.22f, 0.92f));
            }
        }

        /// <summary>The universally readable gull mark: two arched wings that beat up and down; heading only mirrors it.</summary>
        static void Bird(Painter2D p, Vector2 c, Vector2 dir, Vector2 n, float span, float flap, Color col)
        {
            float lift = (0.12f + 0.38f * (0.5f + 0.5f * flap)) * span;     // wing tips rise and fall
            Vector2 l = c + new Vector2(-span, -lift * 0.9f + span * 0.1f), r = c + new Vector2(span, -lift * 0.9f + span * 0.1f);
            p.strokeColor = col; p.fillColor = col; p.lineWidth = span * 0.2f;
            p.BeginPath();
            p.MoveTo(l);
            QuadTo(p, l, c + new Vector2(-span * 0.55f, -lift * 1.25f), c + new Vector2(0f, span * 0.12f), 7);
            QuadTo(p, c + new Vector2(0f, span * 0.12f), c + new Vector2(span * 0.55f, -lift * 1.25f), r, 7);
            p.Stroke();
            p.BeginPath(); HubDraw.Ellipse(p, c + new Vector2(0f, span * 0.08f), span * 0.14f, span * 0.2f, 10); p.Fill();
        }

        void DrawTumbleweed(Painter2D p, float W, float s)
        {
            float u = evT / evDur, R = 40f * s;
            float x0 = evDir > 0f ? -90f * s : W + 90f * s, x1 = evDir > 0f ? W + 90f * s : -90f * s;
            float x = Mathf.Lerp(x0, x1, u);
            float baseY = evY + Mathf.Sin(u * 6.2832f * 0.5f) * 90f * s * (evSeed - 0.5f);
            if (!Visible(baseY, 80f)) return;
            float hop = Mathf.Abs(Mathf.Sin(t * 2.6f)) * 30f * s;
            Vector2 c = new Vector2(x, baseY - R - hop);
            float rot = (x / R) * evDir;
            // shadow stays on the ground and shrinks as the ball hops
            float sh = 1f - hop / (60f * s);
            p.fillColor = new Color(0f, 0f, 0f, 0.16f * sh);
            HubDraw.Ellipse(p, new Vector2(x + 8f * s, baseY - 4f * s), R * 0.95f * sh, R * 0.32f * sh, 16); p.Fill();
            p.fillColor = new Color(0.66f, 0.47f, 0.25f, 0.30f);
            HubDraw.Circle(p, c, R); p.Fill();
            p.strokeColor = new Color(0.47f, 0.31f, 0.15f, 0.95f); p.lineWidth = 3.4f * s;
            HubDraw.Circle(p, c, R); p.Stroke();
            p.lineWidth = 2.4f * s;
            for (int i = 0; i < 7; i++)
            {
                float a1 = rot + i * 0.9f, a2 = a1 + 2.2f + H(i, 5) * 1.3f;
                Vector2 q1 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * R * 0.97f, q2 = c + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * R * 0.97f;
                p.BeginPath(); p.MoveTo(q1); QuadTo(p, q1, c + (q1 + q2 - 2f * c) * 0.18f, q2, 5); p.Stroke();
            }
        }

        // ---- Masters: pennants, fountain, gold ----------------------------------------------

        void DrawFlags(Painter2D p, float s)
        {
            Vector2[] red = profile.flagsRed, green = profile.flagsGreen;
            for (int i = 0; i < red.Length; i++) Pennant(p, red[i] * s, s, i * 1.9f, new Color(0.65f, 0.19f, 0.23f, 1f));
            for (int i = 0; i < green.Length; i++) Pennant(p, green[i] * s, s, i * 1.9f + 0.7f, new Color(0.15f, 0.375f, 0.27f, 1f));
        }

        /// <summary>
        /// A rippling copy of the baked cloth (same colour, size and attachment to the pole); it is drawn a hair larger so the still
        /// cloth underneath never shows as a second flag. Amplitude grows toward the free edge, like cloth does.
        /// </summary>
        void Pennant(Painter2D p, Vector2 o, float s, float phase, Color col)
        {
            if (!Visible(o.y, 80f)) return;
            const int N = 6;
            float w = 46f * s, h = 31f * s, tip = 9f * s;
            float breeze = 1f + 0.35f * Mathf.Sin(t * 0.37f + phase);
            float amp = 5.2f * s * breeze, wt = t * 3.1f + phase;
            for (int i = 0; i < N; i++)
            {
                float u0 = i / (float)N, u1 = (i + 1) / (float)N;
                float y0 = o.y + Wave(u0, wt, amp), y1 = o.y + Wave(u1, wt, amp);
                float x0 = o.x + u0 * w * (1f - 0.06f * u0 * (1f + Mathf.Sin(wt))), x1 = o.x + u1 * w * (1f - 0.06f * u1 * (1f + Mathf.Sin(wt)));
                float b0 = h + tip * (1f - Mathf.Abs(2f * u0 - 1f)), b1 = h + tip * (1f - Mathf.Abs(2f * u1 - 1f));
                float shade = 1f + 0.55f * (Wave(u1, wt, 1f) - Wave(u0, wt, 1f));      // crests catch light, troughs fall into shade
                p.fillColor = HubDraw.Shade(col, Mathf.Clamp(shade, 0.8f, 1.15f));
                p.BeginPath();
                p.MoveTo(new Vector2(x0 - 0.5f * s, y0 - 1.2f * s)); p.LineTo(new Vector2(x1 + 0.5f * s, y1 - 1.2f * s));
                p.LineTo(new Vector2(x1 + 0.5f * s, y1 + b1 + 1.2f * s)); p.LineTo(new Vector2(x0 - 0.5f * s, y0 + b0 + 1.2f * s));
                p.ClosePath(); p.Fill();
                // the darker lower band of the baked pennant
                p.fillColor = HubDraw.Shade(col, 0.9f * Mathf.Clamp(shade, 0.8f, 1.15f));
                p.BeginPath();
                p.MoveTo(new Vector2(x0, y0 + h * 0.74f)); p.LineTo(new Vector2(x1, y1 + h * 0.74f));
                p.LineTo(new Vector2(x1, y1 + b1 + 1.2f * s)); p.LineTo(new Vector2(x0, y0 + b0 + 1.2f * s));
                p.ClosePath(); p.Fill();
            }
        }

        static float Wave(float u, float wt, float amp) => amp * u * Mathf.Sin(wt - u * 5.2f);

        void DrawFountain(Painter2D p, float s)
        {
            if (!profile.fountain || !Visible(2040f * s, 220f * s)) return;
            Vector2 top = new Vector2(907f, 1985f) * s, baseC = new Vector2(900f, 2056f) * s;
            float jet = cur == Ev.FountainJet ? Env(evT / evDur) : 0f;
            // ripples spreading from the pedestal, kept inside the water and left of the cream wall
            for (int i = 0; i < 3; i++)
            {
                float u = Mathf.Repeat(t / (jet > 0f ? 2.2f : 3.2f) + i / 3f, 1f);
                float rx = (14f + 78f * u) * s, ry = rx * 0.74f, a = 0.50f * (1f - u) * Mathf.Min(1f, u * 6f);
                p.strokeColor = new Color(0.92f, 1f, 1f, a); p.lineWidth = 2.6f * s;
                bool pen = false;
                for (int k = 0; k <= 28; k++)
                {
                    float ang = k / 28f * 6.2832f;
                    Vector2 q = baseC + new Vector2(Mathf.Cos(ang) * rx, Mathf.Sin(ang) * ry);
                    float ex = (q.x / s - 915f) / 104f, ey = (q.y / s - 2035f) / 77f;
                    bool ok = q.x / s <= 905f && ex * ex + ey * ey < 0.92f;
                    if (ok) { if (!pen) { p.BeginPath(); p.MoveTo(q); pen = true; } else p.LineTo(q); }
                    else if (pen) { p.Stroke(); pen = false; }
                }
                if (pen) p.Stroke();
            }
            // droplets arc out of the dome and fall back into the basin
            int drops = 8 + Mathf.RoundToInt(8f * jet);
            float lift = (22f + 34f * jet) * s;
            for (int i = 0; i < drops; i++)
            {
                float period = 1.15f, u = Mathf.Repeat(t / period + H(seed, 61, i), 1f);
                int cyc = Mathf.FloorToInt(t / period + H(seed, 61, i));
                float side = (H(seed, 62, i + cyc * 13) - 0.5f) * 2f;
                float h = 4f * u * (1f - u) * lift;
                Vector2 q = top + new Vector2(side * 34f * u * s, 18f * u * s * (0.6f + 0.4f * Mathf.Abs(side)) - h);
                p.fillColor = new Color(0.86f, 0.97f, 1f, 0.9f * Mathf.Min(1f, (1f - u) * 4f));
                HubDraw.Circle(p, q, (2.4f + 1.2f * H(seed, 63, i)) * s); p.Fill();
            }
        }

        void DrawGoldGlint(Painter2D p, float s)
        {
            float u = evT / evDur, env = Env(u);
            Vector2 c = evAnchor * s;
            // a soft warm bloom behind a ring of glints that wake up one after another
            for (int i = 0; i < 3; i++)
            {
                p.fillColor = new Color(1f, 0.86f, 0.40f, 0.10f * env * (1f - i * 0.28f));
                HubDraw.Circle(p, c, (70f + i * 30f + 20f * u) * s); p.Fill();
            }
            for (int i = 0; i < 6; i++)
            {
                float local = Mathf.Clamp01((u - i * 0.1f) / 0.5f), a = Mathf.Sin(Mathf.PI * local);
                if (a < 0.03f) continue;
                float ang = H(seed, 71, i) * 6.2832f, rad = (26f + 44f * H(seed, 72, i)) * s;
                Star(p, c + new Vector2(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad * 0.8f), (13f + 9f * H(seed, 73, i)) * s * a, new Color(1f, 0.95f, 0.72f, 0.95f * a));
            }
        }

        // ---- Workshop: gears -----------------------------------------------------------------

        const float Tilt = 0.77f;      // vertical squash of the map camera; gears are circles seen through it
        static readonly Vector2[] polyIn = new Vector2[64], polyA = new Vector2[64], polyB = new Vector2[64];

        void DrawGears(Painter2D p, float s)
        {
            Gear[] gears = profile.gears;
            for (int i = 0; i < gears.Length; i++)
            {
                Gear g = gears[i];
                Vector2 c = g.c * s;
                if (!Visible(c.y, g.rt * s + 40f)) continue;
                float rt = g.rt * s, rb = rt * 0.845f, tw = rt * 0.27f, rot = (g.phase + gearAng[i]) * Mathf.Deg2Rad;
                Rect clip = Rect.MinMaxRect(g.clip.x * s, g.clip.y * s, g.clip.z * s, g.clip.w * s);
                // cast shadow (the light comes from the lower left), then the side of the plate, then the face
                p.fillColor = new Color(0.04f, 0.07f, 0.12f, 0.13f);
                FillDisc(p, c + new Vector2(13f, -9f) * s, rb + (rt - rb) * 0.3f, 28, clip);
                GearShape(p, c + new Vector2(0f, 7f * s), rb, rt, 12, tw, rot, HubDraw.Shade(g.face, 0.70f), clip, true);
                GearShape(p, c, rb, rt, 12, tw, rot, g.face, clip, false);
                // twelve spokes, slightly darker than the plate, under the teeth
                p.strokeColor = HubDraw.Shade(g.face, 0.86f); p.lineWidth = rt * 0.095f; p.lineCap = LineCap.Butt;
                p.BeginPath();
                for (int k = 0; k < 12; k++)
                {
                    float a = rot + k * Mathf.PI / 6f, ca = Mathf.Cos(a), sa = Mathf.Sin(a) * Tilt;
                    Vector2 q0 = c + new Vector2(ca, sa) * rb * 0.40f, q1 = c + new Vector2(ca, sa) * rb * 0.90f;
                    if (ClipSegment(ref q0, ref q1, clip)) { p.MoveTo(q0); p.LineTo(q1); }
                }
                p.Stroke(); p.lineCap = LineCap.Round;
                // hub: a small toothed disc with a hole that shows the floor
                float hub = rt * 0.34f;
                GearShape(p, c, hub * 0.86f, hub, 9, hub * 0.42f, -rot * 1.3f, new Color(0.26f, 0.30f, 0.35f, 1f), clip, false);
                p.fillColor = new Color(0.60f, 0.66f, 0.72f, 1f);
                FillDisc(p, c, hub * 0.40f, 6, clip);
            }
        }

        /// <summary>A gear = disc + radial teeth, every piece convex so each is clipped to the visible region on its own.</summary>
        void GearShape(Painter2D p, Vector2 c, float rb, float rt, int teeth, float tw, float rot, Color col, Rect clip, bool lowerHalfOnly)
        {
            p.fillColor = col;
            FillDisc(p, c, rb, 28, clip);
            float hw = tw * 0.5f;
            for (int k = 0; k < teeth; k++)
            {
                float a = rot + k * (Mathf.PI * 2f / teeth), ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                if (lowerHalfOnly && sa < -0.35f) continue;      // the plate's side is only visible below the face
                Vector2 ax = new Vector2(ca, sa * Tilt), nx = new Vector2(-sa, ca * Tilt);
                float rin = rb * 0.9f;
                polyIn[0] = c + ax * rin - nx * hw; polyIn[1] = c + ax * rt - nx * hw * 0.82f; polyIn[2] = c + ax * rt + nx * hw * 0.82f; polyIn[3] = c + ax * rin + nx * hw;
                FillClipped(p, 4, clip);
            }
        }

        void FillDisc(Painter2D p, Vector2 c, float r, int seg, Rect clip)
        {
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                polyIn[i] = c + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * Tilt);
            }
            FillClipped(p, seg, clip);
        }

        void FillClipped(Painter2D p, int n, Rect r)
        {
            // Sutherland-Hodgman against the four sides of the visible rectangle (polyIn -> polyA -> polyB -> polyA -> polyB)
            n = ClipSide(polyIn, n, polyA, 0, r.xMin, true);
            n = ClipSide(polyA, n, polyB, 0, r.xMax, false);
            n = ClipSide(polyB, n, polyA, 1, r.yMin, true);
            n = ClipSide(polyA, n, polyB, 1, r.yMax, false);
            if (n < 3) return;
            p.BeginPath(); p.MoveTo(polyB[0]);
            for (int i = 1; i < n; i++) p.LineTo(polyB[i]);
            p.ClosePath(); p.Fill();
        }

        static int ClipSide(Vector2[] src, int n, Vector2[] dst, int axis, float bound, bool keepGreater)
        {
            int m = 0;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = src[i], b = src[(i + 1) % n];
                float va = (axis == 0 ? a.x : a.y) - bound, vb = (axis == 0 ? b.x : b.y) - bound;
                if (!keepGreater) { va = -va; vb = -vb; }
                bool ina = va >= 0f, inb = vb >= 0f;
                if (ina && m < 63) dst[m++] = a;
                if (ina != inb && m < 63) dst[m++] = Vector2.Lerp(a, b, va / (va - vb));
            }
            return m;
        }

        static bool ClipSegment(ref Vector2 a, ref Vector2 b, Rect r)
        {
            float t0 = 0f, t1 = 1f, dx = b.x - a.x, dy = b.y - a.y;
            float[] pp = { -dx, dx, -dy, dy }, qq = { a.x - r.xMin, r.xMax - a.x, a.y - r.yMin, r.yMax - a.y };
            for (int i = 0; i < 4; i++)
            {
                if (pp[i] == 0f) { if (qq[i] < 0f) return false; continue; }
                float u = qq[i] / pp[i];
                if (pp[i] < 0f) { if (u > t1) return false; if (u > t0) t0 = u; }
                else { if (u < t0) return false; if (u < t1) t1 = u; }
            }
            Vector2 a0 = a; a = a0 + new Vector2(dx, dy) * t0; b = a0 + new Vector2(dx, dy) * t1;
            return true;
        }

        // ---- Workshop: steam -----------------------------------------------------------------

        void DrawVents(Painter2D p, float s)
        {
            Vector2[] v = profile.vents;
            for (int i = 0; i < v.Length; i++)
            {
                Vector2 o = v[i] * s;
                if (!Visible(o.y, 260f * s)) continue;
                bool burst = cur == Ev.Steam && evAnchor == v[i];
                // breathing: one slow puff after another on every stack, ... and a short hard burst when it is the event's vent
                for (int k = 0; k < 2; k++)
                {
                    float period = 4.4f + 0.8f * H(i, 3), u = Mathf.Repeat(t / period + H(i, 4) + k * 0.5f, 1f);
                    Puff(p, o, u, 0.62f, 150f * s, 17f * s, 26f * s, i * 1.3f + k, s);
                }
                if (burst)
                {
                    for (int k = 0; k < 6; k++)
                    {
                        float u = (evT - k * 0.16f) / 2.3f;
                        if (u <= 0f || u >= 1f) continue;
                        Puff(p, o, u, 0.9f, 230f * s, 22f * s, 40f * s, k * 2.1f, s);
                    }
                }
            }
        }

        void Puff(Painter2D p, Vector2 o, float u, float alpha, float rise, float r0, float r1, float phase, float s)
        {
            float a = alpha * Mathf.Pow(1f - u, 1.4f) * Mathf.Min(1f, u * 7f);
            if (a < 0.02f) return;
            Vector2 c = o + new Vector2(Mathf.Sin(u * 3f + phase) * 12f * s + u * 26f * s, -u * rise);
            float r = Mathf.Lerp(r0, r1, Mathf.Sqrt(u));
            p.fillColor = new Color(0.60f, 0.66f, 0.74f, a * 0.35f);              // shaded underside
            HubDraw.Circle(p, c + new Vector2(2f * s, 4f * s), r); p.Fill();
            p.fillColor = new Color(0.98f, 0.98f, 1f, a);
            HubDraw.Circle(p, c, r); p.Fill();
            p.fillColor = new Color(1f, 1f, 1f, a * 0.8f);
            HubDraw.Circle(p, c + new Vector2(-r * 0.25f, -r * 0.3f), r * 0.6f); p.Fill();
        }

        // ---- primitives ----------------------------------------------------------------------

        static Color WithA(Color c, float a) { c.a = a; return c; }

        /// <summary>Quadratic curve as a short polyline (Painter2D's own curve command left stray edges to the origin in the Player).</summary>
        static void QuadTo(Painter2D p, Vector2 from, Vector2 ctrl, Vector2 to, int seg = 6)
        {
            for (int i = 1; i <= seg; i++)
            {
                float u = i / (float)seg, v = 1f - u;
                p.LineTo(v * v * from + 2f * u * v * ctrl + u * u * to);
            }
        }

        static void Leaf(Painter2D p, Vector2 c, float ang, float len, float wid, Color col)
        {
            Vector2 d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len, n = new Vector2(-d.y, d.x).normalized * wid;
            p.fillColor = col;
            p.BeginPath(); p.MoveTo(c - d); QuadTo(p, c - d, c + n * 1.6f, c + d, 5); QuadTo(p, c + d, c - n * 1.6f, c - d, 5); p.ClosePath(); p.Fill();
        }

        /// <summary>Four-point glint: a soft dot plus a thin cross.</summary>
        static void Star(Painter2D p, Vector2 c, float r, Color col)
        {
            p.fillColor = WithA(col, col.a * 0.5f);
            HubDraw.Circle(p, c, r * 0.28f); p.Fill();
            p.strokeColor = col; p.lineWidth = Mathf.Max(1.5f, r * 0.16f);
            p.BeginPath(); p.MoveTo(c + new Vector2(-r, 0f)); p.LineTo(c + new Vector2(r, 0f)); p.MoveTo(c + new Vector2(0f, -r)); p.LineTo(c + new Vector2(0f, r)); p.Stroke();
        }
    }
}
