using UnityEngine;

namespace SlipperyZlope.RingToss.Gameplay
{
    /// <summary>
    /// Marks a collider as belonging to a peg and points back at it.
    ///
    /// Collision callbacks need to answer "was that a peg?" every contact. A
    /// TryGetComponent against this marker is one lookup with no allocation, where
    /// GetComponentInParent would walk the hierarchy on every scrape.
    /// </summary>
    public sealed class PegPart : MonoBehaviour
    {
        [SerializeField] private PegTarget owner;

        public PegTarget Owner => owner;

        public void SetOwner(PegTarget value)
        {
            owner = value;
        }
    }
}
