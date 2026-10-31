#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Core;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The two rooms of the travel test (2026-10-26, the design sweep's rank 1): TRAVEL A (a dummy whose body stays, a
    /// pill, a door east) and TRAVEL B (a door west, a save machine). No vendor data, so both are in git. TravelTests
    /// walks her between them. Batch: tools/unity.sh exec GlorifiedBody.Editor.TravelTestSetup.BuildTravelTest
    /// </summary>
    public static class TravelTestSetup
    {
        public const string SceneA = "TravelTest_A";
        public const string SceneB = "TravelTest_B";
        public const string PathA = MenuLoopSetup.ScenesFolder + "/" + SceneA + ".unity";
        public const string PathB = MenuLoopSetup.ScenesFolder + "/" + SceneB + ".unity";
        public const string EntryFromA = "Entry_FromA"; // in B, inside its west door
        public const string EntryFromB = "Entry_FromB"; // in A, inside its east door

        [MenuItem("GlorifiedBody/Build Travel Test (two rooms)")]
        public static void BuildTravelTest()
        {
            // Room A: 12 x 10 m, the door in the east wall at z 0.
            Scene a = MenuLoopSetup.OpenOrCreate(PathA);
            // The catalog after the scene opens: a new scene unloads the unused assets, and the prefab links with them.
            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog();
            Room(12f, 10f, doorEast: true);
            MenuLoopSetup.EnsureLucy(catalog, new Vector3(0f, 0f, -2f), 90f);
            GameObject dummy = MenuLoopSetup.EnsureDummy("Dummy_Travel", new Vector3(-3f, 0f, 2f), catalog, "guard");
            var healthSo = new SerializedObject(dummy.GetComponent<Health>());
            healthSo.FindProperty("destroyOnDeath").boolValue = false; // the body stays for the return
            healthSo.ApplyModifiedPropertiesWithoutUndo();
            MenuLoopSetup.EnsurePickup("Drug_Nalcon", DrugType.Nalcon, new Vector3(-3f, 0.4f, -3f));
            // A log for the map's marks (2026-10-31).
            ItemAssets.Pickup(null, "Log_Travel", ItemAssets.TestLog(), PrimitiveType.Cube, new Vector3(3f, 0.05f, 3f), new Vector3(0.3f, 0.04f, 0.4f), Quaternion.Euler(0f, 15f, 0f));
            // An audio log (Bruno, 2026-10-31): it plays while she walks.
            ItemAssets.Pickup(null, "Tape_Travel", ItemAssets.TestTape(), PrimitiveType.Cube, new Vector3(1.5f, 0.06f, -3.5f), new Vector3(0.24f, 0.08f, 0.16f), Quaternion.Euler(0f, -20f, 0f));
            Exit("Exit_ToB", new Vector3(6.7f, 1.2f, 0f), SceneB, EntryFromA);
            Entry(EntryFromB, new Vector3(4.6f, 0f, 0f), 270f);
            Finish(a, new Vector3(-5f, 6f, -7f), new Vector3(1f, 0f, 1f));

            // Room B: 10 x 8 m, the door in the west wall at z 0, a save machine.
            Scene b = MenuLoopSetup.OpenOrCreate(PathB);
            catalog = MenuLoopSetup.LoadCatalog();
            Room(10f, 8f, doorEast: false);
            MenuLoopSetup.EnsureLucy(catalog, new Vector3(0f, 0f, 0f), 90f);
            Exit("Exit_ToA", new Vector3(-5.7f, 1.2f, 0f), SceneA, EntryFromB);
            Entry(EntryFromA, new Vector3(-3.6f, 0f, 0f), 90f);
            SaveAssets.SavePoint("Save_TravelB", new Vector3(3f, 0f, 2.5f), 180f);
            // The floor plan of TEST FLOOR 1 (Bruno, 2026-10-31): the KIT TEST room shows on the map before she goes there.
            ItemAssets.Pickup(null, "Plan_TestFloor1", ItemAssets.FloorPlan(MapAtlasSetup.TestFloor1, "FLOOR PLAN: TEST FLOOR 1"), PrimitiveType.Cube, new Vector3(1f, 0.05f, -2.5f), new Vector3(0.4f, 0.03f, 0.3f), Quaternion.Euler(0f, -10f, 0f));
            Finish(b, new Vector3(4f, 6f, -6f), new Vector3(-1f, 0f, 1f));

            MenuLoopSetup.CleanBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[TravelTestSetup] built TravelTest_A and TravelTest_B. Run MapAtlasSetup.BuildAtlas for the map.");
        }

        /// <summary>A floor and four walls (0.5 m thick), a 2 m doorway in the east or the west wall at z 0.</summary>
        private static void Room(float width, float depth, bool doorEast)
        {
            GameObject root = MenuLoopSetup.EnsureRoot("[Room]");
            float hw = width * 0.5f, hd = depth * 0.5f;
            Piece(root, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(width, 0.1f, depth));
            Piece(root, "Wall_North", new Vector3(0f, 1.5f, hd + 0.25f), new Vector3(width + 1f, 3f, 0.5f));
            Piece(root, "Wall_South", new Vector3(0f, 1.5f, -hd - 0.25f), new Vector3(width + 1f, 3f, 0.5f));
            float doorX = doorEast ? hw + 0.25f : -hw - 0.25f;
            float solidX = -doorX;
            Piece(root, "Wall_Solid", new Vector3(solidX, 1.5f, 0f), new Vector3(0.5f, 3f, depth));
            float side = (depth - 2f) * 0.5f;
            Piece(root, "Wall_Door_N", new Vector3(doorX, 1.5f, 1f + side * 0.5f), new Vector3(0.5f, 3f, side));
            Piece(root, "Wall_Door_S", new Vector3(doorX, 1.5f, -1f - side * 0.5f), new Vector3(0.5f, 3f, side));
            // The corridor past the door: a floor and two side walls, so she walks into the exit.
            float corridorX = doorEast ? hw + 1.25f : -hw - 1.25f;
            Piece(root, "Corridor_Floor", new Vector3(corridorX, -0.05f, 0f), new Vector3(2f, 0.1f, 2f));
            Piece(root, "Corridor_N", new Vector3(corridorX, 1.5f, 1.25f), new Vector3(2f, 3f, 0.5f));
            Piece(root, "Corridor_S", new Vector3(corridorX, 1.5f, -1.25f), new Vector3(2f, 3f, 0.5f));
            Piece(root, "Corridor_End", new Vector3(corridorX + (doorEast ? 1.25f : -1.25f), 1.5f, 0f), new Vector3(0.5f, 3f, 2f));
            MenuLoopSetup.EnsureLight();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.31f, 0.34f);
        }

        private static void Piece(GameObject root, string name, Vector3 position, Vector3 scale)
        {
            GameObject obj = MenuLoopSetup.EnsureChildPrimitive(root, name, PrimitiveType.Cube, position, scale);
            obj.isStatic = true;
        }

        private static void Exit(string name, Vector3 position, string scene, string entry)
        {
            GameObject obj = GameObject.Find(name) ?? new GameObject(name);
            obj.transform.position = position;
            BoxCollider box = MenuLoopSetup.EnsureComponent<BoxCollider>(obj);
            box.isTrigger = true;
            box.size = new Vector3(0.6f, 2.4f, 2f);
            obj.layer = LayerMask.NameToLayer("Ignore Raycast"); // a trigger must not block a sight line (2026-10-25)
            RoomExit exit = MenuLoopSetup.EnsureComponent<RoomExit>(obj);
            exit.targetScene = scene;
            exit.targetEntry = entry;
        }

        private static void Entry(string name, Vector3 position, float yaw)
        {
            GameObject obj = GameObject.Find(name) ?? new GameObject(name);
            obj.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            MenuLoopSetup.EnsureComponent<RoomEntry>(obj);
        }

        private static void Finish(Scene scene, Vector3 cameraAt, Vector3 lookAt)
        {
            Camera camera = MenuLoopSetup.EnsureCamera(cameraAt, lookAt, 55f);
            GameObject camerasRoot = MenuLoopSetup.EnsureRoot("[Cameras]");
            FixedCameraDirector director = MenuLoopSetup.EnsureComponent<FixedCameraDirector>(camerasRoot);
            FixedCamera wide = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Wide", camera.transform.position, lookAt, 55f);
            director.SetDefaultCamera(wide);
            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
