using System.Linq;
using SlipperyZlope.RingToss.Audio;
using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.Gameplay;
using SlipperyZlope.RingToss.Juice;
using SlipperyZlope.RingToss.Mobile;
using SlipperyZlope.RingToss.Monetisation;
using SlipperyZlope.RingToss.Save;
using SlipperyZlope.RingToss.UI;
using SlipperyZlope.RingToss.Utils;
using SlipperyZlope.RingToss.World;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using BlendMode = UnityEngine.Rendering.BlendMode;

namespace SlipperyZlope.RingToss.EditorTools
{
    /// <summary>
    /// Builds the whole playable scene from scratch and wires every reference.
    ///
    /// The saved scene is deliberately almost empty: a camera rig, a light, a few
    /// empty containers and one systems object holding components whose references
    /// this script assigns. Everything the player sees — the carnival, the pegs, the
    /// rings, the interface — is generated at load by those components.
    ///
    /// That is the opposite of the usual Unity workflow, and it is a deliberate trade.
    /// A hand-authored scene puts the game's whole appearance into serialized YAML,
    /// where it cannot be read in a review, cannot be diffed usefully, and cannot be
    /// rebuilt on a clean clone. Here the scene file stays small enough to read, and
    /// the art direction lives in code next to the logic it serves.
    ///
    /// Re-running overwrites the scene. Nothing in it is authored by hand, so there is
    /// nothing to lose.
    /// </summary>
    public static class GameSceneBootstrap
    {
        private const string ScenesFolder = "Assets/_Game/Scenes";
        private const string MainScenePath = ScenesFolder + "/Main.unity";
        private const string DataFolder = "Assets/_Game/Data";
        private const string MaterialsFolder = "Assets/_Game/Art/Materials";
        private const string MeshesFolder = "Assets/_Game/Art/Meshes";
        private const string LibraryPath = DataFolder + "/LevelLibrary.asset";
        private const string CosmeticsPath = DataFolder + "/CosmeticsLibrary.asset";

        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string UnlitShader = "Universal Render Pipeline/Unlit";
        private const string ParticleShader = "Universal Render Pipeline/Particles/Unlit";
        private const string SoftDiscShader = "SlipperyZlope/Soft Disc";
        private const string SkyShader = "SlipperyZlope/Sky Gradient";

        // Portrait framing. The camera sits behind and above the throw origin and looks
        // slightly down, so the board reads as depth rather than as a flat backdrop,
        // while still leaving a band of sky along the top of the frame.
        private static readonly Vector3 CameraPosition = new Vector3(0f, 2.55f, -9.4f);
        private static readonly Vector3 CameraEulerAngles = new Vector3(12f, 0f, 0f);
        private const float CameraFieldOfView = 41f;

        // 1.15 to 1.0 to 0.65, over two rounds of the owner saying it still floats.
        //
        // The ring is the biggest thing on the screen and it was held level with the
        // middle of the frame, which read as an object hanging in front of the booth
        // rather than one being held over it. At 0.65 its lower rim sits about a
        // third of a metre off the board and it reads as low, without touching down —
        // a ring resting *on* the floor would look dropped rather than held.
        //
        // The counter is not a constraint at the shipped scenery density: it is gated
        // to Busy and this build runs Calm, so nothing is in front of the ring here.
        //
        // This is the launch height as well as a pose, so EndlessRoundBuilder.LaunchHeight
        // mirrors it and the reachability fixtures are what say it is still throwable —
        // a lower hand has less arc to spend on a peg standing on a plinth.
        private static readonly Vector3 ThrowOriginPosition = new Vector3(0f, 0.65f, -4.5f);

        private const int RingPoolCapacity = 14;
        private const int PegCapacity = 6;

