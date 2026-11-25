using SlipperyZlope.RingToss.Config;
using UnityEngine;

namespace SlipperyZlope.RingToss.Gameplay
{
    /// <summary>
    /// Owns the ring's rigidbody. Applies the physics settings that this game cannot
    /// work without, and nothing else: no input, no scoring, no presentation.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RingController : MonoBehaviour
    {
        [SerializeField] private PhysicsConfig config;

        private Rigidbody body;
        private Vector3 restPosition;
        private Quaternion restRotation;

        public bool IsFlying { get; private set; }

        public Rigidbody Body => body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ApplyConfiguration();

            restPosition = transform.position;
            restRotation = transform.rotation;
        }

        public void SetConfig(PhysicsConfig value)
        {
            config = value;

            if (body != null)
            {
                ApplyConfiguration();
            }
        }

        private void ApplyConfiguration()
        {
            // The ring is thin and travels fast. Without continuous detection it passes
            // straight through the peg between physics steps and the game silently breaks.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Physics runs at 60 Hz and rendering is uncapped; interpolation is what keeps
            // the flight smooth instead of stepped.
            body.interpolation = RigidbodyInterpolation.Interpolate;

            // Gravity is applied by hand in FixedUpdate so the multiplier is meaningful.
            body.useGravity = false;

            if (config == null)
            {
                return;
            }

            body.mass = config.Mass;
            body.linearDamping = config.LinearDamping;
            body.angularDamping = config.AngularDamping;
        }

        private void FixedUpdate()
        {
            if (config == null || body.isKinematic)
            {
                return;
            }

            // ForceMode.Acceleration ignores mass, which is what gravity should do.
            body.AddForce(config.Gravity, ForceMode.Acceleration);
        }

        /// <summary>Holds the ring still at the throw origin, ready to be aimed.</summary>
        public void Arm(Vector3 position, Quaternion rotation)
        {
            restPosition = position;
            restRotation = rotation;

            // Velocities have to be cleared while the body is still dynamic: writing
            // them on a kinematic body is ignored and logs a warning every throw.
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.isKinematic = true;
            Teleport(position, rotation);
            IsFlying = false;
        }

        /// <summary>
        /// Moves the ring without letting interpolation smear the move.
        ///
        /// Writing only the transform is not enough. Interpolation renders the body
        /// between its last two physics poses, so a teleport is blended out of
        /// existence: the ring reads as still being wherever it was, and the next
        /// physics step snaps it back. A pooled ring armed at the throw origin would
        /// spend its first frames somewhere else entirely, and a ring armed for a
        /// scripted throw would simply never arrive.
        ///
        /// Interpolation is switched off across the write so the body's history is
        /// discarded, and the rigidbody pose is set alongside the transform so PhysX
        /// and Unity agree before the next step.
        /// </summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            RigidbodyInterpolation previous = body.interpolation;
            body.interpolation = RigidbodyInterpolation.None;

            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;

            body.interpolation = previous;
        }

        public void Launch(Vector3 velocity, Vector3 angularVelocity)
        {
            body.isKinematic = false;

            // Interpolation only matters while physics is driving the ring. It is
            // switched off the moment the ring is caught or retired, because from
            // there the pose is written by hand every frame and interpolating it
            // against stale physics poses only adds lag.
            body.interpolation = RigidbodyInterpolation.Interpolate;

            body.linearVelocity = velocity;
            body.angularVelocity = angularVelocity;
            IsFlying = true;
        }

        /// <summary>
        /// Stops the body dead and hands the transform back to whatever wants to
        /// animate it. Used the moment a ring is caught: from there the settle is
        /// authored, not simulated.
        /// </summary>
        public void Freeze()
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
            IsFlying = false;
        }

        /// <summary>
        /// Applies an external acceleration for one physics step. Mass independent,
        /// so wind and aim assistance push every ring the same way regardless of the
        /// level's ring size.
        /// </summary>
        public void AddAcceleration(Vector3 acceleration)
        {
            if (body.isKinematic)
            {
                return;
            }

            body.AddForce(acceleration, ForceMode.Acceleration);
        }

        /// <summary>Returns the ring to where it was last armed.</summary>
        public void ResetToRest()
        {
            Arm(restPosition, restRotation);
        }

        /// <summary>
        /// True once the ring has effectively stopped, used to decide when a throw is
        /// over without waiting for the physics engine to put it to sleep.
        /// </summary>
        public bool IsAtRest(float linearThreshold, float angularThreshold)
        {
            return body.linearVelocity.sqrMagnitude < linearThreshold * linearThreshold
                   && body.angularVelocity.sqrMagnitude < angularThreshold * angularThreshold;
        }
    }
}
