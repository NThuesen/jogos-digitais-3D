using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PocketPutt.UI
{
    /// <summary>
    /// H5 / HUB-D9: the table the five maquettes stand on. One continuous, non-interactive layer in scroll space (so it never has a seam and moves
    /// with the maquettes): a desaturated graphite-teal base with a slow tonal drift, soft light pools where the route rests between worlds, a few
    /// very faint cartographic contours and a ruler along both edges. Drawn once with Painter2D (no texture, nothing to load or release).
    /// </summary>
    public sealed class AtlasBackdrop : VisualElement
    {
        /// <summary>Tone alternatives compared in the Player (H5-B): 0 graphite teal (chosen), 1 cooler slate, 2 warmer moss graphite. Test override: -pp-atlas N.</summary>
        public static int Variant = ParseVariant();

        static int ParseVariant()
        {
            string v = PocketPutt.Game.GameApp.CommandLineValue("-pp-atlas");
            return int.TryParse(v, out int n) && n >= 0 && n < 3 ? n : 0;
        }

        static readonly string[][] StopSets =
        {
            new[] { "1E2F33", "24373B", "213338", "253739", "1F3230" },
            new[] { "222B35", "29343F", "252F3A", "2A3540", "232C34" },
            new[] { "232F2C", "2B3A35", "27342F", "2D3B35", "222E29" },
        };

        public static Color Base => Pal.H(StopSets[Variant][1]);

        static Color Stop(int i) => Pal.H(StopSets[Variant][i]);

        readonly List<float> rests;
        readonly int seed;

        public AtlasBackdrop(List<float> restCentres, int seed)
        {
            rests = restCentres; this.seed = seed;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float w = contentRect.width, h = contentRect.height;
            if (w <= 0f || h <= 0f) return;

            // 1) tonal drift: consecutive quads share their edge colour, so there is no seam
            int ns = StopSets[Variant].Length;
            for (int i = 0; i + 1 < ns; i++)
                HubDraw.Gradient(ctx, new Rect(0f, h * i / (ns - 1), w, h / (ns - 1) + 1f), Stop(i), Stop(i + 1));

            // 2) light pools in the resting places between worlds (concentric low-alpha ellipses: reads as diffuse studio light, not a spotlight)
            Color pool = Pal.H("6E9AA2");
            foreach (float y in rests)
                for (int k = 0; k < 22; k++)
                {
                    float s = 1f - k * 0.04f;
                    p.fillColor = Pal.A(pool, 0.0085f);
                    HubDraw.Ellipse(p, new Vector2(w * 0.5f, y), w * 0.82f * s, 230f * s, 56);
                    p.Fill();
                }

            // 3) contours: a handful of faint, wobbling closed curves; deterministic per seed so every Build() looks the same
            var rng = new System.Random(seed);
            p.lineJoin = LineJoin.Round;
            p.lineWidth = 2.5f;
            int clusters = Mathf.Max(3, Mathf.RoundToInt(h / 640f));
            for (int c = 0; c < clusters; c++)
            {
                float cy = h * (c + 0.5f) / clusters + (float)(rng.NextDouble() - 0.5) * 140f;
                float cx = (c % 2 == 0 ? 0.08f : 0.94f) * w + (float)(rng.NextDouble() - 0.5) * 90f;
                float r0 = 90f + (float)rng.NextDouble() * 60f;
                float ph1 = (float)rng.NextDouble() * 6.28f, ph2 = (float)rng.NextDouble() * 6.28f, sq = 0.75f + (float)rng.NextDouble() * 0.5f;
                int rings = 6 + rng.Next(0, 3);
                for (int r = 0; r < rings; r++)
                {
                    float rad = r0 + r * (44f + r * 5f);
                    p.strokeColor = Pal.A(Pal.H("D7E8E4"), Mathf.Lerp(0.09f, 0.04f, r / (float)rings));
                    p.BeginPath();
                    const int n = 80;
                    for (int i = 0; i < n; i++)
                    {
                        float a = Mathf.PI * 2f * i / n;
                        float rr = rad * (1f + 0.09f * Mathf.Sin(3f * a + ph1 + r * 0.35f) + 0.05f * Mathf.Sin(5f * a + ph2 - r * 0.2f));
                        var q = new Vector2(cx + Mathf.Cos(a) * rr, cy + Mathf.Sin(a) * rr * sq);
                        if (i == 0) p.MoveTo(q); else p.LineTo(q);
                    }
                    p.ClosePath();
                    p.Stroke();
                }
            }

            // 4) ruler ticks along both edges: the page of an atlas
            p.lineWidth = 3f;
            p.strokeColor = Pal.A(Pal.H("D7E8E4"), 0.085f);
            p.BeginPath();
            int t = 0;
            for (float y = 60f; y < h - 40f; y += 90f, t++)
            {
                float len = t % 5 == 0 ? 40f : 20f;
                p.MoveTo(new Vector2(20f, y)); p.LineTo(new Vector2(20f + len, y));
                p.MoveTo(new Vector2(w - 20f, y)); p.LineTo(new Vector2(w - 20f - len, y));
            }
            p.Stroke();
        }
    }

    /// <summary>
    /// H5 / HUB-D8: the Home "exhibition table". Warm-neutral studio backdrop (graphite wall into a moss-graphite table, a soft light pool under the hero),
    /// fully opaque so the 3D level preview behind the menu is hidden, no vignette and no flat green.
    /// </summary>
    public sealed class HomeBackdrop : VisualElement
    {
        Vector2 spot = new Vector2(0.5f, 0.5f);   // light pool centre, normalised to this element

        public HomeBackdrop()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void SetSpot(Vector2 normalised)
        {
            if ((normalised - spot).sqrMagnitude < 1e-6f) return;
            spot = normalised;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float w = contentRect.width, h = contentRect.height;
            if (w <= 0f || h <= 0f) return;
            float horizon = h * Mathf.Clamp(spot.y + 0.1f, 0.5f, 0.75f);          // where the wall turns into the table (no visible edge)

            // wall -> table in one continuous run: graphite teal, lighter toward the hero, then a moss-graphite table that darkens
            // toward the buttons so the cream CTAs stay the brightest things on screen
            Color[] stops = { Pal.H("1A2A2E"), Pal.H("22373A"), Pal.H("2E4441"), Pal.H("37453A"), Pal.H("2A332D"), Pal.H("1E2522") };
            float[] at = { 0f, horizon * 0.5f, horizon, horizon + (h - horizon) * 0.3f, horizon + (h - horizon) * 0.65f, h };
            for (int i = 0; i + 1 < stops.Length; i++)
                HubDraw.Gradient(ctx, new Rect(0, at[i], w, at[i + 1] - at[i] + 1f), stops[i], stops[i + 1]);

            // soft studio light under the hero (warm cream, concentric low-alpha ellipses)
            Vector2 c = new Vector2(spot.x * w, spot.y * h);
            for (int k = 0; k < 26; k++)
            {
                float s = 1f - k * 0.035f;
                p.fillColor = Pal.A(Pal.H("F3E4C2"), 0.011f);
                HubDraw.Ellipse(p, c, w * 0.78f * s, h * 0.2f * s, 64);
                p.Fill();
            }
        }
    }
}
