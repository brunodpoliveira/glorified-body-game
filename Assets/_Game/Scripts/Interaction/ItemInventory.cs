using System;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// Lucy's items (the drugs keep their own DrugInventory). The slots come from the outfit: the nude body
    /// carries little, the security guard outfit carries more (design §4.4, Bruno 2026-10-22). One item takes one
    /// slot. A full inventory refuses a pickup.
    /// </summary>
    public sealed class ItemInventory : MonoBehaviour
    {
        private readonly List<ItemDefinition> items = new List<ItemDefinition>();
        private PsychicStats stats;

        public IReadOnlyList<ItemDefinition> Items => items;
        public int Capacity => stats != null && stats.Outfit != null ? stats.Outfit.itemSlots : 4;
        public int Free => Mathf.Max(0, Capacity - items.Count);
        public bool IsFull => Free <= 0;

        public event Action OnChanged;

        private void Awake()
        {
            stats = GetComponent<PsychicStats>();
        }

        public bool Has(string itemId) => items.Exists(i => i != null && i.itemId == itemId);

        /// <summary>Adds the item when a slot is free. False when full.</summary>
        public bool TryAdd(ItemDefinition item)
        {
            if (item == null) return false;
            if (IsFull)
            {
                RunTelemetry.Event("item_full", item.itemId);
                return false;
            }
            items.Add(item);
            RunTelemetry.Event("item_taken", item.itemId);
            OnChanged?.Invoke();
            return true;
        }

        public bool Remove(ItemDefinition item)
        {
            bool removed = items.Remove(item);
            if (removed) OnChanged?.Invoke();
            return removed;
        }

        /// <summary>Tests and debug: empties the inventory.</summary>
        public void Clear()
        {
            items.Clear();
            OnChanged?.Invoke();
        }
    }
}
