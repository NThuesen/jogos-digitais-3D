using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketPutt.Presentation
{
    /// <summary>
    /// Renders each Blender trophy once into a transparent RenderTexture so UI Toolkit can show 3D-quality icons.
    /// Falls back to the vector trophy if the models are missing.
    /// </summary>
    public sealed class TrophyIcons : MonoBehaviour
    {
        public const int Count = 5;
        const int Size = 512;
        public static TrophyIcons I { get; private set; }
        readonly RenderTexture[] icons = new RenderTexture[Count];

        public static RenderTexture Get(int index) => I != null && index >= 0 && index < Count ? I.icons[index] : null;

        public static void Create(Transform parent)
        {
            if (!PropLibrary.HasTrophies) return;
            var go = new GameObject("Trophy Icons");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<TrophyIcons>();
            for (int i = 0; i < Count; i++)
            {
                I.icons[i] = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "TrophyIcon" + i };
                I.icons[i].Create();
            }
            I.StartCoroutine(I.RenderAll());
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            foreach (RenderTexture icon in icons) Destroy(icon);
        }

        IEnumerator RenderAll()
        {
            // Stage far below the playfield so gameplay cameras never see it.
            var stage = new GameObject("Trophy Stage").transform;
            stage.SetParent(transform, false);
            stage.position = new Vector3(0, -600, 0);

            var camGo = new GameObject("Icon Camera");
            camGo.transform.SetParent(stage, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.62f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 20f;
            camGo.transform.localPosition = new Vector3(0, 0.5f + 5f * Mathf.Tan(8f * Mathf.Deg2Rad), -5f);
            camGo.transform.localRotation = Quaternion.Euler(8f, 0, 0);
            cam.enabled = false;

            for (int i = 0; i < Count; i++)
            {
                GameObject t = PropLibrary.SpawnTrophy(i, stage);
                if (t == null) { Debug.LogWarning("[PocketPutt] trophy icon " + i + ": model missing"); continue; }
                cam.targetTexture = icons[i];
                cam.enabled = true;
                yield return null;
                yield return null;
                cam.enabled = false;
                yield return null;
                Destroy(t);
            }
            cam.targetTexture = null; // destroying a camera that still targets an RT can discard its contents
            Destroy(stage.gameObject);
        }
    }
}
