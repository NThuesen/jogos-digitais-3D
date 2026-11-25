using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Utils;
using UnityEngine;

namespace SlipperyZlope.RingToss.Juice
{
    /// <summary>
    /// Every one-shot visual effect, pre-built and reused.
    ///
    /// Effects are the things most likely to be spawned at the exact moment the player
    /// is paying closest attention, which is the worst possible moment for an
    /// allocation. Nothing here instantiates during play: the particle systems are
    /// built once and emitted into by hand, and the shockwaves are a small ring buffer
    /// of meshes.
    /// </summary>
    public sealed class FxPool : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private JuiceConfig juiceConfig;

        [Header("Assets")]
        [SerializeField] private Mesh shockwaveMesh;
        [SerializeField] private Material shockwaveMaterial;
        [SerializeField] private Material sparkMaterial;
        [SerializeField] private Material confettiMaterial;
        [SerializeField] private Mesh confettiMesh;

        [Header("Sizes")]
        [Range(2, 16)]
        [SerializeField] private int shockwaveCount = 6;

        [SerializeField] private int sparkCapacity = 400;
        [SerializeField] private int confettiCapacity = 300;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>One expanding ring in flight.</summary>
        private struct Shockwave
        {
            public bool Active;
            public Transform Root;
            public MeshRenderer Renderer;
            public Color Color;
            public float MaxRadius;
            public float Duration;
            public float Time;
        }

        private Shockwave[] shockwaves;
        private MaterialPropertyBlock block;
        private int shockwaveCursor;

        private ParticleSystem sparks;
        private ParticleSystem confetti;
        private ParticleSystem dust;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            BuildShockwaves();

