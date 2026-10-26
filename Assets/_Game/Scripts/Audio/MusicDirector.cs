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
    ///   Tension while an armed enemy in the room hunts her (alerted, alive, not in retreat). Else Ambient. Menu on
    ///   the main menu, Silent on the death screen (the sting plays) and for a cutscene (Hold).
    /// - The track: Resources/Audio/Music/&lt;room key&gt;/&lt;state&gt;, the room key from a RoomMusic in the scene (a
    ///   sector name) or the scene name. A gap takes Resources/Audio/Music/_placeholder/&lt;state&gt; and logs once.
    /// - A change of track crossfades over FadeSeconds. The pause and the inventory duck the music.
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

        public static MusicDirector Instance { get; private set; }

        private AudioSource a;
        private AudioSource b;
        private AudioSource current;
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

        private void Awake()
        {
            Instance = this;
            a = MakeSource("MusicA");
            b = MakeSource("MusicB");
            current = a;
            roomTone = gameObject.AddComponent<AudioSource>();
            GameAudio.Configure(roomTone, SoundChannel.Sfx, RoomToneLevel, false);
            roomTone.loop = true;
            HitEventBus.OnHit += OnHit;
        }

        private void OnDestroy()
        {
            HitEventBus.OnHit -= OnHit;
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
            if (state != MusicState.Score) Instance.score = null;
        }

        /// <summary>A cutscene's own track (Score) until Hold(null); a null clip holds the silence.</summary>
        public static void Score(AudioClip clip)
        {
            if (Instance == null) return;
            Instance.score = clip;
            Instance.hold = clip != null ? MusicState.Score : MusicState.Silent;
        }

        private void OnHit(HitInfo hit)
        {
            if (hit == null) return;
            bool hers = hit.Attacker != null && hit.Attacker.CompareTag("Player");
            bool onHer = hit.Victim != null && hit.Victim.CompareTag("Player");
            if (hers || onHer) lastThreat = Time.time;
        }

        /// <summary>A new room: its key, its room tone. The music goes on if the new room has the same track.</summary>
        public void EnterRoom(Scene scene)
        {
            RoomMusic room = FindFirstObjectByType<RoomMusic>();
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
            if (target != State)
            {
                RunTelemetry.Event("music", $"{State}->{target}");
                State = target;
            }
            bool placeholder = false;
            AudioClip clip = target == MusicState.Silent ? null : target == MusicState.Score ? score : Resolve(target == MusicState.Menu ? MenuKey : RoomKey, target.ToString().ToLowerInvariant(), out placeholder);
            if (clip != CurrentClip || (clip == null && current.isPlaying))
            {
                StartTrack(clip);
                CurrentIsPlaceholder = clip != null && placeholder;
            }
            fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / FadeSeconds);
            float level = GameAudio.Volume(SoundChannel.Music) * Duck();
            AudioSource other = current == a ? b : a;
            current.volume = level * fade * (CurrentIsPlaceholder ? 0.7f : 1f);
            other.volume = level * (1f - fade) * 0.7f;
            if (fade >= 1f && other.isPlaying) other.Stop();
            roomTone.volume = RoomToneLevel * GameAudio.Volume(SoundChannel.Sfx);
        }

        private float Duck()
        {
            float voice = Time.unscaledTime < voiceDuckUntil ? VoiceDuckLevel : 1f;
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
                foreach (SoldierBrain brain in FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None))
                {
                    if (!brain.Alerted) continue;
                    SoldierBrain.Phase p = brain.Current;
                    if (p == SoldierBrain.Phase.Dead || p == SoldierBrain.Phase.Retreat || p == SoldierBrain.Phase.Cower) continue;
                    hunting = true;
                    break;
                }
            }
            return hunting ? MusicState.Tension : MusicState.Ambient;
        }

        /// <summary>The clip of the room for the name, else the placeholder (logged once).</summary>
        private AudioClip Resolve(string roomKey, string name, out bool placeholder)
        {
            placeholder = false;
            AudioClip clip = string.IsNullOrEmpty(roomKey) ? null : Resources.Load<AudioClip>(Folder + roomKey + "/" + name);
            if (clip != null) return clip;
            if (name == Sfx.RoomTone) return null;
            placeholder = true;
            if (warned.Add(roomKey + "/" + name))
                RunTelemetry.Event("music_placeholder", $"{roomKey}/{name}");
            return Resources.Load<AudioClip>(Folder + Placeholder + "/" + name);
        }

        private void StartTrack(AudioClip clip)
        {
            AudioSource next = current == a ? b : a;
            next.Stop();
            next.clip = clip;
            next.volume = 0f;
            if (clip != null) next.Play();
            current = next;
            fade = 0f;
            Changes++;
        }
    }

    /// <summary>The music key of a room: rooms of one sector share a key, and so a track (Resources/Audio/Music/&lt;key&gt;/).</summary>
    public sealed class RoomMusic : MonoBehaviour
    {
        [SerializeField] private string key = "";

        public string Key
        {
            get => key;
            set => key = value ?? "";
        }
    }
}
