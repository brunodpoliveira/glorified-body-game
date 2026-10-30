#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Audio;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Enemy;
using GlorifiedBody.Story;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The WARD TEST room (2026-10-30, the design sweep's rank 10, and the feed: Bruno, "the only form to regain hp is
    /// by feeding on fallen enemies"). The hall of the KIT TEST room (the same walls and cameras) with: a patient in
    /// her bed (PatientBrain: she stays, she says her line), Marta the nurse (a scan on her chest, the fate flags
    /// marta_killed / marta_spared / marta_scanned), a lab worker and a guard (bodies to feed on), a FlagGate that shows
    /// a mark when Marta is dead (the game remembers), and the door west to TRAVEL B (leaving spares Marta). The
    /// enemies are EnemySpawn markers, so the scene holds no vendor data and is in git. WardTests drive it.
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.WardTestSetup.BuildWardTest
    /// </summary>
    public static class WardTestSetup
    {
        public const string SceneName = "WardTest";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string NavMeshPath = MenuLoopSetup.ScenesFolder + "/WardTest_NavMesh.asset";
        public const string EntryWest = "Entry_WardWest";
        public const string MusicKey = "ward_test";
        public const string Patient = "Patient_W1", Marta = "Marta", Lab = "Lab_W3", Guard = "Guard_W4";
        public const string Fate = "marta";
        public const string Gate = "Gate_MartaKilled";
        public const float Width = 20f, Depth = 14f, Height = 3f;
        public static readonly Vector3 BedTop = new Vector3(-6.4f, 0.55f, 5.9f); // the patient sits at the head end, facing east

        [MenuItem("GlorifiedBody/Build Ward Test Room (a patient, a scan on a person, feeding)")]
        public static void BuildWardTest()
        {
            Scene scene = MenuLoopSetup.OpenOrCreate(ScenePath);
            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog(); // after the scene opens
            MenuLoopSetup.EnsureCamera(new Vector3(9.6f, 4.5f, 0f), new Vector3(-5f, 0f, 0f), 60f);
            MenuLoopSetup.EnsureLight();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.31f, 0.34f);

            // The hall of the kit room, a door west at z 0 with a short corridor to the exit.
            float hw = Width * 0.5f, hd = Depth * 0.5f;
            GameObject room = MenuLoopSetup.EnsureRoot("[Room]");
            RoomKit.Box(room, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(Width, 0.1f, Depth));
            RoomKit.Box(room, "Wall_North", new Vector3(0f, Height * 0.5f, hd + 0.25f), new Vector3(Width + 1f, Height, 0.5f));
            RoomKit.Box(room, "Wall_South", new Vector3(0f, Height * 0.5f, -hd - 0.25f), new Vector3(Width + 1f, Height, 0.5f));
            RoomKit.Box(room, "Wall_East", new Vector3(hw + 0.25f, Height * 0.5f, 0f), new Vector3(0.5f, Height, Depth));
            float side = (Depth - 2f) * 0.5f;
            RoomKit.Box(room, "Wall_West_N", new Vector3(-hw - 0.25f, Height * 0.5f, 1f + side * 0.5f), new Vector3(0.5f, Height, side));
            RoomKit.Box(room, "Wall_West_S", new Vector3(-hw - 0.25f, Height * 0.5f, -1f - side * 0.5f), new Vector3(0.5f, Height, side));
            RoomKit.Box(room, "Corridor_Floor", new Vector3(-hw - 1.25f, -0.05f, 0f), new Vector3(2f, 0.1f, 2f));
            RoomKit.Box(room, "Corridor_N", new Vector3(-hw - 1.25f, Height * 0.5f, 1.25f), new Vector3(2f, Height, 0.5f));
            RoomKit.Box(room, "Corridor_S", new Vector3(-hw - 1.25f, Height * 0.5f, -1.25f), new Vector3(2f, Height, 0.5f));
            RoomKit.Box(room, "Corridor_End", new Vector3(-hw - 2.5f, Height * 0.5f, 0f), new Vector3(0.5f, Height, 2f));
            RoomKit.Box(room, "Pillar", new Vector3(8.2f, Height * 0.5f, -5.2f), new Vector3(0.8f, Height, 0.8f));
            // The bed at the north wall: 2.1 m long (along x), 1 m wide, 0.55 m high. PLACEHOLDER box.
            RoomKit.Box(room, "Bed", new Vector3(BedTop.x + 0.75f, BedTop.y * 0.5f, BedTop.z), new Vector3(2.1f, BedTop.y, 1f));
            RoomKit.BakeNavMesh(room, NavMeshPath);

            // The figures. Marta carries the scan and the fate; the patient sits up in her bed.
            MenuLoopSetup.EnsureLucy(catalog, new Vector3(-7f, 0f, -4.5f), 45f);
            GameObject figures = MenuLoopSetup.EnsureRoot("[Enemies]");
            EnemySpawn patient = RoomKit.Spawn(figures, Patient, "patient", BedTop, 90f);
            patient.line = PatientBrain.DefaultLine; // PLACEHOLDER: the writer's line
            EditorUtility.SetDirty(patient);
            EnemySpawn marta = RoomKit.Spawn(figures, Marta, "nurse", new Vector3(3f, 0f, 4.5f), 200f, variant: 0);
            marta.fate = Fate;
            marta.scanImage = TutorialSetup.EnsureScanImage(); // PLACEHOLDER picture until Marta's scan exists
            marta.scanCaption = "PLACEHOLDER: what the scan shows of Marta.";
            EditorUtility.SetDirty(marta);
            RoomKit.Spawn(figures, Lab, "labworker", new Vector3(7f, 0f, -4f), 250f, variant: 2);
            RoomKit.Spawn(figures, Guard, "guard_pistol", new Vector3(8.5f, 0f, 3f), 240f);

            // The game remembers: a mark on the north wall that shows only when Marta is dead. PLACEHOLDER.
            GameObject gateRoot = MenuLoopSetup.EnsureRoot(Gate);
            gateRoot.transform.position = Vector3.zero;
            FlagGate gate = MenuLoopSetup.EnsureComponent<FlagGate>(gateRoot);
            gate.flag = Fate + "_killed";
            gate.showWhenRaised = true;
            GameObject mark = MenuLoopSetup.EnsureChildPrimitive(gateRoot, "Mark_MartaKilled", PrimitiveType.Cube, new Vector3(1.5f, 1.6f, hd - 0.02f), new Vector3(0.8f, 0.8f, 0.04f));
            Object.DestroyImmediate(mark.GetComponent<Collider>());
            Renderer markRenderer = mark.GetComponent<Renderer>();
            markRenderer.sharedMaterial = ItemAssets.UnlitMaterial(ItemAssets.Drug(GlorifiedBody.Player.DrugType.Red));
            mark.SetActive(false);

            SaveAssets.SavePoint("Save_Ward", new Vector3(-9f, 0f, -3f), 90f);
            RoomKit.Exit("Exit_WardWest", new Vector3(-hw - 1.7f, 1.2f, 0f), new Vector3(0.6f, 2.4f, 2f), TravelTestSetup.SceneB, TravelTestSetup.EntryFromA);
            RoomKit.Entry(EntryWest, new Vector3(-hw + 1.4f, 0f, 0f), 90f);
            GameObject music = MenuLoopSetup.EnsureRoot("[Music]");
            MenuLoopSetup.EnsureComponent<RoomMusic>(music).Key = MusicKey;

            // The cameras of the kit room (the same hall).
            GameObject camerasRoot = LookTestSetup.ResetCameras();
            FixedCameraDirector director = MenuLoopSetup.EnsureComponent<FixedCameraDirector>(camerasRoot);
            FixedCamera west = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_WestHalf", new Vector3(9.6f, 4.5f, 0f), new Vector3(-5f, 0f, 0f), 60f);
            FixedCamera east = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_EastHalf", new Vector3(-9.6f, 4.5f, 3.5f), new Vector3(5f, 0f, -0.5f), 60f);
            FixedCamera door = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Door", new Vector3(-4.5f, 2.6f, 2.6f), new Vector3(-11.5f, 0.8f, 0f), 55f);
            director.SetDefaultCamera(west);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_West", new Vector3(-5f, 1f, 0f), new Vector3(10f, 3f, Depth), west, 1);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_East", new Vector3(5f, 1f, 0f), new Vector3(10f, 3f, Depth), east, 1);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Door", new Vector3(-hw - 0.75f, 1f, 0f), new Vector3(4.5f, 3f, 2.2f), door, 2);

            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();
            MenuLoopSetup.EnsureShotPoints(new[] { west, east, door });
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            MenuLoopSetup.CleanBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[WardTestSetup] built WardTest: a patient in bed, Marta (scan, fate), a lab worker and a guard, a flag gate, a door west.");
        }
    }
}
#endif
