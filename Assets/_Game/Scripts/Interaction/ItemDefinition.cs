using UnityEngine;
using GlorifiedBody.Player;

namespace GlorifiedBody.Interaction
{
    public enum ItemKind
    {
        /// <summary>A thing she carries that takes a slot: a prop to throw, a tool.</summary>
        Object = 0,
        /// <summary>A record she can read. Mission critical: no slot.</summary>
        Log = 1,
        /// <summary>A key, a card. Mission critical: no slot.</summary>
        Key = 2,
        /// <summary>A pill or a capsule: it goes to the drug count and takes a slot.</summary>
        Drug = 3,
        /// <summary>Another mission-critical item (the injector): no slot.</summary>
        Critical = 4
    }

    /// <summary>
    /// One item (Bruno, 2026-10-22): she picks it up after a confirmation. Mission-critical items (keys, logs,
    /// the injector) take no space; the grabbables (pills, objects) take one slot each, and the slots come from
    /// the outfit. A log carries its text, a drug its type. The words are placeholders until the writer gives them.
    /// </summary>
    [CreateAssetMenu(fileName = "item", menuName = "GlorifiedBody/Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string itemId = "item";
        public string displayName = "ITEM";
        public ItemKind kind = ItemKind.Object;
        public DrugType drugType = DrugType.Nalcon;
        [Tooltip("The colour of the item's cell in the inventory and of its unlit material in the room.")]
        public Color colour = Color.white;
        [TextArea(2, 4)] public string description = "";
        [TextArea(6, 20)] public string logText = "";

        /// <summary>Keys, logs and the critical items take no slot (Bruno, 2026-10-22).</summary>
        public bool IsCritical => kind == ItemKind.Log || kind == ItemKind.Key || kind == ItemKind.Critical;

        public const string ResourceFolder = "Items";
        private static System.Collections.Generic.Dictionary<string, ItemDefinition> byId;

        /// <summary>The item with the id, from Resources/Items (a loaded save names its items by id), or null.</summary>
        public static ItemDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (byId == null)
            {
                byId = new System.Collections.Generic.Dictionary<string, ItemDefinition>();
                foreach (ItemDefinition item in Resources.LoadAll<ItemDefinition>(ResourceFolder))
                    if (item != null && !byId.ContainsKey(item.itemId)) byId[item.itemId] = item;
            }
            return byId.TryGetValue(id, out ItemDefinition found) ? found : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => byId = null;
    }
}