        [MenuItem("SlipperyZlope/Bootstrap Game Scene")]
        public static void Run()
        {
            EditorAssets.EnsureFolder(DataFolder);
            EditorAssets.EnsureFolder(MaterialsFolder);
            EditorAssets.EnsureFolder(MeshesFolder);
            EditorAssets.EnsureFolder(ScenesFolder);

            Assets assets = CreateAssets();

            if (assets.Library == null)
            {
                Debug.LogError("[GameSceneBootstrap] No LevelLibrary. Run SlipperyZlope > Bootstrap Levels first.");
                return;
            }

            if (assets.Cosmetics == null)
            {
                Debug.LogError(
                    "[GameSceneBootstrap] No CosmeticsLibrary. Run SlipperyZlope > Bootstrap Cosmetics first.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera camera = CreateCameraRig(out CinemachineCamera virtualCamera);
            Light keyLight = CreateKeyLight();
            CreatePostProcessing();

            Transform world = new GameObject("World").transform;
            Transform board = new GameObject("Board Root").transform;
            board.SetParent(world, false);

            Transform throwOrigin = new GameObject("Throw Origin").transform;
            throwOrigin.SetPositionAndRotation(ThrowOriginPosition, Quaternion.identity);

            Transform ringContainer = new GameObject("Rings").transform;
            Transform shadowContainer = new GameObject("Ring Shadows").transform;

            WireSystems(assets, camera, virtualCamera, keyLight, world, board,
                throwOrigin, ringContainer, shadowContainer);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainScenePath);
            // Game Hub: the build also holds the menu and Pocket Putt, so keep the
            // existing list and only add this scene if it is missing.
            if (!System.Array.Exists(EditorBuildSettings.scenes, s => s.path == MainScenePath))
            {
                EditorBuildSettings.scenes = EditorBuildSettings.scenes
                    .Append(new EditorBuildSettingsScene(MainScenePath, true)).ToArray();
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"[GameSceneBootstrap] Built {MainScenePath} with {assets.Library.Count} levels.");
        }

        /// <summary>Everything the runtime components need handed to them.</summary>
        private struct Assets
        {
            public PhysicsConfig Physics;
            public JuiceConfig Juice;
            public AssistanceConfig Assistance;
            public LevelLibrary Library;
            public CosmeticsLibrary Cosmetics;
            public AdConfig Ads;

            public Mesh RingMesh;
            public Mesh DiscMesh;
            public Mesh AnnulusMesh;
            public Mesh ConfettiMesh;

            public Material RingMaterial;
            public Material TrailMaterial;
            public Material ShadowMaterial;
            public Material ShockwaveMaterial;
            public Material SparkMaterial;
            public Material ConfettiMaterial;
            public Material PreviewDotMaterial;
            public Material PegBodyMaterial;
            public Material PegStripeMaterial;
            public Material PegCapMaterial;
            public Material PegGlowMaterial;
            public Material LitSource;
            public Material UnlitSource;
            public Material SoftDiscSource;
            public Material SkyMaterial;

            public PhysicsMaterial RingPhysics;
            public PhysicsMaterial PegPhysics;
            public PhysicsMaterial GroundPhysics;
        }

        private static Assets CreateAssets()
        {
            Assets assets = new Assets
            {
                Physics = EditorAssets.LoadOrCreate<PhysicsConfig>(DataFolder + "/PhysicsConfig.asset"),
                Juice = EditorAssets.LoadOrCreate<JuiceConfig>(DataFolder + "/JuiceConfig.asset"),
                Assistance = EditorAssets.LoadOrCreate<AssistanceConfig>(DataFolder + "/AssistanceConfig.asset"),
                Ads = EditorAssets.LoadOrCreate<AdConfig>(DataFolder + "/AdConfig.asset"),
                Library = AssetDatabase.LoadAssetAtPath<LevelLibrary>(LibraryPath),
                Cosmetics = AssetDatabase.LoadAssetAtPath<CosmeticsLibrary>(CosmeticsPath)
            };

            PhysicsConfig physics = assets.Physics;

            // The visual ring is a real torus; the collider stays a ring of capsules.
            // A convex hull over a torus closes the hole, which would make the game
            // unwinnable while looking perfectly correct.
            assets.RingMesh = EditorAssets.SaveMesh(
                MeshesFolder + "/RingTorus.asset",
                MeshFactory.Torus(physics.RingOuterRadius, physics.RingTubeRadius, 32, 10));

            assets.DiscMesh = EditorAssets.SaveMesh(
                MeshesFolder + "/Disc.asset", MeshFactory.Disc(0.5f, 32));

            assets.AnnulusMesh = EditorAssets.SaveMesh(
                MeshesFolder + "/Shockwave.asset", MeshFactory.Annulus(0.34f, 0.5f, 48));

            assets.ConfettiMesh = EditorAssets.SaveMesh(
                MeshesFolder + "/ConfettiFlake.asset", MeshFactory.Quad(1f, 0.6f));

            assets.RingMaterial = EditorAssets.LoadOrCreateMaterial(
                MaterialsFolder + "/RingMaterial.mat", LitShader, new Color(1f, 0.82f, 0.4f));
            MakeGlossy(assets.RingMaterial, smoothness: 0.55f, metallic: 0.15f, emission: true);

            assets.TrailMaterial = EditorAssets.LoadOrCreateMaterial(
                MaterialsFolder + "/TrailMaterial.mat", ParticleShader, Color.white);
            MakeAdditive(assets.TrailMaterial);

            assets.ShadowMaterial = EditorAssets.LoadOrCreateMaterial(
                MaterialsFolder + "/RingShadowMaterial.mat", SoftDiscShader, new Color(0f, 0f, 0f, 0.4f));
            SetDiscShape(assets.ShadowMaterial, softness: 0.55f, fill: 0.2f, additive: false);

            assets.ShockwaveMaterial = EditorAssets.LoadOrCreateMaterial(
                MaterialsFolder + "/ShockwaveMaterial.mat", SoftDiscShader, Color.white);
            SetDiscShape(assets.ShockwaveMaterial, softness: 0.9f, fill: 0f, additive: true);

            assets.SparkMaterial = EditorAssets.LoadOrCreateMaterial(
                MaterialsFolder + "/SparkMaterial.mat", ParticleShader, Color.white);
            MakeAdditive(assets.SparkMaterial);

            assets.ConfettiMaterial = EditorAssets.LoadOrCreateMaterial(
                MaterialsFolder + "/ConfettiMaterial.mat", ParticleShader, Color.white);
            MakeAlphaBlended(assets.ConfettiMaterial);
            SetCullOff(assets.ConfettiMaterial);

            assets.PreviewDotMaterial = EditorAssets.LoadOrCreateMaterial(
                MaterialsFolder + "/TrajectoryPreviewMaterial.mat", SoftDiscShader, Color.white);
            SetDiscShape(assets.PreviewDotMaterial, softness: 0.7f, fill: 0.1f, additive: true);

            assets.PegBodyMaterial = EditorAssets.LoadOrCreateMaterial(
                MaterialsFolder + "/PegMaterial.mat", LitShader, new Color(0.89f, 0.24f, 0.34f));
            MakeGlossy(assets.PegBodyMaterial, smoothness: 0.35f, metallic: 0f, emission: false);

            assets.PegStripeMaterial = EditorAssets.LoadOrCreateMaterial(
                MaterialsFolder + "/PegStripeMaterial.mat", LitShader, new Color(0.99f, 0.96f, 0.92f));
            MakeGlossy(assets.PegStripeMaterial, smoothness: 0.4f, metallic: 0f, emission: false);

            assets.PegCapMaterial = EditorAssets.LoadOrCreateMaterial(
                MaterialsFolder + "/PegCapMaterial.mat", LitShader, new Color(1f, 0.9f, 0.6f));
            MakeGlossy(assets.PegCapMaterial, smoothness: 0.7f, metallic: 0.1f, emission: true);

            assets.PegGlowMaterial = EditorAssets.LoadOrCreateMaterial(
                MaterialsFolder + "/PegGlowMaterial.mat", SoftDiscShader, new Color(1f, 0.83f, 0.4f, 0.35f));
            SetDiscShape(assets.PegGlowMaterial, softness: 0.85f, fill: 0.05f, additive: true);

            assets.LitSource = EditorAssets.LoadOrCreateMaterial(
                MaterialsFolder + "/SceneryLit.mat", LitShader, Color.white);
            // Emissive-capable, at black: the deck and its rim glow through clones of this
            // with a streak of perfects (FloorGlow), and a clone can only glow on a phone
            // if the variant is in the build — which it is only if this asset uses it.
            MakeGlossy(assets.LitSource, smoothness: 0.22f, metallic: 0f, emission: true);

            assets.UnlitSource = EditorAssets.LoadOrCreateMaterial(
                MaterialsFolder + "/SceneryUnlit.mat", UnlitShader, Color.white);

            assets.SoftDiscSource = EditorAssets.LoadOrCreateMaterial(
                MaterialsFolder + "/SceneryHalo.mat", SoftDiscShader, new Color(1f, 1f, 1f, 0.5f));
            SetDiscShape(assets.SoftDiscSource, softness: 0.95f, fill: 0f, additive: true);

            assets.SkyMaterial = EditorAssets.LoadOrCreateMaterial(
                MaterialsFolder + "/SkyGradient.mat", SkyShader, Color.white);

            assets.RingPhysics = CreateRingPhysicsMaterial();
            assets.PegPhysics = CreatePegPhysicsMaterial();
            assets.GroundPhysics = CreateGroundPhysicsMaterial();

            AssetDatabase.SaveAssets();
            return assets;
        }

        private static Camera CreateCameraRig(out CinemachineCamera virtualCamera)
        {
            GameObject cameraObject = new GameObject("Main Camera")
            {
                tag = "MainCamera"
            };
            cameraObject.transform.SetPositionAndRotation(
                CameraPosition, Quaternion.Euler(CameraEulerAngles));

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = CameraFieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 90f;

            // The sky is a real gradient skybox now, so clearing to it is correct: the
            // horizon it draws is matched to the ground rather than fighting it.
            camera.clearFlags = CameraClearFlags.Skybox;

            // Post-processing is off by default on a new camera, so the volume would
            // exist and do nothing.
            UniversalAdditionalCameraData cameraData =
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.None;

            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CinemachineBrain>();

            GameObject virtualCameraObject = new GameObject("CM Gameplay Camera");
            virtualCameraObject.transform.SetPositionAndRotation(
                CameraPosition, Quaternion.Euler(CameraEulerAngles));

            virtualCamera = virtualCameraObject.AddComponent<CinemachineCamera>();
            virtualCamera.Lens.FieldOfView = CameraFieldOfView;
            virtualCamera.Lens.NearClipPlane = 0.1f;
            virtualCamera.Lens.FarClipPlane = 90f;
            virtualCamera.Priority = 10;

            // Without a listener the impulse sources generate signals that nothing
            // receives, and the camera never shakes.
            virtualCameraObject.AddComponent<CinemachineImpulseListener>();

            // The lens is authored for one screen shape and solved for the rest. A
            // field of view is vertical, so a phone taller than the design aspect would
            // otherwise crop the sides — the failure that cost this project two rounds
            // of scenery — and this opens the angle until the composed width fits again.
            virtualCameraObject.AddComponent<CameraFitter>();

            return camera;
        }

        /// <summary>
        /// A global volume with the only two post effects this game gets.
        ///
        /// Bloom is what makes the peg caps, the bulbs and the emissive rings read as
        /// lights rather than as pale paint, and the vignette keeps the eye on the
        /// board in portrait. Anything more would cost frame time on a mid-range phone
        /// for an effect nobody would name.
        /// </summary>
        private static void CreatePostProcessing()
        {
            VolumeProfile profile = EditorAssets.LoadOrCreate<VolumeProfile>(
                "Assets/_Game/Settings/GameplayVolumeProfile.asset");

            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>(true);
            }

            bloom.active = true;
            bloom.threshold.Override(0.82f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.78f);
            bloom.tint.Override(new Color(1f, 0.95f, 0.88f));

            // High quality filtering costs more than it is worth at phone resolution.
            bloom.highQualityFiltering.Override(false);

            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = profile.Add<Vignette>(true);
            }

