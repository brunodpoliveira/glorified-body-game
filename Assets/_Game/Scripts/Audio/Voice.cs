using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Subtitles;

namespace GlorifiedBody.Audio
{
    /// <summary>World: from the speaker's head. Radio: in her ear (2D, a collar or a speaker). PA: the building's speakers, after a chime.</summary>
    public enum VoiceMode { World, Radio, PA }

    /// <summary>
    /// The spoken lines (design §1.4: "voice acting is essential"). A line has an id and words. Its clip is
    /// Resources/Voice/&lt;id&gt; (an .ogg or a .wav). With a clip it plays on the Voice channel and the subtitle stays as
    /// long as the clip. With no clip the subtitle shows alone, and the id goes to Missing and to the run log
    /// ("voice_missing"), so the voice work can list the lines still to record. A line ducks the music.
    /// A bark's id is its speaker and its first six words (IdFor): "nurse.come_on_breathe_for_me_im".
    /// </summary>
    public static class Voice
    {
        public const string Folder = "Voice/";
        public const string PaSpeaker = "PA";
        public const float PaDelay = 1.2f;

        private static readonly HashSet<string> missing = new HashSet<string>();

        public static int Said { get; private set; }
        public static string LastId { get; private set; } = "";
        public static bool LastHadClip { get; private set; }
        public static AudioSource LastSource { get; private set; }
        public static IReadOnlyCollection<string> Missing => missing;

        public static AudioClip Clip(string id) => string.IsNullOrEmpty(id) ? null : Resources.Load<AudioClip>(Folder + id);

        /// <summary>The id of a line by its speaker and its words.</summary>
        public static string IdFor(GameObject speaker, string text) => Slug(Speaker.NameOf(speaker), 4) + "." + Slug(text, 6);

        /// <summary>Says the line as the speaker. Returns the playing source, or null when the line has no clip.</summary>
        public static AudioSource Say(GameObject speaker, string id, string text, VoiceMode mode = VoiceMode.World, float minSeconds = 1.6f)
        {
            if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(id)) return null;
            if (string.IsNullOrEmpty(id)) id = IdFor(speaker, text);
            AudioClip clip = Clip(id);
            float seconds = clip != null ? Mathf.Max(minSeconds, clip.length + 0.25f) : minSeconds;
            if (speaker != null) SubtitleService.Say(speaker, text, seconds);
            else SubtitleService.Say("voice:" + id, mode == VoiceMode.PA ? PaSpeaker : SubtitleService.UnknownSpeaker, text, seconds);
            return Play(speaker, id, clip, mode, seconds);
        }

        /// <summary>The clip of a line whose words another panel shows (the tutorial prompt): no subtitle. Null when not recorded.</summary>
        public static AudioSource PlayOnly(string id, VoiceMode mode = VoiceMode.Radio, GameObject speaker = null)
        {
            AudioClip clip = Clip(id);
            return Play(speaker, id, clip, mode, clip != null ? clip.length : 0f);
        }

        /// <summary>The building's speakers: a chime, then the line in every room ("PA: ..." under it).</summary>
        public static void PA(string id, string text)
        {
            Sfx.Ui(Sfx.PaChime, 0.9f);
            AudioHost host = AudioHost.Instance;
            if (host == null) { SayPa(id, text); return; }
            host.StartCoroutine(PaAfterChime(id, text));
        }

        private static IEnumerator PaAfterChime(string id, string text)
        {
            yield return new WaitForSeconds(PaDelay);
            SayPa(id, text);
        }

        private static void SayPa(string id, string text)
        {
            AudioClip clip = Clip(id);
            float seconds = clip != null ? clip.length + 0.25f : 2f;
            SubtitleService.Say("voice:pa", PaSpeaker, text, seconds);
            Play(null, id, clip, VoiceMode.PA, seconds);
        }

        private static AudioSource Play(GameObject speaker, string id, AudioClip clip, VoiceMode mode, float seconds)
        {
            Said++;
            LastId = id;
            LastHadClip = clip != null;
            LastSource = null;
            if (clip == null)
            {
                if (missing.Add(id)) RunTelemetry.Event("voice_missing", id);
                return null;
            }
            bool world = mode == VoiceMode.World && speaker != null;
            Vector3 at = world ? speaker.transform.position + Vector3.up * 1.6f : Vector3.zero;
            LastSource = GameAudio.PlayClip(clip, at, SoundChannel.Voice, 1f, world, 1f, "voice:" + id);
            MusicDirector.DuckForVoice(seconds);
            RunTelemetry.Event("voice", id);
            return LastSource;
        }

        /// <summary>Lower case words joined by "_", at most the word count, letters and digits only.</summary>
        public static string Slug(string text, int words)
        {
            if (string.IsNullOrEmpty(text)) return "line";
            var sb = new StringBuilder();
            int count = 0;
            bool inWord = false;
            foreach (char ch in text.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(ch))
                {
                    if (!inWord)
                    {
                        if (count == words) break;
                        if (count > 0) sb.Append('_');
                        count++;
                        inWord = true;
                    }
                    sb.Append(ch);
                }
                else if (ch != '\'') inWord = false;
            }
            return sb.Length > 0 ? sb.ToString() : "line";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            missing.Clear();
            Said = 0;
            LastId = "";
            LastHadClip = false;
            LastSource = null;
        }
    }
}
