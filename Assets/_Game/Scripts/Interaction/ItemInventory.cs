using System;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// Lucy's inventory (Bruno, 2026-10-22). Mission-critical items (keys, logs, the injector) take no space.
    /// Grabbables take one slot each: the carried objects here and every drug in the DrugInventory. The slots
    /// come from the outfit: the nude body carries little, the security guard outfit more. A full inventory
    /// refuses a grabbable; it stays in the room.
    /// </summary>
    public sealed class ItemInventory : MonoBehaviour
    {
        private readonly List<ItemDefinition> critical = new List<ItemDefinition>();
        private readonly List<ItemDefinition> carried = new List<ItemDefinition>();
        private PsychicStats stats;
        private DrugInventory drugs;

        /// <summary>Keys, logs, the critical items: no slot.</summary>
        public IReadOnlyList<ItemDefinition> Critical => critical;
        /// <summary>The carried objects (the drugs are counted in the DrugInventory).</summary>
        public IReadOnlyList<ItemDefinition> Carried => carried;
        public int Capacity => stats != null && stats.Outfit != null ? stats.Outfit.itemSlots : 4;
        public int DrugsCarried => drugs != null ? drugs.Total : 0;
        public int UsedSlots => carried.Count + DrugsCarried;
        public int Free => Mathf.Max(0, Capacity - UsedSlots);
        public bool IsFull => Free <= 0;

        public event Action OnChanged;

        private void Awake()
        {
            stats = GetComponent<PsychicStats>();
            drugs = GetComponent<DrugInventory>();
            if (drugs != null) drugs.OnChanged += Changed;
        }

        private void OnDestroy()
        {
            if (drugs != null) drugs.OnChanged -= Changed;
        }

        private void Changed() => OnChanged?.Invoke();

        public bool Has(string itemId) => critical.Exists(i => i != null && i.itemId == itemId) || carried.Exists(i => i != null && i.itemId == itemId);

        /// <summary>True when the item fits now: a critical item always, a grabbable when a slot is free.</summary>
        public bool Fits(ItemDefinition item) => item != null && (item.IsCritical || !IsFull);

        /// <summary>Adds the item. A drug goes to the DrugInventory. False when a grabbable finds no free slot.</summary>
        public bool TryAdd(ItemDefinition item)
        {
            if (item == null) return false;
            if (!Fits(item))
            {
                RunTelemetry.Event("item_full", item.itemId);
                return false;
            }
            if (item.kind == ItemKind.Drug)
            {
                if (drugs == null || drugs.Add(item.drugType, 1) <= 0) return false;
            }
            else if (item.IsCritical)
            {
                critical.Add(item);
            }
            else
            {
                carried.Add(item);
            }
            RunTelemetry.Event("item_taken", item.itemId);
            OnChanged?.Invoke();
            return true;
        }

        public bool Remove(ItemDefinition item)
        {
            bool removed = critical.Remove(item) || carried.Remove(item);
            if (removed) OnChanged?.Invoke();
            return removed;
        }

        /// <summary>Tests and debug: empties the items (the drugs stay).</summary>
        public void Clear()
        {
            critical.Clear();
            carried.Clear();
            OnChanged?.Invoke();
        }
    }
}
