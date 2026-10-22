using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using GlorifiedBody.Editor;
using GlorifiedBody.Rendering;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>The assets of the look test, as LookTestSetup.BuildAll and BakeAll write them. Batch: tools/unity.sh test editmode.</summary>
    public class LookTestAssetsTests
    {
        [Test]
        public void TheRendererAssets_Exist_WithTheirFeatures()
        {
            var full = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(LookTestSetup.FullRendererPath);
            var expensive = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(LookTestSetup.ExpensiveRendererPath);
            var plate = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(LookTestSetup.PlateRendererPath);
            Assert.IsNotNull(full, "GB_Full (LookTestSetup.BuildAll)");
            Assert.IsNotNull(expensive, "GB_Expensive");
            Assert.IsNotNull(plate, "GB_Plate");

            string[] fullNames = full.rendererFeatures.Select(f => f.name).ToArray();
            CollectionAssert.AreEqual(new[] { "CharacterStencil", "Dither" }, fullNames, "GB_Full: the stencil mark, then the dither");
            var dither = full.rendererFeatures.OfType<FullScreenPassRendererFeature>().First();
            Assert.IsTrue(dither.bindDepthStencilAttachment, "the dither reads the stencil");
            Assert.IsTrue(dither.fetchColorBuffer, "the dither reads the frame");
            Assert.AreEqual(FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing, dither.injectionPoint);
            Assert.IsNotNull(dither.passMaterial, "the dither material");
            Assert.AreEqual(LookTestSetup.DitherShaderName, dither.passMaterial.shader.name);
            Assert.AreEqual(LookTestSetup.DitherLevels, dither.passMaterial.GetFloat("_Levels"), 0.01f, "32 levels per channel");
            var stencil = full.rendererFeatures.OfType<RenderObjects>().First();
            Assert.AreEqual(1, stencil.settings.stencilSettings.stencilReference, "the characters write 1");
            Assert.AreEqual(LayerMask.GetMask("Player", "Enemy"), stencil.settings.filterSettings.LayerMask.value, "the character layers");

            Assert.IsTrue(expensive.rendererFeatures.Any(f => f is ScreenSpaceAmbientOcclusion), "GB_Expensive keeps the ambient occlusion");
            Assert.IsTrue(expensive.rendererFeatures.Any(f => f.name == "Dither"), "GB_Expensive dithers the plate");

            string[] plateNames = plate.rendererFeatures.Select(f => f.name).ToArray();
            CollectionAssert.AreEqual(new[] { "PlateBlit", "RoomDepth" }, plateNames, "GB_Plate: the plate, then the room depth");
            Assert.IsInstanceOf<PlateBlitFeature>(plate.rendererFeatures[0]);
            var roomDepth = (RenderObjects)plate.rendererFeatures[1];
            Assert.IsNotNull(roomDepth.settings.overrideMaterial, "the depth-only material");
            Assert.AreEqual(LookTestSetup.DepthOnlyShaderName, roomDepth.settings.overrideMaterial.shader.name);
            Assert.AreEqual(RenderPassEvent.BeforeRenderingOpaques, roomDepth.settings.Event);
            Assert.AreEqual(LayerMask.GetMask("Player", "Enemy"), plate.opaqueLayerMask.value, "GB_Plate draws only the characters as opaques");
        }

        [Test]
        public void TheUrpAsset_CarriesTheLookNumbers()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(LookTestSetup.StockPipelinePath);
            Assert.IsNotNull(asset, "PC_RPAsset");
            var so = new SerializedObject(asset);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            Assert.AreEqual(4, list.arraySize, "the stock renderer plus the three of the look");
            Assert.AreEqual("GB_Full", list.GetArrayElementAtIndex(LookTestSetup.FullRendererIndex).objectReferenceValue.name);
            Assert.AreEqual("GB_Expensive", list.GetArrayElementAtIndex(LookTestSetup.ExpensiveRendererIndex).objectReferenceValue.name);
            Assert.AreEqual("GB_Plate", list.GetArrayElementAtIndex(LookTestSetup.PlateRendererIndex).objectReferenceValue.name);
            Assert.AreEqual(LookDirector.LookRenderScale, asset.renderScale, 0.001f, "the frame at 1080p (Bruno, 2026-10-22)");
            Assert.AreEqual(1f, asset.renderScale, 0.001f, "scale 1");
            Assert.AreEqual(1, asset.msaaSampleCount, "MSAA off on disk: the Clear look sets 4x at runtime");
            Assert.AreEqual(UpscalingFilterSelection.Linear, asset.upscalingFilter);
            Assert.IsFalse(asset.supportsSoftShadows, "hard shadows only");
            Assert.IsTrue(asset.supportsAdditionalLightShadows, "the spots cast shadows");
            Assert.AreEqual(LightRenderingMode.PerPixel, asset.additionalLightsRenderingMode);
            Assert.AreEqual(LookTestSetup.ShadowAtlas, asset.additionalLightsShadowmapResolution, "the 4096 atlas (eleven spots at 1024)");
            Assert.AreEqual(1024, so.FindProperty("m_AdditionalLightsShadowResolutionTierHigh").intValue, "the high tier of the spots");
        }

        [Test]
        public void TheTextures_AreImportedAsTheLookWants()
        {
            foreach (string name in LookTestSetup.TextureNames)
            {
                string path = $"{LookTestSetup.TexturesFolder}/{name}.png";
                Assert.IsTrue(File.Exists(path), $"{path} (tools/look_textures.sh then import, or tools/look_textures_procedural.py)");
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, name);
                Assert.AreEqual(LookTestSetup.TextureSize, importer.maxTextureSize, name + ": 2K (Bruno: clarity)");
                Assert.AreEqual(FilterMode.Trilinear, importer.filterMode, name + ": clean filtering");
                Assert.AreEqual(LookTestSetup.TextureAniso, importer.anisoLevel, name + ": the floors stay sharp at a low angle");
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, name + ": no compression");
                bool stretched = LookTestSetup.StretchedTextures.Contains(name);
                Assert.AreEqual(stretched ? TextureWrapMode.Clamp : TextureWrapMode.Mirror, importer.wrapMode, name + ": the wrap");
                Assert.AreEqual(name == "fern", importer.alphaIsTransparency, name + ": only the fern card has a cutout");
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(texture, name);
                Assert.GreaterOrEqual(Mathf.Max(texture.width, texture.height), 2048, name + ": the picture is 2K");
            }
        }

        [Test]
        public void TheScenes_AreInTheBuildList()
        {
            foreach (string path in LookTestSetup.ScenePaths)
            {
                Assert.IsTrue(File.Exists(path), path);
                Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path), path + " in Build Settings");
            }
        }

        [Test]
        public void TheBake_LeftLightmapsAndProbes_ForEveryLookScene()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<LightingSettings>(LookTestSetup.LightingSettingsPath), "the lighting settings of the bake");
            foreach (string path in LookTestSetup.ScenePaths)
            {
                string folder = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
                Assert.IsTrue(Directory.Exists(folder), folder + " (LookTestSetup.BakeAll)");
                Assert.IsTrue(File.Exists(Path.Combine(folder, "LightingData.asset")), folder + "/LightingData.asset");
                Assert.IsTrue(Directory.GetFiles(folder, "Lightmap-*").Any(), folder + ": a lightmap");
            }
        }
    }
}
