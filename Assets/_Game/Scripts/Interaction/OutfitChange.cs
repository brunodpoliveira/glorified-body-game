using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Audio;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// The outfit change in play (2026-10-31, the design sweep's rank 7; design §4.4: "Security guard: more HP and
    /// inventory. No passive recharge"; CONCEPTS: the uniform covers the vents). At a locker (OutfitLocker), or by a
    /// script (the Crown grab of U1 tears the uniform off: StepKind.Outfit). She keeps the damage she took
    /// (PsychicStats.SetOutfit). When the new outfit has fewer slots, the grabbables over the limit fall at her feet
    /// and she picks what to keep (Bruno, round 21, for the Crown grab; the locker does the same, PROPOSED): the
    /// carried objects go first, the last taken first, then the pills. Key items never fall.
    /// </summary>
    public static class OutfitChange
    {
        public const string NudeId = "nude", GuardId = "guard";

        public static OutfitProfile Load(string outfitId) => Resources.Load<OutfitProfile>("Outfits/outfit_" + outfitId);

        /// <summary>Puts the outfit on her. Returns the pickups that fell (empty when all fit). Null when nothing changed.</summary>
        public static List<ItemPickup> Change(GameObject lucy, OutfitProfile to)
        {
            PsychicStats stats = lucy != null ? lucy.GetComponent<PsychicStats>() : null;
            if (stats == null || to == null || stats.Outfit == to) return null;
            string from = stats.Outfit != null ? stats.Outfit.outfitId : "";
            stats.SetOutfit(to, keepDamage: true);
            List<ItemPickup> fell = DropOverLimit(lucy);
            Sfx.Play(Sfx.Pickup, lucy.transform.position + Vector3.up);
            RunTelemetry.Event("outfit_change", $"{from}>{to.outfitId}:fell={fell.Count}");
            return fell;
        }

        /// <summary>The grabbables over the slots of her outfit fall in front of her as pickups.</summary>
        public static List<ItemPickup> DropOverLimit(GameObject lucy)
        {
            var fell = new List<ItemPickup>();
            ItemInventory items = lucy.GetComponent<ItemInventory>();
            DrugInventory drugs = lucy.GetComponent<DrugInventory>();
            if (items == null) return fell;
            int over = items.UsedSlots - items.Capacity;
            for (int i = items.Carried.Count - 1; i >= 0 && over > 0; i--, over--)
            {
                ItemDefinition item = items.Carried[i];
                items.Remove(item);
                fell.Add(Fall(lucy, item, fell.Count));
            }
            if (drugs != null)
            {
                DrugType[] order = { DrugType.DFelon, DrugType.Red, DrugType.Nalcon }; // the rarest falls first, PROPOSED
                foreach (DrugType type in order)
                    while (over > 0 && drugs.Drop(type))
                    {
                        over--;
                        ItemDefinition pill = Resources.Load<ItemDefinition>("Items/item_" + type.ToString().ToLowerInvariant());
                        if (pill != null) fell.Add(Fall(lucy, pill, fell.Count));
                    }
            }
            return fell;
        }

        private static ItemPickup Fall(GameObject lucy, ItemDefinition item, int index)
        {
            RunTelemetry.Event("item_fell", item.itemId);
            return FloorItem.Drop(lucy, item, index);
        }
    }
}
