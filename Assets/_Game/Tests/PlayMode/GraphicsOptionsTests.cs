#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Rendering;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The graphics options (2026-11-03, Bruno: "in the graphics we should allow the usual knobs and changes that most games
    /// allow: anti-aliasing, quality textures, resolution, graphical options on/off"): the High preset is the default; each
    /// knob changes the frame (the URP asset, the quality settings, the camera); a preset sets the knobs and a knob makes
    /// it Custom; the choices persist (a batch run keeps its own keys); the shadows off hold the shadow distance at 0 past a
    /// camera cut; the options screen has the rows. In the training room. Batch: tools/unity.sh test playmode GraphicsOptionsTests.
    /// </summary>
    public class GraphicsOptionsTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GraphicsOptions.ResetToDefaults();
            GameManager.Instance.LoadLevel("TrainingRoom");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "TrainingRoom" && GameManager.Instance.IsPlaying, 10f);
            yield return new WaitForSecondsRealtime(0.3f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GraphicsOptions.ResetToDefaults();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheDefault_IsHigh_AndTheRoomDrawsWithIt()
        {
            yield return null;
            Assert.AreEqual(GraphicsPreset.High, GraphicsOptions.Preset);
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<LookQualityApplier>(), "the applier on the gameplay systems");
            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            UniversalAdditionalCameraData data = Camera.main.GetUniversalAdditionalCameraData();
            Assert.AreSame(asset.GetRenderer(LookQualityApplier.GameRendererIndex), data.scriptableRenderer, "the stock renderer");
            Assert.AreEqual(AntialiasingMode.SubpixelMorphologicalAntiAliasing, data.antialiasing, "SMAA");
            Assert.AreEqual(1, asset.msaaSampleCount, "no MSAA with SMAA");
            Assert.AreEqual(1f, asset.renderScale, 0.001f);
            Assert.AreEqual(2048, asset.mainLightShadowmapResolution, "the look's own sun map");
            Assert.AreEqual(4096, asset.additionalLightsShadowmapResolution, "the look's own spot atlas");
            Assert.AreEqual(0, QualitySettings.globalTextureMipmapLimit, "the full 2K textures");
            Assert.AreEqual(AnisotropicFiltering.ForceEnable, QualitySettings.anisotropicFiltering);
            Assert.IsFalse(data.renderPostProcessing, "no brightness volume at 0");
            Assert.IsFalse(asset.supportsSoftShadows, "the shadows stay hard (ART_LESSONS 1.3)");
        }

        [UnityTest]
        public IEnumerator EachKnob_ChangesTheFrame()
        {
            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            UniversalAdditionalCameraData data = Camera.main.GetUniversalAdditionalCameraData();

            GraphicsOptions.SetAntiAliasing(AntiAliasing.Msaa4);
            yield return null;
            Assert.AreEqual(4, asset.msaaSampleCount, "MSAA 4x");
            Assert.AreEqual(AntialiasingMode.None, data.antialiasing, "no post pass with MSAA");
            GraphicsOptions.SetAntiAliasing(AntiAliasing.Fxaa);
            Assert.AreEqual(1, asset.msaaSampleCount);
            Assert.AreEqual(AntialiasingMode.FastApproximateAntialiasing, data.antialiasing, "FXAA");

            GraphicsOptions.SetRenderScale(75);
            Assert.AreEqual(0.75f, asset.renderScale, 0.001f, "the render scale");

            GraphicsOptions.SetTextures(TextureQuality.Medium);
            Assert.AreEqual(1, QualitySettings.globalTextureMipmapLimit, "half-size textures");
            GraphicsOptions.SetFiltering(TextureFiltering.Off);
            Assert.AreEqual(AnisotropicFiltering.Disable, QualitySettings.anisotropicFiltering);

            GraphicsOptions.SetShadows(ShadowLevel.Ultra);
            Assert.AreEqual(4096, asset.mainLightShadowmapResolution);
            Assert.AreEqual(8192, asset.additionalLightsShadowmapResolution);
            GraphicsOptions.SetShadows(ShadowLevel.Off);
            Assert.AreEqual(0f, asset.shadowDistance, "no shadow at all");
            ShadowDistance.Set(25f); // a camera cut asks for its own distance
            Assert.AreEqual(0f, asset.shadowDistance, "the option holds it at 0 past a camera cut");
            GraphicsOptions.SetShadows(ShadowLevel.High);
            Assert.AreEqual(25f, asset.shadowDistance, 0.001f, "the camera's distance comes back");

            ScreenSpaceAmbientOcclusion ao = asset.rendererDataList[0].rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().FirstOrDefault();
            Assert.IsNotNull(ao, "the stock renderer has its ambient occlusion");
            GraphicsOptions.SetAmbientOcclusion(false);
            Assert.IsFalse(ao.isActive, "ambient occlusion off");
            GraphicsOptions.SetAmbientOcclusion(true);
            Assert.IsTrue(ao.isActive);

            GraphicsOptions.SetBrightness(0.5f);
            yield return null;
            Assert.IsTrue(data.renderPostProcessing, "the brightness turns post-processing on");
            Assert.AreEqual(0.5f, GraphicsOptions.AppliedExposure, 0.001f, "half a stop brighter");
            GraphicsOptions.SetBrightness(0f);
            Assert.IsFalse(data.renderPostProcessing, "and off at 0");

            GraphicsOptions.SetFrameLimit(60);
            Assert.AreEqual(60, Application.targetFrameRate);
            GraphicsOptions.SetFrameLimit(0);
            Assert.AreEqual(-1, Application.targetFrameRate, "no limit");
        }

        [UnityTest]
        public IEnumerator APreset_SetsTheKnobs_AKnobMakesItCustom_AndTheChoicesPersist()
        {
            GraphicsOptions.SetPreset(GraphicsPreset.Low);
            yield return null;
            Assert.AreEqual(AntiAliasing.Off, GraphicsOptions.AntiAliasing);
            Assert.AreEqual(67, GraphicsOptions.RenderScale);
            Assert.AreEqual(ShadowLevel.Low, GraphicsOptions.Shadows);
            Assert.IsFalse(GraphicsOptions.AmbientOcclusion);
            GraphicsOptions.SetAntiAliasing(AntiAliasing.Msaa8);
            Assert.AreEqual(GraphicsPreset.Custom, GraphicsOptions.Preset, "a knob makes it Custom");
            GraphicsOptions.SetAntiAliasing(AntiAliasing.Off);
            Assert.AreEqual(GraphicsPreset.Low, GraphicsOptions.Preset, "the knobs match Low again");

            GraphicsOptions.SetRenderScale(85);
            GraphicsOptions.SetBrightness(-0.3f);
            GraphicsOptions.Reload();
            Assert.AreEqual(GraphicsPreset.Custom, GraphicsOptions.Preset, "the saved preset");
            Assert.AreEqual(85, GraphicsOptions.RenderScale, "the saved knob");
            Assert.AreEqual(-0.3f, GraphicsOptions.Brightness, 0.001f, "the saved brightness");
            Assert.IsTrue(PlayerPrefs.HasKey("batch_gfx_preset"), "a batch run keeps its own keys");
        }

        [UnityTest]
        public IEnumerator TheOptionsScreen_HasTheGraphicsRows_AndAPresetMovesThem()
        {
            GlorifiedBody.UI.OptionsMenuUI options = UnityEngine.Object.FindFirstObjectByType<GlorifiedBody.UI.OptionsMenuUI>(FindObjectsInactive.Include);
            Assert.IsNotNull(options, "the options screen in the room");
            options.Show(null);
            yield return null;
            string[] rows = { "Display Mode", "Frame Rate Limit", "Brightness", "Quality Preset", "Render Scale", "Anti-Aliasing", "Texture Quality", "Texture Filtering", "Shadows", "Ambient Occlusion" };
            foreach (string row in rows)
                Assert.IsTrue(options.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t => t.text == row), "the row " + row);
            Assert.IsFalse(options.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t => t.text == "Rain Effects" || t.text == "Look"), "no Rain or Look row");
            UnityEngine.UI.Dropdown preset = Dropdown(options, "Quality Preset");
            UnityEngine.UI.Dropdown aa = Dropdown(options, "Anti-Aliasing");
            preset.value = (int)GraphicsPreset.Ultra;
            yield return null;
            Assert.AreEqual(GraphicsPreset.Ultra, GraphicsOptions.Preset);
            Assert.AreEqual((int)AntiAliasing.Msaa4, aa.value, "the anti-aliasing row moved with the preset");
            aa.value = (int)AntiAliasing.Smaa;
            yield return null;
            Assert.AreEqual((int)GraphicsPreset.Custom, preset.value, "the preset row shows Custom");
            options.Hide();
        }

        private static UnityEngine.UI.Dropdown Dropdown(Component root, string label)
        {
            Transform row = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DropdownRow_" + label);
            Assert.IsNotNull(row, "the row " + label);
            return row.GetComponentInChildren<UnityEngine.UI.Dropdown>(true);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
