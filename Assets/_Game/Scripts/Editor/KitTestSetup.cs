#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Audio;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Editor.Destruction;
using GlorifiedBody.Player;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The KIT TEST room (2026-10-26, the design sweep's rank 5 and the gaps sweep's "no room has soldiers,
    /// civilians, cover, items and a save machine together"): a hall built from the room kit with all of them, and a
    /// door west to TRAVEL B. The enemies are EnemySpawn markers, so the scene holds no vendor data and is in git
    /// (the prefabs are built locally: InvectorEnemyFactory.BuildEnemyPrefabs). KitTests drive it.
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.KitTestSetup.BuildKitTest
    /// </summary>
    public static class KitTestSetup
    {
        public const string SceneName = "KitTest";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string NavMeshPath = MenuLoopSetup.ScenesFolder + "/KitTest_NavMesh.asset";
        public const string EntryWest = "Entry_KitWest";
        public const string MusicKey = "kit_hall";
        public static readonly string[] Armed = { "Soldier_K1", "Soldier_K2", "Guard_K3" };
        public static readonly string[] Civilians = { "Lab_K4", "Nurse_K5" };
        public const float Width = 20f, Depth = 14f, Height = 3f;

        [MenuItem("GlorifiedBody/Build Kit Test Room (everything together)")]
        public static void BuildKitTest()
        {
            BreakableAssets.EnsureAll();
            Scene scene = MenuLoopSetup.OpenOrCreate(ScenePath);
            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog(); // after the scene opens
            MenuLoopSetup.EnsureCamera(new Vector3(9.6f, 4.5f, 0f), new Vector3(-5f, 0f, 0f), 60f);
            MenuLoopSetup.EnsureLight();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.31f, 0.34f);

            // The hall, a door west at z 0 with a short corridor to the exit.
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
            RoomKit.Box(room, "Pillar", new Vector3(8.2f, Height * 0.5f, -5.2f), new Vector3(0.8f, Height, 0.8f)); // in the corner: its shadow hides little floor
            RoomKit.BakeNavMesh(room, NavMeshPath);

            // Cover the soldiers use, a table with things on it.
            GameObject breakables = MenuLoopSetup.EnsureRoot("[Breakables]");
            for (int i = breakables.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(breakables.transform.GetChild(i).gameObject);
            BreakableAssets.Barricade(breakables, "Barricade_K1", new Vector3(5f, 0f, 2.5f), 0f);
            BreakableAssets.Barricade(breakables, "Barricade_K2", new Vector3(-1f, 0f, 3f), 6f);
            BreakableAssets.Table(breakables, "Table_K1", new Vector3(-4f, 0f, -2.5f), 90f);

            // The figures: three armed (rifle, pistol, shield), two civilians at the north wall.
            MenuLoopSetup.EnsureLucy(catalog, new Vector3(-7f, 0f, -4.5f), 45f);
            GameObject figures = MenuLoopSetup.EnsureRoot("[Enemies]");
            RoomKit.Spawn(figures, Armed[0], "soldier_rifle", new Vector3(7f, 0f, 5f), 200f);
            RoomKit.Spawn(figures, Armed[1], "soldier_pistol", new Vector3(8f, 0f, -3f), 250f);
            RoomKit.Spawn(figures, Armed[2], "guard_shield", new Vector3(3f, 0f, 5.5f), 190f);
            RoomKit.Spawn(figures, Civilians[0], "labworker", new Vector3(-3f, 0f, 5.6f), 170f, variant: 1);
            RoomKit.Spawn(figures, Civilians[1], "nurse", new Vector3(-6.5f, 0f, 5.4f), 150f, variant: 0);

            // Items, a save machine, a checkpoint, the door west, the music key.
            MenuLoopSetup.EnsurePickup("Drug_Nalcon", DrugType.Nalcon, new Vector3(-8.5f, 0.4f, -5.8f));
            SaveAssets.SavePoint("Save_Kit", new Vector3(-9f, 0f, -3f), 90f);
            SaveAssets.Checkpoint("Checkpoint_Kit", new Vector3(-6f, 0f, 0f), 90f, new Vector3(2f, 2f, 2f));
            RoomKit.Exit("Exit_KitWest", new Vector3(-hw - 1.7f, 1.2f, 0f), new Vector3(0.6f, 2.4f, 2f), TravelTestSetup.SceneB, TravelTestSetup.EntryFromA);
            RoomKit.Entry(EntryWest, new Vector3(-hw + 1.4f, 0f, 0f), 90f);
            GameObject music = MenuLoopSetup.EnsureRoot("[Music]");
            MenuLoopSetup.EnsureComponent<RoomMusic>(music).Key = MusicKey;

            // Cameras (the coverage test placed them): the hall reads along its long side. The west half from the east
            // wall, the east half from the west wall, each 10 to 20 m away and 4.5 m up, over the barricades; one at
            // the door for the corridor west. (Behind the south wall, the wall hid the south strip; close inside it,
            // the sides fell out of the frame.)
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
            Debug.Log("[KitTestSetup] built KitTest: 3 armed, 2 civilians, cover, items, a save machine, a checkpoint, a door west.");
        }
    }
}
#endif
