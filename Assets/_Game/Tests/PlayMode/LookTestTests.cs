#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// Build order step 4, the look test, for each look room: the room loads with its cast, cameras and hard
    /// spots; the four dressings switch the renderer, the materials and the baked light; the plate dressing
    /// renders the room once per camera; every dressing renders a frame that is not black; the baked dressing
    /// is brighter than the real-time one (the bounce); every floor point is in the frame of the camera its
    /// zone picks (Bruno, 2026-10-22: "needs a new camera angle to cover this section").
    /// Batch: tools/unity.sh test playmode.
    /// </summary>
    public abstract class LookRoomTests
    {
        private const string MainMenu = "Main_Menu";
        protected abstract string Room { get; }
        protected abstract string[] ExpectedCameras { get; }
        protected abstract string[] ExpectedFigures { get; }
        protected abstract string FloorObject { get; }
        protected abstract string FloorDressedMaterial { get; }
        protected abstract string SecondCamera { get; }
        /// <summary>The share of floor points that may fall out of the frame of their camera.</summary>
        protected virtual float CoverageTolerance => 0.03f;
        /// <summary>A point this close to its camera (across the floor) is not checked: she fills the frame there, cut at the waist as in 1999.</summary>
        protected const float NearCamera = 3f;

        protected LookDirector look;
        protected FixedCameraDirector director;
        protected FixedCamera[] cameras;

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
        public IEnumerator TheRoom_Loads_WithLucyTheFiguresTheCamerasAndHardSpots()
        {
            yield return null;
            CollectionAssert.AreEqual(ExpectedCameras.OrderBy(n => n).ToArray(), cameras.Select(c => c.cameraName).ToArray(), "the fixed cameras");
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            Assert.IsNotNull(player, "Lucy");
            Assert.IsNotNull(player.GetComponent<TankController>(), "the tank controls");
            Assert.AreEqual(LayerMask.NameToLayer("Player"), player.layer, "Lucy on the Player layer (the stencil mask)");
            foreach (string figure in ExpectedFigures)
            {
                GameObject obj = GameObject.Find(figure);
                Assert.IsNotNull(obj, figure);
                Assert.AreEqual(LayerMask.NameToLayer("Enemy"), obj.layer, figure + " on the Enemy layer");
            }
            Assert.GreaterOrEqual(UnityEngine.Object.FindObjectsByType<Health>(FindObjectsSortMode.None).Length, 1 + ExpectedFigures.Length, "Lucy and the figures carry Health");

            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            Light[] spots = lights.Where(l => l.type == LightType.Spot).ToArray();
            Assert.GreaterOrEqual(spots.Length, 5, "the pools of light");
            Assert.IsTrue(lights.Where(l => l.type != LightType.Directional).All(l => l.shadows == LightShadows.Hard), "hard shadows on every light (ART_LESSONS 1.3)");
            Assert.IsFalse(lights.Any(l => l.type == LightType.Directional), "no sun indoors");
            Assert.Less(RenderSettings.ambientLight.maxColorComponent, 0.05f, "near-black corners: no ambient");
            Assert.GreaterOrEqual(look.RoomRendererCount, 20, "the room surfaces the director dresses");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<LightProbeGroup>(), "the light probes of the bake");

            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            Assert.IsNotNull(asset, "a URP asset");
            Assert.AreEqual(LookDirector.LookRenderScale, asset.renderScale, 0.01f, "the 720p render scale");
            Assert.IsFalse(asset.supportsSoftShadows, "soft shadows off");
            Assert.IsTrue(asset.supportsAdditionalLightShadows, "the spots cast shadows");
            Assert.AreEqual(4096, asset.additionalLightsShadowmapResolution, "the 4096 shadow atlas: every spot keeps its resolution");
        }

        [UnityTest]
        public IEnumerator Dressings_SwitchTheRendererTheRoomMaterialsAndTheBakedLight()
        {
            Renderer floor = GameObject.Find(FloorObject).GetComponent<Renderer>();
            Assert.IsTrue(look.HasBakedLight, "the scene carries lightmaps (LookTestSetup.BakeAll)");

            look.SetDressing(LookDressing.Greybox);
            yield return null;
            Assert.AreEqual(0, look.RendererIndex, "the greybox uses the stock renderer");
            Assert.IsFalse(look.IsDressed);
            Assert.IsFalse(look.BakedLightOn, "no baked light in the greybox");
            Assert.AreEqual("GB_Greybox", floor.sharedMaterial.name, "the floor in grey");

            look.SetDressing(LookDressing.RealTime);
            yield return null;
            Assert.AreEqual(1, look.RendererIndex, "the real-time dressing uses GB_Full");
            Assert.IsTrue(look.IsDressed);
            Assert.IsFalse(look.BakedLightOn, "no bounce in real time (the ruling)");
            Assert.AreEqual(0, LightmapSettings.lightmaps.Length, "the lightmaps are off");
            Assert.AreEqual(FloorDressedMaterial, floor.sharedMaterial.name, "the floor dressed");
            Assert.IsNull(PlateBlitFeature.Plate, "no plate in the real-time dressing");

            look.SetDressing(LookDressing.RealTimeBaked);
            yield return null;
            Assert.AreEqual(1, look.RendererIndex, "the baked dressing uses GB_Full too");
            Assert.IsTrue(look.BakedLightOn, "the baked light is on");
            Assert.Greater(LightmapSettings.lightmaps.Length, 0, "the lightmaps are back");
            Assert.IsTrue(floor.lightmapIndex >= 0 && floor.lightmapIndex < LightmapSettings.lightmaps.Length, "the floor reads its lightmap");

            look.SetDressing(LookDressing.Plate);
            yield return null;
            Assert.AreEqual(3, look.RendererIndex, "the plate dressing uses GB_Plate");
            Assert.IsTrue(look.IsDressed);
            Assert.IsTrue(look.BakedLightOn, "the plate carries the bounce");
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
            FixedCamera second = cameras.First(c => c.cameraName == SecondCamera);
            director.Override(second);
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(before + 1, look.PlateCaptures, "a camera cut captures the plate again");
            Assert.AreEqual(SecondCamera, look.PlateCameraName, "for the new camera");

            look.SetDressing(LookDressing.RealTime);
            yield return null;
            Assert.IsNull(PlateBlitFeature.Plate, "the plate is released by the real-time dressing");
        }

        [UnityTest]
        public IEnumerator EveryDressing_RendersAFrame_ThatIsNotBlack()
        {
            look.PlateSizeOverride = new Vector2Int(640, 360);
            foreach (LookDressing dressing in (LookDressing[])Enum.GetValues(typeof(LookDressing)))
            {
                look.SetDressing(dressing);
                yield return null;
                yield return null;
                Color32[] pixels = RenderFrame(960, 540);
                float mean = pixels.Average(p => (p.r + p.g + p.b) / (3f * 255f));
                int distinct = pixels.Take(20000).Select(p => (p.r / 8) * 1000 + (p.g / 8) * 32 + p.b / 8).Distinct().Count();
                Assert.That(mean, Is.InRange(0.02f, 0.9f), $"{dressing}: the frame shows the room (mean {mean:0.000})");
                Assert.Greater(distinct, 30, $"{dressing}: the frame has shading ({distinct} colour bins)");
            }
        }

        [UnityTest]
        public IEnumerator TheBakedDressing_IsBrighterThanRealTime_TheBounce()
        {
            Assert.IsTrue(look.HasBakedLight, "the scene carries lightmaps (LookTestSetup.BakeAll)");
            look.SetDressing(LookDressing.RealTime);
            yield return null;
            yield return null;
            float realTime = RenderFrame(960, 540).Average(p => (p.r + p.g + p.b) / (3f * 255f));
            look.SetDressing(LookDressing.RealTimeBaked);
            yield return null;
            yield return null;
            float baked = RenderFrame(960, 540).Average(p => (p.r + p.g + p.b) / (3f * 255f));
            Assert.Greater(baked, realTime * 1.02f, $"the bounce adds light: real time {realTime:0.000}, baked {baked:0.000}");
        }

        [UnityTest]
        public IEnumerator EveryFloorPoint_IsInTheFrameOfItsCamera()
        {
            look.SetDressing(LookDressing.Greybox);
            yield return null;
            int roomLayers = 1 << LayerMask.NameToLayer("Default");
            CameraZone[] zones = UnityEngine.Object.FindObjectsByType<CameraZone>(FindObjectsSortMode.None);
            float aspect = 16f / 9f;
            var uncovered = new List<string>();
            int total = 0;
            foreach (RoomFace floor in UnityEngine.Object.FindObjectsByType<RoomFace>(FindObjectsSortMode.None).Where(f => f.name.EndsWith("Floor")))
            {
                // A floor face lies flat: its local x runs along the world x, its local y along the world z.
                Vector3 c = floor.transform.position;
                for (float dx = -floor.width * 0.5f + 0.45f; dx <= floor.width * 0.5f - 0.45f; dx += 0.5f)
                {
                    for (float dz = -floor.height * 0.5f + 0.45f; dz <= floor.height * 0.5f - 0.45f; dz += 0.5f)
                    {
                        Vector3 p = new Vector3(c.x + dx, 0f, c.z + dz);
                        // A point inside a planter, a column or a bed is not a place she can stand. The camera
                        // zones are trigger boxes on the same layer: both queries must ignore triggers.
                        if (Physics.CheckBox(p + Vector3.up * 0.9f, new Vector3(0.2f, 0.8f, 0.2f), Quaternion.identity, roomLayers, QueryTriggerInteraction.Ignore)) continue;
                        total++;
                        FixedCamera cam = CameraFor(p, zones);
                        if (cam == null) { uncovered.Add($"{p} no camera"); continue; }
                        Vector3 camPos = cam.transform.position;
                        if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(camPos.x, camPos.z)) < NearCamera) continue;
                        Vector3 head = p + Vector3.up * 1.6f, hip = p + Vector3.up * 0.9f;
                        bool inFrame = InFrame(cam, head, aspect, 0.05f) && InFrame(cam, hip, aspect, 0f);
                        bool hidden = Physics.Linecast(camPos, head, roomLayers, QueryTriggerInteraction.Ignore);
                        if (!inFrame || hidden) uncovered.Add($"{p.x:0.0},{p.z:0.0} {cam.cameraName} {(inFrame ? "hidden" : "out of frame")}");
                    }
                }
            }
            Assert.Greater(total, 50, "floor points");
            var sb = new StringBuilder();
            sb.Append($"{uncovered.Count} of {total} floor points out of their camera's frame:");
            foreach (string line in uncovered.Take(25)) sb.Append("\n  ").Append(line);
            Debug.Log("[LookTestTests] " + Room + ": " + sb);
            Assert.LessOrEqual(uncovered.Count, total * CoverageTolerance, sb.ToString());
        }

        // ── Helpers ───────────────────────────────────────────────────

        private FixedCamera CameraFor(Vector3 p, CameraZone[] zones)
        {
            CameraZone best = null;
            foreach (CameraZone zone in zones)
            {
                if (zone.fixedCamera == null || !zone.Contains(p + Vector3.up)) continue;
                if (best == null || zone.priority >= best.priority) best = zone;
            }
            return best != null ? best.fixedCamera : director.DefaultCamera;
        }

        private static bool InFrame(FixedCamera cam, Vector3 point, float aspect, float margin)
        {
            Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(cam.transform.position, cam.transform.rotation, Vector3.one).inverse;
            Matrix4x4 projection = Matrix4x4.Perspective(cam.fieldOfView, aspect, 0.1f, 100f);
            Vector4 clip = projection * view * new Vector4(point.x, point.y, point.z, 1f);
            if (clip.w <= 0.01f) return false;
            float x = clip.x / clip.w, y = clip.y / clip.w;
            return Mathf.Abs(x) <= 1f - margin && Mathf.Abs(y) <= 1f - margin;
        }

        private static Color32[] RenderFrame(int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Camera cam = Camera.main;
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previous;
            Color32[] pixels = ReadPixels(rt);
            rt.Release();
            return pixels;
        }

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

    /// <summary>The Gallery of floor 1 (LookTest).</summary>
    public class LookTestGalleryTests : LookRoomTests
    {
        protected override string Room => "LookTest";
        protected override string[] ExpectedCameras => new[] { "Cam_Wide", "Cam_Corner", "Cam_Mid", "Cam_Door" };
        protected override string[] ExpectedFigures => new[] { "Figure_Soldier", "Figure_Guard" };
        protected override string FloorObject => "Floor";
        protected override string FloorDressedMaterial => "GB_ConcreteFloor";
        protected override string SecondCamera => "Cam_Door";
    }

    /// <summary>The skylight corridor, the recovery room and the therapy gym (LookTest2).</summary>
    public class LookTest2Tests : LookRoomTests
    {
        protected override string Room => "LookTest2";
        protected override string[] ExpectedCameras => new[] { "Cam_Corridor_S", "Cam_Corridor_N", "Cam_Corridor_Door", "Cam_Recovery", "Cam_Recovery_Bed", "Cam_Gym", "Cam_Gym_Bars" };
        protected override string[] ExpectedFigures => new[] { "Figure_Guard", "Figure_LabWorker", "Figure_Patient" };
        protected override string FloorObject => "Recovery_Floor";
        protected override string FloorDressedMaterial => "GB_TileFloor";
        protected override string SecondCamera => "Cam_Gym";
    }
}
#endif
