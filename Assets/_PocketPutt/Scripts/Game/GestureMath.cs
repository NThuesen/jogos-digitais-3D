using UnityEngine;

namespace PocketPutt.Game
{
    /// <summary>
    /// Pure screen-space maths of the slingshot gesture (no Unity runtime state), shared by the runner, the
    /// on-screen guide and the unit checks. The mapping itself is unchanged: power = pull length / FullDragPixels.
    /// </summary>
    public static class GestureMath
    {
        public const float MinPower = 0.08f;       // below this a release cancels the shot
        public const float AnnouncePower = 0.06f;  // first meaningful drag (hides the intro tip)

        public static float FullDragPixels(float width, float height) => Mathf.Min(height, width * 1.9f) * 0.24f;

        public static float Power(Vector2 anchor, Vector2 pointer, float width, float height)
            => Mathf.Clamp01((anchor - pointer).magnitude / FullDragPixels(width, height));

        /// <summary>The pointer position that delivers exactly <paramref name="power"/> when the shot should go along screen direction shotDir.</summary>
        public static Vector2 PointerFor(Vector2 anchor, Vector2 shotDirScreen, float power, float width, float height)
            => anchor - shotDirScreen.normalized * power * FullDragPixels(width, height);

        public static bool Inside(Vector2 p, float width, float height, float margin = 0f)
            => p.x >= margin && p.x <= width - margin && p.y >= margin && p.y <= height - margin;

        /// <summary>
        /// True when the pointer is pressed against the physical screen edge while still short of full power:
        /// the finger cannot travel further, so the gesture has to be cancelled and restarted closer to the middle.
        /// </summary>
        public static bool BlockedByEdge(Vector2 pointer, float power, float width, float height, float edgePx)
            => power < 0.98f && !Inside(pointer, width, height, edgePx);

        /// <summary>Fraction (0..1) of the directions from which full power is reachable on screen when starting at <paramref name="anchor"/>.</summary>
        public static float ReachableFraction(Vector2 anchor, float width, float height, float margin, int samples = 72)
        {
            int ok = 0;
            for (int i = 0; i < samples; i++)
            {
                float a = Mathf.PI * 2f * i / samples;
                Vector2 p = PointerFor(anchor, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 1f, width, height);
                if (Inside(p, width, height, margin)) ok++;
            }
            return ok / (float)samples;
        }
    }
}
