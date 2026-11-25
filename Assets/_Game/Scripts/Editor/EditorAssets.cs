using UnityEditor;
using UnityEngine;

namespace SlipperyZlope.RingToss.EditorTools
{
    /// <summary>
    /// Shared asset-creation helpers for the bootstraps.
    ///
    /// Both rules encoded here were learned the hard way and both fail silently.
    /// </summary>
    public static class EditorAssets
    {
        /// <summary>
        /// Creates folders through the AssetDatabase rather than Directory.CreateDirectory
        /// plus AssetDatabase.Refresh.
        ///
        /// A Refresh inside an -executeMethod entry point compiles any pending script
        /// change and reloads the app domain in the middle of the running method,
        /// invalidating every object reference it is holding. Assignments then appear
        /// to succeed and quietly do nothing.
        /// </summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        /// <summary>
        /// Loads an asset, creating it if it does not exist.
        ///
        /// A newly created asset has to be written and re-imported before a scene
        /// component can hold a usable reference to it. Without the save and
        /// synchronous re-import, the very first run on a clean clone assigns a
        /// reference that does not survive, and the scene comes out subtly broken.
        /// </summary>
        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        /// <summary>Same contract as <see cref="LoadOrCreate{T}"/>, for plain Objects.</summary>
        public static T LoadOrCreateObject<T>(string path, System.Func<T> create) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset != null)
            {
                return asset;
            }

            asset = create();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        public static Material LoadOrCreateMaterial(string path, string shaderName, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find(shaderName);

                if (shader == null)
                {
                    Debug.LogError($"[EditorAssets] Shader '{shaderName}' not found for '{path}'.");
                    return null;
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                material = AssetDatabase.LoadAssetAtPath<Material>(path);
            }

            // URP shaders expose _BaseColor; the built-in pipeline's _Color that
            // Material.color writes to is not the same property.
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        public static Mesh SaveMesh(string path, Mesh mesh)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (existing != null)
            {
                // Overwrite in place so every reference to this mesh survives a rebuild.
                existing.Clear();
                existing.vertices = mesh.vertices;
                existing.normals = mesh.normals;
                existing.uv = mesh.uv;
                existing.triangles = mesh.triangles;
                existing.RecalculateBounds();

                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
    }
}
