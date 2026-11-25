using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Core;
using SlipperyZlope.RingToss.Gameplay;
using PrimeTween;
using UnityEngine;

namespace SlipperyZlope.RingToss.Juice
{
    /// <summary>
    /// Everything the ring does that physics does not: breathing while it waits,
    /// resisting the drag while it is aimed, settling onto a peg it has caught, and
    /// bowing out when it misses.
    ///
    /// The settle is the centrepiece. It is evaluated from a damped spring every
    /// frame — amplitude * e^(-decay*t) * sin(2*pi*frequency*t) — rather than played
    /// from a clip, so it starts from wherever the ring actually was, tracks a peg
    /// that is still moving, and can be retuned from the inspector.
    ///
    /// Settles are ticked from a fixed array rather than driven by tweens with
    /// captured locals, which keeps a catch free of allocation at exactly the moment
    /// the player is most likely to notice a hitch.
    /// </summary>
    public sealed class RingPresenter : MonoBehaviour
    {
        private struct Settle
        {
            public bool Active;
            public RingInstance Ring;
            public PegTarget Peg;
            public Vector3 StartPosition;
            public Quaternion StartRotation;
            public float Time;
            public float Amplitude;
        }

        [SerializeField] private JuiceConfig juiceConfig;

        [Tooltip("Concurrent settles. One per peg on the biggest board is plenty.")]
        [SerializeField] private int settleCapacity = 8;

        private Settle[] settles;
        private RingInstance armedRing;
        private float idleTime;
        private float aimTarget;
        private float aimCurrent;
        private float yawTarget;
        private float yawCurrent;
        private float armProgress;

        private void Awake()
        {
            settles = new Settle[Mathf.Max(1, settleCapacity)];
        }

        private void OnEnable()
        {
            GameEvents.ThrowArmed += OnThrowArmed;
            GameEvents.AimChanged += OnAimChanged;
            GameEvents.AimCancelled += OnAimCancelled;
            GameEvents.Thrown += OnThrown;
            GameEvents.Ringer += OnRinger;
            GameEvents.Resolved += OnResolved;
            GameEvents.LevelStarted += OnLevelStarted;
        }

        private void OnDisable()
        {
            GameEvents.ThrowArmed -= OnThrowArmed;
            GameEvents.AimChanged -= OnAimChanged;
            GameEvents.AimCancelled -= OnAimCancelled;
            GameEvents.Thrown -= OnThrown;
            GameEvents.Ringer -= OnRinger;
            GameEvents.Resolved -= OnResolved;
            GameEvents.LevelStarted -= OnLevelStarted;
        }

        public void SetConfig(JuiceConfig value)
        {
            juiceConfig = value;
        }

        private void OnLevelStarted(int levelIndex)
        {
            armedRing = null;

            for (int i = 0; i < settles.Length; i++)
            {
                settles[i] = default;
            }
        }

        private void OnThrowArmed(RingInstance ring)
        {
            armedRing = ring;
            idleTime = 0f;
            aimTarget = 0f;
            aimCurrent = 0f;

            armProgress = 0f;

            if (ring == null || ring.Visual == null || juiceConfig == null)
            {
                armProgress = 1f;
                return;
            }

            ring.Visual.localScale = Vector3.zero;

            // The arrival is a single 0..1 value rather than a tween on the transform.
            // The idle pose writes the ring's scale and rotation every frame, so a
            // tween writing the same properties would simply be overwritten and the
            // ring would appear instead of arriving. OutBack overshoots past 1, which
            // is where the pop comes from.
            Tween.Custom(this, 0f, 1f, juiceConfig.ArmPopDuration,
                (presenter, value) => presenter.armProgress = value, Ease.OutBack);
        }

        private void OnAimChanged(AimState state)
        {
            aimTarget = state.NormalisedForce;
            yawTarget = state.NormalisedYaw;
        }

        private void OnAimCancelled()
        {
            aimTarget = 0f;
            yawTarget = 0f;
        }

