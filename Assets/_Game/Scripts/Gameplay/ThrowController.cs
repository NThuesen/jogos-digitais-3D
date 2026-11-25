using SlipperyZlope.RingToss.Config;
using SlipperyZlope.RingToss.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SlipperyZlope.RingToss.Gameplay
{
    /// <summary>
    /// Drag and flick input, and nothing else.
    ///
    /// The pointer is read directly rather than through an action asset: one finger,
    /// one gesture, and the same code path serves mouse in the editor and touch on
    /// device. Reading it in Update rather than through an event keeps the response
    /// same-frame, which the spec treats as non-negotiable.
    ///
    /// This class does not decide anything about the game. It arms the ring it is
    /// given, launches it, and announces the launch. Flight, scoring and the level
    /// belong to <see cref="Core.GameDirector"/>.
    /// </summary>
    public sealed class ThrowController : MonoBehaviour
    {
        private enum InputState
        {
            /// <summary>Input ignored entirely: menus, transitions, ring in flight.</summary>
            Locked,

            /// <summary>Ring is armed and waiting for a finger.</summary>
            Ready,

            Aiming
        }

        [Header("References")]
        [SerializeField] private PhysicsConfig config;
        [SerializeField] private Transform throwOrigin;
        [SerializeField] private TrajectoryPreview trajectoryPreview;

        private InputState state = InputState.Locked;
        private RingInstance ring;

        // The gesture is latched to the device and the finger that started it. Reading
        // Pointer.current every frame instead lets a second finger — or the mouse
        // moving while a touch is down — retarget the drag mid-throw, which reads as
        // the ring lurching for no reason the player can see.
        private Pointer activePointer;
        private int activeTouchId = -1;

        private Vector2 pressPosition;
        private Vector2 currentDrag;
        private Vector2 previousDrag;
        private bool hasAimState;
        private AimState lastAim;

        public bool IsAiming => state == InputState.Aiming;

        /// <summary>The aim as of the last frame of the drag. Zeroed between throws.</summary>
        public AimState CurrentAim => hasAimState ? lastAim : default;

        public RingInstance ArmedRing => ring;

        public Transform Origin => throwOrigin;

        /// <summary>Hands a ring to the player and starts listening for a drag.</summary>
        public void ArmRing(RingInstance value)
        {
            ring = value;
            state = InputState.Ready;
            ClearGesture();

            if (ring != null)
            {
                ring.Arm(throwOrigin.position, throwOrigin.rotation);
                GameEvents.RaiseThrowArmed(ring);
            }
        }

        /// <summary>Stops accepting input. Any in-progress aim is abandoned.</summary>
        public void Lock()
        {
            if (state == InputState.Aiming)
            {
                GameEvents.RaiseAimCancelled();
            }

            state = InputState.Locked;
            ring = null;
            ClearGesture();
            HidePreview();
        }

        private void ClearGesture()
        {
            activePointer = null;
            activeTouchId = -1;
            currentDrag = Vector2.zero;
            previousDrag = Vector2.zero;
            hasAimState = false;
            lastAim = default;
        }

        private void Update()
        {
            switch (state)
            {
                case InputState.Ready:
                    TickReady();
                    break;
                case InputState.Aiming:
                    TickAiming();
                    break;
            }
        }

        private void TickReady()
        {
            Pointer pointer = Pointer.current;

            if (pointer == null || !pointer.press.wasPressedThisFrame)
            {
                return;
            }

            if (IsPointerOverUi(pointer))
            {
                return;
            }

            activePointer = pointer;
            activeTouchId = pointer is Touchscreen touchscreen
                ? touchscreen.primaryTouch.touchId.ReadValue()
                : -1;

            pressPosition = pointer.position.ReadValue();
            currentDrag = Vector2.zero;
            previousDrag = Vector2.zero;
            state = InputState.Aiming;
        }

        private void TickAiming()
        {
            if (activePointer == null || !activePointer.added)
            {
                // The device went away mid-gesture. Whatever the player had wound up
                // is still a throw they meant to make, so it is released rather than
                // silently dropped.
                Release(currentDrag);
                return;
            }

            if (!IsTrackedFingerStillDown())
            {
                Release(currentDrag);
                return;
            }

            previousDrag = currentDrag;
            currentDrag = activePointer.position.ReadValue() - pressPosition;

            if (activePointer.press.wasReleasedThisFrame || !activePointer.press.isPressed)
            {
                Release(ReleaseDrag());
                return;
            }

            UpdateAim(currentDrag);
        }

        /// <summary>
        /// False once the finger that started the gesture is gone, even if another one
        /// is still on the glass.
        /// </summary>
        private bool IsTrackedFingerStillDown()
        {
            if (activeTouchId < 0 || activePointer is not Touchscreen touchscreen)
            {
                return true;
            }

            return touchscreen.primaryTouch.touchId.ReadValue() == activeTouchId;
        }

        /// <summary>
        /// The drag a release should be judged on.
        ///
        /// Touch hardware sometimes reports the lift-off sample short of where the
        /// finger actually was, and on a fast flick that single bad sample is the
        /// difference between a full-power throw and a dribble. Taking whichever of
        /// the last two frames went further forward costs nothing when the player is
        /// easing off — two consecutive frames of a deliberate movement are nearly
        /// identical — and rescues the flick when they are not.
        /// </summary>
        private Vector2 ReleaseDrag()
        {
            // "Further forward" depends on which way forward is. Under the slingshot it
            // was further *down* the screen, and this compared raw y regardless — so on
            // a fast pull it rescued the wrong sample and kept the weaker one.
            float forward = config != null && config.InvertDragDirection ? -1f : 1f;
            return currentDrag.y * forward >= previousDrag.y * forward ? currentDrag : previousDrag;
        }

        private void UpdateAim(Vector2 dragPixels)
        {
            ThrowSolution preview = ThrowSolver.Solve(
                dragPixels, Screen.height, AimBasis, LaunchHeight, config);

            if (!preview.IsValid)
            {
                if (hasAimState)
                {
                    hasAimState = false;
                    lastAim = default;
                    GameEvents.RaiseAimChanged(default);
                }

                HidePreview();
                return;
            }

            AimState aim = new AimState(
                preview.NormalisedForce,
                NormalisedYaw(dragPixels),
                preview.Range);

            hasAimState = true;
            lastAim = aim;
            GameEvents.RaiseAimChanged(aim);

            if (trajectoryPreview != null)
            {
                // From the middle of the ring as drawn, not the transform it hangs from.
                trajectoryPreview.Show(
                    ring != null ? ring.VisualCentre : throwOrigin.position,
                    preview.Velocity,
                    config.Gravity,
                    preview.NormalisedForce,
                    LandingPoint(preview));
            }
        }

        /// <summary>
        /// Where the throw is aimed to arrive, on the board plane. This is the point
        /// the player is actually choosing, so it is worth drawing: the arc alone
        /// leaves them guessing which peg a given drag reaches.
        /// </summary>
        private Vector3 LandingPoint(in ThrowSolution solution)
        {
            Vector3 flat = new Vector3(solution.Velocity.x, 0f, solution.Velocity.z);

            if (flat.sqrMagnitude < 1e-6f)
            {
                return throwOrigin.position;
            }

            Vector3 origin = throwOrigin.position;
            return new Vector3(origin.x, 0f, origin.z) + flat.normalized * solution.Range;
        }

        private float NormalisedYaw(Vector2 dragPixels)
        {
            if (config == null || Screen.height <= 0)
            {
                return 0f;
            }

            float lateral = (config.InvertDragDirection ? -dragPixels.x : dragPixels.x) / Screen.height;

            return Mathf.Clamp(
                lateral / Mathf.Max(config.LateralDragScreenFraction, Mathf.Epsilon), -1f, 1f);
        }

        private void Release(Vector2 dragPixels)
        {
            HidePreview();

            if (ThrowSolver.ShouldCancel(dragPixels, Screen.height, config))
            {
                CancelAim();
                return;
            }

            ThrowSolution solution = ThrowSolver.Solve(
                dragPixels, Screen.height, AimBasis, LaunchHeight, config);

            if (!solution.IsValid)
            {
                CancelAim();
                return;
            }

            LaunchArmedRing(solution.Velocity, solution.AngularVelocity, solution.NormalisedForce);
        }

        /// <summary>
        /// Throws the armed ring with a velocity that did not come from a drag.
        ///
        /// This is the seam every throw goes through, including the player's: Release
        /// solves the drag and then calls this. Having one exit means a scripted throw
        /// and a real one cannot diverge, which is what makes the loop testable
        /// without synthesising touch input.
        /// </summary>
        public void LaunchArmedRing(Vector3 velocity, Vector3 angularVelocity, float normalisedForce = 1f)
        {
            if (ring == null)
            {
                return;
            }

            RingInstance launched = ring;

            // Input stops the moment the ring leaves the hand. The director owns the
            // throw from here, and re-arming is its call, not this class's.
            state = InputState.Locked;
            ring = null;
            ClearGesture();
            HidePreview();

            launched.Launch(velocity, angularVelocity);

            GameEvents.RaiseThrown(new ThrowLaunched(
                throwOrigin.position, velocity, Mathf.Clamp01(normalisedForce)));
        }

        private void CancelAim()
        {
            state = ring == null ? InputState.Locked : InputState.Ready;
            ClearGesture();
            HidePreview();
            GameEvents.RaiseAimCancelled();
        }

        private void HidePreview()
        {
            if (trajectoryPreview != null)
            {
                trajectoryPreview.Hide();
            }
        }

        /// <summary>
        /// Keeps a tap on a HUD button from also throwing the ring. Guarded because
        /// there is no EventSystem at all in the physics test scenes.
        /// </summary>
        private static bool IsPointerOverUi(Pointer pointer)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            if (pointer is Touchscreen touchscreen)
            {
                return EventSystem.current.IsPointerOverGameObject(touchscreen.primaryTouch.touchId.ReadValue());
            }

            return EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>
        /// Yaw-only basis, so the throw is measured against the direction the origin
        /// faces and never against its pitch or roll.
        /// </summary>
        private Quaternion AimBasis =>
            throwOrigin == null
                ? Quaternion.identity
                : Quaternion.Euler(0f, throwOrigin.eulerAngles.y, 0f);

        private float LaunchHeight => throwOrigin == null ? 1.15f : throwOrigin.position.y;

        public void Configure(PhysicsConfig physicsConfig, Transform origin, TrajectoryPreview preview)
        {
            config = physicsConfig;
            throwOrigin = origin;
            trajectoryPreview = preview;
        }
    }
}
