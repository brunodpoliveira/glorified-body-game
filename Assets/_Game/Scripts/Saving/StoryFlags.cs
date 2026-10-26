using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Saving
{
    /// <summary>
    /// The story's facts so far ("met_the_nurse", "ward_door_open"): a set of names the save keeps. Nothing sets one
    /// yet; the story rooms will. The main menu clears them (a new game).
    /// </summary>
    public static class StoryFlags
    {
        private static readonly HashSet<string> flags = new HashSet<string>();

        public static IEnumerable<string> All => flags;
        public static bool Has(string flag) => flags.Contains(flag);
        public static void Raise(string flag) { if (!string.IsNullOrEmpty(flag)) flags.Add(flag); }
        public static void Drop(string flag) => flags.Remove(flag);

        public static void Set(IEnumerable<string> saved)
        {
            flags.Clear();
            if (saved == null) return;
            foreach (string flag in saved) Raise(flag);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => flags.Clear();
    }
}
