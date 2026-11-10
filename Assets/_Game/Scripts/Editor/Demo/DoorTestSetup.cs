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
using GlorifiedBody.Rooms;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>
    /// The DOOR TEST room (2026-11-03, Bruno: "doors should be lockable by the player as well to stop chases and breachable
    /// by the soldier and power armor enemies ... this will need a guess what? a new dev test room"). A small world of five
    /// rooms built like the demo (DemoRoom, Room, RoomTracker, one NavMesh): the hall where she starts, and four rooms
    /// off it, three behind an open fire door with a lever on each side (FireDoor, DoorLever: "a lever that closes+
    /// locks/jams the door shut"), one behind a card door (LockedDoor):
    ///   north, the barracks: two soldiers (they kick a locked door down),
    ///   east, the armour bay: the power armour (two blows), its doorway 2.9 m high,
    ///   west, the office: a guard and an armed scientist (they only bang on it),
    ///   south, the closet behind a card door: a soldier (nothing breaks a card door).
    /// The figures wait unalerted, their backs to the doors. A power, a shot or a scream calls them (Room.Hear: the rooms
    /// next door).
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.Demo.DoorTestSetup.BuildDoorTest
    /// </summary>
    public static class DoorTestSetup
    {
        public const string SceneName = "DoorTest";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string NavPath = MenuLoopSetup.ScenesFolder + "/" + SceneName + "_NavMesh.asset";
        public const string Floor = "door_test";
        public const string Hall = "D1", Barracks = "D2", ArmourBay = "D3", Office = "D4", Closet = "D5";
        public const string CardId = "door_test_card";
        public static readonly Vector3 LucyStart = new Vector3(5f, 0f, 3f);
        public const float ArmourDoorHeight = 2.9f;

        public static List<RoomSpec> Rooms() => new List<RoomSpec>
        {
            Spec(Hall, "THE HALL", 0, 0, 10, 8, 3.2f, DressHall,
                new Door('N', 5, Barracks, fire: true), new Door('E', 4, ArmourBay, fire: true, height: ArmourDoorHeight),
                new Door('W', 4, Office, fire: true), new Door('S', 5, Closet)),
            Spec(Barracks, "THE BARRACKS", 2, 8, 8, 14, 3.2f, DressBarracks, new Door('S', 5, Hall, fire: true)),
            Spec(ArmourBay, "THE ARMOUR BAY", 10, 1, 16, 7, 3.2f, DressArmourBay, new Door('W', 4, Hall, fire: true, height: ArmourDoorHeight)),
            Spec(Office, "THE OFFICE", -6, 1, 0, 7, 3.2f, DressOffice, new Door('E', 4, Hall, fire: true)),
            Spec(Closet, "THE CLOSET", 3, -4, 7, 0, 3.2f, DressCloset, new Door('N', 5, Hall)),
        };

        private static RoomSpec Spec(string id, string title, float x0, float z0, float x1, float z1, float height, System.Action<DemoRoom> dress, params Door[] doors)
        {
            var spec = new RoomSpec { Id = id, Title = title, Floor = Floor, Music = "door_test", X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Height = height, Dress = dress };
            spec.Doors.AddRange(doors);
            return spec;
        }

        [MenuItem("GlorifiedBody/Build Door Test Room (lock, breach, bang)")]
        public static void BuildDoorTest()
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
            EditorSceneManager.SaveScene(scene, ScenePath);
            MenuLoopSetup.CleanBuildSettings();
            AssetDatabase.SaveAssets();
            int doors = Object.FindObjectsByType<Interaction.FireDoor>(FindObjectsSortMode.None).Length;
            Debug.Log($"[DoorTestSetup] built {ScenePath}: {rooms.Count} rooms, {doors} fire doors, {rooms.Values.Sum(r => r.Cameras.Count)} cameras");
        }

        // ── The rooms ─────────────────────────────────────────────────

        private static void DressHall(DemoRoom r)
        {
            r.PlaceLucy(LucyStart, 0f);
            r.LockedLeaf(r.DoorTo(Closet), CardId, "the red card");
            r.Sign("Sign_Rules", "E ON A LEVER: JAM THE FIRE DOOR", new Vector3(1.6f, 2.2f, 7.84f), Vector3.back, new Vector2(2.6f, 0.35f), 40);
            r.Sign("Sign_North", "SOLDIERS: THEY KICK IT DOWN", new Vector3(7.6f, 2.55f, 7.84f), Vector3.back, new Vector2(2.6f, 0.35f), 40);
            r.Sign("Sign_East", "POWER ARMOUR: TWO BLOWS", new Vector3(9.84f, 2.55f, 6.4f), Vector3.left, new Vector2(2.4f, 0.35f), 40);
            r.Sign("Sign_West", "GUARD AND SCIENTIST: THEY BANG", new Vector3(0.16f, 2.55f, 6.4f), Vector3.right, new Vector2(2.6f, 0.35f), 40);
            r.Sign("Sign_South", "CARD DOOR: NOTHING BREAKS IT", new Vector3(7.6f, 2.55f, 0.16f), Vector3.forward, new Vector2(2.6f, 0.35f), 40);
            // Two planters (DEMO_PLAN §10.5): one in a doorway holds it against guards, not soldiers (DoorStop).
            r.Planter("Planter_A", new Vector3(1.6f, 0f, 1.4f), PlanterSize);
            r.Planter("Planter_B", new Vector3(8.4f, 0f, 1.4f), PlanterSize);
        }

        public static readonly Vector3 PlanterSize = new Vector3(1.2f, 0.95f, 1.2f);

        private static void DressBarracks(DemoRoom r)
        {
            r.Figure("Soldier_Rifle", "soldier_rifle", new Vector3(3.8f, 0f, 12.4f), 0f);
            r.Figure("Soldier_Pistol", "soldier_pistol", new Vector3(6.3f, 0f, 12.8f), 0f);
            r.Prop("Bunk_A", new Vector3(2.9f, 0f, 10.5f), new Vector3(0.9f, 0.6f, 2f));
            r.Prop("Bunk_B", new Vector3(7.1f, 0f, 10.5f), new Vector3(0.9f, 0.6f, 2f));
        }

        private static void DressArmourBay(DemoRoom r)
        {
            r.Figure("Armour", "power_armour", new Vector3(14f, 0f, 4f), 90f);
            r.Prop("Rack", new Vector3(15.4f, 0f, 1.8f), new Vector3(0.8f, 2f, 1.2f), 0.26f);
        }

        private static void DressOffice(DemoRoom r)
        {
            r.Figure("Guard", "guard_pistol", new Vector3(-3f, 0f, 5.3f), 270f);
            r.Figure("Scientist", "labworker_pistol", new Vector3(-4.2f, 0f, 2.6f), 270f, variant: 0);
            r.Prop("Desk", new Vector3(-4.5f, 0f, 4f), new Vector3(1.4f, 0.75f, 0.7f));
        }

        private static void DressCloset(DemoRoom r)
        {
            r.Figure("Closet_Soldier", "soldier_rifle", new Vector3(5f, 0f, -2.6f), 0f);
        }
    }
}
#endif
