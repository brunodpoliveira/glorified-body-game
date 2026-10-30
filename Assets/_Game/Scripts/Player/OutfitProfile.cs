using UnityEngine;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// The two outfits of the design (§4.4). Nude: a doll body, little inventory, MK recharges and SHORT drains by
    /// itself. Security guard: more HP and inventory, no passive recharge. No outfit recharges HP (Bruno, 2026-10-30:
    /// only feeding gives HP). Numbers are PROPOSED.
    /// </summary>
    [CreateAssetMenu(fileName = "outfit", menuName = "GlorifiedBody/Outfit Profile")]
    public sealed class OutfitProfile : ScriptableObject
    {
        public string outfitId = "nude";
        public string displayName = "NUDE";
        [Min(1f)] public float maxHp = 100f;
        [Min(0f)] public float nalconRegenPerSecond = 2f; // Bruno, 2026-10-20: 6 was too quick
        [Min(0f)] public float shortDecayPerSecond = 1f;
        [Min(0)] public int drugSlotsPerType = 2;
        /// <summary>The item slots of the inventory (keys, logs, objects). Bruno, 2026-10-22: the guard outfit has more. PROPOSED numbers.</summary>
        [Min(1)] public int itemSlots = 4;
    }
}
