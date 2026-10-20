using System;
using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// The death screen content (design: a picture, a voice line, and a hint to beat that enemy), keyed by
    /// the killer id. The writer supplies the words and the voice later; the fallback entry is a placeholder.
    /// </summary>
    [CreateAssetMenu(fileName = "DeathScreenContent", menuName = "GlorifiedBody/Death Screen Content")]
    public sealed class DeathScreenContent : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("The enemy or hazard that killed Lucy. Empty = the fallback.")]
            public string killerId = "";
            public Sprite picture;
            [TextArea(1, 3)] public string hint = "PLACEHOLDER HINT";
            public AudioClip voiceLine;
        }

        public Entry fallback = new Entry();
        public List<Entry> entries = new List<Entry>();

        /// <summary>The entry for a killer id, or the fallback.</summary>
        public Entry Find(string killerId)
        {
            if (!string.IsNullOrEmpty(killerId))
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i] != null && entries[i].killerId == killerId) return entries[i];
                }
            }

            return fallback;
        }
    }
}
