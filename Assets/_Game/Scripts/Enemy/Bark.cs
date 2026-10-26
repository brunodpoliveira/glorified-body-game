using GlorifiedBody.Subtitles;
using UnityEngine;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// A short spoken line of a figure (a shock, a retreat, a plea). The words are the writer's (barks.csv). Since
    /// 2026-10-25 they show as a subtitle at the bottom of the screen with the speaker's name (Bruno: "move to the
    /// bottom of the screen"), no longer over the head. The figure keeps its last line for the seconds (Shown).
    /// </summary>
    public sealed class Bark : MonoBehaviour
    {
        private string line = "";
        private float hideAt;

        public string Shown => Time.time < hideAt ? line : "";
        public static int Count { get; private set; }

        /// <summary>Says the line as this object for the seconds (the subtitle stays as long as its words need).</summary>
        public static void Say(GameObject who, string line, float seconds = 1.6f)
        {
            if (who == null || string.IsNullOrEmpty(line)) return;
            Bark bark = who.GetComponent<Bark>() ?? who.AddComponent<Bark>();
            bark.line = line;
            bark.hideAt = Time.time + seconds;
            SubtitleService.Say(who, line, seconds);
            Count++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Count = 0;
    }
}
