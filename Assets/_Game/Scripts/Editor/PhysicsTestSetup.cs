#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Interaction;
using GlorifiedBody.Story;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The PHYSICS TEST room (2026-11-06, Bruno: "the tk'd object should be able to be moved with a mouse. this will allow us to do
    /// physics puzzles with boxes and whatnot"). A low rail she cannot cross splits the hall. A plate lies on the far side, and two
    /// boxes on her side. A box on the plate holds the gate in the east partition open. PhysicsTests drive it.
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.PhysicsTestSetup.BuildPhysicsTest
    /// </summary>
    public static class PhysicsTestSetup
    {
        public const string SceneName = "PhysicsTest";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string PlateFlag = "physics_test_plate";
        public const float Width = 16f, Depth = 12f, Height = 3.5f;
        public const float RailZ = 1.5f, RailHeight = 1.2f, GateX = 5f, GateZ = -2.5f;
        public static readonly Vector3 PlatePoint = new Vector3(-2f, 0f, 3.6f);
        public static readonly Vector3 HerPoint = new Vector3(-3f, 0f, -3f);
        public static readonly Vector3 BoxA = new Vector3(-3f, 0f, -0.8f), BoxB = new Vector3(-1f, 0f, -1.2f);

        [MenuItem("GlorifiedBody/Build Physics Test Room (boxes, a plate, a gate)")]
        public static void BuildPhysicsTest()
        {
            Scene scene = MenuLoopSetup.OpenOrCreate(ScenePath);
            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog(); // after the scene opens
            MenuLoopSetup.EnsureCamera(new Vector3(0f, 6f, -9f), new Vector3(0f, 0f, 1f), 60f);
            MenuLoopSetup.EnsureLight();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.33f, 0.36f);

            float hw = Width * 0.5f, hd = Depth * 0.5f;
            MenuLoopSetup.RemoveObject("[Room]");
            GameObject room = MenuLoopSetup.EnsureRoot("[Room]");
            RoomKit.Box(room, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(Width, 0.1f, Depth));
            RoomKit.Box(room, "Wall_North", new Vector3(0f, Height * 0.5f, hd + 0.25f), new Vector3(Width + 1f, Height, 0.5f));
            RoomKit.Box(room, "Wall_South", new Vector3(0f, Height * 0.5f, -hd - 0.25f), new Vector3(Width + 1f, Height, 0.5f));
            RoomKit.Box(room, "Wall_East", new Vector3(hw + 0.25f, Height * 0.5f, 0f), new Vector3(0.5f, Height, Depth));
            RoomKit.Box(room, "Wall_West", new Vector3(-hw - 0.25f, Height * 0.5f, 0f), new Vector3(0.5f, Height, Depth));
            RoomKit.Box(room, "Rail", new Vector3(0f, RailHeight * 0.5f, RailZ), new Vector3(Width, RailHeight, 0.3f));
            // The partition east of her half, from the south wall to the rail, with the gate in it.
            float southLength = GateZ - 0.8f + hd, northLength = RailZ - (GateZ + 0.8f);
            RoomKit.Box(room, "Partition_South", new Vector3(GateX, Height * 0.5f, -hd + southLength * 0.5f), new Vector3(0.3f, Height, southLength));
            RoomKit.Box(room, "Partition_North", new Vector3(GateX, Height * 0.5f, RailZ - northLength * 0.5f), new Vector3(0.3f, Height, northLength));
            GameObject gate = MenuLoopSetup.EnsureChild(room, "Gate");
            FlagGate flagGate = MenuLoopSetup.EnsureComponent<FlagGate>(gate);
            flagGate.flag = PlateFlag;
            flagGate.showWhenRaised = false;
            GameObject leaf = RoomKit.Box(gate, "Gate_Leaf", new Vector3(GateX, Height * 0.5f, GateZ), new Vector3(0.2f, Height, 1.6f));
            leaf.GetComponent<Renderer>().sharedMaterial = ItemAssets.UnlitMaterial("physics_gate", new Color(0.55f, 0.12f, 0.1f));
            RoomKit.Sign(room, "Sign_Through", "THE WAY ON.", new Vector3(hw - 0.02f, 1.6f, GateZ), Vector3.left, new Vector2(2f, 0.4f), 56);

            MenuLoopSetup.RemoveObject("[Puzzle]");
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
            PropAssets.Box("Box_A", BoxA).transform.SetParent(puzzle.transform, true);
            PropAssets.Box("Box_B", BoxB).transform.SetParent(puzzle.transform, true);

            MenuLoopSetup.EnsureLucy(catalog, HerPoint, 0f);
            GameObject camerasRoot = LookTestSetup.ResetCameras();
            FixedCameraDirector director = MenuLoopSetup.EnsureComponent<FixedCameraDirector>(camerasRoot);
            FixedCamera hall = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Hall", new Vector3(-1f, 6f, -6f), new Vector3(-1f, 0f, 1.5f), 60f);
            director.SetDefaultCamera(hall);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Hall", new Vector3(0f, 1f, 0f), new Vector3(Width, 3f, Depth), hall, 1);

            RoomKit.BakeNavMesh(room, MenuLoopSetup.ScenesFolder + "/PhysicsTest_NavMesh.asset");
            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();
            MenuLoopSetup.EnsureShotPoints(new[] { hall });
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            MenuLoopSetup.CleanBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[PhysicsTestSetup] built PhysicsTest: a rail, a plate, two boxes, a gate.");
        }
    }
}
#endif
