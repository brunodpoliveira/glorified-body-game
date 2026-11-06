using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A switch only a dominated figure can use, for the puzzles of mk_edgar (Bruno, 2026-11-06: "either in puzzles"): it stands where
    /// she cannot walk. She sends the figure with the defend key (Dominated.SendToSwitch), and the figure raises the story flag. The
    /// flag is in the save, so the switch stays used.
    /// </summary>
    public sealed class SentSwitch : MonoBehaviour
    {
        public string flag = "";
        [Tooltip("Where the figure stands to use it.")]
        public Transform point;
        public Renderer lamp;
        private static readonly List<SentSwitch> all = new List<SentSwitch>();

        public static IReadOnlyList<SentSwitch> All => all;
        public Vector3 Point => point != null ? point.position : transform.position;
        public bool Used => !string.IsNullOrEmpty(flag) && StoryFlags.Has(flag);

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        public void Use(string by)
        {
            RunTelemetry.Event("switch_used", $"{name} by {by}");
            if (!string.IsNullOrEmpty(flag)) StoryFlags.Raise(flag);
            if (lamp != null) lamp.material.color = new Color(0.3f, 0.95f, 0.4f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => all.Clear();
    }
}
