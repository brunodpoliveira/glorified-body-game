using System;
using UnityEngine;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// The drugs she carries. Each does one thing (Bruno, 2026-10-30, PsychicPowers.UseDrug): the MK capsule
    /// (Nalcon) fills MK, the SHORT capsule (Red) lowers the SHORT bar and ends a short, the second wind pill
    /// (DFelon) gives the second wind back. No drug gives HP. The code names stay (the saves use them).
    /// Each drug takes one slot of the item inventory (Bruno, 2026-10-22: the grabbables take space, the slots
    /// come from the outfit); without an item inventory the outfit's per-type cap applies.
    /// </summary>
    public sealed class DrugInventory : MonoBehaviour
    {
        private readonly int[] counts = new int[3];
        private PsychicStats stats;
        private GlorifiedBody.Interaction.ItemInventory items;

        public event Action OnChanged;

        private void Awake()
        {
            stats = GetComponent<PsychicStats>();
            items = GetComponent<GlorifiedBody.Interaction.ItemInventory>();
        }

        public int Count(DrugType type) => counts[(int)type];

        public int Total => counts[0] + counts[1] + counts[2];

        /// <summary>The most of one type she could hold now: the free slots plus the ones held, or the outfit's per-type cap without an item inventory.</summary>
        public int Capacity => items != null ? items.Free + Total : stats != null && stats.Outfit != null ? stats.Outfit.drugSlotsPerType : 2;

        /// <summary>Adds up to the free slots. Returns the number taken.</summary>
        public int Add(DrugType type, int amount)
        {
            int before = counts[(int)type];
            int room = items != null ? items.Free : Mathf.Max(0, Capacity - before);
            counts[(int)type] = before + Mathf.Clamp(amount, 0, room);
            int taken = counts[(int)type] - before;
            if (taken > 0)
            {
                RunTelemetry.Event("drug_pickup", $"{type}:{counts[(int)type]}");
                OnChanged?.Invoke();
            }
            return taken;
        }

        public bool Has(DrugType type) => counts[(int)type] > 0;

        /// <summary>Uses one drug of the type. Returns false when she has none of it (each drug does its own thing: no stand-in).</summary>
        public bool TryUse(DrugType preferred, out DrugType used)
        {
            used = preferred;
            if (!Has(preferred)) return false;

            counts[(int)used]--;
            RunTelemetry.Event("drug_used", $"{used}:{counts[(int)used]}");
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Tests and bots.</summary>
        public void DebugSet(DrugType type, int count)
        {
            counts[(int)type] = Mathf.Max(0, count);
            OnChanged?.Invoke();
        }
    }
}
