#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Audio;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Story;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>
    /// The Dining Hall (beat 23b, CANON round 60: the set piece, the obligatory item; the details PROPOSED). G18, the
    /// restaurant of the lodge, east of the concourse G2 through its glass doors: the second fireteam of four holds it as
    /// its forward post (round 62) and does not leave it before she takes the duty key (SoldierBrain.LeashUntil). G19, its
    /// kitchen: the duty key on its hook in the office, the key log L34, the night manager by walk-in 1, L33 over the pass.
    /// The release of sector C in G9 takes the key (DUTY KEY REQUIRED, L30 on the console).
    /// The built map differs from the plan in one place: G9 has no free wall (G5, G6, G9b and G12 close it in), so the
    /// service exit of the kitchen opens into G9b, the comms closet, and G9b opens into G9. The serving hatch to G10 is
    /// a shutter on each face of the wall, with no opening yet.
    /// </summary>
    public static class DemoDining
    {
        public const string DutyKeyId = "duty_key";
        public const string DutyKeyTaken = "demo_duty_key_taken";
        public const string TeamGroup = "Hall_Team";

        public static List<RoomSpec> Rooms() => new List<RoomSpec>
        {
            Spec("G18", "THE DINING HALL", 35, 0, 47, 10, 5f, G18, new Door('W', 3, "G2", width: 2.4f), new Door('S', 41, "G19")),
            Spec("G19", "THE KITCHEN", 38, -12, 44, 0, 3.5f, G19, new Door('N', 41, "G18"), ServiceExit("G9b"), new Door('S', 43, "G17b")), // G17b, facilities, and the halon room (2026-11-10)
        };

        /// <summary>The service exit: a push bar on the kitchen side only (a jammed fire door, its one lever in G19, no kick breaks it).</summary>
        public static Door ServiceExit(string to) =>
            new Door(to == "G19" ? 'E' : 'W', -10.8f, to, fire: true, jammed: true, leverRoom: "G19", unbreakable: true);

        public static ItemDefinition DutyKey() =>
            ItemAssets.Item(DutyKeyId, "THE DUTY KEY", ItemKind.Key, ItemAssets.KeyColour, "A brass key on a red tag: DUTY. It turns the release in the security office.", "", GlorifiedBody.Player.DrugType.Nalcon);

        private static RoomSpec Spec(string id, string title, float x0, float z0, float x1, float z1, float height, System.Action<DemoRoom> dress, params Door[] doors)
        {
            var spec = new RoomSpec { Id = id, Title = title, Floor = DemoItems.Floor1, Music = DemoPublic.Music, X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Height = height, Dress = dress };
            spec.Doors.AddRange(doors);
            return spec;
        }

        // ── G18 ───────────────────────────────────────────────────────

        private static void G18(DemoRoom r)
        {
            // The host stand by the glass doors: the book of tonight (L31) and a comment card (L32).
            r.Prop("Host_Stand", new Vector3(36.6f, 0f, 4.9f), new Vector3(0.6f, 1.1f, 0.5f), 0.15f);
            r.Item("L31_Reservation_Book", L31(), new Vector3(36.55f, 1.11f, 4.9f), new Vector3(0.3f, 0.03f, 0.22f));
            r.Item("L32_Comment_Card", L32(), new Vector3(36.8f, 1.11f, 4.75f), new Vector3(0.1f, 0.01f, 0.14f));
            r.Prop("Menu_Stand", new Vector3(37.4f, 0f, 1.2f), new Vector3(0.1f, 1.2f, 0.5f), 0.7f);
            r.Sign("Sign_Winter_Menu", "TONIGHT: THE WINTER MENU.", new Vector3(37.34f, 1f, 1.2f), Vector3.left, new Vector2(0.45f, 0.25f), 20);

            // The hall: round columns, banquettes, the bar at the east wall, the tables on their sides (the soldiers' cover).
            foreach ((string tag, float x, float z) in new[] { ("A", 39.5f, 3.2f), ("B", 39.5f, 7.2f), ("C", 43.2f, 3.2f), ("D", 43.2f, 7.2f) })
                r.Prop("Column_" + tag, new Vector3(x, 0f, z), new Vector3(0.6f, 5f, 0.6f), 0.08f);
            r.Prop("Banquette_N", new Vector3(37.6f, 0f, 9.45f), new Vector3(2.6f, 0.9f, 0.6f), 0.35f);
            r.Prop("Banquette_S", new Vector3(41f, 0f, 0.55f), new Vector3(2.6f, 0.9f, 0.6f), 0.35f);
            r.Prop("Bar", new Vector3(46.2f, 0f, 5f), new Vector3(0.8f, 1.1f, 5f), 0.1f);
            foreach ((string tag, float x, float z, bool across) in new[] { ("A", 41.2f, 5.2f, true), ("B", 44.4f, 2.4f, false), ("C", 44.4f, 8.2f, false), ("D", 38.6f, 5.2f, true) })
                r.Prop("Table_Side_" + tag, new Vector3(x, 0f, z), across ? new Vector3(0.12f, 0.9f, 1.4f) : new Vector3(1.4f, 0.9f, 0.12f), 0.85f);

            // The lodge door, locked tonight (off the map), and its words.
            r.Box(r.Props, "Lodge_Door", new Vector3(44f, 1.2f, 9.9f), new Vector3(2f, 2.4f, 0.1f), DemoRoom.Grey(0.2f));
            r.Sign("Sign_Lodge", "THE LODGE. FOR THE FAMILIES OF OUR PATIENTS, AND FOR OUR GUESTS. THE CITY IS AN HOUR AWAY. STAY CLOSE TO THEM.", new Vector3(44f, 2.85f, 9.83f), Vector3.back, new Vector2(2.6f, 0.4f), 22);
            r.Sign("Sign_Lodge_Closed", "LODGE CLOSED. GUESTS: STAY IN YOUR ROOMS. A MEMBER OF STAFF WILL COME TO YOU.", new Vector3(44f, 1.5f, 9.8f), Vector3.back, new Vector2(0.8f, 0.35f), 16);

            // The second fireteam (round 62): its forward post, the lodge door behind it. Ortiz and Brandt, and two more.
            GameObject team = MenuLoopSetup.EnsureChild(r.Figures, TeamGroup);
            foreach ((string name, string kind, Vector3 at) in new[]
            {
                ("Ortiz", "soldier_rifle", new Vector3(40.4f, 0f, 2.2f)),
                ("Brandt", "soldier_pistol", new Vector3(41.6f, 0f, 8.3f)),
                ("Hall_Soldier_C", "soldier_rifle", new Vector3(44.8f, 0f, 4.2f)),
                ("Hall_Soldier_D", "soldier_shield", new Vector3(45.4f, 0f, 6.6f)), // the shield at the bar
            })
            {
                EnemySpawn s = r.Figure(name, kind, at, 270f, parent: team);
                s.leashUntilFlag = DutyKeyTaken;
                EditorUtility.SetDirty(s);
            }

            // Its own track (§12, round 103): it starts when they see her in the hall, it ends with the last one.
            GameObject holder = MenuLoopSetup.EnsureChild(r.Story, "Score_G18");
            RoomScore score = MenuLoopSetup.EnsureComponent<RoomScore>(holder);
            score.roomId = "G18";
            score.figures = team;
            EditorUtility.SetDirty(score);
        }

        /// <summary>The words over the glass doors, on the G2 face of the wall (§7).</summary>
        public static void GlassDoors(DemoRoom g2) =>
            g2.Sign("Sign_Dining_Hall", "THE DINING HALL. FOR OUR FAMILIES AND OUR GUESTS.", new Vector3(34.83f, 2.9f, 3f), Vector3.left, new Vector2(2.6f, 0.35f), 28);

        // ── G19 ───────────────────────────────────────────────────────

        private static void G19(DemoRoom r)
        {
            // The pass, the note of the chef over it (L33), the range, two walk-ins.
            r.Prop("Pass", new Vector3(41f, 0f, -2.3f), new Vector3(3.2f, 1f, 0.7f), 0.7f);
            r.Item("L33_Chef_Note", L33(), new Vector3(41.7f, 1.01f, -2.3f), new Vector3(0.21f, 0.02f, 0.28f));
            r.Prop("Range", new Vector3(38.8f, 0f, -6.4f), new Vector3(1.2f, 0.95f, 2.4f), 0.2f);
            r.Prop("Walk_In_1", new Vector3(43.15f, 0f, -5f), new Vector3(1.6f, 2.4f, 2.2f), 0.75f);
            r.Prop("Walk_In_2", new Vector3(43.15f, 0f, -7.6f), new Vector3(1.6f, 2.4f, 2.2f), 0.75f);
            r.Sign("Sign_Walk_In_1", "WALK-IN 1", new Vector3(42.33f, 1.9f, -5f), Vector3.left, new Vector2(0.6f, 0.18f), 18);

            // The night manager of the lodge by walk-in 1: he begs her to take the key.
            EnemySpawn manager = r.Figure("Night_Manager", "labworker", new Vector3(41.9f, 0f, -6.4f), 90f, variant: 1);
            manager.hides = true;
            manager.hideLine = "Kitchen's closed. Kitchen's closed. Kitchen's closed."; // the writer's (barks.csv, round 113, PROPOSED)
            manager.foundLine = "Ma'am. The key's on the board, in the office. Take it. I'll sign it out. Just take it and go.";
            EditorUtility.SetDirty(manager);

            // The kitchen office in the south-west corner: the key board, the DUTY KEY on its hook, the key log (L34).
            r.Prop("Office_Desk", new Vector3(40.9f, 0f, -11.3f), new Vector3(1.2f, 0.75f, 0.6f), 0.25f);
            r.Box(r.Props, "Key_Board", new Vector3(40.2f, 1.5f, -11.85f), new Vector3(0.9f, 0.5f, 0.04f), DemoRoom.Colour("key_board", new Color(0.42f, 0.3f, 0.2f)));
            r.Item("Duty_Key", DutyKey(), new Vector3(39.95f, 1.45f, -11.8f), new Vector3(0.06f, 0.12f, 0.02f));
            r.Item("L34_Key_Log", L34(), new Vector3(40.55f, 1.45f, -11.8f), new Vector3(0.21f, 0.28f, 0.02f));
            r.Beat("G19_Duty_Key", "demo_g19_duty_key", Vector3.zero, Vector3.zero, ScriptStep.WaitItem(DutyKeyId), ScriptStep.Raise(DutyKeyTaken));

            // The service exit into G9b (the closet of G9) and the serving hatch to G10, with their words (§7).
            r.Sign("Sign_Service_Exit", "SERVICE EXIT. SECURITY OFFICE. PUSH BAR.", new Vector3(38.17f, 2.45f, -10.8f), Vector3.right, new Vector2(1.6f, 0.3f), 22);
            r.Sign("Sign_Service_Exit_Back", "SERVICE EXIT. SECURITY OFFICE. NO ENTRY FROM THIS SIDE.", new Vector3(37.83f, 2.45f, -10.8f), Vector3.left, new Vector2(1.6f, 0.3f), 20);
            foreach ((string tag, float x, Vector3 facing) in new[] { ("G19", 38.17f, Vector3.right), ("G10", 37.83f, Vector3.left) })
            {
                r.Box(r.Props, "Hatch_Shutter_" + tag, new Vector3(x + facing.x * 0.01f, 1.35f, -2.5f), new Vector3(0.03f, 0.6f, 1.2f), DemoRoom.Grey(0.55f));
                r.Sign("Sign_Servery_" + tag, "SERVERY CLOSED.", new Vector3(x + facing.x * 0.03f, 1.35f, -2.5f), facing, new Vector2(0.9f, 0.2f), 20);
            }
        }

        // ── G9: the release takes the key ─────────────────────────────

        /// <summary>The key switch of the release, the line without the key, and the card L30 on the console (§7, §8).</summary>
        public static void Release(DemoRoom g9, FlagSwitch release)
        {
            release.requiredItem = DutyKeyId;
            release.missingText = "DUTY KEY REQUIRED.";
            EditorUtility.SetDirty(release);
            g9.Sign("Sign_Release", "SECTOR C RELEASE. DUTY KEY.", new Vector3(33.83f, 1.65f, -12.5f), Vector3.left, new Vector2(0.8f, 0.2f), 18);
            g9.Item("L30_Release_Card", L30(), new Vector3(33.82f, 1.2f, -13.1f), new Vector3(0.01f, 0.2f, 0.28f));
        }

        // ── The logs (§8, L30 to L34) ─────────────────────────────────

        public static ItemDefinition L30() => DemoItems.Log("l30_release_card", "A FACILITIES CARD",
            "FACILITIES. SECTOR C RELEASE. TWO PEOPLE, TWO KEYS: THE SECURITY OFFICE AND THE DUTY KEY. AFTER HOURS THE DUTY KEY IS WITH THE NIGHT MANAGER OF THE LODGE, IN THE KITCHEN OFFICE OF THE DINING HALL. NEVER LEAVE THE DUTY KEY IN THE CONSOLE.");

        public static ItemDefinition L31() => DemoItems.Log("l31_reservation_book", "THE BOOK OF TONIGHT",
            "THE DINING HALL. 30 DECEMBER.\n19:00 LODGE 4, 2.\n19:30 LODGE 11, 3.\n20:00 LODGE 7, 2.\n21:30 THE PRIVATE ROOM, ALTERIUS, 6, BEFORE THE MEETING. TASTING MENU.\n21:30 DE HOMEM-CRISTO, 1, THE PRIVATE ROOM. CANCELLED BY THE GUEST. HER DRIVER WAITED IN THE CAR.");

        public static ItemDefinition L32() => DemoItems.Log("l32_comment_card", "A GUEST COMMENT CARD",
            "GUEST COMMENTS. ROOM 11. Thank you for the lodge. We drove an hour each way for nine days before you had a room for us. But no Christmas dinner? Our son spent Christmas upstairs, and we spent it down here with \"the winter menu\". The roast was very good.");

        public static ItemDefinition L33() => DemoItems.Log("l33_chef_note", "A NOTE FROM THE CHEF",
            "FROM THE CHEF. If a guest asks for a Christmas menu, offer the winter menu. Do not say no, and do not say Christmas. Future Day is in February, and it is the best night of our year. The private room gets the tasting menu, no substitutions.\n\nBy hand under it: Turkey for the caf is in walk-in 2. It is for GENE'S BIRTHDAY. Nobody touch it.");

        public static ItemDefinition L34() => DemoItems.Log("l34_key_log", "THE KEY LOG",
            "DUTY KEY.\n27 DEC: OUT 18:00, IN 06:00.\n28 DEC: OUT 18:00, IN 06:00.\n29 DEC: OUT 18:00. 02:10 SHOWN TO SECURITY ON REQUEST (STEVENS). 02:14 BACK ON THE HOOK. IN 06:00.\n30 DEC: OUT 18:00. IN: (empty)");
    }
}
#endif
