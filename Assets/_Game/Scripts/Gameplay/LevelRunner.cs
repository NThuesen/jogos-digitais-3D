using System.Collections.Generic;
using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Utils;
using SlipperyZlope.RingToss.Core;
using UnityEngine;

namespace SlipperyZlope.RingToss.Gameplay
{
    /// <summary>
    /// Owns the board for the level being played: which pegs exist, where they are,
    /// how they move, and the forces that act on a ring in flight.
    ///
    /// Wind and aim assistance live here rather than on the ring because they are
    /// properties of the level, not of the object being thrown.
    /// </summary>
    [DefaultExecutionOrder(-350)]
    public sealed class LevelRunner : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private PhysicsConfig physicsConfig;
        [SerializeField] private AssistanceConfig assistanceConfig;
        [SerializeField] private JuiceConfig juiceConfig;

        [Tooltip("Pegs built at load. Caps how many a level may ask for.")]
        [Range(1, 12)]
        [SerializeField] private int pegCapacity = 6;

        [Header("Scene")]
        [SerializeField] private Transform boardRoot;

        [Header("Assets")]
        [SerializeField] private Material pegBodyMaterial;
        [SerializeField] private Material pegStripeMaterial;
        [SerializeField] private Material pegCapMaterial;
        [SerializeField] private Material pegGlowMaterial;
        [SerializeField] private Mesh discMesh;
        [SerializeField] private PhysicsMaterial pegPhysicsMaterial;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly List<PegTarget> pegs = new List<PegTarget>();
        private readonly List<PegTarget> activePegs = new List<PegTarget>();

        // Runtime clones so the pegs can take the stage palette. Every peg shares
        // them, so this is two materials for the whole board rather than two per peg.
        private Material themedBody;
        private Material themedStripe;

        private LevelConfig level;
        private float levelTime;
        private int consecutiveMisses;

        public IReadOnlyList<PegTarget> ActivePegs => activePegs;

        public int PegsRemaining { get; private set; }

        public LevelConfig CurrentLevel => level;

        public Vector3 BoardOrigin => boardRoot == null ? Vector3.zero : boardRoot.position;

        private void Awake()
        {
            if (boardRoot == null)
            {
                boardRoot = transform;
            }

            BuildPegs();
        }

        private void BuildPegs()
        {
            themedBody = new Material(pegBodyMaterial) { name = "Peg Body (runtime)" };
            themedStripe = new Material(pegStripeMaterial) { name = "Peg Stripe (runtime)" };

            PegFactory.Assets assets = new PegFactory.Assets
            {
                Body = themedBody,
                Stripe = themedStripe,
                Cap = pegCapMaterial,
                Glow = pegGlowMaterial,
                GlowMesh = discMesh,
                Physics = pegPhysicsMaterial
            };

            for (int i = 0; i < pegCapacity; i++)
            {
                pegs.Add(PegFactory.Create($"Peg_{i:D2}", boardRoot, physicsConfig, juiceConfig, assets));
            }
        }

        /// <summary>Lays out the board for a level and resets its progress.</summary>
        public void StartLevel(LevelConfig value)
        {
            level = value;
            levelTime = 0f;
            consecutiveMisses = 0;

            // Said once, now, whether or not this level has any. The drawing follows
            // the last wind it was told about, and a level without wind never raises
            // the event while it plays — so without this the last gust of the previous
            // level stayed on screen through every calm level after it, and through the
            // title. A playtest recording showed exactly that after level 47: streaks
            // drifting across a board with no wind on it.
            GameEvents.RaiseWindChanged(value != null && value.HasWind ? value.WindAt(0f) : Vector3.zero);
            activePegs.Clear();

            int wanted = value == null ? 0 : value.PegCount;
            StageTheme theme = value == null ? null : value.Theme;

            ApplyTheme(theme);

            if (wanted > pegs.Count)
            {
                Debug.LogError(
                    $"[LevelRunner] '{value.name}' asks for {wanted} pegs but only {pegs.Count} exist.",
                    this);
                wanted = pegs.Count;
            }

            for (int i = 0; i < pegs.Count; i++)
            {
                if (i < wanted)
                {
                    // Order matters: Apply sets the visual scale that the presenter
                    // then captures as the pose every squash springs back to.
                    pegs[i].Apply(value.GetPeg(i), BoardOrigin);

                    if (pegs[i].Presenter != null && theme != null)
                    {
                        pegs[i].Presenter.ResetVisuals(theme.PegCapRestColor, theme.PegGlowColor);
                    }

                    activePegs.Add(pegs[i]);
                }
                else
                {
                    pegs[i].gameObject.SetActive(false);
                }
            }

            PegsRemaining = activePegs.Count;

            // The floor's light used to be a pool placed here, under the pegs. It is the
            // whole deck now, and it follows the player's streak rather than the board's
            // layout, so it lives in the feedback layer: see FloorGlow.
        }

        private void ApplyTheme(StageTheme theme)
        {
            if (theme == null)
            {
                return;
            }

            if (themedBody != null)
            {
                themedBody.SetColor(BaseColorId, theme.PegColor);
            }

            if (themedStripe != null)
            {
                themedStripe.SetColor(BaseColorId, theme.PegStripeColor);
            }
        }

