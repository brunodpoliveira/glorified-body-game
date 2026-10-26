#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The exterior look test (Bruno, 2026-10-26: "we'll likely to have exterior envs. is the method we're doing for
    /// illuminating scenes work for them fine?"). The yard of the facility in the late afternoon: the method of the
    /// rooms with a sun in place of the spots. The sun is a Mixed directional light, live and hard. The sky and the
    /// bounce bake into the lightmaps (indirect only), at a lower texel density than a room. Each fixed camera sets
    /// the sun's shadow distance (FixedCamera.shadowDistance), so the shadow map covers only what it sees. Fog for
    /// the distance. The same LookDirector dressings as the rooms (F3), and F11 cycles to it.
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.ExteriorLookSetup.BuildExterior, then BakeExterior.
    /// </summary>
    public static class ExteriorLookSetup
    {
        public const string SceneName = "LookTestExterior";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string LightingPath = LookTestSetup.RenderingFolder + "/GB_ExteriorBake.lighting";
        public const string SkyPath = LookTestSetup.MaterialsFolder + "/GB_Sky.mat";

        // The yard: 30 m along x, 20 m along z, the facade on the north side (z +10), the fence on the south (z -10).
        public const float YardWidth = 30f, YardDepth = 20f, FacadeHeight = 9f;
        public static readonly Color SunColour = new Color(1f, 0.92f, 0.8f);
        public const float SunIntensity = 2.2f;
        public static readonly Vector3 SunAngles = new Vector3(32f, -38f, 0f);

        [MenuItem("GlorifiedBody/Build Look Test Exterior (the yard)")]
        public static void BuildExterior()
        {
            Directory.CreateDirectory(MenuLoopSetup.ScenesFolder);
            LookTestSetup.EnsureTextures();
            Scene scene = LookTestSetup.BeginScene(ScenePath);
            Camera camera = MenuLoopSetup.EnsureCamera(new Vector3(0f, 4f, -9f), new Vector3(0f, 0f, 5f), 55f);
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.farClipPlane = 300f;
            LookTestSetup.Palette p = LookTestSetup.EnsurePalette();
            Material asphalt = LookTestSetup.SurfaceMaterial("GB_Asphalt", "concrete_floor", new Color(0.36f, 0.36f, 0.37f), 0.35f, new Color(0.2f, 0.2f, 0.2f));
            Material facade = LookTestSetup.SurfaceMaterial("GB_Facade", "concrete_wall", new Color(0.82f, 0.8f, 0.76f), 0.2f, new Color(0.2f, 0.2f, 0.2f));
            Material vanWhite = LookTestSetup.SurfaceMaterial("GB_VanWhite", null, new Color(0.86f, 0.86f, 0.84f), 0.35f, new Color(0.25f, 0.25f, 0.25f));
            Material rubber = LookTestSetup.SurfaceMaterial("GB_Rubber", null, new Color(0.05f, 0.05f, 0.05f), 0.3f, new Color(0.1f, 0.1f, 0.1f));

            GameObject yard = MenuLoopSetup.EnsureRoot("[Room]");
            float hw = YardWidth * 0.5f, hd = YardDepth * 0.5f;
            // The yard she walks on, and the ground the cameras see past the fence and the low wall.
            LookTestSetup.Face(yard, "Yard_Floor", Vector3.zero, Quaternion.Euler(90f, 0f, 0f), YardWidth, YardDepth, asphalt, 0.3f, tile: 3f);
            // One ground under all of it, 120 m across, 2 cm under the yard, a quarter of the yard's texels in the bake.
            GameObject apron = LookTestSetup.Face(yard, "Apron", new Vector3(0f, -0.02f, 0f), Quaternion.Euler(90f, 0f, 0f), 120f, 120f, asphalt, 0f, tile: 3f);
            var apronSo = new SerializedObject(apron.GetComponent<MeshRenderer>());
            apronSo.FindProperty("m_ScaleInLightmap").floatValue = 0.25f;
            apronSo.ApplyModifiedPropertiesWithoutUndo();
            // The facade on the north side: a loading door under a canopy, a row of dark windows, the gear panel.
            LookTestSetup.Face(yard, "Facade", new Vector3(0f, FacadeHeight * 0.5f, hd), Quaternion.identity, YardWidth, FacadeHeight, facade, 0.6f, tile: 3f);
            LookTestSetup.Block(yard, "LoadingDoor", PrimitiveType.Cube, new Vector3(4f, 2.25f, hd - 0.08f), new Vector3(4f, 4.5f, 0.14f), p.DarkSteel);
            LookTestSetup.Block(yard, "Canopy", PrimitiveType.Cube, new Vector3(4f, 5.1f, hd - 1.3f), new Vector3(6.5f, 0.3f, 2.6f), p.Steel);
            for (int i = 0; i < 7; i++)
                LookTestSetup.Face(yard, "Window_" + i, new Vector3(-12f + i * 4f, 6.6f, hd - 0.03f), Quaternion.identity, 2.2f, 1.4f, p.Glass, 0f);
            LookTestSetup.Face(yard, "Panel_Gear", new Vector3(-6f, 3.2f, hd - 0.03f), Quaternion.identity, 2f, 2f, p.Emblem, 0f, stretch: true);
            // The west wing, as high as the facade; the low wall on the east; the fence on the south.
            LookTestSetup.Face(yard, "Wing_West", new Vector3(-hw, FacadeHeight * 0.5f, 4f), Quaternion.Euler(0f, -90f, 0f), 12f, FacadeHeight, facade, 0.6f, tile: 3f);
            LookTestSetup.Block(yard, "Wall_East", PrimitiveType.Cube, new Vector3(hw, 0.6f, 0f), new Vector3(0.4f, 1.2f, YardDepth), p.Planter);
            for (int i = 0; i <= 10; i++)
            {
                float x = -hw + i * 3f;
                LookTestSetup.Block(yard, "FencePost_" + i, PrimitiveType.Cylinder, new Vector3(x, 1.2f, -hd), new Vector3(0.1f, 1.2f, 0.1f), p.DarkSteel);
                if (i < 10) LookTestSetup.Face(yard, "FenceMesh_" + i, new Vector3(x + 1.5f, 1.2f, -hd), Quaternion.Euler(0f, 180f, 0f), 3f, 2.4f, p.Grille, 0.05f, stretch: true);
            }
            // The van in the yard (the story's beat at the window), white, its back to the loading door.
            LookTestSetup.Block(yard, "Van_Body", PrimitiveType.Cube, new Vector3(-2f, 1.45f, 3f), new Vector3(2.1f, 2.1f, 4.2f), vanWhite);
            LookTestSetup.Block(yard, "Van_Cab", PrimitiveType.Cube, new Vector3(-2f, 1.15f, 0.2f), new Vector3(2f, 1.5f, 1.4f), vanWhite);
            LookTestSetup.Face(yard, "Van_Windscreen", new Vector3(-2f, 1.55f, -0.51f), Quaternion.Euler(12f, 0f, 0f), 1.8f, 0.6f, p.Glass, 0f); // leans back
            foreach (float wx in new[] { -3f, -1f })
                foreach (float wz in new[] { 0.4f, 4.3f })
                    LookTestSetup.Block(yard, $"Van_Wheel_{wx}_{wz}", PrimitiveType.Cylinder, new Vector3(wx, 0.38f, wz), new Vector3(0.76f, 0.12f, 0.76f), rubber, Quaternion.Euler(0f, 0f, 90f));
            // Concrete barriers, two planters, a dumpster, two lamp poles: cover and silhouettes in the light.
            for (int i = 0; i < 3; i++)
                LookTestSetup.Block(yard, "Barrier_" + i, PrimitiveType.Cube, new Vector3(5f + i * 2.2f, 0.45f, -4f), new Vector3(2f, 0.9f, 0.6f), p.Planter);
            foreach ((string n, float x, float z) in new[] { ("Planter_A", -9f, -5f), ("Planter_B", 10f, 5.5f) })
            {
                LookTestSetup.Block(yard, n, PrimitiveType.Cube, new Vector3(x, 0.35f, z), new Vector3(2.4f, 0.7f, 1.2f), p.Planter);
                LookTestSetup.Block(yard, n + "_Shrub", PrimitiveType.Sphere, new Vector3(x, 1.0f, z), new Vector3(2f, 0.9f, 1f), p.Shrub);
            }
            LookTestSetup.Block(yard, "Dumpster", PrimitiveType.Cube, new Vector3(-11f, 0.7f, 7.5f), new Vector3(2.2f, 1.4f, 1.3f), p.DarkSteel);
            foreach ((string n, float x, float z) in new[] { ("Pole_A", -6f, -8f), ("Pole_B", 9f, -8f) })
            {
                LookTestSetup.Block(yard, n, PrimitiveType.Cylinder, new Vector3(x, 3f, z), new Vector3(0.18f, 3f, 0.18f), p.DarkSteel);
                LookTestSetup.Block(yard, n + "_Head", PrimitiveType.Cube, new Vector3(x, 6f, z + 0.5f), new Vector3(0.4f, 0.15f, 1.1f), p.DarkSteel);
            }

            // The sun, the sky, the fog.
            GameObject sunObj = MenuLoopSetup.EnsureChild(MenuLoopSetup.EnsureRoot("[Lights]"), "Sun");
            sunObj.transform.rotation = Quaternion.Euler(SunAngles);
            Light sun = MenuLoopSetup.EnsureComponent<Light>(sunObj);
            sun.type = LightType.Directional;
            sun.color = SunColour;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = 1f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            sun.lightmapBakeType = LightmapBakeType.Mixed; // live direct light and shadow, the bounce baked
            Material sky = EnsureSky();
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.66f, 0.69f, 0.72f);
            RenderSettings.fogStartDistance = 22f;
            RenderSettings.fogEndDistance = 95f;

            // The cast: Lucy in the open, a soldier by the van, a guard at the door.
            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog();
            LookTestSetup.PlaceLucy(catalog, new Vector3(1f, 0f, -3f), 20f);
            LookTestSetup.PlaceFigure(catalog, "Figure_Soldier", "soldier", new Vector3(1f, 0f, 3f));
            LookTestSetup.PlaceFigure(catalog, "Figure_Guard", "guard", new Vector3(6f, 0f, 7.5f));

            // An open yard needs a camera for each part (the coverage test found 31 % out of frame with three): six
            // zone cameras on a 3 x 2 grid of 10 m cells, each 7 to 12 m back and 4 m up so it looks over the barriers
            // and the planters, and one from the east for the strip behind the van. Each sets the sun's shadow
            // distance to what it sees.
            GameObject camerasRoot = LookTestSetup.ResetCameras();
            FixedCameraDirector director = MenuLoopSetup.EnsureComponent<FixedCameraDirector>(camerasRoot);
            FixedCamera sw = Cam(camerasRoot, "Cam_SW", new Vector3(-10f, 4f, 7f), new Vector3(-10f, 0f, -5f), 26f);
            FixedCamera sc = Cam(camerasRoot, "Cam_SC", new Vector3(6f, 4.5f, 6f), new Vector3(-1f, 0f, -5f), 26f);
            FixedCamera se = Cam(camerasRoot, "Cam_SE", new Vector3(12f, 4f, 8f), new Vector3(10f, 0f, -5f), 26f);
            FixedCamera nw = Cam(camerasRoot, "Cam_NW", new Vector3(-10f, 4f, -9f), new Vector3(-10f, 0f, 5f), 26f);
            FixedCamera nc = Cam(camerasRoot, "Cam_NC", new Vector3(0f, 4f, -9f), new Vector3(0f, 0f, 5f), 26f);
            FixedCamera ne = Cam(camerasRoot, "Cam_NE", new Vector3(11.5f, 4f, -9.3f), new Vector3(10f, 0f, 5f), 26f);
            FixedCamera van = Cam(camerasRoot, "Cam_Van", new Vector3(12f, 3.5f, 7.5f), new Vector3(-2f, 1f, 7.5f), 20f);
            director.SetDefaultCamera(nc);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_SW", new Vector3(-10f, 1f, -5f), new Vector3(10f, 3f, 10f), sw, 1);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_SC", new Vector3(0f, 1f, -5f), new Vector3(10f, 3f, 10f), sc, 1);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_SE", new Vector3(10f, 1f, -5f), new Vector3(10f, 3f, 10f), se, 1);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_NW", new Vector3(-10f, 1f, 5f), new Vector3(10f, 3f, 10f), nw, 1);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_NC", new Vector3(0f, 1f, 5f), new Vector3(10f, 3f, 10f), nc, 1);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_NE", new Vector3(10f, 1f, 5f), new Vector3(10f, 3f, 10f), ne, 1);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_BehindVan", new Vector3(-2f, 1f, 7.55f), new Vector3(5f, 3f, 4.9f), van, 2);
            LookTestSetup.ProbeGrid(new Vector3(-hw, 0f, -hd), new Vector3(hw, 0f, hd), 3f, 0.6f, 2.2f);

            LookTestSetup.FinishScene(scene, p, new[] { sw, sc, se, nw, nc, ne, van }, "the yard, one sun");
            MenuLoopSetup.CleanBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[ExteriorLookSetup] built the yard. Next: BakeExterior.");
        }

        private static FixedCamera Cam(GameObject root, string name, Vector3 at, Vector3 lookAt, float shadowDistance)
        {
            FixedCamera cam = MenuLoopSetup.EnsureFixedCamera(root, name, at, lookAt, 55f);
            cam.shadowDistance = shadowDistance;
            return cam;
        }

        /// <summary>The bounce of the sun and the sky (minutes on the CPU lightmapper): 4 texels a metre, indirect only.</summary>
        [MenuItem("GlorifiedBody/Bake Look Test Exterior")]
        public static void BakeExterior()
        {
            LookTestSetup.Bake(ScenePath, EnsureLightingSettings());
        }

        internal static LightingSettings EnsureLightingSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            if (settings == null)
            {
                settings = new LightingSettings();
                AssetDatabase.CreateAsset(settings, LightingPath);
            }
            settings.name = "GB_ExteriorBake";
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
            settings.realtimeGI = false;
            settings.bakedGI = true;
            settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
            settings.lightmapResolution = 4f;
            settings.lightmapPadding = 2;
            settings.lightmapMaxSize = 2048;
            settings.lightmapCompression = LightmapCompression.None;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.directSampleCount = 32;
            settings.indirectSampleCount = 256;
            settings.environmentSampleCount = 128;
            settings.maxBounces = 2;
            settings.minBounces = 1;
            settings.ao = true;
            settings.aoMaxDistance = 1.5f;
            settings.aoExponentDirect = 0f;
            settings.aoExponentIndirect = 1f;
            settings.filteringMode = LightingSettings.FilterMode.Auto;
            settings.lightProbeSampleCountMultiplier = 4f;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        // A Face shows its front to -z at no rotation (the rooms' north walls use Quaternion.identity): the facade on the
        // north side faces the yard with no turn, the west wing turns -90, the fence on the south turns 180.

        /// <summary>A procedural sky: a hazy late afternoon, a small sun disc.</summary>
        internal static Material EnsureSky()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, SkyPath);
            }
            sky.shader = Shader.Find("Skybox/Procedural");
            sky.SetFloat("_SunDisk", 1f);
            sky.SetFloat("_SunSize", 0.03f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 1.15f);
            sky.SetColor("_SkyTint", new Color(0.55f, 0.6f, 0.68f));
            sky.SetColor("_GroundColor", new Color(0.3f, 0.29f, 0.27f));
            sky.SetFloat("_Exposure", 1.15f);
            EditorUtility.SetDirty(sky);
            return sky;
        }
    }
}
#endif
