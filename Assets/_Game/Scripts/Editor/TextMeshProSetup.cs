using System.IO;
using UnityEditor;
using UnityEngine;

namespace SlipperyZlope.RingToss.EditorTools
{
    /// <summary>
    /// Imports TextMesh Pro's essential resources (default font asset, shaders and
    /// TMP Settings) into the project.
    ///
    /// TMP ships those as a .unitypackage rather than as package content, so a fresh
    /// clone has no TMP shaders at all and every label renders as magenta. Importing
    /// them is normally a one-time interactive dialog; this does it from code so a
    /// clean checkout can be brought to a buildable state headlessly.
    ///
    /// The import is asynchronous even in batch mode, so the batch entry point does
    /// not use -quit: it waits for the completion callback and exits from there.
    /// </summary>
    public static class TextMeshProSetup
    {
        private const string EssentialsPackagePath =
            "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        private const string ImportedMarkerPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        public static bool IsImported => File.Exists(ImportedMarkerPath);

        [MenuItem("SlipperyZlope/Import TextMesh Pro Essentials")]
        public static void Import()
        {
            if (IsImported)
            {
                Debug.Log("[TextMeshProSetup] Essentials already present.");
                return;
            }

            AssetDatabase.ImportPackage(EssentialsPackagePath, false);
        }

        /// <summary>Batch entry point. Exits the editor itself once the import lands.</summary>
        public static void ImportBatch()
        {
            if (IsImported)
            {
                Debug.Log("[TextMeshProSetup] Essentials already present.");
                EditorApplication.Exit(0);
                return;
            }

            AssetDatabase.importPackageCompleted += OnCompleted;
            AssetDatabase.importPackageFailed += OnFailed;
            AssetDatabase.importPackageCancelled += OnCancelled;

            AssetDatabase.ImportPackage(EssentialsPackagePath, false);
        }

        private static void OnCompleted(string packageName)
        {
            Debug.Log($"[TextMeshProSetup] Imported '{packageName}'. Marker present: {IsImported}");
            AssetDatabase.Refresh();
            EditorApplication.Exit(IsImported ? 0 : 1);
        }

        private static void OnFailed(string packageName, string error)
        {
            Debug.LogError($"[TextMeshProSetup] Import of '{packageName}' failed: {error}");
            EditorApplication.Exit(1);
        }

        private static void OnCancelled(string packageName)
        {
            Debug.LogError($"[TextMeshProSetup] Import of '{packageName}' was cancelled.");
            EditorApplication.Exit(1);
        }
    }
}