        private void OnDestroy()
        {
            // Runtime clones belong to nobody else.
            if (themedBody != null)
            {
                Destroy(themedBody);
            }

            if (themedStripe != null)
            {
                Destroy(themedStripe);
            }
        }

        public void ClearBoard()
        {
            for (int i = 0; i < pegs.Count; i++)
            {
                pegs[i].gameObject.SetActive(false);
            }

            activePegs.Clear();
            PegsRemaining = 0;

            // Leaving play is leaving the weather. Every way back to the title goes
            // through here, and the title has no level to have a wind.
            GameEvents.RaiseWindChanged(Vector3.zero);
        }

        /// <summary>
        /// Moves the pegs. Driven from the outside so every peg shares one clock and
        /// a paused level really stops.
        /// </summary>
        public void TickPegs(float deltaTime)
        {
            levelTime += deltaTime;

            // Announced every frame the level is being played, not only while a ring is
            // in the air. Seeing which way it blows *before* committing to a throw is
            // the entire reason the wind is drawn: a crosswind you can only discover by
            // losing a ring to it is not a mechanic, it is a trap — which is what level
            // 44 was until somebody reported it.
            if (level != null && level.HasWind)
            {
                GameEvents.RaiseWindChanged(level.WindAt(levelTime));
            }

            for (int i = 0; i < activePegs.Count; i++)
            {
                activePegs[i].Tick(deltaTime);
            }
        }

        public void BeginThrow()
        {
            for (int i = 0; i < activePegs.Count; i++)
            {
                activePegs[i].ResetForThrow();
            }
        }

        /// <summary>
        /// Wind and aim assistance for one physics step. Called from FixedUpdate while
        /// a ring is in flight.
        /// </summary>
        public void ApplyFlightForces(RingInstance ring, float flightTime)
        {
            if (ring == null || !ring.IsFlying)
            {
                return;
            }

            if (level != null)
            {
                Vector3 wind = level.WindAt(flightTime);

                if (wind.sqrMagnitude > 0f)
                {
                    ring.Controller.AddAcceleration(wind);
                }
            }

            ApplyAssist(ring);
        }

        private void ApplyAssist(RingInstance ring)
        {
            if (assistanceConfig == null || !assistanceConfig.Enabled)
            {
                return;
            }

            Vector3 velocity = ring.Controller.Body.linearVelocity;

            if (assistanceConfig.DescendingOnly && velocity.y > 0f)
            {
                return;
            }

            float radius = assistanceConfig.RadiusForMissStreak(consecutiveMisses);
            PegTarget best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < activePegs.Count; i++)
            {
                PegTarget peg = activePegs[i];

                if (!peg.IsAvailable)
                {
                    continue;
                }

                Vector3 offset = ring.Centre - peg.transform.position;
                float heightAbovePeg = offset.y - (peg.BaseHeight + peg.CurrentHeight);

                if (heightAbovePeg > assistanceConfig.ActivationHeight || heightAbovePeg < -0.2f)
                {
                    continue;
                }

                offset.y = 0f;
                float distance = offset.magnitude;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = peg;
                }
            }

            if (best == null)
            {
                return;
            }

            float strength = assistanceConfig.StrengthAtDistance(bestDistance, radius);

            if (strength <= 0f)
            {
                return;
            }

            Vector3 towards = best.transform.position - ring.Centre;
            towards.y = 0f;

            if (towards.sqrMagnitude < 1e-6f)
            {
                return;
            }

            ring.Controller.AddAcceleration(towards.normalized * strength);
        }

        /// <summary>
        /// The peg that just caught this ring, or null. Only one capture can happen in
        /// a step: once a ring is over a peg it cannot also be over another.
        /// </summary>
        public PegTarget EvaluateCapture(RingInstance ring)
        {
            for (int i = 0; i < activePegs.Count; i++)
            {
                if (activePegs[i].TryCapture(ring))
                {
                    return activePegs[i];
                }
            }

            return null;
        }

        public void ClaimPeg(PegTarget peg)
        {
            if (peg == null || peg.IsClaimed)
            {
                return;
            }

            peg.MarkClaimed();
            PegsRemaining = Mathf.Max(0, PegsRemaining - 1);
            consecutiveMisses = 0;

            GameEvents.RaisePegClaimed(peg, PegsRemaining);
        }

        public void RegisterMiss()
        {
            consecutiveMisses++;
        }

        public void Configure(
            PhysicsConfig physics,
            AssistanceConfig assistance,
            JuiceConfig juice,
            Transform board,
            Material body,
            Material stripe,
            Material cap,
            Material glow,
            Mesh disc,
            PhysicsMaterial pegPhysics,
            int capacity)
        {
            physicsConfig = physics;
            assistanceConfig = assistance;
            juiceConfig = juice;
            boardRoot = board;
            pegBodyMaterial = body;
            pegStripeMaterial = stripe;
            pegCapMaterial = cap;
            pegGlowMaterial = glow;
            discMesh = disc;
            pegPhysicsMaterial = pegPhysics;
            pegCapacity = capacity;
        }
    }
}
