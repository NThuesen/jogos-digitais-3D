using UnityEngine;

namespace PocketPutt.Prototype
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class PrototypeHole : MonoBehaviour
    {
        [SerializeField] private PrototypePuttController ballController;
        [SerializeField, Min(0.01f)] private float captureRadius = 0.27f;
        [SerializeField, Min(0f)] private float captureMaxSpeed = 1.5f;

        public void Configure(PrototypePuttController controller)
        {
            ballController = controller;
        }

        private void OnTriggerEnter(Collider other) => TryCapture(other);
        private void OnTriggerStay(Collider other) => TryCapture(other);

        private void TryCapture(Collider other)
        {
            if (ballController == null || ballController.Finished)
                return;

            Rigidbody body = other.attachedRigidbody;
            if (body == null || body.gameObject != ballController.gameObject)
                return;

            Vector3 difference = other.transform.position - transform.position;
            difference.y = 0f;
            Vector3 velocity = body.linearVelocity;
            velocity.y = 0f;

            if (difference.sqrMagnitude <= captureRadius * captureRadius &&
                velocity.sqrMagnitude <= captureMaxSpeed * captureMaxSpeed)
            {
                ballController.FinishHole();
            }
        }
    }
}
