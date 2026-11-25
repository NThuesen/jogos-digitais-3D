using UnityEngine;

namespace SlipperyZlope.RingToss.Config
{
    /// <summary>
    /// Every skin and trail the shop sells, in the order it sells them.
    ///
    /// Index *is* identity: the save file stores what the player owns as a bitmask
    /// over these arrays. Reordering them re-labels somebody's purchases, so new
    /// items go on the end.
    /// </summary>
    [CreateAssetMenu(menuName = "SlipperyZlope/Cosmetics Library", fileName = "CosmeticsLibrary")]
    public sealed class CosmeticsLibrary : ScriptableObject
    {
        [SerializeField] private RingSkin[] skins = new RingSkin[0];
        [SerializeField] private TrailStyle[] trails = new TrailStyle[0];

        public int SkinCount => skins == null ? 0 : skins.Length;
        public int TrailCount => trails == null ? 0 : trails.Length;

        public RingSkin GetSkin(int index)
        {
            return skins == null || index < 0 || index >= skins.Length ? null : skins[index];
        }

        public TrailStyle GetTrail(int index)
        {
            return trails == null || index < 0 || index >= trails.Length ? null : trails[index];
        }

        public void SetSkins(RingSkin[] value) => skins = value;

        public void SetTrails(TrailStyle[] value) => trails = value;
    }
}
