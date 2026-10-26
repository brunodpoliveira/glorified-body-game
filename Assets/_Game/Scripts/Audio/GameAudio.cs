using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Audio
{
    /// <summary>The four volume channels of the Options menu (Master is AudioListener.volume over all of them).</summary>
    public enum SoundChannel { Sfx, Music, Voice, Ui }

    /// <summary>
    /// The sound system (2026-10-26, after the audio sweep: no mixer, the SFX slider reached only the HUD beeps, the
    /// music slider nothing). Every sound of the game plays on a channel, and the channel's slider sets its volume:
    /// a one-shot takes it when it starts, a long source (a gun, a loop) follows the slider (ChannelSource).
    /// The 3D sounds share one curve: full within 1.5 m, logarithmic to 40 m. The listener is on Lucy (ListenerRig).
    /// The pause, the inventory and the death screen pause the world sounds. The music and the UI go on.
    /// </summary>
    public static class GameAudio
    {
        public const float MinDistance = 1.5f;
        public const float MaxDistance = 40f;

        public static int Played { get; private set; }
        public static AudioClip LastClip { get; private set; }
        public static SoundChannel LastChannel { get; private set; }
        public static AudioSource LastSource { get; private set; }
        private static readonly Dictionary<string, int> playedByKey = new Dictionary<string, int>();

        /// <summary>The slider of the channel, 0..1 (Master is apart, on the listener).</summary>
        public static float Volume(SoundChannel channel)
        {
            SettingsManager settings = SettingsManager.Instance;
            if (settings == null) return 1f;
            switch (channel)
            {
                case SoundChannel.Music: return settings.MusicVolume;
                case SoundChannel.Voice: return settings.VoiceVolume;
                default: return settings.SFXVolume;
            }
        }

        /// <summary>How many times an event (Sfx key, voice id, "clip:name") played since the start (tests).</summary>
        public static int PlayedCount(string key) => key != null && playedByKey.TryGetValue(key, out int n) ? n : 0;

        internal static void Count(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            playedByKey.TryGetValue(key, out int n);
            playedByKey[key] = n + 1;
        }

        /// <summary>The shared settings of a source on a channel: the 3D curve, the pause rule, the volume.</summary>
        public static void Configure(AudioSource source, SoundChannel channel, float baseVolume, bool spatial)
        {
            if (source == null) return;
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.dopplerLevel = 0f;
            source.minDistance = MinDistance;
            source.maxDistance = MaxDistance;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.ignoreListenerPause = channel == SoundChannel.Ui || channel == SoundChannel.Music;
            source.volume = baseVolume * Volume(channel);
        }

        /// <summary>A long-lived source that follows its channel's slider (a gun, a loop).</summary>
        public static void Bind(AudioSource source, SoundChannel channel, float baseVolume, bool spatial = true)
        {
            if (source == null) return;
            Configure(source, channel, baseVolume, spatial);
            ChannelSource bound = null;
            foreach (ChannelSource c in source.GetComponents<ChannelSource>())
                if (c.Source == source) bound = c;
            if (bound == null) bound = source.gameObject.AddComponent<ChannelSource>();
            bound.Set(source, channel, baseVolume);
        }

        /// <summary>A one-shot at a point on a channel. It destroys itself when the clip ends.</summary>
        public static AudioSource PlayClip(AudioClip clip, Vector3 at, SoundChannel channel, float volume = 1f, bool spatial = true, float pitch = 1f, string key = null)
        {
            if (clip == null) return null;
            var obj = new GameObject("Sound_" + clip.name);
            obj.transform.position = at;
            var source = obj.AddComponent<AudioSource>();
            Configure(source, channel, volume, spatial);
            source.clip = clip;
            source.pitch = pitch;
            source.Play();
            Object.Destroy(obj, clip.length / Mathf.Max(0.1f, Mathf.Abs(pitch)) + 0.1f);
            Played++;
            LastClip = clip;
            LastChannel = channel;
            LastSource = source;
            Count(key ?? "clip:" + clip.name);
            return source;
        }

        /// <summary>The volumes changed: every bound source takes its new level.</summary>
        public static void Refresh() => ChannelSource.RefreshAll();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Played = 0;
            LastClip = null;
            LastSource = null;
            playedByKey.Clear();
        }
    }

    /// <summary>Keeps one source at its base volume times its channel's slider.</summary>
    public sealed class ChannelSource : MonoBehaviour
    {
        private static readonly List<ChannelSource> all = new List<ChannelSource>();

        public AudioSource Source { get; private set; }
        public SoundChannel Channel { get; private set; }
        public float BaseVolume { get; private set; } = 1f;

        public void Set(AudioSource source, SoundChannel channel, float baseVolume)
        {
            Source = source;
            Channel = channel;
            BaseVolume = baseVolume;
            Apply();
        }

        public void Apply()
        {
            if (Source != null) Source.volume = BaseVolume * GameAudio.Volume(Channel);
        }

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        internal static void RefreshAll()
        {
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (all[i] == null) all.RemoveAt(i);
                else all[i].Apply();
            }
        }
    }
}
