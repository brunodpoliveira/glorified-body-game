using System;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Audio
{
    /// <summary>The events of a set piece: the power armour comes in, the fight ends.</summary>
    public enum SetPieceEvent { Armour, End }

    /// <summary>
    /// A set piece's own music, cut by event (Bruno, 2026-11-15: the Dining Hall, "Still B", "Yes, cut it"; round 104: the
    /// set piece has its own music). The files are in Resources/Audio/Music/_setpiece/&lt;room id&gt;/ (tools/setpiece_import.sh):
    /// entry (once), fight_loop, armour_entry (once), armour_loop, end (once). Every file starts on a bar line of its tempo.
    /// </summary>
    public sealed class SetPieceMusic
    {
        public const string Folder = "Audio/Music/_setpiece/";
        public const string Entry = "entry", FightLoop = "fight_loop", ArmourEntry = "armour_entry", ArmourLoop = "armour_loop", End = "end";

        public string Key { get; private set; }
        public double Bpm { get; private set; }
        public double BarSeconds => 240.0 / Bpm;
        public AudioClip EntryClip, FightLoopClip, ArmourEntryClip, ArmourLoopClip, EndClip;

        /// <summary>The kit of a room, or null when it has no entry or no fight loop.</summary>
        public static SetPieceMusic Load(string key, double bpm)
        {
            if (string.IsNullOrEmpty(key) || bpm <= 0) return null;
            var kit = new SetPieceMusic
            {
                Key = key,
                Bpm = bpm,
                EntryClip = Resources.Load<AudioClip>(Folder + key + "/" + Entry),
                FightLoopClip = Resources.Load<AudioClip>(Folder + key + "/" + FightLoop),
                ArmourEntryClip = Resources.Load<AudioClip>(Folder + key + "/" + ArmourEntry),
                ArmourLoopClip = Resources.Load<AudioClip>(Folder + key + "/" + ArmourLoop),
                EndClip = Resources.Load<AudioClip>(Folder + key + "/" + End),
            };
            return kit.EntryClip != null && kit.FightLoopClip != null ? kit : null;
        }

        /// <summary>The length of a clip in bars of this tempo (a whole number for a cut file).</summary>
        public int Bars(AudioClip clip) => clip == null ? 0 : (int)Math.Round(clip.samples / (BarSeconds * clip.frequency));
    }

    /// <summary>One section of a set piece as scheduled (tests read the order and the times).</summary>
    public readonly struct SetPieceStart
    {
        public readonly string Name;
        public readonly double At;
        public SetPieceStart(string name, double at) { Name = name; At = at; }
        public override string ToString() => $"{Name}@{At:0.000000}";
    }

    public sealed partial class MusicDirector
    {
        private sealed class Section
        {
            public AudioSource Source;
            public double Start;
            public string Name;
        }

        private SetPieceMusic setPiece;
        private SetPieceMusic spRequestKit;
        private bool spRequested, spRequestArmour, spAbort, spCueArmour, spCueEnd;
        private bool spArmour, spEnding;
        private double spDoneAt = -1;
        private AudioSource spBefore;
        private readonly List<Section> spSections = new List<Section>();
        private readonly List<SetPieceStart> spLog = new List<SetPieceStart>();

        /// <summary>
        /// A set piece starts (RoomScore: she is in the room and the squad sees her). Its entry starts on the next bar line
        /// of the music that plays (the combat track's grid), then its fight loop. It holds the music (Score, a fight) until
        /// its end section is over or Hold(null). armourAlready: the armour came before (a load): the entry goes to the
        /// armour loop.
        /// </summary>
        public static void SetPiece(SetPieceMusic kit, bool armourAlready = false)
        {
            if (Instance == null || kit == null) return;
            Instance.spRequestKit = kit;
            Instance.spRequestArmour = armourAlready;
            Instance.spRequested = true;
            Instance.score = null;
            Instance.fightScore = true;
            Instance.hold = MusicState.Score;
        }

        /// <summary>An event of the set piece: its section starts on the next bar line of the set piece. No set piece: nothing.</summary>
        public static void SetPieceCue(SetPieceEvent e)
        {
            if (Instance == null || (Instance.setPiece == null && !Instance.spRequested)) return;
            if (e == SetPieceEvent.End) Instance.spCueEnd = true;
            else Instance.spCueArmour = true;
        }

        public bool SetPieceActive => setPiece != null || spRequested;
        /// <summary>The sections of the last set piece in the order they were scheduled, with their DSP start times.</summary>
        public IReadOnlyList<SetPieceStart> SetPieceLog => spLog;
        /// <summary>The bar line where the end section of the last set piece stops (the music hands back there). -1 before.</summary>
        public double SetPieceDoneAt => spDoneAt;
        public double SetPieceBarSeconds => setPiece != null ? setPiece.BarSeconds : 0;
        public int SetPieces { get; private set; }

        private bool HeldBySetPiece(AudioSource s)
        {
            foreach (Section x in spSections) if (x.Source == s) return true;
            return false;
        }

        /// <summary>The set piece owns the music this frame (true), or it ended or stopped (false: the normal path runs).</summary>
        private bool StepSetPiece(double dsp)
        {
            if (spRequested) BeginSetPiece(dsp);
            if (setPiece == null) { spCueArmour = spCueEnd = false; return false; }
            if (spAbort || hold != MusicState.Score)
            {
                spAbort = false;
                CancelSetPiece(dsp);
                return false;
            }
            if (spCueEnd && !spEnding) CueEnd(dsp);
            else if (spCueArmour && !spArmour && !spEnding) CueArmour(dsp);
            spCueArmour = spCueEnd = false;
            Section now = SectionAt(dsp);
            current = now != null ? now.Source : spSections[0].Source;
            // A section before the one that plays, and silent now, gives its source back.
            for (int i = spSections.Count - 1; i >= 0; i--)
            {
                Section x = spSections[i];
                if (x != now && now != null && x.Start < now.Start && !x.Source.isPlaying)
                {
                    Release(x.Source);
                    spSections.RemoveAt(i);
                }
            }
            // A quarter second before the end section stops: the normal state is scheduled on that bar line.
            if (spEnding && dsp >= spDoneAt - 0.25)
            {
                FinishSetPiece();
                return false;
            }
            return true;
        }

        private void BeginSetPiece(double dsp)
        {
            spRequested = false;
            SetPieceMusic kit = spRequestKit;
            spRequestKit = null;
            if (setPiece != null) ReleaseSections();
            double at = NextMusicBarLine(dsp + ScheduleLead);
            spBefore = current;
            CutAll(at);
            ClearArc();
            previous = null;
            setPiece = kit;
            spLog.Clear();
            spArmour = spRequestArmour && kit.ArmourLoopClip != null;
            spEnding = false;
            spDoneAt = -1;
            spCueArmour = spCueEnd = false;
            AddSection(kit.EntryClip, false, at, SetPieceMusic.Entry);
            double next = at + kit.Bars(kit.EntryClip) * kit.BarSeconds;
            if (spArmour) AddSection(kit.ArmourLoopClip, true, next, SetPieceMusic.ArmourLoop);
            else AddSection(kit.FightLoopClip, true, next, SetPieceMusic.FightLoop);
            current = spSections[0].Source;
            fade = 1f;
            CurrentIsPlaceholder = false;
            SetPieces++;
            RunTelemetry.Event("music_setpiece", $"{kit.Key}:entry");
        }

        private void CueArmour(double dsp)
        {
            spArmour = true;
            if (setPiece.ArmourEntryClip == null && setPiece.ArmourLoopClip == null) return;
            double at = NextSetPieceBarLine(dsp + ScheduleLead);
            CutSections(at);
            double next = at;
            if (setPiece.ArmourEntryClip != null)
            {
                AddSection(setPiece.ArmourEntryClip, false, at, SetPieceMusic.ArmourEntry);
                next = at + setPiece.Bars(setPiece.ArmourEntryClip) * setPiece.BarSeconds;
            }
            AddSection(setPiece.ArmourLoopClip != null ? setPiece.ArmourLoopClip : setPiece.FightLoopClip, true, next, SetPieceMusic.ArmourLoop);
            RunTelemetry.Event("music_setpiece", $"{setPiece.Key}:armour");
        }

        private void CueEnd(double dsp)
        {
            spEnding = true;
            double at = NextSetPieceBarLine(dsp + ScheduleLead);
            CutSections(at);
            if (setPiece.EndClip != null) AddSection(setPiece.EndClip, false, at, SetPieceMusic.End);
            spDoneAt = at + setPiece.Bars(setPiece.EndClip) * setPiece.BarSeconds;
            RunTelemetry.Event("music_setpiece", $"{setPiece.Key}:end");
        }

        /// <summary>The end section is over: the normal states take the music on its last bar line (StartTrack, resumeAt).</summary>
        private void FinishSetPiece()
        {
            resumeAt = spDoneAt;
            Section last = spSections.Count > 0 ? spSections[spSections.Count - 1] : null;
            foreach (Section x in spSections) if (last == null || x.Source != last.Source) Release(x.Source);
            if (last != null) current = last.Source;
            spSections.Clear();
            setPiece = null;
            spBefore = null;
            hold = null;
            score = null;
            fightScore = false;
            RunTelemetry.Event("music_setpiece", "done");
        }

        /// <summary>She left the room, a cutscene, a death: the sections that wait are dropped, the one that plays fades out
        /// as any track. Before the entry sounds, the music before it plays on.</summary>
        private void CancelSetPiece(double dsp)
        {
            Section now = SectionAt(dsp);
            foreach (Section x in spSections) if (now == null || x.Source != now.Source) Release(x.Source);
            if (now != null)
            {
                Uncut(now.Source);
                current = now.Source;
            }
            else if (spBefore != null && spBefore.clip != null)
            {
                Uncut(spBefore);
                current = spBefore;
            }
            spSections.Clear();
            setPiece = null;
            spBefore = null;
            spCueArmour = spCueEnd = false;
            previous = null;
            fade = 1f;
            RunTelemetry.Event("music_setpiece", "stopped");
        }

        private void ReleaseSections()
        {
            foreach (Section x in spSections) Release(x.Source);
            spSections.Clear();
        }

        private void AddSection(AudioClip clip, bool loop, double at, string name)
        {
            if (clip == null) return;
            AudioSource source = Free();
            Schedule(source, clip, loop, 0, at);
            source.volume = GameAudio.Volume(SoundChannel.Music) * DuckFactor;
            spSections.Add(new Section { Source = source, Start = at, Name = name });
            spLog.Add(new SetPieceStart(name, at));
        }

        /// <summary>The sections that start on or after the bar line are dropped; the one that plays stops on it.</summary>
        private void CutSections(double at)
        {
            for (int i = spSections.Count - 1; i >= 0; i--)
            {
                Section x = spSections[i];
                if (x.Start >= at - 1e-6) { Release(x.Source); spSections.RemoveAt(i); }
                else Cut(x.Source, at);
            }
        }

        /// <summary>The section that plays at t: the last one scheduled to start at or before it.</summary>
        private Section SectionAt(double t)
        {
            Section best = null;
            foreach (Section x in spSections)
                if (x.Start <= t && (best == null || x.Start > best.Start)) best = x;
            return best;
        }

        /// <summary>The next bar line of the set piece's own tempo, from the section that plays then.</summary>
        private double NextSetPieceBarLine(double earliest)
        {
            double bar = setPiece.BarSeconds;
            Section s = SectionAt(earliest);
            if (s == null) s = spSections.Count > 0 ? spSections[0] : null;
            if (s == null) return earliest;
            return s.Source.loop
                ? NextBarLine(s.Start, earliest, s.Source.clip.samples, s.Source.clip.frequency, bar)
                : NextBarLineOnce(s.Start, earliest, bar);
        }

        private void SetPieceVolumes(float level)
        {
            foreach (Section x in spSections) x.Source.volume = level;
        }
    }
}
