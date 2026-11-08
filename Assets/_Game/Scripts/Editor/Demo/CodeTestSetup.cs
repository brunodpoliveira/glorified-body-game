#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Rooms;
using GlorifiedBody.Story;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>
    /// The CODE TEST room (2026-11-08): the code rotation of DEMO_PLAN §10.2 in isolation, before W12, W13 and W14 get it.
    /// Four rooms built like the demo:
    ///   the hall, where she starts: the card on a table, a switch that plays the PA call 1B (the codes rotate), the card
    ///   door to the closet with its reader;
    ///   east, the security post: the terminal and its keypad;
    ///   west, the day room: the code on the wall, 5 2 8 1;
    ///   north, the closet behind the card door.
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.Demo.CodeTestSetup.BuildCodeTest
    /// </summary>
    public static class CodeTestSetup
    {
        public const string SceneName = "CodeTest";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string NavPath = MenuLoopSetup.ScenesFolder + "/" + SceneName + "_NavMesh.asset";
        public const string Floor = "code_test";
        public const string Hall = "K1", Closet = "K2", Post = "K3", DayRoom = "K4";
        public const string CardId = "code_test_card";
        public const string Code = "5281";
        public const string Rotated = "code_test_rotated", Enrolled = "code_test_enrolled";
        public static readonly Vector3 LucyStart = new Vector3(5f, 0f, 2f);

        public static List<RoomSpec> Rooms() => new List<RoomSpec>
        {
            Spec(Hall, "THE HALL", 0, 0, 10, 8, DressHall, new Door('N', 5, Closet), new Door('E', 4, Post), new Door('W', 4, DayRoom)),
            Spec(Closet, "THE CLOSET", 3, 8, 7, 12, DressCloset, new Door('S', 5, Hall)),
            Spec(Post, "THE SECURITY POST", 10, 1, 15, 7, DressPost, new Door('W', 4, Hall)),
            Spec(DayRoom, "THE DAY ROOM", -5, 1, 0, 7, DressDayRoom, new Door('E', 4, Hall)),
        };

        private static RoomSpec Spec(string id, string title, float x0, float z0, float x1, float z1, System.Action<DemoRoom> dress, params Door[] doors)
        {
            var spec = new RoomSpec { Id = id, Title = title, Floor = Floor, Music = "code_test", X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Height = 3.2f, Dress = dress };
            spec.Doors.AddRange(doors);
            return spec;
        }

        public static ItemDefinition Card() =>
            ItemAssets.Item(CardId, "THE TEST CARD", ItemKind.Key, ItemAssets.KeyColour, "A white card. It opens the closet, when its code is current.", "", DrugType.Nalcon);

        [MenuItem("GlorifiedBody/Build Code Test Room (card, rotated code, terminal)")]
        public static void BuildCodeTest()
        {
            Directory.CreateDirectory(MenuLoopSetup.ScenesFolder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.32f, 0.35f);
            var sun = new GameObject("Light");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.9f;
            light.shadows = LightShadows.Hard;
            sun.transform.rotation = Quaternion.Euler(55f, 30f, 0f);
            var world = new GameObject("[World]");
            world.AddComponent<RoomTracker>();
            DemoRoom.Lucy = MenuLoopSetup.EnsureLucy(catalog, LucyStart, 0f);

            List<RoomSpec> specs = Rooms();
            var ledger = new WallLedger(specs);
            var rooms = new Dictionary<string, DemoRoom>();
            foreach (RoomSpec spec in specs) rooms[spec.Id] = DemoWorld.BuildOne(scene, world, spec, catalog, ledger);
            foreach (DemoRoom r in rooms.Values) r.Root.SetActive(true);
            Physics.SyncTransforms();
            DemoWorld.Neighbours(rooms);

            GameObject director = MenuLoopSetup.EnsureChild(world, "[CameraDirector]");
            MenuLoopSetup.EnsureComponent<FixedCameraDirector>(director).SetDefaultCamera(rooms[Hall].Cameras[0]);
            MenuLoopSetup.EnsureCamera(new Vector3(5f, 3f, -1f), new Vector3(5f, 1f, 4f), 60f);
            RoomKit.BakeNavMesh(world, NavPath);
            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();
            DemoWorld.Sight(world);
            EditorSceneManager.SaveScene(scene, ScenePath);
            MenuLoopSetup.CleanBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[CodeTestSetup] built {ScenePath}: {rooms.Count} rooms, {rooms.Values.Sum(r => r.Cameras.Count)} cameras");
        }

        // ── The rooms ─────────────────────────────────────────────────

        private static void DressHall(DemoRoom r)
        {
            r.PlaceLucy(LucyStart, 0f);
            r.Prop("Table", new Vector3(2f, 0f, 6.3f), new Vector3(1.0f, 0.75f, 0.6f), 0.3f);
            r.Item("Card", Card(), new Vector3(2f, 0.8f, 6.3f), new Vector3(0.12f, 0.01f, 0.08f));
            GameObject door = r.LockedLeaf(r.DoorTo(Closet), CardId, "the test card");
            r.CodeLock(door, new Vector3(6.3f, 1.2f, 7.82f), Vector3.back, Rotated, Enrolled);
            FlagSwitch pa = r.Switch("PA_Call_1B", new Vector3(8.5f, 1.3f, 0.2f), Rotated, "Play the PA call 1B? The site codes rotate.", "play the PA call",
                "Site codes rotate at this time. Re-enrol your card at a security station."); // the writer's call 1B (T5_pa.md)
            pa.doneSpeaker = "PA";
            EditorUtility.SetDirty(pa);
            r.Sign("Sign_Rules", "CARD, THEN THE PA, THEN THE DOOR", new Vector3(5f, 2.55f, 0.16f), Vector3.forward, new Vector2(3f, 0.35f), 40);
            r.Sign("Sign_East", "SECURITY POST", new Vector3(9.84f, 2.55f, 5.6f), Vector3.left, new Vector2(1.8f, 0.35f), 40);
            r.Sign("Sign_West", "DAY ROOM", new Vector3(0.16f, 2.55f, 5.6f), Vector3.right, new Vector2(1.4f, 0.35f), 40);
        }

        private static void DressCloset(DemoRoom r)
        {
            r.Prop("Shelf", new Vector3(5f, 0f, 11.5f), new Vector3(2f, 1.6f, 0.4f), 0.26f);
        }

        private static void DressPost(DemoRoom r)
        {
            r.Terminal("Terminal", new Vector3(12.5f, 0f, 6.3f), Vector3.back, CardId, Code, Rotated, Enrolled);
            r.Prop("Chair", new Vector3(12.5f, 0f, 5.2f), new Vector3(0.5f, 0.9f, 0.5f), 0.15f);
        }

        private static void DressDayRoom(DemoRoom r)
        {
            r.Sign("Sign_Code", "5 2 8 1", new Vector3(-2.5f, 1.4f, 6.83f), Vector3.back, new Vector2(0.8f, 0.25f), 44);
        }
    }
}
#endif
