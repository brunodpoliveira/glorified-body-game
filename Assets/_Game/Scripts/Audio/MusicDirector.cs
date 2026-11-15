using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Combat;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Audio
{
    public enum MusicState { Silent, Menu, Ambient, Tension, Combat, Score }

    /// <summary>
    /// The music (Bruno, 2026-10-26: "music for combat, tension, ambient for sectors/environments, and placeholders in
    /// case there's a gap"). One director for the whole game, so the music runs on through a door.
    /// - The state: Combat while an enemy aims at her, or for CombatHoldSeconds after a hit from her or on her.
    ///   Tension while an armed enemy in her room or a room next door hunts her (alerted, alive, not in retreat). Else Ambient. Menu on
    ///   the main menu, Silent on the death screen (the sting plays) and for a cutscene (Hold).
    /// - The track: Resources/Audio/Music/&lt;room key&gt;/&lt;state&gt;, the room key from a RoomMusic in the scene (a
    ///   sector name) or the scene name. Else the demo's piece, Resources/Audio/Music/_demo/&lt;state&gt; (the T43 kit,
    ///   tools/music_kit_import.sh). A gap takes Resources/Audio/Music/_placeholder/&lt;state&gt; and logs once.
    /// - A change of track crossfades over FadeSeconds. The pause and the inventory duck the music.
    /// - Every track starts on the DSP clock (PlayScheduled), so the director knows where its bar lines are.
    /// - The combat arc (Bruno, 2026-11-14, the T43 v4 kit): after ArcAfterBars of combat (or StartArc), the drum break
    ///   starts on the next bar line of the theme, the payoff right after it, then the theme again from its bar 1. No
    ///   fade between the sections (the files have 5 ms at their ends). A fight that ends in the arc crossfades out of it.
    /// - The room tone (a hum, vents) loops under it on the SFX channel: &lt;room key&gt;/room_tone, else the placeholder.
    /// </summary>
    public sealed class MusicDirector : MonoBehaviour
    {
        public const string Folder = "Audio/Music/";
        public const string Placeholder = "_placeholder";
        /// <summary>The main menu's track: Resources/Audio/Music/_menu/menu.</summary>
        public const string MenuKey = "_menu";
        public const float CombatHoldSeconds = 8f;
        public const float FadeSeconds = 2f;
        public const float DuckLevel = 0.4f;
        public const float VoiceDuckLevel = 0.6f;
        public const float RoomToneLevel = 0.22f;
        private const float HuntCheckSeconds = 0.5f;

        /// <summary>The demo's piece for every room with no track of its own (the T43 kit and the T41 states).</summary>
        public const string DemoKey = "_demo";
        /// <summary>One composition at 84 BPM, 4/4 (round 126). One bar = 20/7 s = 137,142.86 samples at 48 kHz.</summary>
        public const double Bpm = 84.0;
        public const int BeatsPerBar = 4;
        public const double BarSeconds = 60.0 * BeatsPerBar / Bpm;
        /// <summary>The arc starts after this much combat: one full theme, as in the v4 file Bruno picked. PROPOSED.</summary>
        public const int ArcAfterBars = 12;
        public const int BreakBars = 4;
        public const int PayoffBars = 14;
        public const string BreakName = "combat_break";
        public const string PayoffName = "combat_payoff";
        /// <summary>A scheduled start is at least this far ahead of the DSP clock (a few audio buffers).</summary>
        public const double ScheduleLead = 0.1;
        /// <summary>Two loops of one piece may differ by a rounding sample (the T43 theme is 1,645,715, the T41 states 1,645,714).</summary>
        public const int SamePieceSlack = 2;
        private const int PoolSize = 5;

        public static MusicDirector Instance { get; private set; }

        private readonly AudioSource[] pool = new AudioSource[PoolSize];
        private AudioSource current;
        private AudioSource previous;
        /// <summary>The DSP time of the current clip's sample 0 (its bar 1). Every bar line is anchor + n bars.</summary>
        private double anchor;
        private double combatSince;
        private bool arcNow;
        private AudioSource arcTheme, arcBreak, arcPayoff, arcReturn;
        private AudioSource roomTone;
        private float fade = 1f;
        private float lastThreat = -100f;
        private float nextHuntCheck;
        private bool hunting;
        private MusicState? hold;
        private float voiceDuckUntil;
        private AudioClip score;
        private readonly HashSet<string> warned = new HashSet<string>();

        public MusicState State { get; private set; } = MusicState.Silent;
        public AudioClip CurrentClip => current != null ? current.clip : null;
        public bool CurrentIsPlaceholder { get; private set; }
        public string RoomKey { get; private set; } = "";
        public float CurrentVolume => current != null ? current.volume : 0f;
        public bool Crossfading => fade < 1f;
        public int Changes { get; private set; }
        public AudioClip RoomToneClip => roomTone != null ? roomTone.clip : null;
        /// <summary>The music's level now, 0..1, before the slider: the pause, a voice line, a tape (tests).</summary>
        public float DuckFactor { get; private set; } = 1f;
        /// <summary>A fight with its own track (a boss): a tape waits as in Combat.</summary>
        public bool InFight => State == MusicState.Combat || (State == MusicState.Score && fightScore);
        private bool fightScore;

        private void Awake()
        {
            Instance = this;
            for (int i = 0; i < PoolSize; i++) pool[i] = MakeSource("Music" + (char)('A' + i));
            current = pool[0];
            roomTone = gameObject.AddComponent<AudioSource>();
            GameAudio.Configure(roomTone, SoundChannel.Sfx, RoomToneLevel, false);
            roomTone.loop = true;
            HitEventBus.OnHit += OnHit;
            GlorifiedBody.Rooms.RoomTracker.OnChanged += OnRoomChanged;
        }

        private void OnDestroy()
        {
            HitEventBus.OnHit -= OnHit;
            GlorifiedBody.Rooms.RoomTracker.OnChanged -= OnRoomChanged;
            if (Instance == this) Instance = null;
        }

        private AudioSource MakeSource(string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            var source = obj.AddComponent<AudioSource>();
            GameAudio.Configure(source, SoundChannel.Music, 0f, false);
            source.loop = true;
            return source;
        }

        /// <summary>Another system says: a threat now (the director also hears every hit of hers or on her).</summary>
        public static void Threat()
        {
            if (Instance != null) Instance.lastThreat = Time.time;
        }

        /// <summary>A cutscene or a scripted beat holds a state (Silent for a cutscene); null lets the room decide.</summary>
        public static void Hold(MusicState? state)
        {
            if (Instance == null) return;
            Instance.hold = state;
            if (state != MusicState.Score) { Instance.score = null; Instance.fightScore = false; }
        }

        /// <summary>A cutscene's own track (Score) until Hold(null); a null clip holds the silence. fight: a boss's track,
        /// which counts as a fight (a tape waits).</summary>
        public static void Score(AudioClip clip, bool fight = false)
        {
            if (Instance == null) return;
            Instance.score = clip;
            Instance.fightScore = clip != null && fight;
            Instance.hold = clip != null ? MusicState.Score : MusicState.Silent;
        }

        private void OnHit(HitInfo hit)
        {
            if (hit == null) return;
            bool hers = hit.Attacker != null && hit.Attacker.CompareTag("Player");
            bool onHer = hit.Victim != null && hit.Victim.CompareTag("Player");
            if (hers || onHer) lastThreat = Time.time;
        }

        /// <summary>A room change in the world scene: a new key starts its music; the same key changes nothing (a chase goes on).</summary>
        private void OnRoomChanged(GlorifiedBody.Rooms.Room from, GlorifiedBody.Rooms.Room to)
        {
            RoomMusic music = to != null ? to.GetComponentInChildren<RoomMusic>() : null;
            string key = music != null && !string.IsNullOrEmpty(music.Key) ? music.Key : RoomKey;
            if (key == RoomKey && from != null) return;
            EnterRoom(SceneManager.GetActiveScene());
        }

        /// <summary>A new room: its key, its room tone. The music goes on if the new room has the same track.</summary>
        public void EnterRoom(Scene scene)
        {
            // In the world scene (2026-11-02) every room has its RoomMusic: the one of her room counts.
            GlorifiedBody.Rooms.Room here = GlorifiedBody.Rooms.RoomTracker.Current;
            RoomMusic room = here != null ? here.GetComponentInChildren<RoomMusic>() : FindFirstObjectByType<RoomMusic>();
            RoomKey = room != null && !string.IsNullOrEmpty(room.Key) ? room.Key : scene.name;
            hunting = false;
            lastThreat = -100f;
            nextHuntCheck = 0f;
            bool gameplay = GameObject.FindGameObjectWithTag("Player") != null;
            AudioClip tone = gameplay ? Resolve(RoomKey, Sfx.RoomTone, out _) ?? Sfx.Pick(Sfx.RoomTone) : null;
            if (roomTone.clip != tone)
            {
                roomTone.Stop();
                roomTone.clip = tone;
                if (tone != null) roomTone.Play();
            }
            RunTelemetry.Event("music_room", RoomKey);
        }

        private void Update()
        {
            MusicState target = Decide();
            double dsp = AudioSettings.dspTime;
            if (target != State)
            {
                RunTelemetry.Event("music", $"{State}->{target}");
                if (target == MusicState.Combat) combatSince = dsp;
                State = target;
            }
            if (ArcActive)
            {
                if (target == MusicState.Combat) StepArc(dsp);
                else CancelArc(dsp);
            }
            if (!ArcActive)
            {
                bool placeholder = false;
                AudioClip clip = target == MusicState.Silent ? null : target == MusicState.Score ? score : Resolve(target == MusicState.Menu ? MenuKey : RoomKey, target.ToString().ToLowerInvariant(), out placeholder);
                if (clip != CurrentClip || (clip == null && current.isPlaying))
                {
                    StartTrack(clip, dsp);
                    CurrentIsPlaceholder = clip != null && placeholder;
                }
                else if (target == MusicState.Combat) TryArc(dsp);
            }
            fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / FadeSeconds);
            DuckFactor = Duck();
            float level = GameAudio.Volume(SoundChannel.Music) * DuckFactor;
            if (ArcActive)
            {
                // The sections join with no fade: each one is at full level, a scheduled one is silent until its bar line.
                arcTheme.volume = arcBreak.volume = arcPayoff.volume = arcReturn.volume = level;
            }
            else
            {
                current.volume = level * fade * (CurrentIsPlaceholder ? 0.7f : 1f);
                if (previous != null)
                {
                    previous.volume = level * (1f - fade) * 0.7f;
                    if (fade >= 1f) { Release(previous); previous = null; }
                }
            }
            roomTone.volume = RoomToneLevel * GameAudio.Volume(SoundChannel.Sfx);
        }

        private float Duck()
        {
            float voice = Time.unscaledTime < voiceDuckUntil ? VoiceDuckLevel : 1f;
            // A tape plays: the music drops to almost nothing (Bruno, 2026-10-31).
            if (TapePlayer.Instance != null && TapePlayer.Instance.IsPlaying) voice = Mathf.Min(voice, TapePlayer.MusicLevel);
            if (GlorifiedBody.Interaction.InWorldScreen.AnyDucking) voice = Mathf.Min(voice, TapePlayer.MusicLevel); // a screen speaks near her
            GameManager gm = GameManager.Instance;
            if (gm == null) return voice;
            bool stopped = gm.CurrentState == GameManager.GameState.Paused || gm.CurrentState == GameManager.GameState.Inventory
                           || gm.CurrentState == GameManager.GameState.Reading;
            return Mathf.Min(voice, stopped ? DuckLevel : 1f);
        }

        /// <summary>A spoken line lowers the music for its length.</summary>
        public static void DuckForVoice(float seconds)
        {
            if (Instance != null) Instance.voiceDuckUntil = Mathf.Max(Instance.voiceDuckUntil, Time.unscaledTime + seconds);
        }

        public bool VoiceDucking => Time.unscaledTime < voiceDuckUntil;

        private MusicState Decide()
        {
            if (hold.HasValue) return hold.Value;
            GameManager gm = GameManager.Instance;
            if (gm == null) return State;
            switch (gm.CurrentState)
            {
                case GameManager.GameState.MainMenu: return MusicState.Menu;
                case GameManager.GameState.Loading: return State;
                case GameManager.GameState.GameOver: return MusicState.Silent;
                case GameManager.GameState.Cutscene: return MusicState.Silent;
            }
            if (GameObject.FindGameObjectWithTag("Player") == null) return MusicState.Silent;
            if (ShooterScheduler.ActiveCount > 0 || Time.time - lastThreat < CombatHoldSeconds) return MusicState.Combat;
            if (Time.unscaledTime >= nextHuntCheck)
            {
                nextHuntCheck = Time.unscaledTime + HuntCheckSeconds;
                hunting = false;
                GlorifiedBody.Rooms.Room here = GlorifiedBody.Rooms.RoomTracker.Current;
                foreach (SoldierBrain brain in FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None))
                {
                    if (!brain.Alerted) continue;
                    if (!InSearchScope(brain.transform.position, here)) continue;
                    SoldierBrain.Phase p = brain.Current;
                    if (p == SoldierBrain.Phase.Dead || p == SoldierBrain.Phase.Retreat || p == SoldierBrain.Phase.Cower) continue;
                    hunting = true;
                    break;
                }
            }
            return hunting ? MusicState.Tension : MusicState.Ambient;
        }

        /// <summary>
        /// The scope of the search (Bruno, 2026-11-13: "how about it being on the room and the attached room? like if lucy is
        /// a room right next to where they're searching the song changes and the player has time to strategize"): a hunter
        /// counts in her room and in every room that shares a doorway with it (Room.neighbours, from the exits: a locked or
        /// jammed door still joins two rooms). A hunter two doors away does not. A hunter in no room (a doorway line)
        /// counts. A scene with no rooms (the test rooms): every hunter counts. Combat does not change: an aim or a hit.
        /// </summary>
        public static bool InSearchScope(Vector3 hunter, GlorifiedBody.Rooms.Room here)
        {
            if (here == null) return true;
            GlorifiedBody.Rooms.Room at = GlorifiedBody.Rooms.Room.At(hunter + Vector3.up * 0.1f);
            return at == null || at == here || here.IsNeighbour(at.id);
        }

        /// <summary>The clip of the room for the name, else the placeholder (logged once).</summary>
        private AudioClip Resolve(string roomKey, string name, out bool placeholder)
        {
            placeholder = false;
            AudioClip clip = string.IsNullOrEmpty(roomKey) ? null : Resources.Load<AudioClip>(Folder + roomKey + "/" + name);
            if (clip != null) return clip;
            if (name == Sfx.RoomTone) return null;
            clip = Resources.Load<AudioClip>(Folder + DemoKey + "/" + name);
            if (clip != null) return clip;
            placeholder = true;
            if (warned.Add(roomKey + "/" + name))
                RunTelemetry.Event("music_placeholder", $"{roomKey}/{name}");
            return Resources.Load<AudioClip>(Folder + Placeholder + "/" + name);
        }

        /// <summary>
        /// One piece with three states (rounds 119 and 126, CANON: exploration, search and combat are cut from the same
        /// audio, so they lock by construction and the game can switch on any beat). Ambient is exploration, Tension is
        /// search (a squad hunts her, no fight yet), Combat is combat. When the old and the new clip are two states of one
        /// piece (the same length in samples), the new one starts at the old one's sample: the crossfade keeps the beat.
        /// </summary>
        public static bool SamePiece(AudioClip from, AudioClip to) =>
            from != null && to != null && from != to && Math.Abs(from.samples - to.samples) <= SamePieceSlack && from.frequency == to.frequency;

        /// <summary>The sample the music plays now (a test of the lock reads it).</summary>
        public int CurrentSample => current != null && current.clip != null ? current.timeSamples : 0;
        public int Locks { get; private set; }

        private void StartTrack(AudioClip clip, double dsp)
        {
            if (previous != null) Release(previous);
            AudioSource next = Free();
            next.Stop();
            next.clip = clip;
            next.volume = 0f;
            bool locked = SamePiece(current.clip, clip) && current.isPlaying && State != MusicState.Score && State != MusicState.Menu;
            if (clip != null)
            {
                double at = dsp + ScheduleLead;
                int start = 0;
                if (locked)
                {
                    // The new state starts at the old one's sample at the same DSP time: the beat goes on.
                    start = SampleAt(anchor, at, current.clip.samples, current.clip.frequency) % clip.samples;
                    Locks++;
                }
                Schedule(next, clip, true, start, at);
                anchor = at - (double)start / clip.frequency;
            }
            previous = current;
            current = next;
            fade = 0f;
            Changes++;
        }

        // ── The combat arc (T43 v4: theme, drum break, payoff, theme) ─────────────────────────

        /// <summary>A combat event asks for the arc now: the break starts on the next bar line (a scripted beat, a test).</summary>
        public static void StartArc()
        {
            if (Instance != null) Instance.arcNow = true;
        }

        public bool ArcActive => arcReturn != null;
        /// <summary>The DSP times of the last arc: the break, the payoff, the theme again. -1 before the first.</summary>
        public double ArcBreakAt { get; private set; } = -1;
        public double ArcPayoffAt { get; private set; } = -1;
        public double ArcReturnAt { get; private set; } = -1;
        /// <summary>The DSP time of the theme's bar 1 when the last arc was scheduled (its bar lines are this + n bars).</summary>
        public double ArcThemeAnchor { get; private set; } = -1;
        /// <summary>The theme's length in samples and rate when the last arc was scheduled.</summary>
        public int ArcThemeSamples { get; private set; }
        public int ArcThemeFrequency { get; private set; }
        /// <summary>The DSP time of the current clip's sample 0 (tests).</summary>
        public double Anchor => anchor;
        /// <summary>The DSP time at which the combat state (or the theme after an arc) began: the arc waits ArcAfterBars from it.</summary>
        public double CombatSince => combatSince;
        public int Arcs { get; private set; }

        private void TryArc(double dsp)
        {
            if (fade < 1f || previous != null) return;
            if (!arcNow && dsp - combatSince < ArcAfterBars * BarSeconds) return;
            AudioClip theme = current.clip;
            if (theme == null || !current.isPlaying) return;
            AudioClip brk = ResolveKit(BreakName);
            AudioClip pay = ResolveKit(PayoffName);
            if (brk == null || pay == null) { arcNow = false; return; }
            arcNow = false;
            double at = NextBarLine(anchor, dsp + ScheduleLead, theme.samples, theme.frequency);
            ArcThemeAnchor = anchor;
            ArcThemeSamples = theme.samples;
            ArcThemeFrequency = theme.frequency;
            ArcBreakAt = at;
            ArcPayoffAt = at + BreakBars * BarSeconds;
            ArcReturnAt = ArcPayoffAt + PayoffBars * BarSeconds;
            arcTheme = current;
            arcTheme.SetScheduledEndTime(at);
            arcBreak = Free();
            Schedule(arcBreak, brk, false, 0, ArcBreakAt);
            arcPayoff = Free();
            Schedule(arcPayoff, pay, false, 0, ArcPayoffAt);
            arcReturn = Free();
            Schedule(arcReturn, theme, true, 0, ArcReturnAt);
            Arcs++;
            RunTelemetry.Event("music_arc", $"{RoomKey} break at bar {Math.Round((at - anchor) / BarSeconds)}");
        }

        /// <summary>In the arc: the source that sounds now is the current one. After the payoff the theme is the track again.</summary>
        private void StepArc(double dsp)
        {
            if (dsp >= ArcReturnAt)
            {
                Release(arcTheme);
                Release(arcBreak);
                Release(arcPayoff);
                current = arcReturn;
                anchor = ArcReturnAt;
                combatSince = ArcReturnAt;
                ClearArc();
                return;
            }
            current = Sounding(dsp);
        }

        /// <summary>The fight ends in the arc: the sections not started yet stop, the one that sounds fades out as usual.</summary>
        private void CancelArc(double dsp)
        {
            AudioSource sounding = Sounding(dsp);
            foreach (AudioSource s in new[] { arcTheme, arcBreak, arcPayoff, arcReturn })
                if (s != sounding) Release(s);
            if (sounding == arcTheme) arcTheme.SetScheduledEndTime(dsp + 3600.0); // the theme goes on: no stop at the bar line
            current = sounding;
            RunTelemetry.Event("music_arc_cancel", sounding.clip != null ? sounding.clip.name : "");
            ClearArc();
        }

        private AudioSource Sounding(double dsp) => dsp >= ArcPayoffAt ? arcPayoff : dsp >= ArcBreakAt ? arcBreak : arcTheme;

        private void ClearArc()
        {
            arcTheme = arcBreak = arcPayoff = arcReturn = null;
        }

        /// <summary>A section of the kit: the room's own, else the demo's. No placeholder: with no kit, no arc.</summary>
        private static AudioClip ResolveKit(string name)
        {
            string key = Instance != null ? Instance.RoomKey : "";
            AudioClip clip = string.IsNullOrEmpty(key) ? null : Resources.Load<AudioClip>(Folder + key + "/" + name);
            return clip != null ? clip : Resources.Load<AudioClip>(Folder + DemoKey + "/" + name);
        }

        private AudioSource Free()
        {
            foreach (AudioSource s in pool)
                if (s != current && s != previous && s != arcTheme && s != arcBreak && s != arcPayoff && s != arcReturn) return s;
            return pool[0] == current ? pool[1] : pool[0]; // not reached: the pool holds every source the arc needs
        }

        /// <summary>A source the director is done with: stopped, and no clip (a stale clip looked like the track).</summary>
        private static void Release(AudioSource source)
        {
            source.Stop();
            source.clip = null;
        }

        private static void Schedule(AudioSource source, AudioClip clip, bool loop, int startSample, double at)
        {
            source.Stop();
            source.clip = clip;
            source.loop = loop;
            source.timeSamples = startSample;
            source.PlayScheduled(at);
        }

        /// <summary>The sample a loop plays at DSP time t, when its sample 0 played at anchor.</summary>
        public static int SampleAt(double anchor, double t, int loopSamples, int frequency)
        {
            long pos = (long)Math.Round((t - anchor) * frequency);
            long m = pos % loopSamples;
            return (int)(m < 0 ? m + loopSamples : m);
        }

        /// <summary>
        /// The first bar line at or after the DSP time earliest, for a loop whose sample 0 played at anchor. The bar lines
        /// are at n bars into each pass of the loop; the end of a pass (the start of the next) is a bar line too. Bar math
        /// in DSP seconds, so a scheduled section starts on the sample of the grid.
        /// </summary>
        public static double NextBarLine(double anchor, double earliest, int loopSamples, int frequency)
        {
            double loopSec = (double)loopSamples / frequency;
            int barsInLoop = Math.Max(1, (int)Math.Round(loopSec / BarSeconds));
            double e = earliest - anchor;
            double pass = Math.Floor(e / loopSec);
            double inLoop = e - pass * loopSec;
            int k = (int)Math.Ceiling(inLoop / BarSeconds - 1e-9);
            double t = k >= barsInLoop ? loopSec : k * BarSeconds;
            return anchor + pass * loopSec + t;
        }
    }
}
