using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// An item lying in the room. The Interact key on it asks "Take it?"; a yes puts it in the inventory and
    /// removes it from the room (a log opens for reading at once). All pickups register here, so the
    /// interactor finds the one she faces without a physics query.
    /// </summary>
    public sealed class ItemPickup : MonoBehaviour
    {
        public ItemDefinition item;
        [Min(0.3f)] public float range = 1.6f;

        private static readonly List<ItemPickup> all = new List<ItemPickup>();
        public static IReadOnlyList<ItemPickup> All => all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => all.Clear();

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
        }

        public string Prompt => item != null ? "the " + item.displayName.ToLowerInvariant() : "it";
    }
}
