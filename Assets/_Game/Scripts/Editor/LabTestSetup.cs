#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Audio;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Editor.Destruction;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The LAB TEST room (2026-10-27, the design sweep's rank 6, Bruno: "let's start with 6"): the armed civilians and
    /// a figure that carries a key item. The hall of the KIT TEST room (the same walls and cameras) with a knife lab
    /// worker, two lab workers with pistols, one barricade, and a locked door west to a store room. One of the gunmen
    /// carries the key card: it drops beside his body at his death (ItemCarrier). The enemies are EnemySpawn markers,
    /// so the scene holds no vendor data and is in git. ArmedCivilianTests and the "lab" bot drive it.
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.LabTestSetup.BuildLabTest
    /// </summary>
    public static class LabTestSetup
    {
        public const string SceneName = "LabTest";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string NavMeshPath = MenuLoopSetup.ScenesFolder + "/LabTest_NavMesh.asset";
        public const string MusicKey = "lab_test";
        public const string Knife = "Lab_Knife";
        public const string Carrier = "Lab_Gun1";
        public const string Gunman = "Lab_Gun2";
        public const string Drop = "Drop_LabCard";
        public const string Door = "Door_Store";
        public const float Width = 20f, Depth = 14f, Height = 3f;

        [MenuItem("GlorifiedBody/Build Lab Test Room (armed civilians, a carried key)")]
        public static void BuildLabTest()
        {
            BreakableAssets.EnsureAll();
            ItemAssets.CreateAll();
            Scene scene = MenuLoopSetup.OpenOrCreate(ScenePath);
            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog(); // after the scene opens
            MenuLoopSetup.EnsureCamera(new Vector3(9.6f, 4.5f, 0f), new Vector3(-5f, 0f, 0f), 60f);
            MenuLoopSetup.EnsureLight();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.31f, 0.34f);

            // The hall of the kit room, the doorway west at z 0 and a store room behind it.
            float hw = Width * 0.5f, hd = Depth * 0.5f;
            GameObject room = MenuLoopSetup.EnsureRoot("[Room]");
            RoomKit.Box(room, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(Width, 0.1f, Depth));
            RoomKit.Box(room, "Wall_North", new Vector3(0f, Height * 0.5f, hd + 0.25f), new Vector3(Width + 1f, Height, 0.5f));
            RoomKit.Box(room, "Wall_South", new Vector3(0f, Height * 0.5f, -hd - 0.25f), new Vector3(Width + 1f, Height, 0.5f));
            RoomKit.Box(room, "Wall_East", new Vector3(hw + 0.25f, Height * 0.5f, 0f), new Vector3(0.5f, Height, Depth));
            float side = (Depth - 2f) * 0.5f;
            RoomKit.Box(room, "Wall_West_N", new Vector3(-hw - 0.25f, Height * 0.5f, 1f + side * 0.5f), new Vector3(0.5f, Height, side));
            RoomKit.Box(room, "Wall_West_S", new Vector3(-hw - 0.25f, Height * 0.5f, -1f - side * 0.5f), new Vector3(0.5f, Height, side));
            RoomKit.Box(room, "Store_Floor", new Vector3(-hw - 1.25f, -0.05f, 0f), new Vector3(2f, 0.1f, 2f));
            RoomKit.Box(room, "Store_N", new Vector3(-hw - 1.25f, Height * 0.5f, 1.25f), new Vector3(2f, Height, 0.5f));
            RoomKit.Box(room, "Store_S", new Vector3(-hw - 1.25f, Height * 0.5f, -1.25f), new Vector3(2f, Height, 0.5f));
            RoomKit.Box(room, "Store_End", new Vector3(-hw - 2.5f, Height * 0.5f, 0f), new Vector3(0.5f, Height, 2f));
            RoomKit.Box(room, "Pillar", new Vector3(8.2f, Height * 0.5f, -5.2f), new Vector3(0.8f, Height, 0.8f));
            // The door leaf in the doorway (it slides north into the wall); the NavMesh keeps the enemies out of the store.
            GameObject leaf = RoomKit.Box(room, "Store_Door", new Vector3(-hw - 0.25f, 1.2f, 0f), new Vector3(0.12f, 2.4f, 2f));
            leaf.isStatic = false;
            RoomKit.BakeNavMesh(room, NavMeshPath);
            GameObject doorRoot = MenuLoopSetup.EnsureRoot(Door);
            doorRoot.transform.position = new Vector3(-hw - 0.25f, 1.1f, 0f);
            Transform reach = doorRoot.transform.Find("Reach");
            if (reach == null) { reach = new GameObject("Reach").transform; reach.SetParent(doorRoot.transform, false); }
            reach.position = new Vector3(-hw + 0.2f, 1.1f, 0f);
            LockedDoor door = MenuLoopSetup.EnsureComponent<LockedDoor>(doorRoot);
            door.keyItemId = "lab_card";
            door.keyName = "the lab key card";
            door.leaf = leaf.transform;
            door.openOffset = new Vector3(0f, 0f, 2.1f);
            door.point = reach;
            door.range = 1.6f;

            // Cover for the gunmen.
            GameObject breakables = MenuLoopSetup.EnsureRoot("[Breakables]");
            for (int i = breakables.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(breakables.transform.GetChild(i).gameObject);
            BreakableAssets.Barricade(breakables, "Barricade_L1", new Vector3(4f, 0f, 2f), 0f);

            // The items: the carried card (hidden until its carrier dies), a pill in the store room.
            GameObject items = MenuLoopSetup.EnsureRoot("[Items]");
            GameObject card = ItemAssets.Pickup(items, Drop, ItemAssets.LabCard(), PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f), new Vector3(0.18f, 0.02f, 0.12f), Quaternion.Euler(0f, 20f, 0f));
            card.SetActive(false);
            MenuLoopSetup.EnsurePickup("Store_Pill", DrugType.Red, new Vector3(-hw - 1.6f, 0.15f, 0f));

            // The figures: the knife, a gunman with the card, a gunman behind the barricade's line.
            MenuLoopSetup.EnsureLucy(catalog, new Vector3(-7f, 0f, -4.5f), 45f);
            GameObject figures = MenuLoopSetup.EnsureRoot("[Enemies]");
            RoomKit.Spawn(figures, Knife, "labworker_knife", new Vector3(6f, 0f, 4.5f), 220f);
            EnemySpawn carrier = RoomKit.Spawn(figures, Carrier, "labworker_pistol", new Vector3(7.5f, 0f, -3f), 250f);
            carrier.drop = card.GetComponent<ItemPickup>();
            EditorUtility.SetDirty(carrier);
            RoomKit.Spawn(figures, Gunman, "labworker_pistol", new Vector3(3f, 0f, 5.5f), 190f);

            SaveAssets.SavePoint("Save_Lab", new Vector3(-9f, 0f, -3f), 90f);
            GameObject music = MenuLoopSetup.EnsureRoot("[Music]");
            MenuLoopSetup.EnsureComponent<RoomMusic>(music).Key = MusicKey;

            // The cameras of the kit room (the same hall): the west half from the east wall, the east half from the west
            // wall, one at the door for the store room.
            GameObject camerasRoot = LookTestSetup.ResetCameras();
            FixedCameraDirector director = MenuLoopSetup.EnsureComponent<FixedCameraDirector>(camerasRoot);
            FixedCamera west = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_WestHalf", new Vector3(9.6f, 4.5f, 0f), new Vector3(-5f, 0f, 0f), 60f);
            FixedCamera east = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_EastHalf", new Vector3(-9.6f, 4.5f, 3.5f), new Vector3(5f, 0f, -0.5f), 60f);
            FixedCamera store = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Door", new Vector3(-4.5f, 2.6f, 2.6f), new Vector3(-11.5f, 0.8f, 0f), 55f);
            director.SetDefaultCamera(west);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_West", new Vector3(-5f, 1f, 0f), new Vector3(10f, 3f, Depth), west, 1);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_East", new Vector3(5f, 1f, 0f), new Vector3(10f, 3f, Depth), east, 1);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Door", new Vector3(-hw - 0.75f, 1f, 0f), new Vector3(4.5f, 3f, 2.2f), store, 2);

            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();
            MenuLoopSetup.EnsureShotPoints(new[] { west, east, store });
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            MenuLoopSetup.CleanBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[LabTestSetup] built LabTest: a knife, two pistols (one carries the card), a barricade, a locked store room.");
        }
    }
}
#endif
