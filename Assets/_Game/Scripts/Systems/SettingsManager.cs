using System;
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

        public enum WallRunInputMode
        {
            Toggle = 0,
            Hold = 1
        }

        public enum SlideInputMode
        {
            Toggle = 0,
            Hold = 1
        }

        public enum CatchThrowInputMode
        {
            Hold = 0,
            Toggle = 1
        }

        // ── Audio ──────────────────────────────────────────────────────

        [Header("Audio Defaults")]
        [SerializeField] private float defaultMasterVolume = 1f;
        [SerializeField] private float defaultSFXVolume = 1f;
        [SerializeField] private float defaultMusicVolume = 1f;

        private float masterVolume;
        private float sfxVolume;
        private float musicVolume;

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

        // ── Mouse ──────────────────────────────────────────────────────

        [Header("Mouse Defaults")]
        [SerializeField] private float defaultMouseSensitivity = 3f;
        [SerializeField] private float minMouseSensitivity = 0.5f;
        [SerializeField] private float maxMouseSensitivity = 10f;
        [SerializeField] private WallRunInputMode defaultWallRunMode = WallRunInputMode.Toggle;
        [SerializeField] private SlideInputMode defaultSlideMode = SlideInputMode.Toggle;
        [SerializeField] private CatchThrowInputMode defaultCatchThrowMode = CatchThrowInputMode.Hold;

        private float mouseSensitivity;
        private WallRunInputMode wallRunMode;
        private SlideInputMode slideMode;
        private CatchThrowInputMode catchThrowMode;

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
        public WallRunInputMode WallRunMode
        {
            get => wallRunMode;
            set
            {
                wallRunMode = ClampWallRunMode(value);
                SaveSettings();
            }
        }

        public SlideInputMode SlideMode
        {
            get => slideMode;
            set
            {
                slideMode = value;
                SaveSettings();
            }
        }

        public CatchThrowInputMode CatchThrowMode
        {
            get => catchThrowMode;
            set
            {
                catchThrowMode = value;
                SaveSettings();
            }
        }

        /// <summary>
        /// Fired when mouse sensitivity changes. ThirdPersonOrbitCamera subscribes to this.
        /// </summary>
        public event Action<float> OnMouseSensitivityChanged;

        // ── Display ────────────────────────────────────────────────────

        [Header("Display Defaults")]
        [SerializeField] private bool defaultFullscreen = true;
        [SerializeField] private int defaultVSync = 1;
        [SerializeField] private bool defaultRainEnabled = true;
        [SerializeField] private LookQuality defaultLookQuality = LookQuality.Authentic;

        private bool fullscreen;
        private int resolutionIndex;
        private int vSyncCount;
        private bool rainEnabled;
        private LookQuality lookQuality;

        public bool Fullscreen
        {
            get => fullscreen;
            set
            {
                fullscreen = value;
                ApplyDisplaySettings();
                SaveSettings();
            }
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

        public bool RainEnabled
        {
            get => rainEnabled;
            set
            {
                rainEnabled = value;
                SaveSettings();
            }
        }

        /// <summary>The LOOK option (Bruno, 2026-10-22): Authentic (the dither, no anti-aliasing) or Clear (MSAA and SMAA, no dither). LookQualityApplier follows it.</summary>
        public LookQuality LookQuality
        {
            get => lookQuality;
            set
            {
                lookQuality = Enum.IsDefined(typeof(LookQuality), value) ? value : LookQuality.Authentic;
                SaveSettings();
                OnLookQualityChanged?.Invoke(lookQuality);
            }
        }

        public event Action<LookQuality> OnLookQualityChanged;

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

        // ── PlayerPrefs Keys ───────────────────────────────────────────

        private const string KEY_MASTER_VOL = "settings_masterVolume";
        private const string KEY_SFX_VOL = "settings_sfxVolume";
        private const string KEY_MUSIC_VOL = "settings_musicVolume";
        private const string KEY_MOUSE_SENS = "settings_mouseSensitivity";
        private const string KEY_WALLRUN_MODE = "settings_wallRunMode";
        private const string KEY_SLIDE_MODE = "settings_slideMode";
        private const string KEY_CATCHTHROW_MODE = "settings_catchThrowMode";
        private const string KEY_FULLSCREEN = "settings_fullscreen";
        private const string KEY_RESOLUTION = "settings_resolutionIndex";
        private const string KEY_VSYNC = "settings_vsync";
        private const string KEY_RAIN_ENABLED = "settings_rainEnabled";
        private const string KEY_LOOK_QUALITY = "settings_lookQuality";
        private const string KEY_GORE_LEVEL = "settings_goreLevel";

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
            mouseSensitivity = PlayerPrefs.GetFloat(KEY_MOUSE_SENS, defaultMouseSensitivity);
            wallRunMode = ClampWallRunMode((WallRunInputMode)PlayerPrefs.GetInt(KEY_WALLRUN_MODE, (int)defaultWallRunMode));
            slideMode = (SlideInputMode)PlayerPrefs.GetInt(KEY_SLIDE_MODE, (int)defaultSlideMode);
            catchThrowMode = (CatchThrowInputMode)PlayerPrefs.GetInt(KEY_CATCHTHROW_MODE, (int)defaultCatchThrowMode);
            fullscreen = PlayerPrefs.GetInt(KEY_FULLSCREEN, defaultFullscreen ? 1 : 0) == 1;
            resolutionIndex = PlayerPrefs.GetInt(KEY_RESOLUTION, GetCurrentResolutionIndex());
            vSyncCount = PlayerPrefs.GetInt(KEY_VSYNC, defaultVSync);
            rainEnabled = PlayerPrefs.GetInt(KEY_RAIN_ENABLED, defaultRainEnabled ? 1 : 0) == 1;
            lookQuality = (LookQuality)PlayerPrefs.GetInt(KEY_LOOK_QUALITY, (int)defaultLookQuality);
            if (!Enum.IsDefined(typeof(LookQuality), lookQuality)) lookQuality = defaultLookQuality;
            goreLevel = Mathf.Clamp(PlayerPrefs.GetInt(KEY_GORE_LEVEL, 2), 0, 2);

            ApplyAudioSettings();
            ApplyDisplaySettings();
            QualitySettings.vSyncCount = vSyncCount;
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetFloat(KEY_MASTER_VOL, masterVolume);
            PlayerPrefs.SetFloat(KEY_SFX_VOL, sfxVolume);
            PlayerPrefs.SetFloat(KEY_MUSIC_VOL, musicVolume);
            PlayerPrefs.SetFloat(KEY_MOUSE_SENS, mouseSensitivity);
            PlayerPrefs.SetInt(KEY_WALLRUN_MODE, (int)wallRunMode);
            PlayerPrefs.SetInt(KEY_SLIDE_MODE, (int)slideMode);
            PlayerPrefs.SetInt(KEY_CATCHTHROW_MODE, (int)catchThrowMode);
            PlayerPrefs.SetInt(KEY_FULLSCREEN, fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(KEY_RESOLUTION, resolutionIndex);
            PlayerPrefs.SetInt(KEY_VSYNC, vSyncCount);
            PlayerPrefs.SetInt(KEY_RAIN_ENABLED, rainEnabled ? 1 : 0);
            PlayerPrefs.SetInt(KEY_LOOK_QUALITY, (int)lookQuality);
            PlayerPrefs.SetInt(KEY_GORE_LEVEL, goreLevel);
            PlayerPrefs.Save();
        }

        // ── Apply ──────────────────────────────────────────────────────

        private void ApplyAudioSettings()
        {
            AudioListener.volume = masterVolume;
        }

        private void ApplyDisplaySettings()
        {
            Resolution[] resolutions = Screen.resolutions;
            if (resolutionIndex >= 0 && resolutionIndex < resolutions.Length)
            {
                Resolution res = resolutions[resolutionIndex];
                Screen.SetResolution(res.width, res.height, fullscreen);
            }
            else
            {
                Screen.fullScreen = fullscreen;
            }
        }

        private int GetCurrentResolutionIndex()
        {
            Resolution current = Screen.currentResolution;
            Resolution[] resolutions = Screen.resolutions;
            for (int i = 0; i < resolutions.Length; i++)
            {
                if (resolutions[i].width == current.width && resolutions[i].height == current.height)
                    return i;
            }
            return resolutions.Length - 1;
        }

        /// <summary>
        /// Reset all settings (not keybinds) to defaults.
        /// </summary>
        public void ResetToDefaults()
        {
            masterVolume = defaultMasterVolume;
            sfxVolume = defaultSFXVolume;
            musicVolume = defaultMusicVolume;
            mouseSensitivity = defaultMouseSensitivity;
            wallRunMode = ClampWallRunMode(defaultWallRunMode);
            slideMode = defaultSlideMode;
            catchThrowMode = defaultCatchThrowMode;
            fullscreen = defaultFullscreen;
            vSyncCount = defaultVSync;
            rainEnabled = defaultRainEnabled;
            lookQuality = defaultLookQuality;
            resolutionIndex = GetCurrentResolutionIndex();

            ApplyAudioSettings();
            ApplyDisplaySettings();
            QualitySettings.vSyncCount = vSyncCount;
            OnMouseSensitivityChanged?.Invoke(mouseSensitivity);
            OnLookQualityChanged?.Invoke(lookQuality);
            SaveSettings();
        }

        private static WallRunInputMode ClampWallRunMode(WallRunInputMode mode)
        {
            return Enum.IsDefined(typeof(WallRunInputMode), mode) ? mode : WallRunInputMode.Toggle;
        }
    }
}
