using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace GlorifiedBody.Rendering
{
    /// <summary>
    /// The plate of the look test (CONCEPTS 7.5): the static room, rendered once per camera with the expensive
    /// settings into a texture, is drawn into the colour target before the opaques of every frame. The GB_Plate
    /// renderer then draws the room depth only (a RenderObjects feature with Hidden/GB/DepthOnly) and the
    /// characters on top, so the room hides them where it should. The texture comes from LookDirector; with no
    /// plate set, the pass does nothing and the camera clears as usual.
    /// </summary>
    public sealed class PlateBlitFeature : ScriptableRendererFeature
    {
        /// <summary>The plate to draw, set by LookDirector. Null = no plate.</summary>
        public static RenderTexture Plate { get; set; }

        private PlateBlitPass pass;

        public override void Create()
        {
            pass = new PlateBlitPass { renderPassEvent = RenderPassEvent.BeforeRenderingOpaques };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (Plate == null) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;
            renderer.EnqueuePass(pass);
        }

        private sealed class PlateBlitPass : ScriptableRenderPass
        {
            private RTHandle plateHandle;
            private RenderTexture plateOfHandle;

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                RenderTexture plate = Plate;
                if (plate == null) return;
                if (plateHandle == null || plateOfHandle != plate)
                {
                    plateHandle?.Release();
                    plateHandle = RTHandles.Alloc(plate);
                    plateOfHandle = plate;
                }
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                TextureHandle source = renderGraph.ImportTexture(plateHandle);
                renderGraph.AddBlitPass(source, resources.activeColorTexture, Vector2.one, Vector2.zero, passName: "GB Plate");
            }
        }
    }
}