        private void OnThrown(ThrowLaunched payload)
        {
            if (armedRing != null && armedRing.Visual != null)
            {
                // Physics owns the ring from here; the presentation pose has to get out
                // of the way or it fights the rigidbody for the whole flight.
                armedRing.Visual.localPosition = Vector3.zero;
                armedRing.Visual.localRotation = Quaternion.identity;
                armedRing.Visual.localScale = Vector3.one;
            }

            armedRing = null;
            aimTarget = 0f;
            aimCurrent = 0f;
            yawTarget = 0f;
            yawCurrent = 0f;
        }

        private void OnRinger(RingerCaught payload)
        {
            BeginSettle(payload.Ring, payload.Peg, payload.IsPerfect);
        }

        private void OnResolved(ThrowResolved payload)
        {
            if (payload.Outcome.IsSuccess() || payload.Ring == null)
            {
                return;
            }

            BeginRetire(payload.Ring);
        }

        /// <summary>
        /// Hands a caught ring over to the spring. The ring keeps its world position
        /// for now and is only parented to the peg once it has come to rest, so a peg
        /// that is still sliding does not yank the ring sideways mid-settle.
        /// </summary>
        private void BeginSettle(RingInstance ring, PegTarget peg, bool isPerfect)
        {
            if (ring == null || peg == null || juiceConfig == null)
            {
                return;
            }

            int slot = -1;

            for (int i = 0; i < settles.Length; i++)
            {
                if (!settles[i].Active)
                {
                    slot = i;
                    break;
                }
            }

            if (slot < 0)
            {
                // Out of slots: snap rather than leave the ring hanging in the air.
                ring.transform.SetPositionAndRotation(
                    peg.RestPositionFor(ring),
                    Quaternion.Euler(0f, ring.transform.eulerAngles.y, 0f));
                ring.transform.SetParent(peg.transform, true);
                return;
            }

            settles[slot] = new Settle
            {
                Active = true,
                Ring = ring,
                Peg = peg,
                StartPosition = ring.transform.position,
                StartRotation = ring.transform.rotation,
                Time = 0f,
                Amplitude = isPerfect ? 1.25f : 1f
            };

            Transform visual = ring.Visual;

            if (visual != null)
            {
                float pop = juiceConfig.CatchPopScale;
                float duration = juiceConfig.CatchPopDuration;

                Sequence.Create()
                    .Group(Tween.Scale(visual, 1f, pop, duration * 0.35f, Ease.OutCubic))
                    .Chain(Tween.Scale(visual, pop, 1f, duration * 0.65f, Ease.OutElastic));
            }
        }

        private void BeginRetire(RingInstance ring)
        {
            if (ring == null || juiceConfig == null)
            {
                return;
            }

            Transform root = ring.transform;
            float linger = juiceConfig.MissLingerDuration;
            float fade = juiceConfig.MissFadeDuration;
            float scale = ring.ScaleMultiplier;

            // Lies there for a beat so the player sees where it went, then leaves.
            // Failure is never punished visually beyond this; the cost of a miss is
            // the ring, not a lecture.
            Sequence.Create()
                .ChainDelay(linger)
                .Group(Tween.Scale(root, scale, scale * 0.01f, fade, Ease.InBack))
                .Group(Tween.PositionY(root, root.position.y, root.position.y - 0.25f, fade, Ease.InQuad))
                .Group(Tween.Rotation(
                    root, root.eulerAngles, root.eulerAngles + new Vector3(0f, 220f, 0f), fade, Ease.InQuad))
                .ChainCallback(ring, target =>
                {
                    target.SetShadowVisible(false);
                    target.gameObject.SetActive(false);
                });
        }

        private void Update()
        {
            TickArmedRing();
            TickSettles();
        }

