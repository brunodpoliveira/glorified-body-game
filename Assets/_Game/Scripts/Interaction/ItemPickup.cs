using UnityEngine;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// An item lying in the room (a log, a key, a pill). The Interact key on it asks "Take it?"; a yes puts it in
    /// the inventory and removes it from the room (a log opens for reading at once). A grabbable that finds no
    /// free slot stays. The pickup is drawn unlit so it stands out of the room light (Bruno, 2026-10-22, after
    /// Resident Evil 4).
    /// </summary>
    public sealed class ItemPickup : Interactable
    {
        public ItemDefinition item;

        public override bool Available => base.Available && item != null;

        public string Name => item != null ? "the " + item.displayName.ToLowerInvariant() : "it";

        public override string Hint => "take " + Name;

        public override InteractKind KindFor(Interactor who) => InteractKind.Ask;

        public override string TextFor(Interactor who) => $"Take {Name}?";

        public override string Confirm(Interactor who)
        {
            ItemInventory inventory = who != null ? who.Inventory : null;
            if (inventory == null || !inventory.TryAdd(item)) return "full:" + item.itemId;
            gameObject.SetActive(false);
            Destroy(gameObject);
            return "taken:" + item.itemId;
        }
    }
}
