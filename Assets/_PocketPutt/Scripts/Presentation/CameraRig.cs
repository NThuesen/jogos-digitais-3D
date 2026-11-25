using PocketPutt.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketPutt.Presentation
{
    /// <summary>Orthographic near-top-down camera that frames a whole course in portrait, leaving room for the HUD.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        // 64 deg: the A/B captures (56-76) showed this gives rail volume and an upright flag while the field still fits; -pp-pitch overrides for comparisons.
        public static readonly float Pitch = ResolvePitch();

        static float ResolvePitch()
        {
            string dbg = PocketPutt.Game.GameApp.CommandLineValue("-pp-pitch");
            return !string.IsNullOrEmpty(dbg) ? float.Parse(dbg, System.Globalization.CultureInfo.InvariantCulture) : 64f;
        }

        Camera cam;
        Rect bounds;
        bool framed;
        float topInset = 0.12f, bottomInset = 0.04f;
        int lastW, lastH;
        Vector3 shake;
        float shakeT;

        public Camera Cam => cam != null ? cam : (cam = GetComponent<Camera>());

        public void Setup()
        {
            Cam.orthographic = true;
            Cam.nearClipPlane = 0.1f;
            Cam.farClipPlane = 80f;
            transform.rotation = Quaternion.Euler(Pitch, 0, 0);
        }

        public void Frame(Rect courseBounds, float top = 0.12f, float bottom = 0.05f, bool instant = false)
        {
            // A new course snaps into place; re-framing the same course (e.g. for the result sheet) glides.
            instant |= !framed || bounds != courseBounds;
            bounds = courseBounds;
            topInset = top;
            bottomInset = bottom;
            framed = true;
            snap = instant;
            Refit();
        }

        bool snap = true;
        float curSize;
        Vector3 curPos;
        float targetSize;
        Vector3 targetPos;

        public void Kick(float strength)
        {
            shakeT = 0.18f;
            shake = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized * strength;
        }

        void LateUpdate()
        {
            if (framed && (Screen.width != lastW || Screen.height != lastH)) { snap = true; Refit(); }
            if (framed && !snap)
            {
                float k = 1f - Mathf.Exp(-Time.deltaTime * 7f);
                curSize = Mathf.Lerp(curSize, targetSize, k);
                curPos = Vector3.Lerp(curPos, targetPos, k);
                Cam.orthographicSize = curSize;
                basePos = curPos;
            }
            if (shakeT > 0f)
            {
                shakeT -= Time.deltaTime;
                float k = Mathf.Max(0, shakeT / 0.18f);
                transform.position = basePos + shake * (k * k);
            }
            else transform.position = basePos;
        }

        Vector3 basePos;

        void Refit()
        {
            lastW = Screen.width; lastH = Screen.height;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Cam.orthographic = true;
            transform.rotation = Quaternion.Euler(Pitch, 0, 0);
            Vector3 center = new Vector3(bounds.center.x, 0, bounds.center.y);
            Vector3 pos0 = center - transform.forward * 18f;
            Vector3 right = transform.right, up = transform.up;

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            const float pad = 0.55f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = new Vector3(i % 2 == 0 ? bounds.xMin - pad : bounds.xMax + pad, (i / 4) * 0.4f,
                    ((i / 2) % 2 == 0) ? bounds.yMin - pad : bounds.yMax + pad);
                Vector3 d = p - pos0;
                float x = Vector3.Dot(d, right), y = Vector3.Dot(d, up);
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
            float aspect = (float)Screen.width / Screen.height;
            float freeFrac = Mathf.Max(0.3f, 1f - topInset - bottomInset);
            float sizeH = (maxY - minY) * 0.5f / freeFrac;
            float sizeW = (maxX - minX) * 0.5f / aspect;
            float size = Mathf.Max(3.2f, Mathf.Max(sizeH, sizeW));
            float cx = (minX + maxX) * 0.5f, cy = (minY + maxY) * 0.5f;
            float regionCenter = (bottomInset - topInset); // in units of half-screen heights (2*dy_frac)
            targetSize = size;
            targetPos = pos0 + right * cx + up * (cy - regionCenter * size);
            if (snap || curSize <= 0f)
            {
                curSize = targetSize; curPos = targetPos;
                Cam.orthographicSize = curSize;
                basePos = curPos;
                transform.position = basePos;
            }
        }
    }

    /// <summary>Lighting and backdrop for a theme.</summary>
    public static class StageRig
    {
        static Light key;

        public static void Apply(Camera cam, Theme theme)
        {
            ThemePalette pal = ArtFactory.Palette(theme);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = pal.ground;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(pal.sky, Color.white, 0.45f) * 0.5f;
            RenderSettings.fog = false;

            if (key == null)
            {
                var go = new GameObject("Key Light");
                key = go.AddComponent<Light>();
                key.type = LightType.Directional;
            }
            key.color = theme == Theme.Glacier ? new Color(0.9f, 0.96f, 1f) : theme == Theme.Arena ? new Color(1f, 0.95f, 0.85f) : new Color(1f, 0.95f, 0.85f);
            key.intensity = 1.0f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.75f;
            key.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
            string dbg = PocketPutt.Game.GameApp.CommandLineValue("-pp-ambient");
            string dbgKey = PocketPutt.Game.GameApp.CommandLineValue("-pp-key");
            if (!string.IsNullOrEmpty(dbgKey)) key.intensity = float.Parse(dbgKey, System.Globalization.CultureInfo.InvariantCulture);
            Debug.Log($"[PocketPutt] light intensity={key.intensity} ambient={RenderSettings.ambientLight} dbg={dbg} dbgKey={dbgKey} lights={UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length}");
            if (!string.IsNullOrEmpty(dbg)) RenderSettings.ambientLight = Color.white * float.Parse(dbg, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
