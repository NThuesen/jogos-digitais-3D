using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Core;
using PrimeTween;
using Unity.Cinemachine;
using UnityEngine;

namespace SlipperyZlope.RingToss.Juice
{
    /// <summary>
    /// The camera's share of the feedback: the small zoom-out while aiming, and the
    /// shake on impact.
    ///
    /// Cinemachine owns the camera, so nothing here moves the Camera object. The zoom
    /// writes to the virtual camera's lens and the shake goes through Impulse, which
    /// means shake, framing and any future camera behaviour compose instead of
    /// fighting over the same transform.
    ///
    /// Shake amplitudes are deliberately small, and were cut again after the first
    /// play test. Over-shaking is the standard way this kind of feedback goes wrong,
    /// and it is much harder to notice while tuning than it is on a phone.
    ///
    /// A perfect ringer does not shake the camera at all. Shake reads as impact, and a
    /// perfect is the one throw that never touched anything — letting the screen sit
    /// completely still is what sells it, and it separates a clean catch from a
    /// scrappy one far better than a bigger kick would. The perfect still gets its
    /// longer hit stop, its brighter chime and its own particles; it just gets them
    /// against a steady frame.
    /// </summary>
    public sealed class CameraJuice : MonoBehaviour
    {
        [SerializeField] private JuiceConfig juiceConfig;
        [SerializeField] private CinemachineCamera gameplayCamera;
        [SerializeField] private CinemachineImpulseSource impulseSource;

        private float baseFieldOfView = 40f;
        private Vector3 basePosition;
        private float aimBlend;
        private float aimTarget;

        private void Awake()
        {
            if (gameplayCamera != null)
            {
                baseFieldOfView = gameplayCamera.Lens.FieldOfView;
                basePosition = gameplayCamera.transform.position;
            }
        }

        private void OnEnable()
        {
            GameEvents.AimChanged += OnAimChanged;
            GameEvents.AimCancelled += OnAimCancelled;
            GameEvents.Thrown += OnThrown;
            GameEvents.Impact += OnImpact;
            GameEvents.Resolved += OnResolved;
            GameEvents.LevelStarted += OnLevelStarted;
        }

        private void OnDisable()
        {
            GameEvents.AimChanged -= OnAimChanged;
            GameEvents.AimCancelled -= OnAimCancelled;
            GameEvents.Thrown -= OnThrown;
            GameEvents.Impact -= OnImpact;
            GameEvents.Resolved -= OnResolved;
            GameEvents.LevelStarted -= OnLevelStarted;
        }

        public void Bind(JuiceConfig juice, CinemachineCamera camera, CinemachineImpulseSource impulse)
        {
            juiceConfig = juice;
            gameplayCamera = camera;
            impulseSource = impulse;

            if (camera != null)
            {
                baseFieldOfView = camera.Lens.FieldOfView;
                basePosition = camera.transform.position;
            }
        }

        private void OnAimChanged(AimState state)
        {
            aimTarget = state.NormalisedForce;
        }

        private void OnAimCancelled()
        {
            aimTarget = 0f;
        }

        private void OnThrown(ThrowLaunched payload)
        {
            aimTarget = 0f;

            // A flick of shake on release sells the effort of the throw. Scaled by
            // force so a gentle lob does not kick the camera.
            Shake(juiceConfig == null ? 0f : juiceConfig.ShakeOnThrow * payload.NormalisedForce, Vector3.up);
        }

        private void OnImpact(RingImpact payload)
        {
            if (juiceConfig == null)
            {
                return;
            }

            // Scenery scrapes are ignored: shaking on every bounce along the ground
            // turns the end of a miss into visual noise.
            if (!payload.HitPeg)
            {
                return;
            }

            float strength = Mathf.Clamp01(payload.Speed / 6f);
            Shake(juiceConfig.ShakeOnBounce * strength, payload.Normal);
        }

        private void OnResolved(ThrowResolved payload)
        {
            if (juiceConfig == null)
            {
                return;
            }

            // Only the scrappy catch shakes. PerfectRinger is deliberately absent:
            // see the note at the top of this class.
            if (payload.Outcome == ThrowOutcome.Ringer)
            {
                Shake(juiceConfig.ShakeOnRinger, Vector3.up);
            }
        }

        private void OnLevelStarted(int levelIndex)
        {
            aimTarget = 0f;
            aimBlend = 0f;

            if (gameplayCamera == null || juiceConfig == null)
            {
                return;
            }

            gameplayCamera.Lens.FieldOfView = baseFieldOfView;

            // A short push-in to open the level. Small enough to read as the camera
            // settling rather than as a cutscene.
            Transform rig = gameplayCamera.transform;
            Vector3 from = basePosition - new Vector3(0f, 0.35f, 0.75f);

            rig.position = from;
            Tween.Position(rig, from, basePosition, juiceConfig.LevelIntroDuration, Ease.OutCubic);
        }

        private void Update()
        {
            if (gameplayCamera == null || juiceConfig == null)
            {
                return;
            }

            aimBlend = Mathf.Lerp(
                aimBlend,
                aimTarget,
                1f - Mathf.Exp(-(1f / Mathf.Max(0.01f, juiceConfig.AimZoomDuration)) * Time.deltaTime));

            gameplayCamera.Lens.FieldOfView =
                baseFieldOfView * (1f + juiceConfig.AimZoomAmount * aimBlend);
        }

        private void Shake(float amplitude, Vector3 direction)
        {
            if (impulseSource == null || amplitude <= 0f)
            {
                return;
            }

            if (direction.sqrMagnitude < 1e-4f)
            {
                direction = Vector3.up;
            }

            impulseSource.GenerateImpulseWithVelocity(direction.normalized * amplitude);
        }
    }
}
