#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace SlipperyZlope.RingToss.EditorTools
{
    /// <summary>
    /// Adds the permissions the game asks for at runtime to the generated manifest.
    ///
    /// Unity writes the Android manifest itself and infers permissions from the engine
    /// APIs a build uses. It cannot infer this one: the haptics go to the platform
    /// Vibrator over JNI rather than through Handheld.Vibrate, so nothing in the build
    /// declares the dependency and VIBRATE is silently left out. The pulses then throw
    /// a SecurityException on every impact.
    ///
    /// Injecting it here rather than shipping a hand-written
    /// Assets/Plugins/Android/AndroidManifest.xml is deliberate. That file *replaces*
    /// Unity's generated manifest rather than merging into it, which means owning the
    /// activity, the intent filters and the meta-data by hand forever, and re-checking
    /// them against every editor upgrade. This adds one node and leaves the rest to
    /// Unity.
    /// </summary>
    public sealed class AndroidManifestPermissions : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";

        private static readonly string[] RequiredPermissions =
        {
            // Short impact pulses. See SlipperyZlope.RingToss.Mobile.Haptics.
            "android.permission.VIBRATE"
        };

        // After Unity's own manifest work, before the manifest merger runs.
        public int callbackOrder => 1;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");

            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"[Android] No manifest at '{manifestPath}'; permissions not added.");
                return;
            }

            XmlDocument document = new XmlDocument();
            document.Load(manifestPath);

            XmlElement root = document.DocumentElement;

            if (root == null)
            {
                Debug.LogWarning("[Android] Manifest has no root element; permissions not added.");
                return;
            }

            bool changed = false;

            foreach (string permission in RequiredPermissions)
            {
                if (HasPermission(root, permission))
                {
                    continue;
                }

                XmlElement node = document.CreateElement("uses-permission");
                node.SetAttribute("name", AndroidNamespace, permission);
                root.AppendChild(node);
                changed = true;

                Debug.Log($"[Android] Added {permission} to the manifest.");
            }

            if (!changed)
            {
                return;
            }

            document.Save(manifestPath);
        }

        private static bool HasPermission(XmlElement root, string permission)
        {
            foreach (XmlNode node in root.SelectNodes("uses-permission"))
            {
                if (node is XmlElement element
                    && element.GetAttribute("name", AndroidNamespace) == permission)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
