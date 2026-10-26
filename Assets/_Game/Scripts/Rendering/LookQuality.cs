using UnityEngine;
using UnityEngine.Rendering.Universal;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Rendering
{
    /// <summary>
    /// The LOOK option of the settings (Bruno, 2026-10-22: "some players might want pixel clarity, others
    /// the authentic look... we want the vibes of the look, but we don't need to be clamped by the same tech
    /// limitations"). Both looks keep the hard pools, the baked bounce, the 2K textures and the 1080p frame.
    /// </summary>
    public enum LookQuality
    {
        /// <summary>The 1999 frame: the 32-level Bayer dither on the background, no anti-aliasing.</summary>
        Authentic = 0,
        /// <summary>The same picture at its best: no dither, MSAA 4x and SMAA on the edges.</summary>
        Clear = 1
    }

    /// <summary>
    /// Applies the LOOK setting to the main camera of a room: the renderer (GB_Full with the dither for
    /// Authentic, the stock one for Clear), the MSAA count of the pipeline and the camera's anti-aliasing.
    /// On "[GameplaySystems]" in every playable room; it follows the setting while the room is open. In a
    /// look test room the LookDirector owns the renderer (a dressing may use the plate) and reads the
    /// quality itself.
    /// </summary>
    public sealed class LookQualityApplier : MonoBehaviour
    {
        public const int AuthenticRendererIndex = 1; // GB_Full: the stencil mark and the dither
        public const int ClearRendererIndex = 0;     // the stock renderer: no dither
        public const int ClearMsaa = 4;

        public static LookQuality Current => SettingsManager.Instance != null ? SettingsManager.Instance.LookQuality : LookQuality.Authentic;

        public static int RendererIndexFor(LookQuality quality) => quality == LookQuality.Clear ? ClearRendererIndex : AuthenticRendererIndex;

        private void Start()
        {
            Apply(Current);
            if (SettingsManager.Instance != null) SettingsManager.Instance.OnLookQualityChanged += Apply;
        }

        private void OnDestroy()
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.OnLookQualityChanged -= Apply;
        }

        // The asset is a shared file: the first change keeps its own value, the end of play puts it back (2026-10-26:
        // the editor kept the Clear look's MSAA on disk).
        private static int? ownMsaa;

        public static void RestoreAsset()
        {
            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            if (asset != null && ownMsaa.HasValue) asset.msaaSampleCount = ownMsaa.Value;
            ownMsaa = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void HookQuit()
        {
            ownMsaa = null;
            Application.quitting -= RestoreAsset;
            Application.quitting += RestoreAsset;
        }

        public static void Apply(LookQuality quality)
        {
            bool clear = quality == LookQuality.Clear;
            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            if (asset != null)
            {
                if (!ownMsaa.HasValue) ownMsaa = asset.msaaSampleCount;
                asset.msaaSampleCount = clear ? ClearMsaa : 1;
            }
            Camera cam = Camera.main;
            if (cam == null) return;
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.antialiasing = clear ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
            data.antialiasingQuality = AntialiasingQuality.High;
            if (LookDirector.Instance == null) data.SetRenderer(RendererIndexFor(quality));
            RunTelemetry.Event("look_quality", quality.ToString().ToLowerInvariant());
        }
    }
}
