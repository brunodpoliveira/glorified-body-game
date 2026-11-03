using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Rendering
{
    public enum GraphicsPreset { Low = 0, Medium = 1, High = 2, Ultra = 3, Custom = 4 }
    public enum AntiAliasing { Off = 0, Fxaa = 1, Smaa = 2, Msaa2 = 3, Msaa4 = 4, Msaa8 = 5 }
    /// <summary>The texture mip limit: Low a quarter of the size, Medium a half, High the full 2K.</summary>
    public enum TextureQuality { Low = 0, Medium = 1, High = 2 }
    public enum TextureFiltering { Off = 0, X4 = 1, X8 = 2, X16 = 3 }
    /// <summary>No soft shadows: the shadows stay hard (ART_LESSONS 1.3). Only the map sizes change.</summary>
    public enum ShadowLevel { Off = 0, Low = 1, Medium = 2, High = 3, Ultra = 4 }
    public enum DisplayMode { Fullscreen = 0, Borderless = 1, Windowed = 2 }

    /// <summary>
    /// The graphics options (2026-11-03, Bruno: "in the graphics we should allow the usual knobs and changes that most games
    /// allow: anti-aliasing, quality textures, resolution, graphical options on/off that sort of thing"). A preset sets the
    /// knobs (Low, Medium, High, Ultra); a change to one knob makes the preset Custom. The display mode, the frame rate
    /// limit and the brightness stand apart from the preset; the resolution and VSync stay in SettingsManager.
    /// ApplyAll puts them on the URP asset (MSAA, render scale, shadow maps), the quality settings (texture mips, filtering,
    /// frame rate), the stock renderer's ambient occlusion and the main camera (FXAA or SMAA, the brightness volume). The
    /// URP asset is a shared file: the first change keeps its own values, and the end of play puts them back.
    /// A batch run (the tests, the bots) reads and writes its own keys ("batch_" first), so Bruno's choices never change a
    /// test. Numbers PROPOSED.
    /// </summary>
    public static class GraphicsOptions
    {
        public static readonly int[] RenderScales = { 50, 67, 75, 85, 100 };
        public static readonly int[] FrameLimits = { 0, 30, 60, 120, 144 }; // 0: no limit
        public const float BrightnessRange = 1f;                             // exposure stops each way

        public static GraphicsPreset Preset { get; private set; } = GraphicsPreset.High;
        public static AntiAliasing AntiAliasing { get; private set; }
        public static int RenderScale { get; private set; } = 100;
        public static TextureQuality Textures { get; private set; }
        public static TextureFiltering Filtering { get; private set; }
        public static ShadowLevel Shadows { get; private set; }
        public static bool AmbientOcclusion { get; private set; }
        public static DisplayMode Display { get; private set; } = DisplayMode.Borderless;
        public static int FrameLimit { get; private set; }
        public static float Brightness { get; private set; }

        /// <summary>Any option changed (the applier and the menu follow it).</summary>
        public static event Action OnChanged;

        private static bool loaded;
        private static string Prefix => Application.isBatchMode ? "batch_gfx_" : "gfx_";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            loaded = false;
            OnChanged = null;
            own = null;
            brightnessVolume = null;
            Application.quitting -= RestoreAsset;
            Application.quitting += RestoreAsset;
        }

        // ── The presets ───────────────────────────────────────────────

        private struct Knobs
        {
            public AntiAliasing aa;
            public int scale;
            public TextureQuality textures;
            public TextureFiltering filtering;
            public ShadowLevel shadows;
            public bool ao;
        }

        private static Knobs KnobsOf(GraphicsPreset preset)
        {
            switch (preset)
            {
                case GraphicsPreset.Low: return new Knobs { aa = AntiAliasing.Off, scale = 67, textures = TextureQuality.Medium, filtering = TextureFiltering.Off, shadows = ShadowLevel.Low, ao = false };
                case GraphicsPreset.Medium: return new Knobs { aa = AntiAliasing.Fxaa, scale = 85, textures = TextureQuality.High, filtering = TextureFiltering.X4, shadows = ShadowLevel.Medium, ao = false };
                case GraphicsPreset.Ultra: return new Knobs { aa = AntiAliasing.Msaa4, scale = 100, textures = TextureQuality.High, filtering = TextureFiltering.X16, shadows = ShadowLevel.Ultra, ao = true };
                default: return new Knobs { aa = AntiAliasing.Smaa, scale = 100, textures = TextureQuality.High, filtering = TextureFiltering.X8, shadows = ShadowLevel.High, ao = true };
            }
        }

        private static void SetKnobs(Knobs k)
        {
            AntiAliasing = k.aa;
            RenderScale = k.scale;
            Textures = k.textures;
            Filtering = k.filtering;
            Shadows = k.shadows;
            AmbientOcclusion = k.ao;
        }

        // ── The setters (each saves and applies) ──────────────────────

        public static void SetPreset(GraphicsPreset preset)
        {
            Load();
            Preset = preset;
            if (preset != GraphicsPreset.Custom) SetKnobs(KnobsOf(preset));
            Changed();
        }

        public static void SetAntiAliasing(AntiAliasing value) { Load(); AntiAliasing = value; Custom(); }
        public static void SetRenderScale(int percent) { Load(); RenderScale = Mathf.Clamp(percent, 50, 100); Custom(); }
        public static void SetTextures(TextureQuality value) { Load(); Textures = value; Custom(); }
        public static void SetFiltering(TextureFiltering value) { Load(); Filtering = value; Custom(); }
        public static void SetShadows(ShadowLevel value) { Load(); Shadows = value; Custom(); }
        public static void SetAmbientOcclusion(bool on) { Load(); AmbientOcclusion = on; Custom(); }
        public static void SetDisplay(DisplayMode value) { Load(); Display = value; Changed(); }
        public static void SetFrameLimit(int fps) { Load(); FrameLimit = Mathf.Max(0, fps); Changed(); }
        public static void SetBrightness(float stops) { Load(); Brightness = Mathf.Clamp(stops, -BrightnessRange, BrightnessRange); Changed(); }

        /// <summary>A knob moved: the preset is Custom unless the knobs still match one.</summary>
        private static void Custom()
        {
            Preset = GraphicsPreset.Custom;
            foreach (GraphicsPreset p in new[] { GraphicsPreset.Low, GraphicsPreset.Medium, GraphicsPreset.High, GraphicsPreset.Ultra })
                if (Matches(KnobsOf(p))) Preset = p;
            Changed();
        }

        private static bool Matches(Knobs k) => k.aa == AntiAliasing && k.scale == RenderScale && k.textures == Textures
            && k.filtering == Filtering && k.shadows == Shadows && k.ao == AmbientOcclusion;

        public static void ResetToDefaults()
        {
            Load();
            Preset = GraphicsPreset.High;
            SetKnobs(KnobsOf(GraphicsPreset.High));
            Display = DisplayMode.Borderless;
            FrameLimit = 0;
            Brightness = 0f;
            Changed();
        }

        private static void Changed()
        {
            Save();
            ApplyAll();
            RunTelemetry.Event("graphics", $"{Preset} aa={AntiAliasing} scale={RenderScale} tex={Textures} aniso={Filtering} shadows={Shadows} ao={AmbientOcclusion} display={Display} fps={FrameLimit} bright={Brightness:0.00}");
            OnChanged?.Invoke();
        }

        // ── Save and load ─────────────────────────────────────────────

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            string p = Prefix;
            Preset = (GraphicsPreset)PlayerPrefs.GetInt(p + "preset", (int)GraphicsPreset.High);
            SetKnobs(KnobsOf(Preset == GraphicsPreset.Custom ? GraphicsPreset.High : Preset));
            if (Preset == GraphicsPreset.Custom)
            {
                AntiAliasing = (AntiAliasing)PlayerPrefs.GetInt(p + "aa", (int)AntiAliasing);
                RenderScale = PlayerPrefs.GetInt(p + "scale", RenderScale);
                Textures = (TextureQuality)PlayerPrefs.GetInt(p + "textures", (int)Textures);
                Filtering = (TextureFiltering)PlayerPrefs.GetInt(p + "filtering", (int)Filtering);
                Shadows = (ShadowLevel)PlayerPrefs.GetInt(p + "shadows", (int)Shadows);
                AmbientOcclusion = PlayerPrefs.GetInt(p + "ao", AmbientOcclusion ? 1 : 0) == 1;
            }
            // Before 2026-11-03 the window was a fullscreen on/off (SettingsManager): off becomes Windowed.
            int legacyFullscreen = PlayerPrefs.GetInt("settings_fullscreen", 1);
            Display = (DisplayMode)PlayerPrefs.GetInt(p + "display", legacyFullscreen == 1 || Application.isBatchMode ? (int)DisplayMode.Borderless : (int)DisplayMode.Windowed);
            FrameLimit = PlayerPrefs.GetInt(p + "fps", 0);
            Brightness = PlayerPrefs.GetFloat(p + "brightness", 0f);
        }

        /// <summary>Reads the saved choices again (the tests).</summary>
        public static void Reload()
        {
            loaded = false;
            Load();
        }

        private static void Save()
        {
            string p = Prefix;
            PlayerPrefs.SetInt(p + "preset", (int)Preset);
            PlayerPrefs.SetInt(p + "aa", (int)AntiAliasing);
            PlayerPrefs.SetInt(p + "scale", RenderScale);
            PlayerPrefs.SetInt(p + "textures", (int)Textures);
            PlayerPrefs.SetInt(p + "filtering", (int)Filtering);
            PlayerPrefs.SetInt(p + "shadows", (int)Shadows);
            PlayerPrefs.SetInt(p + "ao", AmbientOcclusion ? 1 : 0);
            PlayerPrefs.SetInt(p + "display", (int)Display);
            PlayerPrefs.SetInt(p + "fps", FrameLimit);
            PlayerPrefs.SetFloat(p + "brightness", Brightness);
            PlayerPrefs.Save();
        }

        // ── Apply ─────────────────────────────────────────────────────

        public static int MsaaSamples => AntiAliasing == AntiAliasing.Msaa2 ? 2 : AntiAliasing == AntiAliasing.Msaa4 ? 4 : AntiAliasing == AntiAliasing.Msaa8 ? 8 : 1;
        public static AntialiasingMode PostAntiAliasing => AntiAliasing == AntiAliasing.Fxaa ? AntialiasingMode.FastApproximateAntialiasing
            : AntiAliasing == AntiAliasing.Smaa ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
        /// <summary>The sun's map and the spots' atlas (the High pair is the look's own: 2048 and 4096).</summary>
        public static int MainShadowMap => Shadows == ShadowLevel.Low ? 512 : Shadows == ShadowLevel.Medium ? 1024 : Shadows == ShadowLevel.Ultra ? 4096 : 2048;
        public static int SpotShadowAtlas => Shadows == ShadowLevel.Low ? 1024 : Shadows == ShadowLevel.Medium ? 2048 : Shadows == ShadowLevel.Ultra ? 8192 : 4096;
        public static int Anisotropy => Filtering == TextureFiltering.X4 ? 4 : Filtering == TextureFiltering.X8 ? 8 : Filtering == TextureFiltering.X16 ? 16 : 1;

        private struct OwnValues { public int msaa, mainMap, atlas; public float scale; public bool ao; }
        private static OwnValues? own;
        private static Volume brightnessVolume;
        private static ColorAdjustments exposure;

        private static UniversalRenderPipelineAsset Pipeline => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        /// <summary>The stock renderer's ambient occlusion (renderer 0, the one the game draws with).</summary>
        private static ScreenSpaceAmbientOcclusion Occlusion(UniversalRenderPipelineAsset asset)
        {
            if (asset == null || asset.rendererDataList.Length == 0 || asset.rendererDataList[0] == null) return null;
            foreach (ScriptableRendererFeature f in asset.rendererDataList[0].rendererFeatures)
                if (f is ScreenSpaceAmbientOcclusion ao) return ao;
            return null;
        }

        /// <summary>The window of the display mode (SettingsManager uses it with the resolution).</summary>
        public static FullScreenMode ScreenMode => Display == DisplayMode.Fullscreen ? FullScreenMode.ExclusiveFullScreen
            : Display == DisplayMode.Borderless ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        public static void ApplyAll()
        {
            Load();
            UniversalRenderPipelineAsset asset = Pipeline;
            if (asset != null)
            {
                ScreenSpaceAmbientOcclusion ao = Occlusion(asset);
                if (!own.HasValue)
                    own = new OwnValues { msaa = asset.msaaSampleCount, scale = asset.renderScale, mainMap = asset.mainLightShadowmapResolution, atlas = asset.additionalLightsShadowmapResolution, ao = ao != null && ao.isActive };
                asset.msaaSampleCount = MsaaSamples;
                asset.renderScale = RenderScale / 100f;
                asset.mainLightShadowmapResolution = MainShadowMap;
                asset.additionalLightsShadowmapResolution = SpotShadowAtlas;
                if (ao != null) ao.SetActive(AmbientOcclusion);
            }
            ShadowDistance.ShadowsOn = Shadows != ShadowLevel.Off;
            QualitySettings.globalTextureMipmapLimit = 2 - (int)Textures;
            QualitySettings.anisotropicFiltering = Filtering == TextureFiltering.Off ? AnisotropicFiltering.Disable : AnisotropicFiltering.ForceEnable;
            Texture.SetGlobalAnisotropicFilteringLimits(Anisotropy, Anisotropy);
            Application.targetFrameRate = FrameLimit > 0 ? FrameLimit : -1;
            ApplyDisplayMode();
            ApplyCamera(Camera.main);
        }

        private static void ApplyDisplayMode()
        {
            if (Application.isBatchMode || Application.isEditor) return;
            if (Screen.fullScreenMode != ScreenMode) Screen.fullScreenMode = ScreenMode;
        }

        /// <summary>The camera's part: FXAA or SMAA, and the brightness (a post-processing volume, on only when it moves).</summary>
        public static void ApplyCamera(Camera cam)
        {
            if (cam == null) return;
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.antialiasing = PostAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            bool bright = Mathf.Abs(Brightness) > 0.001f;
            if (bright) EnsureBrightnessVolume();
            if (exposure != null) exposure.postExposure.value = Brightness;
            if (brightnessVolume != null) brightnessVolume.enabled = bright;
            data.renderPostProcessing = bright || HasOtherVolume();
        }

        private static bool HasOtherVolume()
        {
            foreach (Volume v in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                if (v != brightnessVolume && v.isActiveAndEnabled) return true;
            return false;
        }

        private static void EnsureBrightnessVolume()
        {
            if (brightnessVolume != null) return;
            var go = new GameObject("[Brightness]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            brightnessVolume = go.AddComponent<Volume>();
            brightnessVolume.isGlobal = true;
            brightnessVolume.priority = 1000f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            exposure = profile.Add<ColorAdjustments>(true);
            exposure.postExposure.overrideState = true;
            brightnessVolume.sharedProfile = profile;
        }

        /// <summary>The URP asset's own values back (the end of play: the asset is a file).</summary>
        public static void RestoreAsset()
        {
            UniversalRenderPipelineAsset asset = Pipeline;
            if (asset != null && own.HasValue)
            {
                asset.msaaSampleCount = own.Value.msaa;
                asset.renderScale = own.Value.scale;
                asset.mainLightShadowmapResolution = own.Value.mainMap;
                asset.additionalLightsShadowmapResolution = own.Value.atlas;
                ScreenSpaceAmbientOcclusion ao = Occlusion(asset);
                if (ao != null) ao.SetActive(own.Value.ao);
            }
            own = null;
        }

        /// <summary>The volume of the brightness (the tests).</summary>
        public static float AppliedExposure => brightnessVolume != null && brightnessVolume.enabled && exposure != null ? exposure.postExposure.value : 0f;
    }
}