        /// <summary>
        /// The waiting pose. Breathing scale, a slow bob and a lazy spin, blended with
        /// however far the player has dragged. Several properties move at once on
        /// purpose: a ring that only changes scale reads as a UI element, one that
        /// breathes, drifts and turns reads as an object being held.
        /// </summary>
        private void TickArmedRing()
        {
            if (armedRing == null || armedRing.Visual == null || juiceConfig == null)
            {
                return;
            }

            if (armedRing.State != RingState.Armed)
            {
                armedRing = null;
                return;
            }

            float deltaTime = Time.deltaTime;
            idleTime += deltaTime;

            float follow = 1f - Mathf.Exp(-juiceConfig.AimDamping * deltaTime);
            aimCurrent = Mathf.Lerp(aimCurrent, aimTarget, follow);
            yawCurrent = Mathf.Lerp(yawCurrent, yawTarget, follow);

            float breathePhase = idleTime * (Mathf.PI * 2f / Mathf.Max(0.01f, juiceConfig.IdleBreathDuration));
            float breathe = 1f + Mathf.Sin(breathePhase) * juiceConfig.IdleBreathAmount;
            float bob = Mathf.Sin(breathePhase * 0.85f) * juiceConfig.IdleBobAmount;

            // Idle motion is damped away as the player commits to a drag, so the ring
            // stops drifting exactly when precision starts to matter.
            float idleWeight = 1f - aimCurrent;

            Transform visual = armedRing.Visual;

            // The ring stays exactly where it will leave from. It used to be pulled
            // back towards the player by 0.34 m at full power and slid sideways with
            // the aim, while the dotted arc was drawn from the launch point — so at any
            // real drag the arc left from the ring's far rim, one ring radius from its
            // centre, and that is what the owner saw. The pose now says "loaded" and
            // "aimed" with rotation and squash alone, and the centre of the ring, the
            // start of the arc and the start of the flight are one point.
            visual.localPosition = new Vector3(0f, bob * idleWeight, 0f);

            // The arrival spin folds into the same rotation the idle pose writes.
            float arrivalSpin = Mathf.Lerp(-160f, 0f, Mathf.Clamp01(armProgress));

            // Leans into the push: the front edge dips as the throw builds.
            visual.localRotation = Quaternion.Euler(
                juiceConfig.AimTiltDegrees * aimCurrent,
                arrivalSpin + idleTime * juiceConfig.IdleSpinSpeed * idleWeight,
                -juiceConfig.AimRollDegrees * yawCurrent);

            // Squash slightly as the push builds: the ring reads as loaded.
            float stretch = 1f + aimCurrent * 0.08f;
            visual.localScale = new Vector3(
                breathe * stretch,
                breathe * (1f - aimCurrent * 0.06f),
                breathe * stretch) * armProgress;
        }

        private void TickSettles()
        {
            if (juiceConfig == null)
            {
                return;
            }

            float duration = Mathf.Max(0.01f, juiceConfig.SettleDuration);
            float deltaTime = Time.deltaTime;

            for (int i = 0; i < settles.Length; i++)
            {
                if (!settles[i].Active)
                {
                    continue;
                }

                ref Settle settle = ref settles[i];

                if (settle.Ring == null || settle.Peg == null)
                {
                    settle.Active = false;
                    continue;
                }

                settle.Time += deltaTime;

                float normalised = Mathf.Clamp01(settle.Time / duration);
                float approach = 1f - Mathf.Pow(1f - normalised, 3f);

                // Recomputed every frame rather than captured, so the ring rides a peg
                // that is still sliding instead of settling where it used to be.
                Vector3 restPosition = settle.Peg.RestPositionFor(settle.Ring);
                Vector3 position = Vector3.LerpUnclamped(settle.StartPosition, restPosition, approach);

                float spring = juiceConfig.EvaluateSettle(settle.Time, settle.Amplitude);
                position.y = Mathf.Max(position.y + spring, restPosition.y);

                Quaternion flat = Quaternion.Euler(0f, settle.StartRotation.eulerAngles.y, 0f);
                Quaternion rotation = Quaternion.SlerpUnclamped(settle.StartRotation, flat, approach);

                // The same spring drives a tilt wobble, so the ring rocks as it drops
                // rather than sliding down a rail.
                float tilt = spring * juiceConfig.SettleTiltPerMetre;
                rotation *= Quaternion.Euler(tilt, 0f, tilt * 0.55f);

                settle.Ring.transform.SetPositionAndRotation(position, rotation);

                if (settle.Time < duration)
                {
                    continue;
                }

                settle.Ring.transform.SetPositionAndRotation(restPosition, flat);
                settle.Ring.transform.SetParent(settle.Peg.transform, true);
                settle.Active = false;
                settle.Ring = null;
                settle.Peg = null;
            }
        }
    }
}
