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
        private static float wanted;
        private static bool shadowsOn = true;

        /// <summary>The Shadows option (GraphicsOptions): off holds the distance at 0, so no light casts a shadow.</summary>
        public static bool ShadowsOn
        {
            get => shadowsOn;
            set
            {
                if (shadowsOn == value) return;
                shadowsOn = value;
                Set(wanted);
            }
        }

        public static float Current => Pipeline != null ? Pipeline.shadowDistance : 0f;

        private static UniversalRenderPipelineAsset Pipeline => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        /// <summary>The distance in metres, or 0 for the asset's own.</summary>
        public static void Set(float metres)
        {
            wanted = metres;
            UniversalRenderPipelineAsset asset = Pipeline;
            if (asset == null) return;
            if (!shadowsOn)
            {
                if (!original.HasValue) original = asset.shadowDistance;
                asset.shadowDistance = 0f;
                return;
            }
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
            wanted = 0f;
            shadowsOn = true;
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            Application.quitting -= Restore;
            Application.quitting += Restore;
        }

        private static void OnLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            wanted = 0f;
            Restore();
            if (!shadowsOn) Set(0f);
        }
    }
}
