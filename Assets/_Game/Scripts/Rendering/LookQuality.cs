using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GlorifiedBody.Rendering
{
    /// <summary>
    /// Applies the graphics options (GraphicsOptions) in a room: on "[GameplaySystems]" in every playable room, it puts
    /// the options on at the start and follows each change while the room is open. The game draws with the stock renderer
    /// (index 0). In a look test room the LookDirector owns the renderer (a dressing may use the plate).
    /// The class keeps its old name: every room scene holds it. Until 2026-11-03 it applied the LOOK option (Authentic
    /// with the dither, or Clear); the dither went, and the anti-aliasing became one of the options.
    /// </summary>
    public sealed class LookQualityApplier : MonoBehaviour
    {
        public const int GameRendererIndex = 0; // the stock renderer (its ambient occlusion is an option)

        private void Start()
        {
            Apply();
            GraphicsOptions.OnChanged += Apply;
        }

        private void OnDestroy() => GraphicsOptions.OnChanged -= Apply;

        public static void Apply()
        {
            GraphicsOptions.ApplyAll();
            Camera cam = Camera.main;
            if (cam != null && LookDirector.Instance == null) cam.GetUniversalAdditionalCameraData().SetRenderer(GameRendererIndex);
        }
    }
}
