using UnityEngine;

namespace SlipperyZlope.RingToss.UI
{
    /// <summary>
    /// Keeps a rect inside the part of the screen the hardware is not using.
    ///
    /// A portrait canvas is stretched over the whole display, and on a modern phone
    /// the whole display includes a camera cutout, a status bar, rounded corners and a
    /// gesture bar. Anything anchored to an edge lands in one of them. The level name
    /// sat 2.5% from the top of this game, which is underneath the punch-hole on most
    /// handsets and underneath the notch on the rest, and the emulator has neither —
    /// so it looked correct everywhere except on a phone.
    ///
    /// Applied to the content of a screen rather than to the screen itself, because a
    /// dimming overlay behind a panel *should* run under the notch: insetting it
    /// leaves a bright band across the top of the display.
    ///
    /// Re-read every frame the safe area changes rather than once at start. It changes
    /// on rotation, on a foldable being unfolded, and on Android when the keyboard
    /// opens; a value cached in Awake is wrong in all three cases and the wrongness
    /// only shows up on a device.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        private RectTransform rect;
        private Rect applied;
        private Vector2Int resolution;

        private void Awake()
        {
            rect = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != applied ||
                Screen.width != resolution.x ||
                Screen.height != resolution.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            applied = Screen.safeArea;
            resolution = new Vector2Int(Screen.width, Screen.height);

            if (resolution.x <= 0 || resolution.y <= 0)
            {
                return;
            }

            // Anchors rather than offsets, so the rect stays correct whatever the
            // canvas scaler resolves the scale factor to. Offsets would need the
            // scale factor and would be wrong the frame it changed.
            Vector2 min = applied.position;
            Vector2 max = applied.position + applied.size;

            min.x /= resolution.x;
            min.y /= resolution.y;
            max.x /= resolution.x;
            max.y /= resolution.y;

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
