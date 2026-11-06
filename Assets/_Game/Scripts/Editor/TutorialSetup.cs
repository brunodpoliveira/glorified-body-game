#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Story;
using GlorifiedBody.Tutorial;
using GlorifiedBody.UI;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The tutorial scene (Bruno, 2026-10-22: "tutorial mode, since it'll allow us to add the other features
    /// and test them"; 2026-11-06: "redo the tutorial room to be fully greyboxed and include the new skills/habilities/interactions").
    /// A gym in grey boxes with its door shut, Lucy at the door, four padded targets (one with a gun), a capsule by the bars, and north
    /// of it, through a fire door, an annex with a box, a plate, a locker, a lab worker, a guard and a switch. No textures and no bake.
    /// Menu: GlorifiedBody > Build Tutorial. Batch: tools/unity.sh exec GlorifiedBody.Editor.TutorialSetup.BuildTutorial
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
        public static readonly Vector3 KeyPoint = new Vector3(-2.9f, 0f, 15.4f);   // where she stands to take the key
        public static readonly Vector3 KeyStand = new Vector3(-3.72f, 0f, 15.4f);
        public static readonly Vector3 CratePoint = new Vector3(0.4f, 0f, 15.4f);
        public const string CoverName = "Barricade_Gym";
        public static readonly Vector3 CoverPoint = new Vector3(2.8f, 0f, 12.7f);
        public static readonly Vector3 DoorPoint = new Vector3(-1.6f, 0f, 10.9f);  // where she stands to scan and open the door
        public static readonly Vector3 DoorFocus = new Vector3(-0.45f, 1.2f, 10.45f); // the west half of the door leaf
        public const string ItemsFolder = MenuLoopSetup.ResourcesFolder + "/Items";
        public const string TestLogPath = ItemsFolder + "/item_test_log.asset";
        public const string ScanFolder = "Assets/_Game/Scan";
        public const string ScanImagePath = ScanFolder + "/scan_placeholder_image.png";
        public const string ScanVideoPath = ScanFolder + "/scan_placeholder_video.webm";
        public const float ReachRadius = 1.3f;
        public const float TargetHealth = 400f; // padded: it takes the whole demonstration, the MK boost doubles every hit
        public static readonly string[] TargetNames = { "Target_1", "Target_2", "Target_3", "Target_Gun" };

        // ── The annex (2026-11-06) ────────────────────────────────
        // North of the gym, through a fire door: the box over the rail, the locker behind the gate, the far side with a lab worker
        // and a switch, and in the east room a guard.
        public static readonly Vector3 FireDoorMouth = new Vector3(2.5f, 0f, 17.45f);
        public const float AnnexMinX = -6f, AnnexMaxX = 10f, AnnexMinZ = 17.6f, AnnexMaxZ = 29.6f, AnnexHeight = 3.5f;
        public const float RailZ = 24f, RailHeight = 1.2f, GateX = 6f, GateZ = 20.8f;
        public const string PlateFlag = "tutorial_plate", SwitchFlag = "tutorial_switch";
        public static readonly Vector3 BoxPoint = new Vector3(-3f, 0f, 21.5f);
        public static readonly Vector3 PlatePoint = new Vector3(-3f, 0f, 26f);
        public static readonly Vector3 WorkerPoint = new Vector3(3f, 0f, 26.2f);
        public static readonly Vector3 SwitchPoint = new Vector3(8f, 0f, 28.6f);
        public static readonly Vector3 GuardPoint = new Vector3(8.4f, 0f, 22.6f);
        public static readonly Vector3 LockerPoint = new Vector3(8.4f, 0f, 18.2f);

        /// <summary>The grey of the room: no texture, no bake (Bruno, 2026-11-06: "fully greyboxed").</summary>
        private static LookTestSetup.Palette GreyPalette()
        {
            Material wall = Demo.DemoRoom.Grey(0.42f), floor = Demo.DemoRoom.Grey(0.3f), dark = Demo.DemoRoom.Grey(0.2f), light = Demo.DemoRoom.Grey(0.62f);
            return new LookTestSetup.Palette
            {
                TileWall = wall, TileFloor = floor, Ceiling = dark, Steel = light, DarkSteel = dark, Emblem = dark, Canvas = light, Vest = Demo.DemoRoom.Grey(0.36f),
            };
        }

        private static GameObject Grey(GameObject parent, string name, Vector3 centre, Vector3 size, float shade)
        {
            GameObject box = RoomKit.Box(parent, name, centre, size);
            box.GetComponent<Renderer>().sharedMaterial = Demo.DemoRoom.Grey(shade);
            return box;
        }

        [MenuItem("GlorifiedBody/Build Tutorial")]
        public static void BuildTutorial()
        {
            Directory.CreateDirectory(MenuLoopSetup.ScenesFolder);
            AssetDatabase.Refresh();
            if (Directory.Exists(MenuLoopSetup.ScenesFolder + "/" + SceneName)) AssetDatabase.DeleteAsset(MenuLoopSetup.ScenesFolder + "/" + SceneName); // the old bake
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog(); // after the scene opens
            Camera camera = MenuLoopSetup.EnsureCamera(new Vector3(3.3f, 1.7f, 10.7f), new Vector3(-1.6f, 1f, 15.5f), 62f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.32f, 0.35f);
            RenderSettings.fog = false;
            MenuLoopSetup.EnsureLight();
            LookTestSetup.Palette p = GreyPalette();
            ItemAssets.CreateAll();

            GameObject room = MenuLoopSetup.EnsureRoot("[Room]");
            GameObject lights = MenuLoopSetup.EnsureRoot("[Lights]");
            BuildGreyGym(room);
            BuildLockedDoorAndCloset(room, lights, p);
            BuildAnnex(room);

            MenuLoopSetup.EnsureLucy(catalog, LucyStart, 0f);

            // A row across the middle of the gym, 3 m or more from every lens and off the lines from the cameras
            // to the step points (the first places hid Lucy from the north-east camera).
            PaddedTarget("Target_1", new Vector3(0.8f, 0f, 13.6f), 180f, p, gun: false);
            GameObject shieldDummy = PaddedTarget("Target_2", new Vector3(1.8f, 0f, 13.6f), 180f, p, gun: false);
            ShieldFor(shieldDummy, p);
            PaddedTarget("Target_3", new Vector3(2.8f, 0f, 13.6f), 180f, p, gun: false);
            // The cover step (2026-10-24): a chest-high barricade south of Target_3, off until its step (it would
            // stop the earlier faculties), and a breakable lamp over it.
            GameObject breakables = MenuLoopSetup.EnsureRoot("[Breakables]");
            Destruction.BreakableAssets.Barricade(breakables, CoverName, CoverPoint, 0f).gameObject.SetActive(false);
            Destruction.BreakableAssets.Lamp(breakables, "Lamp_Gym", new Vector3(2.6f, LookTestSetup.GymMax.y, 12.4f), new Color(1f, 0.95f, 0.85f), 5f, 80f, 5f);
            // The gun against the west wall by the door: a clear line to the monitors side, no padded target in
            // the way, and no floor behind it for her to vanish into.
            PaddedTarget("Target_Gun", new Vector3(-3.65f, 0f, 11.6f), 30f, p, gun: true);
            MenuLoopSetup.EnsurePickup("Capsule", DrugType.Nalcon, CapsulePoint + Vector3.up * 0.4f);
            // The crate for the prop step: between the bars and the first target, off every walk (the walks keep
            // west of the bars), clear of the camera lines to the step points.
            PropAssets.Crate("Prop_Crate", CratePoint, 15f).GetComponent<Renderer>().sharedMaterial = Demo.DemoRoom.Grey(0.62f);

            // The test log and the test key on two stands against the west wall, unlit so they stand out
            // (Bruno, 2026-10-22, after Resident Evil 4), and the two scan targets: the door (a picture, seen from
            // close by its west half) and the wall of monitors (a clip). Placeholders.
            Grey(room, "Log_Stand", LogStand + new Vector3(0f, 0.45f, 0f), new Vector3(0.4f, 0.9f, 0.5f), 0.2f);
            ItemAssets.Pickup(null, "Log_Clipboard", ItemAssets.TestLog(), PrimitiveType.Cube, LogStand + new Vector3(0f, 0.93f, 0f), new Vector3(0.24f, 0.03f, 0.32f), Quaternion.identity);
            Grey(room, "Key_Stand", KeyStand + new Vector3(0f, 0.45f, 0f), new Vector3(0.4f, 0.9f, 0.4f), 0.2f);
            ItemAssets.Pickup(null, "Test_Key", ItemAssets.TestKey(), PrimitiveType.Cube, KeyStand + new Vector3(0f, 0.93f, 0f), new Vector3(0.1f, 0.03f, 0.2f), Quaternion.identity);
            Texture2D scanImage = EnsureScanImage();
            AssetDatabase.ImportAsset(ScanVideoPath);
            VideoClip scanClip = AssetDatabase.LoadAssetAtPath<VideoClip>(ScanVideoPath);
            if (scanClip == null) Debug.LogError($"[TutorialSetup] no clip at {ScanVideoPath}.");
            ScanTarget("Scan_Door", DoorFocus, "gym_door", "PLACEHOLDER: what opens this door.", scanImage, null);
            ScanTarget("Scan_Monitors", new Vector3(-2.2f, 1.3f, LookTestSetup.GymMax.z - 0.55f), "gym_monitors", "PLACEHOLDER: what the monitors saw.", null, scanClip);

            // Cameras (ART_LESSONS 1.6), from the coverage check with the near skip at 1.2 m: the door corner (the default),
            // the corner over the rack looking back across the room (the north band), the west wall looking east (the strip by
            // the door), the closet, and one high over the annex.
            GameObject camerasRoot = MenuLoopSetup.EnsureRoot("[Cameras]");
            FixedCameraDirector director = MenuLoopSetup.EnsureComponent<FixedCameraDirector>(camerasRoot);
            FixedCamera gym = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Gym", new Vector3(3.3f, 1.7f, 10.7f), new Vector3(-1.6f, 1f, 15.5f), 62f);
            FixedCamera north = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Gym_NE", new Vector3(3.6f, 1.7f, 16.9f), new Vector3(-0.5f, 1f, 12.5f), 62f);
            FixedCamera west = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Gym_W", new Vector3(-3.7f, 1.7f, 13.2f), new Vector3(3f, 1f, 11.5f), 62f);
            director.SetDefaultCamera(gym);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Gym_North", new Vector3(0f, 1.5f, 15.4f), new Vector3(8f, 3f, 3.8f), north, 1);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Gym_DoorStrip", new Vector3(3f, 1.5f, 11.9f), new Vector3(2f, 3f, 3.2f), west, 1);
            // The closet behind the locked door: its own camera in the far corner, looking back at the doorway.
            FixedCamera closet = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Closet", new Vector3(0.7f, 1.9f, 8.6f), new Vector3(-0.4f, 0.7f, 10.2f), 70f);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Closet", new Vector3(0f, 1.5f, 9.3f), new Vector3(1.8f, 3f, 1.9f), closet, 5);
            FixedCamera annex = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Annex", new Vector3(0f, 9.5f, 18.5f), new Vector3(0f, 0f, 24f), 72f);
            FixedCamera east = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Annex_East", new Vector3(8f, 9.5f, 18.5f), new Vector3(8f, 0f, 24f), 72f);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Annex_East", new Vector3(8f, 1.5f, 23.6f), new Vector3(4f, 3f, AnnexMaxZ - AnnexMinZ), east, 3);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Annex", new Vector3(2f, 1.5f, 23.6f), new Vector3(AnnexMaxX - AnnexMinX, 3f, AnnexMaxZ - AnnexMinZ), annex, 2);
            MenuLoopSetup.EnsureShotPoints(new[] { gym, north, west, closet, annex, east });

            RoomKit.BakeNavMesh(room, MenuLoopSetup.ScenesFolder + "/Tutorial_NavMesh.asset");
            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();
            MenuLoopSetup.EnsureComponent<TutorialPromptUI>(MenuLoopSetup.EnsureRoot("[RuntimeUI]"));

            GameObject tutorialRoot = MenuLoopSetup.EnsureRoot("[Tutorial]");
            TutorialDirector tutorial = MenuLoopSetup.EnsureComponent<TutorialDirector>(tutorialRoot);
            var so = new SerializedObject(tutorial);
            so.FindProperty("barsPoint").vector3Value = BarsPoint;
            so.FindProperty("monitorsPoint").vector3Value = MonitorsPoint;
            so.FindProperty("capsulePoint").vector3Value = CapsulePoint;
            so.FindProperty("logPoint").vector3Value = LogPoint;
            so.FindProperty("logItemId").stringValue = "test_log";
            so.FindProperty("keyPoint").vector3Value = KeyPoint;
            so.FindProperty("keyItemId").stringValue = "test_key";
            so.FindProperty("doorPoint").vector3Value = DoorPoint;
            so.FindProperty("doorScanId").stringValue = "gym_door";
            so.FindProperty("monitorsScanId").stringValue = "gym_monitors";
            so.FindProperty("reachRadius").floatValue = ReachRadius;
            so.FindProperty("shooterTargetName").stringValue = "Target_Gun";
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            MenuLoopSetup.CleanBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TutorialSetup] {ScenePath}: the greybox gym and annex, four targets, six cameras, the director.");
        }

        /// <summary>
        /// The gym in plain boxes: floor, roof, walls, the parallel bars, the block of monitors. The door south (BuildLockedDoorAndCloset),
        /// a fire door north. The walks of the steps keep their old places.
        /// </summary>
        private static void BuildGreyGym(GameObject room)
        {
            Vector3 min = LookTestSetup.GymMin, max = LookTestSetup.GymMax;
            float h = max.y, cx = (min.x + max.x) * 0.5f, cz = (min.z + max.z) * 0.5f, w = max.x - min.x, d = max.z - min.z, t = 0.3f;
            Grey(room, "Gym_Floor", new Vector3(cx, -0.05f, cz), new Vector3(w + 2f * t, 0.1f, d + 2f * t), 0.3f);
            Object.DestroyImmediate(Grey(room, "Gym_Roof", new Vector3(cx, h + 0.05f, cz), new Vector3(w + 2f * t, 0.1f, d + 2f * t), 0.2f).GetComponent<Collider>());
            Grey(room, "Gym_Wall_East", new Vector3(max.x + t * 0.5f, h * 0.5f, cz), new Vector3(t, h, d), 0.42f);
            Grey(room, "Gym_Wall_West", new Vector3(min.x - t * 0.5f, h * 0.5f, cz), new Vector3(t, h, d), 0.42f);
            // South: a doorway of 1.2 x 2.2 m at x 0.
            float zs = min.z - t * 0.5f;
            Grey(room, "Gym_Wall_South_W", new Vector3((min.x - t - 0.6f) * 0.5f, h * 0.5f, zs), new Vector3(-0.6f - min.x + t, h, t), 0.42f);
            Grey(room, "Gym_Wall_South_E", new Vector3((max.x + t + 0.6f) * 0.5f, h * 0.5f, zs), new Vector3(max.x + t - 0.6f, h, t), 0.42f);
            Grey(room, "Gym_Wall_South_Top", new Vector3(0f, (2.2f + h) * 0.5f, zs), new Vector3(1.2f, h - 2.2f, t), 0.42f);
            // North: a doorway of 1.6 x 2.4 m for the fire door.
            float zn = FireDoorMouth.z, fx = FireDoorMouth.x;
            Grey(room, "Gym_Wall_North_W", new Vector3((min.x - t + fx - 0.8f) * 0.5f, h * 0.5f, zn), new Vector3(fx - 0.8f - min.x + t, h, t), 0.42f);
            Grey(room, "Gym_Wall_North_E", new Vector3((max.x + t + fx + 0.8f) * 0.5f, h * 0.5f, zn), new Vector3(max.x + t - fx - 0.8f, h, t), 0.42f);
            Grey(room, "Gym_Wall_North_Top", new Vector3(fx, (2.4f + h) * 0.5f, zn), new Vector3(1.6f, h - 2.4f, t), 0.42f);
            // The block of monitors in the north-west corner, and the parallel bars.
            Grey(room, "Monitors", new Vector3(min.x + 1.75f, 1.1f, max.z - 0.3f), new Vector3(3.5f, 2.2f, 0.6f), 0.2f);
            foreach (float bx in new[] { -1.05f, -0.15f })
            {
                Grey(room, $"Bar_{bx}", new Vector3(bx, 0.9f, 13.5f), new Vector3(0.1f, 0.1f, 4.4f), 0.62f);
                foreach (float bz in new[] { 11.5f, 13.5f, 15.5f }) Grey(room, $"BarPost_{bx}_{bz}", new Vector3(bx, 0.45f, bz), new Vector3(0.1f, 0.9f, 0.1f), 0.62f);
            }
        }

        /// <summary>The annex: the fire door, the rail, the box and the plate, the gate to the east room, the locker, the guard, the lab worker, the switch.</summary>
        private static void BuildAnnex(GameObject room)
        {
            float cx = (AnnexMinX + AnnexMaxX) * 0.5f, cz = (AnnexMinZ + AnnexMaxZ) * 0.5f, w = AnnexMaxX - AnnexMinX, d = AnnexMaxZ - AnnexMinZ, h = AnnexHeight, t = 0.3f;
            Grey(room, "Annex_Floor", new Vector3(cx, -0.05f, cz), new Vector3(w + 2f * t, 0.1f, d + 2f * t), 0.3f);
            Grey(room, "Annex_Wall_West", new Vector3(AnnexMinX - t * 0.5f, h * 0.5f, cz), new Vector3(t, h, d), 0.42f);
            Grey(room, "Annex_Wall_East", new Vector3(AnnexMaxX + t * 0.5f, h * 0.5f, cz), new Vector3(t, h, d), 0.42f);
            Grey(room, "Annex_Wall_North", new Vector3(cx, h * 0.5f, AnnexMaxZ + t * 0.5f), new Vector3(w + 2f * t, h, t), 0.42f);
            // South: the gym's north wall stands between x -4 and 4. The rest of the annex's south side:
            Grey(room, "Annex_Wall_South_W", new Vector3(-5f, h * 0.5f, AnnexMinZ - t * 0.5f), new Vector3(2f, h, t), 0.42f);
            Grey(room, "Annex_Wall_South_E", new Vector3(7f, h * 0.5f, AnnexMinZ - t * 0.5f), new Vector3(6f, h, t), 0.42f);
            Grey(room, "Rail", new Vector3(cx, RailHeight * 0.5f, RailZ), new Vector3(w, RailHeight, t), 0.36f);
            // The partition east of her half, with the gate that the plate opens.
            float southLength = GateZ - 0.8f - AnnexMinZ, northLength = RailZ - (GateZ + 0.8f);
            Grey(room, "Partition_South", new Vector3(GateX, h * 0.5f, AnnexMinZ + southLength * 0.5f), new Vector3(t, h, southLength), 0.42f);
            Grey(room, "Partition_North", new Vector3(GateX, h * 0.5f, RailZ - northLength * 0.5f), new Vector3(t, h, northLength), 0.42f);
            GameObject gate = MenuLoopSetup.EnsureChild(room, "Gate");
            FlagGate flagGate = MenuLoopSetup.EnsureComponent<FlagGate>(gate);
            flagGate.flag = PlateFlag;
            flagGate.showWhenRaised = false;
            RoomKit.Box(gate, "Gate_Leaf", new Vector3(GateX, h * 0.5f, GateZ), new Vector3(0.2f, h, 1.6f)).GetComponent<Renderer>().sharedMaterial = ItemAssets.UnlitMaterial("physics_gate", new Color(0.55f, 0.12f, 0.1f));

            Destruction.BreakableAssets.Door(room, "FireDoor_Tutorial", FireDoorMouth, Vector3.forward, 1.6f, 2.4f, t, -1,
                Demo.DemoRoom.Grey(0.34f), Demo.DemoRoom.Grey(0.5f), Demo.DemoRoom.Colour("fire_band", new Color(0.55f, 0.12f, 0.1f)), Demo.DemoRoom.Grey(0.12f), Demo.DemoRoom.Colour("fire_lever", new Color(0.85f, 0.15f, 0.1f)),
                Demo.DemoRoom.Colour("jam_bar", new Color(0.8f, 0.66f, 0.18f)), false, "");

            GameObject puzzle = MenuLoopSetup.EnsureRoot("[Puzzle]");
            GameObject plate = MenuLoopSetup.EnsureChild(puzzle, "Plate");
            plate.transform.position = PlatePoint;
            GameObject top = MenuLoopSetup.EnsureChildPrimitive(plate, "Top", PrimitiveType.Cube, new Vector3(0f, 0.025f, 0f), new Vector3(1.2f, 0.05f, 1.2f));
            Object.DestroyImmediate(top.GetComponent<Collider>());
            top.GetComponent<Renderer>().sharedMaterial = ItemAssets.UnlitMaterial("physics_plate", new Color(0.8f, 0.65f, 0.15f));
            PressurePlate pressure = MenuLoopSetup.EnsureComponent<PressurePlate>(plate);
            pressure.flag = PlateFlag;
            pressure.top = top.transform;
            EditorUtility.SetDirty(pressure);
            GameObject box = PropAssets.Box("Box_Tutorial", BoxPoint);
            box.GetComponent<Renderer>().sharedMaterial = Demo.DemoRoom.Grey(0.62f);
            box.transform.SetParent(puzzle.transform, true);

            // The switch on the far side, where she cannot walk: a dominated figure uses it.
            GameObject lever = MenuLoopSetup.EnsureChild(puzzle, "Switch_Far");
            lever.transform.position = SwitchPoint;
            GameObject post = MenuLoopSetup.EnsureChildPrimitive(lever, "Post", PrimitiveType.Cube, new Vector3(0f, 0.6f, 0f), new Vector3(0.3f, 1.2f, 0.2f));
            post.GetComponent<Renderer>().sharedMaterial = ItemAssets.UnlitMaterial("edgar_switch", new Color(0.8f, 0.2f, 0.15f));
            GameObject stand = MenuLoopSetup.EnsureChild(lever, "Stand");
            stand.transform.position = SwitchPoint + Vector3.back * 0.9f;
            SentSwitch sent = MenuLoopSetup.EnsureComponent<SentSwitch>(lever);
            sent.flag = SwitchFlag;
            sent.point = stand.transform;
            sent.lamp = post.GetComponent<Renderer>();
            EditorUtility.SetDirty(sent);

            // The locker in the east room: the guard uniform.
            GameObject locker = MenuLoopSetup.EnsureChild(puzzle, "Locker_Uniform");
            locker.transform.position = LockerPoint;
            Material black = Demo.DemoRoom.Grey(0.07f);
            Grey(locker, "Back", LockerPoint + new Vector3(0f, 0.95f, -0.3f), new Vector3(0.9f, 1.9f, 0.05f), 0.07f);
            Grey(locker, "Side_W", LockerPoint + new Vector3(-0.43f, 0.95f, -0.05f), new Vector3(0.04f, 1.9f, 0.55f), 0.07f);
            Grey(locker, "Side_E", LockerPoint + new Vector3(0.43f, 0.95f, -0.05f), new Vector3(0.04f, 1.9f, 0.55f), 0.07f);
            GameObject uniform = MenuLoopSetup.EnsureChild(locker, "Uniform_Hanger");
            GameObject torso = MenuLoopSetup.EnsureChildPrimitive(uniform, "Torso", PrimitiveType.Cube, LockerPoint + new Vector3(0f, 1.3f, -0.15f), new Vector3(0.48f, 0.62f, 0.14f));
            GameObject legs = MenuLoopSetup.EnsureChildPrimitive(uniform, "Legs", PrimitiveType.Cube, LockerPoint + new Vector3(0f, 0.68f, -0.15f), new Vector3(0.38f, 0.6f, 0.12f));
            foreach (GameObject part in new[] { torso, legs })
            {
                Object.DestroyImmediate(part.GetComponent<Collider>());
                part.GetComponent<Renderer>().sharedMaterial = Demo.DemoRoom.Colour("uniform_torso", new Color(0.34f, 0.4f, 0.42f));
            }
            OutfitLocker component = MenuLoopSetup.EnsureComponent<OutfitLocker>(locker);
            component.outfitId = OutfitChange.GuardId;
            component.thing = "the guard uniform";
            component.shown = uniform;
            component.range = 1.6f;
            GameObject point = MenuLoopSetup.EnsureChild(locker, "Point");
            point.transform.position = LockerPoint + new Vector3(0f, 1f, 0.45f);
            component.point = point.transform;
            EditorUtility.SetDirty(component);

            // The figures of mk_edgar: a lab worker at full HP behind the rail, a guard who waits in the east room.
            GameObject figures = MenuLoopSetup.EnsureRoot("[Figures]");
            EnemySpawn worker = RoomKit.Spawn(figures, TutorialDirector.WorkerName, "labworker", WorkerPoint, 180f, 0);
            worker.hides = true;
            worker.foundAt = 0f;
            EditorUtility.SetDirty(worker);
            EnemySpawn guard = RoomKit.Spawn(figures, TutorialDirector.GuardName, "guard_pistol", GuardPoint, 270f);
            guard.heldUntilFlag = TutorialDirector.GuardWakeFlag;
            EditorUtility.SetDirty(guard);
        }

        /// <summary>
        /// The locked door in the gym's south doorway: the steel leaf (it slides west when the key opens it), its
        /// frame, the jambs and the soffit of the opening, the gear over it, and a closet behind with a dim light
        /// and a red pill (the reward). The LockedDoor sits on "Door_Gym" and measures the reach to the leaf's
        /// west half, where she stands to scan it.
        /// </summary>
        private static void BuildLockedDoorAndCloset(GameObject room, GameObject lights, LookTestSetup.Palette p)
        {
            float z = LookTestSetup.GymMin.z;
            LookTestSetup.DoorTunnel(room, "GymDoor", new Vector3(0f, 0f, z), Quaternion.Euler(0f, 180f, 0f), 0.3f, 1.2f, 2.2f, p.TileWall, p.Steel);
            LookTestSetup.Block(room, "Gym_DoorFrame_Top", PrimitiveType.Cube, new Vector3(0f, 2.27f, z + 0.05f), new Vector3(1.5f, 0.14f, 0.1f), p.Steel);
            LookTestSetup.Block(room, "Gym_DoorFrame_W", PrimitiveType.Cube, new Vector3(-0.67f, 1.1f, z + 0.05f), new Vector3(0.14f, 2.2f, 0.1f), p.Steel);
            LookTestSetup.Block(room, "Gym_DoorFrame_E", PrimitiveType.Cube, new Vector3(0.67f, 1.1f, z + 0.05f), new Vector3(0.14f, 2.2f, 0.1f), p.Steel);
            LookTestSetup.Face(room, "Gym_DoorPanel", new Vector3(0f, 2.95f, z + 0.02f), Quaternion.Euler(0f, 180f, 0f), 0.8f, 0.8f, p.Emblem, 0f, stretch: true); // faces into the gym

            // The closet: 1.8 x 1.6 m, 2.4 m high, behind the wall (z from 8.4 to 10.0).
            float z0 = 8.4f, z1 = z - 0.3f, w = 1.8f, h = 2.4f;
            Vector3 c = new Vector3(0f, 0f, (z0 + z1) * 0.5f);
            LookTestSetup.Face(room, "Closet_Floor", c, Quaternion.Euler(90f, 0f, 0f), w, z1 - z0, p.TileFloor, 0.3f);
            LookTestSetup.Face(room, "Closet_Ceiling", c + Vector3.up * h, Quaternion.Euler(-90f, 0f, 0f), w, z1 - z0, p.Ceiling, 0.3f);
            LookTestSetup.Face(room, "Closet_Wall_South", new Vector3(0f, h * 0.5f, z0), Quaternion.Euler(0f, 180f, 0f), w, h, p.TileWall, 0.3f);
            LookTestSetup.Face(room, "Closet_Wall_West", new Vector3(-w * 0.5f, h * 0.5f, c.z), Quaternion.Euler(0f, -90f, 0f), z1 - z0, h, p.TileWall, 0.3f);
            LookTestSetup.Face(room, "Closet_Wall_East", new Vector3(w * 0.5f, h * 0.5f, c.z), Quaternion.Euler(0f, 90f, 0f), z1 - z0, h, p.TileWall, 0.3f);
            LookTestSetup.Face(room, "Closet_Wall_North_W", new Vector3(-0.75f, h * 0.5f, z1), Quaternion.identity, 0.3f, h, p.TileWall, 0f);
            LookTestSetup.Face(room, "Closet_Wall_North_E", new Vector3(0.75f, h * 0.5f, z1), Quaternion.identity, 0.3f, h, p.TileWall, 0f);
            LookTestSetup.Face(room, "Closet_Wall_North_Top", new Vector3(0f, 2.3f, z1), Quaternion.identity, 1.2f, 0.2f, p.TileWall, 0f);
            LookTestSetup.Point(lights, "Closet_Light", new Vector3(0f, h - 0.2f, c.z), new Color(1f, 0.9f, 0.75f), 4f, 3f);
            ItemAssets.Pickup(null, "Closet_Pill", ItemAssets.Drug(DrugType.Red), PrimitiveType.Capsule, new Vector3(0f, 0.15f, z0 + 0.5f), Vector3.one * 0.3f, Quaternion.Euler(0f, 0f, 90f));

            // The leaf: a movable steel slab in the opening, not static (it slides), its own collider.
            GameObject leaf = MenuLoopSetup.EnsureChildPrimitive(room, "Gym_Door", PrimitiveType.Cube, new Vector3(0f, 1.1f, z + 0.05f), new Vector3(1.2f, 2.2f, 0.08f));
            leaf.transform.rotation = Quaternion.identity;
            leaf.isStatic = false;
            leaf.GetComponent<Renderer>().sharedMaterial = p.Steel;
            GameObject doorRoot = MenuLoopSetup.EnsureRoot("Door_Gym");
            doorRoot.transform.position = new Vector3(0f, 1.1f, z + 0.1f);
            Transform reach = doorRoot.transform.Find("Reach");
            if (reach == null) { reach = new GameObject("Reach").transform; reach.SetParent(doorRoot.transform, false); }
            reach.position = DoorFocus;
            LockedDoor door = MenuLoopSetup.EnsureComponent<LockedDoor>(doorRoot);
            door.keyItemId = "test_key";
            door.keyName = "the test key";
            door.leaf = leaf.transform;
            door.openOffset = new Vector3(-1.25f, 0f, 0f);
            door.point = reach;
            door.range = 1.6f;
        }

        /// <summary>The placeholder log (Bruno, 2026-10-22: "add a placeholder test log"). The writer gives the words later.</summary>
        internal static ItemDefinition EnsureTestLog() => ItemAssets.TestLog();

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
            target.range = Scannable.DefaultRange; // close to the object (Bruno, 2026-10-22)
            target.focus = null;
        }

        /// <summary>
        /// A padded training dummy on a stand: the root carries the capsule, Health, the lock point and the
        /// Enemy layer and tag, so every power lands on it; the body is canvas and a vest from primitives (no
        /// colliders of their own). With a gun it carries a DummyShooter, off until its step.
        /// </summary>
        /// <summary>The shield step's dummy: a riot shield (a flat steel slab) in front of its left side, off until its step.</summary>
        private static void ShieldFor(GameObject dummy, LookTestSetup.Palette p)
        {
            Transform old = dummy.transform.Find("Shield");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Shield";
            Object.DestroyImmediate(slab.GetComponent<Collider>());
            slab.transform.SetParent(dummy.transform, false);
            slab.transform.localScale = new Vector3(0.6f, 1.0f, 0.05f);
            slab.transform.localPosition = new Vector3(-0.18f, 1.05f, 0.42f);
            slab.GetComponent<Renderer>().sharedMaterial = p.DarkSteel;
            slab.layer = LayerMask.NameToLayer("Enemy");
            slab.SetActive(false); // EnemyShield shows it when its step turns the shield on
            GlorifiedBody.Enemy.EnemyShield guard = MenuLoopSetup.EnsureComponent<GlorifiedBody.Enemy.EnemyShield>(dummy);
            guard.Bind(slab.transform, Quaternion.identity);
            guard.enabled = false;
            EditorUtility.SetDirty(guard);
        }

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
