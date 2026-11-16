using System.Collections.Generic;
using GlorifiedBody.Audio;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Subtitles;
using UnityEngine;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// A short spoken line of a figure (a shock, a retreat, a plea). The words are the writer's (barks.csv). Since
    /// 2026-10-25 they show as a subtitle at the bottom of the screen with the speaker's name (Bruno: "move to the
    /// bottom of the screen"), no longer over the head. The figure keeps its last line for the seconds (Shown).
    ///
    /// Two voices at most (Bruno, 2026-11-15: "two people can bark at a time, no more, or else it'll become a cacophony of
    /// voices"; "two people of two different archetypes, to maximize information ... never two nurses, two soldiers etc
    /// at the same time"). A bark plays only when fewer than MaxVoices others speak and none of them is of its archetype
    /// (BarkVoice.Enemy: guard, soldier, armour, nurse, labworker, patient). Else it drops. A speaker who talks already may
    /// go on, but not over a forced line of his own. A line the player asked for, or a line of a story beat, is forced (SayForced): it always plays, and it
    /// cuts the oldest voice when the two places are taken. A line nobody hears (two rooms away) takes no place.
    /// </summary>
    public sealed class Bark : MonoBehaviour
    {
        public const int MaxVoices = 2;

        private string line = "";
        private float hideAt;

        public string Shown => Time.time < hideAt ? line : "";
        public static int Count { get; private set; }
        /// <summary>The lines dropped by the two-voice rule.</summary>
        public static int Dropped { get; private set; }
        /// <summary>The lines of this figure (a test counts one speaker: the whole count takes the others too).</summary>
        public int Said { get; private set; }

        public static int CountOf(GameObject who) => who != null && who.TryGetComponent(out Bark bark) ? bark.Said : 0;

        private struct Voicing
        {
            public GameObject who;
            public string archetype;
            public float until;
            public AudioSource source;
            public AudioClip clip;
            public bool forced;
        }

        private static readonly List<Voicing> speaking = new List<Voicing>();

        /// <summary>The figures that speak now (a test, the HUD of a bot).</summary>
        public static int Speaking
        {
            get { Prune(); return speaking.Count; }
        }

        /// <summary>The archetype of a speaker: its enemy class, or the thing itself when it is no person.</summary>
        public static string ArchetypeOf(GameObject who)
        {
            BarkVoice voice = BarkVoice.Of(who);
            return voice != null && !string.IsNullOrEmpty(voice.Enemy) ? voice.Enemy : "thing:" + who.GetInstanceID();
        }

        /// <summary>A gated bark could play now for this figure (BarkBook asks before it takes a line from the deck).</summary>
        public static bool CanSpeak(GameObject who)
        {
            if (who == null) return false;
            Prune();
            int own = Find(who);
            if (own >= 0) return !speaking[own].forced; // a story line keeps its speaker until it ends (the call for the armour)
            if (speaking.Count >= MaxVoices) return false;
            string archetype = ArchetypeOf(who);
            foreach (Voicing v in speaking)
                if (v.archetype == archetype) return false;
            return true;
        }

        /// <summary>Says the line as this object for the seconds, under the two-voice rule. False: it dropped.</summary>
        public static bool Say(GameObject who, string line, float seconds = 1.6f) => Speak(who, line, seconds, false);

        /// <summary>A line the player asked for, or a line of a story beat: it always plays, and it cuts the oldest voice when it needs the place.</summary>
        public static bool SayForced(GameObject who, string line, float seconds = 1.6f) => Speak(who, line, seconds, true);

        private static bool Speak(GameObject who, string line, float seconds, bool forced)
        {
            if (who == null || string.IsNullOrEmpty(line)) return false;
            bool heard = Voice.Heard(who);
            if (heard && !forced && !CanSpeak(who))
            {
                Dropped++;
                RunTelemetry.Event("bark_dropped", $"{who.name}:{ArchetypeOf(who)}");
                return false;
            }
            Bark bark = who.GetComponent<Bark>() ?? who.AddComponent<Bark>();
            bark.line = line;
            bark.hideAt = Time.time + seconds;
            bark.Said++;
            Count++;
            if (!heard) return true; // the old rule: nobody hears it, nothing shows, it takes no place
            int own = Find(who);
            if (own >= 0) Cut(own, false);
            if (forced)
            {
                // Make room: first a voice of the same archetype, then the oldest.
                string archetype = ArchetypeOf(who);
                int same = speaking.FindIndex(v => v.archetype == archetype);
                if (same >= 0) Cut(same, true);
                while (speaking.Count >= MaxVoices) Cut(0, true);
            }
            // The voice player shows the subtitle, and plays Resources/Voice/<id> when the line has a clip (2026-10-26).
            AudioSource source = Voice.Say(who, Voice.IdFor(who, line), line, VoiceMode.World, seconds);
            float clip = source != null && source.clip != null ? source.clip.length : 0f;
            speaking.Add(new Voicing
            {
                who = who,
                archetype = ArchetypeOf(who),
                until = Time.time + Mathf.Max(seconds, SubtitleService.ReadSeconds(line), clip),
                source = source,
                clip = source != null ? source.clip : null,
                forced = forced,
            });
            return true;
        }

        /// <summary>
        /// Stops this figure's line at once, its clip and its subtitle (Bruno, 2026-11-16: "when the enemy gets staggered/stunned
        /// their dialogue line gets cut out immediately"). Any line of the figure: a bark or a scripted line.
        /// </summary>
        public static void Silence(GameObject who)
        {
            if (who == null) return;
            int own = Find(who);
            if (own >= 0) Cut(own, true);
            if (who.TryGetComponent(out Bark bark)) bark.hideAt = 0f;
            if (Voice.Stop(who)) RunTelemetry.Event("bark_cut", who.name);
        }

        private static int Find(GameObject who) => speaking.FindIndex(v => v.who == who);

        /// <summary>Takes a voice off: its clip stops and, when cut by another, its subtitle goes.</summary>
        private static void Cut(int index, bool hide)
        {
            Voicing v = speaking[index];
            speaking.RemoveAt(index);
            if (v.source != null && v.source.isPlaying && v.source.clip == v.clip) v.source.Stop(); // a pooled source may play another line now
            if (hide && v.who != null) SubtitleService.Hide("object:" + v.who.GetInstanceID());
        }

        private static void Prune()
        {
            float now = Time.time;
            speaking.RemoveAll(v => v.who == null || now >= v.until);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Count = 0;
            Dropped = 0;
            speaking.Clear();
        }

        /// <summary>A test starts with no voice.</summary>
        public static void ResetVoices() => speaking.Clear();
    }
}
