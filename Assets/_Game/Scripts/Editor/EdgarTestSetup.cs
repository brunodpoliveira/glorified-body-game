#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Story;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The EDGAR TEST room (2026-11-06, Bruno: "this is the last core gameplay element that needs an isolated test before being implemented
    /// in the final game"). A hall with a soldier, a guard, a lab worker and a boss. Behind a low rail she cannot cross: a guard marked for
    /// the puzzle (it can be taken at full HP) and a switch that opens the gate. Capsules on the floor. The room unlocks mk_edgar.
    /// The figures stay passive until a switch beside her start wakes them (Interact).
    /// EdgarTests drive it. Local until the enemy prefabs are built: tools/unity.sh exec GlorifiedBody.Editor.EdgarTestSetup.BuildEdgarTest
    /// </summary>
    public static class EdgarTestSetup
    {
        public const string SceneName = "EdgarTest";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string SwitchFlag = "edgar_test_switch";
        public const string AwakeFlag = "edgar_test_awake"; // the figures stay passive until the switch (Bruno, 2026-11-07)
        public const float Width = 24f, Depth = 16f, Height = 3.5f;
        public const float RailZ = 3f, GateX = 8f, GateZ = 6f;

        [MenuItem("GlorifiedBody/Build Edgar Test Room (figures, a boss, a puzzle guard, a switch)")]
        public static void BuildEdgarTest()
        {
            Scene scene = MenuLoopSetup.OpenOrCreate(ScenePath);
            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog(); // after the scene opens
            MenuLoopSetup.EnsureCamera(new Vector3(0f, 8f, -12f), new Vector3(0f, 0f, 0f), 60f);
            MenuLoopSetup.EnsureLight();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.33f, 0.36f);
            ItemAssets.CreateAll();

            float hw = Width * 0.5f, hd = Depth * 0.5f;
            MenuLoopSetup.RemoveObject("[Room]");
            GameObject room = MenuLoopSetup.EnsureRoot("[Room]");
            RoomKit.Box(room, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(Width, 0.1f, Depth));
            RoomKit.Box(room, "Wall_North", new Vector3(0f, Height * 0.5f, hd + 0.25f), new Vector3(Width + 1f, Height, 0.5f));
            RoomKit.Box(room, "Wall_South", new Vector3(0f, Height * 0.5f, -hd - 0.25f), new Vector3(Width + 1f, Height, 0.5f));
            RoomKit.Box(room, "Wall_East", new Vector3(hw + 0.25f, Height * 0.5f, 0f), new Vector3(0.5f, Height, Depth));
            RoomKit.Box(room, "Wall_West", new Vector3(-hw - 0.25f, Height * 0.5f, 0f), new Vector3(0.5f, Height, Depth));
            RoomKit.Box(room, "Rail", new Vector3(0f, 0.5f, RailZ), new Vector3(Width, 1f, 0.3f));
            // The far side: a partition with the gate, east of the switch.
            GameObject gate = MenuLoopSetup.EnsureChild(room, "Gate");
            FlagGate flagGate = MenuLoopSetup.EnsureComponent<FlagGate>(gate);
            flagGate.flag = SwitchFlag;
            flagGate.showWhenRaised = false;
            RoomKit.Box(room, "Partition_North", new Vector3(GateX, Height * 0.5f, (hd + GateZ + 0.8f) * 0.5f), new Vector3(0.3f, Height, hd - GateZ - 0.8f));
            RoomKit.Box(room, "Partition_South", new Vector3(GateX, Height * 0.5f, (GateZ - 0.8f + RailZ) * 0.5f), new Vector3(0.3f, Height, GateZ - 0.8f - RailZ));
            GameObject leaf = RoomKit.Box(gate, "Gate_Leaf", new Vector3(GateX, Height * 0.5f, GateZ), new Vector3(0.2f, Height, 1.6f));
            leaf.GetComponent<Renderer>().sharedMaterial = ItemAssets.UnlitMaterial("physics_gate", new Color(0.55f, 0.12f, 0.1f));

            MenuLoopSetup.RemoveObject("[Puzzle]");
            GameObject puzzle = MenuLoopSetup.EnsureRoot("[Puzzle]");
            GameObject lever = MenuLoopSetup.EnsureChild(puzzle, "Switch_Far");
            lever.transform.position = new Vector3(4f, 0f, 7.5f);
            GameObject post = MenuLoopSetup.EnsureChildPrimitive(lever, "Post", PrimitiveType.Cube, new Vector3(0f, 0.6f, 0f), new Vector3(0.3f, 1.2f, 0.2f));
            post.GetComponent<Renderer>().sharedMaterial = ItemAssets.UnlitMaterial("edgar_switch", new Color(0.8f, 0.2f, 0.15f));
            GameObject stand = MenuLoopSetup.EnsureChild(lever, "Stand");
            stand.transform.position = new Vector3(4f, 0f, 6.6f);
            SentSwitch sent = MenuLoopSetup.EnsureComponent<SentSwitch>(lever);
            sent.flag = SwitchFlag;
            sent.point = stand.transform;
            sent.lamp = post.GetComponent<Renderer>();
            EditorUtility.SetDirty(sent);
            MenuLoopSetup.EnsurePickup("Capsule_Edgar_A", DrugType.NalconMind, new Vector3(-10f, 0.4f, -6f));
            MenuLoopSetup.EnsurePickup("Capsule_Edgar_B", DrugType.NalconMind, new Vector3(-9f, 0.4f, -6f));

            MenuLoopSetup.RemoveObject("[Figures]");
            GameObject figures = MenuLoopSetup.EnsureRoot("[Figures]");
            RoomKit.Spawn(figures, "Soldier_A", "soldier_rifle", new Vector3(-2f, 0f, -2f), 180f);
            RoomKit.Spawn(figures, "Guard_B", "guard_pistol", new Vector3(3f, 0f, -2f), 180f);
            RoomKit.Spawn(figures, "Worker_C", "labworker", new Vector3(-7f, 0f, 0f), 180f, 0);
            EnemySpawn boss = RoomKit.Spawn(figures, "Boss_D", "power_armour", new Vector3(8f, 0f, -4f), 270f);
            boss.boss = true;
            EnemySpawn farGuard = RoomKit.Spawn(figures, "Guard_Far", "guard_pistol", new Vector3(-2f, 0f, 6f), 180f);
            farGuard.dominableUnhurt = true;
            foreach (EnemySpawn spawn in figures.GetComponentsInChildren<EnemySpawn>())
            {
                spawn.passiveUntilFlag = AwakeFlag;
                EditorUtility.SetDirty(spawn);
            }

            // Bruno, 2026-11-07: "i get my shit rocked in before i can do anything. please disable the ai or something".
            GameObject wake = RoomKit.Box(room, "Switch_Wake", new Vector3(-6.5f, 0.6f, -4f), new Vector3(0.3f, 1.2f, 0.3f));
            wake.GetComponent<Renderer>().sharedMaterial = ItemAssets.UnlitMaterial("edgar_wake", new Color(0.85f, 0.7f, 0.2f));
            FlagSwitch wakeSwitch = MenuLoopSetup.EnsureComponent<FlagSwitch>(wake);
            wakeSwitch.flag = AwakeFlag;
            wakeSwitch.question = "Wake the figures?";
            wakeSwitch.hint = "wake the figures";
            wakeSwitch.doneText = "THE FIGURES ARE AWAKE";
            wakeSwitch.range = 1.6f;
            EditorUtility.SetDirty(wakeSwitch);
            RoomKit.Sign(room, "Sign_Wake", "AI OFF. SWITCH WAKES THEM", new Vector3(-6.5f, 1.55f, -4.2f), Vector3.back, new Vector2(2.6f, 0.4f));

            // The room unlocks the power, as the story will.
            ScriptedSequence unlock = RoomKit.Sequence("Seq_Unlock", "edgar_test_unlock", ScriptStep.Raise(PsychicPowers.EdgarFlag));
            unlock.runOnStart = true;
            EditorUtility.SetDirty(unlock);

            MenuLoopSetup.EnsureLucy(catalog, new Vector3(-4f, 0f, -5f), 0f);
            GameObject camerasRoot = LookTestSetup.ResetCameras();
            FixedCameraDirector director = MenuLoopSetup.EnsureComponent<FixedCameraDirector>(camerasRoot);
            FixedCamera hall = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Hall", new Vector3(0f, 8f, -9f), new Vector3(0f, 0f, 1f), 60f);
            director.SetDefaultCamera(hall);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_Hall", new Vector3(0f, 1f, 0f), new Vector3(Width, 3f, Depth), hall, 1);

            RoomKit.BakeNavMesh(room, MenuLoopSetup.ScenesFolder + "/EdgarTest_NavMesh.asset");
            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();
            MenuLoopSetup.EnsureShotPoints(new[] { hall });
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            MenuLoopSetup.CleanBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[EdgarTestSetup] built EdgarTest: figures, a boss, a puzzle guard behind a rail, a switch, capsules.");
        }
    }
}
#endif
