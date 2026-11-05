#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Story;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>
    /// The floorplan v2 of the demo (2026-11-05, the demo plan §3.4, the writer's map game/act1_map_v2.py, Bruno: "you can
    /// get started on the v2 map"): twelve rooms in greybox, five on the ward (W13 to W17) and seven on the public floor
    /// (G9b to G15), the fixtures of round 30 (a microwave, a coffee machine, a toilet, a faucet: the Interact key, a
    /// sound, a small move of the prop, no effect), and the conference room beat (cutscene 07b: the name cards, the
    /// flashback once). The main way does not change. The places follow the map's doors; where the built rooms of v1 leave
    /// no wall for a door, the room sits on the nearest free wall (the devlog note says where). Nothing here is a puzzle:
    /// the terminal of W13, the code on the wall of W14 and the three PCs of G9b wait for Bruno's picks (PUZZLES.md).
    /// </summary>
    public static class DemoV2
    {
        public const string CardFlag = "demo_g12_card";
        public const string FlashbackId = "07b_the_conference_room";
        public const string EncoreFlagDone = "demo_u1_encore";
        public const float ShortGain = 10f; // PROPOSED: a tenth of the SHORT bar after the flashback (the writer)

        public static readonly string[] WardIds = { "W13", "W14", "W15", "W16", "W17" };
        public static readonly string[] PublicIds = { "G9b", "G10", "G11", "G12", "G13", "G14", "G15" };

        // ── The specs ─────────────────────────────────────────────────

        /// <summary>The five new rooms of the ward, in plan metres (x east, z north), beside the rooms of DemoWard.</summary>
        public static List<RoomSpec> WardRooms() => new List<RoomSpec>
        {
            // The map: beside the stairwell, a door from W12 and a second door for a loop around W9. The built ward has
            // W8 north of W9, so the loop's second door opens into W10 (the map says W8: an OPEN point for Bruno).
            Spec("W13", "WARD SECURITY POST", DemoItems.Floor2, DemoWard.Music, 17, -14, 23, -10, 3f, W13, new Door('W', -12, "W12"), new Door('N', 20, "W10")),
            // The map: off W2's north wall at its west end. W1B and W3 fill that wall, so the room opens off W2's west end.
            Spec("W14", "CHILDREN'S DAY ROOM", DemoItems.Floor2, DemoWard.Music, -10, 9, 0, 14, 3f, W14, new Door('E', 12.75f, "W2")),
            // The map: off the north end of W5, which in the built ward is the junction with W2: the room opens off W2's east end.
            Spec("W15", "STAFF BREAK ROOM", DemoItems.Floor2, DemoWard.Music, 24, 9, 34, 14, 3f, W15, new Door('W', 12.75f, "W2")),
            Spec("W16", "COLD ROOM", DemoItems.Floor2, DemoWard.Music, 3, -23, 8, -18, 3f, W16, new Door('N', 5.25f, "W11")),
            Spec("W17", "STORAGE", DemoItems.Floor2, DemoWard.Music, 8, -22, 13, -18, 3f, W17, new Door('N', 11.25f, "W11")),
        };

        /// <summary>The seven new rooms of the public floor, in plan metres (DemoWorld moves the floor to its place).</summary>
        public static List<RoomSpec> PublicRooms() => new List<RoomSpec>
        {
            Spec("G9b", "COMMS CLOSET", DemoItems.Floor1, DemoPublic.Music, 34, -16, 38, -9, 3f, G9b, new Door('W', -14.5f, "G9")),
            // Two doors: off the concourse and off the hall (a second way into G5, §12).
            Spec("G10", "CAFETERIA", DemoItems.Floor1, DemoPublic.Music, 28, -5, 38, 0, 4f, G10, new Door('N', 30, "G2"), new Door('W', -3.5f, "G5")),
            Spec("G11", "STAFF BREAK ROOM", DemoItems.Floor1, DemoPublic.Music, -2, -22, 6, -16, 3f, G11, new Door('E', -19, "G8"), new Door('S', 2, "G15")),
            Spec("G12", "CONFERENCE ROOM", DemoItems.Floor1, DemoPublic.Music, 26, -22, 38, -16, 3.5f, G12, new Door('N', 27, "G5")),
            // Two doors: off the concourse and off the family rooms (a second way into G5 through G7, §12).
            Spec("G13", "ADMISSIONS OFFICE", DemoItems.Floor1, DemoPublic.Music, 4, -9, 12, 0, 3.5f, G13, new Door('N', 8, "G2"), new Door('S', 8, "G7")),
            Spec("G14", "BUILDERS' STORAGE", DemoItems.Floor1, DemoPublic.Music, 26, -28, 32, -22, 4f, G14, new Door('W', -23, "U1")),
            Spec("G15", "STAFF TOILETS", DemoItems.Floor1, DemoPublic.Music, -2, -27, 6, -22, 3f, G15, new Door('N', 2, "G11")),
        };

        private static RoomSpec Spec(string id, string title, string floor, string music, float x0, float z0, float x1, float z1, float height, System.Action<DemoRoom> dress, params Door[] doors)
        {
            var spec = new RoomSpec { Id = id, Title = title, Floor = floor, Music = music, X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Height = height, Dress = dress };
            spec.Doors.AddRange(doors);
            return spec;
        }

        // ── The ward ──────────────────────────────────────────────────

        /// <summary>W13: the post is empty. A terminal on the desk, the chair pushed back, a radio on a shelf. No puzzle yet.</summary>
        private static void W13(DemoRoom r)
        {
            r.Prop("Desk", new Vector3(21.4f, 0f, -13.2f), new Vector3(2.2f, 0.75f, 0.8f), 0.25f);
            GameObject terminal = r.CrtWall("Terminal", new Vector3(21.4f, 0.75f, -13.3f), 1, 1, new List<TapeLine>(), 0.24f, Vector3.forward);
            terminal.GetComponent<InWorldScreen>().thing = "the terminal";
            r.Prop("Empty_Chair", new Vector3(21.4f, 0f, -12f), new Vector3(0.5f, 0.9f, 0.5f), 0.15f);
            r.Prop("Shelf", new Vector3(18.6f, 0f, -13.5f), new Vector3(0.8f, 0.75f, 0.4f), 0.2f);
            r.Prop("Radio", new Vector3(18.6f, 0.75f, -13.5f), new Vector3(0.3f, 0.18f, 0.12f), 0.1f);
            r.Prop("Radio_Antenna", new Vector3(18.72f, 0.93f, -13.5f), new Vector3(0.02f, 0.4f, 0.02f), 0.6f);
            r.Box(r.Props, "Key_Cabinet", new Vector3(17.2f, 1.5f, -10.6f), new Vector3(0.3f, 0.5f, 0.6f), DemoRoom.Grey(0.3f));
            Label(r, "WARD SECURITY", new Vector3(18.2f, 2f, -10.17f), Vector3.back, 1.4f);
        }

        /// <summary>
        /// W14: toys, a TV with the sound down, crayon drawings on the wall at a child's height. Among them a door with a red
        /// dot and the digits 5 2 8 1, the crayon on the floor under it, and a long black car (PUZZLES.md §2B, not a puzzle yet).
        /// The room is empty, a cup is still warm (the writer: no child on screen in act 1).
        /// </summary>
        private static void W14(DemoRoom r)
        {
            r.Crt("TV", new Vector3(-9.2f, 0f, 10.2f), 45f, new List<TapeLine>(), interactive: false, loop: false);
            r.Prop("Low_Table", new Vector3(-6.5f, 0f, 12.3f), new Vector3(1.2f, 0.5f, 0.8f), 0.45f);
            r.Prop("Cup_Warm", new Vector3(-6.3f, 0.5f, 12.2f), new Vector3(0.08f, 0.1f, 0.08f), 0.9f);
            r.Prop("Small_Chair_A", new Vector3(-7.4f, 0f, 11.4f), new Vector3(0.4f, 0.5f, 0.4f), 0.5f);
            r.Prop("Small_Chair_B", new Vector3(-5.6f, 0f, 11.4f), new Vector3(0.4f, 0.5f, 0.4f), 0.5f);
            r.Box(r.Props, "Play_Mat", new Vector3(-4f, 0.005f, 11.2f), new Vector3(2.4f, 0.01f, 2f), DemoRoom.Colour("play_mat", new Color(0.22f, 0.32f, 0.28f)));
            Piece(r.Props, "Toy_Block_R", new Vector3(-4.2f, 0.1f, 10.6f), new Vector3(0.2f, 0.2f, 0.2f), DemoRoom.Colour("toy_red", new Color(0.8f, 0.15f, 0.12f)), true);
            Piece(r.Props, "Toy_Block_B", new Vector3(-3.9f, 0.1f, 10.9f), new Vector3(0.2f, 0.2f, 0.2f), DemoRoom.Colour("toy_blue", new Color(0.15f, 0.3f, 0.75f)), true);
            Piece(r.Props, "Toy_Block_Y", new Vector3(-4.05f, 0.3f, 10.75f), new Vector3(0.2f, 0.2f, 0.2f), DemoRoom.Colour("toy_yellow", new Color(0.85f, 0.75f, 0.2f)), true);
            Piece(r.Props, "Toy_Ball", new Vector3(-2.5f, 0.12f, 11.8f), new Vector3(0.24f, 0.24f, 0.24f), DemoRoom.Colour("toy_green", new Color(0.2f, 0.6f, 0.25f)), true);
            Piece(r.Props, "Toy_Car", new Vector3(-3.2f, 0.06f, 13.2f), new Vector3(0.25f, 0.12f, 0.12f), DemoRoom.Colour("toy_red", new Color(0.8f, 0.15f, 0.12f)), true);
            // The drawings, at a child's height (about 1 m) on the north wall.
            Flat(r, "Drawing_Sun", new Vector3(-8f, 1f, 13.84f), new Vector2(0.35f, 0.35f), DemoRoom.Colour("crayon_yellow", new Color(0.9f, 0.8f, 0.25f)), Vector3.back);
            Flat(r, "Drawing_House", new Vector3(-7.2f, 1f, 13.84f), new Vector2(0.35f, 0.4f), DemoRoom.Colour("crayon_orange", new Color(0.85f, 0.45f, 0.15f)), Vector3.back);
            Material black = DemoRoom.Colour("crayon_black", new Color(0.04f, 0.04f, 0.045f));
            Flat(r, "Drawing_Car_Body", new Vector3(-6f, 0.95f, 13.84f), new Vector2(0.75f, 0.22f), black, Vector3.back); // the long black car (cutscene 07b, note 4)
            Flat(r, "Drawing_Car_Wheel_A", new Vector3(-6.25f, 0.82f, 13.84f), new Vector2(0.14f, 0.14f), black, Vector3.back);
            Flat(r, "Drawing_Car_Wheel_B", new Vector3(-5.75f, 0.82f, 13.84f), new Vector2(0.14f, 0.14f), black, Vector3.back);
            Flat(r, "Drawing_Door", new Vector3(-3f, 1f, 13.84f), new Vector2(0.32f, 0.5f), DemoRoom.Colour("crayon_brown", new Color(0.3f, 0.2f, 0.12f)), Vector3.back);
            Flat(r, "Drawing_Door_Dot", new Vector3(-2.92f, 1f, 13.825f), new Vector2(0.06f, 0.06f), DemoRoom.Colour("crayon_red", new Color(0.9f, 0.1f, 0.1f)), Vector3.back);
            r.Sign("Sign_Code", "5 2 8 1", new Vector3(-3f, 1.4f, 13.83f), Vector3.back, new Vector2(0.6f, 0.2f), 44);
            Flat(r, "Drawing_Green", new Vector3(-1.6f, 1f, 13.84f), new Vector2(0.3f, 0.3f), DemoRoom.Colour("crayon_green", new Color(0.25f, 0.55f, 0.25f)), Vector3.back);
            Piece(r.Props, "Crayon", new Vector3(-3f, 0.015f, 13.6f), new Vector3(0.12f, 0.03f, 0.03f), DemoRoom.Colour("crayon_red", new Color(0.9f, 0.1f, 0.1f)), false);
            Label(r, "DAY ROOM", new Vector3(-0.17f, 2f, 11f), Vector3.left, 1f);
        }

        /// <summary>W15: the staff break room of the ward. A counter with the sink, the microwave and the coffee machine, a fridge, a table.</summary>
        private static void W15(DemoRoom r)
        {
            r.Prop("Counter", new Vector3(29f, 0f, 13.65f), new Vector3(6f, 0.9f, 0.6f), 0.25f);
            Faucet(r, "Faucet", new Vector3(27f, 0.9f, 13.65f), Vector3.back);
            Microwave(r, "Microwave", new Vector3(30.2f, 0.9f, 13.65f), Vector3.back);
            CoffeeMachine(r, "Coffee_Machine", new Vector3(31.5f, 0.9f, 13.65f), Vector3.back);
            r.Prop("Fridge", new Vector3(33.3f, 0f, 13.4f), new Vector3(0.8f, 1.8f, 0.7f), 0.6f);
            r.Prop("Table", new Vector3(29f, 0f, 11f), new Vector3(1.8f, 0.75f, 0.9f), 0.3f);
            foreach ((string tag, float x, float z) in new[] { ("A", 28.4f, 10.2f), ("B", 29.6f, 10.2f), ("C", 28.4f, 11.8f), ("D", 29.6f, 11.8f) })
                r.Prop("Chair_" + tag, new Vector3(x, 0f, z), new Vector3(0.4f, 0.45f, 0.4f), 0.2f);
            r.Prop("Bin", new Vector3(25f, 0f, 13.5f), new Vector3(0.4f, 0.6f, 0.4f), 0.35f);
            r.Sign("Sign_Wash", "WASH YOUR CUP.", new Vector3(27f, 1.7f, 13.83f), Vector3.back, new Vector2(1f, 0.22f), 36);
            Label(r, "STAFF ROOM", new Vector3(24.17f, 2f, 10.8f), Vector3.right, 1f);
        }

        /// <summary>W16: the cold room. Racks of samples, the freezer door open, a thermometer that reads minus 80, the log of temperatures.</summary>
        private static void W16(DemoRoom r)
        {
            r.Prop("Rack_W", new Vector3(3.45f, 0f, -20.8f), new Vector3(0.6f, 2.2f, 3.2f), 0.75f);
            r.Prop("Rack_S", new Vector3(5.5f, 0f, -22.6f), new Vector3(3.2f, 2.2f, 0.6f), 0.75f);
            r.Prop("Rack_E", new Vector3(7.55f, 0f, -20.8f), new Vector3(0.6f, 2.2f, 3.2f), 0.75f);
            int n = 0;
            foreach ((float x, float y, float z) in new[] { (3.45f, 0.7f, -21.6f), (3.45f, 1.4f, -20.2f), (5f, 0.7f, -22.6f), (6.2f, 1.4f, -22.6f), (7.55f, 0.7f, -20.4f), (7.55f, 1.4f, -21.8f) })
                r.Prop($"Sample_Box_{n++}", new Vector3(x, y, z), new Vector3(0.3f, 0.2f, 0.3f), 0.9f);
            r.Prop("Freezer_Leaf", new Vector3(3.75f, 0f, -18.5f), new Vector3(1.3f, 2.2f, 0.12f), 0.8f);
            r.Sign("Sign_Temperature", "-80 C", new Vector3(6.8f, 1.45f, -18.17f), Vector3.back, new Vector2(0.6f, 0.25f), 44);
            Flat(r, "Temperature_Log", new Vector3(7.5f, 1.3f, -18.16f), new Vector2(0.22f, 0.3f), DemoRoom.Grey(0.85f), Vector3.back);
            Label(r, "COLD ROOM. KEEP CLOSED.", new Vector3(7f, 2f, -18.17f), Vector3.back, 1.6f);
        }

        /// <summary>W17: storage. Shelves, boxes, a spare small chair of the lab.</summary>
        private static void W17(DemoRoom r)
        {
            r.Prop("Shelves_W", new Vector3(8.45f, 0f, -20f), new Vector3(0.6f, 2f, 3.2f), 0.2f);
            r.Prop("Shelves_S", new Vector3(10.6f, 0f, -21.65f), new Vector3(3.2f, 2f, 0.6f), 0.2f);
            r.Prop("Box_A", new Vector3(9.6f, 0f, -19.4f), new Vector3(0.6f, 0.5f, 0.5f), 0.4f);
            r.Prop("Box_B", new Vector3(9.6f, 0.5f, -19.4f), new Vector3(0.5f, 0.4f, 0.4f), 0.45f);
            r.Prop("Box_C", new Vector3(12.3f, 0f, -20.4f), new Vector3(0.7f, 0.6f, 0.6f), 0.4f);
            r.Prop("Spare_Small_Chair", new Vector3(12.4f, 0f, -19f), new Vector3(0.6f, 0.7f, 0.9f), 0.5f);
            Label(r, "STORAGE", new Vector3(9.4f, 2f, -18.17f), Vector3.back, 1f);
        }

        // ── The public floor ──────────────────────────────────────────

        /// <summary>G9b: the comms closet. Racks on two walls, three PCs on the floor on their UPS units: NET, REC, ACCESS. No puzzle yet.</summary>
        private static void G9b(DemoRoom r)
        {
            r.Prop("Rack_N", new Vector3(36f, 0f, -9.4f), new Vector3(3.4f, 2.2f, 0.6f), 0.2f);
            r.Prop("Rack_E", new Vector3(37.65f, 0f, -12.6f), new Vector3(0.6f, 2.2f, 4.4f), 0.2f);
            Material green = DemoRoom.Colour("led_green", new Color(0.2f, 0.9f, 0.3f));
            for (int i = 0; i < 6; i++)
                Piece(r.Props, $"Rack_Led_{i}", new Vector3(34.6f + i * 0.5f, 1.2f + (i % 3) * 0.3f, -9.705f), new Vector3(0.03f, 0.03f, 0.01f), green, false);
            Material amber = DemoRoom.Colour("led_amber", new Color(0.95f, 0.6f, 0.1f));
            int k = 0;
            foreach (string label in new[] { "NET", "REC", "ACCESS" })
            {
                float x = 35f + k++ * 0.9f;
                r.Prop("UPS_" + label, new Vector3(x, 0f, -15.45f), new Vector3(0.36f, 0.26f, 0.42f), 0.12f);
                r.Prop("PC_" + label, new Vector3(x, 0.26f, -15.45f), new Vector3(0.3f, 0.42f, 0.42f), 0.3f);
                Piece(r.Props, "Led_" + label, new Vector3(x - 0.1f, 0.2f, -15.23f), new Vector3(0.03f, 0.03f, 0.01f), amber, false);
                r.Sign("Label_" + label, label, new Vector3(x, 0.56f, -15.23f), Vector3.forward, new Vector2(0.26f, 0.09f), 22);
            }
            Label(r, "COMMS", new Vector3(34.17f, 2f, -12.4f), Vector3.right, 0.9f);
        }

        /// <summary>
        /// G10: the cafeteria. Trays on a hot counter, tables, the microwave and the coffee machine on a side counter, two
        /// civilians who shelter by the tables (hiders; a figure cannot crouch inside a table top's collider, so they are
        /// beside the tables, low). PLACEHOLDER lines until the writer's.
        /// </summary>
        private static void G10(DemoRoom r)
        {
            r.Prop("Hot_Counter", new Vector3(37.3f, 0f, -2.5f), new Vector3(1.2f, 1f, 4f), 0.55f);
            for (int i = 0; i < 4; i++) r.Prop($"Tray_{i}", new Vector3(37.3f, 1f, -4f + i), new Vector3(0.4f, 0.03f, 0.3f), 0.65f);
            // One row of two tables in the middle: the aisles around them are 1.2 m or more (the NavMesh agent is 0.5 m).
            foreach ((string tag, float x) in new[] { ("A", 31.5f), ("B", 34.3f) })
            {
                Table(r, "Table_" + tag, new Vector3(x, 0f, -2.5f), 1.6f, 0.8f);
                foreach ((string side, float dx, float dz) in new[] { ("NW", -0.4f, 0.95f), ("NE", 0.4f, 0.95f), ("SW", -0.4f, -0.95f), ("SE", 0.4f, -0.95f) })
                    r.Prop($"Chair_{tag}_{side}", new Vector3(x + dx, 0f, -2.5f + dz), new Vector3(0.4f, 0.45f, 0.4f), 0.2f);
            }
            r.Prop("Side_Counter", new Vector3(30.5f, 0f, -4.7f), new Vector3(3f, 0.9f, 0.6f), 0.25f);
            Microwave(r, "Microwave", new Vector3(29.8f, 0.9f, -4.7f), Vector3.forward);
            CoffeeMachine(r, "Coffee_Machine", new Vector3(31.2f, 0.9f, -4.7f), Vector3.forward);
            EnemySpawn cook = r.Figure("Hider_Cook", "labworker", new Vector3(36f, 0f, -2.5f), 270f, variant: 1);
            cook.hides = true;
            cook.hideLine = "Don't. We only serve the food. We only serve the food."; // PLACEHOLDER
            cook.foundLine = "Please. I'll stay down. I'll stay right here."; // PLACEHOLDER
            EditorUtility.SetDirty(cook);
            EnemySpawn server = r.Figure("Hider_Server", "labworker", new Vector3(32.9f, 0f, -2.5f), 90f, variant: 3);
            server.hides = true;
            server.hideLine = "She's in here. She's in here."; // PLACEHOLDER
            server.foundLine = "Please. We're not part of it. We just work here."; // PLACEHOLDER
            EditorUtility.SetDirty(server);
            r.Sign("Sign_Menu", "TONIGHT: SOUP. BREAD. TEA.", new Vector3(37.83f, 2.2f, -2.5f), Vector3.left, new Vector2(1.8f, 0.35f), 36);
            Label(r, "CAFETERIA", new Vector3(32.2f, 2.3f, -0.17f), Vector3.back, 1.6f);
        }

        /// <summary>G11: the staff break room of the public floor. The same counter: a sink, the microwave, the coffee machine.</summary>
        private static void G11(DemoRoom r)
        {
            r.Prop("Counter", new Vector3(1.5f, 0f, -16.35f), new Vector3(5f, 0.9f, 0.6f), 0.25f);
            Faucet(r, "Faucet", new Vector3(-0.3f, 0.9f, -16.35f), Vector3.back);
            Microwave(r, "Microwave", new Vector3(1.5f, 0.9f, -16.35f), Vector3.back);
            CoffeeMachine(r, "Coffee_Machine", new Vector3(3f, 0.9f, -16.35f), Vector3.back);
            r.Prop("Fridge", new Vector3(5.3f, 0f, -16.6f), new Vector3(0.8f, 1.8f, 0.7f), 0.6f);
            r.Prop("Table", new Vector3(1.5f, 0f, -19.6f), new Vector3(1.6f, 0.75f, 0.9f), 0.3f);
            foreach ((string tag, float x, float z) in new[] { ("A", 0.9f, -20.4f), ("B", 2.1f, -20.4f), ("C", 0.9f, -18.8f) })
                r.Prop("Chair_" + tag, new Vector3(x, 0f, z), new Vector3(0.4f, 0.45f, 0.4f), 0.2f);
            r.Prop("Lockers", new Vector3(-1.65f, 0f, -20.2f), new Vector3(0.6f, 1.9f, 2.2f), 0.1f);
            Label(r, "STAFF ROOM", new Vector3(5.83f, 2f, -17.3f), Vector3.left, 1f);
        }

        /// <summary>
        /// G12: the conference room (cutscene 07b, beat 22b, first pass, 0 art). The meeting ended an hour ago: the long
        /// table, nine chairs pushed back, the name cards (COLTON, CHURCH, two face down, the head: DE HOMEN-CRISTO), the
        /// last slide on the screen, the projector, a full cup beside the head card. The Interact key on the head card
        /// plays the flashback once and raises the flag; the other cards read their name.
        /// </summary>
        private static void G12(DemoRoom r)
        {
            r.Prop("Table", new Vector3(32.5f, 0f, -19f), new Vector3(7f, 0.75f, 1.4f), 0.08f);
            int n = 0;
            // 1.4 m apart (a 0.9 m gap between chairs makes no NavMesh sliver), 0.3 m from the table: the aisles along the walls
            // are 1.5 m, and the NavMesh agent (0.5 m) walks both sides of the table.
            foreach (float x in new[] { 30f, 31.4f, 32.8f, 34.2f })
            {
                r.Prop($"Chair_N{n}", new Vector3(x, 0f, -17.75f), new Vector3(0.5f, 0.9f, 0.5f), 0.12f);
                r.Prop($"Chair_S{n++}", new Vector3(x, 0f, -20.25f), new Vector3(0.5f, 0.9f, 0.5f), 0.12f);
            }
            r.Prop("Chair_Head", new Vector3(36.95f, 0f, -19f), new Vector3(0.5f, 0.9f, 0.5f), 0.12f);
            Card(r, "Card_Colton", "COLTON", new Vector3(30f, 0.75f, -18.5f));
            Card(r, "Card_Church", "CHURCH", new Vector3(31.4f, 0.75f, -18.5f));
            Card(r, "Card_Down_A", "", new Vector3(32.8f, 0.75f, -18.5f));
            Card(r, "Card_Down_B", "", new Vector3(31.4f, 0.75f, -19.5f));
            NameCard head = Card(r, "Card_Head", "DE HOMEN-CRISTO", new Vector3(35.65f, 0.75f, -19f));
            head.flashback = Flashback();
            head.flag = CardFlag;
            head.shortGain = ShortGain;
            EditorUtility.SetDirty(head);
            r.Prop("Cup_Full", new Vector3(35.65f, 0.75f, -18.6f), new Vector3(0.08f, 0.09f, 0.08f), 0.92f);
            Piece(r.Props, "Cup_Coffee", new Vector3(35.65f, 0.842f, -18.6f), new Vector3(0.062f, 0.004f, 0.062f), DemoRoom.Colour("coffee_skin", new Color(0.32f, 0.22f, 0.14f)), false);
            r.Prop("Projector", new Vector3(29.7f, 0.75f, -19f), new Vector3(0.3f, 0.12f, 0.25f), 0.35f);
            Piece(r.Props, "Projector_Lens", new Vector3(29.53f, 0.81f, -19f), new Vector3(0.04f, 0.04f, 0.04f), DemoRoom.Grey(0.9f), false);
            r.Prop("Water_Jug", new Vector3(33f, 0.75f, -19.35f), new Vector3(0.1f, 0.2f, 0.1f), 0.8f);
            r.Prop("Flip_Chart", new Vector3(37.4f, 0f, -21.5f), new Vector3(0.7f, 1.8f, 0.06f), 0.85f);
            // The last slide: the white gear on black (a plain square for now), FAMILY OBSERVATION. V3. THANK YOU.
            r.Sign("Screen_Slide", "FAMILY OBSERVATION. V3. THANK YOU.", new Vector3(26.17f, 1.75f, -19f), Vector3.right, new Vector2(2.4f, 1.3f), 52);
            Piece(r.Props, "Slide_Gear", new Vector3(26.19f, 2.12f, -19f), new Vector3(0.02f, 0.5f, 0.5f), DemoRoom.Grey(0.92f), false);
            Label(r, "CONFERENCE ROOM", new Vector3(29.2f, 2.3f, -16.17f), Vector3.back, 1.8f);
        }

        /// <summary>G13: the admissions office. A counter, a queue barrier, rows of desks with PCs (§12).</summary>
        private static void G13(DemoRoom r)
        {
            r.Prop("Counter", new Vector3(10.3f, 0f, -1.6f), new Vector3(2.6f, 1.1f, 0.7f), 0.25f);
            r.Prop("Counter_Screen", new Vector3(10.3f, 1.1f, -1.6f), new Vector3(0.4f, 0.35f, 0.3f), 0.15f);
            foreach ((string tag, float x, float z) in new[] { ("A", 5.8f, -1.3f), ("B", 5.8f, -2.7f), ("C", 7.2f, -2.7f) })
                r.Prop("Post_" + tag, new Vector3(x, 0f, z), new Vector3(0.08f, 1f, 0.08f), 0.5f);
            r.Prop("Tape_A", new Vector3(5.8f, 0.9f, -2f), new Vector3(0.02f, 0.02f, 1.4f), 0.7f);
            r.Prop("Tape_B", new Vector3(6.5f, 0.9f, -2.7f), new Vector3(1.4f, 0.02f, 0.02f), 0.7f);
            int n = 0;
            foreach (float z in new[] { -4f, -6f, -8f })
                foreach (float x in new[] { 5.2f, 10.6f })
                {
                    r.Prop($"Desk_{n}", new Vector3(x, 0f, z), new Vector3(1.4f, 0.75f, 0.7f), 0.25f);
                    r.Prop($"Monitor_{n}", new Vector3(x, 0.75f, z + 0.2f), new Vector3(0.4f, 0.33f, 0.08f), 0.1f);
                    r.Prop($"Keyboard_{n}", new Vector3(x, 0.75f, z - 0.1f), new Vector3(0.35f, 0.02f, 0.12f), 0.6f);
                    r.Prop($"Chair_{n++}", new Vector3(x, 0f, z - 0.8f), new Vector3(0.45f, 0.9f, 0.45f), 0.15f);
                }
            Label(r, "ADMISSIONS", new Vector3(10.4f, 2.3f, -0.17f), Vector3.back, 1.4f);
        }

        /// <summary>G14: the builders' storage, raw block like U1. Cement bags on a pallet, a ladder, sheeting, a work light.</summary>
        private static void G14(DemoRoom r)
        {
            r.Prop("Pallet", new Vector3(30.4f, 0f, -26.6f), new Vector3(1.2f, 0.12f, 0.9f), 0.4f);
            for (int i = 0; i < 6; i++)
                r.Prop($"Cement_Bag_{i}", new Vector3(30.4f + (i % 2 == 0 ? -0.28f : 0.28f), 0.12f + (i / 2) * 0.22f, -26.6f), new Vector3(0.55f, 0.22f, 0.75f), 0.6f);
            r.Prop("Ladder", new Vector3(31.7f, 0f, -24.2f), new Vector3(0.5f, 2.6f, 0.1f), 0.5f);
            r.Prop("Sheeting_Roll", new Vector3(28.2f, 0f, -27.4f), new Vector3(2.2f, 0.3f, 0.3f), 0.72f);
            r.Prop("Work_Light", new Vector3(27.4f, 0f, -24.9f), new Vector3(0.3f, 1.8f, 0.3f), 0.8f);
            r.Prop("Bucket", new Vector3(29.2f, 0f, -24.4f), new Vector3(0.3f, 0.35f, 0.3f), 0.45f);
            r.Box(r.Props, "Sheeting", new Vector3(29.6f, 1.8f, -22.45f), new Vector3(2.4f, 3.6f, 0.04f), DemoRoom.Colour("sheeting", new Color(0.7f, 0.72f, 0.7f)));
            Label(r, "BUILDERS' STORE", new Vector3(26.17f, 2f, -25.6f), Vector3.right, 1.6f);
        }

        /// <summary>G15: the staff toilets. Stalls, two sinks under a mirror. A toilet that flushes, a faucet that runs.</summary>
        private static void G15(DemoRoom r)
        {
            int n = 0;
            foreach (float x in new[] { -0.4f, 1.1f, 2.6f }) r.Prop($"Stall_Wall_{n++}", new Vector3(x, 0f, -26.2f), new Vector3(0.06f, 2f, 1.6f), 0.35f);
            // 0.4 and 1.9, not the stall's middle: a tank edge on the coverage grid's box (a 0.00 m gap) made the builder and the test disagree on one point.
            Toilet(r, "Toilet", new Vector3(0.4f, 0f, -26.45f), Vector3.forward);
            Toilet(r, "Toilet_B", new Vector3(1.9f, 0f, -26.45f), Vector3.forward, works: false);
            r.Prop("Sink_A", new Vector3(-1.65f, 0f, -23.3f), new Vector3(0.5f, 0.85f, 0.6f), 0.55f);
            r.Prop("Sink_B", new Vector3(-1.65f, 0f, -24.3f), new Vector3(0.5f, 0.85f, 0.6f), 0.55f);
            Faucet(r, "Faucet", new Vector3(-1.65f, 0.85f, -23.3f), Vector3.right);
            r.Box(r.Props, "Mirror", new Vector3(-1.83f, 1.4f, -23.8f), new Vector3(0.04f, 0.8f, 1.8f), DemoRoom.Colour("mirror", new Color(0.62f, 0.68f, 0.72f)));
            Label(r, "STAFF TOILETS", new Vector3(4.3f, 2f, -22.17f), Vector3.back, 1.4f);
        }

        // ── The fixtures (round 30) ───────────────────────────────────

        /// <summary>A microwave on a counter: its light panel glows while it runs, with the lunch box inside, then it dings.</summary>
        public static Fixture Microwave(DemoRoom r, string name, Vector3 at, Vector3 facing)
        {
            GameObject holder = Holder(r, name, at, facing);
            GameObject body = Piece(holder, "Body", new Vector3(0f, 0.17f, 0f), new Vector3(0.5f, 0.34f, 0.4f), DemoRoom.Grey(0.5f), true);
            Piece(holder, "Window", new Vector3(-0.06f, 0.17f, 0.205f), new Vector3(0.24f, 0.22f, 0.012f), DemoRoom.Grey(0.06f), false);
            Piece(holder, "Panel", new Vector3(0.17f, 0.17f, 0.205f), new Vector3(0.1f, 0.22f, 0.012f), DemoRoom.Grey(0.3f), false);
            GameObject glow = MenuLoopSetup.EnsureChild(holder, "Glow");
            Piece(glow, "Light", new Vector3(-0.06f, 0.17f, 0.212f), new Vector3(0.22f, 0.2f, 0.006f), DemoRoom.Colour("microwave_light", new Color(0.95f, 0.85f, 0.5f)), false);
            Piece(glow, "Lunch_Box", new Vector3(-0.06f, 0.13f, 0.216f), new Vector3(0.12f, 0.07f, 0.004f), DemoRoom.Colour("lunch_box", new Color(0.25f, 0.3f, 0.45f)), false);
            glow.SetActive(false);
            return MakeFixture(holder, Fixture.Kind.Microwave, "the microwave", body, glow, null);
        }

        /// <summary>A coffee machine: a cup under the spout, the coffee in it grows as it fills, the cup leaves while she drinks.</summary>
        public static Fixture CoffeeMachine(DemoRoom r, string name, Vector3 at, Vector3 facing)
        {
            GameObject holder = Holder(r, name, at, facing);
            GameObject body = Piece(holder, "Body", new Vector3(0f, 0.21f, -0.06f), new Vector3(0.3f, 0.42f, 0.3f), DemoRoom.Grey(0.18f), true);
            Piece(holder, "Spout", new Vector3(0f, 0.3f, 0.13f), new Vector3(0.04f, 0.04f, 0.1f), DemoRoom.Grey(0.6f), false);
            Piece(holder, "Tray", new Vector3(0f, 0.01f, 0.13f), new Vector3(0.16f, 0.02f, 0.14f), DemoRoom.Grey(0.4f), false);
            Piece(holder, "Button", new Vector3(0.09f, 0.36f, 0.095f), new Vector3(0.03f, 0.03f, 0.01f), DemoRoom.Colour("led_green", new Color(0.2f, 0.9f, 0.3f)), false);
            GameObject cup = MenuLoopSetup.EnsureChild(holder, "Cup");
            cup.transform.localPosition = new Vector3(0f, 0.02f, 0.13f);
            cup.transform.localRotation = Quaternion.identity;
            Material white = DemoRoom.Grey(0.92f);
            Piece(cup, "Wall_N", new Vector3(0f, 0.045f, 0.036f), new Vector3(0.08f, 0.09f, 0.008f), white, false);
            Piece(cup, "Wall_S", new Vector3(0f, 0.045f, -0.036f), new Vector3(0.08f, 0.09f, 0.008f), white, false);
            Piece(cup, "Wall_E", new Vector3(0.036f, 0.045f, 0f), new Vector3(0.008f, 0.09f, 0.08f), white, false);
            Piece(cup, "Wall_W", new Vector3(-0.036f, 0.045f, 0f), new Vector3(0.008f, 0.09f, 0.08f), white, false);
            Piece(cup, "Bottom", new Vector3(0f, 0.004f, 0f), new Vector3(0.08f, 0.008f, 0.08f), white, false);
            GameObject fill = Piece(cup, "Coffee", new Vector3(0f, 0.043f, 0f), new Vector3(0.062f, 0.07f, 0.062f), DemoRoom.Colour("coffee", new Color(0.22f, 0.14f, 0.08f)), false);
            return MakeFixture(holder, Fixture.Kind.Coffee, "the coffee machine", body, cup, fill.transform);
        }

        /// <summary>A toilet: the bowl opens to the facing. The water in the bowl drops at the flush and comes back. Works: it is a fixture.</summary>
        public static Fixture Toilet(DemoRoom r, string name, Vector3 at, Vector3 facing, bool works = true, string answer = "")
        {
            GameObject holder = Holder(r, name, at, facing);
            Material white = DemoRoom.Grey(0.9f);
            GameObject bowl = Piece(holder, "Bowl", new Vector3(0f, 0.2f, 0.05f), new Vector3(0.38f, 0.4f, 0.5f), white, true);
            Piece(holder, "Tank", new Vector3(0f, 0.6f, -0.26f), new Vector3(0.4f, 0.4f, 0.18f), white, true);
            Piece(holder, "Seat_W", new Vector3(-0.17f, 0.415f, 0.05f), new Vector3(0.06f, 0.03f, 0.46f), DemoRoom.Grey(0.8f), false);
            Piece(holder, "Seat_E", new Vector3(0.17f, 0.415f, 0.05f), new Vector3(0.06f, 0.03f, 0.46f), DemoRoom.Grey(0.8f), false);
            Piece(holder, "Seat_Back", new Vector3(0f, 0.415f, -0.17f), new Vector3(0.4f, 0.03f, 0.06f), DemoRoom.Grey(0.8f), false);
            Piece(holder, "Lever", new Vector3(0.16f, 0.78f, -0.17f), new Vector3(0.06f, 0.02f, 0.05f), DemoRoom.Grey(0.7f), false);
            GameObject water = Piece(holder, "Water", new Vector3(0f, 0.405f, 0.08f), new Vector3(0.26f, 0.01f, 0.32f), DemoRoom.Colour("water", new Color(0.45f, 0.6f, 0.75f)), false);
            if (!works) return null;
            Fixture f = MakeFixture(holder, Fixture.Kind.Toilet, "the toilet", bowl, null, water.transform);
            f.answerLine = answer;
            EditorUtility.SetDirty(f);
            return f;
        }

        /// <summary>A faucet at the back of a basin (basinTop: the middle of the sink's top; facing: from the wall into the room). The stream shows while it runs.</summary>
        public static Fixture Faucet(DemoRoom r, string name, Vector3 basinTop, Vector3 facing)
        {
            GameObject holder = Holder(r, name, basinTop, facing);
            Piece(holder, "Basin", new Vector3(0f, 0.004f, 0f), new Vector3(0.36f, 0.008f, 0.26f), DemoRoom.Colour("basin", new Color(0.3f, 0.33f, 0.36f)), false);
            GameObject post = Piece(holder, "Tap", new Vector3(0f, 0.09f, -0.17f), new Vector3(0.03f, 0.18f, 0.03f), DemoRoom.Grey(0.75f), false);
            Piece(holder, "Spout", new Vector3(0f, 0.17f, -0.11f), new Vector3(0.025f, 0.025f, 0.12f), DemoRoom.Grey(0.75f), false);
            Piece(holder, "Handle", new Vector3(0.04f, 0.19f, -0.17f), new Vector3(0.05f, 0.015f, 0.015f), DemoRoom.Grey(0.75f), false);
            GameObject stream = Piece(holder, "Stream", new Vector3(0f, 0.085f, -0.06f), new Vector3(0.016f, 0.16f, 0.016f), DemoRoom.Colour("water_stream", new Color(0.6f, 0.75f, 0.9f)), false);
            stream.SetActive(false);
            return MakeFixture(holder, Fixture.Kind.Faucet, "the faucet", post, stream, null);
        }

        private static GameObject Holder(DemoRoom r, string name, Vector3 at, Vector3 facing)
        {
            GameObject holder = MenuLoopSetup.EnsureChild(r.Props, name);
            holder.transform.SetPositionAndRotation(at, Quaternion.LookRotation(facing.normalized, Vector3.up));
            return holder;
        }

        private static Fixture MakeFixture(GameObject holder, Fixture.Kind kind, string thing, GameObject point, GameObject shown, Transform moving)
        {
            Fixture f = MenuLoopSetup.EnsureComponent<Fixture>(holder);
            f.kind = kind;
            f.thing = thing;
            f.point = point.transform;
            f.shown = shown;
            f.moving = moving;
            f.range = 1.6f;
            EditorUtility.SetDirty(f);
            return f;
        }

        // ── The conference room beat ──────────────────────────────────

        /// <summary>A name card on the table: a tent card with its name, or a card face down (no name).</summary>
        private static NameCard Card(DemoRoom r, string name, string words, Vector3 at)
        {
            GameObject holder = MenuLoopSetup.EnsureChild(r.Props, name);
            holder.transform.SetPositionAndRotation(at, Quaternion.identity);
            Material white = DemoRoom.Grey(0.95f);
            GameObject card = string.IsNullOrEmpty(words)
                ? Piece(holder, "Card", new Vector3(0f, 0.002f, 0f), new Vector3(0.12f, 0.004f, 0.08f), white, false)
                : Piece(holder, "Card", new Vector3(0f, 0.035f, 0f), new Vector3(0.12f, 0.07f, 0.015f), white, false);
            NameCard component = MenuLoopSetup.EnsureComponent<NameCard>(holder);
            component.cardName = words;
            component.point = card.transform;
            component.range = 1.6f;
            EditorUtility.SetDirty(component);
            return component;
        }

        /// <summary>
        /// The flashback of cutscene 07b, first pass: black and text cards with the sounds of F0 to F5 (placeholders from
        /// tools/fixture_placeholders.py), hard cuts, 13.5 s, the French lines as subtitles with no voice. The sound of a shot
        /// runs on under the next ones (the metronome under F2, F3 and "Encore."; the count and the blow under F4).
        /// </summary>
        public static CutsceneAsset Flashback()
        {
            System.IO.Directory.CreateDirectory(ScriptTestSetup.CutscenesFolder);
            string path = ScriptTestSetup.CutscenesFolder + "/cutscene_" + FlashbackId + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<CutsceneAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CutsceneAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.cutsceneId = FlashbackId;
            asset.music = null;
            Texture2D black = Still("cs07b_black"), f1 = Still("cs07b_f1"), f3 = Still("cs07b_f3"), f4 = Still("cs07b_f4");
            AudioClip gravel = Sound("cs07b_f0"), metronome = Sound("cs07b_f2"), count = Sound("cs07b_f4");
            int n = 0;
            CutsceneAsset.Shot Shot(Texture2D still, float seconds, AudioClip sound = null, string text = "")
            {
                return new CutsceneAsset.Shot
                {
                    still = still, seconds = seconds, sound = sound, panFrom = new Vector3(0f, 0f, 1f), panTo = new Vector3(0f, 0f, 1f),
                    speaker = "", lineId = string.IsNullOrEmpty(text) ? "" : $"cs07b_{n++:00}", text = text,
                };
            }
            asset.shots = new[]
            {
                Shot(black, 2.5f, gravel),                     // F0: gravel under tyres, slow. A heavy car door shuts.
                Shot(f1, 0.8f),                                // F1: the gravel drive, the sill of the long black car, the hand on the shoulder.
                Shot(black, 2f, metronome),                    // F2: black. A metronome, slow.
                Shot(f3, 0.8f),                                // F3: the white line, two pairs of small feet.
                Shot(black, 1.2f, null, "Encore."),            // the man, close.
                Shot(f4, 0.8f, count),                         // F4: the small hand around the rod.
                Shot(black, 2.2f, null, "Un. Deux. Trois."),   // the count, then the blow. Nobody cries.
                Shot(black, 1.6f, null, "Elle ne pleure pas."), // F5: the metronome has stopped. The woman.
                Shot(black, 1.6f, null, "Pas elle. L'autre."),  // the man.
            };
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static Texture2D Still(string name)
        {
            string path = DemoScripts.FmvFolder + "/" + name + ".png";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                AssetDatabase.ImportAsset(path);
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            if (tex == null) Debug.LogWarning("[DemoV2] no card " + path + " (run tools/fixture_placeholders.py)");
            return tex;
        }

        private static AudioClip Sound(string name)
        {
            string path = DemoScripts.FmvFolder + "/sfx/" + name + ".ogg";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                AssetDatabase.ImportAsset(path);
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            if (clip == null) Debug.LogWarning("[DemoV2] no sound " + path + " (run tools/fixture_placeholders.py)");
            return clip;
        }

        /// <summary>
        /// Cutscene 09, the voices of the night, layer 4: if she read the card in the conference room, "Encore." is among the
        /// voices. The voices are not built yet (U1's Crown beat holds the place), so a sequence under a flag gate says the
        /// one line beside the Crown beat, in the same trigger, when the flag is up. One flag (the writer's note 7).
        /// </summary>
        public static void EncoreInU1(DemoRoom r, Vector3 triggerAt, Vector3 triggerSize)
        {
            GameObject holder = MenuLoopSetup.EnsureChild(r.Story, "Gate_Encore");
            FlagGate gate = MenuLoopSetup.EnsureComponent<FlagGate>(holder);
            gate.flag = CardFlag;
            gate.showWhenRaised = true;
            ScriptedSequence seq = GlorifiedBody.Editor.RoomKit.Sequence("U1_Encore", EncoreFlagDone, ScriptStep.Wait(2.5f), ScriptStep.Radio("", "u1_voice_encore", "Encore."));
            seq.transform.SetParent(holder.transform, true);
            GlorifiedBody.Editor.RoomKit.Trigger("Trigger_U1_Encore", triggerAt, triggerSize, seq).transform.SetParent(holder.transform, true);
        }

        // ── A NavMesh report ──────────────────────────────────────────

        /// <summary>
        /// Where the NavMesh is (2026-11-05, the floorplan v2 tests: G8 had no mesh under its floor point): for every room, the
        /// floor points (0.5 m apart, clear of props) with mesh under them, and how many of those a path from G5 reaches.
        /// Batch: tools/unity.sh exec GlorifiedBody.Editor.Demo.DemoV2.NavMeshReport
        /// </summary>
        public static void NavMeshReport()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(DemoWorld.ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
            if (surface == null || surface.navMeshData == null) { Debug.LogError("[NavMeshReport] no NavMeshSurface with data"); return; }
            UnityEngine.AI.NavMesh.RemoveAllNavMeshData();
            UnityEngine.AI.NavMesh.AddNavMeshData(surface.navMeshData);
            Physics.SyncTransforms();
            int solid = LayerMask.GetMask("Default");
            GlorifiedBody.Rooms.Room g5 = Object.FindObjectsByType<GlorifiedBody.Rooms.Room>(FindObjectsSortMode.None).First(r => r.id == "G5");
            Vector3 from = g5.transform.position + new Vector3(20f, 0f, -8f);
            UnityEngine.AI.NavMesh.SamplePosition(from, out UnityEngine.AI.NavMeshHit start, 1f, UnityEngine.AI.NavMesh.AllAreas);
            foreach (GlorifiedBody.Rooms.Room room in Object.FindObjectsByType<GlorifiedBody.Rooms.Room>(FindObjectsSortMode.None).OrderBy(r => r.id))
            {
                int points = 0, withMesh = 0, reached = 0;
                string sample = "";
                for (float x = room.rect.x + 0.45f; x <= room.rect.z - 0.45f; x += 0.5f)
                    for (float z = room.rect.y + 0.45f; z <= room.rect.w - 0.45f; z += 0.5f)
                    {
                        Vector3 p = room.transform.position + new Vector3(x, 0f, z);
                        if (!Physics.Raycast(p + Vector3.up * 2.5f, Vector3.down, out RaycastHit hit, 10f, solid, QueryTriggerInteraction.Ignore)) continue;
                        if (hit.point.y > room.BaseY + 0.3f) continue; // a prop's top
                        if (Physics.CheckBox(hit.point + Vector3.up * 0.9f, new Vector3(0.2f, 0.8f, 0.2f), Quaternion.identity, solid, QueryTriggerInteraction.Ignore)) continue;
                        points++;
                        if (!UnityEngine.AI.NavMesh.SamplePosition(hit.point, out UnityEngine.AI.NavMeshHit on, 0.3f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                        withMesh++;
                        var path = new UnityEngine.AI.NavMeshPath();
                        if (UnityEngine.AI.NavMesh.CalculatePath(start.position, on.position, UnityEngine.AI.NavMesh.AllAreas, path) && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete) reached++;
                        else if (sample.Length < 200) sample += $" ({x:0.0},{z:0.0})";
                    }
                Debug.Log($"[NavMeshReport] {room.id}: {points} floor points, {withMesh} with mesh, {reached} reached from G5. Unreached:{sample}");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────

        /// <summary>A table on four legs: a top she cannot walk through, room under it.</summary>
        public static void Table(DemoRoom r, string name, Vector3 centre, float width, float depth, float height = 0.75f)
        {
            GameObject holder = MenuLoopSetup.EnsureChild(r.Props, name);
            holder.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Piece(holder, "Top", centre + Vector3.up * (height - 0.025f), new Vector3(width, 0.05f, depth), DemoRoom.Grey(0.35f), true);
            int n = 0;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Piece(holder, $"Leg_{n++}", centre + new Vector3(sx * (width * 0.5f - 0.08f), (height - 0.05f) * 0.5f, sz * (depth * 0.5f - 0.08f)), new Vector3(0.05f, height - 0.05f, 0.05f), DemoRoom.Grey(0.25f), true);
        }

        /// <summary>
        /// A flat thing on a wall (a drawing, a page): no collider. The size is width, height; the thin side is along the facing
        /// (the way it looks, into the room). Its middle stands 0.16 m off the room line: the wall is 0.3 m thick on the line.
        /// </summary>
        private static void Flat(DemoRoom r, string name, Vector3 at, Vector2 size, Material material, Vector3 facing)
        {
            Vector3 scale = Mathf.Abs(facing.x) > 0.5f ? new Vector3(0.02f, size.y, size.x) : new Vector3(size.x, size.y, 0.02f);
            Piece(r.Props, name, at, scale, material, false);
        }

        /// <summary>The sign of the room's name, inside, beside its door, 0.17 m off the room line (the wall's face is at 0.15).</summary>
        private static void Label(DemoRoom r, string words, Vector3 at, Vector3 facing, float width)
        {
            r.Sign("Sign_Name", words, at, facing, new Vector2(width, 0.3f), 40);
        }

        /// <summary>A primitive under a parent, in the parent's frame: solid (it blocks, static) or a loose piece (no collider, it may move).</summary>
        private static GameObject Piece(GameObject parent, string name, Vector3 localPosition, Vector3 scale, Material material, bool solid)
        {
            GameObject obj = MenuLoopSetup.EnsureChildPrimitive(parent, name, PrimitiveType.Cube, localPosition, scale);
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (solid)
            {
                obj.isStatic = true;
                obj.layer = LayerMask.NameToLayer("Default");
            }
            else
            {
                Collider collider = obj.GetComponent<Collider>();
                if (collider != null) Object.DestroyImmediate(collider);
            }
            return obj;
        }
    }
}
#endif
