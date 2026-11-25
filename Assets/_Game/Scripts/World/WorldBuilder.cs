using System.Collections.Generic;
using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.Juice;
using SlipperyZlope.RingToss.Utils;
using UnityEngine;

namespace SlipperyZlope.RingToss.World
{
    /// <summary>
    /// How much dressing the booth is built with.
    ///
    /// This exists because a scene that reads well on a monitor can be unusable on
    /// a phone. Everything here was composed and measured at 540x1200 — the right
    /// aspect — but *density* was judged on a desktop screen, which is a different
    /// question: a band of prizes that reads as texture at 27 inches reads as noise
    /// at four.
    ///
    /// Nothing is deleted at the lower settings. Every piece of scenery still
    /// exists in code with its reasoning attached and comes back by changing one
    /// field, which is the point — a density control is reversible in a way that
    /// deleting geometry is not.
    ///
    /// Ordered so a higher value means more, and a piece can ask "am I at least
    /// this dense" rather than listing every level it belongs to.
    /// </summary>
    public enum SceneryDensity
    {
        /// <summary>Only what the game needs to be legible.</summary>
        Clean,

        /// <summary>A fairground, without the parts that turn into noise.</summary>
        Calm,

        /// <summary>Everything that was ever built.</summary>
        Busy,
    }

