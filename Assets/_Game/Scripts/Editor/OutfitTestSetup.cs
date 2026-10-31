#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Audio;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Story;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The OUTFIT TEST room (2026-10-31, the design sweep's rank 7: "a uniform pickup or locker, the swap to the
    /// lucy_guard model, the stats, the HUD name"; the demo plan: W6, the lockers with the guard uniform). A closed hall
    /// with a row of black lockers on the north wall (the middle one holds the uniform: OutfitLocker), six pills on the
    /// floor (the bare body carries 4, the uniform 8), a save machine, and a dark square at the east end where a script
    /// tears the uniform off (the Crown grab of U1, Bruno's round 20) and the pills over the limit fall. OutfitTests
    /// drive it. Batch: tools/unity.sh exec GlorifiedBody.Editor.OutfitTestSetup.BuildOutfitTest
    /// </summary>
    public static class OutfitTestSetup
    {
        public const string SceneName = "OutfitTest";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string Locker = "Locker_Uniform", Uniform = "Uniform_Hanger";
        public const string GrabFlag = "outfit_test_grab";
        public const string MusicKey = "outfit_test";
        public const float Width = 20f, Depth = 14f, Height = 3f;
        public static readonly Vector3 LockerFront = new Vector3(-1f, 0f, 5.7f); // where she stands to use it, facing north
        public static readonly Vector3 GrabCentre = new Vector3(7f, 0f, -2f);
        public const int Pills = 6;

        [MenuItem("GlorifiedBody/Build Outfit Test Room (a locker with the uniform, pills, the grab)")]
        public static void BuildOutfitTest()
        {
            Scene scene = MenuLoopSetup.OpenOrCreate(ScenePath);
            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog(); // after the scene opens
            MenuLoopSetup.EnsureCamera(new Vector3(9.6f, 4.5f, 0f), new Vector3(-5f, 0f, 0f), 60f);
            MenuLoopSetup.EnsureLight();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.31f, 0.34f);
            ItemAssets.CreateAll();

            float hw = Width * 0.5f, hd = Depth * 0.5f;
            MenuLoopSetup.RemoveObject("[Room]");
            GameObject room = MenuLoopSetup.EnsureRoot("[Room]");
            RoomKit.Box(room, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(Width, 0.1f, Depth));
            RoomKit.Box(room, "Wall_North", new Vector3(0f, Height * 0.5f, hd + 0.25f), new Vector3(Width + 1f, Height, 0.5f));
            RoomKit.Box(room, "Wall_South", new Vector3(0f, Height * 0.5f, -hd - 0.25f), new Vector3(Width + 1f, Height, 0.5f));
            RoomKit.Box(room, "Wall_East", new Vector3(hw + 0.25f, Height * 0.5f, 0f), new Vector3(0.5f, Height, Depth));
            RoomKit.Box(room, "Wall_West", new Vector3(-hw - 0.25f, Height * 0.5f, 0f), new Vector3(0.5f, Height, Depth));
            Material black = ItemAssets.UnlitMaterial("locker_black", new Color(0.07f, 0.07f, 0.08f));

            // The lockers: five black steel boxes on the north wall. The middle one stands open with the uniform in it.
            GameObject lockers = MenuLoopSetup.EnsureRoot("[Lockers]");
            for (int i = 0; i < 5; i++)
            {
                if (i == 2) continue;
                GameObject box = RoomKit.Box(lockers, $"Locker_{i}", new Vector3(-3f + i * 1f, 0.95f, hd - 0.3f), new Vector3(0.9f, 1.9f, 0.55f));
                box.GetComponent<Renderer>().sharedMaterial = black;
            }
            GameObject lockerObj = MenuLoopSetup.EnsureChild(lockers, Locker);
            lockerObj.transform.position = new Vector3(-1f, 0f, hd - 0.3f);
            foreach (Transform child in lockerObj.transform) Object.DestroyImmediate(child.gameObject);
            GameObject back = RoomKit.Box(lockerObj, "Back", new Vector3(0f, 0.95f, 0.25f), new Vector3(0.9f, 1.9f, 0.05f));
            back.GetComponent<Renderer>().sharedMaterial = black;
            foreach (float side in new[] { -0.43f, 0.43f })
            {
                GameObject wall = RoomKit.Box(lockerObj, side < 0f ? "Side_W" : "Side_E", new Vector3(side, 0.95f, 0f), new Vector3(0.04f, 1.9f, 0.55f));
                wall.GetComponent<Renderer>().sharedMaterial = black;
            }
            // The uniform on its hanger: a slate grey-blue body with three white bands (CONCEPTS: the guard). Unlit, so it
            // stands out as a thing she can take (Bruno, 2026-10-22). PLACEHOLDER shape.
            GameObject uniform = MenuLoopSetup.EnsureChild(lockerObj, Uniform);
            uniform.transform.localPosition = Vector3.zero;
            Material slate = ItemAssets.UnlitMaterial("uniform_slate", new Color(0.33f, 0.4f, 0.5f));
            Material band = ItemAssets.UnlitMaterial("uniform_band", new Color(0.92f, 0.93f, 0.95f));
            Part(uniform, "Torso", new Vector3(0f, 1.3f, 0.05f), new Vector3(0.48f, 0.62f, 0.14f), slate);
            Part(uniform, "Legs", new Vector3(0f, 0.68f, 0.05f), new Vector3(0.38f, 0.6f, 0.12f), slate);
            Part(uniform, "Band_1", new Vector3(0f, 1.44f, -0.025f), new Vector3(0.49f, 0.05f, 0.01f), band);
            Part(uniform, "Band_2", new Vector3(0f, 1.32f, -0.025f), new Vector3(0.49f, 0.05f, 0.01f), band);
            Part(uniform, "Band_3", new Vector3(0f, 1.05f, -0.025f), new Vector3(0.49f, 0.05f, 0.01f), band);
            Part(uniform, "Hanger", new Vector3(0f, 1.66f, 0.05f), new Vector3(0.42f, 0.03f, 0.03f), black);
            OutfitLocker locker = MenuLoopSetup.EnsureComponent<OutfitLocker>(lockerObj);
            locker.outfitId = OutfitChange.GuardId;
            locker.thing = "the guard uniform";
            locker.shown = uniform;
            locker.range = 1.6f;
            GameObject point = MenuLoopSetup.EnsureChild(lockerObj, "Point");
            point.transform.position = new Vector3(-1f, 1f, hd - 0.75f);
            locker.point = point.transform;
            EditorUtility.SetDirty(locker);

            // Six red pills in a row in the middle of the room: more than the bare body carries (4).
            GameObject pills = MenuLoopSetup.EnsureRoot("[Pills]");
            for (int i = pills.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(pills.transform.GetChild(i).gameObject);
            for (int i = 0; i < Pills; i++)
            {
                MenuLoopSetup.EnsurePickup($"Pill_{i}", DrugType.Red, new Vector3(-4f + i * 1.2f, 0.4f, 1f));
                GameObject pill = GameObject.Find($"Pill_{i}");
                if (pill != null) pill.transform.SetParent(pills.transform, true);
            }

            // The grab: a dark square at the east end. A step on it tears the uniform off (a script, once a game).
            MenuLoopSetup.RemoveObject("Grab_Zone");
            GameObject zone = MenuLoopSetup.EnsureChildPrimitive(room, "Grab_Zone", PrimitiveType.Cube, GrabCentre + new Vector3(0f, 0.005f, 0f), new Vector3(2.4f, 0.01f, 2.4f));
            if (zone.GetComponent<Collider>() != null) Object.DestroyImmediate(zone.GetComponent<Collider>());
            zone.GetComponent<Renderer>().sharedMaterial = ItemAssets.UnlitMaterial("grab_zone", new Color(0.2f, 0.02f, 0.03f));
            ScriptedSequence grab = RoomKit.Sequence("Seq_Grab", GrabFlag,
                ScriptStep.Radio("GRAB", "outfit_test_grab", "PLACEHOLDER: the grab tears the uniform off."),
                ScriptStep.Outfit(OutfitChange.NudeId));
            RoomKit.Trigger("Trigger_Grab", GrabCentre + new Vector3(0f, 1f, 0f), new Vector3(2.4f, 2f, 2.4f), grab);

            SaveAssets.SavePoint("Save_Lockers", new Vector3(-8.5f, 0f, 4f), 90f);
            // The poster of W6 (the demo plan §2.3, PROPOSED words), over the lockers.
            RoomKit.Sign(room, "Sign_HardDays", "THANK YOU FOR THE HARD DAYS.", new Vector3(-1f, 2.35f, hd - 0.02f), Vector3.back, new Vector2(2.4f, 0.4f), 56);
            MenuLoopSetup.EnsureLucy(catalog, new Vector3(-6f, 0f, -3f), 30f);
            RoomKit.Music(MusicKey);

            // Two cameras, one for each half of the hall, as in the kit room.
            GameObject camerasRoot = LookTestSetup.ResetCameras();
            FixedCameraDirector director = MenuLoopSetup.EnsureComponent<FixedCameraDirector>(camerasRoot);
            FixedCamera west = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_WestHalf", new Vector3(5f, 3.6f, -6f), new Vector3(-3f, 0.8f, 3f), 60f);
            FixedCamera east = MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_EastHalf", new Vector3(-6f, 3.6f, -6f), new Vector3(5f, 0.6f, 1f), 60f);
            director.SetDefaultCamera(west);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_West", new Vector3(-4f, 1f, 0f), new Vector3(12f, 3f, Depth), west, 1);
            MenuLoopSetup.EnsureCameraZone(camerasRoot, "Zone_East", new Vector3(6f, 1f, 0f), new Vector3(8f, 3f, Depth), east, 1);

            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();
            MenuLoopSetup.EnsureShotPoints(new[] { west, east });
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            MenuLoopSetup.CleanBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[OutfitTestSetup] built OutfitTest: the locker with the uniform, six pills, the grab zone, a save machine.");
        }

        private static void Part(GameObject parent, string name, Vector3 local, Vector3 scale, Material material)
        {
            GameObject obj = MenuLoopSetup.EnsureChildPrimitive(parent, name, PrimitiveType.Cube, local, scale);
            if (obj.GetComponent<Collider>() != null) Object.DestroyImmediate(obj.GetComponent<Collider>());
            obj.GetComponent<Renderer>().sharedMaterial = material;
            obj.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
#endif
