#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
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
    /// The LOOK option (Bruno, 2026-10-22): Authentic puts the dither renderer on the room's camera with no
    /// anti-aliasing, Clear puts the stock renderer with MSAA 4x and SMAA, the choice persists, and the
    /// applier follows a change while the room is open. Batch: tools/unity.sh test playmode.
    /// </summary>
    public class LookQualityTests
    {
        private const string MainMenu = "Main_Menu";
        private const string Room = "TrainingRoom";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainMenu);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == MainMenu && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            SettingsManager.Instance.LookQuality = LookQuality.Authentic;
            GameManager.Instance.LoadLevel(Room);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Room && GameManager.Instance.IsPlaying, 10f);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // The batch runs share PlayerPrefs with Bruno's editor: leave the default behind.
            if (SettingsManager.Instance != null) SettingsManager.Instance.LookQuality = LookQuality.Authentic;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Authentic_UsesTheDitherRenderer_WithoutAntiAliasing()
        {
            yield return null;
            Assert.AreEqual(LookQuality.Authentic, SettingsManager.Instance.LookQuality);
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<LookQualityApplier>(), "the applier on the gameplay systems");
            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            UniversalAdditionalCameraData data = Camera.main.GetUniversalAdditionalCameraData();
            Assert.AreSame(asset.GetRenderer(LookQualityApplier.AuthenticRendererIndex), data.scriptableRenderer, "GB_Full with the dither");
            Assert.AreEqual(1, asset.msaaSampleCount, "no MSAA");
            Assert.AreEqual(AntialiasingMode.None, data.antialiasing, "no anti-aliasing: the authentic frame");
            Assert.AreEqual(LayerMask.NameToLayer("Player"), GameObject.FindGameObjectWithTag("Player").layer, "Lucy on the Player layer: the dither skips her");
            Assert.AreEqual(LayerMask.NameToLayer("Enemy"), GameObject.Find("Dummy_A").layer, "the dummies on the Enemy layer");
        }

        [UnityTest]
        public IEnumerator Clear_UsesTheStockRenderer_WithMsaaAndSmaa_AndPersists()
        {
            SettingsManager.Instance.LookQuality = LookQuality.Clear;
            yield return null;
            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            UniversalAdditionalCameraData data = Camera.main.GetUniversalAdditionalCameraData();
            Assert.AreSame(asset.GetRenderer(LookQualityApplier.ClearRendererIndex), data.scriptableRenderer, "the stock renderer: no dither");
            Assert.AreEqual(LookQualityApplier.ClearMsaa, asset.msaaSampleCount, "MSAA 4x");
            Assert.AreEqual(AntialiasingMode.SubpixelMorphologicalAntiAliasing, data.antialiasing, "SMAA");
            Assert.AreEqual(AntialiasingQuality.High, data.antialiasingQuality);
            Assert.AreEqual((int)LookQuality.Clear, PlayerPrefs.GetInt("settings_lookQuality", -1), "the choice is saved");

            SettingsManager.Instance.LookQuality = LookQuality.Authentic;
            yield return null;
            Assert.AreSame(asset.GetRenderer(LookQualityApplier.AuthenticRendererIndex), data.scriptableRenderer, "back to the dither");
            Assert.AreEqual(1, asset.msaaSampleCount);
            Assert.AreEqual(AntialiasingMode.None, data.antialiasing);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