    /// <summary>
    /// Builds the carnival the game is played in, and repaints it when the stage
    /// changes.
    ///
    /// The scene is generated at load rather than authored. There is no imported art
    /// in this project, so every shape here is a primitive or a procedural mesh, and
    /// the entire art direction is a palette plus these proportions — which means it
    /// can be read, reviewed and changed like any other code.
    ///
    /// Layout runs front to back: the booth counter the player throws over, the board
    /// the pegs stand on, a striped canvas backdrop stacked with prizes, a post-and-beam
    /// frame carrying the bunting and the string lights, and two layers of flat
    /// silhouettes for the rest of the fair.
    ///
    /// The proportions are driven by what the camera can see rather than by what a
    /// booth would really be shaped like. In portrait at a 41 degree field of view the
    /// frustum is a narrow wedge: at the backdrop it is only about five metres across,
    /// and everything above the beam is open sky. Anything built outside that wedge is
    /// invisible, and anything hung inside it whose supports are outside reads as
    /// floating. Both mistakes were in here.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class WorldBuilder : MonoBehaviour
    {
        [Header("Base materials")]
        [Tooltip("Cloned per role and recoloured. Never assigned to a renderer directly.")]
        [SerializeField] private Material litSource;

        [SerializeField] private Material unlitSource;
        [SerializeField] private Material softDiscSource;
        [SerializeField] private Material skyboxSource;

        [Header("Wind")]
        [Tooltip("Streaks of drifting air. Zero disables the whole thing.")]
        [Range(0, 40)]
        [SerializeField] private int windStreaks = 18;

        [Tooltip("Low bounce, so a missed ring lands and rolls instead of skipping away.")]
        [SerializeField] private PhysicsMaterial groundPhysicsMaterial;

        [Header("Layout")]
        [SerializeField] private float groundSize = 60f;
        [SerializeField] private float boardHalfWidth = 2.5f;
        [Tooltip("The board's near edge. Deliberately behind the camera's own ground " +
                 "line, so the lane runs off the bottom of the screen instead of " +
                 "ending in a metre and a half of empty floor.")]
        [SerializeField] private float boardFront = -5.2f;
        [SerializeField] private float boardBack = 1.9f;
        [SerializeField] private float counterZ = -5.3f;
        [SerializeField] private float backdropZ = 4.3f;

        [Tooltip("Height of the canvas wall. Every centimetre of this is a centimetre " +
                 "of sky the camera cannot see: see the note on beamHeight.")]
        [SerializeField] private float backdropHeight = 2.8f;
        [Tooltip("Half the width of the canvas wall.\n\n" +
                 "This number decides whether the booth can have neighbours at all. " +
                 "The camera sees 2.05 m either side of centre at the wall's depth, so " +
                 "a wall wider than that overhangs the frame and seals the booth off — " +
                 "at the old 3.4 m it overhung by more than a metre and nothing placed " +
                 "beside it in the same plane could ever be seen.\n\n" +
                 "The cost of narrowing it is peg backing. Every legally placed peg " +
                 "projects onto this plane within 1.81 m of centre, so at 1.65 m the " +
                 "outermost tenth of the fan is seen against the lane rather than " +
                 "against the canvas. That is why the neighbouring stalls are kept dark " +
                 "and unsaturated.")]
        [SerializeField] private float backdropHalfWidth = 1.65f;

        [Tooltip("Half the distance between the two visible booth posts. Sits just " +
                 "inside the camera's fan at the frame's depth, so the posts are in " +
                 "shot and the dressing strung between them visibly ends somewhere.\n\n" +
                 "Measured against the *narrowest* phone the game supports, not against " +
                 "a 9:16 screenshot. The camera's field of view is vertical, so a " +
                 "taller screen shows less width, not more — at 9:20 the frame is 80% " +
                 "of what a 9:16 capture suggests, and the first version of this booth " +
                 "put its own posts off the edge of a real handset.\n\n" +
                 "It is now also bounded from the other direction. Everything the booth " +
                 "owns has to stay inside the wall's own line of sight, or it stands in " +
                 "the window beside the wall and hides the neighbours — which is exactly " +
                 "what the posts, the side panels and the corner crates all did on the " +
                 "first attempt. See BoothLimitAt.")]
        [SerializeField] private float frameHalfWidth = 1.45f;

        [SerializeField] private float frameZ = 3.98f;

        [Tooltip("Height the beam sits at. Everything overhead hangs below it and the " +
                 "skyline shows above it.\n\n" +
                 "This number decides how much sky exists at all, and it is far more " +
                 "sensitive than it looks. The frame's top edge sits 8.5 degrees above " +
                 "horizontal; a beam at 3.86 m occludes up to 7.2 of those degrees and " +
                 "leaves a band of sky 29 pixels tall on a 1080x1920 screen. Nothing " +
                 "reads in 29 pixels, and the fairground behind the booth was being " +
                 "sliced into unrecognisable fragments of rim and roof. Dropping the " +
                 "beam half a metre triples the band.")]
        [SerializeField] private float beamHeight = 3.1f;

        [Header("Dressing")]
        [Tooltip("How much of the booth's dressing is built.\n\n" +
                 "Lower settings delete nothing: every piece still exists in code and " +
                 "comes back by changing this field.\n\n" +
                 "NOTE: serialized, so the value saved in the scene is what the game " +
                 "runs on. Changing the default here changes nothing.")]
        [SerializeField] private SceneryDensity density = SceneryDensity.Calm;

        [Range(0, 24)] [SerializeField] private int buntingPerLine = 13;
        [Range(0, 32)] [SerializeField] private int bulbCount = 13;
        [SerializeField] private float buntingSwaySpeed = 0.75f;
        [SerializeField] private float buntingSwayAmount = 7f;
        [SerializeField] private float ferrisWheelSpeed = 5.5f;

        [Header("Skyline")]
        [Tooltip("Depth of the stalls either side of the player's. Close enough that " +
                 "only their rooflines clear the booth's beam, which is what makes them " +
                 "read as next door rather than as more fairground.")]
        [SerializeField] private float midLayerZ = 15f;

        [SerializeField] private float nearLayerZ = 23f;
        [SerializeField] private float farLayerZ = 44f;

        [Tooltip("Degrees per second the swing rides turn about their masts.")]
        [SerializeField] private float swingRideSpeed = 26f;

        [SerializeField] private float carouselSpeed = 17f;

        [Tooltip("How long a sky balloon takes to cross the whole visible band, in seconds.")]
        [SerializeField] private float balloonDriftSeconds = 34f;

        [Tooltip("Elevation of the key light, in degrees. Taken from the theme's sun " +
                 "azimuth but deliberately not from its elevation.")]
        [Range(10f, 80f)]
        [SerializeField] private float keyLightPitch = 42f;

        [Header("Systems")]
        [SerializeField] private GameDirector director;
        [SerializeField] private Light keyLight;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly List<Transform> buntingLines = new List<Transform>();
        private readonly List<Transform> bulbs = new List<Transform>();
        private readonly List<MeshRenderer> bulbRenderers = new List<MeshRenderer>();
        private readonly List<Material> buntingMaterials = new List<Material>();
        private readonly List<Material> prizeMaterials = new List<Material>();
        private readonly List<Transform> ferrisWheels = new List<Transform>();
        private readonly List<Transform> balloonClusters = new List<Transform>();
        private readonly List<Transform> swingRides = new List<Transform>();
        private readonly List<Transform> carousels = new List<Transform>();
        private readonly List<Transform> skyBalloons = new List<Transform>();
        private readonly List<Transform> carnivalFolk = new List<Transform>();
        private readonly List<Transform> strikerPucks = new List<Transform>();
        private readonly List<float> strikerTravel = new List<float>();
        private readonly List<Transform> folkArms = new List<Transform>();

        /// <summary>Which side of its owner each arm hangs on, so the two swing apart.</summary>
        private readonly List<int> folkArmSigns = new List<int>();

        /// <summary>Per sky balloon: x is the height it starts at, y is how far it rises.</summary>
        private readonly List<Vector2> skyBalloonTravel = new List<Vector2>();

        private Material groundMaterial;
        private Material boothFloorMaterial;
        private Material boardMaterial;
        private FloorGlow floorGlow;
        private Material boardSeamMaterial;
        private Material chalkMaterial;
        private Material spareRingMaterial;
        private Material counterMaterial;
        private Material counterTopMaterial;
        private Material trimMaterial;
        private Material glowFrameMaterial;
        private Material glowSkirtMaterial;
        private Texture2D glowSkirtFade;
        private Material valanceMaterial;
        private Material backdropMaterial;
        private Material backdropStripeMaterial;
        private Material silhouetteMaterial;
        private Material neighbourMaterial;
        private Material neighbourCanvasMaterial;
        private Material neighbourTrimMaterial;
        private Material fairwayMaterial;
        private Material fairwayCanvasMaterial;
        private Material fairwayTrimMaterial;
        private Material folkSuitMaterial;
        private Material folkCoatMaterial;
        private Material folkSkinMaterial;
        private Material folkAccentMaterial;
        private Material skylineMidMaterial;
        private Material skylineFarMaterial;
        private Material postMaterial;
        private Material shelfMaterial;
        private Material crateMaterial;
        private Material bulbMaterial;
        private Material haloMaterial;
        private Material skyboxMaterial;

        private MaterialPropertyBlock block;
        private float animationTime;

        private Transform windRoot;
        private Material windMaterial;
        private Transform[] wind;
        private float[] windPhase;
        private Vector3 currentWind;

        private void Awake()
        {
            block = new MaterialPropertyBlock();

            CreateMaterials();
            BuildGround();
            BuildBoard();
            BuildBoardDeck();
            BuildWind();

            // The counter the player throws over. It reads as the booth wrapping round
            // you on a monitor, and on a phone it is a slab across the bottom third of
            // the frame: 0.87 m of woodwork half a metre from the lens, directly under
            // the ring, exactly where a thumb already is. The lane runs to the bottom
            // of the screen without it, which is the composition this game wants.
            if (AtLeast(SceneryDensity.Busy))
            {
                BuildCounter();
            }

            BuildBackdrop();
            BuildNeighbourStalls();
            BuildFairway();
            BuildBoothFrame();
            BuildBackShelf();
            BuildCornerStacks();
            BuildBoothFloor();
            BuildBoothSides();

            // The skirt under the counter. Only ever seen with the counter itself.
            if (AtLeast(SceneryDensity.Busy))
            {
                BuildBoothApron();
            }
            BuildHangingPrizes();
            BuildBalloons();
            BuildBunting();
            BuildStringLights();
            BuildSilhouettes();
        }

        private void Start()
        {
            // Nothing has raised LevelStarted yet, so without this the menus are shown
            // over a scene with every material still at its default white.
            if (director != null && director.Library != null && director.Library.Count > 0)
            {
                LevelConfig first = director.Library.Get(0);
                ApplyTheme(first == null ? null : first.Theme);
            }
        }

        private void OnEnable()
        {
            GameEvents.LevelStarted += OnLevelStarted;
            GameEvents.WindChanged += OnWindChanged;
        }

        private void OnDisable()
        {
            GameEvents.LevelStarted -= OnLevelStarted;
            GameEvents.WindChanged -= OnWindChanged;
        }

        private void OnDestroy()
        {
            // Runtime material clones are not owned by the asset database, so nothing
            // else will ever collect them.
            for (int i = 0; i < ownedMaterials.Count; i++)
            {
                if (ownedMaterials[i] != null)
                {
                    Destroy(ownedMaterials[i]);
                }
            }

            ownedMaterials.Clear();

            if (glowSkirtFade != null)
            {
                Destroy(glowSkirtFade);
                glowSkirtFade = null;
            }
        }

        public void Bind(
            Material lit,
            Material unlit,
            Material softDisc,
            Material skybox,
            PhysicsMaterial groundPhysics,
            GameDirector gameDirector,
            Light key)
        {
            litSource = lit;
            unlitSource = unlit;
            softDiscSource = softDisc;
            skyboxSource = skybox;
            groundPhysicsMaterial = groundPhysics;
            director = gameDirector;
            keyLight = key;
        }

        private Material Own(Material material)
        {
            ownedMaterials.Add(material);
            return material;
        }

        private void CreateMaterials()
        {
            groundMaterial = Own(new Material(litSource) { name = "Ground (runtime)" });
            boothFloorMaterial = Own(new Material(litSource) { name = "Booth Floor (runtime)" });
            boardMaterial = Own(new Material(litSource) { name = "Board (runtime)" });
            counterMaterial = Own(new Material(litSource) { name = "Counter (runtime)" });
            counterTopMaterial = Own(new Material(litSource) { name = "Counter Top (runtime)" });
            trimMaterial = Own(new Material(litSource) { name = "Trim (runtime)" });
            glowFrameMaterial = Own(new Material(litSource) { name = "Glow Frame (runtime)" });

            // Additive and unlit: what it draws is added to the floor behind it, which is
            // what light does and what a second painted band does not. Set up by hand
            // because URP's transparency is a set of properties a shader GUI normally
            // writes together, and nothing writes them for a material made in code.
            glowSkirtMaterial = Own(new Material(unlitSource) { name = "Glow Skirt (runtime)" });
            glowSkirtMaterial.SetFloat("_Surface", 1f);
            glowSkirtMaterial.SetFloat("_Blend", 1f);
            glowSkirtMaterial.SetFloat("_SrcBlend", 1f);
            glowSkirtMaterial.SetFloat("_DstBlend", 1f);
            glowSkirtMaterial.SetFloat("_ZWrite", 0f);
            glowSkirtMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glowSkirtMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            glowSkirtMaterial.renderQueue = 3000;

            // The falloff, as a one-dimensional texture across the band.
            //
            // WedgeBorder runs u from 0 at the outer edge — where the lit border is — to
            // 1 at the inner one, so this is simply the shape of the light leaving the
            // line. Without it the band is a rectangle of even brightness, which is not a
            // glow at all: it is a second, wider line, which is exactly what it looked
            // like and exactly what the owner said about it.
            glowSkirtFade = new Texture2D(64, 1, TextureFormat.RGBA32, false)
            {
                name = "Glow Falloff",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            for (int i = 0; i < glowSkirtFade.width; i++)
            {
                float across = i / (float)(glowSkirtFade.width - 1);

                // A gentle power rather than a square. Squared put nearly all of the
                // light in the first few centimetres, which read as a second hard line
                // beside the first — the owner's words were that it was "só outra linha".
                // This carries the light most of the way across the band before it goes.
                float fall = Mathf.Pow(1f - across, 1.45f);
                glowSkirtFade.SetPixel(i, 0, new Color(fall, fall, fall, fall));
            }

            glowSkirtFade.Apply();
            glowSkirtMaterial.SetTexture("_BaseMap", glowSkirtFade);
            valanceMaterial = Own(new Material(litSource) { name = "Valance (runtime)" });
            backdropMaterial = Own(new Material(litSource) { name = "Backdrop (runtime)" });
            backdropStripeMaterial = Own(new Material(litSource) { name = "Backdrop Stripe (runtime)" });
            postMaterial = Own(new Material(litSource) { name = "Post (runtime)" });
            shelfMaterial = Own(new Material(litSource) { name = "Shelf (runtime)" });
            crateMaterial = Own(new Material(litSource) { name = "Crate (runtime)" });
            boardSeamMaterial = Own(new Material(litSource) { name = "Board Seam (runtime)" });
            chalkMaterial = Own(new Material(litSource) { name = "Chalk (runtime)" });
            spareRingMaterial = Own(new Material(litSource) { name = "Spare Ring (runtime)" });
            neighbourMaterial = Own(new Material(litSource) { name = "Neighbour (runtime)" });
            neighbourCanvasMaterial = Own(new Material(litSource) { name = "Neighbour Canvas (runtime)" });
            neighbourTrimMaterial = Own(new Material(litSource) { name = "Neighbour Trim (runtime)" });
            folkSuitMaterial = Own(new Material(litSource) { name = "Folk Suit (runtime)" });
            folkCoatMaterial = Own(new Material(litSource) { name = "Folk Coat (runtime)" });
            folkSkinMaterial = Own(new Material(litSource) { name = "Folk Skin (runtime)" });
            folkAccentMaterial = Own(new Material(litSource) { name = "Folk Accent (runtime)" });
            silhouetteMaterial = Own(new Material(unlitSource) { name = "Silhouette (runtime)" });
            fairwayMaterial = Own(new Material(litSource) { name = "Fairway (runtime)" });
            fairwayCanvasMaterial = Own(new Material(litSource) { name = "Fairway Canvas (runtime)" });
            fairwayTrimMaterial = Own(new Material(litSource) { name = "Fairway Trim (runtime)" });
            skylineMidMaterial = Own(new Material(unlitSource) { name = "Skyline Mid (runtime)" });
            skylineFarMaterial = Own(new Material(unlitSource) { name = "Skyline Far (runtime)" });

            // Silhouettes are single-sided fans and flat tent shapes; a layer seen
            // slightly off-axis would otherwise disappear from one side.
            Material[] flat = { silhouetteMaterial, skylineMidMaterial, skylineFarMaterial };

            for (int i = 0; i < flat.Length; i++)
            {
                if (flat[i].HasProperty("_Cull"))
                {
                    flat[i].SetFloat("_Cull", 0f);
                }
            }

            for (int i = 0; i < 3; i++)
            {
                prizeMaterials.Add(Own(new Material(litSource) { name = $"Prize {i} (runtime)" }));
            }

            bulbMaterial = Own(new Material(unlitSource) { name = "Bulb (runtime)" });
            haloMaterial = Own(new Material(softDiscSource) { name = "Halo (runtime)" });

            // Pennants are single triangles seen from both sides as they sway.
            for (int i = 0; i < 3; i++)
            {
                Material pennant = Own(new Material(unlitSource) { name = $"Bunting {i} (runtime)" });

                if (pennant.HasProperty("_Cull"))
                {
                    pennant.SetFloat("_Cull", 0f);
                }

                buntingMaterials.Add(pennant);
            }

            if (skyboxSource != null)
            {
                skyboxMaterial = Own(new Material(skyboxSource) { name = "Sky (runtime)" });
                RenderSettings.skybox = skyboxMaterial;
            }
        }

        private void BuildGround()
        {
            GameObject ground = GeometryFactory.CreateMeshObject(
                "Ground", transform, MeshFactory.Disc(groundSize * 0.5f, 48), groundMaterial,
                castShadows: false, receiveShadows: true);
            ground.transform.localPosition = Vector3.zero;

            // The scenery is built from meshes and from primitives whose colliders are
            // stripped, so without this the world has no floor at all: a missed ring
            // falls forever and is only ever resolved by the out-of-bounds check. It
            // reads as the ring vanishing rather than as a miss.
            //
            // A box rather than a plane: an infinite plane would also catch the
            // silhouettes far behind the booth, and a box can be given real thickness
            // so a fast ring cannot tunnel through it.
            GameObject floor = GeometryFactory.CreateEmpty("Floor Collider", transform);
            floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);

            BoxCollider collider = floor.AddComponent<BoxCollider>();
            collider.size = new Vector3(groundSize, 1f, groundSize);
            collider.sharedMaterial = groundPhysicsMaterial;
        }

        /// <summary>
        /// The mat the pegs stand on. Slightly proud of the ground and trimmed, so the
        /// playfield has an edge and the player can read how deep a throw has to go.
        /// </summary>
        /// <summary>
        /// The board, cut to the shape the camera can see.
        ///
        /// It used to be a five-metre slab, and the camera sees 2.19 m at its front and
        /// 3.66 m at its back — so it ran off both edges of the screen at every depth
        /// and the player never saw an edge of the thing they were throwing at. The
        /// board is now a wedge whose sides follow <see cref="BoardHalfWidthAt"/>,
        /// which is the frustum with a margin, capped at the authored width.
        ///
        /// That margin is not decoration: every legal peg sits inside
        /// <see cref="CameraFraming.SafeHalfWidthAt"/>, and the board is 6% wider than
        /// that, so no peg can ever end up standing off the edge of its own board.
        /// </summary>
        private void BuildBoard()
        {
            Transform board = GeometryFactory.CreateEmpty("Board", transform).transform;

            // One width for the whole board, wide enough to leave the frame at every
            // depth. The rim is therefore only ever seen along the back edge, which is
            // the only edge that says anything: it is the line a throw has to stay
            // short of. The sides said nothing and cost the composition its floor.
            float halfWidth = BoardHalfWidthAt(boardBack);

            // Metres past the deck rather than a fraction of it. A fraction only had a
            // job while the two ends were different widths.
            const float rim = 0.17f;

            GameObject trim = GeometryFactory.CreateMeshObject(
                "Trim", board,
                MeshFactory.Wedge(
                    boardFront - 0.06f, halfWidth + rim,
                    boardBack + rim, halfWidth + rim),
                trimMaterial, receiveShadows: true);
            trim.transform.localPosition = new Vector3(0f, 0.012f, 0f);

            GameObject surface = GeometryFactory.CreateMeshObject(
                "Surface", board,
                MeshFactory.Wedge(
                    boardFront, halfWidth,
                    boardBack, halfWidth),
                boardMaterial, receiveShadows: true);
            surface.transform.localPosition = new Vector3(0f, BoardMetrics.DeckSurfaceY, 0f);

            // The lit border, and the third answer to one note.
            //
            // It was the deck, then it was the trim. The trim is a wedge 0.17 m larger
            // than the deck on every side, and the code that built it says as much: "the
            // rim is therefore only ever seen along the back edge". So the one part of it
            // the camera could see was the strip *behind* the board — the owner's words
            // were "só fica brilhando atrás do cenário, que é justamente onde não era
            // para brilhar", and they were describing the geometry exactly.
            //
            // This traces the deck's own outline instead, inset into it and lying on top
            // of it. What lights up is now the boundary of the square a ring can land in,
            // which is what was asked for both times.
            // The spill first, so the bright core draws over it.
            GameObject glowSkirt = GeometryFactory.CreateMeshObject(
                "Glow Skirt", board,
                MeshFactory.WedgeBorder(
                    boardFront, halfWidth,
                    boardBack, halfWidth,
                    BoardMetrics.GlowSkirtWidth),
                glowSkirtMaterial);
            glowSkirt.transform.localPosition =
                new Vector3(0f, BoardMetrics.GroundFxY - 0.004f, 0f);

            GameObject glowFrame = GeometryFactory.CreateMeshObject(
                "Glow Frame", board,
                MeshFactory.WedgeBorder(
                    boardFront, halfWidth,
                    boardBack, halfWidth,
                    BoardMetrics.GlowBorderWidth),
                glowFrameMaterial);
            glowFrame.transform.localPosition = new Vector3(0f, BoardMetrics.GroundFxY, 0f);

            // The keyword goes on this one material, and its emission stays black until
            // FloorGlow gives it some. The trim is scenery again and never lights.
            EnableEmission(glowFrameMaterial);

            floorGlow = GetComponent<FloorGlow>();

            if (floorGlow == null)
            {
                floorGlow = gameObject.AddComponent<FloorGlow>();
            }

            floorGlow.Bind(
                glowFrame.GetComponent<Renderer>(),
                glowSkirt.GetComponent<Renderer>(),
                director == null ? null : director.Juice);
        }

        private static void EnableEmission(Material material)
        {
            if (material == null || !material.HasProperty(ShaderIds.EmissionColor))
            {
                return;
            }

            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            material.SetColor(ShaderIds.EmissionColor, Color.black);
        }

        /// <summary>
        /// How wide the board is: the authored slab, and never narrower than the frame
        /// it has to run off the sides of.
        ///
        /// It took the depth for a while and returned a wedge cut to the frustum, so
        /// the player could see the board's own edges converging. That is defensible
        /// and it was not what the game wanted: a booth floor that stops short of the
        /// sides of the screen reads as a rug laid on the fairground rather than as the
        /// floor of the place, and the two converging edges pull the eye to the corners
        /// of the frame, which is the one place nothing ever happens.
        ///
        /// The depth is still taken so the floor cannot be narrower than the frame at
        /// the back edge if the board is ever made deeper.
        /// </summary>
        private float BoardHalfWidthAt(float z) =>
            Mathf.Max(boardHalfWidth, CameraFraming.HalfWidthAt(z) * 1.08f);

        /// <summary>The booth counter in the near foreground, which the player throws over.</summary>
        private void BuildCounter()
        {
            Transform counter = GeometryFactory.CreateEmpty("Counter", transform).transform;

            GameObject body = GeometryFactory.CreatePrimitive(
                "Body", counter, PrimitiveType.Cube, counterMaterial, castShadows: true);
            body.transform.localPosition = new Vector3(0f, 0.42f, counterZ);
            body.transform.localScale = new Vector3(7.5f, 0.84f, 0.5f);

            GameObject top = GeometryFactory.CreatePrimitive(
                "Top", counter, PrimitiveType.Cube, counterTopMaterial, castShadows: true);
            top.transform.localPosition = new Vector3(0f, 0.87f, counterZ);
            top.transform.localScale = new Vector3(7.7f, 0.1f, 0.66f);
        }

        /// <summary>
        /// The deck the pegs stand on, and the ground in front of it.
        ///
        /// The board was a single flat slab covering the bottom third of the frame with
        /// nothing on it, which is the largest unbroken area in the picture and the one
        /// the player looks at most. Nothing can *stand* on it — the visible wedge is
        /// only 0.84 m wide at the front and every peg is inside it, so a prop there
        /// would sit in the play area — so it is filled the only way it can be: flat.
        ///
        /// Seams across rather than along. Both are honest ways to lay a deck, but they
        /// cue different things: seams running away from the camera converge and tell
        /// you about sideways position, seams running across compress with distance and
        /// tell you about *depth*, which is the only judgement a ring toss actually
        /// asks for. They are also the reason the board no longer reads as a backdrop
        /// the pegs are pasted on.
        ///
        /// Kept very close to the board's own colour. The pegs have to stay the only
        /// saturated thing on screen, and a deck with visible joinery under a flying
        /// ring is worse than a plain one.
        /// </summary>
        private void BuildBoardDeck()
        {
            Transform root = GeometryFactory.CreateEmpty("Deck", transform).transform;

            // Eleven rather than seven. Seams running across the board compress with
            // distance, which is the only thing on the deck that says how deep it is,
            // and testers reported they could not read depth at all. More of them is a
            // stronger gradient for no extra material and no extra draw call.
            const int seams = 11;

            for (int i = 0; i < seams; i++)
            {
                float z = Mathf.Lerp(boardFront + 0.5f, boardBack - 0.2f, (float)i / (seams - 1));

                // Inset from the board's own edge, so the trim reads as a rim rather
                // than as the last seam.
                float span = BoardHalfWidthAt(z) * 0.94f;

                GameObject seam = GeometryFactory.CreatePrimitive(
                    $"Seam_{i}", root, PrimitiveType.Cube, boardSeamMaterial);
                seam.transform.localPosition = new Vector3(0f, 0.043f, z);
                seam.transform.localScale = new Vector3(span * 2f, 0.006f, 0.035f);
            }

            // And the joints between them. Seams one way alone read as a running
            // track; crossed, they read as a floor. These are the quieter of the two —
            // they converge on the vanishing point and would draw the eye up the middle
            // of the board if they matched the cross seams for weight.
            float[] joints = { -1.45f, -0.85f, -0.25f, 0.25f, 0.85f, 1.45f };

            for (int i = 0; i < joints.Length; i++)
            {
                // Each starts where the board is finally wide enough to hold it.
                // Solved against the board rather than against the frame, so a joint
                // can never run out over the edge of the deck it is drawn on — the
                // board is a wedge now and the two stopped being the same line.
                float from = boardFront + 0.1f;

                while (from < boardBack && BoardHalfWidthAt(from) * 0.9f < Mathf.Abs(joints[i]))
                {
                    from += 0.05f;
                }

                if (from >= boardBack - 0.2f)
                {
                    continue;
                }

                GameObject joint = GeometryFactory.CreatePrimitive(
                    $"Joint_{i}", root, PrimitiveType.Cube, boardSeamMaterial);
                joint.transform.localPosition =
                    new Vector3(joints[i], 0.042f, (from + boardBack - 0.2f) * 0.5f);
                joint.transform.localScale =
                    new Vector3(0.028f, 0.005f, boardBack - 0.2f - from);
            }

            // The line the player throws from, chalked on the ground rather than on the
            // board. It sits in the band between the board's front edge and the bottom
            // of the frame, which is otherwise bare floor.
            // On the board rather than in front of it. The apron hides everything
            // below 0.12 m at the board's front edge, so a line chalked on the ground
            // where the player stands is a line nobody sees.
            GameObject chalk = GeometryFactory.CreatePrimitive(
                "Throw Line", root, PrimitiveType.Cube, chalkMaterial);
            chalk.transform.localPosition = new Vector3(0f, 0.044f, boardFront + 1f);
            chalk.transform.localScale = new Vector3(2.1f, 0.006f, 0.05f);
        }

        /// <summary>
        /// What is on the floor of the booth, between the back of the board and the
        /// shelf.
        ///
        /// A strip about seventy centimetres deep, and the only place in the whole
        /// lower half of the frame where something can stand without being in the way
        /// of a ring. Everything here is under half a metre tall and sits in the band
        /// between the board's back edge and the underside of the shelf — measured, not
        /// guessed: the shelf plank hides everything below its own sight line, which is
        /// 0.31 m at three and a half metres, and the corner crates throw a shadow of
        /// their own from 0.85 to 1.26 m across at that depth. The gap between those two
        /// is where this goes.
        ///
        /// It is the booth's own clutter rather than more scenery: spare hoops, a crate
        /// of prizes, a sack. Two rings lie where they were dropped, which is the one
        /// detail here that says somebody has been playing this game rather than that
        /// the booth was just built.
        /// </summary>
        private void BuildBoothFloor()
        {
            Transform root = GeometryFactory.CreateEmpty("Booth Floor", transform).transform;

            // The floor of the booth itself, from where the board's rim ends to the foot
            // of the frame. Until now this strip was the fairground's ground disc showing
            // through — and on the owner's phone that strip came out as a bright band, tan
            // in the early themes and light cyan by the Midnight rounds, which read as the
            // whole floor lighting up. The editor draws the same strip dark, as intended,
            // and nothing in the scene explains the difference; the ground disc is the one
            // mesh here built from thirty-metre triangles. So the player never looks at it
            // there any more: this is a floor of its own, in shade, and the only light on
            // the floor is the landing glow on the board.
            //
            // It starts at the trim's back edge rather than the board's, so the rim — the
            // line a throw has to stay short of — stays visible.
            float floorHalfWidth = BoardHalfWidthAt(frameZ) + 0.2f;
            GameObject slab = GeometryFactory.CreateMeshObject(
                "Floor", root,
                MeshFactory.Wedge(boardBack + 0.17f, floorHalfWidth, frameZ - 0.15f, floorHalfWidth),
                boothFloorMaterial, castShadows: false, receiveShadows: true);

            // Three centimetres up: clear of the ground by more than a sixteen-bit depth
            // buffer can confuse at this distance, and too little to see under a crate.
            slab.transform.localPosition = new Vector3(0f, 0.03f, 0f);

            BuildDroppedHoops(root);

            // Everything below this line stands up, and standing things in the corners
            // are what a small screen reads as clutter. The two hoops lying flat above
            // cost nothing and say the same thing.
            if (!AtLeast(SceneryDensity.Calm))
            {
                return;
            }

            // A pail of spare hoops.
            Transform pail = GeometryFactory.CreateEmpty("Hoop Pail", root).transform;
            pail.localPosition = new Vector3(-0.78f, 0f, 2.95f);

            GameObject body = GeometryFactory.CreatePrimitive(
                "Body", pail, PrimitiveType.Cylinder, crateMaterial, castShadows: true);
            body.transform.localPosition = new Vector3(0f, 0.17f, 0f);
            body.transform.localScale = new Vector3(0.38f, 0.17f, 0.38f);

            GameObject rim = GeometryFactory.CreatePrimitive(
                "Rim", pail, PrimitiveType.Cylinder, shelfMaterial);
            rim.transform.localPosition = new Vector3(0f, 0.33f, 0f);
            rim.transform.localScale = new Vector3(0.41f, 0.02f, 0.41f);

            Mesh hoop = MeshFactory.Torus(0.17f, 0.035f, 14, 7);

            for (int i = 0; i < 3; i++)
            {
                GameObject spare = GeometryFactory.CreateMeshObject(
                    $"Hoop_{i}", pail, hoop, spareRingMaterial, castShadows: true);
                spare.transform.localPosition = new Vector3(-0.02f + i * 0.03f, 0.38f, -0.04f);
                spare.transform.localRotation = Quaternion.Euler(72f, 8f - i * 9f, i * 6f);
            }

            // A sack, slumped. A capsule on its side is the whole shape.
            // Darker than the crates around it. At the valance's warm cream it was the
            // lightest thing below the shelf and read as a bolster lying in the light
            // rather than as a sack on a floor in shade.
            GameObject sack = GeometryFactory.CreatePrimitive(
                "Sack", root, PrimitiveType.Capsule, shelfMaterial, castShadows: true);
            sack.transform.localPosition = new Vector3(-0.24f, 0.13f, 3.28f);
            sack.transform.localRotation = Quaternion.Euler(0f, 24f, 68f);
            sack.transform.localScale = new Vector3(0.26f, 0.21f, 0.26f);

            // A crate with the prizes still in it.
            Transform crate = GeometryFactory.CreateEmpty("Prize Crate", root).transform;
            // Held inside the corner crates' own shadow line. They stand at 1.0 m and
            // hide from 0.86 to 1.26 m across at this depth, and the first placement of
            // this crate put its lit half squarely inside that.
            crate.localPosition = new Vector3(0.7f, 0f, 3.16f);
            crate.localRotation = Quaternion.Euler(0f, -13f, 0f);

            GameObject box = GeometryFactory.CreatePrimitive(
                "Box", crate, PrimitiveType.Cube, crateMaterial, castShadows: true);
            box.transform.localPosition = new Vector3(0f, 0.17f, 0f);
            box.transform.localScale = new Vector3(0.46f, 0.34f, 0.4f);

            for (int i = 0; i < 2; i++)
            {
                GameObject prize = GeometryFactory.CreatePrimitive(
                    $"Prize_{i}", crate, i == 0 ? PrimitiveType.Sphere : PrimitiveType.Capsule,
                    prizeMaterials[(i + 2) % prizeMaterials.Count], castShadows: true);
                prize.transform.localPosition = new Vector3(-0.09f + i * 0.17f, 0.4f, 0.02f);
                prize.transform.localScale = i == 0
                    ? Vector3.one * 0.2f
                    : new Vector3(0.14f, 0.12f, 0.14f);
            }

        }

        /// <summary>
        /// Two hoops lying where they were dropped.
        ///
        /// Flat, so a ring that overshoots and comes to rest here cannot end up inside
        /// them — and flat is also why they survive every density: they add nothing to
        /// the silhouette of the corners, which is the thing a small screen struggles
        /// with, while still saying somebody has been playing this game.
        /// </summary>
        private void BuildDroppedHoops(Transform root)
        {
            Vector3[] dropped = { new Vector3(-0.5f, 0.055f, 2.76f), new Vector3(0.6f, 0.055f, 2.84f) };
            float[] turns = { 18f, -34f };

            for (int i = 0; i < dropped.Length; i++)
            {
                GameObject lying = GeometryFactory.CreateMeshObject(
                    $"Dropped Hoop_{i}", root, MeshFactory.Torus(0.23f, 0.045f, 16, 8),
                    spareRingMaterial, castShadows: true);
                lying.transform.localPosition = dropped[i];
                lying.transform.localRotation = Quaternion.Euler(90f, turns[i], 0f);
            }
        }

        /// <summary>
        /// The front of the booth, between the player and the board.
        ///
        /// This is the last piece of the frame that had nothing in it, and it is the
        /// hardest-won: the peg fan owns almost the entire lower half of the screen.
        /// Pegs can be placed anywhere from two to 5.9 metres out and up to seventeen
        /// degrees either side, and the union of every position a generator may pick
        /// covers the frame from y=520 down to y=989 on a 540x1200 portrait screen,
        /// across every column but the outermost dozen. Anything standing in that band
        /// and nearer than the board can hide a peg on some level nobody has played
        /// yet — which is the one failure this game cannot ship.
        ///
        /// So the apron stops at 0.8 m, which is exactly where the nearest possible
        /// peg's base projects. That leaves about ninety pixels of frame between it and
        /// the HUD, full width, and every centimetre of that is used: a striped face
        /// matching the wall behind, a scalloped valance, a counter rail, and the spare
        /// hoops hung along the front the way a booth actually stores them.
        ///
        /// The other half of the trade is what it hides. An apron this near occludes
        /// everything below its own sight line, which at the board's front edge is
        /// 0.12 m — so the board's front trim goes, and the throw line had to move back
        /// a metre to stay in shot. That is the correct trade: a line on the floor is
        /// worth less than a booth the player is standing inside.
        /// </summary>
        private void BuildBoothApron()
        {
            Transform root = GeometryFactory.CreateEmpty("Apron", transform).transform;

            const float z = -4.3f;
            const float face = 0.76f;
            const float rail = 0.8f;

            // Wider than the frame at this depth on purpose. The apron is 0.6 m from
            // edge to edge of the screen here, and a panel cut to exactly that would
            // show its own ends the moment the aspect got any wider.
            const float half = 0.92f;

            GameObject body = GeometryFactory.CreatePrimitive(
                "Face", root, PrimitiveType.Cube, counterMaterial, castShadows: true);
            body.transform.localPosition = new Vector3(0f, face * 0.5f, z);
            body.transform.localScale = new Vector3(half * 2f, face, 0.36f);

            GameObject top = GeometryFactory.CreatePrimitive(
                "Rail", root, PrimitiveType.Cube, counterTopMaterial, castShadows: true);
            top.transform.localPosition = new Vector3(0f, (face + rail) * 0.5f, z - 0.055f);
            top.transform.localScale = new Vector3(half * 2f + 0.06f, rail - face, 0.48f);

            const int scallops = 11;

            for (int i = 0; i < scallops; i++)
            {
                float t = (i + 0.5f) / scallops;

                GameObject scallop = GeometryFactory.CreatePrimitive(
                    $"Scallop_{i:D2}", root, PrimitiveType.Sphere, valanceMaterial);
                scallop.transform.localPosition = new Vector3(
                    Mathf.Lerp(-half, half, t), face - 0.06f, z - 0.215f);
                scallop.transform.localScale =
                    new Vector3(half * 2f / scallops * 0.94f, 0.13f, 0.09f);
            }

            // What is actually seen of this apron is the rail's top surface: the
            // camera looks down on it, the face below is a seventeen pixel band and
            // everything under that is behind the HUD. A first version put nine stripes
            // and five hung hoops on the face, all of it below the fold. So the detail
            // is on the top, and it is flat, because 0.80 m is exactly where the nearest
            // possible peg's base lands and anything standing here would cover it.
            const int planks = 5;

            for (int i = 0; i < planks; i++)
            {
                float t = (i + 0.5f) / planks;

                GameObject seam = GeometryFactory.CreatePrimitive(
                    $"Seam_{i}", root, PrimitiveType.Cube, counterMaterial);
                seam.transform.localPosition = new Vector3(
                    Mathf.Lerp(-half, half, t), rail + 0.004f, z - 0.055f);
                seam.transform.localScale = new Vector3(0.022f, 0.006f, 0.46f);
            }

            // Two hoops waiting on the counter. Flat, so they cannot hide anything, and
            // the same shape as the one in the player's hand — which is about as direct
            // a statement of what this booth sells as the picture can make.
            Mesh spare = MeshFactory.Torus(0.15f, 0.026f, 18, 8);

            for (int i = -1; i <= 1; i += 2)
            {
                GameObject hoop = GeometryFactory.CreateMeshObject(
                    $"Hoop_{(i < 0 ? "L" : "R")}", root, spare, spareRingMaterial, castShadows: true);
                hoop.transform.localPosition = new Vector3(i * 0.52f, rail + 0.008f, z - 0.08f - i * 0.06f);
                hoop.transform.localRotation = Quaternion.Euler(90f, i * 17f, 0f);
            }
        }

        /// <summary>
        /// Crates and sacks down the inside of the booth, either side of the board.
        ///
        /// These sit *behind* every peg, and that one fact is what makes them possible
        /// at all. A prop nearer than the board can cover a peg and is therefore
        /// confined to the strip below the whole fan; a prop past the board's back edge
        /// can only ever be covered *by* a peg, which is what depth is supposed to do.
        /// So this is the one part of the lower frame where something can be built at
        /// full height, and it lands exactly where the picture was emptiest: the left
        /// and right thirds, just above the back of the board.
        ///
        /// Each is anchored on <see cref="BoothLimitAt"/> so it stays inside the wall's
        /// own line of sight. A centimetre past that and it is standing in the window
        /// the neighbouring stalls are seen through, which is a trade this scene has
        /// already made once and does not need to make again.
        /// </summary>
        private void BuildBoothSides()
        {
            Transform root = GeometryFactory.CreateEmpty("Booth Sides", transform).transform;

            // depth, half width, height, and how far the stack is turned.
            float[] depths = { 2.75f, 3.45f, 4.05f };
            float[] widths = { 0.3f, 0.3f, 0.28f };
            float[] heights = { 0.84f, 1.24f, 1.04f };

            // Three stacks a side fill the lower corners on a monitor and close the
            // frame in on a phone. One is enough to say the booth has sides.
            int stacks = Some(0, 1, depths.Length);

            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < stacks; i++)
                {
                    // Staggered, so the two sides are not the same three boxes mirrored.
                    float z = depths[i] + (side < 0 ? 0f : 0.12f);
                    float width = widths[i];
                    float height = heights[i] * (side < 0 ? 1f : 0.92f);

                    Transform stack = GeometryFactory
                        .CreateEmpty($"Stack_{(side < 0 ? "L" : "R")}{i}", root).transform;

                    stack.localPosition =
                        new Vector3(side * (BoothLimitAt(z) - width - 0.02f), 0f, z);
                    stack.localRotation = Quaternion.Euler(0f, side * (11f + i * 7f), 0f);

                    GameObject lower = GeometryFactory.CreatePrimitive(
                        "Crate", stack, PrimitiveType.Cube, crateMaterial, castShadows: true);
                    lower.transform.localPosition = new Vector3(0f, height * 0.32f, 0f);
                    lower.transform.localScale =
                        new Vector3(width * 2f, height * 0.64f, width * 1.7f);

                    // The top of each stack is a different object, so a row of three
                    // does not read as three of the same box at three sizes.
                    if (i == 1)
                    {
                        // Small, and the crates' own colour. At a metre and a half long
                        // in the shelf's dark brown these read as two enormous sausages
                        // laid across the booth, which is not what a sack looks like at
                        // any size.
                        GameObject sack = GeometryFactory.CreatePrimitive(
                            "Sack", stack, PrimitiveType.Capsule, crateMaterial, castShadows: true);
                        sack.transform.localPosition = new Vector3(0f, height * 0.74f, -0.04f);
                        sack.transform.localRotation = Quaternion.Euler(0f, 0f, 68f);
                        sack.transform.localScale = new Vector3(width * 0.86f, width * 0.9f, width * 0.86f);
                    }
                    else if (i == 0)
                    {
                        GameObject barrel = GeometryFactory.CreatePrimitive(
                            "Barrel", stack, PrimitiveType.Cylinder, valanceMaterial, castShadows: true);
                        barrel.transform.localPosition = new Vector3(width * 0.1f, height * 0.82f, -0.05f);
                        barrel.transform.localScale =
                            new Vector3(width * 1.25f, height * 0.2f, width * 1.25f);
                    }
                    else
                    {
                        GameObject upper = GeometryFactory.CreatePrimitive(
                            "Crate Upper", stack, PrimitiveType.Cube, crateMaterial, castShadows: true);
                        upper.transform.localPosition = new Vector3(-width * 0.16f, height * 0.82f, 0.03f);
                        upper.transform.localRotation = Quaternion.Euler(0f, side * -21f, 0f);
                        upper.transform.localScale =
                            new Vector3(width * 1.4f, height * 0.36f, width * 1.3f);
                    }

                    // A hoop propped against the front of the nearest stack, so the row
                    // has something in it that is not a box.
                    if (i == 0)
                    {
                        GameObject leaning = GeometryFactory.CreateMeshObject(
                            "Leaning Hoop", stack, MeshFactory.Torus(0.22f, 0.042f, 16, 8),
                            spareRingMaterial, castShadows: true);
                        leaning.transform.localPosition =
                            new Vector3(side * -width * 0.7f, 0.23f, -width * 1.1f);
                        leaning.transform.localRotation = Quaternion.Euler(0f, side * -24f, side * 13f);
                    }
                }
            }
        }

        /// <summary>
        /// The posts and beam that hold the booth up, and that everything overhead
        /// hangs from.
        ///
        /// This exists because of what the camera can actually see. Portrait at a 41
        /// degree field of view, pitched down twelve degrees from just behind the
        /// counter, leaves a wedge of open sky across the top of the frame above the
        /// backdrop and nothing else. Anything hung in that wedge with its ends past
        /// the frame edge reads as floating, because the thing holding it up is off
        /// screen. Bringing the posts inside the frame and hanging the dressing
        /// between them is the whole fix: every wire now visibly ends somewhere.
        /// </summary>
        private void BuildBoothFrame()
        {
            Transform frame = GeometryFactory.CreateEmpty("Booth Frame", transform).transform;

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject post = GeometryFactory.CreatePrimitive(
                    $"Post_{(side < 0 ? "L" : "R")}", frame, PrimitiveType.Cube, postMaterial,
                    castShadows: true);
                post.transform.localPosition =
                    new Vector3(side * frameHalfWidth, beamHeight * 0.5f, frameZ);
                post.transform.localScale = new Vector3(0.26f, beamHeight, 0.3f);

                // A collar near the top, so the beam reads as resting on the post
                // rather than passing through it.
                GameObject collar = GeometryFactory.CreatePrimitive(
                    $"Collar_{(side < 0 ? "L" : "R")}", frame, PrimitiveType.Cube, valanceMaterial);
                collar.transform.localPosition =
                    new Vector3(side * frameHalfWidth, beamHeight - 0.28f, frameZ);
                collar.transform.localScale = new Vector3(0.36f, 0.12f, 0.4f);
            }

            GameObject beam = GeometryFactory.CreatePrimitive(
                "Beam", frame, PrimitiveType.Cube, postMaterial, castShadows: true);
            beam.transform.localPosition = new Vector3(0f, beamHeight + 0.09f, frameZ);
            // Runs a little past the posts and stops just inside the frame. A beam
            // that ends short of the screen edge reads as a plank; one that runs off it
            // reads as structure continuing past the shot, which is what it is.
            beam.transform.localScale = new Vector3(frameHalfWidth * 2f + 0.36f, 0.22f, 0.34f);

            // Scalloped valance hanging under the beam: a row of half-buried spheres,
            // which is the cheapest way to get a fairground silhouette out of
            // primitives.
            const int scallops = 11;

            for (int i = 0; i < scallops; i++)
            {
                float t = (i + 0.5f) / scallops;
                float x = Mathf.Lerp(-frameHalfWidth - 0.16f, frameHalfWidth + 0.16f, t);

                GameObject scallop = GeometryFactory.CreatePrimitive(
                    $"Scallop_{i:D2}", frame, PrimitiveType.Sphere, valanceMaterial);
                scallop.transform.localPosition = new Vector3(x, beamHeight - 0.02f, frameZ - 0.06f);

                // Small enough to read as a trim. At half again this size they are a
                // row of brown lumps sitting on the frame.
                scallop.transform.localScale = new Vector3(0.4f, 0.26f, 0.18f);
            }
        }

        /// <summary>The striped canvas wall behind the board, and what is stacked on it.</summary>
        private void BuildBackdrop()
        {
            Transform backdrop = GeometryFactory.CreateEmpty("Backdrop", transform).transform;

            GameObject wall = GeometryFactory.CreatePrimitive(
                "Wall", backdrop, PrimitiveType.Cube, backdropMaterial, receiveShadows: true);
            wall.transform.localPosition = new Vector3(0f, backdropHeight * 0.5f, backdropZ);
            wall.transform.localScale = new Vector3(backdropHalfWidth * 2f, backdropHeight, 0.18f);

            const int stripes = 7;

            for (int i = 0; i < stripes; i++)
            {
                float t = (i + 0.5f) / stripes;
                float x = Mathf.Lerp(-backdropHalfWidth, backdropHalfWidth, t);

                GameObject stripe = GeometryFactory.CreatePrimitive(
                    $"Stripe_{i:D2}", backdrop, PrimitiveType.Cube, backdropStripeMaterial);
                stripe.transform.localPosition = new Vector3(x, backdropHeight * 0.5f, backdropZ - 0.1f);
                stripe.transform.localScale =
                    new Vector3(backdropHalfWidth * 2f / stripes * 0.5f, backdropHeight * 0.98f, 0.04f);
            }

            // Side walls running forward from the canvas. They used to be sealing
            // panels a metre and a half outside the frame, put there so no sky could
            // appear beside the wall — which is precisely what was stopping the booth
            // from having neighbours. Now they are at the wall's own edge, where they
            // are seen at a steep angle and read as the booth's own sides, with the
            // lane and the next stall along visible past them.
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject panel = GeometryFactory.CreatePrimitive(
                    $"Side_{(side < 0 ? "L" : "R")}", backdrop, PrimitiveType.Cube,
                    backdropMaterial, receiveShadows: true);
                // Laid along the sight line over the wall's own edge rather than
                // straight down the Z axis. A panel parallel to Z swings outward in
                // angle as it comes towards the camera and ends up further into the
                // window than the wall it is attached to; one laid along the sight line
                // stays exactly inside it however long it is, and is seen edge-on,
                // which is what the side of a booth looks like from in front of it.
                float slope = backdropHalfWidth / (backdropZ - CameraFraming.CameraZ);
                float lean = Mathf.Atan(slope) * Mathf.Rad2Deg;

                panel.transform.localPosition = new Vector3(
                    side * (BoothLimitAt(backdropZ - 0.5f) - 0.09f),
                    backdropHeight * 0.5f, backdropZ - 0.5f);
                panel.transform.localRotation = Quaternion.Euler(0f, side * lean, 0f);
                panel.transform.localScale = new Vector3(0.16f, backdropHeight, 1.1f);
            }

            BuildPrizeShelves(backdrop);
        }

        /// <summary>
        /// The stalls either side of the player's, and the people standing between
        /// them.
        ///
        /// These are the reason the backdrop was narrowed. The wall used to span 3.4 m
        /// either side of centre while the camera can only see 2.05 m at that depth —
        /// it overhung the frame by more than a metre and sealed the booth off, so
        /// nothing placed beside it in the same plane could ever appear. Every attempt
        /// to fill the periphery had to go over the roofline instead, which is why the
        /// fairground read as a distant backdrop rather than as somewhere the player is
        /// standing.
        ///
        /// With the wall at 1.65 m there is a window of about a fifth of the half-frame
        /// down each side — roughly fifty pixels on a 540-wide portrait screen — and
        /// everything here is sized and placed to sit inside it. The window is an
        /// *angular* one, bounded inside by the wall's edge and outside by the screen's,
        /// so it does not widen with distance: a stall twice as far away is not seen any
        /// wider, only smaller. That is what <see cref="SideWindow"/> exists to compute.
        /// </summary>
        private void BuildNeighbourStalls()
        {
            Transform root = GeometryFactory.CreateEmpty("Neighbours", transform).transform;

            // Staggered depths. Two stalls at the same distance either side read as one
            // symmetrical set dressing rather than as a row that happens to contain the
            // player.
            // Low, and lower than they look right at. A stall's height here is not
            // chosen against the booth beside it but against everything behind it: the
            // lane is one column a tenth of the screen wide, so a stall's roofline is
            // the ceiling for every single thing further down it. At the 2.95 m these
            // started at, the roofline sat so high that the lamp post, the striker and
            // the rides behind them were measured at zero visible pixels. Six hundred
            // millimetres off the roof is what buys the whole second half of the lane.
            BuildNeighbourStall(root, -1, 6.9f, 2.35f);
            BuildNeighbourStall(root, 1, 7.6f, 2.5f);

            // Folk stand nearer than the stalls they belong to, so they overlap them
            // and the lane gains a foreground. One each, and both well inside the window rather than across the middle
            // of it. A figure is a third of the window wide at this distance, so two of
            // them anywhere near the same fraction across simply stand in front of each
            // other — which is what the third figure this lane used to have was doing.
            BuildCarnivalFigure(
                root, "Clown", new Vector3(-FairwayX(6.2f, 0.3f), 0f, 6.2f),
                facing: 24f, scale: 1f, clown: true);

            BuildCarnivalFigure(
                root, "Barker", new Vector3(FairwayX(6f, 0.34f), 0f, 6f),
                facing: -20f, scale: 1.04f, clown: false);
        }

        /// <summary>
        /// The band of world X, at a given depth, that is both past the edge of the
        /// booth's wall and still inside the frame. Returns the inner and outer bounds
        /// as positive numbers; mirror them for the left side.
        ///
        /// Both bounds are straight lines through the lens, so the band is a fixed slice
        /// of the screen at every distance. Anything placed outside it is either hidden
        /// behind the player's own booth or off the edge of the screen, and both
        /// mistakes look identical from the inspector.
        /// </summary>
        private Vector2 SideWindow(float z)
        {
            float distance = z - CameraFraming.CameraZ;
            float inner = backdropHalfWidth / (backdropZ - CameraFraming.CameraZ) * distance;
            return new Vector2(inner, CameraFraming.HalfWidthAt(z));
        }

        /// <summary>
        /// One neighbouring stall: a dark body, a striped canvas, an awning and a lit
        /// sign.
        ///
        /// Deliberately darker and less saturated than the player's own booth, and not
        /// only so the eye stays on the board. Pegs on the outermost rows are now seen
        /// against this rather than against the striped wall, and a bright stall behind
        /// them would cost exactly the readability the narrow wall bought.
        /// </summary>
        private void BuildNeighbourStall(
            Transform parent, int side, float z, float height, float width = 1.9f)
        {
            Transform root = GeometryFactory
                .CreateEmpty($"Stall_{(side < 0 ? "L" : "R")}", parent).transform;

            FairwayDressing(z, out Material stallBody, out Material stallCanvas, out Material stallTrim);

            // Centred on the window, not tucked against the inside of it.
            //
            // A stall is four times wider than the slot it is seen through, so only
            // about a quarter of it can ever appear and the only real decision is
            // *which* quarter. Anchored on the window's inner edge — which is what the
            // first version did — the visible quarter is the stall's flank: a post, a
            // stripe and nothing that says stall. Centred, it is the middle: the awning
            // line, the counter, the sign and the lamp, all crossing the slot
            // horizontally. The flanks disappear behind the player's own booth, which
            // is exactly what a neighbouring stall does when you are standing in one.
            //
            // Everything that identifies the stall is therefore pulled in towards that
            // centre line below, and the structural posts are left out at the corners
            // where they belong even though they are never seen.
            float centre = side * FairwayX(z, 0.5f);

            // Local +x is world +x on the right and world -x on the left, so a feature
            // meant to sit on the booth side of the stall has to be mirrored.
            float inward = -side;

            root.localPosition = new Vector3(centre, 0f, z);
            // Turned a little towards the camera, so the awning and the counter are seen
            // at an angle rather than as flat slabs facing straight out of the screen.
            root.localRotation = Quaternion.Euler(0f, side * -14f, 0f);

            float bodyTop = height * 0.6f;

            GameObject body = GeometryFactory.CreatePrimitive(
                "Body", root, PrimitiveType.Cube, stallBody, receiveShadows: true);
            body.transform.localPosition = new Vector3(0f, bodyTop * 0.5f, 0f);
            body.transform.localScale = new Vector3(width, bodyTop, 1.1f);

            GameObject canvas = GeometryFactory.CreatePrimitive(
                "Canvas", root, PrimitiveType.Cube, stallCanvas, receiveShadows: true);
            canvas.transform.localPosition = new Vector3(0f, (height + bodyTop) * 0.5f, 0.02f);
            canvas.transform.localScale = new Vector3(width, height - bodyTop, 1.06f);

            const int stripes = 5;

            for (int i = 0; i < stripes; i++)
            {
                float t = (i + 0.5f) / stripes;

                GameObject stripe = GeometryFactory.CreatePrimitive(
                    $"Stripe_{i}", root, PrimitiveType.Cube, stallBody);
                stripe.transform.localPosition = new Vector3(
                    Mathf.Lerp(-width * 0.5f, width * 0.5f, t), (height + bodyTop) * 0.5f, -0.56f);
                stripe.transform.localScale =
                    new Vector3(width / stripes * 0.46f, (height - bodyTop) * 0.94f, 0.06f);
            }

            // The awning, tilted forward. This is the shape that says "stall" at a
            // glance more than anything else on the structure.
            GameObject awning = GeometryFactory.CreatePrimitive(
                "Awning", root, PrimitiveType.Cube, stallTrim, castShadows: true);
            awning.transform.localPosition = new Vector3(0f, bodyTop + 0.42f, -0.78f);
            awning.transform.localRotation = Quaternion.Euler(-26f, 0f, 0f);
            awning.transform.localScale = new Vector3(width * 1.04f, 0.09f, 1.15f);

            const int scallops = 7;

            for (int i = 0; i < scallops; i++)
            {
                float t = (i + 0.5f) / scallops;

                GameObject scallop = GeometryFactory.CreatePrimitive(
                    $"Scallop_{i}", root, PrimitiveType.Sphere, stallTrim);
                scallop.transform.localPosition = new Vector3(
                    Mathf.Lerp(-width * 0.5f, width * 0.5f, t), bodyTop + 0.16f, -1.28f);
                scallop.transform.localScale = new Vector3(width / scallops * 0.9f, 0.22f, 0.16f);
            }

            GameObject counter = GeometryFactory.CreatePrimitive(
                "Counter", root, PrimitiveType.Cube, stallTrim, castShadows: true);
            counter.transform.localPosition = new Vector3(0f, 0.52f, -1.15f);
            counter.transform.localScale = new Vector3(width * 0.92f, 0.13f, 0.42f);

            for (int i = -1; i <= 1; i += 2)
            {
                GameObject post = GeometryFactory.CreatePrimitive(
                    $"Post_{(i < 0 ? "L" : "R")}", root, PrimitiveType.Cube,
                    postMaterial, castShadows: true);
                post.transform.localPosition = new Vector3(i * width * 0.46f, bodyTop * 0.5f, -1.24f);
                post.transform.localScale = new Vector3(0.13f, bodyTop + 0.5f, 0.13f);

                // The bay posts. These are the ones that are actually seen: they land
                // on the two edges of the window, so the slice of stall showing through
                // it has a vertical edge at each side instead of bleeding into the wall
                // on one side and the screen edge on the other.
                GameObject bay = GeometryFactory.CreatePrimitive(
                    $"Bay Post_{(i < 0 ? "L" : "R")}", root, PrimitiveType.Cube,
                    postMaterial, castShadows: true);
                bay.transform.localPosition = new Vector3(i * width * 0.13f, bodyTop * 0.5f, -1.2f);
                bay.transform.localScale = new Vector3(0.1f, bodyTop + 0.42f, 0.1f);
            }

            // A shelf of prizes, reusing the booth's own prize materials so the two
            // stalls are visibly selling the same sort of thing.
            for (int i = 0; i < 5; i++)
            {
                float t = (i + 0.5f) / 5f;

                GameObject prize = GeometryFactory.CreatePrimitive(
                    $"Prize_{i}", root, i % 2 == 0 ? PrimitiveType.Sphere : PrimitiveType.Capsule,
                    prizeMaterials[i % prizeMaterials.Count], castShadows: true);
                prize.transform.localPosition = new Vector3(
                    Mathf.Lerp(-width * 0.2f, width * 0.2f, t), bodyTop * 0.62f, -0.62f);
                prize.transform.localScale = i % 2 == 0
                    ? Vector3.one * 0.24f
                    : new Vector3(0.17f, 0.15f, 0.17f);
            }

            GameObject sign = GeometryFactory.CreatePrimitive(
                "Sign", root, PrimitiveType.Cube, stallTrim, castShadows: true);
            sign.transform.localPosition = new Vector3(0f, height + 0.34f, -0.35f);
            sign.transform.localScale = new Vector3(width * 0.56f, 0.5f, 0.1f);

            GameObject pennant = GeometryFactory.CreateMeshObject(
                "Pennant", root, MeshFactory.Pennant(0.42f, 0.3f), buntingMaterials[side < 0 ? 0 : 2]);
            pennant.transform.localPosition =
                new Vector3(-inward * width * 0.09f, height + 0.86f, -0.35f);
            pennant.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            // A lamp on the sign, joining the booth's own bulbs so the whole row
            // twinkles on one clock.
            GameObject bulb = GeometryFactory.CreatePrimitive(
                "Lamp", root, PrimitiveType.Sphere, bulbMaterial);
            bulb.transform.localPosition = new Vector3(inward * width * 0.11f, height + 0.34f, -0.46f);
            bulb.transform.localScale = Vector3.one * 0.17f;

            GameObject glow = GeometryFactory.CreateMeshObject(
                "Lamp Glow", root, MeshFactory.Disc(0.5f, 16), haloMaterial);
            glow.transform.localPosition = new Vector3(inward * width * 0.11f, height + 0.34f, -0.54f);
            glow.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            glow.transform.localScale = Vector3.one * 0.62f;

            bulbs.Add(bulb.transform);
            bulbRenderers.Add(glow.GetComponent<MeshRenderer>());
        }

        /// <summary>
        /// A person, built from the same primitives as everything else.
        ///
        /// Read at the size these appear — a head and shoulders in a fifty pixel window
        /// — a figure is a silhouette and nothing more, so the parts that carry it are
        /// the ones that break the outline: a hat, a ruff, and arms held away from the
        /// body. Facial detail at this distance is a single dot for the nose, and even
        /// that only reads because it is the one saturated thing on the figure.
        ///
        /// The arms and the body are hung off separate transforms because they are
        /// animated on different clocks. A figure that bobs and swings as one piece
        /// reads as an object being moved rather than as somebody standing there.
        /// </summary>
        private void BuildCarnivalFigure(
            Transform parent, string name, Vector3 position, float facing, float scale, bool clown)
        {
            Transform root = GeometryFactory.CreateEmpty(name, parent).transform;
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, facing, 0f);
            root.localScale = Vector3.one * scale;

            Material suit = clown ? folkSuitMaterial : folkCoatMaterial;

            for (int i = -1; i <= 1; i += 2)
            {
                GameObject leg = GeometryFactory.CreatePrimitive(
                    $"Leg_{(i < 0 ? "L" : "R")}", root, PrimitiveType.Capsule, suit, castShadows: true);
                leg.transform.localPosition = new Vector3(i * 0.11f, 0.36f, 0f);
                leg.transform.localScale = new Vector3(0.17f, 0.36f, 0.17f);
            }

            Transform torso = GeometryFactory.CreateEmpty("Torso", root).transform;
            torso.localPosition = new Vector3(0f, 0.72f, 0f);
            carnivalFolk.Add(torso);

            GameObject body = GeometryFactory.CreatePrimitive(
                "Body", torso, PrimitiveType.Capsule, suit, castShadows: true);
            body.transform.localPosition = new Vector3(0f, 0.24f, 0f);
            body.transform.localScale = new Vector3(0.42f, 0.3f, 0.34f);

            // Buttons, and the one place a clown differs from the barker below the neck.
            if (clown)
            {
                for (int i = 0; i < 3; i++)
                {
                    GameObject button = GeometryFactory.CreatePrimitive(
                        $"Button_{i}", torso, PrimitiveType.Sphere, folkAccentMaterial);
                    button.transform.localPosition = new Vector3(0f, 0.14f + i * 0.14f, -0.17f);
                    button.transform.localScale = Vector3.one * 0.09f;
                }
            }

            GameObject collar = GeometryFactory.CreatePrimitive(
                "Collar", torso, PrimitiveType.Sphere,
                clown ? folkAccentMaterial : folkCoatMaterial);
            collar.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            collar.transform.localScale = clown
                ? new Vector3(0.44f, 0.13f, 0.44f)
                : new Vector3(0.3f, 0.1f, 0.3f);

            GameObject head = GeometryFactory.CreatePrimitive(
                "Head", torso, PrimitiveType.Sphere, folkSkinMaterial, castShadows: true);
            head.transform.localPosition = new Vector3(0f, 0.66f, 0f);
            head.transform.localScale = Vector3.one * 0.28f;

            GameObject nose = GeometryFactory.CreatePrimitive(
                "Nose", torso, PrimitiveType.Sphere, folkAccentMaterial);
            nose.transform.localPosition = new Vector3(0f, 0.65f, -0.13f);
            nose.transform.localScale = Vector3.one * (clown ? 0.11f : 0.07f);

            if (clown)
            {
                // Hair either side and a small conical hat. Together they are most of
                // what makes the silhouette read as a clown rather than as a person.
                for (int i = -1; i <= 1; i += 2)
                {
                    GameObject hair = GeometryFactory.CreatePrimitive(
                        $"Hair_{(i < 0 ? "L" : "R")}", torso, PrimitiveType.Sphere, folkAccentMaterial);
                    hair.transform.localPosition = new Vector3(i * 0.14f, 0.66f, 0.02f);
                    hair.transform.localScale = Vector3.one * 0.16f;
                }

                for (int i = 0; i < 3; i++)
                {
                    // A stepped cone: Unity has no cone primitive, and three shrinking
                    // cylinders are indistinguishable from one at this size.
                    GameObject tier = GeometryFactory.CreatePrimitive(
                        $"Hat_{i}", torso, PrimitiveType.Cylinder, folkSuitMaterial);
                    tier.transform.localPosition = new Vector3(0f, 0.83f + i * 0.09f, 0.02f);
                    tier.transform.localScale = new Vector3(0.2f - i * 0.06f, 0.05f, 0.2f - i * 0.06f);
                }

                GameObject bobble = GeometryFactory.CreatePrimitive(
                    "Bobble", torso, PrimitiveType.Sphere, folkAccentMaterial);
                bobble.transform.localPosition = new Vector3(0f, 1.05f, 0.02f);
                bobble.transform.localScale = Vector3.one * 0.09f;
            }
            else
            {
                GameObject brim = GeometryFactory.CreatePrimitive(
                    "Hat Brim", torso, PrimitiveType.Cylinder, folkSuitMaterial);
                brim.transform.localPosition = new Vector3(0f, 0.79f, 0f);
                brim.transform.localScale = new Vector3(0.36f, 0.02f, 0.36f);

                GameObject crown = GeometryFactory.CreatePrimitive(
                    "Hat Crown", torso, PrimitiveType.Cylinder, folkSuitMaterial);
                crown.transform.localPosition = new Vector3(0f, 0.92f, 0f);
                crown.transform.localScale = new Vector3(0.23f, 0.13f, 0.23f);

                GameObject band = GeometryFactory.CreatePrimitive(
                    "Hat Band", torso, PrimitiveType.Cylinder, folkAccentMaterial);
                band.transform.localPosition = new Vector3(0f, 0.82f, 0f);
                band.transform.localScale = new Vector3(0.24f, 0.02f, 0.24f);
            }

            for (int i = -1; i <= 1; i += 2)
            {
                Transform shoulder = GeometryFactory
                    .CreateEmpty($"Shoulder_{(i < 0 ? "L" : "R")}", torso).transform;
                shoulder.localPosition = new Vector3(i * 0.21f, 0.44f, 0f);
                folkArms.Add(shoulder);
                folkArmSigns.Add(i);

                GameObject arm = GeometryFactory.CreatePrimitive(
                    "Arm", shoulder, PrimitiveType.Capsule, suit, castShadows: true);
                arm.transform.localPosition = new Vector3(0f, -0.19f, 0f);
                arm.transform.localScale = new Vector3(0.13f, 0.2f, 0.13f);

                GameObject glove = GeometryFactory.CreatePrimitive(
                    "Glove", shoulder, PrimitiveType.Sphere,
                    clown ? folkAccentMaterial : folkSkinMaterial);
                glove.transform.localPosition = new Vector3(0f, -0.39f, 0f);
                glove.transform.localScale = Vector3.one * 0.13f;
            }
        }

        /// <summary>
        /// Everything down the lane either side of the booth.
        ///
        /// The shape of the space decides the whole design here, and it is not the
        /// shape it looks like. The window beside the booth is bounded inside by the
        /// booth's own wall and outside by the screen, and both bounds are straight
        /// lines through the lens — so it is a fixed tenth of the screen wide at every
        /// distance. It is one column, not a stage.
        ///
        /// The first version of this lane assumed a column open at the far end could be
        /// filled by receding: near stalls, then dimmer stalls, then silhouettes. It
        /// cannot. In a column that narrow the nearest thing at any given height covers
        /// everything behind it, and a painter's-order model of the built geometry put
        /// thirteen of the pieces at *zero* visible pixels — two stalls, a kiosk, two
        /// lamp posts, two figures, six far shapes — which a debug-coloured capture then
        /// confirmed at 0.21% of the frame for the entire middle distance.
        ///
        /// So depth is not what separates things here. Height is. The camera is pitched
        /// down, so the ground rises up the frame as it recedes, and an object is seen
        /// past the one in front of it only if it is taller *on screen* — its height
        /// over its distance, not its height. That gives the lane bands rather than
        /// ranks, and one thing per band:
        ///
        ///   a cart or a crate, low, in front of everything
        ///   a figure, head and shoulders over it
        ///   a stall, roofline just clear of the figure
        ///   a lamp post or kiosk, clear of the stall
        ///   a striker or a banner, clear of that
        ///   a ride, clear of everything, handing over to the skyline
        ///
        /// Six per side, none of them redundant, every one of them measured. Adding a
        /// seventh does not make the fairground busier; it makes one of the six
        /// invisible.
        /// </summary>
        private void BuildFairway()
        {
            Transform root = GeometryFactory.CreateEmpty("Fairway", transform).transform;
            Transform still = GeometryFactory.CreateEmpty("Structures", root).transform;
            Transform lit = GeometryFactory.CreateEmpty("Lights", root).transform;

            // Ground band. Both are meant to be cut by the wall on one side and the
            // screen on the other: a prop sliced by the frame says the fairground
            // continues past the shot far more cheaply than a whole prop standing clear
            // of it.
            BuildCandyCart(still, -1, 5f);
            BuildFairwayClutter(still, 1, 5.2f);

            // Over the barker's head, where a bunch of balloons is the one thing in the
            // lane allowed to be fully saturated.
            BuildBalloonBunch(root, 1, 6f);

            // Above the stall rooflines. The lamp and the kiosk sign are both lit, which
            // is what carries them at this size — a shape this small reads as a light
            // before it reads as an object.
            BuildLampPost(still, lit, -FairwayX(11f, 0.22f), 11f, 5f, fairwayMaterial);
            BuildTicketKiosk(still, 1, 11.6f, 4.3f);

            // The tall band. The striker is the only thing in the picture that moves
            // vertically, which in a column this narrow is worth more than any amount
            // of detail: it is the one motion that cannot be read as the camera
            // drifting.
            BuildHighStriker(still, lit, -1, 14f, 5.4f);

            // The banner is almost without width on purpose, and stands in *front* of
            // the kiosk rather than beyond it. Behind, its whole height fell inside the
            // kiosk's roof and it measured zero; in front, it costs the kiosk three
            // columns of a hundred and eight and gains a lit vertical the lane
            // otherwise has nothing of.
            BuildBannerPole(still, lit, 1, 10f, 5f);

            // The rides close the lane off. Dressed in the skyline's own material
            // because they stand level with it: they are the join between the street
            // the player is in and the fairground behind the booth, and a lit ride at
            // twenty metres would break that seam wide open. Both turn.
            BuildSwingRide(still, -FairwayX(19.5f, 0.82f), 19.5f, 7f, 1.9f, skylineMidMaterial);
            BuildCarousel(still, FairwayX(18.5f, 0.55f), 18.5f, 6.4f, 1.5f, skylineMidMaterial);

            // Festoons last, so they hang in front of everything they cross. They stop
            // short of the strikers: past that the lane's own tall band is in the way,
            // and a light string that disappears behind a tower is right, while one
            // built where the tower already is would be waste.
            BuildFestoon(still, lit, -1, 5.4f, 12.5f, 3.05f, 3.95f);
            BuildFestoon(still, lit, 1, 6.2f, 13.5f, 3.2f, 4.1f);
        }

        /// <summary>
        /// A world X inside the window beside the booth, as a fraction across it.
        ///
        /// Zero is the edge of the booth's own wall and one the edge of the screen.
        /// Always positive: mirror it for the left side. Values outside 0..1 are legal
        /// and useful — a prop meant to be cut by the wall is placed below zero on
        /// purpose.
        /// </summary>
        private float FairwayX(float z, float across)
        {
            Vector2 window = SideWindow(z);
            return Mathf.Lerp(window.x, window.y, across);
        }

        /// <summary>
        /// Which set of materials a structure at this depth is dressed in.
        ///
        /// Depth picks the palette rather than the caller, so a stall moved further
        /// down the lane re-dresses itself and cannot end up the near rank's value at
        /// the far rank's size — which is the one mistake that collapses the whole
        /// recession back into a flat strip.
        /// </summary>
        private void FairwayDressing(
            float z, out Material body, out Material canvas, out Material trim)
        {
            bool near = z < 10f;
            body = near ? neighbourMaterial : fairwayMaterial;
            canvas = near ? neighbourCanvasMaterial : fairwayCanvasMaterial;
            trim = near ? neighbourTrimMaterial : fairwayTrimMaterial;
        }

        /// <summary>
        /// A wheeled cart with a peaked canopy and a lamp.
        ///
        /// Placed to be cut in half by the booth's wall. Everything that identifies it
        /// — the canopy peak, the lamp, the wheel — is on the outer half; the half
        /// nobody will see is still built, because the shadow it casts is the reason
        /// the ground beside the booth does not read as empty floor.
        /// </summary>
        private void BuildCandyCart(Transform parent, int side, float z)
        {
            Transform root = GeometryFactory
                .CreateEmpty($"Candy Cart {(side < 0 ? "L" : "R")}", parent).transform;

            root.localPosition = new Vector3(side * FairwayX(z, 0.55f), 0f, z);
            root.localRotation = Quaternion.Euler(0f, side * -22f, 0f);

            // Built at a comfortable size and then set down, rather than drawn small.
            // Its canopy is the highest thing on it and it is the nearest thing in the
            // lane, so this one number sets the floor for everything behind it: at 0.74
            // the clown two metres further back was measured at eight cells out of
            // twenty-six thousand — a hat brim. A barrow rather than a stall is the
            // right size for the front of a queue anyway.
            root.localScale = Vector3.one * 0.62f;

            const float width = 1.02f;

            GameObject body = GeometryFactory.CreatePrimitive(
                "Body", root, PrimitiveType.Cube, neighbourCanvasMaterial, castShadows: true);
            body.transform.localPosition = new Vector3(0f, 0.66f, 0f);
            body.transform.localScale = new Vector3(width, 0.52f, 0.62f);

            GameObject skirt = GeometryFactory.CreatePrimitive(
                "Skirt", root, PrimitiveType.Cube, neighbourMaterial, castShadows: true);
            skirt.transform.localPosition = new Vector3(0f, 0.44f, 0f);
            skirt.transform.localScale = new Vector3(width * 0.96f, 0.14f, 0.6f);

            for (int i = -1; i <= 1; i += 2)
            {
                GameObject wheel = GeometryFactory.CreatePrimitive(
                    $"Wheel_{(i < 0 ? "L" : "R")}", root, PrimitiveType.Cylinder,
                    postMaterial, castShadows: true);
                wheel.transform.localPosition = new Vector3(i * width * 0.42f, 0.24f, 0.06f);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.transform.localScale = new Vector3(0.46f, 0.05f, 0.46f);

                GameObject post = GeometryFactory.CreatePrimitive(
                    $"Post_{(i < 0 ? "L" : "R")}", root, PrimitiveType.Cube, postMaterial);
                post.transform.localPosition = new Vector3(i * width * 0.44f, 1.12f, -0.24f);
                post.transform.localScale = new Vector3(0.06f, 0.78f, 0.06f);
            }

            List<Vector2> peak = new List<Vector2>
            {
                new Vector2(-width * 0.62f, 0f),
                new Vector2(width * 0.62f, 0f),
                new Vector2(0f, 0.46f)
            };

            GameObject canopy = GeometryFactory.CreateMeshObject(
                "Canopy", root, MeshFactory.Fan(peak), neighbourTrimMaterial, castShadows: true);
            canopy.transform.localPosition = new Vector3(0f, 1.5f, -0.3f);

            const int scallops = 5;

            for (int i = 0; i < scallops; i++)
            {
                float t = (i + 0.5f) / scallops;

                GameObject scallop = GeometryFactory.CreatePrimitive(
                    $"Scallop_{i}", root, PrimitiveType.Sphere, neighbourTrimMaterial);
                scallop.transform.localPosition = new Vector3(
                    Mathf.Lerp(-width * 0.58f, width * 0.58f, t), 1.46f, -0.34f);
                scallop.transform.localScale = new Vector3(width / scallops * 0.86f, 0.17f, 0.14f);
            }

            // A jar of something bright on the counter. One saturated dot is the whole
            // difference between a cart and a crate on wheels.
            GameObject jar = GeometryFactory.CreatePrimitive(
                "Jar", root, PrimitiveType.Sphere, prizeMaterials[0], castShadows: true);
            jar.transform.localPosition = new Vector3(width * 0.2f, 1.04f, -0.16f);
            jar.transform.localScale = new Vector3(0.26f, 0.3f, 0.26f);

            AddFairwayLamp(root, new Vector3(-width * 0.28f, 1.62f, -0.34f), 0.15f);
        }

        /// <summary>
        /// A kiosk selling tickets: a small box with a lit hatch and a peaked roof.
        ///
        /// The hatch is the point. A dark rectangle low on a lit structure is read as a
        /// serving window at any size, and it is the cheapest way to say that the thing
        /// beside the booth is open for business rather than a shed.
        /// </summary>
        private void BuildTicketKiosk(Transform parent, int side, float z, float height)
        {
            Transform root = GeometryFactory
                .CreateEmpty($"Ticket Kiosk {(side < 0 ? "L" : "R")}", parent).transform;

            root.localPosition = new Vector3(side * FairwayX(z, 0.2f), 0f, z);
            root.localRotation = Quaternion.Euler(0f, side * -18f, 0f);

            FairwayDressing(z, out Material body, out Material canvas, out Material trim);

            const float width = 1.3f;

            GameObject box = GeometryFactory.CreatePrimitive(
                "Body", root, PrimitiveType.Cube, canvas, castShadows: true);
            box.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            box.transform.localScale = new Vector3(width, height, 1f);

            GameObject hatch = GeometryFactory.CreatePrimitive(
                "Hatch", root, PrimitiveType.Cube, body);
            hatch.transform.localPosition = new Vector3(0f, height * 0.56f, -0.52f);
            hatch.transform.localScale = new Vector3(width * 0.62f, 0.6f, 0.06f);

            GameObject ledge = GeometryFactory.CreatePrimitive(
                "Ledge", root, PrimitiveType.Cube, trim, castShadows: true);
            ledge.transform.localPosition = new Vector3(0f, height * 0.56f - 0.34f, -0.62f);
            ledge.transform.localScale = new Vector3(width * 0.86f, 0.09f, 0.3f);

            List<Vector2> roof = new List<Vector2>
            {
                new Vector2(-width * 0.78f, 0f),
                new Vector2(width * 0.78f, 0f),
                new Vector2(0f, 0.66f)
            };

            GameObject cap = GeometryFactory.CreateMeshObject(
                "Roof", root, MeshFactory.Fan(roof), trim, castShadows: true);
            cap.transform.localPosition = new Vector3(0f, height, -0.5f);

            GameObject board = GeometryFactory.CreatePrimitive(
                "Sign", root, PrimitiveType.Cube, trim, castShadows: true);
            board.transform.localPosition = new Vector3(0f, height + 0.82f, -0.3f);
            board.transform.localScale = new Vector3(width * 0.7f, 0.42f, 0.08f);

            GameObject mast = GeometryFactory.CreatePrimitive(
                "Mast", root, PrimitiveType.Cube, postMaterial);
            mast.transform.localPosition = new Vector3(0f, height + 0.48f, -0.3f);
            mast.transform.localScale = new Vector3(0.07f, 0.5f, 0.07f);

            AddFairwayLamp(root, new Vector3(width * 0.3f, height + 0.82f, -0.4f), 0.16f);
        }

        /// <summary>
        /// A test-your-strength tower, with a puck that is thrown up the shaft.
        ///
        /// Chosen for the shape of the space rather than for the fairground: it is tall
        /// and almost without width, which is the only proportion that uses a slot a
        /// tenth of the screen wide from top to bottom. The puck is the reason it is
        /// here at all — see <see cref="AnimateStrikers"/>.
        /// </summary>
        private void BuildHighStriker(
            Transform still, Transform lit, int side, float z, float height)
        {
            Transform root = GeometryFactory
                .CreateEmpty($"High Striker {(side < 0 ? "L" : "R")}", still).transform;

            root.localPosition = new Vector3(side * FairwayX(z, 0.6f), 0f, z);

            FairwayDressing(z, out Material body, out Material canvas, out Material trim);

            GameObject plinth = GeometryFactory.CreatePrimitive(
                "Plinth", root, PrimitiveType.Cube, body, castShadows: true);
            plinth.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            plinth.transform.localScale = new Vector3(0.92f, 0.4f, 0.72f);

            GameObject shaft = GeometryFactory.CreatePrimitive(
                "Shaft", root, PrimitiveType.Cube, canvas, castShadows: true);
            shaft.transform.localPosition = new Vector3(0f, height * 0.5f + 0.4f, 0f);
            shaft.transform.localScale = new Vector3(0.24f, height, 0.24f);

            // Graduations up the shaft. They are two pixels apart at this distance and
            // will never be counted, but they are what makes the shaft read as a scale
            // rather than as a pole.
            const int marks = 7;

            for (int i = 0; i < marks; i++)
            {
                float t = (i + 1f) / (marks + 1f);

                GameObject mark = GeometryFactory.CreatePrimitive(
                    $"Mark_{i}", root, PrimitiveType.Cube, trim);
                mark.transform.localPosition =
                    new Vector3(0.14f, 0.4f + height * t, -0.06f);
                mark.transform.localScale = new Vector3(0.16f, 0.05f, 0.05f);
            }

            Transform puck = GeometryFactory.CreateEmpty("Puck", root).transform;
            puck.localPosition = new Vector3(0f, 0.5f, 0f);

            GameObject slug = GeometryFactory.CreatePrimitive(
                "Slug", puck, PrimitiveType.Cube, trim, castShadows: true);
            slug.transform.localScale = new Vector3(0.36f, 0.16f, 0.3f);

            strikerPucks.Add(puck);
            strikerTravel.Add(height - 0.5f);

            GameObject bell = GeometryFactory.CreatePrimitive(
                "Bell", root, PrimitiveType.Sphere, trim, castShadows: true);
            bell.transform.localPosition = new Vector3(0f, height + 0.58f, 0f);
            bell.transform.localScale = new Vector3(0.42f, 0.34f, 0.42f);

            Transform head = GeometryFactory.CreateEmpty("Bell Light", lit).transform;
            head.localPosition = root.localPosition;
            AddFairwayLamp(head, new Vector3(0f, height + 0.58f, -0.16f), 0.22f);

            GameObject mallet = GeometryFactory.CreatePrimitive(
                "Mallet", root, PrimitiveType.Cube, postMaterial, castShadows: true);
            mallet.transform.localPosition = new Vector3(-0.42f, 0.62f, -0.3f);
            mallet.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
            mallet.transform.localScale = new Vector3(0.08f, 1.1f, 0.08f);
        }

        /// <summary>
        /// The bunch of balloons the barker is holding.
        ///
        /// The figure is not built here — the barker already stands in the lane and a
        /// second person beside him was measured as hiding him almost completely. He
        /// gets the balloons instead, which costs nothing and puts the one fully
        /// saturated thing in the lane exactly where the eye already is.
        /// </summary>
        /// <summary>
        /// The balloons the barker is holding, out in the lane.
        ///
        /// Busy only, for the same reason as the booth's: there is no string between the
        /// bunch and the hand, the barker is half off the edge of the frame at this
        /// aspect, and what the player sees is a group of spheres hanging in the air
        /// beside the stall. Everything in the frame has to be held up by something
        /// visible or it is not in the frame.
        /// </summary>
        private void BuildBalloonBunch(Transform parent, int side, float z)
        {
            if (!AtLeast(SceneryDensity.Busy))
            {
                return;
            }

            Transform cluster = GeometryFactory.CreateEmpty("Barker Balloons", parent).transform;
            cluster.localPosition = new Vector3(side * FairwayX(z, 0.4f), 1.15f, z - 0.2f);
            balloonClusters.Add(cluster);

            // The string is what ties the bunch to the person. Without it the balloons
            // read as scenery that happens to be above somebody's head.
            GameObject tether = GeometryFactory.CreatePrimitive(
                "String", cluster, PrimitiveType.Cube, postMaterial);
            tether.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            tether.transform.localScale = new Vector3(0.025f, 0.84f, 0.025f);

            Vector3[] offsets =
            {
                new Vector3(-0.16f, 1.02f, 0.02f),
                new Vector3(0.17f, 0.96f, -0.06f),
                new Vector3(0.02f, 1.2f, 0.06f),
                new Vector3(-0.05f, 0.86f, -0.12f)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                GameObject balloon = GeometryFactory.CreatePrimitive(
                    $"Balloon_{i}", cluster, PrimitiveType.Sphere,
                    prizeMaterials[i % prizeMaterials.Count], castShadows: true);
                balloon.transform.localPosition = offsets[i];
                balloon.transform.localScale = new Vector3(0.3f, 0.35f, 0.3f);
            }
        }

        /// <summary>
        /// A banner on a pole, for the one band of the lane with no room for a
        /// building.
        ///
        /// Almost without width on purpose. Everything else out here is sized against
        /// a window a tenth of the screen wide and loses most of itself to it; a pole
        /// is the one shape that fits, so it is what stands where the measurement says
        /// nothing else can.
        /// </summary>
        private void BuildBannerPole(
            Transform still, Transform lit, int side, float z, float height)
        {
            Transform root = GeometryFactory
                .CreateEmpty($"Banner Pole {(side < 0 ? "L" : "R")}", still).transform;

            root.localPosition = new Vector3(side * FairwayX(z, 0.86f), 0f, z);

            GameObject mast = GeometryFactory.CreatePrimitive(
                "Mast", root, PrimitiveType.Cube, postMaterial, castShadows: true);
            mast.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            mast.transform.localScale = new Vector3(0.11f, height, 0.11f);

            GameObject banner = GeometryFactory.CreatePrimitive(
                "Banner", root, PrimitiveType.Cube, fairwayCanvasMaterial, castShadows: true);
            banner.transform.localPosition = new Vector3(0f, height * 0.62f, -0.05f);
            banner.transform.localScale = new Vector3(0.42f, height * 0.5f, 0.06f);

            const int bands = 4;

            for (int i = 0; i < bands; i++)
            {
                float t = (i + 0.5f) / bands;

                GameObject stripe = GeometryFactory.CreatePrimitive(
                    $"Band_{i}", root, PrimitiveType.Cube, fairwayTrimMaterial);
                stripe.transform.localPosition = new Vector3(
                    0f, Mathf.Lerp(height * 0.4f, height * 0.84f, t), -0.09f);
                stripe.transform.localScale = new Vector3(0.4f, height * 0.06f, 0.04f);
            }

            GameObject pennant = GeometryFactory.CreateMeshObject(
                "Pennant", root, MeshFactory.Pennant(0.36f, 0.28f),
                buntingMaterials[side < 0 ? 1 : 2]);
            pennant.transform.localPosition = new Vector3(side * -0.16f, height + 0.16f, -0.05f);
            pennant.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            Transform head = GeometryFactory.CreateEmpty("Banner Light", lit).transform;
            head.localPosition = root.localPosition;
            AddFairwayLamp(head, new Vector3(0f, height * 0.9f, -0.14f), 0.15f);
        }

        /// <summary>
        /// Crates, a barrel and a bale at the foot of the lane.
        ///
        /// Ground clutter, and nothing more ambitious than that. The lane was reading
        /// as a clean floor with objects standing on it, which is how an empty stage
        /// looks; a few things left lying where they were put is what makes ground look
        /// walked on.
        /// </summary>
        private void BuildFairwayClutter(Transform parent, int side, float z)
        {
            Transform root = GeometryFactory
                .CreateEmpty($"Clutter {(side < 0 ? "L" : "R")}", parent).transform;

            root.localPosition = new Vector3(side * FairwayX(z, 0.58f), 0f, z);

            GameObject low = GeometryFactory.CreatePrimitive(
                "Crate", root, PrimitiveType.Cube, crateMaterial, castShadows: true);
            low.transform.localPosition = new Vector3(0f, 0.24f, 0f);
            low.transform.localRotation = Quaternion.Euler(0f, side * 17f, 0f);
            low.transform.localScale = new Vector3(0.62f, 0.48f, 0.58f);

            GameObject stacked = GeometryFactory.CreatePrimitive(
                "Crate Stacked", root, PrimitiveType.Cube, crateMaterial, castShadows: true);
            stacked.transform.localPosition = new Vector3(-side * 0.08f, 0.66f, -0.06f);
            stacked.transform.localRotation = Quaternion.Euler(0f, side * -9f, 0f);
            stacked.transform.localScale = new Vector3(0.5f, 0.38f, 0.48f);

            GameObject barrel = GeometryFactory.CreatePrimitive(
                "Barrel", root, PrimitiveType.Cylinder, postMaterial, castShadows: true);
            barrel.transform.localPosition = new Vector3(side * 0.46f, 0.3f, -0.34f);
            barrel.transform.localScale = new Vector3(0.42f, 0.3f, 0.42f);

            GameObject bale = GeometryFactory.CreatePrimitive(
                "Bale", root, PrimitiveType.Capsule, crateMaterial, castShadows: true);
            bale.transform.localPosition = new Vector3(-side * 0.5f, 0.22f, 0.3f);
            bale.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            bale.transform.localScale = new Vector3(0.44f, 0.3f, 0.44f);
        }

        /// <summary>
        /// A run of festoon lights strung down the lane, away from the player.
        ///
        /// This is the single element that turns a row of props into a street. Every
        /// other thing out there is at one depth; the festoon is at all of them at once,
        /// and a line that visibly gets smaller as it goes is the only unambiguous
        /// statement in the picture that the lane has length.
        ///
        /// It is built last so it hangs in front of everything it crosses, and its
        /// bulbs join the booth's own so the whole fair twinkles on one clock.
        /// </summary>
        private void BuildFestoon(
            Transform still, Transform lit, int side, float fromZ, float toZ, float fromY, float toY)
        {
            Transform root = GeometryFactory
                .CreateEmpty($"Festoon {(side < 0 ? "L" : "R")}", still).transform;

            Transform lights = GeometryFactory
                .CreateEmpty($"Festoon Lights {(side < 0 ? "L" : "R")}", lit).transform;

            const int nodes = 15;
            const int spans = 3;
            const float sag = 0.34f;

            Vector3 previous = Vector3.zero;

            for (int i = 0; i < nodes; i++)
            {
                float t = (float)i / (nodes - 1);
                float z = Mathf.Lerp(fromZ, toZ, t);

                // Hung near the outer edge of the window, so the lane's own structures
                // are seen underneath it rather than through it.
                float x = side * FairwayX(z, 0.74f);
                float dip = Mathf.Sin(Mathf.Repeat(t * spans, 1f) * Mathf.PI) * sag;
                Vector3 at = new Vector3(x, Mathf.Lerp(fromY, toY, t) - dip, z);

                if (i > 0)
                {
                    Vector3 delta = at - previous;

                    GameObject cord = GeometryFactory.CreatePrimitive(
                        $"Cord_{i:D2}", root, PrimitiveType.Cube, postMaterial);
                    cord.transform.localPosition = (previous + at) * 0.5f;
                    cord.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                    cord.transform.localScale = new Vector3(0.035f, 0.035f, delta.magnitude);
                }

                previous = at;

                // A pole wherever a span ends, because a light string with nothing
                // holding it up is the same floating-dressing mistake the booth's own
                // frame exists to avoid.
                if (i % (nodes / spans) == 0 || i == nodes - 1)
                {
                    float ground = at.y - dip;

                    GameObject pole = GeometryFactory.CreatePrimitive(
                        $"Pole_{i:D2}", root, PrimitiveType.Cube, postMaterial, castShadows: true);
                    pole.transform.localPosition = new Vector3(at.x, ground * 0.5f, z);
                    pole.transform.localScale = new Vector3(0.08f, ground, 0.08f);

                    GameObject flag = GeometryFactory.CreateMeshObject(
                        $"Pennant_{i:D2}", root, MeshFactory.Pennant(0.34f, 0.26f),
                        buntingMaterials[i % buntingMaterials.Count]);
                    flag.transform.localPosition = new Vector3(at.x + side * 0.16f, ground + 0.1f, z);
                    flag.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }

                // Every other node carries a bulb. All fifteen lit is a solid line at
                // this distance, which is a cable, not a string of lights.
                if (i % 2 == 0)
                {
                    AddFairwayLamp(lights, at, Mathf.Lerp(0.15f, 0.1f, t));
                }
            }
        }

        /// <summary>
        /// A bulb and its halo, registered with the booth's own so it twinkles in time.
        /// </summary>
        private void AddFairwayLamp(Transform parent, Vector3 at, float size)
        {
            GameObject bulb = GeometryFactory.CreatePrimitive(
                "Bulb", parent, PrimitiveType.Sphere, bulbMaterial);
            bulb.transform.localPosition = at;
            bulb.transform.localScale = Vector3.one * size;

            GameObject glow = GeometryFactory.CreateMeshObject(
                "Glow", parent, MeshFactory.Disc(0.5f, 12), haloMaterial);
            glow.transform.localPosition = at + new Vector3(0f, 0f, -0.05f);
            glow.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            glow.transform.localScale = Vector3.one * (size * 4.2f);

            bulbs.Add(bulb.transform);
            bulbRenderers.Add(glow.GetComponent<MeshRenderer>());
        }

        /// <summary>
        /// Two shelves of prizes on the back wall.
        ///
        /// The wall is the largest thing in frame and it was nine stripes and nothing
        /// else, which is what made the booth read as a backdrop rather than a place.
        /// The prizes are deliberately desaturated towards the wall: the pegs have to
        /// stay the only saturated thing on screen or they stop being findable at a
        /// glance, and a shelf of bright plush directly behind them is the fastest way
        /// to lose that.
        /// </summary>
        private void BuildPrizeShelves(Transform parent)
        {
            Transform root = GeometryFactory.CreateEmpty("Prizes", parent).transform;

            float[] heights = { 1.28f, 2.08f };
            const int perShelf = 9;
            // Clamped to the canvas as well as to the frame. The wall is now narrower
            // than the visible frame at its depth, so sizing against the frame alone
            // would hang the shelves off the ends of the wall into open air.
            float span = Mathf.Min(
                Mathf.Min(frameHalfWidth + 0.2f, CameraFraming.SafeHalfWidthAt(backdropZ)),
                backdropHalfWidth - 0.12f);

            for (int shelf = 0; shelf < heights.Length; shelf++)
            {
                float y = heights[shelf];

                GameObject plank = GeometryFactory.CreatePrimitive(
                    $"Shelf_{shelf}", root, PrimitiveType.Cube, shelfMaterial, castShadows: true);
                plank.transform.localPosition = new Vector3(0f, y, backdropZ - 0.24f);
                plank.transform.localScale = new Vector3(span * 2f, 0.07f, 0.36f);

                for (int i = 0; i < perShelf; i++)
                {
                    float t = (i + 0.5f) / perShelf;
                    float x = Mathf.Lerp(-span + 0.2f, span - 0.2f, t);

                    // Three shapes cycling, offset per shelf, so neither row repeats
                    // against the other.
                    int kind = (i + shelf * 2) % 3;
                    Material material = prizeMaterials[(i + shelf) % prizeMaterials.Count];

                    PrimitiveType shape = kind == 0
                        ? PrimitiveType.Sphere
                        : kind == 1 ? PrimitiveType.Capsule : PrimitiveType.Cube;

                    GameObject prize = GeometryFactory.CreatePrimitive(
                        $"Prize_{shelf}_{i:D2}", root, shape, material, castShadows: true);

                    float size = kind == 1 ? 0.15f : 0.21f;
                    float lift = kind == 1 ? 0.19f : size * 0.5f;

                    prize.transform.localPosition =
                        new Vector3(x, y + 0.035f + lift, backdropZ - 0.26f);
                    prize.transform.localScale = kind == 1
                        ? new Vector3(size, 0.13f, size)
                        : Vector3.one * size;
                    prize.transform.localRotation = Quaternion.Euler(0f, (i * 37) % 90 - 45f, 0f);
                }
            }
        }

        /// <summary>
        /// A low shelf of prizes across the back of the board.
        ///
        /// The strip between the deepest peg row and the wall was the largest empty
        /// surface in the frame — three metres of bare board running the whole width,
        /// right in the middle of where the eye rests between throws. It sits well
        /// behind the play area, so it can be filled without ever getting in the way
        /// of a throw.
        /// </summary>
        private void BuildBackShelf()
        {
            Transform root = GeometryFactory.CreateEmpty("Back Shelf", transform).transform;

            const float z = 3.55f;

            // Inside the frame at this depth on the narrowest supported screen.
            float span = Mathf.Min(CameraFraming.SafeHalfWidthAt(z), BoothLimitAt(z) - 0.1f);

            GameObject plank = GeometryFactory.CreatePrimitive(
                "Plank", root, PrimitiveType.Cube, shelfMaterial, castShadows: true);
            plank.transform.localPosition = new Vector3(0f, 0.28f, z);
            plank.transform.localScale = new Vector3(span * 2f, 0.08f, 0.44f);

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject leg = GeometryFactory.CreatePrimitive(
                    $"Leg_{(side < 0 ? "L" : "R")}", root, PrimitiveType.Cube, shelfMaterial);
                leg.transform.localPosition = new Vector3(side * (span - 0.2f), 0.14f, z);
                leg.transform.localScale = new Vector3(0.12f, 0.28f, 0.3f);
            }

            // Eleven reads as a shelf of prizes on a monitor and as a dotted line at
            // phone size, where the individual shapes are four pixels across.
            int prizes = Some(5, 7, 11);

            for (int i = 0; i < prizes; i++)
            {
                float t = (i + 0.5f) / prizes;
                float x = Mathf.Lerp(-span + 0.24f, span - 0.24f, t);

                int kind = i % 3;
                Material material = prizeMaterials[i % prizeMaterials.Count];

                PrimitiveType shape = kind == 0
                    ? PrimitiveType.Sphere
                    : kind == 1 ? PrimitiveType.Capsule : PrimitiveType.Cube;

                GameObject prize = GeometryFactory.CreatePrimitive(
                    $"Prize_{i:D2}", root, shape, material, castShadows: true);

                float size = kind == 1 ? 0.13f : 0.19f;
                float lift = kind == 1 ? 0.17f : size * 0.5f;

                prize.transform.localPosition = new Vector3(x, 0.32f + lift, z);
                prize.transform.localScale = kind == 1
                    ? new Vector3(size, 0.12f, size)
                    : Vector3.one * size;
                prize.transform.localRotation = Quaternion.Euler(0f, (i * 43) % 90 - 45f, 0f);
            }
        }

        /// <summary>
        /// Crates, barrels and a flag pole in each back corner of the board.
        ///
        /// The corners are the part of the frame the booth's own geometry cannot reach:
        /// the wall is behind them and the posts are above them. Left bare they read as
        /// the set running out, which is exactly what an empty corner always reads as.
        /// </summary>
        private void BuildCornerStacks()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Transform root = GeometryFactory
                    .CreateEmpty($"Corner_{(side < 0 ? "L" : "R")}", transform).transform;
                // Placed against the wall's line of sight rather than at a remembered
                // X. At 1.5 m these crates sat squarely in the window beside the booth.
                root.localPosition = new Vector3(side * (BoothLimitAt(2.6f) - 0.42f), 0f, 2.6f);

                // Two crates, the upper one turned, so the stack does not read as one
                // tall box.
                GameObject lower = GeometryFactory.CreatePrimitive(
                    "Crate Lower", root, PrimitiveType.Cube, crateMaterial, castShadows: true);
                lower.transform.localPosition = new Vector3(0f, 0.19f, 0f);
                lower.transform.localScale = new Vector3(0.38f, 0.38f, 0.38f);
                lower.transform.localRotation = Quaternion.Euler(0f, side * 9f, 0f);

                GameObject upper = GeometryFactory.CreatePrimitive(
                    "Crate Upper", root, PrimitiveType.Cube, crateMaterial, castShadows: true);
                upper.transform.localPosition = new Vector3(side * -0.05f, 0.52f, 0.04f);
                upper.transform.localScale = new Vector3(0.29f, 0.29f, 0.29f);
                upper.transform.localRotation = Quaternion.Euler(0f, side * -24f, 0f);

                if (!AtLeast(SceneryDensity.Calm))
                {
                    continue;
                }

                GameObject barrel = GeometryFactory.CreatePrimitive(
                    "Barrel", root, PrimitiveType.Cylinder, valanceMaterial, castShadows: true);
                barrel.transform.localPosition = new Vector3(side * 0.42f, 0.24f, -0.32f);
                barrel.transform.localScale = new Vector3(0.34f, 0.24f, 0.34f);

                GameObject hoop = GeometryFactory.CreatePrimitive(
                    "Barrel Hoop", root, PrimitiveType.Cylinder, crateMaterial);
                hoop.transform.localPosition = new Vector3(side * 0.42f, 0.24f, -0.32f);
                hoop.transform.localScale = new Vector3(0.36f, 0.05f, 0.36f);

                // A pole with a pennant, to carry the eye upward out of the corner.
                // At a smaller size it carries the eye out of the *frame*: it is the
                // tallest thing in the corner and the first to read as clutter.
                if (!AtLeast(SceneryDensity.Busy))
                {
                    continue;
                }

                // A pole with a pennant, to carry the eye upward out of the corner.
                GameObject pole = GeometryFactory.CreatePrimitive(
                    "Pole", root, PrimitiveType.Cube, postMaterial, castShadows: true);
                pole.transform.localPosition = new Vector3(side * -0.28f, 0.62f, 0.2f);
                pole.transform.localScale = new Vector3(0.06f, 1.24f, 0.06f);

                // A lantern rather than a pennant. A lone bright triangle sitting at
                // the very edge of the frame reads as a UI chevron whatever it is meant
                // to be, and the first version had one in each corner pointing inward.
                GameObject lantern = GeometryFactory.CreatePrimitive(
                    "Lantern", root, PrimitiveType.Sphere, bulbMaterial);
                lantern.transform.localPosition = new Vector3(side * -0.28f, 1.2f, 0.2f);
                lantern.transform.localScale = new Vector3(0.15f, 0.19f, 0.15f);

                GameObject cap = GeometryFactory.CreatePrimitive(
                    "Lantern Cap", root, PrimitiveType.Cylinder, postMaterial);
                cap.transform.localPosition = new Vector3(side * -0.28f, 1.31f, 0.2f);
                cap.transform.localScale = new Vector3(0.13f, 0.02f, 0.13f);

                GameObject glow = GeometryFactory.CreateMeshObject(
                    "Lantern Glow", root, MeshFactory.Disc(0.5f, 16), haloMaterial);
                glow.transform.localPosition = new Vector3(side * -0.28f, 1.2f, 0.14f);
                glow.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                glow.transform.localScale = Vector3.one * 0.44f;
            }
        }

        /// <summary>
        /// Big plush prizes hanging from the ends of the beam.
        ///
        /// These fill the upper corners, which the striped wall alone left as two
        /// blank columns, and they are the single most "fairground" thing in the
        /// scene: a ring toss stall is defined by the wall of prizes you are playing
        /// for, and until now the player could not see what they were playing for.
        /// </summary>
        private void BuildHangingPrizes()
        {
            Transform root = GeometryFactory.CreateEmpty("Hanging Prizes", transform).transform;

            // Just inside the posts, in front of the wall, clear of the bunting.
            float[] columns = { -frameHalfWidth + 0.24f, frameHalfWidth - 0.24f };

            // Three deep on each side is a curtain of prizes across the top third of
            // the frame. On a phone it competes with the score and the level name,
            // which are the two things that have to be readable.
            int deep = Some(0, 2, 3);

            for (int c = 0; c < columns.Length; c++)
            {
                for (int i = 0; i < deep; i++)
                {
                    float y = beamHeight - 0.62f - i * 0.62f;
                    float x = columns[c] + (i % 2 == 0 ? 0f : (c == 0 ? 0.16f : -0.16f));

                    Material material = prizeMaterials[(c * 3 + i) % prizeMaterials.Count];

                    GameObject cord = GeometryFactory.CreateMeshObject(
                        $"Cord_{c}_{i}", root, MeshFactory.Quad(0.02f, 0.3f), silhouetteMaterial);
                    cord.transform.localPosition = new Vector3(x, y + 0.24f, frameZ - 0.5f);

                    GameObject body = GeometryFactory.CreatePrimitive(
                        $"Prize_{c}_{i}", root, PrimitiveType.Sphere, material, castShadows: true);
                    body.transform.localPosition = new Vector3(x, y, frameZ - 0.5f);
                    body.transform.localScale = new Vector3(0.26f, 0.3f, 0.26f);

                    // Two ears on the round ones: the difference between a hanging ball
                    // and a hanging toy is about four centimetres of geometry.
                    if (i % 2 != 0)
                    {
                        continue;
                    }

                    for (int ear = -1; ear <= 1; ear += 2)
                    {
                        GameObject lug = GeometryFactory.CreatePrimitive(
                            $"Ear_{c}_{i}_{ear}", root, PrimitiveType.Sphere, material);
                        lug.transform.localPosition =
                            new Vector3(x + ear * 0.11f, y + 0.14f, frameZ - 0.5f);
                        lug.transform.localScale = Vector3.one * 0.12f;
                    }
                }
            }
        }

        /// <summary>Balloon clusters on the frame posts, drifting on the same clock as the bunting.</summary>
        /// <summary>
        /// Balloon clusters on the booth's frame posts.
        ///
        /// Busy only, and the reason is the one thing in this scene that could not be
        /// defended: each balloon trails a 42 cm string that is tied to *nothing*. It
        /// ends in mid-air a third of a metre below the balloon, which at this distance
        /// reads exactly as what it is — five spheres floating beside the booth with
        /// loose thread under them. "Alguns balões só aparecem sem nenhum apoio."
        ///
        /// Kept rather than deleted, because the fix is a real one — tie the strings to
        /// the post and shorten them to reach — and that is a different piece of work
        /// from thinning the background.
        /// </summary>
        private void BuildBalloons()
        {
            if (!AtLeast(SceneryDensity.Busy))
            {
                return;
            }

            for (int side = -1; side <= 1; side += 2)
            {
                Transform root = GeometryFactory
                    .CreateEmpty($"Balloons_{(side < 0 ? "L" : "R")}", transform).transform;
                // Placed against the wall's line of sight, less the cluster's own reach.
                // A balloon is a sphere on an offset, so the group is a third of a metre
                // wider than the transform it hangs from — and at the frame's half-width
                // that put five bright spheres squarely across the window beside the
                // booth, which measured as teal and yellow filling the band where the
                // neighbour's canvas should have been.
                const float clusterReach = 0.3f;
                root.localPosition = new Vector3(
                    side * (BoothLimitAt(frameZ - 0.35f) - clusterReach),
                    beamHeight - 0.95f, frameZ - 0.35f);

                balloonClusters.Add(root);

                for (int i = 0; i < 5; i++)
                {
                    float angle = i / 5f * Mathf.PI * 2f;
                    Vector3 offset = new Vector3(
                        Mathf.Cos(angle) * 0.17f, Mathf.Sin(angle) * 0.19f + 0.24f, i % 2 * 0.06f);

                    GameObject balloon = GeometryFactory.CreatePrimitive(
                        $"Balloon_{i}", root, PrimitiveType.Sphere,
                        buntingMaterials[i % buntingMaterials.Count], castShadows: true);
                    balloon.transform.localPosition = offset;
                    balloon.transform.localScale = new Vector3(0.21f, 0.25f, 0.21f);

                    GameObject tether = GeometryFactory.CreateMeshObject(
                        $"String_{i}", root, MeshFactory.Quad(0.014f, 0.42f), silhouetteMaterial);
                    tether.transform.localPosition =
                        new Vector3(offset.x * 0.5f, offset.y - 0.34f, offset.z);
                }
            }
        }

        /// <summary>
        /// One line of pennants, strung between the two frame posts.
        ///
        /// One line and not two. The second used to hang over the board with both ends
        /// past the edge of the frame and nothing at either end, which is the single
        /// clearest way to make a scene look assembled rather than built.
        /// </summary>
        private void BuildBunting()
        {
            Transform root = GeometryFactory.CreateEmpty("Bunting", transform).transform;
            root.localPosition = new Vector3(0f, beamHeight - 0.16f, frameZ - 0.22f);
            buntingLines.Add(root);

            float span = frameHalfWidth;

            Mesh cordMesh = MeshFactory.Quad(span * 2f, 0.03f);
            GameObject cord = GeometryFactory.CreateMeshObject("Cord", root, cordMesh, silhouetteMaterial);
            cord.transform.localPosition = new Vector3(0f, -0.05f, 0f);

            // Visible knots where the cord meets the posts. Small, but they are the
            // difference between a string that is tied to something and one that ends.
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject knot = GeometryFactory.CreatePrimitive(
                    $"Knot_{(side < 0 ? "L" : "R")}", root, PrimitiveType.Sphere, silhouetteMaterial);
                knot.transform.localPosition = new Vector3(side * span, -0.05f, 0f);
                knot.transform.localScale = Vector3.one * 0.09f;
            }

            int pennants = Some(buntingPerLine - 5, buntingPerLine - 3, buntingPerLine);

            for (int i = 0; i < pennants; i++)
            {
                float t = pennants <= 1 ? 0.5f : (float)i / (pennants - 1);
                float x = Mathf.Lerp(-span, span, t);

                // Shallow catenary, so the string hangs instead of running level.
                float sag = -Mathf.Cos((t - 0.5f) * Mathf.PI) * 0.16f;

                Material material = buntingMaterials[i % buntingMaterials.Count];
                GameObject pennant = GeometryFactory.CreateMeshObject(
                    $"Pennant_{i:D2}", root, MeshFactory.Pennant(0.26f, 0.36f), material);
                pennant.transform.localPosition = new Vector3(x, sag - 0.06f, 0f);
            }
        }

        /// <summary>
        /// Bulbs strung between the same two posts, on a lower wire than the bunting,
        /// each with a soft halo.
        /// </summary>
        private void BuildStringLights()
        {
            Transform root = GeometryFactory.CreateEmpty("String Lights", transform).transform;
            root.localPosition = new Vector3(0f, beamHeight - 0.72f, frameZ - 0.44f);

            float span = frameHalfWidth - 0.06f;

            Mesh cordMesh = MeshFactory.Quad(span * 2f, 0.022f);
            GameObject wire = GeometryFactory.CreateMeshObject("Wire", root, cordMesh, silhouetteMaterial);
            wire.transform.localPosition = Vector3.zero;

            Mesh halo = MeshFactory.Disc(0.5f, 20);

            // Named for the count, not "bulbs": that is a field on this class holding
            // every lit thing in the scene, and shadowing it here compiles as far as
            // the first Add.
            int lit = Some(bulbCount - 5, bulbCount - 2, bulbCount);

            for (int i = 0; i < lit; i++)
            {
                float t = lit <= 1 ? 0.5f : (float)i / (lit - 1);
                float x = Mathf.Lerp(-span, span, t);
                float sag = -Mathf.Cos((t - 0.5f) * Mathf.PI) * 0.22f;

                GameObject bulb = GeometryFactory.CreatePrimitive(
                    $"Bulb_{i:D2}", root, PrimitiveType.Sphere, bulbMaterial);
                bulb.transform.localPosition = new Vector3(x, sag, 0f);
                bulb.transform.localScale = Vector3.one * 0.1f;

                GameObject glow = GeometryFactory.CreateMeshObject(
                    $"Halo_{i:D2}", root, halo, haloMaterial);
                glow.transform.localPosition = new Vector3(x, sag, -0.03f);
                glow.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

                // Sized against the bulb, not against the booth. Wider than this and
                // the halos merge into one white band across the backdrop.
                glow.transform.localScale = Vector3.one * 0.28f;

                bulbs.Add(bulb.transform);
                bulbRenderers.Add(glow.GetComponent<MeshRenderer>());
            }
        }

        /// <summary>
        /// The rest of the fair, behind the booth, in three layers.
        ///
        /// Everything here is placed against the frame rather than in remembered world
        /// metres — see the note above <see cref="SkylineFloorAt"/> for why. The layers
        /// are a mid one close enough that only its rooflines clear the beam, a near
        /// one carrying most of the movement, and a far one that is bigger, paler and
        /// slower.
        /// </summary>
        private void BuildSilhouettes()
        {
            // Two roots rather than one: everything that turns, sways or drifts is kept
            // apart from everything that never moves, so the moving list stays short
            // and the static half can be reasoned about as scenery.
            Transform still = GeometryFactory.CreateEmpty("Skyline", transform).transform;
            Transform moving = GeometryFactory.CreateEmpty("Skyline Moving", transform).transform;

            BuildFarSkyline(still, moving);
            BuildNearSkyline(still, moving);
            BuildMidSkyline(still, moving);
            BuildSkyBalloons(moving);

            // A distant land line along the bottom of the wedge, so the sky has a floor
            // and the towers have something to stand against rather than fading into
            // the gradient at the ankles. It also hides the edge of the ground disc.
            GameObject horizon = GeometryFactory.CreatePrimitive(
                "Horizon", still, PrimitiveType.Cube, skylineFarMaterial);
            horizon.transform.localPosition = new Vector3(0f, SkylineFloorAt(farLayerZ) * 0.5f, farLayerZ + 10f);
            horizon.transform.localScale =
                new Vector3(groundSize * 2f, SkylineFloorAt(farLayerZ) + 0.4f, 1f);
        }

        /// <summary>
        /// The biggest, palest and slowest layer. Read as the far side of the fair.
        /// </summary>
        private void BuildFarSkyline(Transform still, Transform moving)
        {
            float z = farLayerZ;
            Material material = skylineFarMaterial;

            BuildBigTop(still, Across(z, -0.34f), z, Crown(z, 0.72f), material);
            BuildTower(still, Across(z, -0.86f), z + 1f, Crown(z, 0.94f), 0.85f, material);
            BuildTent(still, Across(z, 0.16f), z + 2.2f, Crown(z, 0.4f), 3.4f, material);

            // The far wheel is behind the score. Two wheels on screen at once is also
            // one wheel drawn twice, which is the note the near layer already carries.
            if (AtLeast(SceneryDensity.Calm))
            {
                BuildFerrisWheel(moving, Across(z, 0.58f), z, Crown(z, 0.84f), 3.6f, material);
                BuildTent(still, Across(z, 0.9f), z + 1.4f, Crown(z, 0.34f), 3.1f, material);
            }

            // A run of flags between the two tallest things out here. The line is what
            // ties a skyline together: without it the pieces read as separate cut-outs
            // standing in a row rather than as one fairground.
            //
            // One line, and only out here. A coaster and a second line of flags used to
            // cross the middle layer as well, and at this scale a thin diagonal does not
            // read as a rail or a rope — it reads as a wire. Three sets of them turned
            // the sky into scaffolding.
            BuildFlagLine(
                still,
                new Vector3(Across(z, -0.86f), Crown(z, 0.78f), z + 1f),
                new Vector3(Across(z, 0.62f), Crown(z, 0.74f), z),
                11, 0.4f, material);
        }

        /// <summary>The layer that carries most of the movement.</summary>
        private void BuildNearSkyline(Transform still, Transform moving)
        {
            float z = nearLayerZ;
            Material material = silhouetteMaterial;

            // Both edges of the rim inside the frame, and a crown low enough that the
            // roofline cuts it. A wheel wider than the frame is not a wheel — it is an
            // arc drawn across the sky with its ends nowhere, which is what this was.
            // This layer sits directly behind the level name, the subtitle and the
            // score. On a monitor that band is scenery; at phone size the subtitle is
            // grey text on a field of tent peaks and wheel spokes, and the spokes are
            // the worst of it — a thin diagonal at this scale reads as a wire, and a
            // wheel of them reads as static.
            //
            // So the wheel and the swing go first, then the flanking tents. The
            // carousel stays at every density: it is one solid dome, it turns, and it
            // is the piece that says "fairground" with the least noise per pixel.
            // Busy, not Calm. Two wheels on screen is one wheel drawn twice, and the
            // owner counted them: "não precisa de 2 rodas gigantes". The far layer keeps
            // its one, because a wheel on the horizon is what says fairground; this one
            // sat directly behind the level name and the score.
            if (AtLeast(SceneryDensity.Busy))
            {
                BuildFerrisWheel(moving, Across(z, -0.55f), z, Crown(z, 0.66f), 2.2f, material);
            }

            if (AtLeast(SceneryDensity.Busy))
            {
                BuildSwingRide(moving, Across(z, 0.66f), z - 1.2f, Crown(z, 0.88f), 2.1f, material);
                BuildTent(still, Across(z, 0.96f), z + 2.4f, Crown(z, 0.36f), 2.7f, material);
            }

            BuildCarousel(moving, Across(z, 0.12f), z + 1.8f, Crown(z, 0.72f), 2.3f, material);
            BuildTent(still, Across(z, -0.24f), z - 1.6f, Crown(z, 0.3f), 2.5f, material);
            BuildLampPost(still, moving, Across(z, -0.46f), z - 3f, Crown(z, 0.24f), material);
            BuildLampPost(still, moving, Across(z, 0.4f), z - 3f, Crown(z, 0.24f), material);
        }

        /// <summary>
        /// The stalls either side of the player's own, close enough that only their
        /// roofs clear the booth's beam.
        ///
        /// This is the layer the scene was missing. The booth used to stand alone with
        /// a fairground on the horizon and nineteen metres of nothing in between, which
        /// is why the sides of the frame read as empty however much was built into the
        /// distance — there was no evidence of anywhere immediately next door.
        /// </summary>
        private void BuildMidSkyline(Transform still, Transform moving)
        {
            float z = midLayerZ;
            Material material = skylineMidMaterial;

            BuildStallRow(still, Across(z, -0.72f), z, Crown(z, 0.34f), 3, material);
            BuildStallRow(still, Across(z, 0.74f), z + 1.2f, Crown(z, 0.3f), 3, material);
            // The two tents sit squarely behind the level name and the score. Above
            // that band the sky is the only thing keeping the interface readable.
            if (AtLeast(SceneryDensity.Calm))
            {
                BuildTent(still, Across(z, -0.28f), z + 3.4f, Crown(z, 0.62f), 2.1f, material);
            }

            if (AtLeast(SceneryDensity.Busy))
            {
                BuildTent(still, Across(z, 0.3f), z + 4.2f, Crown(z, 0.56f), 1.9f, material);
            }
            BuildLampPost(still, moving, Across(z, -0.5f), z - 1.5f, Crown(z, 0.62f), material);
            BuildLampPost(still, moving, Across(z, 0.52f), z - 1.5f, Crown(z, 0.58f), material);
        }

        // ------------------------------------------------------------------
        // Placement against the frame rather than against remembered numbers.
        //
        // Everything behind the booth is seen through a wedge bounded below by the
        // sight line over the beam and above by the top edge of the frame. That wedge
        // is roughly four degrees tall, it moves whenever the booth moves, and two
        // separate rounds of scenery were lost to placing shapes in world metres and
        // discovering afterwards that the wedge was somewhere else. An audit of the
        // previous layout found two of its nine pieces entirely off screen and four
        // more clipped by the edge.
        //
        // So nothing here is placed in metres. Depth is chosen, and then height is a
        // fraction of the visible band and width a fraction of the visible half-width.
        // Move the beam and the skyline follows it.
        // ------------------------------------------------------------------

        /// <summary>
        /// The widest the booth's own structure may reach at a given depth.
        ///
        /// The canvas wall ends at <see cref="backdropHalfWidth"/>, and the straight
        /// line from the lens over that edge is what everything beyond it is seen
        /// through. Anything of the booth's own that crosses this line stands *in* the
        /// window rather than beside it, and hides the stall next door.
        ///
        /// This caught three separate pieces on the first attempt — the frame posts at
        /// 1.72 m, the backdrop's side panels, and the corner crates — none of which
        /// looked wrong in isolation, and all of which together left the openings
        /// completely blocked while the neighbours were built and lit behind them.
        /// </summary>
        /// <summary>Whether the scene is dressed at least this densely.</summary>
        private bool AtLeast(SceneryDensity level)
        {
            return density >= level;
        }

        /// <summary>How many of something to build, one count per density.</summary>
        private int Some(int clean, int calm, int busy)
        {
            return density == SceneryDensity.Clean ? clean
                : density == SceneryDensity.Calm ? calm : busy;
        }

        private float BoothLimitAt(float z)
        {
            return backdropHalfWidth / (backdropZ - CameraFraming.CameraZ)
                   * (z - CameraFraming.CameraZ);
        }

        /// <summary>Lowest world height at a given depth that the beam does not hide.</summary>
        private float SkylineFloorAt(float z)
        {
            return CameraFraming.SightLineAt(beamHeight + 0.2f, frameZ, z);
        }

        /// <summary>Height an element's crown must reach to sit <paramref name="t"/> up the visible band.</summary>
        private float Crown(float z, float t)
        {
            return Mathf.Lerp(SkylineFloorAt(z), CameraFraming.TopHeightAt(z), t);
        }

        /// <summary>Sideways position as a fraction of the visible half-width at that depth.</summary>
        private static float Across(float z, float fraction)
        {
            return CameraFraming.HalfWidthAt(z) * fraction;
        }

        private void BuildFerrisWheel(
            Transform parent, float x, float z, float crown, float radius, Material material)
        {
            Transform root = GeometryFactory.CreateEmpty("Ferris Wheel", parent).transform;
            float hub = crown - radius;
            root.localPosition = new Vector3(x, hub, z);

            // Legs first: they must not turn with the wheel.
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject leg = GeometryFactory.CreatePrimitive(
                    $"Leg_{(side < 0 ? "L" : "R")}", root, PrimitiveType.Cube, material);
                leg.transform.localPosition = new Vector3(side * radius * 0.34f, -hub * 0.5f, 0f);
                leg.transform.localRotation = Quaternion.Euler(0f, 0f, side * 11f);
                leg.transform.localScale = new Vector3(0.28f, hub, 0.28f);
            }

            Transform wheel = GeometryFactory.CreateEmpty("Wheel", root).transform;
            ferrisWheels.Add(wheel);

            // The torus is built in the XZ plane, so it has to be stood up to face the
            // camera.
            GameObject outerRim = GeometryFactory.CreateMeshObject(
                "Rim Outer", wheel, MeshFactory.Torus(radius, 0.1f, 40, 6), material);
            outerRim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            GameObject innerRim = GeometryFactory.CreateMeshObject(
                "Rim Inner", wheel, MeshFactory.Torus(radius * 0.62f, 0.07f, 32, 6), material);
            innerRim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            const int spokes = 12;

            for (int i = 0; i < spokes; i++)
            {
                float angle = 360f / spokes * i;

                GameObject spoke = GeometryFactory.CreatePrimitive(
                    $"Spoke_{i:D2}", wheel, PrimitiveType.Cube, material);
                spoke.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                spoke.transform.localPosition =
                    spoke.transform.localRotation * new Vector3(0f, radius * 0.5f, 0f);
                spoke.transform.localScale = new Vector3(0.07f, radius, 0.07f);

                GameObject gondola = GeometryFactory.CreatePrimitive(
                    $"Gondola_{i:D2}", wheel, PrimitiveType.Cube, material);
                gondola.transform.localPosition =
                    Quaternion.Euler(0f, 0f, angle) * new Vector3(0f, radius, 0f);
                gondola.transform.localScale = new Vector3(0.4f, 0.34f, 0.32f);
            }
        }

        /// <summary>
        /// A swing carousel: a mast, a canopy, and seats on chains that fly outwards.
        ///
        /// It turns about its mast rather than in the plane of the screen, so the seats
        /// sweep across and foreshorten instead of tracing a circle. That is what
        /// separates it from the ferris wheel at a glance, and it is the only element
        /// out here whose motion reads as three-dimensional.
        /// </summary>
        private void BuildSwingRide(
            Transform parent, float x, float z, float crown, float radius, Material material)
        {
            Transform root = GeometryFactory.CreateEmpty("Swing Ride", parent).transform;
            root.localPosition = new Vector3(x, 0f, z);

            float mastTop = crown - radius * 0.34f;

            GameObject mast = GeometryFactory.CreatePrimitive("Mast", root, PrimitiveType.Cube, material);
            mast.transform.localPosition = new Vector3(0f, mastTop * 0.5f, 0f);
            mast.transform.localScale = new Vector3(0.34f, mastTop, 0.34f);

            List<Vector2> canopy = new List<Vector2>
            {
                new Vector2(-radius * 0.62f, 0f),
                new Vector2(radius * 0.62f, 0f),
                new Vector2(0f, radius * 0.55f)
            };

            GameObject roof = GeometryFactory.CreateMeshObject(
                "Canopy", root, MeshFactory.Fan(canopy), material);
            roof.transform.localPosition = new Vector3(0f, mastTop, 0f);

            GameObject finial = GeometryFactory.CreatePrimitive("Finial", root, PrimitiveType.Cube, material);
            finial.transform.localPosition = new Vector3(0f, crown - 0.12f, 0f);
            finial.transform.localScale = new Vector3(0.08f, 0.34f, 0.08f);

            Transform hub = GeometryFactory.CreateEmpty("Hub", root).transform;
            hub.localPosition = new Vector3(0f, mastTop - 0.16f, 0f);
            swingRides.Add(hub);

            const int seats = 8;

            for (int i = 0; i < seats; i++)
            {
                float angle = 360f / seats * i;
                Quaternion around = Quaternion.Euler(0f, angle, 0f);
                Vector3 out_ = around * new Vector3(radius * 0.78f, 0f, 0f);

                // The chain leans out from the hub, which is the whole silhouette of a
                // swing ride: a seat hanging straight down reads as a fairground stall
                // with something dangling off it.
                GameObject chain = GeometryFactory.CreateMeshObject(
                    $"Chain_{i}", hub, MeshFactory.Quad(0.03f, radius * 0.66f), material);
                chain.transform.localPosition = new Vector3(out_.x * 0.5f, -radius * 0.24f, out_.z * 0.5f);
                chain.transform.localRotation = around * Quaternion.Euler(0f, 0f, -22f);

                GameObject seat = GeometryFactory.CreatePrimitive(
                    $"Seat_{i}", hub, PrimitiveType.Cube, material);
                seat.transform.localPosition = new Vector3(out_.x, -radius * 0.5f, out_.z);
                seat.transform.localScale = new Vector3(0.26f, 0.2f, 0.22f);
            }
        }

        /// <summary>A merry-go-round: a striped conical roof over a ring of poles.</summary>
        private void BuildCarousel(
            Transform parent, float x, float z, float crown, float radius, Material material)
        {
            Transform root = GeometryFactory.CreateEmpty("Carousel", parent).transform;
            root.localPosition = new Vector3(x, 0f, z);

            float eaves = crown - radius * 0.62f;

            GameObject platform = GeometryFactory.CreatePrimitive(
                "Platform", root, PrimitiveType.Cylinder, material);
            platform.transform.localPosition = new Vector3(0f, eaves * 0.5f - radius * 0.3f, 0f);
            platform.transform.localScale = new Vector3(radius * 1.6f, radius * 0.16f, radius * 1.6f);

            List<Vector2> roofOutline = new List<Vector2>
            {
                new Vector2(-radius, 0f),
                new Vector2(radius, 0f),
                new Vector2(0f, radius * 0.62f)
            };

            GameObject roof = GeometryFactory.CreateMeshObject(
                "Roof", root, MeshFactory.Fan(roofOutline), material);
            roof.transform.localPosition = new Vector3(0f, eaves, 0f);

            GameObject flag = GeometryFactory.CreateMeshObject(
                "Flag", root, MeshFactory.Pennant(0.42f, 0.3f), material);
            flag.transform.localPosition = new Vector3(0.2f, crown + 0.1f, 0f);
            flag.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            Transform ring = GeometryFactory.CreateEmpty("Ring", root).transform;
            ring.localPosition = new Vector3(0f, 0f, 0f);
            carousels.Add(ring);

            const int poles = 8;

            for (int i = 0; i < poles; i++)
            {
                Quaternion around = Quaternion.Euler(0f, 360f / poles * i, 0f);
                Vector3 at = around * new Vector3(radius * 0.72f, 0f, 0f);

                GameObject pole = GeometryFactory.CreatePrimitive(
                    $"Pole_{i}", ring, PrimitiveType.Cube, material);
                pole.transform.localPosition =
                    new Vector3(at.x, eaves - radius * 0.42f, at.z);
                pole.transform.localScale = new Vector3(0.07f, radius * 0.84f, 0.07f);

                GameObject mount = GeometryFactory.CreatePrimitive(
                    $"Mount_{i}", ring, PrimitiveType.Cube, material);
                mount.transform.localPosition =
                    new Vector3(at.x, eaves - radius * 0.46f, at.z);
                mount.transform.localScale = new Vector3(0.3f, 0.2f, 0.16f);
            }
        }

        /// <summary>
        /// The row of stalls next door: a fascia and a run of awning peaks.
        ///
        /// Only the roofline clears the booth's beam, which is the point — a neighbour
        /// you can see all of is not a neighbour, it is a second booth.
        /// </summary>
        private void BuildStallRow(
            Transform parent, float x, float z, float crown, int stalls, Material material)
        {
            Transform root = GeometryFactory.CreateEmpty("Stall Row", parent).transform;
            root.localPosition = new Vector3(x, 0f, z);

            const float stallWidth = 1.5f;
            float span = stallWidth * stalls;

            GameObject fascia = GeometryFactory.CreatePrimitive(
                "Fascia", root, PrimitiveType.Cube, material);
            fascia.transform.localPosition = new Vector3(0f, (crown - 0.55f) * 0.5f, 0f);
            fascia.transform.localScale = new Vector3(span, crown - 0.55f, 0.6f);

            for (int i = 0; i < stalls; i++)
            {
                float t = stalls <= 1 ? 0.5f : (i + 0.5f) / stalls;
                float offset = Mathf.Lerp(-span * 0.5f, span * 0.5f, t);

                List<Vector2> peak = new List<Vector2>
                {
                    new Vector2(-stallWidth * 0.52f, 0f),
                    new Vector2(stallWidth * 0.52f, 0f),
                    new Vector2(0f, 0.62f)
                };

                GameObject awning = GeometryFactory.CreateMeshObject(
                    $"Awning_{i}", root, MeshFactory.Fan(peak), material);
                awning.transform.localPosition = new Vector3(offset, crown - 0.62f, -0.32f);

                GameObject pennant = GeometryFactory.CreateMeshObject(
                    $"Pennant_{i}", root, MeshFactory.Pennant(0.3f, 0.22f), material);
                pennant.transform.localPosition = new Vector3(offset + 0.14f, crown + 0.16f, -0.32f);
                pennant.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

                GameObject pole = GeometryFactory.CreatePrimitive(
                    $"Pole_{i}", root, PrimitiveType.Cube, material);
                pole.transform.localPosition = new Vector3(offset, crown + 0.06f, -0.32f);
                pole.transform.localScale = new Vector3(0.05f, 0.3f, 0.05f);
            }
        }

        /// <summary>A slim ride tower with a cap and a flag, to break up the skyline.</summary>
        private void BuildTower(
            Transform parent, float x, float z, float crown, float width, Material material)
        {
            Transform root = GeometryFactory.CreateEmpty("Tower", parent).transform;
            root.localPosition = new Vector3(x, 0f, z);

            // The flag is the crown, so the shaft has to stop short of it by everything
            // stacked on top.
            float height = crown - width * 3.2f;

            GameObject shaft = GeometryFactory.CreatePrimitive(
                "Shaft", root, PrimitiveType.Cube, material);
            shaft.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            shaft.transform.localScale = new Vector3(width, height, width);

            List<Vector2> cap = new List<Vector2>
            {
                new Vector2(-width * 1.5f, 0f),
                new Vector2(width * 1.5f, 0f),
                new Vector2(0f, width * 2.1f)
            };

            GameObject roof = GeometryFactory.CreateMeshObject("Cap", root, MeshFactory.Fan(cap), material);
            roof.transform.localPosition = new Vector3(0f, height, 0f);

            GameObject pole = GeometryFactory.CreatePrimitive("Pole", root, PrimitiveType.Cube, material);
            pole.transform.localPosition = new Vector3(0f, height + width * 2.6f, 0f);
            pole.transform.localScale = new Vector3(0.08f, width * 1.1f, 0.08f);

            GameObject flag = GeometryFactory.CreateMeshObject(
                "Flag", root, MeshFactory.Pennant(width * 0.9f, width * 0.7f), material);
            flag.transform.localPosition = new Vector3(width * 0.4f, height + width * 3f, 0f);
            flag.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        private void BuildTent(
            Transform parent, float x, float z, float crown, float width, Material material)
        {
            Transform root = GeometryFactory.CreateEmpty("Tent", parent).transform;
            root.localPosition = new Vector3(x, 0f, z);

            float height = crown - 0.78f;

            List<Vector2> outline = new List<Vector2>
            {
                new Vector2(-width, 0f),
                new Vector2(width, 0f),
                new Vector2(width * 0.62f, height * 0.55f),
                new Vector2(0f, height),
                new Vector2(-width * 0.62f, height * 0.55f)
            };

            GeometryFactory.CreateMeshObject("Body", root, MeshFactory.Fan(outline), material);

            GameObject flag = GeometryFactory.CreateMeshObject(
                "Flag", root, MeshFactory.Pennant(0.5f, 0.38f), material);
            flag.transform.localPosition = new Vector3(0.25f, height + 0.42f, 0f);
            flag.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            GameObject pole = GeometryFactory.CreatePrimitive(
                "Pole", root, PrimitiveType.Cube, material);
            pole.transform.localPosition = new Vector3(0f, height + 0.26f, 0f);
            pole.transform.localScale = new Vector3(0.07f, 0.52f, 0.07f);
        }

        /// <summary>The main marquee: a wide tent with a scalloped hem and a ring of flags.</summary>
        private void BuildBigTop(
            Transform parent, float x, float z, float crown, Material material)
        {
            Transform root = GeometryFactory.CreateEmpty("Big Top", parent).transform;
            root.localPosition = new Vector3(x, 0f, z);

            float height = crown - 1.1f;
            float width = height * 0.62f;

            List<Vector2> outline = new List<Vector2>
            {
                new Vector2(-width, 0f),
                new Vector2(width, 0f),
                new Vector2(width * 0.78f, height * 0.42f),
                new Vector2(width * 0.34f, height * 0.82f),
                new Vector2(0f, height),
                new Vector2(-width * 0.34f, height * 0.82f),
                new Vector2(-width * 0.78f, height * 0.42f)
            };

            GeometryFactory.CreateMeshObject("Body", root, MeshFactory.Fan(outline), material);

            // Scalloped hem, the same trick the booth's own valance uses.
            const int scallops = 9;

            for (int i = 0; i < scallops; i++)
            {
                float t = (i + 0.5f) / scallops;

                GameObject scallop = GeometryFactory.CreatePrimitive(
                    $"Scallop_{i:D2}", root, PrimitiveType.Sphere, material);
                scallop.transform.localPosition =
                    new Vector3(Mathf.Lerp(-width, width, t), height * 0.42f, -0.05f);
                scallop.transform.localScale = new Vector3(width * 0.26f, width * 0.17f, 0.12f);
            }

            GameObject pole = GeometryFactory.CreatePrimitive("Pole", root, PrimitiveType.Cube, material);
            pole.transform.localPosition = new Vector3(0f, height + 0.45f, 0f);
            pole.transform.localScale = new Vector3(0.1f, 0.9f, 0.1f);

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject flag = GeometryFactory.CreateMeshObject(
                    $"Flag_{(side < 0 ? "L" : "R")}", root, MeshFactory.Pennant(0.62f, 0.42f), material);
                flag.transform.localPosition = new Vector3(side * 0.16f, height + 0.78f, 0f);
                flag.transform.localRotation = Quaternion.Euler(0f, 0f, side * 90f);
            }
        }

        /// <summary>
        /// A lamp post with a lit head. The head joins the booth's own bulbs, so the
        /// whole fair twinkles on one clock instead of the booth twinkling alone.
        /// </summary>
        private void BuildLampPost(
            Transform still, Transform moving, float x, float z, float crown, Material material)
        {
            Transform root = GeometryFactory.CreateEmpty("Lamp Post", still).transform;
            root.localPosition = new Vector3(x, 0f, z);

            float height = crown - 0.2f;

            GameObject post = GeometryFactory.CreatePrimitive("Post", root, PrimitiveType.Cube, material);
            post.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            post.transform.localScale = new Vector3(0.1f, height, 0.1f);

            GameObject arm = GeometryFactory.CreatePrimitive("Arm", root, PrimitiveType.Cube, material);
            arm.transform.localPosition = new Vector3(0f, height, 0f);
            arm.transform.localScale = new Vector3(0.44f, 0.08f, 0.1f);

            // The lit parts hang off the moving root: they are driven by a property
            // block every frame, and grouping them with the scenery that never changes
            // would be misleading to anyone reading the hierarchy.
            Transform lights = GeometryFactory.CreateEmpty("Lamp Heads", moving).transform;
            lights.localPosition = new Vector3(x, 0f, z);

            Mesh halo = MeshFactory.Disc(0.5f, 16);

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject bulb = GeometryFactory.CreatePrimitive(
                    $"Bulb_{(side < 0 ? "L" : "R")}", lights, PrimitiveType.Sphere, bulbMaterial);
                bulb.transform.localPosition = new Vector3(side * 0.2f, height - 0.06f, 0f);
                bulb.transform.localScale = Vector3.one * 0.14f;

                GameObject glow = GeometryFactory.CreateMeshObject(
                    $"Halo_{(side < 0 ? "L" : "R")}", lights, halo, haloMaterial);
                glow.transform.localPosition = new Vector3(side * 0.2f, height - 0.06f, -0.04f);
                glow.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                glow.transform.localScale = Vector3.one * 0.5f;

                bulbs.Add(bulb.transform);
                bulbRenderers.Add(glow.GetComponent<MeshRenderer>());
            }
        }

        /// <summary>A run of pennants slung between two points of the skyline.</summary>
        private void BuildFlagLine(
            Transform parent, Vector3 from, Vector3 to, int count, float sag, Material material)
        {
            Transform root = GeometryFactory.CreateEmpty("Flag Line", parent).transform;
            root.localPosition = (from + to) * 0.5f;

            Vector3 delta = to - from;
            float span = new Vector2(delta.x, delta.y).magnitude;

            GameObject cord = GeometryFactory.CreateMeshObject(
                "Cord", root, MeshFactory.Quad(span, 0.04f), material);
            cord.transform.localRotation =
                Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0.5f : (float)i / (count - 1);
                Vector3 at = Vector3.Lerp(from, to, t) - root.localPosition;

                // The same shallow catenary the booth's bunting hangs on, so the two
                // read as the same kind of object at two distances.
                at.y -= Mathf.Cos((t - 0.5f) * Mathf.PI) * sag;

                GameObject pennant = GeometryFactory.CreateMeshObject(
                    $"Pennant_{i:D2}", root, MeshFactory.Pennant(0.24f, 0.32f), material);
                pennant.transform.localPosition = at;
            }
        }

        /// <summary>
        /// Balloons let go somewhere off frame, rising through the sky band.
        ///
        /// They exist because the sky is the one part of the picture with nothing in it
        /// between throws, and a slow vertical drift is the cheapest possible way to
        /// stop the top of the screen reading as a flat fill. Both ends of the travel
        /// sit outside the visible band, so a balloon is never seen to pop back to the
        /// bottom when it wraps.
        /// </summary>
        private void BuildSkyBalloons(Transform parent)
        {
            Transform root = GeometryFactory.CreateEmpty("Sky Balloons", parent).transform;

            float[] fractions = { -0.78f, -0.3f, 0.24f, 0.58f, 0.88f };

            for (int i = 0; i < fractions.Length; i++)
            {
                float z = Mathf.Lerp(nearLayerZ, farLayerZ, (i * 0.27f) % 1f);
                float floor = SkylineFloorAt(z);
                float ceiling = CameraFraming.TopHeightAt(z);

                Transform balloon = GeometryFactory.CreateEmpty($"Balloon_{i}", root).transform;
                balloon.localPosition = new Vector3(Across(z, fractions[i]), floor, z);

                float scale = Mathf.Lerp(0.42f, 0.24f, (z - nearLayerZ) / (farLayerZ - nearLayerZ));
                Material material = i % 2 == 0 ? silhouetteMaterial : skylineMidMaterial;

                GameObject body = GeometryFactory.CreatePrimitive(
                    "Body", balloon, PrimitiveType.Sphere, material);
                body.transform.localScale = new Vector3(scale, scale * 1.18f, scale);

                GameObject basket = GeometryFactory.CreatePrimitive(
                    "Basket", balloon, PrimitiveType.Cube, material);
                basket.transform.localPosition = new Vector3(0f, -scale * 0.95f, 0f);
                basket.transform.localScale = Vector3.one * (scale * 0.3f);

                skyBalloons.Add(balloon);

                // Start below the sight line and finish above the top of the frame.
                skyBalloonTravel.Add(new Vector2(floor - scale * 2f, ceiling - floor + scale * 4f));
            }
        }

        private void OnLevelStarted(int levelIndex)
        {
            LevelConfig level = director == null ? null : director.CurrentLevel;
            ApplyTheme(level == null ? null : level.Theme);
        }

        /// <summary>Repaints the whole scene. Called whenever the stage changes.</summary>
        public void ApplyTheme(StageTheme theme)
        {
            if (theme == null)
            {
                return;
            }

            SetColor(groundMaterial, theme.GroundColor);

            // In shade under the awning: darker than the board, so the board is the lit
            // place and the booth floor is somewhere things are kept.
            Color boothFloor = Color.Lerp(theme.BoardColor, theme.GroundColor, 0.5f);
            SetColor(boothFloorMaterial, new Color(boothFloor.r * 0.8f, boothFloor.g * 0.8f, boothFloor.b * 0.8f, 1f));
            SetColor(boardMaterial, theme.BoardColor);
            SetColor(counterMaterial, Color.Lerp(theme.BoardColor, Color.black, 0.35f));

            // Muted well down from the accent. At full strength this is a bright slab
            // across the bottom fifth of the frame and it out-shouts the board.
            SetColor(counterTopMaterial, Color.Lerp(theme.BoardTrimColor, theme.BoardColor, 0.62f));

            // Deck joinery, and the chalk line in front of it. Both are a hair either
            // side of the board's own colour and nothing more. This is the largest
            // surface the player looks at and the pegs have to stay the only saturated
            // thing on it, so the seams are there to be read at a glance and not
            // noticed — high contrast here would put a ladder under every ring.
            SetColor(boardSeamMaterial, Color.Lerp(theme.BoardColor, Color.black, 0.17f));
            SetColor(chalkMaterial, Color.Lerp(theme.GroundColor, Color.white, 0.3f));

            // Spare hoops on the floor, pulled well down towards the board. They are
            // the same shape as the thing the player is throwing, and at full ring
            // colour the eye keeps going back to them looking for a ring that is not
            // in play.
            SetColor(spareRingMaterial, Color.Lerp(theme.RingColor(0), theme.BoardColor, 0.52f));
            // Mixed most of the way back to the board's own colour. The trim used to
            // be a 22 cm rim around a slab that ran off both edges of the screen, so
            // almost none of it was ever seen and the theme's full-strength gold was
            // free. It now outlines the whole lane, and at full strength it is a
            // bright rectangle around the play area competing with the pegs — which
            // are supposed to be the only saturated thing in the frame.
            SetColor(trimMaterial, Color.Lerp(theme.BoardColor, theme.BoardTrimColor, 0.42f));

            // The lit border is the deck's own colour, so at rest it is not there at all.
            // Anything brighter would draw a permanent rectangle round the play area and
            // the streak would have nothing left to say.
            SetColor(glowFrameMaterial, theme.BoardColor);

            // The floor's glow is the pegs' glow, so a streak lights the deck in the
            // colour the targets already pulse in.
            if (floorGlow != null)
            {
                floorGlow.SetColour(theme.PegGlowColor);
            }

            // The valance runs along the top of the frame, right where the HUD sits.
            // At full accent strength it blows out under the key light and takes the
            // score with it, so it is pulled well down towards the board.
            SetColor(valanceMaterial, Color.Lerp(theme.BoardTrimColor, theme.BoardColor, 0.34f));

            // The posts are structure, so they sit darker than everything they carry.
            // A frame the same value as the canvas behind it stops reading as a frame.
            SetColor(postMaterial, Color.Lerp(theme.BoardColor, Color.black, 0.5f));
            SetColor(shelfMaterial, Color.Lerp(theme.BoardColor, Color.black, 0.28f));

            // Crates read as bare wood: warm, and a shade lighter than the shelf they
            // stand beside so the stack does not merge into it.
            SetColor(crateMaterial, Color.Lerp(theme.BoardTrimColor, theme.BoardColor, 0.66f));
            // The booth is derived from the board rather than given its own colours.
            // It is the largest surface in the frame, so it has to recede; the pegs
            // are then the only saturated thing on the board, which is exactly what
            // makes them findable at a glance.
            SetColor(backdropMaterial, Color.Lerp(theme.BoardColor, theme.PegColor, 0.3f));
            SetColor(backdropStripeMaterial, Color.Lerp(theme.BoardColor, theme.PegStripeColor, 0.42f));
            // The neighbours sit between the booth and the silhouettes in value, which
            // is what places them in the middle distance without any fog. Kept well
            // down towards the ground colour: the outermost pegs are seen against this,
            // and a bright stall behind them would undo the whole point of the narrow
            // wall.
            // Only the lower third of a stall shares screen space with a peg, and only
            // with the outermost tenth of the fan, so the body stays dark and everything
            // above it is allowed to read.
            // Warm rather than dark. Towards the silhouette colour the body measured at
            // the same luminance as the lane behind it — 0.21 against 0.22 — so the
            // stall had no edge at all and read as more ground. Only its lowest few
            // centimetres share screen space with a peg, so the rest can afford this.
            SetColor(neighbourMaterial, Color.Lerp(theme.BoardColor, theme.BoardTrimColor, 0.2f));
            SetColor(neighbourCanvasMaterial, Color.Lerp(theme.BoardColor, theme.PegStripeColor, 0.46f));
            SetColor(neighbourTrimMaterial, Color.Lerp(theme.BoardTrimColor, theme.BoardColor, 0.32f));

            // The same three, pushed towards the sky for the second rank of the
            // fairway. Aerial perspective is the only depth cue available out there:
            // the window beside the booth is a tenth of the screen wide, far too narrow
            // for overlap alone to say which stall is in front of which, so the whole
            // recession is carried by value.
            SetColor(fairwayMaterial, Color.Lerp(
                Color.Lerp(theme.BoardColor, theme.BoardTrimColor, 0.2f), theme.SkyMiddle, 0.34f));
            SetColor(fairwayCanvasMaterial, Color.Lerp(
                Color.Lerp(theme.BoardColor, theme.PegStripeColor, 0.46f), theme.SkyMiddle, 0.34f));
            SetColor(fairwayTrimMaterial, Color.Lerp(
                Color.Lerp(theme.BoardTrimColor, theme.BoardColor, 0.32f), theme.SkyMiddle, 0.34f));

            // People are the one thing out there allowed to be warm. They are small and
            // they move, so they hold attention without competing with the board — and
            // a figure the same value as the stall behind it is not a figure at all.
            SetColor(folkSuitMaterial, Color.Lerp(theme.RingColor(1), theme.BoardColor, 0.3f));
            SetColor(folkCoatMaterial, Color.Lerp(theme.BoardColor, Color.black, 0.35f));
            SetColor(folkSkinMaterial, Color.Lerp(theme.PegStripeColor, theme.BoardTrimColor, 0.4f));
            SetColor(folkAccentMaterial, theme.PegColor);

            SetColor(silhouetteMaterial, theme.SilhouetteColor);

            // Each layer is lifted towards the sky it is seen against, which is the
            // whole of aerial perspective in two lines and the only thing separating
            // the three skylines into distinct distances.
            //
            // Towards SkyMiddle, not SkyHorizon. The horizon colour is the warm band
            // low in the gradient, and the skyline is not seen against it — it sits
            // well above, against the purple. Tinting towards the horizon turned the
            // middle layer brown, which put the *nearest* silhouettes at the *lightest*
            // value and stood the depth cue on its head: the stalls next door came
            // forward of the fairground behind them.
            SetColor(skylineMidMaterial, Color.Lerp(theme.SilhouetteColor, theme.SkyMiddle, 0.3f));
            SetColor(skylineFarMaterial, Color.Lerp(theme.SilhouetteColor, theme.SkyMiddle, 0.62f));

            for (int i = 0; i < prizeMaterials.Count; i++)
            {
                // Pulled well down towards the wall. The pegs have to stay the only
                // saturated thing on screen; a shelf of bright prizes directly behind
                // them is the fastest way to lose that.
                SetColor(prizeMaterials[i], Color.Lerp(theme.RingColor(i), theme.BoardColor, 0.42f));
            }

            SetColor(bulbMaterial, theme.PegGlowColor);
            SetColor(haloMaterial, new Color(theme.PegGlowColor.r, theme.PegGlowColor.g, theme.PegGlowColor.b, 0.5f));

            for (int i = 0; i < buntingMaterials.Count; i++)
            {
                SetColor(buntingMaterials[i], theme.RingColor(i));
            }

            ApplySky(theme);
            ApplyLighting(theme);
        }

        private void ApplySky(StageTheme theme)
        {
            if (skyboxMaterial == null)
            {
                return;
            }

            skyboxMaterial.SetColor(ShaderIds.TopColor, theme.SkyTop);
            skyboxMaterial.SetColor(ShaderIds.MiddleColor, theme.SkyMiddle);
            skyboxMaterial.SetColor(ShaderIds.HorizonColor, theme.SkyHorizon);
            skyboxMaterial.SetFloat(ShaderIds.HorizonHeight, theme.HorizonHeight);
            skyboxMaterial.SetColor(ShaderIds.SunColor, theme.SunColor);
            skyboxMaterial.SetFloat(ShaderIds.SunSize, theme.SunSize);

            if (skyboxMaterial.HasProperty("_GroundColor"))
            {
                skyboxMaterial.SetColor("_GroundColor", Color.Lerp(theme.GroundColor, Color.black, 0.35f));
            }

            // The sun's screen position is authored in the theme as a 0..1 pair; turn
            // that into a direction the shader can dot against.
            Vector2 placement = theme.SunPosition;
            float yaw = Mathf.Lerp(-70f, 70f, placement.x);
            float pitch = Mathf.Lerp(-4f, 45f, placement.y);
            Vector3 direction = Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;

            skyboxMaterial.SetVector(ShaderIds.SunDirection, direction);

            if (keyLight != null)
            {
                // The key light borrows the sun's side of the sky and its own fixed
                // elevation, and travels into the screen rather than out of it.
                //
                // The sun vector points from the camera towards the sun, so a light
                // travelling along it goes into the screen and lights the faces the
                // player can see. Aiming the light back along it, as physics would,
                // puts every one of those faces in shadow: the pegs then render their
                // ambient colour rather than their own, and a coral peg comes out
                // muddy brown.
                Vector3 lightDirection =
                    Quaternion.Euler(keyLightPitch, yaw, 0f) * Vector3.forward;

                keyLight.transform.rotation = Quaternion.LookRotation(lightDirection, Vector3.up);
            }
        }

        private void ApplyLighting(StageTheme theme)
        {
            if (keyLight != null)
            {
                keyLight.color = theme.KeyLightColor;
                keyLight.intensity = theme.KeyLightIntensity;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = theme.AmbientSky;
            RenderSettings.ambientEquatorColor = theme.AmbientEquator;
            RenderSettings.ambientGroundColor = theme.AmbientGround;
        }

        private static void SetColor(Material material, Color color)
        {
            if (material != null && material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, color);
            }
        }

        /// <summary>
        /// The scenery is never still. A fairground that stops moving reads as a
        /// screenshot, and the contrast with a moving background is most of what makes
        /// the board itself feel present.
        /// </summary>
        /// <summary>
        /// Moving air.
        ///
        /// Level 44 shipped unwinnable because of a crosswind, and the reason nobody
        /// could play around it is not that it was strong — it is that it was
        /// *invisible*. The ring drifted, the player had no way to know why, and the
        /// only reading available to them was that the game had cheated. A difficulty
        /// lever the player cannot perceive is not difficulty, it is noise.
        ///
        /// So the wind is drawn: thin lozenges of light drifting across the board at
        /// the speed and in the direction the physics is actually pushing. They are
        /// built from the same soft disc every glow in this game uses, stretched, and
        /// they cost eighteen quads with one shared material.
        ///
        /// A windless level draws nothing at all — the alpha is proportional to the
        /// wind, so this is invisible on the forty levels that have none rather than
        /// being a permanent ambient effect nobody can switch off.
        /// </summary>
        private void BuildWind()
        {
            if (windStreaks <= 0 || softDiscSource == null)
            {
                return;
            }

            windRoot = GeometryFactory.CreateEmpty("Wind", transform).transform;

            windMaterial = new Material(softDiscSource) { name = "Wind (runtime)" };
            windMaterial.SetColor(BaseColorId, new Color(1f, 1f, 1f, 0f));

            // Handed to the existing owner rather than destroyed by hand: runtime
            // clones belong to nobody else, and there is already one place that knows it.
            ownedMaterials.Add(windMaterial);

            Mesh streak = MeshFactory.Disc(0.5f, 14);
            wind = new Transform[windStreaks];
            windPhase = new float[windStreaks];

            for (int i = 0; i < windStreaks; i++)
            {
                GameObject piece = GeometryFactory.CreateMeshObject(
                    $"Streak_{i:D2}", windRoot, streak, windMaterial, castShadows: false);

                // Spread through the depth the player is looking at and up to about
                // the top of a throw's arc. Deterministic from the index rather than
                // random, so two runs of the same level look the same.
                float across = (i * 0.618f) % 1f;
                float depth = Mathf.Lerp(boardFront + 1f, boardBack, (i * 0.382f) % 1f);
                float height = Mathf.Lerp(0.25f, 2.1f, (i * 0.276f) % 1f);

                piece.transform.localPosition = new Vector3(0f, height, depth);
                piece.transform.localScale = Vector3.one;

                wind[i] = piece.transform;
                windPhase[i] = across;
            }
        }

        /// <summary>
        /// Drifts the streaks at whatever the level's wind is doing this frame.
        ///
        /// Read from the event rather than from the level, so this stays a listener
        /// like everything else in the feedback layer and does not need to know which
        /// mode is running or where the clock lives.
        /// </summary>
        private void TickWind()
        {
            if (wind == null || windMaterial == null)
            {
                return;
            }

            float speed = currentWind.x;
            float strength = Mathf.Clamp01(Mathf.Abs(speed) / 2.5f);

            // Nothing to draw on a still level, and nothing to pay for either.
            if (strength <= 0.001f)
            {
                if (windRoot.gameObject.activeSelf)
                {
                    windRoot.gameObject.SetActive(false);
                }

                return;
            }

            if (!windRoot.gameObject.activeSelf)
            {
                windRoot.gameObject.SetActive(true);
            }

            windMaterial.SetColor(BaseColorId, new Color(1f, 1f, 1f, 0.1f + strength * 0.14f));

            float direction = Mathf.Sign(speed);

            for (int i = 0; i < wind.Length; i++)
            {
                Transform piece = wind[i];
                float span = CameraFraming.HalfWidthAt(piece.localPosition.z) * 2.4f;

                // Wraps through a normalised lap rather than by testing an edge, so a
                // gust reversing mid-flight cannot strand a streak off screen.
                windPhase[i] = Mathf.Repeat(
                    windPhase[i] + Time.deltaTime * (0.15f + strength * 0.5f) * direction, 1f);

                Vector3 position = piece.localPosition;
                position.x = Mathf.Lerp(-span, span, windPhase[i]);
                piece.localPosition = position;

                // Long and thin, and longer the harder it blows. The fade at the ends
                // of the lap stops them appearing and vanishing at the frame edge.
                float edge = Mathf.Sin(windPhase[i] * Mathf.PI);
                piece.localScale = new Vector3(
                    (0.5f + strength * 1.5f) * edge, 0.035f + strength * 0.02f, 1f);
            }
        }

        private void OnWindChanged(Vector3 value)
        {
            currentWind = value;
        }

        private void Update()
        {
            animationTime += Time.deltaTime;

            TickWind();

            for (int i = 0; i < ferrisWheels.Count; i++)
            {
                // The far wheel turns slower than the near one. Two wheels at the same
                // angular speed read as one wheel drawn twice.
                float speed = ferrisWheelSpeed / (1f + i * 0.85f);
                ferrisWheels[i].localRotation = Quaternion.Euler(0f, 0f, animationTime * speed);
            }

            for (int i = 0; i < buntingLines.Count; i++)
            {
                float phase = animationTime * buntingSwaySpeed + i * 0.7f;
                buntingLines[i].localRotation = Quaternion.Euler(
                    0f, 0f, Mathf.Sin(phase) * buntingSwayAmount * 0.25f);
            }

            for (int i = 0; i < balloonClusters.Count; i++)
            {
                // Balloons drift rather than sway: they are tethered at the bottom, so
                // the whole cluster leans and bobs instead of pivoting on its middle.
                float phase = animationTime * 0.55f + i * 1.9f;

                balloonClusters[i].localRotation = Quaternion.Euler(
                    Mathf.Sin(phase * 0.7f) * 4f, 0f, Mathf.Sin(phase) * 7f);
            }

            for (int i = 0; i < swingRides.Count; i++)
            {
                // Alternating directions. Two rides turning the same way at the same
                // distance read as one mechanism, which is the same mistake the two
                // ferris wheels were making before they were detuned.
                float direction = i % 2 == 0 ? 1f : -1f;
                swingRides[i].localRotation =
                    Quaternion.Euler(0f, animationTime * swingRideSpeed * direction, 0f);
            }

            for (int i = 0; i < carousels.Count; i++)
            {
                carousels[i].localRotation = Quaternion.Euler(0f, animationTime * -carouselSpeed, 0f);
            }

            AnimateFolk();
            AnimateStrikers();
            DriftSkyBalloons();
            TwinkleBulbs();
        }

        /// <summary>
        /// Keeps the people in the lane alive.
        ///
        /// Two motions on unrelated clocks: the torso rises and falls on a slow breath,
        /// and the arms swing on a faster one. Driving both from a single sine would
        /// give a figure that pumps like a mechanism, which is worse than not moving at
        /// all — a still figure reads as somebody standing, a badly moving one reads as
        /// a puppet.
        /// </summary>
        private void AnimateFolk()
        {
            for (int i = 0; i < carnivalFolk.Count; i++)
            {
                float phase = animationTime * 1.15f + i * 2.3f;

                Vector3 position = carnivalFolk[i].localPosition;
                position.y = 0.72f + Mathf.Sin(phase) * 0.024f;
                carnivalFolk[i].localPosition = position;

                carnivalFolk[i].localRotation =
                    Quaternion.Euler(0f, Mathf.Sin(phase * 0.43f) * 7f, Mathf.Sin(phase * 0.7f) * 1.8f);
            }

            for (int i = 0; i < folkArms.Count; i++)
            {
                // Offset by the arm's own side, so the two never swing together.
                float phase = animationTime * 1.7f + i * 1.1f;
                folkArms[i].localRotation = Quaternion.Euler(
                    Mathf.Sin(phase) * 11f, 0f, folkArmSigns[i] * (14f + Mathf.Sin(phase * 0.8f) * 9f));
            }
        }

        /// <summary>
        /// Throws the puck up each high striker and lets it fall back.
        ///
        /// Timed as the real thing is, not as a sine: a fast rise, a moment held at the
        /// top, a slower slide down and then a long wait before anyone swings again.
        /// The wait is most of the cycle and is the reason the tower reads as somebody
        /// playing rather than as a machine — a puck that pumps continuously is the
        /// same mistake as folk that bob on one clock.
        /// </summary>
        private void AnimateStrikers()
        {
            for (int i = 0; i < strikerPucks.Count; i++)
            {
                float cycle = Mathf.Repeat(animationTime * 0.29f + i * 0.37f, 1f);
                float lift;

                if (cycle < 0.12f)
                {
                    // The swing. Eased out, so the puck arrives rather than stops.
                    lift = Mathf.Sin(cycle / 0.12f * Mathf.PI * 0.5f);
                }
                else if (cycle < 0.2f)
                {
                    lift = 1f;
                }
                else if (cycle < 0.46f)
                {
                    lift = 1f - (cycle - 0.2f) / 0.26f;
                }
                else
                {
                    lift = 0f;
                }

                Vector3 at = strikerPucks[i].localPosition;
                at.y = 0.5f + lift * strikerTravel[i];
                strikerPucks[i].localPosition = at;
            }
        }

        /// <summary>
        /// Walks each sky balloon up its own travel and wraps it round.
        ///
        /// The wrap is safe to do abruptly because both ends of the travel were placed
        /// outside the visible band when the balloon was built, so it is always off
        /// screen at the moment it jumps.
        /// </summary>
        private void DriftSkyBalloons()
        {
            if (balloonDriftSeconds <= 0.01f)
            {
                return;
            }

            for (int i = 0; i < skyBalloons.Count; i++)
            {
                Vector2 travel = skyBalloonTravel[i];

                // Staggered by index, so they are never in a row.
                float t = Mathf.Repeat(animationTime / balloonDriftSeconds + i * 0.31f, 1f);

                Vector3 position = skyBalloons[i].localPosition;
                position.y = travel.x + t * travel.y;
                skyBalloons[i].localPosition = position;

                // A slow lean, so a rising balloon is not a sphere on a rail.
                skyBalloons[i].localRotation =
                    Quaternion.Euler(0f, 0f, Mathf.Sin(animationTime * 0.4f + i * 1.7f) * 6f);
            }
        }

        private void TwinkleBulbs()
        {
            for (int i = 0; i < bulbRenderers.Count; i++)
            {
                // Two detuned sines per bulb: the pattern never repeats visibly, and no
                // two bulbs are ever in step.
                float phase = animationTime * 1.7f + i * 0.83f;
                float flicker = 0.55f + 0.2f * Mathf.Sin(phase) + 0.12f * Mathf.Sin(phase * 2.37f);

                bulbRenderers[i].GetPropertyBlock(block);
                block.SetColor(BaseColorId, new Color(1f, 1f, 1f, flicker * 0.3f));
                bulbRenderers[i].SetPropertyBlock(block);
            }
        }
    }
}
