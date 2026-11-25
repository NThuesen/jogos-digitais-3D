#if UNITY_EDITOR
using System;
using PocketPutt.Prototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PocketPutt.EditorTools
{
    /// <summary>
    /// Builds decorative C1-01 modular pieces from the authored Blender kit.
    /// Physics colliders, goal trigger and the launch controller remain owned
    /// by BuildPrototypeLevel and the original level JSON.
    /// </summary>
    internal static class BuildModularPresentation
    {
        private const string ModelFolder = "Assets/_PocketPutt/Art/Models/Modular/";
        private const string MaterialFolder = "Assets/_PocketPutt/Art/PresentationMaterials";
        private const float BallDiameter = 0.44f;
        private const float SizeTolerance = 0.17f;

        private enum VerticalAnchor { Top, Bottom, Center }

        private struct Appearance
        {
            public Material Turf, TurfStripe, Wood, WoodTop, Groove;
            public Material Brass, Dark, Pearl, Mark, Base, Rubber, Background;
        }

        private static readonly string[] RequiredAssets =
        {
            "PP_Kit_TurfTile.fbx",
            "PP_Kit_RailStraight.fbx",
            "PP_Kit_RailEndCap.fbx",
            "PP_Kit_RailCorner.fbx",
            "PP_Kit_DisplayBase.fbx",
            "PP_Kit_RubberFoot.fbx",
            "PP_Kit_CupRim.fbx",
            "PP_Kit_GolfBall.fbx"
        };

        internal static bool TryBuild(Transform levelRoot, Transform ball, PrototypeLevelDefinition level, Camera camera)
        {
            foreach (string file in RequiredAssets)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + file) == null)
                {
                    Debug.LogWarning($"[Pocket Putt] Modular art missing: {file}. Keeping the legacy visual.");
                    return false;
                }
            }

            Transform visualRoot = new GameObject("Visuals - Modular Kit (No Colliders)").transform;
            visualRoot.SetParent(levelRoot, false);
            Transform ballPivot = null;

            try
            {
                EnsureFolder(MaterialFolder);
                Appearance art = LoadAppearance();

                // The source JSON controls dimensions and positions, not the FBXs.
                LevelBlock floor = level.floors[0];
                float centerX = floor.x;
                float centerZ = floor.z;
                float left = centerX - floor.width * 0.5f - 0.09f;  // -1.64 for C1-01
                float right = centerX + floor.width * 0.5f + 0.09f;
                float endZ = floor.length * 0.5f + 0.12f;

                // Display base sits wholly below the existing playable floor.
                Place("PP_Kit_DisplayBase.fbx", "Display Base", visualRoot,
                    new Vector2(centerX, centerZ), 3.97f, 8.29f, VerticalAnchor.Top,
                    -0.055f, 0f, art.Base, art.Groove, art.Brass, art.WoodTop);

                for (int i = 0; i < 9; i++)
                {
                    float z = centerZ - floor.length * 0.5f + (i + 0.5f) * floor.length / 9f;
                    Place("PP_Kit_TurfTile.fbx", $"Turf Tile {i + 1:00}", visualRoot,
                        new Vector2(centerX, z), floor.width, 0.795f,
                        VerticalAnchor.Top, 0.003f, 0f, art.Turf,
                        (i % 2 == 0) ? art.TurfStripe : art.Turf);
                }

                for (int side = 0; side < 2; side++)
                {
                    float x = side == 0 ? left : right;
                    for (int i = 0; i < 7; i++)
                    {
                        float z = centerZ - 3.15f + i * 1.05f;
                        Place("PP_Kit_RailStraight.fbx", $"Side {side} Rail {i:00}",
                            visualRoot, new Vector2(x, z), 0.288f, 1.055f,
                            VerticalAnchor.Bottom, 0f, side == 0 ? 0f : 180f,
                            art.Wood, art.WoodTop, art.Groove, art.Brass);
                    }
                }

                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Place("PP_Kit_RailEndCap.fbx", sign < 0 ? "Near End Rail" : "Far End Rail",
                        visualRoot, new Vector2(centerX, centerZ + sign * endZ),
                        3.56f, 0.288f, VerticalAnchor.Bottom, 0f,
                        sign < 0 ? 0f : 180f,
                        art.Wood, art.WoodTop, art.Groove, art.Brass);

                    foreach (float x in new[] { left, right })
                    {
                        Place("PP_Kit_RailCorner.fbx", $"Corner {x:F2} {sign}",
                            visualRoot, new Vector2(x, centerZ + sign * endZ),
                            0.32f, 0.32f, VerticalAnchor.Bottom, 0f, 0f,
                            art.WoodTop, art.Brass);
                    }
                }

                foreach (float x in new[] { centerX - 1.40f, centerX + 1.40f })
                {
                    foreach (float z in new[] { centerZ - 3.45f, centerZ + 3.45f })
                    {
                        Place("PP_Kit_RubberFoot.fbx", $"Rubber Foot {x:F2} {z:F2}",
                            visualRoot, new Vector2(x, z), 0.48f, 0.70f,
                            VerticalAnchor.Top, -0.44f, 0f, art.Rubber);
                    }
                }

                Vector2 holePosition = new Vector2(level.hole.x, level.hole.z);
                GameObject cup = Place("PP_Kit_CupRim.fbx", "Cup - Visual Only",
                    visualRoot, holePosition, 0.662f, 0.662f,
                    VerticalAnchor.Bottom, 0.01f, 0f,
                    art.Dark, art.Brass, art.Groove);

                // The visual anchor is the center of the *separate cup module*,
                // not the rectangular field bounds. Reject any offset.
                if (!TryBounds(cup, out Bounds cupBounds) ||
                    Vector2.Distance(
                        new Vector2(cupBounds.center.x, cupBounds.center.z),
                        holePosition) > 0.08f)
                    throw new InvalidOperationException("Decorative cup does not match physical JSON hole.");

                // The Rigidbody sphere is scaled 0.44: inverse scaling the
                // child pivot keeps a 0.44-world-unit art mesh in a 0.44 ball.
                ballPivot = new GameObject("Ball Art Pivot").transform;
                ballPivot.SetParent(ball, false);
                ballPivot.localPosition = Vector3.zero;
                ballPivot.localRotation = Quaternion.identity;
                ballPivot.localScale = Vector3.one / Mathf.Max(ball.lossyScale.x, 0.001f);

                GameObject ballArt = Place("PP_Kit_GolfBall.fbx", "Golf Ball - Visual Only",
                    ballPivot, new Vector2(ball.position.x, ball.position.z),
                    BallDiameter, BallDiameter, VerticalAnchor.Center,
                    ball.position.y, 0f, art.Pearl, art.Mark);

                Renderer[] shellRenderers = ballArt.GetComponentsInChildren<Renderer>(true);
                if (shellRenderers.Length == 0)
                    throw new InvalidOperationException("Decorative ball FBX contains no renderers.");

                // Hide physics geometry ONLY when all modules have succeeded.
                foreach (Transform child in levelRoot)
                {
                    if (child.name != "Floor" && child.name != "Wall" &&
                        child.name != "Hole Rim" && child.name != "Hole Center")
                        continue;
                    MeshRenderer placeholder = child.GetComponent<MeshRenderer>();
                    if (placeholder != null)
                        placeholder.enabled = false;
                }

                MeshRenderer ballPlaceholder = ball.GetComponent<MeshRenderer>();
                if (ballPlaceholder != null)
                    ballPlaceholder.enabled = false;

                SetStage(camera, levelRoot, art.Background);
                Debug.Log("[Pocket Putt] C1-01 modular presentation generated (8 source FBXs, no art colliders).");
                return true;
            }
            catch (Exception error)
            {
                // Never leave a partially hidden/unplayable scene.
                UnityEngine.Object.DestroyImmediate(visualRoot.gameObject);
                if (ballPivot != null)
                    UnityEngine.Object.DestroyImmediate(ballPivot.gameObject);
                Debug.LogError($"[Pocket Putt] Modular art failed: {error.Message}. Falling back to known working visual.");
                return false;
            }
        }

        private static Appearance LoadAppearance()
        {
            return new Appearance
            {
                Turf = Surface("S1_Turf", "#548F5C", 0.94f),
                TurfStripe = Surface("S1_TurfStripe", "#599763", 0.94f),
                Wood = Surface("S1_WoodSide", "#84583F", 0.77f),
                WoodTop = Surface("S1_WoodTop", "#AC805A", 0.68f),
                Groove = Surface("S1_WoodGroove", "#63422E", 0.87f),
                Brass = Surface("S1_BrushedBrass", "#BCA06C", 0.42f, 0.38f),
                Dark = Surface("S1_CupDark", "#13241D", 0.93f),
                Pearl = Surface("S1_Pearl", "#EAF2EB", 0.23f),
                Mark = Surface("S1_PearlMark", "#8BA4A2", 0.64f),
                Base = Surface("S1_Base", "#51362B", 0.73f),
                Rubber = Surface("S1_Rubber", "#1B2520", 0.95f),
                Background = Surface("S1_Backdrop", "#A5B6A3", 0.98f)
            };
        }

        private static Material Surface(string name, string hex, float roughness, float metallic = 0f)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP Lit is required by the modular art presentation.");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.shader != shader)
                material.shader = shader;

            ColorUtility.TryParseHtmlString(hex, out Color color);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", Mathf.Clamp01(1f - roughness));
            material.SetFloat("_Metallic", metallic);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject Place(
            string file, string name, Transform parent, Vector2 center,
            float expectedWidth, float expectedLength,
            VerticalAnchor anchor, float anchorHeight, float yaw,
            params Material[] palette)
        {
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + file);
            GameObject wrapper = new GameObject(name);
            wrapper.transform.SetParent(parent, false);

            GameObject meshRoot = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
            if (meshRoot == null)
                throw new InvalidOperationException($"Failed to instantiate {file}");
            meshRoot.transform.SetParent(wrapper.transform, false);

            foreach (Collider collider in meshRoot.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            if (!TryBounds(meshRoot, out Bounds bounds))
                throw new InvalidOperationException($"{file} has no renderable mesh.");

            // Blender FBXs may import with different root rotation/scale.
            // Align the long axis and scale consistently before positioning.
            if (bounds.size.y > Mathf.Max(bounds.size.x, bounds.size.z) * 1.2f)
            {
                meshRoot.transform.Rotate(90f, 0f, 0f, Space.World);
                TryBounds(meshRoot, out bounds);
            }

            float direct = RatioError(bounds.size.x, bounds.size.z, expectedWidth, expectedLength);
            float rotated = RatioError(bounds.size.z, bounds.size.x, expectedWidth, expectedLength);
            if (rotated + 0.01f < direct)
            {
                meshRoot.transform.Rotate(0f, 90f, 0f, Space.World);
                TryBounds(meshRoot, out bounds);
            }

            // A 180 degree yaw changes the groove side without moving the module.
            if (Mathf.Abs(yaw) > 0.001f)
            {
                meshRoot.transform.Rotate(0f, yaw, 0f, Space.World);
                TryBounds(meshRoot, out bounds);
            }

            float xScale = expectedWidth / Mathf.Max(bounds.size.x, 0.0001f);
            float zScale = expectedLength / Mathf.Max(bounds.size.z, 0.0001f);
            if (Mathf.Abs(xScale - zScale) > SizeTolerance)
                throw new InvalidOperationException(
                    $"Unexpected proportions in {file}: actual {bounds.size}, desired {expectedWidth} x {expectedLength}.");
            meshRoot.transform.localScale *= (xScale + zScale) * 0.5f;
            if (!TryBounds(meshRoot, out bounds))
                throw new InvalidOperationException($"Failed to size {file}.");

            float targetY = anchor == VerticalAnchor.Top ? anchorHeight - bounds.max.y :
                anchor == VerticalAnchor.Bottom ? anchorHeight - bounds.min.y :
                anchorHeight - bounds.center.y;
            meshRoot.transform.position += new Vector3(
                center.x - bounds.center.x, targetY, center.y - bounds.center.z);

            // Blender merged meshes have stable submesh material order. Keep all
            // palette slots so wood/brass/turf remain distinct after FBX import.
            foreach (MeshRenderer renderer in meshRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                int slotCount = filter != null && filter.sharedMesh != null
                    ? Mathf.Max(1, filter.sharedMesh.subMeshCount)
                    : Mathf.Max(1, renderer.sharedMaterials.Length);
                Material[] mapped = new Material[slotCount];
                for (int index = 0; index < mapped.Length; index++)
                    mapped[index] = palette[Mathf.Min(index, palette.Length - 1)];
                renderer.sharedMaterials = mapped;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            return wrapper;
        }

        private static float RatioError(float actualWidth, float actualLength, float expectedWidth, float expectedLength)
        {
            float x = expectedWidth / Mathf.Max(0.0001f, actualWidth);
            float z = expectedLength / Mathf.Max(0.0001f, actualLength);
            return Mathf.Abs(x - z);
        }

        private static bool TryBounds(GameObject go, out Bounds bounds)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        private static void SetStage(Camera camera, Transform levelRoot, Material background)
        {
            Transform stage = new GameObject("Presentation - Studio").transform;
            stage.SetParent(levelRoot, false);
            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Backdrop (No Collider)";
            table.transform.SetParent(stage, false);
            table.transform.position = new Vector3(0f, -0.77f, 0f);
            table.transform.localScale = new Vector3(60f, 0.10f, 60f);
            table.GetComponent<MeshRenderer>().sharedMaterial = background;
            UnityEngine.Object.DestroyImmediate(table.GetComponent<Collider>());

            camera.transform.position = new Vector3(0f, 12f, -2.2f);
            camera.transform.rotation = Quaternion.LookRotation(-camera.transform.position, Vector3.forward);
            camera.orthographic = true;
            // At 9:16 keep the whole 3.97-wide / 8.29-long base visible.
            const float margin = 0.6f;
            float portraitAspect = Mathf.Min(Mathf.Max(camera.aspect, 0.4f), 1f);
            float heightNeeded = (8.29f + margin) * 0.5f;
            float widthNeeded = (3.97f + margin) / (2f * portraitAspect);
            camera.orthographicSize = Mathf.Max(heightNeeded, widthNeeded);
            // Runtime component recomputes fit for 9:16 and taller devices.
            PocketPutt.Presentation.PrototypeCameraFit fit =
                camera.gameObject.AddComponent<PocketPutt.Presentation.PrototypeCameraFit>();
            fit.Configure(3.97f, 8.29f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.65f, 0.71f, 0.65f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.50f, 0.56f, 0.51f);
            Light primary = levelRoot.Find("Key Light")?.GetComponent<Light>();
            if (primary != null)
            {
                primary.intensity = 1.18f;
                primary.shadows = LightShadows.Soft;
                primary.shadowStrength = 0.48f;
                primary.transform.rotation = Quaternion.Euler(52f, -34f, 0f);
                primary.color = new Color(1f, 0.94f, 0.85f);
            }
            GameObject fill = new GameObject("Studio Fill Light");
            fill.transform.SetParent(stage, false);
            Light fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.shadows = LightShadows.None;
            fillLight.intensity = 0.25f;
            fillLight.color = new Color(0.75f, 0.83f, 1f);
            fillLight.transform.rotation = Quaternion.Euler(40f, 142f, 0f);
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }
}
#endif
