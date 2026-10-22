#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Cameras;
using GlorifiedBody.Core;
using GlorifiedBody.Player;
using GlorifiedBody.Rendering;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// Build order step 4, the look test: the Gallery loads with its cast, cameras and hard spots, the three
    /// dressings switch the renderer and the materials, the plate dressing renders the room once per camera,
    /// and every dressing renders a frame that is not black. Batch: tools/unity.sh test playmode.
    /// </summary>
    public class LookTestTests
    {
        private const string MainMenu = "Main_Menu";
        private const string Room = "LookTest";

        private LookDirector look;
        private FixedCameraDirector director;
        private FixedCamera[] cameras;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainMenu);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == MainMenu && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(Room);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Room && GameManager.Instance.IsPlaying, 10f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSeconds(0.2f);
            look = LookDirector.Instance;
            director = FixedCameraDirector.Instance;
            cameras = UnityEngine.Object.FindObjectsByType<FixedCamera>(FindObjectsSortMode.None).OrderBy(c => c.cameraName).ToArray();
            Assert.IsNotNull(look, "LookDirector in the room");
            Assert.IsNotNull(director, "FixedCameraDirector in the room");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (look != null) look.PlateSizeOverride = default;
            if (director != null) director.Override(null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheGallery_Loads_WithLucyTwoFiguresThreeCamerasAndHardSpots()
        {
            yield return null;
            CollectionAssert.AreEqual(new[] { "Cam_Door", "Cam_Mid", "Cam_Wide" }, cameras.Select(c => c.cameraName).ToArray(), "three fixed cameras");
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Assert.IsNotNull(player, "Lucy");
            Assert.IsNotNull(player.GetComponent<TankController>(), "the tank controls");
            Assert.AreEqual(LayerMask.NameToLayer("Player"), player.layer, "Lucy on the Player layer (the stencil mask)");
            Assert.IsNotNull(GameObject.Find("Figure_Soldier"), "the soldier");
            Assert.IsNotNull(GameObject.Find("Figure_Guard"), "the guard");
            Assert.AreEqual(LayerMask.NameToLayer("Enemy"), GameObject.Find("Figure_Soldier").layer, "the figures on the Enemy layer");
            Assert.GreaterOrEqual(UnityEngine.Object.FindObjectsByType<Health>(FindObjectsSortMode.None).Length, 3, "Lucy and the two figures carry Health");

            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            Light[] spots = lights.Where(l => l.type == LightType.Spot).ToArray();
            Assert.GreaterOrEqual(spots.Length, 5, "the pools of light");
            Assert.IsTrue(spots.All(l => l.shadows == LightShadows.Hard), "hard shadows on every spot (ART_LESSONS 1.3)");
            Assert.IsFalse(lights.Any(l => l.type == LightType.Directional), "no sun indoors");
            Assert.Less(RenderSettings.ambientLight.maxColorComponent, 0.05f, "near-black corners: no ambient, no bounce");
            Assert.GreaterOrEqual(look.RoomRendererCount, 20, "the room surfaces the director dresses");

            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            Assert.IsNotNull(asset, "a URP asset");
            Assert.AreEqual(LookDirector.LookRenderScale, asset.renderScale, 0.01f, "the 720p render scale");
            Assert.IsFalse(asset.supportsSoftShadows, "soft shadows off");
            Assert.IsTrue(asset.supportsAdditionalLightShadows, "the spots cast shadows");
        }

        [UnityTest]
        public IEnumerator Dressings_SwitchTheRendererAndTheRoomMaterials()
        {
            Renderer floor = GameObject.Find("Floor").GetComponent<Renderer>();
            look.SetDressing(LookDressing.Greybox);
            yield return null;
            Assert.AreEqual(0, look.RendererIndex, "the greybox uses the stock renderer");
            Assert.IsFalse(look.IsDressed);
            Assert.AreEqual("GB_Greybox", floor.sharedMaterial.name, "the floor in grey");

            look.SetDressing(LookDressing.RealTime);
            yield return null;
            Assert.AreEqual(1, look.RendererIndex, "the real-time dressing uses GB_Full");
            Assert.IsTrue(look.IsDressed);
            Assert.AreEqual("GB_ConcreteFloor", floor.sharedMaterial.name, "the floor in concrete");
            Assert.IsNull(PlateBlitFeature.Plate, "no plate in the real-time dressing");

            look.SetDressing(LookDressing.Plate);
            yield return null;
            Assert.AreEqual(3, look.RendererIndex, "the plate dressing uses GB_Plate");
            Assert.IsTrue(look.IsDressed);
            Assert.IsNotNull(PlateBlitFeature.Plate, "the plate is armed");
        }

        [UnityTest]
        public IEnumerator PlateDressing_RendersTheRoomOncePerCamera_IntoAPlateWithContent()
        {
            look.PlateSizeOverride = new Vector2Int(1280, 720);
            look.SetDressing(LookDressing.Plate);
            yield return null;
            yield return null;
            Assert.IsNotNull(look.Plate, "a plate");
            Assert.AreEqual(1280, look.Plate.width, "the plate width");
            Assert.AreEqual(720, look.Plate.height, "the plate height");
            Assert.GreaterOrEqual(look.PlateCaptures, 1, "captured once on the switch");
            Assert.AreEqual(director.CurrentName, look.PlateCameraName, "the plate is of the current camera");
            Assert.AreSame(look.Plate, PlateBlitFeature.Plate, "the blit draws this plate");

            Color32[] pixels = ReadPixels(look.Plate);
            float mean = pixels.Average(p => (p.r + p.g + p.b) / (3f * 255f));
            int bright = pixels.Count(p => p.r + p.g + p.b > 3 * 40);
            int dark = pixels.Count(p => p.r + p.g + p.b < 3 * 12);
            Assert.That(mean, Is.InRange(0.02f, 0.9f), $"the plate shows the room (mean {mean:0.000})");
            Assert.Greater(bright, pixels.Length / 50, "pools of light on the plate");
            Assert.Greater(dark, pixels.Length / 50, "near-black corners on the plate");

            int before = look.PlateCaptures;
            FixedCamera door = cameras.First(c => c.cameraName == "Cam_Door");
            director.Override(door);
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(before + 1, look.PlateCaptures, "a camera cut captures the plate again");
            Assert.AreEqual("Cam_Door", look.PlateCameraName, "for the new camera");

            look.SetDressing(LookDressing.RealTime);
            yield return null;
            Assert.IsNull(PlateBlitFeature.Plate, "the plate is released by the real-time dressing");
        }

        [UnityTest]
        public IEnumerator EveryDressing_RendersAFrame_ThatIsNotBlack()
        {
            look.PlateSizeOverride = new Vector2Int(640, 360);
            foreach (LookDressing dressing in new[] { LookDressing.Greybox, LookDressing.RealTime, LookDressing.Plate })
            {
                look.SetDressing(dressing);
                yield return null;
                yield return null;
                var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
                Camera cam = Camera.main;
                RenderTexture previous = cam.targetTexture;
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = previous;
                Color32[] pixels = ReadPixels(rt);
                rt.Release();
                float mean = pixels.Average(p => (p.r + p.g + p.b) / (3f * 255f));
                int distinct = pixels.Take(20000).Select(p => (p.r / 8) * 1000 + (p.g / 8) * 32 + p.b / 8).Distinct().Count();
                Assert.That(mean, Is.InRange(0.02f, 0.9f), $"{dressing}: the frame shows the room (mean {mean:0.000})");
                Assert.Greater(distinct, 30, $"{dressing}: the frame has shading ({distinct} colour bins)");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static Color32[] ReadPixels(RenderTexture rt)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            Color32[] pixels = tex.GetPixels32();
            UnityEngine.Object.Destroy(tex);
            return pixels;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
