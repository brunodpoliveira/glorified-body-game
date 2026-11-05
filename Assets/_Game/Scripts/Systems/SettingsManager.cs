using System;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Rendering;

namespace GlorifiedBody.Systems
{
    /// <summary>
    /// Manages persistent game settings: audio volumes, display/graphics, mouse sensitivity.
    /// Uses PlayerPrefs for storage. Works alongside InputManager for keybinds.
    /// Persists across scenes via DontDestroyOnLoad.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }


        // ── Audio ──────────────────────────────────────────────────────

        [Header("Audio Defaults")]
        [SerializeField] private float defaultMasterVolume = 1f;
        [SerializeField] private float defaultSFXVolume = 1f;
        [SerializeField] private float defaultMusicVolume = 1f;
        [SerializeField] private float defaultVoiceVolume = 1f;

        private float masterVolume;
        private float sfxVolume;
        private float musicVolume;
        private float voiceVolume;

        /// <summary>A volume changed (the sound channels follow it, GlorifiedBody.Audio.GameAudio).</summary>
        public event Action OnAudioChanged;

        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                ApplyAudioSettings();
                SaveSettings();
            }
        }

        public float SFXVolume
        {
            get => sfxVolume;
            set
            {
                sfxVolume = Mathf.Clamp01(value);
                ApplyAudioSettings();
                SaveSettings();
            }
        }

        public float MusicVolume
        {
            get => musicVolume;
            set
            {
                musicVolume = Mathf.Clamp01(value);
                ApplyAudioSettings();
                SaveSettings();
            }
        }

        /// <summary>The spoken lines: barks, the PA, the cutscenes, the death screen (2026-10-26).</summary>
        public float VoiceVolume
        {
            get => voiceVolume;
            set
            {
                voiceVolume = Mathf.Clamp01(value);
                ApplyAudioSettings();
                SaveSettings();
            }
        }

        // ── Mouse ──────────────────────────────────────────────────────

        [Header("Mouse Defaults")]
        [SerializeField] private float defaultMouseSensitivity = 3f;
        [SerializeField] private float minMouseSensitivity = 0.5f;
        [SerializeField] private float maxMouseSensitivity = 10f;

        private float mouseSensitivity;
        private bool mouseTurn;

        /// <summary>
        /// The mouse turns her (2026-11-03, Bruno: "keep it and have it actually do something"): a left or right move of the
        /// mouse adds to A and D, MouseSensitivity degrees for each unit of "Mouse X". Off: the keys alone (pure tank controls).
        /// </summary>
        public bool MouseTurn
        {
            get => mouseTurn;
            set
            {
                mouseTurn = value;
                SaveSettings();
            }
        }

        public float MouseSensitivity
        {
            get => mouseSensitivity;
            set
            {
                mouseSensitivity = Mathf.Clamp(value, minMouseSensitivity, maxMouseSensitivity);
                SaveSettings();
                OnMouseSensitivityChanged?.Invoke(mouseSensitivity);
            }
        }

        public float MinMouseSensitivity => minMouseSensitivity;
        public float MaxMouseSensitivity => maxMouseSensitivity;
        /// <summary>
        /// Fired when mouse sensitivity changes. ThirdPersonOrbitCamera subscribes to this.
        /// </summary>
        public event Action<float> OnMouseSensitivityChanged;

        // ── Display ────────────────────────────────────────────────────

        [Header("Display Defaults")]
        [SerializeField] private int defaultVSync = 1;

        private int resolutionIndex;
        private int vSyncCount;

        /// <summary>The window is not windowed (the display mode is a graphics option since 2026-11-03).</summary>
        public bool Fullscreen => GlorifiedBody.Rendering.GraphicsOptions.Display != GlorifiedBody.Rendering.DisplayMode.Windowed;

        /// <summary>
        /// The screen's sizes from lowest to highest, one row for each size at its best refresh rate (Bruno, 2026-11-05: "it
        /// should be from lowest to highest res"; Screen.resolutions holds no promise of an order and lists a size once for
        /// each rate). ResolutionIndex is a place in this list.
        /// </summary>
        public static Resolution[] Resolutions()
        {
            var bySize = new Dictionary<long, Resolution>();
            foreach (Resolution r in Screen.resolutions)
            {
                long size = r.width * 100000L + r.height;
                if (!bySize.TryGetValue(size, out Resolution best) || r.refreshRateRatio.value > best.refreshRateRatio.value) bySize[size] = r;
            }
            var sizes = new List<long>(bySize.Keys);
            sizes.Sort();
            var list = new Resolution[sizes.Count];
            for (int i = 0; i < sizes.Count; i++) list[i] = bySize[sizes[i]];
            return list;
        }

        public int ResolutionIndex
        {
            get => resolutionIndex;
            set
            {
                resolutionIndex = value;
                ApplyDisplaySettings();
                SaveSettings();
            }
        }

        public int VSyncCount
        {
            get => vSyncCount;
            set
            {
                vSyncCount = Mathf.Clamp(value, 0, 2);
                QualitySettings.vSyncCount = vSyncCount;
                SaveSettings();
            }
        }




        /// <summary>The Gore option (Bruno, 2026-10-24: the Gore System): 0 Off, 1 Low, 2 Full. GoreBootstrap follows it.</summary>
        public int GoreLevel
        {
            get => goreLevel;
            set
            {
                goreLevel = Mathf.Clamp(value, 0, 2);
                SaveSettings();
                OnGoreLevelChanged?.Invoke(goreLevel);
            }
        }

        public event Action<int> OnGoreLevelChanged;
        private int goreLevel = 2;

        /// <summary>The subtitles (Bruno, 2026-10-25: "on by default"). SubtitleUI follows it.</summary>
        public bool SubtitlesOn
        {
            get => subtitlesOn;
            set
            {
                subtitlesOn = value;
                SaveSettings();
            }
        }
        private bool subtitlesOn = true;

        /// <summary>The subtitle size: 0 Small, 1 Medium (the default), 2 Large.</summary>
        public int SubtitleSize
        {
            get => subtitleSize;
            set
            {
                subtitleSize = Mathf.Clamp(value, 0, 2);
                SaveSettings();
            }
        }
        private int subtitleSize = 1;

        // ── PlayerPrefs Keys ───────────────────────────────────────────

        private const string KEY_MASTER_VOL = "settings_masterVolume";
        private const string KEY_SFX_VOL = "settings_sfxVolume";
        private const string KEY_MUSIC_VOL = "settings_musicVolume";
        private const string KEY_VOICE_VOL = "settings_voiceVolume";
        private const string KEY_MOUSE_SENS = "settings_mouseSensitivity";
        private const string KEY_MOUSE_TURN = "settings_mouseTurn";
        // The size, not a place in the list (2026-11-05: the list changed order, and "settings_resolutionIndex" meant nothing after it).
        private const string KEY_RES_WIDTH = "settings_resolutionWidth";
        private const string KEY_RES_HEIGHT = "settings_resolutionHeight";
        private const string KEY_VSYNC = "settings_vsync";
        private const string KEY_GORE_LEVEL = "settings_goreLevel";
        private const string KEY_SUBTITLES = "settings_subtitles";
        private const string KEY_SUBTITLE_SIZE = "settings_subtitleSize";

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }

        // ── Load / Save ───────────────────────────────────────────────

        private void LoadSettings()
        {
            masterVolume = PlayerPrefs.GetFloat(KEY_MASTER_VOL, defaultMasterVolume);
            sfxVolume = PlayerPrefs.GetFloat(KEY_SFX_VOL, defaultSFXVolume);
            musicVolume = PlayerPrefs.GetFloat(KEY_MUSIC_VOL, defaultMusicVolume);
            voiceVolume = PlayerPrefs.GetFloat(KEY_VOICE_VOL, defaultVoiceVolume);
            mouseSensitivity = PlayerPrefs.GetFloat(KEY_MOUSE_SENS, defaultMouseSensitivity);
            mouseTurn = PlayerPrefs.GetInt(KEY_MOUSE_TURN, 1) == 1;
            resolutionIndex = IndexOfSize(PlayerPrefs.GetInt(KEY_RES_WIDTH, 0), PlayerPrefs.GetInt(KEY_RES_HEIGHT, 0));
            if (resolutionIndex < 0) resolutionIndex = GetCurrentResolutionIndex();
            vSyncCount = PlayerPrefs.GetInt(KEY_VSYNC, defaultVSync);
            goreLevel = Mathf.Clamp(PlayerPrefs.GetInt(KEY_GORE_LEVEL, 2), 0, 2);
            subtitlesOn = PlayerPrefs.GetInt(KEY_SUBTITLES, 1) == 1;
            subtitleSize = Mathf.Clamp(PlayerPrefs.GetInt(KEY_SUBTITLE_SIZE, 1), 0, 2);

            ApplyAudioSettings();
            GlorifiedBody.Rendering.GraphicsOptions.Load();
            ApplyDisplaySettings();
            QualitySettings.vSyncCount = vSyncCount;
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetFloat(KEY_MASTER_VOL, masterVolume);
            PlayerPrefs.SetFloat(KEY_SFX_VOL, sfxVolume);
            PlayerPrefs.SetFloat(KEY_MUSIC_VOL, musicVolume);
            PlayerPrefs.SetFloat(KEY_VOICE_VOL, voiceVolume);
            PlayerPrefs.SetFloat(KEY_MOUSE_SENS, mouseSensitivity);
            PlayerPrefs.SetInt(KEY_MOUSE_TURN, mouseTurn ? 1 : 0);
            Resolution[] sizes = Resolutions();
            if (resolutionIndex >= 0 && resolutionIndex < sizes.Length)
            {
                PlayerPrefs.SetInt(KEY_RES_WIDTH, sizes[resolutionIndex].width);
                PlayerPrefs.SetInt(KEY_RES_HEIGHT, sizes[resolutionIndex].height);
            }
            PlayerPrefs.SetInt(KEY_VSYNC, vSyncCount);
            PlayerPrefs.SetInt(KEY_GORE_LEVEL, goreLevel);
            PlayerPrefs.SetInt(KEY_SUBTITLES, subtitlesOn ? 1 : 0);
            PlayerPrefs.SetInt(KEY_SUBTITLE_SIZE, subtitleSize);
            PlayerPrefs.Save();
        }

        // ── Apply ──────────────────────────────────────────────────────

        private void ApplyAudioSettings()
        {
            AudioListener.volume = Application.isBatchMode ? 0f : masterVolume; // batch runs (bots, tests) play silent
            OnAudioChanged?.Invoke();
        }

        private void ApplyDisplaySettings()
        {
            Resolution[] resolutions = Resolutions();
            if (resolutionIndex >= 0 && resolutionIndex < resolutions.Length)
            {
                Resolution res = resolutions[resolutionIndex];
                Screen.SetResolution(res.width, res.height, GlorifiedBody.Rendering.GraphicsOptions.ScreenMode);
            }
            else
            {
                Screen.fullScreenMode = GlorifiedBody.Rendering.GraphicsOptions.ScreenMode;
            }
        }

        private int GetCurrentResolutionIndex()
        {
            Resolution current = Screen.currentResolution;
            int here = IndexOfSize(current.width, current.height);
            return here >= 0 ? here : Resolutions().Length - 1;
        }

        /// <summary>The place of the size in Resolutions(), or -1.</summary>
        private static int IndexOfSize(int width, int height)
        {
            Resolution[] resolutions = Resolutions();
            for (int i = 0; i < resolutions.Length; i++)
                if (resolutions[i].width == width && resolutions[i].height == height) return i;
            return -1;
        }

        /// <summary>
        /// Reset all settings (not keybinds) to defaults.
        /// </summary>
        public void ResetToDefaults()
        {
            masterVolume = defaultMasterVolume;
            sfxVolume = defaultSFXVolume;
            musicVolume = defaultMusicVolume;
            voiceVolume = defaultVoiceVolume;
            mouseSensitivity = defaultMouseSensitivity;
            mouseTurn = true;
            vSyncCount = defaultVSync;
            resolutionIndex = GetCurrentResolutionIndex();

            ApplyAudioSettings();
            ApplyDisplaySettings();
            QualitySettings.vSyncCount = vSyncCount;
            OnMouseSensitivityChanged?.Invoke(mouseSensitivity);
            GlorifiedBody.Rendering.GraphicsOptions.ResetToDefaults();
            SaveSettings();
        }

    }
}
