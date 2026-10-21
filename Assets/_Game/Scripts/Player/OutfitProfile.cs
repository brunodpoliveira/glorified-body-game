using UnityEngine;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// The two outfits of the design (§4.4). Nude: a doll body, little inventory, the three bars recharge by
    /// themselves. Security guard: more HP and inventory, no passive recharge. Numbers are PROPOSED.
    /// </summary>
    [CreateAssetMenu(fileName = "outfit", menuName = "GlorifiedBody/Outfit Profile")]
    public sealed class OutfitProfile : ScriptableObject
    {
        public string outfitId = "nude";
        public string displayName = "NUDE";
        [Min(1f)] public float maxHp = 100f;
        [Min(0f)] public float hpRegenPerSecond = 1f;
        [Min(0f)] public float nalconRegenPerSecond = 2f; // Bruno, 2026-10-20: 6 was too quick
        [Min(0f)] public float shortDecayPerSecond = 1f;
        [Min(0)] public int drugSlotsPerType = 2;
    }
}
