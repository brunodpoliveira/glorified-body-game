using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace GlorifiedBody.Rendering
{
    /// <summary>
    /// T26 remake look, switch 5: the light in the air (Hidden/GB/Volumetric). After the transparents, a ray march at
    /// half size through the lights marked with <see cref="VolumetricLight"/> (at most four), with their real-time
    /// shadow maps, then a blur in two passes, then the result added over the frame (before the bloom and the grade,
    /// so the lamps glow through them). Off when <see cref="On"/> is false or no marked light is visible.
    /// </summary>
    public sealed class VolumetricLightFeature : ScriptableRendererFeature
    {
        /// <summary>The switch (RemakeDirector). Off by default: only profile B turns it on.</summary>
        public static bool On;
        // A white tiled room hides added light: at 0.14 the cone added 3 levels of 255 (check run 182605); 1.0 shows it.
        public static float Density = 1.0f;
        /// <summary>For the bot: the frames the pass ran and the marked lights it last found.</summary>
        public static int Runs, LastFound;
        /// <summary>0 the look; checks: 1 the march length and the found light, 2 the marked light on the surfaces, 3 the same with no shadow, 4 the shadow only.</summary>
        public static int CheckMode;
        public static float MaxDistance = 10f;
        public static int Steps = 32;
        public static float Anisotropy = 0.35f;
        public static float NoiseScale = 0.9f;
        public static float NoiseAmount = 0.55f;
        public static Vector3 Wind = new Vector3(0.05f, 0.02f, 0.03f);

        [SerializeField] private Shader shader;
        private Material material;
        private VolumetricPass pass;

        public void SetShader(Shader value) => shader = value;

        public override void Create()
        {
            if (shader == null) shader = Shader.Find("Hidden/GB/Volumetric");
            if (shader != null && material == null) material = CoreUtils.CreateEngineMaterial(shader);
            pass = new VolumetricPass(material) { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!On || material == null || VolumetricLight.All.Count == 0) return;
            CameraType type = renderingData.cameraData.cameraType;
            if (type != CameraType.Game && type != CameraType.SceneView) return;
            renderer.EnqueuePass(pass);
        }

        private sealed class VolumetricPass : ScriptableRenderPass
        {
            private static readonly int LightPos = Shader.PropertyToID("_VolLightPos");
            private static readonly int Counts = Shader.PropertyToID("_VolCounts");
            private static readonly int ShadowAtlas = Shader.PropertyToID("_AdditionalLightsShadowmapTexture");
            private readonly Vector4[] positions = new Vector4[4];
            private static readonly int Params = Shader.PropertyToID("_VolParams");
            private static readonly int Noise = Shader.PropertyToID("_VolNoise");
            private static readonly int WindId = Shader.PropertyToID("_VolWind");
            private static readonly int Texel = Shader.PropertyToID("_VolTexel");
            private readonly Material material;

            private class PassData
            {
                public Material material;
                public TextureHandle source;
                public TextureHandle shadows;
                public int pass;
            }

            public VolumetricPass(Material material)
            {
                this.material = material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null) return;
                UniversalResourceData resources = frameData.Get<UniversalResourceData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                // The marked lights that are visible this frame, by position: the shader finds each one in URP's
                // additional-light arrays by its position (the order of those arrays is URP's business).
                int found = 0, additional = 0;
                var visible = lightData.visibleLights;
                for (int i = 0; i < visible.Length; i++)
                {
                    if (i == lightData.mainLightIndex) continue;
                    additional++;
                    Light light = visible[i].light;
                    if (found < 4 && light != null && VolumetricLight.Strengths.TryGetValue(light, out float strength))
                    {
                        Vector3 p = visible[i].localToWorldMatrix.GetColumn(3);
                        positions[found++] = new Vector4(p.x, p.y, p.z, strength);
                    }
                }
                for (int k = found; k < 4; k++) positions[k] = Vector4.zero;
                LastFound = found;
                if (found == 0) return;
                Runs++;

                material.SetVectorArray(LightPos, positions);
                material.SetVector(Counts, new Vector4(Mathf.Min(additional, 256), CheckMode, 0f, 0f));
                material.SetVector(Params, new Vector4(Density, MaxDistance, Steps, Anisotropy));
                material.SetVector(Noise, new Vector4(NoiseScale, NoiseAmount, 0f, 0f));
                material.SetVector(WindId, new Vector4(Wind.x, Wind.y, Wind.z, Application.isPlaying ? Time.time : 0f));

                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                int width = Mathf.Max(16, desc.width / 2), height = Mathf.Max(16, desc.height / 2);
                material.SetVector(Texel, new Vector4(1f / width, 1f / height, 0f, 0f));
                var texDesc = new TextureDesc(width, height)
                {
                    colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "GB_Volumetric_A",
                };
                TextureHandle a = renderGraph.CreateTexture(texDesc);
                texDesc.name = "GB_Volumetric_B";
                TextureHandle b = renderGraph.CreateTexture(texDesc);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("GB Volumetric March", out var data))
                {
                    data.material = material;
                    data.pass = 0;
                    builder.SetRenderAttachment(a, 0);
                    builder.UseAllGlobalTextures(true);
                    if (resources.cameraDepthTexture.IsValid()) builder.UseTexture(resources.cameraDepthTexture);
                    // The shadow atlas by its handle as well as the global: the march samples it at every step.
                    data.shadows = resources.additionalShadowsTexture;
                    if (data.shadows.IsValid()) builder.UseTexture(data.shadows);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                    {
                        if (d.shadows.IsValid()) d.material.SetTexture(ShadowAtlas, d.shadows);
                        Blitter.BlitTexture(ctx.cmd, new Vector4(1, 1, 0, 0), d.material, d.pass);
                    });
                }
                Blit(renderGraph, a, b, 1, "GB Volumetric BlurH");
                Blit(renderGraph, b, a, 2, "GB Volumetric BlurV");
                Blit(renderGraph, a, resources.activeColorTexture, 3, "GB Volumetric Add");
            }

            private void Blit(RenderGraph renderGraph, TextureHandle source, TextureHandle target, int passIndex, string name)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(name, out var data))
                {
                    data.material = material;
                    data.source = source;
                    data.pass = passIndex;
                    builder.UseTexture(source);
                    builder.SetRenderAttachment(target, 0);
                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) => Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, d.pass));
                }
            }
        }
    }
}
