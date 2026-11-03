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

            // No dither (Bruno, 2026-11-03: "you may remove the dithering effect"), and no stencil mark of the characters
            // (it served the dither only), in any renderer.
            Assert.IsEmpty(full.rendererFeatures, "GB_Full: no feature");
            Assert.IsTrue(expensive.rendererFeatures.Any(f => f is ScreenSpaceAmbientOcclusion), "GB_Expensive keeps the ambient occlusion");
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets/_Game" }))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsFalse(data.rendererFeatures.Any(f => f != null && (f.name == "Dither" || f.name == "CharacterStencil")), data.name + ": no dither pass");
            }
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Shader>(LookTestSetup.DitherShaderPath), "the dither shader is gone");

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
