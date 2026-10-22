#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Tutorial;
using GlorifiedBody.UI;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The tutorial scene (Bruno, 2026-10-22: "tutorial mode, since it'll allow us to add the other features
    /// and test them"): the therapy gym of LookTest2 with its door shut, Lucy at the door, four padded targets
    /// on the floor (one with a gun), a capsule by the bars, three fixed cameras, the systems, the prompt and
    /// the TutorialDirector with its steps. Dressed and lit like the look rooms (the baked bounce: BakeTutorial).
    /// Menu: GlorifiedBody > Build Tutorial. Batch: tools/unity.sh exec GlorifiedBody.Editor.TutorialSetup.BuildTutorial
    /// then tools/unity.sh exec GlorifiedBody.Editor.TutorialSetup.BakeTutorial
    /// </summary>
    public static class TutorialSetup
    {
        public const string SceneName = TutorialDirector.SceneName;
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        // The points sit west of the bars, so no walk crosses a rail: the door, the south end of the bars, the
        // monitors, the capsule. Lucy starts 3.5 m from the bars point (the first pass started her inside it).
        public static readonly Vector3 LucyStart = new Vector3(0.3f, 0f, 11f); // 3 m from the door camera: in its frame
        public static readonly Vector3 BarsPoint = new Vector3(-1.8f, 0f, 11.2f);
        public static readonly Vector3 MonitorsPoint = new Vector3(-2.4f, 0f, 16f);
        public static readonly Vector3 CapsulePoint = new Vector3(-2.6f, 0f, 13.5f);
        public static readonly Vector3 LogPoint = new Vector3(-2.9f, 0f, 14.6f);   // where she stands to take the log
        public static readonly Vector3 LogStand = new Vector3(-3.72f, 0f, 14.6f);  // the stand against the west wall
        public const string ItemsFolder = MenuLoopSetup.ResourcesFolder + "/Items";
        public const string TestLogPath = ItemsFolder + "/item_test_log.asset";
        public const string ScanFolder = "Assets/_Game/Scan";
        public const string ScanImagePath = ScanFolder + "/scan_placeholder_image.png";
        public const string ScanVideoPath = ScanFolder + "/scan_placeholder_video.webm";
        public const float ReachRadius = 1.3f;
        public const float TargetHealth = 150f; // padded: it takes the whole demonstration
        public static readonly string[] TargetNames = { "Target_1", "Target_2", "Target_3", "Target_Gun" };

        [MenuItem("GlorifiedBody/Build Tutorial")]
        public static void BuildTutorial()
        {
            Directory.CreateDirectory(MenuLoopSetup.ScenesFolder);
            AssetDatabase.Refresh();
            LookTestSetup.EnsureLightingSettings();
            Scene scene = LookTestSetup.BeginScene(ScenePath);
            Camera camera = MenuLoopSetup.EnsureCamera(new Vector3(3.3f, 1.7f, 10.7f), new Vector3(-1.6f, 1f, 15.5f), 62f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            LookTestSetup.Palette p = LookTestSetup.EnsurePalette();

            GameObject room = MenuLoopSetup.EnsureRoot("[Room]");
            GameObject lights = MenuLoopSetup.EnsureRoot("[Lights]");
            LookTestSetup.BuildTherapyGym(room, lights, p, corridorOpening: false);
            LookTestSetup.ProbeGrid(LookTestSetup.GymMin, LookTestSetup.GymMax, 1.5f, 0.6f, 2.4f);

            // Lucy at the door, the lock marker on (she must see what the lock holds).
            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog();
            MenuLoopSetup.EnsureLucy(catalog, LucyStart, 0f);

            // The padded targets on the floor east of the lane, the one with the gun by the door.
            // A row across the middle of the gym, 3 m or more from every lens and off the lines from the cameras
            // to the step points (the first places hid Lucy from the north-east camera).
            PaddedTarget("Target_1", new Vector3(0.8f, 0f, 13.6f), 180f, p, gun: false);
            PaddedTarget("Target_2", new Vector3(1.8f, 0f, 13.6f), 180f, p, gun: false);
            PaddedTarget("Target_3", new Vector3(2.8f, 0f, 13.6f), 180f, p, gun: false);
            // The gun against the west wall by the door: a clear line to the monitors side, no padded target in
            // the way, and no floor behind it for her to vanish into.
            PaddedTarget("Target_Gun", new Vector3(-3.65f, 0f, 11.6f), 30f, p, gun: true);
            MenuLoopSetup.EnsurePickup("Capsule", DrugType.Nalcon, CapsulePoint + Vector3.up * 0.4f);

            // The test log on a steel stand against the west wall, and the two scan targets: the door (a
            // picture) and the wall of monitors (a clip). Placeholders (Bruno, 2026-10-22).
            ItemDefinition log = EnsureTestLog();
            GameObject stand = LookTestSetup.Block(room, "Log_Stand", PrimitiveType.Cube, LogStand + new Vector3(0f, 0.45f, 0f), new Vector3(0.4f, 0.9f, 0.5f), p.DarkSteel);
            GameObject logObject = LookTestSetup.Block(room, "Log_Clipboard", PrimitiveType.Cube, LogStand + new Vector3(0f, 0.92f, 0f), new Vector3(0.24f, 0.03f, 0.32f), p.Mattress, Quaternion.Euler(0f, 8f, 0f));
            logObject.isStatic = false;
            ItemPickup pickup = MenuLoopSetup.EnsureComponent<ItemPickup>(logObject);
            pickup.item = log;
            pickup.range = 1.6f;
            Texture2D scanImage = EnsureScanImage();
            VideoClip scanClip = AssetDatabase.LoadAssetAtPath<VideoClip>(ScanVideoPath);
            if (scanClip == null) Debug.LogError($"[TutorialSetup] no clip at {ScanVideoPath}.");
            ScanTarget("Scan_Door", new Vector3(0f, 1.2f, LookTestSetup.GymMin.z + 0.1f), "gym_door", "PLACEHOLDER: what opens this door.", scanImage, null);
            ScanTarget("Scan_Monitors", new Vector3(-2.2f, 1.3f, LookTestSetup.GymMax.z - 0.55f), "gym_monitors", "PLACEHOLDER: what the monitors saw.", null, scanClip);

            // Cameras (ART_LESSONS 1.6), from the coverage check with the near skip at 1.2 m (the first layout
            // put a camera 1.2 m from the monitors point: Lucy out of frame where the run step ends): the door
            // corner (the default), the corner over the rack looking back across the room (the north band:
            // the monitors, the mirror, the bars' far end), the west wall looking east (the strip by the door).
            GameObject camerasRoot = MenuLoopSetup.EnsureRoot("[Cameras]");
            foreach (string old in new[] { "Cam_Gym_Bars", "Cam_Gym_Rack", "Zone_Gym_Bars", "Zone_Gym_Rack", "ShotPoint_Cam_Gym_Bars", "ShotPoint_Cam_Gym_Rack" })
            {
                Transform child = camerasRoot.transform.Find(old);
                if (child != null) Object.DestroyImmediate(child.gameObject);
                MenuLoopSetup.RemoveObject(old);
            }
            FixedCameraDirector director = MenuLoopSetup.EnsureComponent<FixedCameraDirector>(camerasRoot);
            FixedCamera gym = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Gym", new Vector3(3.3f, 1.7f, 10.7f), new Vector3(-1.6f, 1f, 15.5f), 62f);
            FixedCamera north = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Gym_NE", new Vector3(3.6f, 1.7f, 16.9f), new Vector3(-2f, 1f, 12.5f), 62f);
            FixedCamera west = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Gym_W", new Vector3(-3.7f, 1.7f, 13.2f), new Vector3(3f, 1f, 11.5f), 62f);
            director.SetDefaultCamera(gym);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Gym_North", new Vector3(0f, 1.5f, 15.4f), new Vector3(8f, 3f, 3.8f), north, 1);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Gym_DoorStrip", new Vector3(3f, 1.5f, 11.9f), new Vector3(2f, 3f, 3.2f), west, 1);
            MenuLoopSetup.EnsureShotPoints(new[] { gym, north, west });

            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();
            MenuLoopSetup.EnsureComponent<TutorialPromptUI>(MenuLoopSetup.EnsureRoot("[RuntimeUI]"));

            GameObject probesRoot = MenuLoopSetup.EnsureRoot("[Probes]");
            LightProbeGroup probes = MenuLoopSetup.EnsureComponent<LightProbeGroup>(probesRoot);
            probes.probePositions = LookTestSetup.probePositions.ToArray();
            probes.dering = true;

            GameObject tutorialRoot = MenuLoopSetup.EnsureRoot("[Tutorial]");
            TutorialDirector tutorial = MenuLoopSetup.EnsureComponent<TutorialDirector>(tutorialRoot);
            var so = new SerializedObject(tutorial);
            so.FindProperty("barsPoint").vector3Value = BarsPoint;
            so.FindProperty("monitorsPoint").vector3Value = MonitorsPoint;
            so.FindProperty("capsulePoint").vector3Value = CapsulePoint;
            so.FindProperty("logPoint").vector3Value = LogPoint;
            so.FindProperty("logItemId").stringValue = "test_log";
            so.FindProperty("doorScanId").stringValue = "gym_door";
            so.FindProperty("monitorsScanId").stringValue = "gym_monitors";
            so.FindProperty("reachRadius").floatValue = ReachRadius;
            so.FindProperty("shooterTargetName").stringValue = "Target_Gun";
            so.ApplyModifiedPropertiesWithoutUndo();

            MenuLoopSetup.CleanBuildSettings();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TutorialSetup] {ScenePath}: the therapy gym, four targets, three cameras, the director. Next: BakeTutorial.");
        }

        /// <summary>The placeholder log (Bruno, 2026-10-22: "add a placeholder test log"). The writer gives the words later.</summary>
        internal static ItemDefinition EnsureTestLog()
        {
            Directory.CreateDirectory(ItemsFolder);
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(TestLogPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(item, TestLogPath);
            }
            item.itemId = "test_log";
            item.displayName = "TEST LOG";
            item.kind = ItemKind.Log;
            item.description = "A placeholder record on a clipboard.";
            item.logText = "PLACEHOLDER LOG\n\nSession 0. Subject L.\n\nThis page stands in for the logs of the demo: the whiteboard, the orientation program, the tape. "
                + "The writer gives the words later.\n\nIt proves the Interact key: she faces it, the game asks, she takes it, it opens here, and it stays in her inventory.";
            EditorUtility.SetDirty(item);
            return item;
        }

        /// <summary>The placeholder picture of the scan, imported as a plain 1280 x 720 UI texture.</summary>
        internal static Texture2D EnsureScanImage()
        {
            var importer = AssetImporter.GetAtPath(ScanImagePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = false;
                importer.sRGBTexture = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            Texture2D image = AssetDatabase.LoadAssetAtPath<Texture2D>(ScanImagePath);
            if (image == null) Debug.LogError($"[TutorialSetup] no picture at {ScanImagePath}.");
            return image;
        }

        private static void ScanTarget(string name, Vector3 position, string scanId, string caption, Texture2D image, VideoClip clip)
        {
            GameObject obj = MenuLoopSetup.EnsureRoot(name);
            obj.transform.SetPositionAndRotation(position, Quaternion.identity);
            Scannable target = MenuLoopSetup.EnsureComponent<Scannable>(obj);
            target.scanId = scanId;
            target.caption = caption;
            target.image = image;
            target.clip = clip;
            target.range = 3f;
            target.focus = null;
        }

        [MenuItem("GlorifiedBody/Bake Tutorial (the bounce light)")]
        public static void BakeTutorial() => LookTestSetup.Bake(ScenePath);

        /// <summary>
        /// A padded training dummy on a stand: the root carries the capsule, Health, the lock point and the
        /// Enemy layer and tag, so every power lands on it; the body is canvas and a vest from primitives (no
        /// colliders of their own). With a gun it carries a DummyShooter, off until its step.
        /// </summary>
        private static GameObject PaddedTarget(string name, Vector3 position, float yaw, LookTestSetup.Palette p, bool gun)
        {
            GameObject root = MenuLoopSetup.EnsureRoot(name);
            root.tag = "Enemy";
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            root.transform.localScale = Vector3.one;
            root.isStatic = false;
            CapsuleCollider capsule = MenuLoopSetup.EnsureComponent<CapsuleCollider>(root);
            capsule.isTrigger = false;
            capsule.height = 1.9f;
            capsule.radius = 0.32f;
            capsule.center = new Vector3(0f, 0.95f, 0f);
            Health health = MenuLoopSetup.EnsureComponent<Health>(root);
            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("maxHealth").floatValue = TargetHealth;
            healthSo.ApplyModifiedPropertiesWithoutUndo();
            LockOnTargetable target = MenuLoopSetup.EnsureComponent<LockOnTargetable>(root);
            var targetSo = new SerializedObject(target);
            targetSo.FindProperty("fallbackHeight").floatValue = Body.ChestHeight;
            targetSo.ApplyModifiedPropertiesWithoutUndo();

            Part(root, "Stand", PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0f), new Vector3(0.5f, 0.015f, 0.5f), p.DarkSteel);
            Part(root, "Post", PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f), new Vector3(0.08f, 0.3f, 0.08f), p.DarkSteel);
            Part(root, "Body", PrimitiveType.Capsule, new Vector3(0f, 1.05f, 0f), new Vector3(0.44f, 0.5f, 0.32f), p.Canvas);
            Part(root, "Vest", PrimitiveType.Cube, new Vector3(0f, 1.15f, 0f), new Vector3(0.46f, 0.45f, 0.28f), p.Vest);
            Part(root, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.72f, 0f), new Vector3(0.24f, 0.28f, 0.24f), p.Canvas);
            Part(root, "Arm_L", PrimitiveType.Cylinder, new Vector3(-0.3f, 1.1f, 0f), new Vector3(0.1f, 0.28f, 0.1f), p.Canvas);
            Part(root, "Arm_R", PrimitiveType.Cylinder, new Vector3(0.3f, 1.1f, 0f), new Vector3(0.1f, 0.28f, 0.1f), p.Canvas);
            Transform oldGun = root.transform.Find("Gun");
            if (gun)
            {
                Part(root, "Gun", PrimitiveType.Cube, new Vector3(0.3f, 1.1f, 0.28f), new Vector3(0.06f, 0.07f, 0.36f), p.DarkSteel);
                DummyShooter shooter = MenuLoopSetup.EnsureComponent<DummyShooter>(root);
                shooter.enabled = false;
            }
            else
            {
                if (oldGun != null) Object.DestroyImmediate(oldGun.gameObject);
                MenuLoopSetup.RemoveComponent<DummyShooter>(root);
            }
            MenuLoopSetup.SetLayer(root, "Enemy");
            return root;
        }

        private static void Part(GameObject root, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject part = MenuLoopSetup.EnsureChildPrimitive(root, name, type, localPosition, localScale);
            part.transform.localRotation = Quaternion.identity;
            foreach (Collider collider in part.GetComponents<Collider>()) Object.DestroyImmediate(collider);
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
#endif
