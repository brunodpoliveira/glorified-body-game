using System;
using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.UI
{
    /// <summary>
    /// The death screen content (design: a picture, a voice line, and a hint to beat that enemy), keyed by
    /// the killer id. A killer id names the class and the weapon ("soldier_rifle", "guard_pistol"): the lookup tries
    /// the whole id, then drops the last part ("soldier"), then takes the fallback. The writer's lines come from
    /// game/death_screens.csv (DeathScreenSetup). Until a picture and a voice clip exist, the screen shows the
    /// writer's picture note on a grey card and the voice line as a caption.
    /// </summary>
    [CreateAssetMenu(fileName = "DeathScreenContent", menuName = "GlorifiedBody/Death Screen Content")]
    public sealed class DeathScreenContent : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("The enemy or hazard that killed Lucy (\"soldier_rifle\", or a class: \"soldier\"). Empty = the fallback.")]
            public string killerId = "";
            public Sprite picture;
            [Tooltip("What the picture shows (the writer's note), on the card until the picture exists.")]
            [TextArea(1, 3)] public string pictureNote = "";
            [TextArea(1, 3)] public string hint = "PLACEHOLDER HINT";
            public AudioClip voiceLine;
            [Tooltip("The words of the voice line, shown as a caption.")]
            [TextArea(1, 2)] public string voiceText = "";
        }

        public Entry fallback = new Entry();
        public List<Entry> entries = new List<Entry>();

        /// <summary>The entry for a killer id: the whole id, then shorter ids ("power_armour_launchers", "power_armour", "power"), then the fallback.</summary>
        public Entry Find(string killerId)
        {
            for (string key = killerId; !string.IsNullOrEmpty(key); key = Shorter(key))
            {
                Entry e = Exact(key);
                if (e != null) return e;
            }
            return fallback;
        }

        /// <summary>The entry with exactly this id, or null.</summary>
        public Entry Exact(string killerId)
        {
            if (string.IsNullOrEmpty(killerId)) return null;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].killerId == killerId) return entries[i];
            }
            return null;
        }

        private static string Shorter(string key)
        {
            int cut = key.LastIndexOf('_');
            return cut > 0 ? key.Substring(0, cut) : "";
        }
    }
}
