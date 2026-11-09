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
using GlorifiedBody.Rendering;
using GlorifiedBody.Rooms;
using GlorifiedBody.Story;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>
    /// The DARK TEST room (2026-11-08): the dark room of DEMO_PLAN §10.1 in isolation, before G9 and G9b get it. Three rooms
    /// built like the demo:
    ///   the hall, where she starts, lit, with the IT note on the wall;
    ///   north, the office: the monitor wall (six screens, each with a light on the dark layer) and the release switch;
    ///   east of the office, the closet: three UPS units, NET, REC and ACCESS.
    /// She enters the office: the office and the closet go dark, the monitors go black, the units beep.
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.Demo.DarkTestSetup.BuildDarkTest
    /// </summary>
    public static class DarkTestSetup
    {
        public const string SceneName = "DarkTest";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string NavPath = MenuLoopSetup.ScenesFolder + "/" + SceneName + "_NavMesh.asset";
        public const string Floor = "dark_test";
        public const string Hall = "Q1", Office = "Q2", Closet = "Q3";
        public const string DarkFlag = "dark_test_dark", DoneFlag = "dark_test_units_up", Released = "dark_test_released";
        public static readonly Vector3 LucyStart = new Vector3(4f, 0f, 1.5f);
        public static readonly string[] Labels = { "NET", "REC", "ACCESS" };
        // The units stand in the shelf order REC, ACCESS, NET: the order of the shelf is not the order to start them.
        private static readonly string[] ShelfOrder = { "REC", "ACCESS", "NET" };
        private static readonly float[] Pitches = { 0.8f, 1.0f, 1.3f };

        public static List<RoomSpec> Rooms() => new List<RoomSpec>
        {
            Spec(Hall, "THE HALL", 0, 0, 8, 6, DressHall, new Door('N', 4, Office)),
            Spec(Office, "THE OFFICE", 0, 6, 10, 14, DressOffice, new Door('S', 4, Hall), new Door('E', 10, Closet)),
            Spec(Closet, "THE COMMS CLOSET", 10, 8.5f, 13, 11.5f, DressCloset, new Door('W', 10, Office)),
        };

        private static RoomSpec Spec(string id, string title, float x0, float z0, float x1, float z1, System.Action<DemoRoom> dress, params Door[] doors)
        {
            var spec = new RoomSpec { Id = id, Title = title, Floor = Floor, Music = "dark_test", X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Height = 3.2f, Dress = dress };
            spec.Doors.AddRange(doors);
            return spec;
        }

        [MenuItem("GlorifiedBody/Build Dark Test Room (lights out, the UPS order)")]
        public static void BuildDarkTest()
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
            light.renderingLayerMask = (int)Darkness.LitLayer; // a dark room leaves the sun
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
            Wire(rooms[Office], rooms[Closet]);

            GameObject director = MenuLoopSetup.EnsureChild(world, "[CameraDirector]");
            MenuLoopSetup.EnsureComponent<FixedCameraDirector>(director).SetDefaultCamera(rooms[Hall].Cameras[0]);
            MenuLoopSetup.EnsureCamera(new Vector3(4f, 3f, -1f), new Vector3(4f, 1f, 3f), 60f);
            RoomKit.BakeNavMesh(world, NavPath);
            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();
            DemoWorld.Sight(world);
            EditorSceneManager.SaveScene(scene, ScenePath);
            MenuLoopSetup.CleanBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[DarkTestSetup] built {ScenePath}: {rooms.Count} rooms, {rooms.Values.Sum(r => r.Cameras.Count)} cameras");
        }

        /// <summary>The office and the closet go dark together; the bank holds the units and the monitors.</summary>
        private static void Wire(DemoRoom office, DemoRoom closet)
        {
            DarkRoom dark = MenuLoopSetup.EnsureComponent<DarkRoom>(office.Root);
            dark.darkFlag = DarkFlag;
            dark.litFlag = DoneFlag; // the units up: the lights come back for good
            dark.alsoDark = new[] { closet.Root.GetComponent<Room>() };
            EditorUtility.SetDirty(dark);
            GameObject holder = MenuLoopSetup.EnsureChild(office.Story, "UPS_Bank");
            UpsBank bank = MenuLoopSetup.EnsureComponent<UpsBank>(holder);
            bank.darkRoom = dark;
            bank.doneFlag = DoneFlag;
            bank.order = Labels;
            bank.units = Labels.Select(l => closet.Props.transform.Find("UPS_" + l).GetComponent<UpsUnit>()).ToArray();
            foreach (UpsUnit u in bank.units) { u.bank = bank; EditorUtility.SetDirty(u); }
            var screens = new List<Renderer>();
            var lights = new List<Light>();
            for (int i = 0; i < 6; i++)
            {
                Transform m = office.Props.transform.Find("Monitor_" + i);
                screens.Add(m.GetComponent<Renderer>());
                lights.Add(m.GetComponentInChildren<Light>(true));
            }
            bank.monitors = screens.ToArray();
            bank.monitorLights = lights.ToArray();
            EditorUtility.SetDirty(bank);
        }

        // ── The rooms ─────────────────────────────────────────────────

        private static void DressHall(DemoRoom r)
        {
            r.PlaceLucy(LucyStart, 0f);
            r.Sign("Sign_Note", "AFTER AN OUTAGE: NET. THEN REC. THEN ACCESS. NEVER ACCESS FIRST. - T.", new Vector3(1.4f, 1.5f, 5.84f), Vector3.back, new Vector2(2.4f, 0.5f), 30);
            r.Sign("Sign_Rules", "THE OFFICE GOES DARK. THE UNITS ARE IN THE CLOSET", new Vector3(4f, 2.55f, 0.16f), Vector3.forward, new Vector2(3.4f, 0.35f), 40);
        }

        private static void DressOffice(DemoRoom r)
        {
            r.Prop("Console", new Vector3(5f, 0f, 11.6f), new Vector3(3.2f, 0.8f, 0.8f), 0.22f);
            r.Prop("Chair", new Vector3(5f, 0f, 10.6f), new Vector3(0.5f, 0.9f, 0.5f), 0.15f);
            r.Prop("Desk_West", new Vector3(1.4f, 0f, 9f), new Vector3(1.4f, 0.75f, 2.2f), 0.25f);
            // The monitor wall: two rows of three on the north wall, each with its light on the dark layer.
            for (int i = 0; i < 6; i++)
            {
                Vector3 at = new Vector3(3.2f + (i % 3) * 1.8f, i < 3 ? 2.2f : 1.45f, 13.8f);
                GameObject screen = r.Box(r.Props, "Monitor_" + i, at, new Vector3(1.5f, 0.62f, 0.08f), DemoRoom.Colour("monitor_screen", Color.white));
                screen.isStatic = false; // its colour changes in play
                screen.AddComponent<Emissive>();
                var lightObj = new GameObject("Light");
                lightObj.transform.SetParent(screen.transform, false);
                lightObj.transform.position = at + Vector3.back * 0.6f;
                Light l = lightObj.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 6f;
                l.intensity = 1.6f;
                l.color = new Color(0.55f, 0.85f, 0.7f);
                l.shadows = LightShadows.None;
                l.renderingLayerMask = (int)Darkness.DarkLayer; // they light only the dark
            }
            FlagSwitch release = r.Switch("Release_Button", new Vector3(9.8f, 1.2f, 7.4f), Released, "Press the release?", "press the release", "SECTOR C RELEASED.");
            release.requiredFlag = DoneFlag;
            release.waitingText = "WAITING FOR NET.";
            EditorUtility.SetDirty(release);
        }

        private static void DressCloset(DemoRoom r)
        {
            r.Prop("Rack_North", new Vector3(11.3f, 0f, 11.15f), new Vector3(2.0f, 2f, 0.3f), 0.18f);
            for (int i = 0; i < ShelfOrder.Length; i++)
                Unit(r, ShelfOrder[i], new Vector3(12.65f, 0f, 9.1f + i * 0.75f), Vector3.left);
        }

        /// <summary>
        /// One UPS unit on the floor, its front to facing (DARK TEST, and the demo's G9b): the body, the LED and the front
        /// display on the front, the point of the Interact key in front of it. Its own pitch of beep by its label.
        /// </summary>
        public static UpsUnit Unit(DemoRoom r, string label, Vector3 floorPoint, Vector3 facing)
        {
            Quaternion turn = Quaternion.LookRotation(facing);
            Vector3 front = floorPoint + facing * 0.21f;
            GameObject body = r.Box(r.Props, "UPS_" + label, floorPoint + Vector3.up * 0.25f, DemoRoom.Abs(turn * new Vector3(0.35f, 0.5f, 0.4f)), DemoRoom.Grey(0.2f));
            GameObject led = r.Box(r.Props, "UPS_LED_" + label, front + Vector3.up * 0.4f, DemoRoom.Abs(turn * new Vector3(0.05f, 0.05f, 0.02f)), DemoRoom.Colour("ups_led", Color.white));
            led.isStatic = false;
            led.AddComponent<Emissive>();
            GameObject display = r.Sign("UPS_Label_" + label, label, front + Vector3.up * 0.62f, facing, new Vector2(0.5f, 0.12f), 30);
            display.isStatic = false;
            UpsUnit unit = MenuLoopSetup.EnsureComponent<UpsUnit>(body);
            unit.label = label;
            unit.display = display.GetComponentInChildren<UnityEngine.UI.Text>(true); // the front display: the label, or a fault's countdown
            unit.led = led.GetComponent<Renderer>();
            unit.pitch = Pitches[System.Array.IndexOf(Labels, label)];
            GameObject point = MenuLoopSetup.EnsureChild(body, "Point");
            point.transform.position = floorPoint + facing * 0.65f + Vector3.up * 0.5f;
            unit.point = point.transform;
            unit.range = 0.9f;
            EditorUtility.SetDirty(unit);
            return unit;
        }
    }
}
#endif
