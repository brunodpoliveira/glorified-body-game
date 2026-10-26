#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Destruction;
using GlorifiedBody.Enemy;
using GlorifiedBody.Integration.Invector.Build;

namespace GlorifiedBody.Editor.Destruction
{
    /// <summary>
    /// The cover test room (Bruno, 2026-10-24: breakable cover "for more kinetic scenarios"): a dark office hall of
    /// 16 x 12 m, 3.2 m high, lit only by six breakable ceiling lamps (Realtime, hard shadows, a fake bounce each)
    /// and a weak blue fill. Three chest-high barricades (two of them the soldiers' cover), two tall screens on her
    /// side, three tables with items (two beside the soldiers' barricades), four plaster patches on the walls. Two FSM soldiers (a rifleman and a soldier) and Lucy at the south end.
    /// Two fixed cameras under the ceiling. Local only, as the AI test room: the soldiers carry vendor data
    /// (.gitignore). The main menu's COVER TEST button opens it.
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.Destruction.CoverTestSetup.BuildCoverTest
    /// </summary>
    public static class CoverTestSetup
    {
        public const string SceneName = "CoverTest";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string NavMeshPath = MenuLoopSetup.ScenesFolder + "/CoverTest_NavMesh.asset";
        public const float Width = 16f, Depth = 12f, Height = 3.2f, Wall = 0.5f;
        public static readonly Vector3 LucyStart = new Vector3(0f, 0f, -4.5f);
        public static readonly string[] SoldierNames = { "Rifleman_A", "Soldier_B" };

        [MenuItem("GlorifiedBody/Build Cover Test Room")]
        public static void BuildCoverTest()
        {
            BreakableAssets.EnsureAll();
            Scene scene = MenuLoopSetup.OpenOrCreate(ScenePath);
            Camera camera = MenuLoopSetup.EnsureCamera(new Vector3(-7f, 2.9f, -5.4f), new Vector3(0f, 0.6f, 1f), 62f);
            MenuLoopSetup.RemoveObject("Directional Light"); // indoors: the lamps only
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.065f, 0.08f);

            float hw = Width * 0.5f, hd = Depth * 0.5f;
            GameObject room = MenuLoopSetup.EnsureRoot("[Room]");
            Static(Child(room, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(Width, 0.1f, Depth)));
            Static(Child(room, "Ceiling", new Vector3(0f, Height + 0.05f, 0f), new Vector3(Width, 0.1f, Depth)));
            Static(Child(room, "Wall_North", new Vector3(0f, Height * 0.5f, hd + Wall * 0.5f), new Vector3(Width + Wall * 2f, Height, Wall)));
            Static(Child(room, "Wall_South", new Vector3(0f, Height * 0.5f, -hd - Wall * 0.5f), new Vector3(Width + Wall * 2f, Height, Wall)));
            Static(Child(room, "Wall_East", new Vector3(hw + Wall * 0.5f, Height * 0.5f, 0f), new Vector3(Wall, Height, Depth)));
            Static(Child(room, "Wall_West", new Vector3(-hw - Wall * 0.5f, Height * 0.5f, 0f), new Vector3(Wall, Height, Depth)));
            Static(Child(room, "Pillar", new Vector3(5.5f, Height * 0.5f, -2.5f), new Vector3(0.8f, Height, 0.8f)));

            // The NavMesh from the room only: the breakables carve it while they stand (NavMeshObstacle).
            NavMeshSurface surface = MenuLoopSetup.EnsureComponent<NavMeshSurface>(room);
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = LayerMask.GetMask("Default");
            AssetDatabase.DeleteAsset(NavMeshPath);
            GlorifiedBody.Editor.NavAgentSettings.Ensure(); // the desk is no step
            surface.BuildNavMesh();
            if (surface.navMeshData != null) AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            else Debug.LogError("[CoverTestSetup] the NavMesh did not build.");

