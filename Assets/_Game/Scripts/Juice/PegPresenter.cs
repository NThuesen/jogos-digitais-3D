using SlipperyZlope.RingToss.Config;
using PrimeTween;
using UnityEngine;

namespace SlipperyZlope.RingToss.Juice
{
    /// <summary>
    /// A peg's reactions: the idle shimmer on its cap, the squash when a ring clips
    /// it, and the flare when it is finally claimed.
    ///
    /// Colour is driven entirely through a MaterialPropertyBlock. Writing to
    /// renderer.material would clone the material for every peg on the board, which
    /// costs a material per peg and breaks batching for scenery that is otherwise
    /// identical.
    /// </summary>
    public sealed class PegPresenter : MonoBehaviour
    {
        [SerializeField] private JuiceConfig juiceConfig;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private MeshRenderer capRenderer;
        [SerializeField] private MeshRenderer glowRenderer;
        [SerializeField] private Transform glowTransform;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock block;
        private Color glowColor = Color.white;
        private Color capColor = Color.white;
        private float idlePhase;
        private bool claimed;
        private Vector3 baseVisualScale = Vector3.one;

        // Held so a second hit can stop the first reaction. Once a tween is inside a
        // Sequence the Sequence owns it, and stopping it directly is an error.
        private Sequence bodyReaction;
        private Sequence glowReaction;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            idlePhase = Random.value * Mathf.PI * 2f;
        }

        public void Bind(
            JuiceConfig juice,
            Transform visual,
            MeshRenderer cap,
            MeshRenderer glow,
            Transform glowRoot)
        {
            juiceConfig = juice;
            visualRoot = visual;
            capRenderer = cap;
            glowRenderer = glow;
            glowTransform = glowRoot;
        }

        public void SetConfig(JuiceConfig value)
        {
            juiceConfig = value;
        }

        /// <summary>Puts the peg back to its unclaimed look. Called when a level starts.</summary>
        public void ResetVisuals(Color cap, Color glow)
        {
            claimed = false;
            capColor = cap;
            glowColor = glow;

            if (visualRoot != null)
            {
                baseVisualScale = visualRoot.localScale;
            }

            ApplyCap();
            ApplyGlow(0.22f);
        }

        /// <summary>A ring clipped the peg. Squash, flash, and let it spring back.</summary>
        public void PlayHit(float strength)
        {
            if (juiceConfig == null || visualRoot == null)
            {
                return;
            }

            float amount = juiceConfig.PegSquashAmount * Mathf.Clamp01(strength);
            float duration = juiceConfig.PegSquashDuration;

            StopIfAlive(ref bodyReaction);

            // Volume-preserving squash: shorter and wider, then elastic back. A peg
            // that only scales down reads as shrinking, not as being struck.
            Vector3 squashed = new Vector3(
                baseVisualScale.x * (1f + amount * 0.6f),
                baseVisualScale.y * (1f - amount),
                baseVisualScale.z * (1f + amount * 0.6f));

            bodyReaction = Sequence.Create()
                .Group(Tween.Scale(visualRoot, baseVisualScale, squashed, duration * 0.25f, Ease.OutQuad))
                .Chain(Tween.Scale(visualRoot, squashed, baseVisualScale, duration * 0.75f, Ease.OutElastic));

        }

        /// <summary>The peg has been ringed. This is the biggest reaction in the game.</summary>
        public void PlayClaimed(Color successColor)
        {
            claimed = true;
            capColor = successColor;

            if (juiceConfig == null || visualRoot == null)
            {
                ApplyCap();
                return;
            }

            StopIfAlive(ref bodyReaction);

            Vector3 stretched = new Vector3(
                baseVisualScale.x * 0.86f,
                baseVisualScale.y * 1.16f,
                baseVisualScale.z * 0.86f);

            bodyReaction = Sequence.Create()
                .Group(Tween.Scale(visualRoot, baseVisualScale, stretched, 0.12f, Ease.OutQuad))
                .Chain(Tween.Scale(visualRoot, stretched, baseVisualScale, 0.55f, Ease.OutElastic));

            if (glowTransform != null)
            {
                Vector3 glowScale = glowTransform.localScale;

                StopIfAlive(ref glowReaction);

                glowReaction = Sequence.Create()
                    .Group(Tween.Scale(glowTransform, glowScale.x, glowScale.x * 2.1f, 0.28f, Ease.OutCubic))
                    .Chain(Tween.Scale(glowTransform, glowScale.x * 2.1f, glowScale.x * 1.35f, 0.4f, Ease.OutBack));
            }
        }

        private static void StopIfAlive(ref Sequence sequence)
        {
            if (sequence.isAlive)
            {
                sequence.Stop();
            }
        }

        private void Update()
        {
            // A slow shimmer keeps an untouched peg alive on screen without asking for
            // attention. Claimed pegs sit brighter and steadier so the two states read
            // apart at a glance.
            idlePhase += Time.deltaTime * (claimed ? 1.4f : 2.2f);

            float shimmer = 1f + Mathf.Sin(idlePhase) * (claimed ? 0.06f : 0.14f);
            ApplyGlow((claimed ? 0.55f : 0.22f) * shimmer);
        }

        private void ApplyCap()
        {
            if (capRenderer == null)
            {
                return;
            }

            capRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, capColor);

            // The ball on top of the peg does not light up. It used to, and once the
            // emission variant actually started shipping the owner saw it on a handset
            // and asked for it back the way it was: "não é para a bola no topo da estaca
            // brilhar". The peg still reacts — it squashes, it stretches when it is
            // ringed, it shimmers — it just does it in colour and movement rather than
            // by glowing, which is also the only way the *floor's* streak glow stays the
            // one lit thing in the frame.
            block.SetColor(EmissionColorId, Color.black);
            capRenderer.SetPropertyBlock(block);
        }

        private void ApplyGlow(float alpha)
        {
            if (glowRenderer == null)
            {
                return;
            }

            glowRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, new Color(glowColor.r, glowColor.g, glowColor.b, alpha));
            glowRenderer.SetPropertyBlock(block);
        }
    }
}
