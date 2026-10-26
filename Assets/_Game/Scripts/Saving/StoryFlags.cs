using System;
using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Saving
{
    /// <summary>
    /// The story's facts so far ("met_the_nurse", "ward_door_open"): a set of names the save keeps. A scripted sequence
    /// raises them (GlorifiedBody.Story), a FlagGate and a RoomExit read them. The main menu clears them (a new game).
    /// </summary>
    public static class StoryFlags
    {
        private static readonly HashSet<string> flags = new HashSet<string>();

        public static IEnumerable<string> All => flags;
        /// <summary>A flag went up or down (a FlagGate shows or hides its objects). Null after a whole Set.</summary>
        public static event Action<string> OnChanged;

        public static bool Has(string flag) => flags.Contains(flag);

        public static void Raise(string flag)
        {
            if (!string.IsNullOrEmpty(flag) && flags.Add(flag)) OnChanged?.Invoke(flag);
        }

        public static void Drop(string flag)
        {
            if (flags.Remove(flag)) OnChanged?.Invoke(flag);
        }

        public static void Set(IEnumerable<string> saved)
        {
            flags.Clear();
            if (saved != null) foreach (string flag in saved) if (!string.IsNullOrEmpty(flag)) flags.Add(flag);
            OnChanged?.Invoke(null);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            flags.Clear();
            OnChanged = null;
        }
    }
}
