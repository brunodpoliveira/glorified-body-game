using UnityEngine;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A pickup made in play (2026-10-31, Bruno: "fix both"): an item she dropped, or one that fell at an outfit change.
    /// The room keeps it: a save, a load and a return put it back where it lay (SaveSystem, RoomState.floorItems).
    /// </summary>
    public sealed class FloorItem : MonoBehaviour
    {
        public string itemId = "";

        /// <summary>
        /// Puts the item on the floor in front of her as a pickup: in an arc 0.7 m out, 25 degrees apart by its index.
        /// </summary>
        public static ItemPickup Drop(GameObject lucy, ItemDefinition item, int index)
        {
            float side = (index % 2 == 0 ? 1f : -1f) * ((index + 1) / 2) * 25f;
            Vector3 dir = Quaternion.Euler(0f, side, 0f) * Vector3.ProjectOnPlane(lucy.transform.forward, Vector3.up).normalized;
            Vector3 at = lucy.transform.position + dir * 0.7f + Vector3.up * 0.06f;
            return Place(item, at, side * 3f);
        }

        /// <summary>The pickup at a place (a drop, or a load that puts it back).</summary>
        public static ItemPickup Place(ItemDefinition item, Vector3 at, float yaw)
        {
            ItemPickup pickup = ItemPickup.Spawn(item, "Floor_" + item.itemId, at, new Vector3(0.14f, 0.1f, 0.14f), Quaternion.Euler(0f, yaw, 0f));
            pickup.gameObject.AddComponent<FloorItem>().itemId = item.itemId;
            return pickup;
        }
    }
}
