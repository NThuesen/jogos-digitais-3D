using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PocketPutt.UI
{
    public static class Pal
    {
        public static readonly Color Ink = H("1F3A2B");
        public static readonly Color Deep = H("143524");
        public static readonly Color Cream = H("FFF6DF");
        public static readonly Color CreamDark = H("EBDDB8");
        public static readonly Color Gold = H("F5B93B");
        public static readonly Color GoldDark = H("C98C16");
        public static readonly Color Green = H("3DA35D");
        public static readonly Color GreenDark = H("2A7C44");
        public static readonly Color Red = H("E4573D");
        public static readonly Color Slate = H("5B6B63");
        public static readonly Color StarOff = H("CDBF9C");
        public static Color H(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color c); return c; }
        public static Color A(Color c, float a) { c.a = a; return c; }
    }

    public enum Icon { Star, Lock, Back, Restart, Gear, Play, Check, Flag, Chevron, Home, Trophy }

    /// <summary>Vector icon drawn with Painter2D so there are no image assets to import.</summary>
    public sealed class IconElement : VisualElement
    {
        public Icon icon;
        public Color color = Color.white;
        public Color outline = new Color(0, 0, 0, 0);
        public float direction = 1f; // Chevron: +1 right, -1 left

        public IconElement(Icon icon, float size, Color color)
        {
            this.icon = icon; this.color = color;
            style.width = size; style.height = size;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Set(Color c, Color o) { color = c; outline = o; MarkDirtyRepaint(); }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float w = contentRect.width, h = contentRect.height;
            float s = Mathf.Min(w, h);
            Vector2 c = new Vector2(w * 0.5f, h * 0.5f);
            p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
            switch (icon)
            {
                case Icon.Star: DrawStar(p, c, s * 0.48f, s * 0.21f, color, outline, s * 0.07f); break;
                case Icon.Lock:
                {
                    float bw = s * 0.62f, bh = s * 0.46f;
                    p.strokeColor = color; p.lineWidth = s * 0.11f;
                    p.BeginPath();
                    p.Arc(new Vector2(c.x, c.y - s * 0.08f), s * 0.17f, new Angle(180, AngleUnit.Degree), new Angle(360, AngleUnit.Degree));
                    p.LineTo(new Vector2(c.x + s * 0.17f, c.y + s * 0.04f));
                    p.MoveTo(new Vector2(c.x - s * 0.17f, c.y - s * 0.08f));
                    p.LineTo(new Vector2(c.x - s * 0.17f, c.y + s * 0.04f));
                    p.Stroke();
                    RoundRect(p, c.x - bw * 0.5f, c.y + s * 0.02f, bw, bh, s * 0.1f, color);
                    break;
                }
                case Icon.Back:
                    p.strokeColor = color; p.lineWidth = s * 0.13f;
                    p.BeginPath(); p.MoveTo(new Vector2(c.x + s * 0.12f, c.y - s * 0.3f)); p.LineTo(new Vector2(c.x - s * 0.18f, c.y)); p.LineTo(new Vector2(c.x + s * 0.12f, c.y + s * 0.3f)); p.Stroke();
                    break;
                case Icon.Chevron:
                    p.strokeColor = color; p.lineWidth = s * 0.14f;
                    p.BeginPath(); p.MoveTo(new Vector2(c.x - direction * s * 0.12f, c.y - s * 0.3f)); p.LineTo(new Vector2(c.x + direction * s * 0.18f, c.y)); p.LineTo(new Vector2(c.x - direction * s * 0.12f, c.y + s * 0.3f)); p.Stroke();
                    break;
                case Icon.Restart:
                {
                    p.strokeColor = color; p.lineWidth = s * 0.12f;
                    p.BeginPath();
                    p.Arc(c, s * 0.3f, new Angle(-60, AngleUnit.Degree), new Angle(230, AngleUnit.Degree));
                    p.Stroke();
                    // arrow head at the start of the arc
                    Vector2 tip = c + new Vector2(Mathf.Cos(-60 * Mathf.Deg2Rad), Mathf.Sin(-60 * Mathf.Deg2Rad)) * s * 0.3f;
                    p.fillColor = color;
                    p.BeginPath();
                    p.MoveTo(tip + new Vector2(s * 0.2f, -s * 0.06f)); p.LineTo(tip + new Vector2(-s * 0.1f, -s * 0.18f)); p.LineTo(tip + new Vector2(-0.02f * s, s * 0.14f)); p.ClosePath(); p.Fill();
                    break;
                }
                case Icon.Gear:
                {
                    p.fillColor = color;
                    p.BeginPath();
                    int teeth = 8;
                    for (int i = 0; i < teeth * 2; i++)
                    {
                        float a = Mathf.PI * i / teeth;
                        float r = (i % 2 == 0) ? s * 0.44f : s * 0.32f;
                        Vector2 v = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                        if (i == 0) p.MoveTo(v); else p.LineTo(v);
                    }
                    p.ClosePath(); p.Fill();
                    p.fillColor = outline.a > 0 ? outline : new Color(0, 0, 0, 0.35f);
                    p.BeginPath(); p.Arc(c, s * 0.14f, new Angle(0, AngleUnit.Degree), new Angle(360, AngleUnit.Degree)); p.ClosePath(); p.Fill();
                    break;
                }
                case Icon.Play:
                    p.fillColor = color; p.BeginPath();
                    p.MoveTo(new Vector2(c.x - s * 0.18f, c.y - s * 0.3f)); p.LineTo(new Vector2(c.x + s * 0.3f, c.y)); p.LineTo(new Vector2(c.x - s * 0.18f, c.y + s * 0.3f)); p.ClosePath(); p.Fill();
                    break;
                case Icon.Check:
                    p.strokeColor = color; p.lineWidth = s * 0.14f;
                    p.BeginPath(); p.MoveTo(new Vector2(c.x - s * 0.28f, c.y)); p.LineTo(new Vector2(c.x - s * 0.06f, c.y + s * 0.22f)); p.LineTo(new Vector2(c.x + s * 0.3f, c.y - s * 0.22f)); p.Stroke();
                    break;
                case Icon.Flag:
                    p.strokeColor = color; p.lineWidth = s * 0.08f;
                    p.BeginPath(); p.MoveTo(new Vector2(c.x - s * 0.16f, c.y + s * 0.4f)); p.LineTo(new Vector2(c.x - s * 0.16f, c.y - s * 0.4f)); p.Stroke();
                    p.fillColor = color; p.BeginPath();
                    p.MoveTo(new Vector2(c.x - s * 0.14f, c.y - s * 0.4f)); p.LineTo(new Vector2(c.x + s * 0.36f, c.y - s * 0.2f)); p.LineTo(new Vector2(c.x - s * 0.14f, c.y)); p.ClosePath(); p.Fill();
                    break;
                case Icon.Home:
                    p.fillColor = color; p.BeginPath();
                    p.MoveTo(new Vector2(c.x, c.y - s * 0.4f)); p.LineTo(new Vector2(c.x + s * 0.42f, c.y)); p.LineTo(new Vector2(c.x + s * 0.28f, c.y)); p.LineTo(new Vector2(c.x + s * 0.28f, c.y + s * 0.36f));
                    p.LineTo(new Vector2(c.x - s * 0.28f, c.y + s * 0.36f)); p.LineTo(new Vector2(c.x - s * 0.28f, c.y)); p.LineTo(new Vector2(c.x - s * 0.42f, c.y)); p.ClosePath(); p.Fill();
                    break;
            }
        }

        public static void DrawStar(Painter2D p, Vector2 c, float outer, float inner, Color fill, Color line, float lineW)
        {
            p.BeginPath();
            for (int i = 0; i < 10; i++)
            {
                float a = -Mathf.PI / 2f + Mathf.PI * i / 5f;
                float r = (i % 2 == 0) ? outer : inner;
                Vector2 v = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (i == 0) p.MoveTo(v); else p.LineTo(v);
            }
            p.ClosePath();
            p.fillColor = fill;
            p.Fill();
            if (line.a > 0f)
            {
                p.strokeColor = line; p.lineWidth = lineW; p.lineJoin = LineJoin.Round;
                p.Stroke();
            }
        }

        public static void RoundRect(Painter2D p, float x, float y, float w, float h, float r, Color fill)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x + r, y));
            p.LineTo(new Vector2(x + w - r, y)); p.ArcTo(new Vector2(x + w, y), new Vector2(x + w, y + r), r);
            p.LineTo(new Vector2(x + w, y + h - r)); p.ArcTo(new Vector2(x + w, y + h), new Vector2(x + w - r, y + h), r);
            p.LineTo(new Vector2(x + r, y + h)); p.ArcTo(new Vector2(x, y + h), new Vector2(x, y + h - r), r);
            p.LineTo(new Vector2(x, y + r)); p.ArcTo(new Vector2(x, y), new Vector2(x + r, y), r);
            p.ClosePath();
            p.fillColor = fill;
            p.Fill();
        }
    }

    /// <summary>Row of three stars (earned = gold, others dim).</summary>
    public sealed class StarRow : VisualElement
    {
        readonly IconElement[] stars = new IconElement[3];

        public StarRow(float size, float gap)
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            pickingMode = PickingMode.Ignore;
            for (int i = 0; i < 3; i++)
            {
                stars[i] = new IconElement(Icon.Star, size, Pal.StarOff);
                stars[i].style.marginLeft = stars[i].style.marginRight = gap * 0.5f;
                Add(stars[i]);
            }
        }

        public new IconElement this[int i] => stars[i];

        public void Set(int earned, Color? offColor = null)
        {
            Color off = offColor ?? Pal.StarOff;
            for (int i = 0; i < 3; i++)
                stars[i].Set(i < earned ? Pal.Gold : off, i < earned ? Pal.GoldDark : new Color(0, 0, 0, 0.15f));
        }
    }

    /// <summary>Vector trophy (five designs, one per championship).</summary>
    public sealed class TrophyElement : VisualElement
    {
        public int design;
        public bool silhouette;

        RenderTexture icon;

        public TrophyElement(int design, float size)
        {
            this.design = design;
            style.width = size; style.height = size;
            pickingMode = PickingMode.Ignore;
            icon = PocketPutt.Presentation.TrophyIcons.Get(design);
            if (icon != null)
            {
                style.backgroundImage = Background.FromRenderTexture(icon);
                style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            else generateVisualContent += Draw;
        }

        public void SetSilhouette(bool s)
        {
            silhouette = s;
            if (icon != null) style.unityBackgroundImageTintColor = s ? new Color(0.08f, 0.06f, 0.05f, 0.9f) : Color.white;
            MarkDirtyRepaint();
        }

        static readonly Color[] Main = { Pal.H("C98A4B"), Pal.H("C06A32"), Pal.H("C5D2DC"), Pal.H("8D98A5"), Pal.H("F5C242") };
        static readonly Color[] Dark = { Pal.H("8E5A2A"), Pal.H("8B431B"), Pal.H("8497A6"), Pal.H("5C6672"), Pal.H("C28A12") };

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float s = Mathf.Min(contentRect.width, contentRect.height);
            Vector2 o = new Vector2((contentRect.width - s) * 0.5f, (contentRect.height - s) * 0.5f);
            Func<float, float, Vector2> P = (x, y) => o + new Vector2(x * s, y * s);
            Color main = silhouette ? new Color(0, 0, 0, 0.28f) : Main[design];
            Color dark = silhouette ? new Color(0, 0, 0, 0.35f) : Dark[design];
            Color shine = silhouette ? new Color(0, 0, 0, 0) : new Color(1, 1, 1, 0.4f);
            p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;

            // handles
            p.strokeColor = dark; p.lineWidth = s * 0.045f;
            p.BeginPath(); p.MoveTo(P(0.24f, 0.18f)); p.BezierCurveTo(P(0.0f, 0.18f), P(0.02f, 0.5f), P(0.28f, 0.52f)); p.Stroke();
            p.BeginPath(); p.MoveTo(P(0.76f, 0.18f)); p.BezierCurveTo(P(1.0f, 0.18f), P(0.98f, 0.5f), P(0.72f, 0.52f)); p.Stroke();

            // base
            IconElement.RoundRect(p, o.x + 0.28f * s, o.y + 0.86f * s, 0.44f * s, 0.1f * s, s * 0.03f, dark);
            IconElement.RoundRect(p, o.x + 0.34f * s, o.y + 0.77f * s, 0.32f * s, 0.1f * s, s * 0.03f, main);
            // stem
            p.fillColor = main; p.BeginPath();
            p.MoveTo(P(0.44f, 0.6f)); p.LineTo(P(0.56f, 0.6f)); p.LineTo(P(0.6f, 0.78f)); p.LineTo(P(0.4f, 0.78f)); p.ClosePath(); p.Fill();
            // bowl
            p.fillColor = main; p.BeginPath();
            p.MoveTo(P(0.22f, 0.12f)); p.LineTo(P(0.78f, 0.12f));
            p.BezierCurveTo(P(0.78f, 0.45f), P(0.68f, 0.62f), P(0.5f, 0.64f));
            p.BezierCurveTo(P(0.32f, 0.62f), P(0.22f, 0.45f), P(0.22f, 0.12f)); p.ClosePath(); p.Fill();
            // rim
            IconElement.RoundRect(p, o.x + 0.18f * s, o.y + 0.08f * s, 0.64f * s, 0.07f * s, s * 0.03f, dark);
            // shine
            p.fillColor = shine; p.BeginPath();
            p.MoveTo(P(0.29f, 0.18f)); p.LineTo(P(0.37f, 0.18f)); p.BezierCurveTo(P(0.37f, 0.4f), P(0.4f, 0.5f), P(0.43f, 0.54f)); p.BezierCurveTo(P(0.33f, 0.46f), P(0.29f, 0.34f), P(0.29f, 0.18f)); p.ClosePath(); p.Fill();

            if (silhouette) return;
            // emblem
            Vector2 e = P(0.5f, 0.35f);
            switch (design)
            {
                case 0: IconElement.DrawStar(p, e, s * 0.1f, s * 0.045f, Cream, dark, s * 0.01f); break;
                case 1: IconElement.DrawStar(p, e, s * 0.11f, s * 0.05f, Cream, dark, s * 0.01f); IconElement.DrawStar(p, P(0.5f, 0.5f), s * 0.05f, s * 0.022f, Cream, dark, 0); break;
                case 2: p.fillColor = Pal.H("5BB5E6"); p.BeginPath(); p.MoveTo(e + new Vector2(0, -s * 0.12f)); p.LineTo(e + new Vector2(s * 0.09f, 0)); p.LineTo(e + new Vector2(0, s * 0.12f)); p.LineTo(e + new Vector2(-s * 0.09f, 0)); p.ClosePath(); p.Fill(); break;
                case 3: p.fillColor = Pal.H("F0B93A"); p.BeginPath(); for (int i = 0; i < 12; i++) { float a = Mathf.PI * i / 6f; float r = (i % 2 == 0) ? s * 0.11f : s * 0.08f; Vector2 v = e + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r; if (i == 0) p.MoveTo(v); else p.LineTo(v); } p.ClosePath(); p.Fill(); break;
                default: IconElement.DrawStar(p, e, s * 0.13f, s * 0.06f, Cream, dark, s * 0.012f); break;
            }
            if (design == 4)
            {
                IconElement.DrawStar(p, P(0.5f, 0.02f), s * 0.07f, s * 0.03f, Pal.Cream, dark, s * 0.008f);
            }
        }

        static readonly Color Cream = Pal.H("FFF6DF");
    }

    /// <summary>Layout helpers so screens read like markup.</summary>
    public static class Ui
    {
        public static VisualElement Box(float? w = null, float? h = null)
        {
            var v = new VisualElement();
            if (w.HasValue) v.style.width = w.Value;
            if (h.HasValue) v.style.height = h.Value;
            return v;
        }

        public static VisualElement Fill(VisualElement v)
        {
            v.style.position = Position.Absolute;
            v.style.left = 0; v.style.right = 0; v.style.top = 0; v.style.bottom = 0;
            return v;
        }

        public static Label Text(string s, float size, Color color, bool bold = true, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var l = new Label(s);
            l.style.fontSize = size;
            l.style.color = color;
            l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            l.style.unityTextAlign = align;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.pickingMode = PickingMode.Ignore;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            return l;
        }

        public static void Radius(VisualElement v, float r)
        {
            v.style.borderTopLeftRadius = r; v.style.borderTopRightRadius = r;
            v.style.borderBottomLeftRadius = r; v.style.borderBottomRightRadius = r;
        }

        public static void Border(VisualElement v, float w, Color c)
        {
            v.style.borderTopWidth = w; v.style.borderBottomWidth = w; v.style.borderLeftWidth = w; v.style.borderRightWidth = w;
            v.style.borderTopColor = c; v.style.borderBottomColor = c; v.style.borderLeftColor = c; v.style.borderRightColor = c;
        }

        public static void Pad(VisualElement v, float all) => Pad(v, all, all, all, all);
        public static void Pad(VisualElement v, float l, float t, float r, float b)
        {
            v.style.paddingLeft = l; v.style.paddingTop = t; v.style.paddingRight = r; v.style.paddingBottom = b;
        }

        public static void Center(VisualElement v)
        {
            v.style.alignItems = Align.Center;
            v.style.justifyContent = Justify.Center;
        }

        public static void Press(VisualElement v, Action onClick, Action<VisualElement> sound = null)
        {
            bool down = false;
            v.RegisterCallback<PointerDownEvent>(e => { down = true; v.style.scale = new Scale(new Vector3(0.95f, 0.95f, 1f)); }, TrickleDown.NoTrickleDown);
            v.RegisterCallback<PointerUpEvent>(e =>
            {
                v.style.scale = new Scale(Vector3.one);
                if (down) { down = false; Audio.Sfx.I?.Click(); onClick?.Invoke(); }
            });
            v.RegisterCallback<PointerLeaveEvent>(e => { down = false; v.style.scale = new Scale(Vector3.one); });
            v.RegisterCallback<PointerCancelEvent>(e => { down = false; v.style.scale = new Scale(Vector3.one); });
        }

        /// <summary>Chunky game button with a darker bottom lip.</summary>
        public static VisualElement Button(string text, Color face, Color lip, Color textColor, float width, float height, float fontSize, Action onClick)
        {
            var b = new VisualElement();
            b.style.width = width; b.style.height = height;
            b.style.backgroundColor = face;
            Radius(b, height * 0.3f);
            b.style.borderBottomWidth = height * 0.09f;
            b.style.borderBottomColor = lip;
            Center(b);
            b.style.flexDirection = FlexDirection.Row;
            if (!string.IsNullOrEmpty(text))
            {
                var l = Text(text, fontSize, textColor);
                b.Add(l);
            }
            Press(b, onClick);
            b.name = "btn_" + text;
            return b;
        }

        public static VisualElement IconButton(Icon icon, float size, Color face, Color lip, Color glyph, Action onClick)
        {
            var b = new VisualElement();
            b.style.width = size; b.style.height = size;
            b.style.backgroundColor = face;
            Radius(b, size * 0.32f);
            b.style.borderBottomWidth = size * 0.07f;
            b.style.borderBottomColor = lip;
            Center(b);
            b.Add(new IconElement(icon, size * 0.62f, glyph));
            Press(b, onClick);
            return b;
        }

        public static void Fade(VisualElement v, float from, float to, int ms, Action done = null)
        {
            v.style.opacity = from;
            v.experimental.animation.Start(from, to, ms, (e, val) => e.style.opacity = val).OnCompleted(() => done?.Invoke());
        }

        public static void Pop(VisualElement v, int delayMs, int ms = 320)
        {
            v.style.scale = new Scale(new Vector3(0.01f, 0.01f, 1f));
            v.schedule.Execute(() =>
                v.experimental.animation.Start(0f, 1f, ms, (e, t) =>
                {
                    float s = Overshoot(t);
                    e.style.scale = new Scale(new Vector3(s, s, 1f));
                })).ExecuteLater(delayMs);
        }

        static float Overshoot(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