            sparks = BuildSparks();
            confetti = BuildConfetti();
            dust = BuildDust();
        }

        private void BuildShockwaves()
        {
            shockwaves = new Shockwave[shockwaveCount];

            for (int i = 0; i < shockwaveCount; i++)
            {
                GameObject wave = GeometryFactory.CreateMeshObject(
                    $"Shockwave_{i:D2}", transform, shockwaveMesh, shockwaveMaterial);
                wave.SetActive(false);

                shockwaves[i] = new Shockwave
                {
                    Root = wave.transform,
                    Renderer = wave.GetComponent<MeshRenderer>()
                };
            }
        }

        private ParticleSystem BuildSparks()
        {
            GameObject host = new GameObject("Sparks");
            host.transform.SetParent(transform, false);

            ParticleSystem system = host.AddComponent<ParticleSystem>();

            // A ParticleSystem starts playing the moment it is added, and several of
            // the module properties below cannot be written while it is running.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 3f;
            main.maxParticles = sparkCapacity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.4f;
            main.startSpeed = 0f;
            main.startSize = 0.08f;
            main.startLifetime = 0.55f;

            // Emission and shape are both off: every particle is emitted by hand with
            // its own velocity and colour, which is what makes one system able to serve
            // every result type without a system per colour.
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(FadeOutGradient(0.65f));

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));

            ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 1.6f;
            renderer.sharedMaterial = sparkMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            system.Play();
            return system;
        }

        private ParticleSystem BuildConfetti()
        {
            GameObject host = new GameObject("Confetti");
            host.transform.SetParent(transform, false);

            ParticleSystem system = host.AddComponent<ParticleSystem>();

            // A ParticleSystem starts playing the moment it is added, and several of
            // the module properties below cannot be written while it is running.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 6f;
            main.maxParticles = confettiCapacity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.85f;
            main.startSpeed = 0f;
            main.startSize = 0.06f;
            main.startLifetime = 2.4f;
            main.startRotation3D = true;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            // Drag is what turns falling cubes into fluttering paper.
            ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.08f;
            limit.limit = 3.5f;

            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = new ParticleSystem.MinMaxCurve(-4f, 4f);
            rotation.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rotation.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(FadeOutGradient(0.8f));

            ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = confettiMesh;
            renderer.sharedMaterial = confettiMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            system.Play();
            return system;
        }

        private ParticleSystem BuildDust()
        {
            GameObject host = new GameObject("Dust");
            host.transform.SetParent(transform, false);

            ParticleSystem system = host.AddComponent<ParticleSystem>();

            // A ParticleSystem starts playing the moment it is added, and several of
            // the module properties below cannot be written while it is running.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 3f;
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.05f;
            main.startSpeed = 0f;
            main.startSize = 0.22f;
            main.startLifetime = 0.5f;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.6f));

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(FadeOutGradient(0.25f));

            ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = sparkMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            system.Play();
            return system;
        }

        /// <summary>Alpha ramp that holds, then fades. Built once; gradients allocate.</summary>
        private static Gradient FadeOutGradient(float holdUntil)
        {
            Gradient gradient = new Gradient();

            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, holdUntil),
                    new GradientAlphaKey(0f, 1f)
                });

            return gradient;
        }

        /// <summary>
        /// An expanding ring on the ground. Scaled and faded by a tween rather than a
        /// particle system, so its timing can be matched exactly to the hit stop.
        /// </summary>
        public void PlayShockwave(Vector3 position, Color color, float maxRadius, float duration)
        {
            if (shockwaves == null || shockwaves.Length == 0)
            {
                return;
            }

            int index = shockwaveCursor;
            shockwaveCursor = (shockwaveCursor + 1) % shockwaves.Length;

            ref Shockwave wave = ref shockwaves[index];

            wave.Active = true;
            wave.Color = color;
            wave.MaxRadius = Mathf.Max(0.2f, maxRadius);
            wave.Duration = Mathf.Max(0.05f, duration);
            wave.Time = 0f;

            wave.Root.gameObject.SetActive(true);

            // On the board rather than inside it. This was 0.02 above the peg's anchor,
            // the board root sits at zero and the deck's top is at 0.026, so every
            // shockwave this game has ever played was drawn underneath an opaque floor.
            // Nobody saw it — not in play, not in a single capture — because a hidden
            // flat mesh does not look broken, it looks like nothing happened.
            wave.Root.position = new Vector3(
                position.x, Mathf.Max(position.y, 0f) + BoardMetrics.GroundFxY, position.z);
            wave.Root.rotation = Quaternion.identity;
            wave.Root.localScale = Vector3.one * StartRadius;
        }

        private const float StartRadius = 0.15f;

        /// <summary>
        /// Shockwaves are ticked from an array rather than driven by tweens. A tween
        /// here would have to capture the colour and the renderer in a closure, and
        /// that closure would be allocated on the frame of every single catch — the
        /// one frame in the game where a collection is most likely to be felt.
        /// </summary>
        private void Update()
        {
            if (shockwaves == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            for (int i = 0; i < shockwaves.Length; i++)
            {
                if (!shockwaves[i].Active)
                {
                    continue;
                }

                ref Shockwave wave = ref shockwaves[i];
                wave.Time += deltaTime;

                float normalised = Mathf.Clamp01(wave.Time / wave.Duration);

                // Expands fast then eases out, fades on a slower curve, so the ring is
                // already wide by the time it becomes faint.
                float expand = 1f - Mathf.Pow(1f - normalised, 3f);
                float radius = Mathf.Lerp(StartRadius, wave.MaxRadius, expand);
                wave.Root.localScale = new Vector3(radius, 1f, radius);

                float alpha = 1f - normalised * normalised;

                wave.Renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, new Color(wave.Color.r, wave.Color.g, wave.Color.b, alpha));
                wave.Renderer.SetPropertyBlock(block);

                if (normalised < 1f)
                {
                    continue;
                }

                wave.Active = false;
                wave.Root.gameObject.SetActive(false);
            }
        }

        /// <summary>A radial burst around a point, biased upwards so it reads against the ground.</summary>
        public void PlaySparks(Vector3 position, Color color, int count, float speed, float size = 1f)
        {
            if (sparks == null)
            {
                return;
            }

            ParticleSystem.EmitParams parameters = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false
            };

            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) * 0.9f + 0.35f;
                direction.Normalize();

                parameters.position = position + direction * 0.05f;
                parameters.velocity = direction * speed * Random.Range(0.55f, 1.3f);
                parameters.startColor = color;
                parameters.startSize = Random.Range(0.05f, 0.11f) * size;
                parameters.startLifetime = Random.Range(0.35f, 0.7f);

                sparks.Emit(parameters, 1);
            }
        }

        /// <summary>Fired only at high combo. Below that it is noise rather than reward.</summary>
        public void PlayConfetti(Vector3 position, Color primary, Color secondary, int count)
        {
            if (confetti == null)
            {
                return;
            }

            ParticleSystem.EmitParams parameters = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false
            };

            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) * 1.4f + 0.8f;
                direction.Normalize();

                parameters.position = position + Random.insideUnitSphere * 0.12f;
                parameters.velocity = direction * Random.Range(2.2f, 4.6f);
                parameters.startColor = Color.Lerp(primary, secondary, Random.value);
                parameters.startSize = Random.Range(0.04f, 0.08f);
                parameters.startLifetime = Random.Range(1.6f, 2.8f);
                parameters.rotation3D = Random.insideUnitSphere * 180f;

                confetti.Emit(parameters, 1);
            }
        }

        /// <summary>A soft puff where the ring scuffs the ground.</summary>
        public void PlayDust(Vector3 position, Color color, float strength)
        {
            if (dust == null)
            {
                return;
            }

            int count = Mathf.Clamp(Mathf.RoundToInt(strength * 5f), 2, 14);

            ParticleSystem.EmitParams parameters = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false
            };

            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * 0.14f;

                parameters.position = position + new Vector3(offset.x, 0.02f, offset.y);
                parameters.velocity = new Vector3(offset.x, 0.25f, offset.y) * strength;
                parameters.startColor = color;
                parameters.startSize = Random.Range(0.14f, 0.3f);
                parameters.startLifetime = Random.Range(0.32f, 0.6f);

                dust.Emit(parameters, 1);
            }
        }

        public void Configure(
            JuiceConfig juice,
            Mesh shockwave,
            Material shockwaveMat,
            Material sparkMat,
            Material confettiMat,
            Mesh confettiShape)
        {
            juiceConfig = juice;
            shockwaveMesh = shockwave;
            shockwaveMaterial = shockwaveMat;
            sparkMaterial = sparkMat;
            confettiMaterial = confettiMat;
            confettiMesh = confettiShape;
        }
    }
}
