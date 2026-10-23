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
    /// The AI test room (Bruno, 2026-10-23: "start on the FSM AI"): a greybox hall of 18 x 14 m with low cover
    /// blocks, a baked NavMesh, Lucy at the south end, and three FSM soldiers at the north end: a soldier, a guard
    /// and the power armour (heavy: mk_orzabal cannot lift it). Each one sees Lucy, walks to a firing spot, stops,
    /// shoots with the telegraph, and moves again ("move or shoot, never both"). The difficulty decides how many aim
    /// at once. Two fixed cameras. The main menu's AI TEST button opens it.
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.AiTestSetup.BuildAiTest
    /// </summary>
    public static class AiTestSetup
    {
        public const string SceneName = "AiTest";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string NavMeshPath = MenuLoopSetup.ScenesFolder + "/AiTest_NavMesh.asset";
        public static readonly Vector3 LucyStart = new Vector3(0f, 0f, -5f);
        public static readonly string[] SoldierNames = { "Soldier_A", "Guard_B", "Armour_C" };

        [MenuItem("GlorifiedBody/Build AI Test Room")]
        public static void BuildAiTest()
        {
            Scene scene = MenuLoopSetup.OpenOrCreate(ScenePath);
            Camera camera = MenuLoopSetup.EnsureCamera(new Vector3(0f, 11f, -13f), new Vector3(0f, 0f, 0.5f), 50f);
            MenuLoopSetup.EnsureLight();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.33f, 0.36f);

            // The hall: 18 x 14 m, walls 3 m high and 0.5 m thick.
            GameObject room = MenuLoopSetup.EnsureRoot("[Room]");
            Static(Child(room, "Floor", PrimitiveType.Cube, new Vector3(0f, -0.05f, 0f), new Vector3(18f, 0.1f, 14f)));
            Static(Child(room, "Wall_North", PrimitiveType.Cube, new Vector3(0f, 1.5f, 7.25f), new Vector3(18.5f, 3f, 0.5f)));
            Static(Child(room, "Wall_South", PrimitiveType.Cube, new Vector3(0f, 1.5f, -7.25f), new Vector3(18.5f, 3f, 0.5f)));
            Static(Child(room, "Wall_East", PrimitiveType.Cube, new Vector3(9.25f, 1.5f, 0f), new Vector3(0.5f, 3f, 14f)));
            Static(Child(room, "Wall_West", PrimitiveType.Cube, new Vector3(-9.25f, 1.5f, 0f), new Vector3(0.5f, 3f, 14f)));
            // Low cover (1.1 m: it blocks the sight of a crouch later, not of a standing figure now) and two
            // pillars that do block the sight.
            Static(Child(room, "Cover_1", PrimitiveType.Cube, new Vector3(-4f, 0.55f, -1.5f), new Vector3(2f, 1.1f, 0.6f)));
            Static(Child(room, "Cover_2", PrimitiveType.Cube, new Vector3(4f, 0.55f, 0.5f), new Vector3(2f, 1.1f, 0.6f)));
            Static(Child(room, "Pillar_1", PrimitiveType.Cube, new Vector3(-1.5f, 1.5f, 2.5f), new Vector3(1f, 3f, 1f)));
            Static(Child(room, "Pillar_2", PrimitiveType.Cube, new Vector3(2.5f, 1.5f, -3f), new Vector3(1f, 3f, 1f)));

            // The NavMesh from the room's colliders (the figures are on other layers and stay out of it).
            NavMeshSurface surface = MenuLoopSetup.EnsureComponent<NavMeshSurface>(room);
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = LayerMask.GetMask("Default");
            AssetDatabase.DeleteAsset(NavMeshPath);
            surface.BuildNavMesh();
            if (surface.navMeshData != null) AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            else Debug.LogError("[AiTestSetup] the NavMesh did not build.");

            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog();
            MenuLoopSetup.EnsureLucy(catalog, LucyStart, 0f);

            var fsm = InvectorEnemyFactory.BuildSoldierFsm();
            GameObject enemies = MenuLoopSetup.EnsureRoot("[Enemies]");
            Place(enemies, InvectorEnemyFactory.BuildSoldier(SoldierNames[0], InvectorEnemyFactory.SpecFor("soldier"), new Vector3(-5f, 0f, 5.5f), 160f, catalog, fsm));
            Place(enemies, InvectorEnemyFactory.BuildSoldier(SoldierNames[1], InvectorEnemyFactory.SpecFor("guard"), new Vector3(5f, 0f, 5.5f), 200f, catalog, fsm));
            Place(enemies, InvectorEnemyFactory.BuildSoldier(SoldierNames[2], InvectorEnemyFactory.SpecFor("power_armour"), new Vector3(0f, 0f, 6f), 180f, catalog, fsm));

            // Two cameras: the wide one from over the south wall (the default), a low one from the north-east
            // corner for the south half.
            GameObject camerasRoot = MenuLoopSetup.EnsureRoot("[Cameras]");
            FixedCameraDirector director = MenuLoopSetup.EnsureComponent<FixedCameraDirector>(camerasRoot);
            FixedCamera wide = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Wide", camera.transform.position, new Vector3(0f, 0f, 0.5f), 50f);
            FixedCamera low = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_NorthEast", new Vector3(8.4f, 2.6f, 6.4f), new Vector3(-2f, 0.8f, -4f), 60f);
            director.SetDefaultCamera(wide);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_SouthWest", new Vector3(-6f, 1f, -5f), new Vector3(6f, 3f, 4f), low, 1);

            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();
            MenuLoopSetup.EnsureShotPoints(new[] { wide, low });

            MenuLoopSetup.CleanBuildSettings();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AiTestSetup] {ScenePath}: the hall, the NavMesh, Lucy, three FSM soldiers, two cameras.");
        }

        private static GameObject Child(GameObject parent, string name, PrimitiveType type, Vector3 position, Vector3 scale)
        {
            GameObject obj = MenuLoopSetup.EnsureChildPrimitive(parent, name, type, position, scale);
            obj.layer = LayerMask.NameToLayer("Default");
            return obj;
        }

        private static void Static(GameObject obj) => obj.isStatic = true;

        private static void Place(GameObject parent, GameObject soldier)
        {
            if (soldier != null) soldier.transform.SetParent(parent.transform, true);
        }
    }
}
#endif