            vignette.active = true;
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.42f);
            vignette.color.Override(new Color(0.04f, 0.02f, 0.08f));

            EditorUtility.SetDirty(profile);

            GameObject volumeObject = new GameObject("Global Volume");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
        }

        private static Light CreateKeyLight()
        {
            GameObject lightObject = new GameObject("Key Light");
            lightObject.transform.rotation = Quaternion.Euler(42f, -28f, 0f);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.6f;

            return light;
        }

        private static void WireSystems(
            Assets assets,
            Camera camera,
            CinemachineCamera virtualCamera,
            Light keyLight,
            Transform world,
            Transform board,
            Transform throwOrigin,
            Transform ringContainer,
            Transform shadowContainer)
        {
            GameObject systems = new GameObject("Game Systems");

            systems.AddComponent<FrameRateController>();
            ProgressStore progress = systems.AddComponent<ProgressStore>();

            // The world builder owns the scenery and the theme, and runs before
            // anything that reads a colour from it.
            WorldBuilder worldBuilder = world.gameObject.AddComponent<WorldBuilder>();

            RingPool ringPool = systems.AddComponent<RingPool>();
            ringPool.Configure(
                assets.Physics, assets.Juice,
                assets.RingMesh, assets.RingMaterial, assets.TrailMaterial,
                assets.DiscMesh, assets.ShadowMaterial, assets.RingPhysics,
                ringContainer, shadowContainer, RingPoolCapacity);

            LevelRunner levelRunner = systems.AddComponent<LevelRunner>();
            levelRunner.Configure(
                assets.Physics, assets.Assistance, assets.Juice, board,
                assets.PegBodyMaterial, assets.PegStripeMaterial, assets.PegCapMaterial,
                assets.PegGlowMaterial, assets.DiscMesh, assets.PegPhysics, PegCapacity);

            GameObject previewObject = new GameObject("Trajectory Preview");
            TrajectoryPreview preview = previewObject.AddComponent<TrajectoryPreview>();
            preview.Bind(assets.Juice, assets.DiscMesh, assets.PreviewDotMaterial, camera);

            ThrowController throwController = systems.AddComponent<ThrowController>();
            throwController.Configure(assets.Physics, throwOrigin, preview);

            GameDirector director = systems.AddComponent<GameDirector>();
            director.Configure(
                assets.Library, assets.Physics, assets.Juice,
                ringPool, levelRunner, throwController, progress, throwOrigin);

            worldBuilder.Bind(
                assets.LitSource, assets.UnlitSource, assets.SoftDiscSource,
                assets.SkyMaterial, assets.GroundPhysics, director, keyLight);

            HitStop hitStop = systems.AddComponent<HitStop>();
            hitStop.SetConfig(assets.Juice);

            GameObject fxObject = new GameObject("FX");
            FxPool fx = fxObject.AddComponent<FxPool>();
            fx.Configure(
                assets.Juice, assets.AnnulusMesh, assets.ShockwaveMaterial,
                assets.SparkMaterial, assets.ConfettiMaterial, assets.ConfettiMesh);

            GameObject popupObject = new GameObject("Score Popups");
            ScorePopupPool popups = popupObject.AddComponent<ScorePopupPool>();
            popups.SetConfig(assets.Juice);
            popups.SetCamera(camera);

            RingPresenter ringPresenter = systems.AddComponent<RingPresenter>();
            ringPresenter.SetConfig(assets.Juice);

            GameObject impulseObject = new GameObject("Camera Impulse");
            CinemachineImpulseSource impulseSource = impulseObject.AddComponent<CinemachineImpulseSource>();
            ConfigureImpulse(impulseSource, assets.Juice);

            CameraJuice cameraJuice = systems.AddComponent<CameraJuice>();
            cameraJuice.Bind(assets.Juice, virtualCamera, impulseSource);

            JuiceDirector juiceDirector = systems.AddComponent<JuiceDirector>();
            juiceDirector.Bind(assets.Juice, director, hitStop, fx, popups);

            AudioDirector audio = systems.AddComponent<AudioDirector>();
            audio.SetConfig(assets.Juice);

            CosmeticsDirector cosmetics = systems.AddComponent<CosmeticsDirector>();
            cosmetics.Bind(assets.Cosmetics, progress, fx);

            // Advertising. One component shows adverts and it is Unity's mediation;
            // without the LevelPlay package compiled in it draws a placeholder instead,
            // and with AdConfig.adsEnabled off nothing is requested at all. The game is
            // fully playable in every one of those states.
            // See docs/ADS_LEVELPLAY.md and docs/legal/COMPLIANCE_CHECKLIST.md.
            ConsentService consent = systems.AddComponent<ConsentService>();
            consent.Configure(assets.Ads);

            UnityAds mediation = systems.AddComponent<UnityAds>();

            AdService ads = systems.AddComponent<AdService>();
            ads.Configure(assets.Ads, consent, progress, mediation);

            GameObject uiObject = new GameObject("UI");
            UiRoot ui = uiObject.AddComponent<UiRoot>();
            ui.Bind(director, audio, assets.Juice, camera, assets.Cosmetics, cosmetics);
            ui.BindMonetisation(ads, consent);
        }

        private static void ConfigureImpulse(CinemachineImpulseSource source, JuiceConfig juice)
        {
            source.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Recoil;
            source.ImpulseDefinition.ImpulseDuration = juice == null ? 0.32f : juice.ShakeDuration;
            source.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            source.ImpulseDefinition.AmplitudeGain = 1f;
            source.ImpulseDefinition.FrequencyGain = juice == null ? 22f : juice.ShakeFrequency;
        }

        private static void MakeGlossy(Material material, float smoothness, float metallic, bool emission)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (emission && material.HasProperty("_EmissionColor"))
            {
                // The keyword has to be on for a MaterialPropertyBlock to be able to
                // drive the emission colour later; setting the property alone does
                // nothing while the variant without emission is compiled.
                //
                // Flagged RealtimeEmissive, not EmissiveIsBlack. URP 17 recomputes the
                // keyword every time it validates a material, and keeps it only while the
                // GI flags include an emissive bit (BaseShaderGUI.SetMaterialKeywords:
                // `flags & AnyEmissive`). EmissiveIsBlack, and None as well, silently
                // stripped it from these assets — and with it the emission variant from
                // every build: the pulse showed in the editor and not on a phone. The
                // project has no realtime GI, so the flag costs nothing. See
                // EmissiveMaterialsTests.
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(material);
        }

        private static void MakeAdditive(Material material)
        {
            ConfigureTransparent(material, BlendMode.One, BlendMode.One);
        }

        private static void MakeAlphaBlended(Material material)
        {
            ConfigureTransparent(material, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha);
        }

        /// <summary>
        /// Puts a URP material into transparent mode by hand.
        ///
        /// URP's surface options are a combination of a float, two blend modes, a
        /// keyword, a render queue and a render type tag. Setting only the inspector
        /// field leaves the shader variant unchanged, which shows up as a material
        /// that looks correct in the inspector and renders opaque in the game.
        /// </summary>
        private static void ConfigureTransparent(Material material, BlendMode source, BlendMode destination)
        {
            if (material == null)
            {
                return;
            }

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)source);
            material.SetFloat("_DstBlend", (float)destination);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Blend", source == BlendMode.One ? 2f : 0f);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            EditorUtility.SetDirty(material);
        }

        private static void SetCullOff(Material material)
        {
            if (material != null && material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", 0f);
                EditorUtility.SetDirty(material);
            }
        }

        private static void SetDiscShape(Material material, float softness, float fill, bool additive)
        {
            if (material == null)
            {
                return;
            }

            material.SetFloat("_Softness", softness);
            material.SetFloat("_Fill", fill);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.enableInstancing = true;

            EditorUtility.SetDirty(material);
        }

        private static PhysicsMaterial CreateRingPhysicsMaterial()
        {
            PhysicsMaterial material = EditorAssets.LoadOrCreateObject(
                DataFolder + "/RingPhysicsMaterial.asset",
                () => new PhysicsMaterial("RingPhysicsMaterial"));

            // Low bounce: a ring that pings off the peg reads as a bug, not as physics.
            material.bounciness = 0.08f;
            material.dynamicFriction = 0.4f;
            material.staticFriction = 0.45f;
            material.bounceCombine = PhysicsMaterialCombine.Minimum;
            material.frictionCombine = PhysicsMaterialCombine.Average;

            EditorUtility.SetDirty(material);
            return material;
        }

        private static PhysicsMaterial CreateGroundPhysicsMaterial()
        {
            PhysicsMaterial material = EditorAssets.LoadOrCreateObject(
                DataFolder + "/GroundPhysicsMaterial.asset",
                () => new PhysicsMaterial("GroundPhysicsMaterial"));

            // A missed ring should land, wobble and stop, not skitter off the board.
            material.bounciness = 0.12f;
            material.dynamicFriction = 0.62f;
            material.staticFriction = 0.7f;
            material.bounceCombine = PhysicsMaterialCombine.Minimum;
            material.frictionCombine = PhysicsMaterialCombine.Average;

            EditorUtility.SetDirty(material);
            return material;
        }

        private static PhysicsMaterial CreatePegPhysicsMaterial()
        {
            PhysicsMaterial material = EditorAssets.LoadOrCreateObject(
                DataFolder + "/PegPhysicsMaterial.asset",
                () => new PhysicsMaterial("PegPhysicsMaterial"));

            // Slight friction so the final settle around the peg is not slippery.
            material.bounciness = 0.05f;
            material.dynamicFriction = 0.55f;
            material.staticFriction = 0.6f;
            material.bounceCombine = PhysicsMaterialCombine.Minimum;
            material.frictionCombine = PhysicsMaterialCombine.Average;

            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
