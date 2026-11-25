#if UNITY_EDITOR
using System;
using System.IO;
using PocketPutt.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PocketPutt.EditorTools
{
    /// <summary>Rebuildable greybox scene from one authored JSON source of truth.</summary>
    public static class BuildPrototypeLevel
    {
        private const string DataPath = "Assets/_PocketPutt/Data/Levels/C1-01.json";
        private const string ScenePath = "Assets/_PocketPutt/Scenes/C1-01_Prototype.unity";
        private const string MaterialFolder = "Assets/_PocketPutt/Art/PrototypeMaterials";

        [MenuItem("Pocket Putt/Build C1-01 Prototype")]
        public static void Build()
        {
            TextAsset jsonAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath);
            if (jsonAsset == null)
                throw new FileNotFoundException($"Missing level data: {DataPath}");

            PrototypeLevelDefinition level = JsonUtility.FromJson<PrototypeLevelDefinition>(jsonAsset.text);
            if (level == null)
                throw new ArgumentException("Could not parse prototype level JSON.");
            level.Validate();

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(MaterialFolder);
            Material turf = GetMaterial("Turf", new Color(0.34f, 0.63f, 0.36f));
            Material walls = GetMaterial("Wood", new Color(0.57f, 0.38f, 0.23f));
            Material white = GetMaterial("Ball", new Color(0.96f, 0.97f, 0.93f));
            Material shadow = GetMaterial("Hole", new Color(0.055f, 0.085f, 0.07f));
            Material rim = GetMaterial("HoleRim", new Color(0.85f, 0.78f, 0.58f));
            PhysicsMaterial wallPhysics = GetWallPhysics();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject($"Level {level.id} - {level.displayName}");

            foreach (LevelBlock floor in level.floors)
                MakeBlock("Floor", floor, -0.15f, 0.3f, turf, root.transform);
            foreach (LevelBlock wall in level.walls)
                MakeBlock("Wall", wall, 0.25f, 0.5f, walls, root.transform, wallPhysics);

            GameObject cameraObject = new GameObject("Gameplay Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 12f, 0f);
            cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = 5.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.15f, 0.23f, 0.19f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            cameraObject.transform.SetParent(root.transform);

            GameObject lightObject = new GameObject("Key Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.45f;
            lightObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            lightObject.transform.SetParent(root.transform);

            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Player Ball";
            ball.transform.SetParent(root.transform);
            ball.transform.position = new Vector3(level.ballStart.x, 0.22f, level.ballStart.z);
            ball.transform.localScale = Vector3.one * 0.44f;
            ball.GetComponent<Renderer>().sharedMaterial = white;
            Rigidbody rb = ball.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 1f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            PrototypePuttController controller = ball.AddComponent<PrototypePuttController>();
            controller.Configure(camera, level.displayName, level.threeStarMaxShots, level.twoStarMaxShots);

            // Visual marker (not a physical shaft hole) so physics can be tested first.
            MakeDisc("Hole Rim", level.hole, 0.37f, 0.008f, rim, root.transform);
            MakeDisc("Hole Center", level.hole, 0.29f, 0.015f, shadow, root.transform);
            GameObject goal = new GameObject("Hole Trigger");
            goal.transform.SetParent(root.transform);
            goal.transform.position = new Vector3(level.hole.x, 0.22f, level.hole.z);
            SphereCollider trigger = goal.AddComponent<SphereCollider>();
            trigger.radius = 0.48f;
            trigger.isTrigger = true;
            PrototypeHole hole = goal.AddComponent<PrototypeHole>();
            hole.Configure(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            // Build premium visual modules, but keep a fully playable fallback
            // if any module fails import or alignment. The gameplay JSON and
            // physical colliders are identical in both paths.
            if (!BuildModularPresentation.TryBuild(root.transform, ball.transform, level, camera))
                AttachArtModels(root.transform, ball.transform,
                    new Vector3(level.hole.x, 0f, level.hole.z));

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException($"Scene could not be saved at {ScenePath}");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Pocket Putt] Created {ScenePath} from {DataPath}. Open and press Play to test.");
        }

        private static void MakeBlock(
            string name, LevelBlock block, float heightCenter, float height,
            Material material, Transform parent, PhysicsMaterial collisionMaterial = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(new Vector3(block.x, heightCenter, block.z),
                Quaternion.Euler(0f, block.rotation, 0f));
            go.transform.localScale = new Vector3(block.width, height, block.length);
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (collisionMaterial != null)
                go.GetComponent<Collider>().sharedMaterial = collisionMaterial;
        }

        private static void MakeDisc(string name, LevelPoint center, float radius,
            float y, Material material, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(center.x, y, center.z);
            go.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }


        private const string CourseArtPath = "Assets/_PocketPutt/Art/Models/PP_C1_01_Course.fbx";
        private const string BallArtPath = "Assets/_PocketPutt/Art/Models/PP_GolfBall.fbx";

        private static void AttachArtModels(Transform levelRoot, Transform ball, Vector3 expectedHole)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CourseArtPath);
            if (prefab != null)
            {
                GameObject visual = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (visual == null)
                    throw new InvalidOperationException("Could not instantiate decorative course.");
                visual.name = "Visuals - Imported Course";
                visual.transform.SetParent(levelRoot, true);
                DisableVisualColliders(visual);
                if (AlignCourse(visual, expectedHole))
                {
                    ApplyCoursePalette(visual);

                    // Only hide the old art after verifying that its cup
                    // aligns with the gameplay hole. Colliders remain untouched.
                    foreach (Transform child in levelRoot)
                    {
                        if (child.name != "Floor" && child.name != "Wall" &&
                            child.name != "Hole Rim" && child.name != "Hole Center")
                            continue;
                        MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                        if (renderer != null)
                            renderer.enabled = false;
                    }
                    Debug.Log("[Pocket Putt] Validated course art alignment; physical colliders preserved.");
                }
                else
                {
                    // Wrong or unrecognizable FBX must not mask the playable geometry.
                    UnityEngine.Object.DestroyImmediate(visual);
                    Debug.LogError("[Pocket Putt] Course art not aligned to physical hole. Greybox kept visible.");
                }
            }
            else
                Debug.LogWarning("[Pocket Putt] Course FBX missing. Retaining greybox.");

            GameObject ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallArtPath);
            if (ballPrefab == null)
            {
                Debug.LogWarning("[Pocket Putt] Ball FBX missing. Retaining sphere.");
                return;
            }
            GameObject artPivot = new GameObject("Ball Art Pivot");
            artPivot.transform.SetParent(ball, false);
            artPivot.transform.localPosition = Vector3.zero;
            GameObject ballModel = PrefabUtility.InstantiatePrefab(ballPrefab) as GameObject;
            if (ballModel == null)
                throw new InvalidOperationException("Could not instantiate decorative ball.");
            ballModel.name = "Visuals - Imported Ball";
            ballModel.transform.SetParent(artPivot.transform, false);
            ballModel.transform.localPosition = Vector3.zero;
            ballModel.transform.localRotation = Quaternion.identity;
            ballModel.transform.localScale = Vector3.one;
            DisableVisualColliders(ballModel);
            AlignBall(ballModel, ball);
            foreach (MeshRenderer renderer in ballModel.GetComponentsInChildren<MeshRenderer>(true))
                renderer.sharedMaterial = renderer.name.Contains("TopMark")
                    ? GetMaterial("BallMark", new Color(0.79f, 0.87f, 0.77f))
                    : GetMaterial("BallPearl", new Color(0.95f, 0.96f, 0.92f));

            MeshRenderer sphereRenderer = ball.GetComponent<MeshRenderer>();
            if (sphereRenderer != null)
                sphereRenderer.enabled = false;
            Debug.Log("[Pocket Putt] Decorative ball integrated; original Rigidbody/collider preserved.");
        }

        private static void DisableVisualColliders(GameObject go)
        {
            foreach (Collider collider in go.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
        }

        private static bool BoundsFor(GameObject go, out Bounds bounds)
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

        private static bool TryFindVisualCup(GameObject visual, out Vector3 position)
        {
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                // Blender authoring contract. The cup is a visible marker with
                // this stable name, independent from any Unity gameplay collider.
                if (renderer.gameObject.name.Contains("Cup_DarkInset"))
                {
                    position = renderer.bounds.center;
                    return true;
                }
            }
            position = default;
            return false;
        }

        private static bool AlignCourse(GameObject visual, Vector3 targetHole)
        {
            const float expectedWidth = 3.57f;
            const float expectedLength = 7.73f;
            const float expectedBottom = -0.105f;
            const float maxCupError = 0.2f;

            if (!BoundsFor(visual, out Bounds bounds))
                return false;

            // Fit the FBX to the non-visual physics footprint.
            if (bounds.size.y > bounds.size.z && bounds.size.y > bounds.size.x)
            {
                visual.transform.Rotate(90f, 0f, 0f, Space.World);
                BoundsFor(visual, out bounds);
            }
            if (bounds.size.x > bounds.size.z)
            {
                visual.transform.Rotate(0f, 90f, 0f, Space.World);
                BoundsFor(visual, out bounds);
            }

            float scaleX = expectedWidth / Mathf.Max(0.001f, bounds.size.x);
            float scaleZ = expectedLength / Mathf.Max(0.001f, bounds.size.z);
            if (Mathf.Abs(scaleX - scaleZ) > 0.2f)
                Debug.LogWarning("[Pocket Putt] Course FBX has unusual proportions.");

            visual.transform.localScale *= Mathf.Min(scaleX, scaleZ);
            BoundsFor(visual, out bounds);
            visual.transform.position += new Vector3(
                -bounds.center.x, expectedBottom - bounds.min.y, -bounds.center.z);

            // Bounds alone cannot distinguish a 180-degree flipped level:
            // the rectangle is symmetrical but the cup is NOT.
            if (!TryFindVisualCup(visual, out Vector3 cup))
            {
                Debug.LogError("[Pocket Putt] FBX does not contain Cup_DarkInset. Cannot verify orientation.");
                return false;
            }
            Vector3 flatCup = new Vector3(cup.x, 0f, cup.z);
            Vector3 flatTarget = new Vector3(targetHole.x, 0f, targetHole.z);
            if (flatCup.sqrMagnitude < 0.0001f || flatTarget.sqrMagnitude < 0.0001f)
            {
                Debug.LogError("[Pocket Putt] Invalid cup anchor for orientation check.");
                return false;
            }

            // For C1-01 this corrects the Blender->Unity north/south flip.
            float yaw = Vector3.SignedAngle(flatCup, flatTarget, Vector3.up);
            visual.transform.RotateAround(Vector3.zero, Vector3.up, yaw);

            if (!TryFindVisualCup(visual, out cup))
                return false;
            float cupError = Vector2.Distance(
                new Vector2(cup.x, cup.z), new Vector2(targetHole.x, targetHole.z));

            if (cupError > maxCupError)
            {
                Debug.LogError($"[Pocket Putt] Visual cup misses physical hole by {cupError:F3} units. Do not enable mismatched art.");
                return false;
            }

            Debug.Log($"[Pocket Putt] Visual cup aligned: yaw correction {yaw:F1}°, offset {cupError:F3} units.");
            return true;
        }

        private static void AlignBall(GameObject model, Transform ball)
        {
            if (!BoundsFor(model, out Bounds bounds))
                throw new InvalidOperationException("Ball art has no renderable meshes.");

            float diameter = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            model.transform.localScale *= 0.44f / Mathf.Max(0.001f, diameter);
            BoundsFor(model, out bounds);
            model.transform.position += ball.position - bounds.center;
        }

        private static void ApplyCoursePalette(GameObject root)
        {
            Material turf = GetMaterial("ArtGrass", new Color(0.30f, 0.56f, 0.36f));
            Material stripe = GetMaterial("ArtGrassLight", new Color(0.37f, 0.65f, 0.44f));
            Material wood = GetMaterial("ArtWalnut", new Color(0.48f, 0.28f, 0.17f));
            Material inset = GetMaterial("ArtWalnutInset", new Color(0.32f, 0.19f, 0.14f));
            Material honey = GetMaterial("ArtHoney", new Color(0.73f, 0.51f, 0.27f));
            Material brass = GetMaterial("ArtBrass", new Color(0.76f, 0.59f, 0.31f));
            Material cup = GetMaterial("ArtCupDark", new Color(0.07f, 0.12f, 0.10f));
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = renderer.gameObject.name;
                Material m = wood;
                if (n.StartsWith("Turf_SubtleStripe_"))
                {
                    string suffix = n.Substring("Turf_SubtleStripe_".Length);
                    m = int.TryParse(suffix, out int index) && index % 2 == 0 ? stripe : turf;
                }
                else if (n.StartsWith("Turf_")) m = turf;
                else if (n.Contains("Wood_TopTrim")) m = honey;
                else if (n.Contains("Wood_InsideInlay") || n.Contains("Cup_ShadowLip")) m = inset;
                else if (n.Contains("Rail_Fastener") || n.Contains("Cup_BurnishedRim")) m = brass;
                else if (n.Contains("Cup_DarkInset")) m = cup;
                renderer.sharedMaterial = m;
            }
        }

        private static PhysicsMaterial GetWallPhysics()
        {
            string path = $"{MaterialFolder}/WallBounce.physicMaterial";
            PhysicsMaterial physics = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (physics != null)
                return physics;

            // Essential for bank shots; Unity's default collider is not sufficiently bouncy.
            physics = new PhysicsMaterial("WallBounce");
            physics.bounciness = 0.82f;
            physics.dynamicFriction = 0.05f;
            physics.staticFriction = 0.05f;
            physics.bounceCombine = PhysicsMaterialCombine.Maximum;
            physics.frictionCombine = PhysicsMaterialCombine.Minimum;
            AssetDatabase.CreateAsset(physics, path);
            return physics;
        }

        private static Material GetMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("No supported shader is available.");

            material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            else
                material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
