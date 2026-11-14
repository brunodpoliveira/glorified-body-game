#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Subtitles;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>
    /// Lucy's art texts as audio logs (round 138, CANON; the writer's game/lines/T13_art_history.md, logs A1 to A5, PROPOSED
    /// ids). Each "### ... (log An)" part under "## The texts" becomes an audio log (item art_an): its lines are the text's
    /// sentences under LUCY, timed at reading pace (no voice yet, round 40). Place puts an ArtLog on an art object of a room.
    /// The beat ("### The beat") plays after the first one she hears to its end (ArtLog).
    /// </summary>
    public static class DemoArt
    {
        public const string File = "lines/T13_art_history.md";
        public const string Gear = "A5"; // the gear log has a beat of its own (T13 note 6)
        private static readonly Regex HeadingRx = new Regex(@"^###\s+(.*?)\s*\(log\s+(A\d+)\)\s*$");

        /// <summary>The texts by log id ("A1" .. "A5"): the heading and the words, and the beat.</summary>
        public static Dictionary<string, (string heading, string text)> Texts(out string beat)
        {
            var texts = new Dictionary<string, (string, string)>();
            beat = "";
            string path = Path.Combine(DemoScripts.WriterGame, File);
            if (!System.IO.File.Exists(path)) { Debug.LogWarning("[DemoArt] no file " + path); return texts; }
            string id = null, heading = "";
            bool inBeat = false;
            var words = new System.Text.StringBuilder();
            void Close()
            {
                if (id != null) texts[id] = (heading, words.ToString().Trim());
                id = null;
                words.Clear();
            }
            foreach (string raw in System.IO.File.ReadAllLines(path))
            {
                if (raw.StartsWith("## ")) { Close(); inBeat = false; continue; }
                if (raw.StartsWith("### "))
                {
                    Close();
                    Match m = HeadingRx.Match(raw);
                    inBeat = raw.Substring(4).StartsWith("The beat");
                    if (m.Success) { id = m.Groups[2].Value; heading = m.Groups[1].Value; }
                    continue;
                }
                if (inBeat && raw.Trim().Length > 0) beat = (beat + " " + raw.Trim()).Trim();
                else if (id != null) words.Append(raw.Trim()).Append(' ');
            }
            Close();
            return texts;
        }

        /// <summary>The audio log of an art text: item art_an, her lines one sentence each.</summary>
        public static ItemDefinition Log(string logId)
        {
            Dictionary<string, (string heading, string text)> texts = Texts(out _);
            if (!texts.TryGetValue(logId, out var t)) { Debug.LogWarning("[DemoArt] no log " + logId + " in " + File); return null; }
            string name = t.heading.Contains(":") ? t.heading.Substring(t.heading.LastIndexOf(':') + 1).Trim() : t.heading;
            name = name.Split('(')[0].Trim().ToUpperInvariant();
            ItemDefinition item = ItemAssets.Item("art_" + logId.ToLowerInvariant(), name, ItemKind.AudioLog, ItemAssets.TapeColour,
                "Her own voice, about the thing in front of her. She does not know how she knows it.", "", DrugType.Nalcon);
            item.audioClip = null; // no voice yet: the lines set the length
            item.tapeLines = new List<TapeLine>();
            float at = 0.4f;
            foreach (string sentence in Sentences(t.text))
            {
                // Round 145: the gear log's own beat is a line of its own, shown only when the gear is not her first art log.
                string own = logId == Gear ? GearBeat(sentence) : null;
                string said = own != null ? sentence.Substring(0, sentence.Length - own.Length).TrimEnd(' ', ',') : sentence;
                if (said.Length > 0)
                {
                    if (own != null) said += ".";
                    item.tapeLines.Add(new TapeLine { at = at, speaker = "LUCY", text = said });
                    at += SubtitleService.ReadSeconds(said) + 0.2f;
                }
                if (own == null) continue;
                item.tapeLines.Add(new TapeLine { at = at, speaker = "LUCY", text = own, skipIfFlag = ArtLog.GearFirstFlag });
                at += SubtitleService.ReadSeconds(own) + 0.2f;
            }
            EditorUtility.SetDirty(item);
            return item;
        }

        /// <summary>The gear's own beat at the end of a line ("I'm sure of the year and I don't know why I'm sure."), or null.</summary>
        public static string GearBeat(string line)
        {
            int beat = line.IndexOf("don't know why", System.StringComparison.Ordinal);
            if (beat < 0) return null;
            int cut = line.LastIndexOf(", I", beat, System.StringComparison.Ordinal);
            return cut < 0 ? line : line.Substring(cut + 2); // the split at the commas can leave it a line of its own
        }

        /// <summary>The sentences of a text; a long one splits at its commas into parts of 140 characters at most.</summary>
        public static List<string> Sentences(string text)
        {
            var parts = new List<string>();
            foreach (string s in Regex.Split(text, @"(?<=[.?!])\s+(?=[A-Z])"))
            {
                string sentence = s.Trim();
                if (sentence.Length == 0) continue;
                while (sentence.Length > 140)
                {
                    int cut = sentence.LastIndexOf(", ", 140);
                    if (cut < 40) break;
                    parts.Add(sentence.Substring(0, cut + 1));
                    sentence = sentence.Substring(cut + 2);
                }
                parts.Add(sentence);
            }
            return parts;
        }

        /// <summary>An ArtLog on an art object: a point in front of its face (facing: the way she looks at it from).</summary>
        public static ArtLog Place(DemoRoom r, string name, string logId, Vector3 at, string thing)
        {
            ItemDefinition item = Log(logId);
            if (item == null) return null;
            Texts(out string beat);
            GameObject host = MenuLoopSetup.EnsureChild(r.Props, name);
            host.transform.position = at;
            ArtLog art = MenuLoopSetup.EnsureComponent<ArtLog>(host);
            art.log = item;
            art.thing = thing;
            art.beatLine = beat;
            art.ownBeat = logId == Gear;
            art.range = 1.8f;
            EditorUtility.SetDirty(art);
            return art;
        }
    }
}
#endif
