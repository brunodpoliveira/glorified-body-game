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
    /// Bruno, 2026-11-08: "the code rotation puzzle is another backtracker; so the player needs to make progress by grabbing
    /// the card, using it to unlock some previously unlocked doors, then the code is rotated so the player needs to go to a
    /// security office and reset the card's password to the code on the wall". Six rooms built like the demo:
    ///   the hall, where she starts: the card on a table, three card doors (store A, store B, the way on), each with a
    ///   reader; the card opens two of them, then the PA call 1B rotates the codes (CodeRotation), and the third refuses;
    ///   north, store A and store B behind their card doors (store A's shelf holds an MK capsule, store B's is empty: the
    ///   common interaction);
    ///   south, the way on behind its card door;
    ///   east, the security post: the terminal and its keypad (seven digits);
    ///   west, the day room: the code on the wall, 8 6 7 5 3 0 9.
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.Demo.CodeTestSetup.BuildCodeTest
    /// </summary>
    public static class CodeTestSetup
    {
        public const string SceneName = "CodeTest";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string NavPath = MenuLoopSetup.ScenesFolder + "/" + SceneName + "_NavMesh.asset";
        public const string Floor = "code_test";
        public const string Hall = "K1", StoreA = "K2", Post = "K3", DayRoom = "K4", StoreB = "K5", WayOn = "K6";
        public const string CardId = "code_test_card";
        public const string Code = "8675309"; // Bruno, 2026-11-08
        public const int OpensBeforeRotation = 2;
        public const string Rotated = "code_test_rotated", Enrolled = "code_test_enrolled";
        public static readonly Vector3 LucyStart = new Vector3(5f, 0f, 4f);

        public static List<RoomSpec> Rooms() => new List<RoomSpec>
        {
            Spec(Hall, "THE HALL", 0, 0, 10, 8, DressHall, new Door('N', 2.5f, StoreA), new Door('N', 7.5f, StoreB), new Door('S', 5, WayOn),
                new Door('E', 4, Post), new Door('W', 4, DayRoom)),
            Spec(StoreA, "STORE A", 0, 8, 5, 12, r => DressStore(r, 2.5f, true), new Door('S', 2.5f, Hall)),
            Spec(StoreB, "STORE B", 5, 8, 10, 12, r => DressStore(r, 7.5f, false), new Door('S', 7.5f, Hall)),
            Spec(WayOn, "THE WAY ON", 3, -4, 7, 0, DressWayOn, new Door('N', 5, Hall)),
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
            r.Prop("Table", new Vector3(1.6f, 0f, 1.0f), new Vector3(1.0f, 0.75f, 0.6f), 0.3f);
            r.Item("Card", Card(), new Vector3(1.6f, 0.8f, 1.0f), new Vector3(0.12f, 0.01f, 0.08f));
            // Three card doors, each with its reader on the hall side.
            CardDoor(r, StoreA, new Vector3(3.8f, 1.2f, 7.82f), Vector3.back);
            CardDoor(r, StoreB, new Vector3(8.8f, 1.2f, 7.82f), Vector3.back);
            CardDoor(r, WayOn, new Vector3(6.3f, 1.2f, 0.18f), Vector3.forward).rotatesOnTry = true;
            var rotation = new GameObject("Code_Rotation").AddComponent<CodeRotation>();
            rotation.transform.SetParent(r.Props.transform, false);
            rotation.cardItemId = CardId;
            rotation.rotatedFlag = Rotated;
            rotation.opensBeforeRotation = OpensBeforeRotation;
            rotation.paLine = "Site codes rotate at this time. Re-enrol your card at a security station."; // the fallback with no writer file
            rotation.paLines = DemoScripts.Lines("lines/T5_pa.md", "Call 1C", "*").ConvertAll(l => l.text).ToArray(); // round 138: "re-enroll"
            EditorUtility.SetDirty(rotation);
            r.Sign("Sign_Rules", "THE CARD OPENS TWO DOORS. THEN THE CODES ROTATE", new Vector3(5f, 2.6f, 7.84f), Vector3.back, new Vector2(4.4f, 0.32f), 40);
            r.Sign("Sign_WayOn", "THE WAY ON", new Vector3(8f, 2.3f, 0.16f), Vector3.forward, new Vector2(1.6f, 0.35f), 40);
            r.Sign("Sign_East", "SECURITY POST", new Vector3(9.84f, 2.55f, 5.6f), Vector3.left, new Vector2(1.8f, 0.35f), 40);
            r.Sign("Sign_West", "DAY ROOM", new Vector3(0.16f, 2.55f, 5.6f), Vector3.right, new Vector2(1.4f, 0.35f), 40);
        }

        private static LockedDoor CardDoor(DemoRoom r, string to, Vector3 readerAt, Vector3 facing)
        {
            GameObject door = r.LockedLeaf(r.DoorTo(to), CardId, "the test card");
            return r.CodeLock(door, readerAt, facing, Rotated, Enrolled);
        }

        // The common interaction (2026-11-08): the shelf of store A holds an MK capsule, the shelf of store B is empty.
        private static void DressStore(DemoRoom r, float x, bool capsule)
        {
            if (capsule) r.Container("Shelf", new Vector3(x, 0f, 11.5f), new Vector3(2f, 1.6f, 0.4f), 0.26f, ItemAssets.Drug(DrugType.Nalcon));
            else r.Prop("Shelf", new Vector3(x, 0f, 11.5f), new Vector3(2f, 1.6f, 0.4f), 0.26f);
        }

        private static void DressWayOn(DemoRoom r)
        {
            r.Prop("Crate", new Vector3(5f, 0f, -3.3f), new Vector3(1.2f, 0.9f, 0.8f), 0.2f);
        }

        private static void DressPost(DemoRoom r)
        {
            r.Terminal("Terminal", new Vector3(12.5f, 0f, 6.3f), Vector3.back, CardId, Code, Rotated, Enrolled);
            r.Prop("Chair", new Vector3(12.5f, 0f, 5.2f), new Vector3(0.5f, 0.9f, 0.5f), 0.15f);
        }

        private static void DressDayRoom(DemoRoom r)
        {
            r.Sign("Sign_Code", "8 6 7 5 3 0 9", new Vector3(-2.5f, 1.4f, 6.83f), Vector3.back, new Vector2(1.5f, 0.25f), 44);
        }
    }
}
#endif
