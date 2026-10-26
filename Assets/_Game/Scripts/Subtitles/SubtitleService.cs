using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GlorifiedBody.Subtitles
{
    /// <summary>
    /// Every spoken line on screen (Bruno, 2026-10-25: "on by default; move to the bottom of the screen. subtitles
    /// should be yellow with black outlines, and subtitle must always identify speaker"; "english only for now").
    /// The barks, the voice lines, the death screen's line and the cutscene track all come here; SubtitleUI draws
    /// them. A line has a speaker and an end in game time, so a pause or a freeze holds it. A cutscene line has a
    /// key and stays until its track hides it. One line per owner: a new line of the same owner replaces the old
    /// one. At most MaxLines show, the newest at the bottom. A new scene clears them.
    /// </summary>
    public static class SubtitleService
    {
        public const int MaxLines = 3;
        public const string UnknownSpeaker = "VOICE";

        public sealed class Line
        {
            public string Owner;
            public string Speaker;
            public string Text;
            public float Until;
        }

        private static readonly List<Line> lines = new List<Line>();

        /// <summary>The lines on screen now, the oldest first.</summary>
        public static IReadOnlyList<Line> Lines => lines;
        /// <summary>All lines said since the start (tests).</summary>
        public static int Said { get; private set; }
        /// <summary>Goes up on every change, so the UI draws again only then.</summary>
        public static int Version { get; private set; }

        /// <summary>What the screen shows for a line: the speaker, always, then the words.</summary>
        public static string Format(Line line) => $"{line.Speaker}: {line.Text}";

        /// <summary>A line of this object (a bark, a voice line). Its speaker is Speaker.NameOf(who).</summary>
        public static void Say(GameObject who, string text, float seconds)
        {
            if (who == null) return;
            Say("object:" + who.GetInstanceID(), Speaker.NameOf(who), text, seconds);
        }

        /// <summary>A line for the seconds, or longer when the words need more time to read.</summary>
        public static void Say(string owner, string speaker, string text, float seconds)
        {
            if (string.IsNullOrEmpty(text)) return;
            Put(owner, speaker, text, Time.time + Mathf.Max(seconds, ReadSeconds(text)));
        }

        /// <summary>A line with no end (a cutscene clip): Hide(key) takes it away.</summary>
        public static void Show(string key, string speaker, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Put(key, speaker, text, float.PositiveInfinity);
        }

        public static void Hide(string key)
        {
            if (lines.RemoveAll(l => l.Owner == key) > 0) Version++;
        }

        public static void Clear()
        {
            if (lines.Count == 0) return;
            lines.Clear();
            Version++;
        }

        /// <summary>Takes away the lines whose time is over (the UI calls it every frame).</summary>
        public static void Tick()
        {
            float now = Time.time;
            if (lines.RemoveAll(l => l.Until <= now) > 0) Version++;
        }

        /// <summary>The time to read the words: 1 s plus 55 ms a letter, 7 s at most. PROPOSED.</summary>
        public static float ReadSeconds(string text) => Mathf.Min(7f, 1f + 0.055f * (text?.Length ?? 0));

        /// <summary>"COMPUTER: Subject contained." gives the speaker COMPUTER. With no upper-case name first, the fallback.</summary>
        public static (string speaker, string text) Split(string line, string fallbackSpeaker)
        {
            if (string.IsNullOrEmpty(line)) return (fallbackSpeaker, "");
            int colon = line.IndexOf(':');
            if (colon > 0 && colon <= 24)
            {
                string name = line.Substring(0, colon).Trim();
                if (name.Length > 0 && name == name.ToUpperInvariant()) return (name, line.Substring(colon + 1).Trim());
            }
            return (fallbackSpeaker, line.Trim());
        }

        private static void Put(string owner, string speaker, string text, float until)
        {
            lines.RemoveAll(l => l.Owner == owner);
            lines.Add(new Line { Owner = owner, Speaker = string.IsNullOrEmpty(speaker) ? UnknownSpeaker : speaker, Text = text, Until = until });
            while (lines.Count > MaxLines) lines.RemoveAt(0);
            Said++;
            Version++;
            SubtitleUI.Ensure();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            lines.Clear();
            Said = 0;
            Version++;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Clear();
        }
    }
}
