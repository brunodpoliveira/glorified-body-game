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

        public override bool Available => base.Available && item != null && !Taken;

        /// <summary>She took it, or a load removed it: the saves count it as taken, even while it is still in the room.</summary>
        public bool Taken { get; private set; }

        public void MarkTaken() => Taken = true;

        public string Name => item != null ? "the " + ItemDefinition.Spoken(item.displayName) : "it";

        public override string Hint => "take " + Name;

        public override InteractKind KindFor(Interactor who) => InteractKind.Ask;

        public override string TextFor(Interactor who) => $"Take {Name}?";

        /// <summary>
        /// A pickup made in play (a scripted beat, the tutorial): a box with a trigger, the item's colour unlit, the
        /// pickup range of the room builders (ItemAssets.Pickup makes the same in the editor).
        /// </summary>
        public static ItemPickup Spawn(ItemDefinition item, string name, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.transform.localScale = scale;
            obj.GetComponent<Collider>().isTrigger = true;
            Renderer renderer = obj.GetComponent<Renderer>();
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null)
            {
                var material = new Material(unlit);
                material.SetColor("_BaseColor", item != null ? item.colour : Color.white);
                renderer.sharedMaterial = material;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ItemPickup pickup = obj.AddComponent<ItemPickup>();
            pickup.item = item;
            pickup.range = 1.6f;
            return pickup;
        }

        public override string Confirm(Interactor who)
        {
            ItemInventory inventory = who != null ? who.Inventory : null;
            if (inventory == null || !inventory.TryAdd(item)) return "full:" + item.itemId;
            Taken = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
            return "taken:" + item.itemId;
        }
    }
}
