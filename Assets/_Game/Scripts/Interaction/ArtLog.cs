using UnityEngine;
using GlorifiedBody.Audio;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// An art object with Lucy's art text (round 138, CANON: "the art texts are audio logs"; the writer's
    /// game/lines/T13_art_history.md, logs A1 to A5, PROPOSED ids). The Interact key on it, with no question: the log goes
    /// into her logs (an audio log takes no slot) and plays at once on the tape player, so a fight that cuts it off loses
    /// nothing: the tape key or the inventory plays it again. The first art log she plays to its end gives the beat (one
    /// line of hers). Round 145: the gear log (A5) keeps its own small beat, shown only when the gear is not her first
    /// art log (GearFirstFlag skips that line); the general beat plays after the first one in every case.
    /// </summary>
    public sealed class ArtLog : Interactable
    {
        /// <summary>Up after the first art log she played to its end (the beat came, or the gear took its place).</summary>
        public const string FirstHeardFlag = "art_log_first_heard";
        public const string IdPrefix = "art_";
        /// <summary>Up when she played an art log (the first one decides the gear's beat).</summary>
        public const string StartedFlag = "art_log_started";
        /// <summary>Up when the gear log was her first art log: its own beat line does not show (round 145).</summary>
        public const string GearFirstFlag = "art_gear_first";

        [Tooltip("The audio log (kind AudioLog) of this object.")]
        public ItemDefinition log;
        [Tooltip("The words of the hint: \"look at the wall\".")]
        public string thing = "it";
        [Tooltip("The beat after the first art log (T13, \"The beat\").")]
        [TextArea] public string beatLine = "";
        [Tooltip("This log has its own beat (the gear, A5): played first, its beat line does not show (round 145).")]
        public bool ownBeat;

        /// <summary>The times the key played the log here (the tests).</summary>
        public int Plays { get; private set; }
        /// <summary>The beats said (the tests).</summary>
        public static int Beats { get; private set; }

        private static ArtLog lastPlayed;

        public override string Hint => "look at " + thing;
        public override InteractKind KindFor(Interactor who) => InteractKind.Act;
        public override string TextFor(Interactor who) => "";
        public override bool Available => base.Available && log != null;

        public override string Confirm(Interactor who)
        {
            ItemInventory items = who != null ? who.GetComponent<ItemInventory>() : null;
            if (items != null && !items.Has(log.itemId)) items.TryAdd(log);
            if (!StoryFlags.Has(StartedFlag))
            {
                StoryFlags.Raise(StartedFlag);
                if (ownBeat) StoryFlags.Raise(GearFirstFlag);
            }
            lastPlayed = this;
            Plays++;
            TapePlayer.Instance?.Play(log);
            RunTelemetry.Event("art_log", log.itemId);
            return "art:" + log.itemId;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Beats = 0;
            lastPlayed = null;
            TapePlayer.OnEnded -= OnTapeEnded;
            TapePlayer.OnEnded += OnTapeEnded;
        }

        private static void OnTapeEnded(ItemDefinition tape)
        {
            if (tape == null || !tape.itemId.StartsWith(IdPrefix) || StoryFlags.Has(FirstHeardFlag)) return;
            StoryFlags.Raise(FirstHeardFlag);
            ArtLog source = lastPlayed != null && lastPlayed.log == tape ? lastPlayed : null;
            if (source == null || string.IsNullOrEmpty(source.beatLine)) return;
            Beats++;
            Voice.SayAs("LUCY", "art_beat", source.beatLine, VoiceMode.Radio, Subtitles.SubtitleService.ReadSeconds(source.beatLine));
            RunTelemetry.Event("art_beat", tape.itemId);
        }
    }
}