            GameObject breakables = MenuLoopSetup.EnsureRoot("[Breakables]");
            for (int i = breakables.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(breakables.transform.GetChild(i).gameObject);
            BreakableAssets.Barricade(breakables, "Barricade_1", new Vector3(-3.5f, 0f, 2f), 0f);
            BreakableAssets.Barricade(breakables, "Barricade_2", new Vector3(3f, 0f, 2.5f), 8f);
            // Her side: two tall screens (their bullets wear them down, Bruno's C3) and a low barricade.
            BreakableAssets.Barricade(breakables, "Barricade_3", new Vector3(-1f, 0f, -2f), -6f);
            BreakableAssets.Screen(breakables, "Screen_1", new Vector3(-3.6f, 0f, -3.2f), 10f);
            BreakableAssets.Screen(breakables, "Screen_2", new Vector3(2.4f, 0f, -3.4f), -8f);
            // The tables stand beside the soldiers' barricades: a blast at a soldier in cover reaches them (C4).
            BreakableAssets.Table(breakables, "Table_1", new Vector3(-5.4f, 0f, 2.8f), 90f);
            BreakableAssets.Table(breakables, "Table_2", new Vector3(2.5f, 0f, -0.8f), 0f);
            BreakableAssets.Table(breakables, "Table_3", new Vector3(4.9f, 0f, 3.2f), 90f);
            float inner = hd;
            BreakableAssets.Plaster(breakables, "Plaster_N1", new Vector3(-4.5f, 1.5f, inner), Vector3.back, new Vector2(1.4f, 1.1f));
            BreakableAssets.Plaster(breakables, "Plaster_N2", new Vector3(2f, 1.4f, inner), Vector3.back, new Vector2(1.2f, 1f));
            BreakableAssets.Plaster(breakables, "Plaster_E1", new Vector3(hw, 1.5f, 1.5f), Vector3.left, new Vector2(1.3f, 1.1f));
            BreakableAssets.Plaster(breakables, "Plaster_W1", new Vector3(-hw, 1.4f, 3f), Vector3.right, new Vector2(1.2f, 1f));
            Color warm = new Color(1f, 0.95f, 0.85f);
            int n = 0;
            foreach (float x in new[] { -4.5f, 0f, 4.5f })
                foreach (float z in new[] { -2.8f, 2.6f })
                    BreakableAssets.Lamp(breakables, $"Lamp_{++n}", new Vector3(x, Height, z), warm, 9f, 95f, 6f);

            // A weak blue fill with no shadows: when every lamp is out, the room is dark, not black (PROPOSED;
            // the darkness mechanic waits for Bruno's brainstorm).
            GameObject fill = MenuLoopSetup.EnsureChild(room, "Fill");
            fill.transform.SetPositionAndRotation(new Vector3(0f, Height - 0.3f, 0f), Quaternion.Euler(90f, 0f, 0f));
            Light fillLight = MenuLoopSetup.EnsureComponent<Light>(fill);
            fillLight.type = LightType.Point;
            fillLight.color = new Color(0.55f, 0.65f, 0.9f);
            fillLight.intensity = 0.6f;
            fillLight.range = 14f;
            fillLight.shadows = LightShadows.None;
            fillLight.lightmapBakeType = LightmapBakeType.Realtime;

            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog();
            MenuLoopSetup.EnsureLucy(catalog, LucyStart, 0f);

            GameObject enemies = MenuLoopSetup.EnsureRoot("[Enemies]");
            // Spawn markers since 2026-10-26 (the room kit): the scene holds no vendor data.
            RoomKit.Spawn(enemies, SoldierNames[0], "soldier_rifle", new Vector3(-4f, 0f, 4.2f), 180f);
            RoomKit.Spawn(enemies, SoldierNames[1], "soldier_pistol", new Vector3(4.5f, 0f, 4.4f), 190f);

            // Two cameras under the ceiling: from the south-west corner (the default) and from the north-east one
            // for the south half.
            GameObject camerasRoot = MenuLoopSetup.EnsureRoot("[Cameras]");
            FixedCameraDirector director = MenuLoopSetup.EnsureComponent<FixedCameraDirector>(camerasRoot);
            FixedCamera main = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_SouthWest", camera.transform.position, new Vector3(0f, 0.6f, 1f), 62f);
            FixedCamera back = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_NorthEast", new Vector3(7.4f, 2.9f, 5.4f), new Vector3(-1f, 0.6f, -2.5f), 62f);
            director.SetDefaultCamera(main);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_SouthWest", new Vector3(-5f, 1f, -4f), new Vector3(6f, 3f, 4f), back, 1);

            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();
            MenuLoopSetup.EnsureShotPoints(new[] { main, back });

            MenuLoopSetup.CleanBuildSettings();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CoverTestSetup] {ScenePath}: the hall, 3 barricades, 3 tables, 4 plaster patches, 6 lamps, 2 soldiers.");
        }

        private static GameObject Child(GameObject parent, string name, Vector3 position, Vector3 scale)
        {
            GameObject obj = MenuLoopSetup.EnsureChildPrimitive(parent, name, PrimitiveType.Cube, position, scale);
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
