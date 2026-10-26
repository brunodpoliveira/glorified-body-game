#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Integration.Invector.Build;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The civilian test room (Bruno, 2026-10-25: "another test room to test the unarmed civvies"): a lab office of
    /// 16 x 12 m with desks, a pillar and a dead-end alcove in the north-east corner. Three unarmed civilians at
    /// their desks (two lab workers and a nurse, CivilianBrain), one guard with a pistol at the east door (the
    /// civilians' scream calls him), Lucy at the south end. Two fixed cameras. Local only, as the AI test room: the
    /// figures carry vendor data (.gitignore). The main menu's CIVILIAN TEST button opens it.
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.CivilianTestSetup.BuildCivilianTest
    /// </summary>
    public static class CivilianTestSetup
    {
        public const string SceneName = "CivilianTest";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string NavMeshPath = MenuLoopSetup.ScenesFolder + "/CivilianTest_NavMesh.asset";
        public const float Width = 16f, Depth = 12f, Height = 3f;
        public static readonly Vector3 LucyStart = new Vector3(0f, 0f, -4.5f);
        public static readonly string[] CivilianNames = { "Lab_A", "Lab_B", "Nurse_C" };
        public const string GuardName = "Guard_D";
        /// <summary>The civilian in the alcove: it has nowhere to run, so it begs at once.</summary>
        public const string CorneredName = "Nurse_C";
        /// <summary>Just outside the alcove's mouth.</summary>
        public static readonly Vector3 AlcoveMouth = new Vector3(5.4f, 0f, 2.1f);

        [MenuItem("GlorifiedBody/Build Civilian Test Room")]
        public static void BuildCivilianTest()
        {
            Scene scene = MenuLoopSetup.OpenOrCreate(ScenePath);
            Camera camera = MenuLoopSetup.EnsureCamera(new Vector3(0f, 10f, -11.5f), new Vector3(0f, 0f, 0.6f), 52f);
            MenuLoopSetup.EnsureLight();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.33f, 0.36f);

            float hw = Width * 0.5f, hd = Depth * 0.5f;
            GameObject room = MenuLoopSetup.EnsureRoot("[Room]");
            Static(Child(room, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(Width, 0.1f, Depth)));
            Static(Child(room, "Wall_North", new Vector3(0f, Height * 0.5f, hd + 0.25f), new Vector3(Width + 0.5f, Height, 0.5f)));
            Static(Child(room, "Wall_South", new Vector3(0f, Height * 0.5f, -hd - 0.25f), new Vector3(Width + 0.5f, Height, 0.5f)));
            Static(Child(room, "Wall_East", new Vector3(hw + 0.25f, Height * 0.5f, 0f), new Vector3(0.5f, Height, Depth)));
            Static(Child(room, "Wall_West", new Vector3(-hw - 0.25f, Height * 0.5f, 0f), new Vector3(0.5f, Height, Depth)));
            // The desks (0.75 m: they stop no sight) and a pillar that does.
            Static(Child(room, "Desk_1", new Vector3(-4.5f, 0.375f, 2.5f), new Vector3(2.2f, 0.75f, 0.9f)));
            Static(Child(room, "Desk_2", new Vector3(-0.5f, 0.375f, 2.5f), new Vector3(2.2f, 0.75f, 0.9f)));
            Static(Child(room, "Desk_3", new Vector3(-4.5f, 0.375f, -0.5f), new Vector3(2.2f, 0.75f, 0.9f)));
            Static(Child(room, "Pillar", new Vector3(2f, Height * 0.5f, -1f), new Vector3(0.9f, Height, 0.9f)));
            // The alcove in the north-east corner (x 4.6 to 8, z 2.95 to 6): a wall on the west, a wall on the south
            // with a 1.25 m mouth at its west end. A civilian in it has nowhere to run when she stands at the mouth.
            Static(Child(room, "Alcove_West", new Vector3(4.6f, Height * 0.5f, 4.4f), new Vector3(0.3f, Height, 3.2f)));
            Static(Child(room, "Alcove_South", new Vector3(7.05f, Height * 0.5f, 2.95f), new Vector3(2.1f, Height, 0.3f)));

            NavMeshSurface surface = MenuLoopSetup.EnsureComponent<NavMeshSurface>(room);
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = LayerMask.GetMask("Default");
            AssetDatabase.DeleteAsset(NavMeshPath);
            NavAgentSettings.Ensure(); // the desk is no step
            surface.BuildNavMesh();
            if (surface.navMeshData != null) AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            else Debug.LogError("[CivilianTestSetup] the NavMesh did not build.");

            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog();
            MenuLoopSetup.EnsureLucy(catalog, LucyStart, 0f);

            GameObject figures = MenuLoopSetup.EnsureRoot("[Enemies]");
            // Spawn markers since 2026-10-26 (the room kit): the scene holds no vendor data.
            RoomKit.Spawn(figures, CivilianNames[0], "labworker", new Vector3(-4.5f, 0f, 3.4f), 180f);
            RoomKit.Spawn(figures, CivilianNames[1], "labworker", new Vector3(-0.5f, 0f, 3.4f), 170f);
            // The nurse works in the alcove, her back to the room.
            RoomKit.Spawn(figures, CivilianNames[2], "nurse", new Vector3(6.6f, 0f, 4.7f), 20f);
            // The guard at the east wall faces the wall: he cannot see her, so the civilians' scream calls him.
            RoomKit.Spawn(figures, GuardName, "guard_pistol", new Vector3(6.8f, 0f, -3.5f), 90f);

            GameObject camerasRoot = MenuLoopSetup.EnsureRoot("[Cameras]");
            FixedCameraDirector director = MenuLoopSetup.EnsureComponent<FixedCameraDirector>(camerasRoot);
            FixedCamera wide = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Wide", camera.transform.position, new Vector3(0f, 0f, 0.6f), 52f);
            FixedCamera corner = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_NorthWest", new Vector3(-7.4f, 2.6f, 5.4f), new Vector3(2f, 0.8f, -3f), 62f);
            director.SetDefaultCamera(wide);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_South", new Vector3(0f, 1f, -4.5f), new Vector3(16f, 3f, 3f), corner, 1);

            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();
            MenuLoopSetup.EnsureShotPoints(new[] { wide, corner });

            MenuLoopSetup.CleanBuildSettings();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CivilianTestSetup] {ScenePath}: the office, the NavMesh, Lucy, three civilians, one guard, two cameras.");
        }

        private static GameObject Child(GameObject parent, string name, Vector3 position, Vector3 scale)
        {
            GameObject obj = MenuLoopSetup.EnsureChildPrimitive(parent, name, PrimitiveType.Cube, position, scale);
            obj.layer = LayerMask.NameToLayer("Default");
            return obj;
        }

        private static void Static(GameObject obj) => obj.isStatic = true;

        private static void Place(GameObject parent, GameObject figure)
        {
            if (figure != null) figure.transform.SetParent(parent.transform, true);
        }
    }
}
#endif
