using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PocketPutt.UI
{
    /// <summary>
    /// Vertical one-finger scroller for the hub. Content never receives pointer events: a press that stays under the drag
    /// threshold is reported as a tap in content coordinates, so scrolling can never fire a level and a level tap never scrolls.
    /// </summary>
    public sealed class DragScroller : VisualElement
    {
        public readonly VisualElement content = new VisualElement();
        public event Action<Vector2> Tapped;          // content-space position
        public event Action Scrolled;
        /// <summary>Movement (logical px) that turns a press into a scroll. 8 dp is ~21 px at the 1080 px reference width.</summary>
        public float DragThreshold = 21f;

        public float ScrollY { get; private set; }
        public bool Dragging { get; private set; }
        public float MaxScroll => Mathf.Max(0f, content.layout.height - layout.height);

        bool pressed;
        int pointerId = -1;
        Vector2 start, last;
        float startScroll, velocity, lastTime;
        bool animating;
        float animFrom, animTo, animT, animDur;

        public DragScroller()
        {
            style.overflow = Overflow.Hidden;
            pickingMode = PickingMode.Position;
            content.style.position = Position.Absolute;
            content.style.left = 0; content.style.top = 0; content.style.right = 0;
            content.pickingMode = PickingMode.Ignore;
            Add(content);
            RegisterCallback<PointerDownEvent>(OnDown);
            RegisterCallback<PointerMoveEvent>(OnMove);
            RegisterCallback<PointerUpEvent>(OnUp);
            RegisterCallback<PointerCancelEvent>(e => Abort());
            RegisterCallback<PointerCaptureOutEvent>(e => Abort());
            RegisterCallback<WheelEvent>(e => { animating = false; velocity = 0f; SetScroll(ScrollY + e.delta.y * 90f); e.StopPropagation(); });
            RegisterCallback<GeometryChangedEvent>(e => SetScroll(ScrollY));
        }

        void OnDown(PointerDownEvent e)
        {
            if (pressed || e.button > 0) return;
            pressed = true; Dragging = false;
            pointerId = e.pointerId;
            start = last = e.localPosition;
            startScroll = ScrollY;
            velocity = 0f; animating = false;
            lastTime = Time.unscaledTime;
            this.CapturePointer(pointerId);
            e.StopPropagation();
        }

        void OnMove(PointerMoveEvent e)
        {
            if (!pressed || e.pointerId != pointerId) return;
            Vector2 p = e.localPosition;
            if (!Dragging)
            {
                if ((p - start).magnitude < DragThreshold) return;
                // start scrolling from here so the content does not jump by the threshold
                Dragging = true; start = p; startScroll = ScrollY; last = p; lastTime = Time.unscaledTime;
                return;
            }
            float before = ScrollY;
            SetScroll(startScroll - (p.y - start.y));
            float now = Time.unscaledTime, dt = Mathf.Max(now - lastTime, 1e-3f);
            velocity = Mathf.Lerp(velocity, (ScrollY - before) / dt, 0.4f);
            last = p; lastTime = now;
            e.StopPropagation();
        }

        void OnUp(PointerUpEvent e)
        {
            if (!pressed || e.pointerId != pointerId) return;
            Release();
            if (!Dragging)
            {
                Tapped?.Invoke(new Vector2(e.localPosition.x, e.localPosition.y + ScrollY));
            }
            else if (Time.unscaledTime - lastTime > 0.12f || Mathf.Abs(velocity) < 160f) velocity = 0f;
            Dragging = false;
            e.StopPropagation();
        }

        void Release()
        {
            pressed = false;
            if (pointerId >= 0 && this.HasPointerCapture(pointerId)) this.ReleasePointer(pointerId);
            pointerId = -1;
        }

        void Abort()
        {
            if (!pressed) return;
            pressed = false; Dragging = false; velocity = 0f; pointerId = -1;
        }

        public void SetScroll(float y)
        {
            float c = Mathf.Clamp(y, 0f, MaxScroll);
            if (Mathf.Abs(c - ScrollY) > 0.01f || content.style.translate.value.y.value != -c)
            {
                ScrollY = c;
                content.style.translate = new Translate(0, -c);
                Scrolled?.Invoke();
            }
            else ScrollY = c;
        }

        /// <summary>Eased scroll, used to focus a node (spec: at most ~450 ms, never forced after a plain tap).</summary>
        public void ScrollTo(float y, float seconds = 0.4f)
        {
            float t = Mathf.Clamp(y, 0f, MaxScroll);
            if (seconds <= 0f) { animating = false; SetScroll(t); return; }
            animating = true; animFrom = ScrollY; animTo = t; animT = 0f; animDur = seconds; velocity = 0f;
        }

        public void Tick(float dt)
        {
            if (pressed) return;
            if (animating)
            {
                animT += dt / animDur;
                float k = Mathf.Clamp01(animT);
                float e = 1f - Mathf.Pow(1f - k, 3f);
                SetScroll(Mathf.Lerp(animFrom, animTo, e));
                if (k >= 1f) animating = false;
                return;
            }
            if (Mathf.Abs(velocity) > 8f)
            {
                float before = ScrollY;
                SetScroll(ScrollY + velocity * dt);
                velocity *= Mathf.Exp(-4.2f * dt);
                if (Mathf.Approximately(before, ScrollY)) velocity = 0f;
            }
            else velocity = 0f;
        }

        public bool IsMoving => animating || Mathf.Abs(velocity) > 8f || Dragging;
    }
}
