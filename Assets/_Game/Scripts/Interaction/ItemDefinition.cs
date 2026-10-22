using UnityEngine;

namespace GlorifiedBody.Interaction
{
    public enum ItemKind
    {
        /// <summary>A thing she carries: a key card, a tool, a prop.</summary>
        Object = 0,
        /// <summary>A record she can read: a note, a report, a page of a log.</summary>
        Log = 1
    }

    /// <summary>
    /// One item of the inventory (Bruno, 2026-10-22: she picks objects up after a confirmation, the inventory
    /// has slots, the guard outfit has more of them). A log carries its text. The words are placeholders
    /// until the writer gives them.
    /// </summary>
    [CreateAssetMenu(fileName = "item", menuName = "GlorifiedBody/Item")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string itemId = "item";
        public string displayName = "ITEM";
        public ItemKind kind = ItemKind.Object;
        [TextArea(2, 4)] public string description = "";
        [TextArea(6, 20)] public string logText = "";
    }
}
