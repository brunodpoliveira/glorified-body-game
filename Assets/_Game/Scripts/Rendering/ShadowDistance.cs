using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GlorifiedBody.Rendering
{
    /// <summary>
    /// The sun's shadow distance for the camera of the moment (2026-10-26, the exterior look test): an exterior
    /// camera sees a known area, so the 2048 shadow map spreads over that and no more. The value lives on the URP
    /// asset, a shared file: the first change keeps the asset's own value, and a new room or the end of play puts it
    /// back (the editor would save the changed asset otherwise).
    /// </summary>
    public static class ShadowDistance
    {
        private static float? original;

        public static float Current => Pipeline != null ? Pipeline.shadowDistance : 0f;

        private static UniversalRenderPipelineAsset Pipeline => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        /// <summary>The distance in metres, or 0 for the asset's own.</summary>
        public static void Set(float metres)
        {
            UniversalRenderPipelineAsset asset = Pipeline;
            if (asset == null) return;
            if (metres <= 0f)
            {
                Restore();
                return;
            }
            if (!original.HasValue) original = asset.shadowDistance;
            asset.shadowDistance = metres;
        }

        public static void Restore()
        {
            UniversalRenderPipelineAsset asset = Pipeline;
            if (asset != null && original.HasValue) asset.shadowDistance = original.Value;
            original = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Hook()
        {
            original = null;
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            Application.quitting -= Restore;
            Application.quitting += Restore;
        }

        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Restore();
        }
    }
}
