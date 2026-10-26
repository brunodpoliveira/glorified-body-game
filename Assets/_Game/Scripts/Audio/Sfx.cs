using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Audio
{
    /// <summary>
    /// The sound events: Resources/Audio/Sfx/&lt;key&gt;/ holds the clips of one event, and a play picks one of them
    /// (never the same twice in a row), with a small pitch change. A new sound is a new file in the folder
    /// (tools/audio_placeholders.py makes the placeholders: Kenney CC0 and our own). An event with no folder is
    /// silent and logs once.
    /// </summary>
    public static class Sfx
    {
        public const string Folder = "Audio/Sfx/";

        // The events of the game, one constant each (the folder names).
        public const string StepLucy = "step_lucy";
        public const string StepBoots = "step_boots";
        public const string Dodge = "dodge";
        public const string ChargeHum = "charge_hum";
        public const string Blast = "blast";
        public const string Fire = "fire";
        public const string Lift = "lift";
        public const string Throw = "throw";
        public const string Slam = "slam";
        public const string Catch = "catch";
        public const string Refuse = "refuse";
        public const string FleshHit = "flesh_hit";
        public const string LucyHurt = "lucy_hurt";
        public const string BodyFall = "body_fall";
        public const string GrenadeBounce = "grenade_bounce";
        public const string RocketLoop = "rocket_loop";
        public const string BurnLoop = "burn_loop";
        public const string GasHiss = "gas_hiss";
        public const string UiMove = "ui_move";
        public const string UiConfirm = "ui_confirm";
        public const string UiBack = "ui_back";
        public const string UiOpen = "ui_open";
        public const string UiClose = "ui_close";
        public const string Pickup = "pickup";
        public const string Drug = "drug";
        public const string DoorLocked = "door_locked";
        public const string DoorOpen = "door_open";
        public const string DoorClose = "door_close";
        public const string Scan = "scan";
        public const string Save = "save";
        public const string Checkpoint = "checkpoint";
        public const string LogOpen = "log_open";
        public const string LogClose = "log_close";
        public const string RoomTone = "room_tone";
        public const string PaChime = "pa_chime";
        public const string DeathSting = "death_sting";

        private static readonly Dictionary<string, AudioClip[]> cache = new Dictionary<string, AudioClip[]>();
        private static readonly Dictionary<string, int> lastPick = new Dictionary<string, int>();
        private static readonly HashSet<string> warned = new HashSet<string>();

        public static AudioClip[] Clips(string key)
        {
            if (string.IsNullOrEmpty(key)) return new AudioClip[0];
            if (!cache.TryGetValue(key, out AudioClip[] clips))
            {
                clips = Resources.LoadAll<AudioClip>(Folder + key).OrderBy(c => c.name).ToArray();
                cache[key] = clips;
            }
            return clips;
        }

        public static bool Has(string key) => Clips(key).Length > 0;

        /// <summary>One clip of the event, not the one of the last play when it has more.</summary>
        public static AudioClip Pick(string key)
        {
            AudioClip[] clips = Clips(key);
            if (clips.Length == 0)
            {
                if (warned.Add(key))
                {
                    Debug.LogWarning($"[Sfx] no sound for the event '{key}' (Resources/{Folder}{key}/).");
                    RunTelemetry.Event("sfx_missing", key);
                }
                return null;
            }
            lastPick.TryGetValue(key, out int last);
            int i = clips.Length == 1 ? 0 : Random.Range(0, clips.Length - 1);
            if (clips.Length > 1 && i >= last) i++;
            lastPick[key] = i;
            return clips[i];
        }

        /// <summary>The event at a point in the world (3D, on the SFX channel).</summary>
        public static AudioSource Play(string key, Vector3 at, float volume = 1f, float pitchJitter = 0.06f)
        {
            return Play(key, at, SoundChannel.Sfx, volume, true, pitchJitter);
        }

        public static AudioSource Play(string key, Vector3 at, SoundChannel channel, float volume, bool spatial, float pitchJitter)
        {
            AudioClip clip = Pick(key);
            if (clip == null) return null;
            float pitch = 1f + (pitchJitter > 0f ? Random.Range(-pitchJitter, pitchJitter) : 0f);
            return GameAudio.PlayClip(clip, at, channel, volume, spatial, pitch, key);
        }

        /// <summary>A menu or HUD sound: 2D, on the UI channel, it plays during the pause.</summary>
        public static AudioSource Ui(string key, float volume = 0.8f) => Play(key, Vector3.zero, SoundChannel.Ui, volume, false, 0f);

        /// <summary>A looping source of the event on the host (a fire, a rocket, a hiss). Stop it with Destroy or Stop.</summary>
        public static AudioSource Loop(string key, GameObject host, float volume = 1f, SoundChannel channel = SoundChannel.Sfx, bool spatial = true)
        {
            AudioClip clip = Pick(key);
            if (clip == null || host == null) return null;
            var source = host.AddComponent<AudioSource>();
            GameAudio.Bind(source, channel, volume, spatial);
            source.clip = clip;
            source.loop = true;
            source.Play();
            GameAudio.Count(key);
            return source;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            cache.Clear();
            lastPick.Clear();
            warned.Clear();
        }
    }
}
