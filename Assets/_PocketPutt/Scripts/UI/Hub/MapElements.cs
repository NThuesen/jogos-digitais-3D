using PocketPutt.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace PocketPutt.UI
{
    static class HubDraw
    {
        public static void Circle(Painter2D p, Vector2 c, float r)
        {
            p.BeginPath();
            p.Arc(c, r, new Angle(0, AngleUnit.Degree), new Angle(360, AngleUnit.Degree));
            p.ClosePath();
        }

        public static void Ellipse(Painter2D p, Vector2 c, float rx, float ry, int seg = 36)
        {
            p.BeginPath();
            for (int i = 0; i < seg; i++)
            {
                float a = Mathf.PI * 2f * i / seg;
                Vector2 q = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                if (i == 0) p.MoveTo(q); else p.LineTo(q);
            }
            p.ClosePath();
        }

        /// <summary>Seamless vertical gradient as one coloured quad (strips of fills show hairline seams between them).</summary>
        public static void Gradient(MeshGenerationContext ctx, Rect r, Color top, Color bottom)
        {
            MeshWriteData mesh = ctx.Allocate(4, 6);
            var v = new Vertex[4];
            Color32 t = top, b = bottom;
            v[0].position = new Vector3(r.xMin, r.yMin, Vertex.nearZ); v[0].tint = t;
            v[1].position = new Vector3(r.xMax, r.yMin, Vertex.nearZ); v[1].tint = t;
            v[2].position = new Vector3(r.xMax, r.yMax, Vertex.nearZ); v[2].tint = b;
            v[3].position = new Vector3(r.xMin, r.yMax, Vertex.nearZ); v[3].tint = b;
            mesh.SetAllVertices(v);
            mesh.SetAllIndices(new ushort[] { 0, 1, 2, 2, 3, 0 });
        }

        public static Color Shade(Color c, float f) => new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), c.a);
    }

    /// <summary>Vertical alpha/colour gradient drawn as a single mesh (no seams).</summary>
    public sealed class HubGradient : VisualElement
    {
        readonly Color top, bottom;
        public HubGradient(Color top, Color bottom)
        {
            this.top = top; this.bottom = bottom;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += ctx => HubDraw.Gradient(ctx, contentRect, this.top, this.bottom);
        }
    }

    /// <summary>
    /// One level marker standing on its pad in the map art: a chunky coin with state colour, number, lock/stars and a selection halo.
    /// Purely visual (pointer events go to the scroller, which hit-tests the node centres).
    /// </summary>
    public sealed class MapNodeElement : VisualElement
    {
        public const float Diameter = 140f, FinalDiameter = 196f, Margin = 34f;

        public readonly string levelId;
        public readonly int index;
        public readonly bool isFinal;
        public NodeVisual visual;
        public bool selected;
        public bool trophyWon;
        public float pulse;                 // 0..1, driven by the hub for the suggested node only
        Color accent = Pal.Green, accentDark = Pal.GreenDark;
        readonly Label number;
        readonly IconElement lockIcon;
        readonly StarRow stars;
        readonly TrophyElement trophy;
        readonly VisualElement ribbon;
        readonly Label ribbonText;
        readonly IconElement ribbonLock;

        public float Radius => (isFinal ? FinalDiameter : Diameter) * 0.5f;
        public float HitRadius => Radius + (isFinal ? 18f : 14f);

        public MapNodeElement(string levelId, int index, bool isFinal, int cupIndex)
        {
            this.levelId = levelId; this.index = index; this.isFinal = isFinal;
            float d = (isFinal ? FinalDiameter : Diameter) + Margin * 2f;
            style.position = Position.Absolute;
            style.width = d; style.height = d;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;

            number = Ui.Text(isFinal ? "" : (index + 1).ToString(), 70, Color.white);
            Ui.Fill(number);
            number.style.top = -4;
            Add(number);
            lockIcon = new IconElement(Icon.Lock, 42, Pal.A(Pal.Cream, 0.9f));
            lockIcon.style.position = Position.Absolute;
            lockIcon.style.left = d * 0.5f - 21f; lockIcon.style.top = d * 0.5f + 14f;
            Add(lockIcon);

            if (isFinal)
            {
                trophy = new TrophyElement(cupIndex, 150);
                trophy.style.position = Position.Absolute;
                trophy.style.left = d * 0.5f - 75f; trophy.style.top = d * 0.5f - 96f;
                Add(trophy);
                ribbon = new VisualElement();
                ribbon.style.position = Position.Absolute;
                ribbon.style.flexDirection = FlexDirection.Row;
                ribbon.style.alignItems = Align.Center;
                ribbon.style.justifyContent = Justify.Center;
                ribbon.style.backgroundColor = Pal.A(Pal.Deep, 0.92f);
                Ui.Radius(ribbon, 34);
                Ui.Border(ribbon, 3, Pal.A(Pal.Gold, 0.85f));
                Ui.Pad(ribbon, 26, 6, 26, 6);
                ribbon.pickingMode = PickingMode.Ignore;
                ribbonLock = new IconElement(Icon.Lock, 40, Pal.A(Pal.Cream, 0.9f));
                ribbonLock.style.marginRight = 8;
                ribbon.Add(ribbonLock);
                ribbonText = Ui.Text(Loc.T("cup.final"), 36, Pal.Cream);
                ribbonText.style.whiteSpace = WhiteSpace.NoWrap;
                ribbon.Add(ribbonText);
                Add(ribbon);
            }
            else
            {
                stars = new StarRow(40, 4);
                stars.style.position = Position.Absolute;
                stars.style.left = 0; stars.style.right = 0;
                stars.style.top = d * 0.5f + Radius + 6f;
                stars.style.backgroundColor = Pal.A(Pal.Deep, 0.0f);
                Add(stars);
            }
        }

        public void Bind(ThemeColors tc, int starCount, bool won)
        {
            accent = tc.accent; accentDark = tc.accentDark;
            trophyWon = won;
            number.style.color = visual == NodeVisual.Locked ? Pal.A(Pal.Cream, 0.8f) : visual == NodeVisual.Completed ? Pal.Cream : Pal.GreenDark;
            if (!isFinal)
            {
                number.style.fontSize = visual == NodeVisual.Locked ? 58 : 70;
                number.style.top = visual == NodeVisual.Locked ? -22 : -4;
            }
            lockIcon.style.display = visual == NodeVisual.Locked && !isFinal ? DisplayStyle.Flex : DisplayStyle.None;
            if (stars != null)
            {
                bool show = visual == NodeVisual.Completed;
                stars.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (show) stars.Set(starCount, Pal.A(Color.black, 0.45f));
            }
            if (isFinal)
            {
                trophy.SetSilhouette(!won);
                ribbonLock.style.display = visual == NodeVisual.Locked ? DisplayStyle.Flex : DisplayStyle.None;
                ribbon.style.left = style.width.value.value * 0.5f;
                ribbon.style.translate = new Translate(Length.Percent(-50), 0);
                ribbon.style.top = style.height.value.value * 0.5f + Radius + 6f;
                ribbon.style.backgroundColor = Pal.A(visual == NodeVisual.Locked ? Pal.Deep : Pal.GoldDark, 0.94f);
                if (visual == NodeVisual.Completed && won)
                {
                    stars?.Set(starCount);
                }
            }
            style.scale = new Scale(selected ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one);
            MarkDirtyRepaint();
        }

        public void SetSelected(bool s)
        {
            selected = s;
            style.scale = new Scale(s ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one);
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            float w = contentRect.width;
            Vector2 c = new Vector2(w * 0.5f, contentRect.height * 0.5f);
            float R = Radius;
            Color face, rim, side;
            switch (visual)
            {
                case NodeVisual.Locked: face = Pal.H("8D897E"); rim = Pal.H("6C685F"); side = Pal.H("55524B"); break;
                case NodeVisual.Completed: face = Pal.Green; rim = Pal.GreenDark; side = Pal.H("1F5B33"); break;
                case NodeVisual.Next: face = Pal.Cream; rim = Pal.Gold; side = Pal.GoldDark; break;
                default: face = Pal.Cream; rim = accent; side = accentDark; break;
            }
            if (isFinal)
            {
                if (visual == NodeVisual.Locked) { face = Pal.H("A9A397"); rim = Pal.H("7B766B"); side = Pal.H("5C5850"); }
                else { face = trophyWon ? Pal.H("FFE9A8") : Pal.Cream; rim = Pal.Gold; side = Pal.GoldDark; }
            }

            // contact shadow on the pad
            p.fillColor = new Color(0.05f, 0.1f, 0.05f, 0.30f);
            HubDraw.Ellipse(p, c + new Vector2(0, R * 0.78f), R * 1.04f, R * 0.40f);
            p.Fill();

            if (selected)
            {
                p.strokeColor = new Color(1f, 1f, 1f, 0.95f); p.lineWidth = 9;
                HubDraw.Circle(p, c, R + 16f); p.Stroke();
                p.strokeColor = new Color(Pal.Gold.r, Pal.Gold.g, Pal.Gold.b, 0.9f); p.lineWidth = 5;
                HubDraw.Circle(p, c, R + 26f); p.Stroke();
            }
            if (visual == NodeVisual.Next && pulse > 0f)
            {
                p.strokeColor = new Color(Pal.Gold.r, Pal.Gold.g, Pal.Gold.b, (1f - pulse) * 0.7f);
                p.lineWidth = 8;
                HubDraw.Circle(p, c, R + 6f + pulse * 22f); p.Stroke();
            }

            // coin: thickness, face, rim, highlight
            p.fillColor = side; HubDraw.Circle(p, c + new Vector2(0, 11f), R); p.Fill();
            p.fillColor = rim; HubDraw.Circle(p, c, R); p.Fill();
            p.fillColor = face; HubDraw.Circle(p, c, R - 11f); p.Fill();
            p.strokeColor = new Color(1f, 1f, 1f, visual == NodeVisual.Locked ? 0.10f : 0.38f); p.lineWidth = 6; p.lineCap = LineCap.Round;
            p.BeginPath(); p.Arc(c, R - 18f, new Angle(200, AngleUnit.Degree), new Angle(262, AngleUnit.Degree)); p.Stroke();

            if (visual == NodeVisual.Next)
            {
                // small play badge so "next" does not rely on the gold ring alone
                Vector2 b = c + new Vector2(R * 0.74f, -R * 0.74f);
                p.fillColor = Pal.Green; HubDraw.Circle(p, b, 25f); p.Fill();
                p.strokeColor = Color.white; p.lineWidth = 4; HubDraw.Circle(p, b, 25f); p.Stroke();
                p.fillColor = Color.white;
                p.BeginPath(); p.MoveTo(b + new Vector2(-6, -11)); p.LineTo(b + new Vector2(12, 0)); p.LineTo(b + new Vector2(-6, 11)); p.ClosePath(); p.Fill();
            }
            else if (visual == NodeVisual.Completed && !isFinal)
            {
                Vector2 b = c + new Vector2(R * 0.74f, -R * 0.74f);
                p.fillColor = Pal.Gold; HubDraw.Circle(p, b, 22f); p.Fill();
                p.strokeColor = Pal.Ink; p.lineWidth = 6; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
                p.BeginPath(); p.MoveTo(b + new Vector2(-9, 1)); p.LineTo(b + new Vector2(-3, 8)); p.LineTo(b + new Vector2(10, -7)); p.Stroke();
            }
        }
    }

    /// <summary>Route overlay: the walked stretch is lit (warm dotted line), the unreached stretch stays visible but quiet.</summary>
    public sealed class MapPathElement : VisualElement
    {
        Vector2[] path = System.Array.Empty<Vector2>();
        int litUpTo = -1;
        float aspect = 3.2f;

        public MapPathElement()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            generateVisualContent += Draw;
        }

        public void Set(Vector2[] polyline, int lit, float aspectRatio)
        {
            path = polyline; litUpTo = lit; aspect = aspectRatio;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            if (path.Length < 2) return;
            var p = ctx.painter2D;
            float w = contentRect.width, h = contentRect.height;
            p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;

            // walk the polyline laying dashes of constant length
            float dash = 16f, gap = 24f, acc = 0f;
            bool on = true;
            Vector2 prev = new Vector2(path[0].x * w, path[0].y * h);
            for (int i = 1; i < path.Length; i++)
            {
                Vector2 cur = new Vector2(path[i].x * w, path[i].y * h);
                Vector2 seg = cur - prev;
                float len = seg.magnitude;
                if (len < 1e-3f) continue;
                Vector2 dir = seg / len;
                float pos = 0f;
                bool lit = i <= litUpTo;
                while (pos < len)
                {
                    float room = (on ? dash : gap) - acc;
                    float step = Mathf.Min(room, len - pos);
                    if (on)
                    {
                        Vector2 a = prev + dir * pos, b = prev + dir * (pos + step);
                        if (lit)
                        {
                            p.strokeColor = new Color(0.25f, 0.13f, 0.02f, 0.45f); p.lineWidth = 17;
                            p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
                            p.strokeColor = Pal.Gold; p.lineWidth = 10;
                            p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
                        }
                        else
                        {
                            p.strokeColor = new Color(0.22f, 0.22f, 0.2f, 0.30f); p.lineWidth = 7;
                            p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
                        }
                    }
                    acc += step; pos += step;
                    if (acc >= (on ? dash : gap) - 1e-3f) { on = !on; acc = 0f; }
                }
                prev = cur;
            }
        }
    }

    /// <summary>
    /// Procedural stand-in for the map art of cups whose themed diorama does not exist yet (H3 after creator approval of the Garden).
    /// Deliberately plain so nobody mistakes it for final art.
    /// </summary>
    public sealed class PlaceholderMapArt : VisualElement
    {
        readonly Color top, bottom, ribbon;
        Vector2[] path = System.Array.Empty<Vector2>();

        public PlaceholderMapArt(ThemeColors tc)
        {
            bottom = tc.bg; top = Color.Lerp(tc.bg, Color.white, 0.18f); ribbon = Color.Lerp(tc.bg, Pal.Cream, 0.55f);
            pickingMode = PickingMode.Ignore;
            Ui.Fill(this);
            generateVisualContent += Draw;
        }

        public void SetPath(Vector2[] p) { path = p; MarkDirtyRepaint(); }

        void Draw(MeshGenerationContext ctx)
        {
            var q = ctx.painter2D;
            float w = contentRect.width, h = contentRect.height;
            HubDraw.Gradient(ctx, new Rect(0, 0, w, h), top, bottom);
            if (path.Length < 2) return;
            q.lineCap = LineCap.Round; q.lineJoin = LineJoin.Round;
            q.strokeColor = new Color(0f, 0f, 0f, 0.18f); q.lineWidth = 104;
            q.BeginPath(); q.MoveTo(new Vector2(path[0].x * w, path[0].y * h + 8f));
            for (int i = 1; i < path.Length; i++) q.LineTo(new Vector2(path[i].x * w, path[i].y * h + 8f));
            q.Stroke();
            q.strokeColor = ribbon; q.lineWidth = 96;
            q.BeginPath(); q.MoveTo(new Vector2(path[0].x * w, path[0].y * h));
            for (int i = 1; i < path.Length; i++) q.LineTo(new Vector2(path[i].x * w, path[i].y * h));
            q.Stroke();
        }
    }
}
