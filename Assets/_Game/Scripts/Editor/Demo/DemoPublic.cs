#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Story;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>
    /// Floor 1 of the demo, the public floor (the demo plan §3.2): G1 to G9 and U1, in greybox. The way: G1, G2, G5, G9,
    /// back to G5, U1. Side rooms: G3, G4, G6, G7, G8, and since 2026-11-05 G9b to G15 (DemoV2, the floorplan v2). The brain sector (B1) comes later and apart (Bruno, 2026-10-31):
    /// U1 ends with the Crown beat, a card for B1, the exit lines, the bulletin, and the title. No fire in G8 (rank 11 is
    /// out of the demo). Batch: tools/unity.sh exec GlorifiedBody.Editor.Demo.DemoPublic.BuildAll
    /// </summary>
    public static class DemoPublic
    {
        public const string Music = "public";
        public const string Released = "sector_c_released";
        public const string Reinforced = "demo_g5_reinforced";
        public const string ReinforcementGroup = "Group_Reinforcements";

        public static List<RoomSpec> Rooms()
        {
            var rooms = new List<RoomSpec>
            {
                Room("G1", "STAIR LANDING", 0, 0, 5, 5, 3f, G1, new Door('N', 2.5f, "S1"), new Door('E', 2.5f, "G2")),
                Room("G2", "THE CONCOURSE", 5, 0, 35, 6, 6f, G2, new Door('W', 2.5f, "G1"), new Door('N', 12, "G3"), new Door('N', 28, "G4"), new Door('S', 20, "G5", fire: true),
                    new Door('S', 8, "G13"), new Door('S', 30, "G10"), new Door('N', 18, "G16")),
                Room("G3", "THE DONOR WALL", 8, 6, 16, 12, 3.5f, G3, new Door('S', 12, "G2")),
                Room("G4", "THE CHAPEL", 24, 6, 32, 14, 4f, G4, new Door('S', 28, "G2")),
                // The floorplan v2 (2026-11-05) moved two side rooms off G5's walls: G13 takes G6's old place (it needs G2 and G7), G10
                // takes G8's (it needs G2 and G5). G6 is east of the hall under the cafeteria now, G8 south-west of the hall beside U1.
                Room("G5", "RECEPTION HALL", 12, -16, 28, 0, 5f, G5, new Door('N', 20, "G2", fire: true), new Door('W', -12, "G7"), new Door('E', -12, "G9"), new Door('S', 20, "U1", flag: Released),
                    new Door('E', -3.5f, "G10"), new Door('E', -7, "G6"), new Door('S', 13, "G8"), new Door('S', 27, "G12")),
                Room("G6", "PUBLIC BATHROOMS", 28, -9, 36, -5, 3f, G6, new Door('W', -7, "G5")),
                Room("G7", "FAMILY ROOMS", 4, -16, 12, -9, 3f, G7, new Door('E', -12, "G5"), new Door('N', 8, "G13")),
                Room("G8", "OFFICES AND RECORDS", 6, -22, 14, -16, 3f, G8, new Door('N', 13, "G5"), new Door('W', -19, "G11")),
                Room("G9", "SECURITY OFFICE", 28, -16, 34, -9, 3f, G9, new Door('W', -12, "G5"), new Door('E', -14.5f, "G9b")),
                Room("U1", "UNFINISHED WING", 14, -28, 26, -16, 4f, U1, new Door('N', 20, "G5"), new Door('E', -23, "G14")),
            };
            rooms.AddRange(DemoV2.PublicRooms()); // the floorplan v2 (2026-11-05): G9b to G15
            rooms.AddRange(DemoCoda.Rooms());     // the coda (2026-11-06): X1, R1, R2, R3
            rooms.AddRange(DemoBrain.Rooms());    // the brain sector (2026-11-09): B1, B2
            return rooms;
        }

        private static RoomSpec Room(string id, string title, float x0, float z0, float x1, float z1, float height, System.Action<DemoRoom> dress, params Door[] doors)
        {
            var spec = new RoomSpec { Id = id, Title = title, Floor = DemoItems.Floor1, Music = Music, X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Height = height, Dress = dress };
            spec.Doors.AddRange(doors);
            return spec;
        }

        /// <summary>The public floor is a part of the world scene: this builds the whole world (DemoWorld.BuildAll).</summary>
        public static void BuildAll() => DemoWorld.BuildAll();

        // ── The rooms ─────────────────────────────────────────────────

        /// <summary>G1: the first granite, a save machine, the gear (beat 17).</summary>
        private static void G1(DemoRoom r)
        {
            SaveAssets.SavePoint("Save_G1", new Vector3(0.6f, 0f, 1f), 90f).transform.SetParent(r.Props.transform, true);
            r.Prop("Planter", new Vector3(4.2f, 0f, 4.2f), new Vector3(0.8f, 0.8f, 0.8f), 0.2f);
            r.Box(r.Props, "Gear_Panel", new Vector3(2.5f, 1.8f, 0.17f), new Vector3(1f, 1f, 0.04f), DemoRoom.Grey(0.85f)); // 0.17 off the line: the wall is 0.3 m thick on it (2026-11-05)
        }

        /// <summary>G2: the concourse, two soldier pairs in the second half, the planters as cover, the mission statement.</summary>
        private static void G2(DemoRoom r)
        {
            for (int i = 0; i < 6; i++)
            {
                float x = 9f + i * 4.5f;
                r.Prop($"Planter_{i}", new Vector3(x, 0f, 3f + (i % 2 == 0 ? 0.8f : -0.8f)), new Vector3(1.2f, 0.95f, 1.2f), 0.2f);
                r.Prop($"Column_{i}", new Vector3(x + 2.2f, 0f, 5.4f), new Vector3(0.5f, 6f, 0.5f), 0.6f);
            }
            r.Figure("Soldier_Pair1_A", "soldier_rifle", new Vector3(24f, 0f, 2f), 270f);
            r.Figure("Soldier_Pair1_B", "soldier_pistol", new Vector3(25.5f, 0f, 4.2f), 270f);
            r.Figure("Soldier_Pair2_A", "soldier_rifle", new Vector3(31f, 0f, 1.6f), 270f);
            r.Figure("Soldier_Pair2_B", "soldier_shield", new Vector3(32.5f, 0f, 4f), 270f);
            // When these four are dead and sector C is released, a second fireteam of four comes (Bruno, 2026-11-06: "if the
            // player kills all four soldiers, then spawn in more soldiers"; the writer: round 32, no pairs; TeamReinforcement).
            // They stand beside the door to G5, hidden from that room by the wall, and run to the door of sector C.
            GameObject after = r.Group(ReinforcementGroup, Reinforced);
            after.GetComponent<FlagGate>().waitUnseen = false; // TeamReinforcement waits until she is out of this room
            r.Figure("Reinforcement_A", "soldier_rifle", new Vector3(16f, 0f, 1f), 90f, parent: after);
            r.Figure("Reinforcement_B", "soldier_pistol", new Vector3(24.5f, 0f, 1f), 270f, parent: after);
            r.Figure("Reinforcement_C", "soldier_rifle", new Vector3(16f, 0f, 4f), 90f, parent: after);
            r.Figure("Reinforcement_D", "soldier_shield", new Vector3(24.5f, 0f, 4f), 270f, parent: after);
            // The mission statement (the plan names the company: it waits for the name check, so only the motto for now).
            r.Sign("Sign_Mission", "SO THAT HUMANITY ENDURES.", new Vector3(20f, 4.2f, 5.83f), Vector3.back, new Vector2(6f, 0.8f), 72);
            r.Sign("Sign_Directory", "THE WARD  -  2ND FLOOR\nTHE CHAPEL\nRECEPTION\nSECURITY", new Vector3(5.17f, 1.6f, 4.6f), Vector3.right, new Vector2(1.2f, 1.2f), 36);
        }

        /// <summary>G3: the donor wall, the plaque, the scan C6 (beat 18). A safe room.</summary>
        private static void G3(DemoRoom r)
        {
            r.Box(r.Props, "Donor_Wall", new Vector3(12f, 1.75f, 11.83f), new Vector3(7.6f, 3.5f, 0.04f), DemoRoom.Colour("donor_wall", new Color(0.55f, 0.32f, 0.36f)));
            r.Sign("Sign_Plaque", "FOR THOSE WHO GAVE THE BODY.", new Vector3(12f, 1.2f, 11.79f), Vector3.back, new Vector2(2.2f, 0.35f), 40);
            r.Prop("Bench", new Vector3(12f, 0f, 8f), new Vector3(2f, 0.45f, 0.45f));
            r.Capsule("Capsule_G3", DrugType.Nalcon, new Vector3(9f, 0.15f, 7f));
            r.Scan("Scan_Donor_Wall", new Vector3(12f, 1.4f, 11.6f), "demo_c6_donor_wall", "PLACEHOLDER (C6): a hand pressed into wet paint, a larger hand on top of it.");
        }

        /// <summary>G4: the chapel, a nurse who prays and does not stop (a hider never found), the prayer card L8, the verse (beat 19).</summary>
        private static void G4(DemoRoom r)
        {
            for (int i = 0; i < 6; i++)
                r.Prop($"Pew_{i}", new Vector3(i % 2 == 0 ? 26.2f : 29.8f, 0f, 8f + (i / 2) * 1.6f), new Vector3(2.6f, 0.5f, 0.5f), 0.1f);
            r.Prop("Lectern", new Vector3(28f, 0f, 12.6f), new Vector3(0.6f, 1.1f, 0.5f), 0.15f);
            r.Capsule("Capsule_G4", DrugType.NalconLift, new Vector3(25f, 0.15f, 13.2f));
            r.Box(r.Props, "Gear", new Vector3(28f, 2.6f, 13.83f), new Vector3(1.2f, 1.2f, 0.04f), DemoRoom.Grey(0.9f));
            r.Sign("Sign_Verse", "IT IS SOWN IN WEAKNESS. IT IS RAISED IN POWER.", new Vector3(28f, 1.7f, 13.83f), Vector3.back, new Vector2(3.4f, 0.4f), 40);
            EnemySpawn nurse = r.Figure("Praying_Nurse", "nurse", new Vector3(29.8f, 0f, 11.4f), 0f, variant: 0);
            nurse.hides = true;
            nurse.foundAt = 0f; // she prays on, found or not
            nurse.hideLine = "Tonight we pray for the candidates in surgery, for the children of the programme.";
            EditorUtility.SetDirty(nurse);
            r.Item("L8_Prayer_Card", DemoItems.L8(), new Vector3(26.2f, 0.52f, 9.6f), new Vector3(0.1f, 0.02f, 0.16f));
        }

        /// <summary>G5: the reception hall, the fight, the sheeted door to sector C (a gate the security office opens, beat 20).</summary>
        private static void G5(DemoRoom r)
        {
            r.Prop("Reception_Desk", new Vector3(20f, 0f, -4f), new Vector3(6f, 1.1f, 1f), 0.1f);
            for (int i = 0; i < 4; i++) r.Prop($"Column_{i}", new Vector3(i < 2 ? 15f : 25f, 0f, i % 2 == 0 ? -4.5f : -11.5f), new Vector3(0.6f, 5f, 0.6f), 0.6f);
            for (int i = 0; i < 4; i++) r.Prop($"Waiting_Chairs_{i}", new Vector3(16f + i * 2.6f, 0f, -9f), new Vector3(1.6f, 0.5f, 0.5f), 0.3f);
            r.Figure("Soldier_A", "soldier_rifle", new Vector3(22f, 0f, -12f), 0f);
            r.Figure("Soldier_B", "soldier_pistol", new Vector3(17f, 0f, -13f), 20f);
            r.Figure("Lab_Pistol_A", "labworker_pistol", new Vector3(24f, 0f, -7f), 300f);
            r.Figure("Lab_Pistol_B", "labworker_pistol", new Vector3(14f, 0f, -7f), 60f);
            r.Figure("Civilian_A", "labworker", new Vector3(26.5f, 0f, -1.5f), 200f, variant: 0);
            r.Figure("Civilian_B", "nurse", new Vector3(15.5f, 0f, -14.5f), 30f, variant: 3); // 2 m east since v2: the door to G8 is at x 13
            // The sheeted door: plastic in the doorway until the security office releases sector C.
            GameObject sheet = MenuLoopSetup.EnsureChild(r.Story, "Gate_Sheeting");
            FlagGate gate = MenuLoopSetup.EnsureComponent<FlagGate>(sheet);
            gate.flag = Released;
            gate.showWhenRaised = false;
            r.Box(sheet, "Sheeting", new Vector3(20f, 1.2f, -16.1f), new Vector3(1.8f, 2.4f, 0.1f), DemoRoom.Colour("sheeting", new Color(0.7f, 0.72f, 0.7f)));
            r.Sign("Sign_Sector_C", "SECTOR C. CONSTRUCTION. ACCESS: SECURITY OFFICE.", new Vector3(20f, 2.7f, -15.83f), Vector3.forward, new Vector2(3.2f, 0.4f), 36);
            r.Capsule("Capsule_G5", DrugType.Nalcon, new Vector3(13f, 0.15f, -1.2f));
        }

        /// <summary>G6: the public bathrooms, a clerk who hides in a stall ("Occupied."). Since v2 at x 28..36, z -9..-5, east of the hall. A toilet that flushes (the hider answers it once), a faucet.</summary>
        private static void G6(DemoRoom r)
        {
            for (int i = 0; i < 4; i++) r.Prop($"Stall_Wall_{i}", new Vector3(29.5f + i * 1.5f, 0f, -5.95f), new Vector3(0.06f, 2f, 1.9f), 0.35f);
            DemoV2.Toilet(r, "Toilet", new Vector3(30.25f, 0f, -5.45f), Vector3.back, answer: "Occupied.");
            DemoV2.Toilet(r, "Toilet_B", new Vector3(33.25f, 0f, -5.45f), Vector3.back, works: false);
            EnemySpawn clerk = r.Figure("Hider_Clerk", "labworker", new Vector3(31.75f, 0f, -5.6f), 0f, variant: 0);
            clerk.hides = true;
            clerk.hideLine = "Occupied.";
            clerk.foundLine = "Please. Please don't.";
            EditorUtility.SetDirty(clerk);
            for (int i = 0; i < 3; i++) r.Prop($"Sink_{i}", new Vector3(29.3f + i * 1.6f, 0f, -8.6f), new Vector3(0.6f, 0.85f, 0.5f), 0.55f);
            DemoV2.Faucet(r, "Faucet", new Vector3(29.3f, 0.85f, -8.6f), Vector3.forward);
            r.Box(r.Props, "Mirror", new Vector3(30.9f, 1.4f, -8.83f), new Vector3(3.6f, 0.8f, 0.04f), DemoRoom.Colour("mirror", new Color(0.62f, 0.68f, 0.72f)));
            r.Sign("Sign_Name", "BATHROOMS", new Vector3(28.17f, 2f, -5.6f), Vector3.right, new Vector2(1.2f, 0.3f), 40);
        }

        /// <summary>G7: the family rooms, staff who shelter in place (hiders), the orientation program on the CRT (beat 21).</summary>
        private static void G7(DemoRoom r)
        {
            r.Prop("Table", new Vector3(8f, 0f, -12.5f), new Vector3(1.6f, 0.75f, 1f), 0.3f);
            r.Capsule("Capsule_G7", DrugType.NalconFire, new Vector3(6f, 0.15f, -10f));
            var lines = new List<TapeLine>();
            float at = 1f;
            // The voice is Kyoko, not named in act 1 (P5): the subtitle names the program.
            foreach ((string _, string text) in DemoScripts.Lines("lines/T7_orientation_program.md"))
            {
                lines.Add(new TapeLine { at = at, speaker = "THE PROGRAM", text = text });
                at += Mathf.Max(3f, Subtitles.SubtitleService.ReadSeconds(text) + 0.8f);
            }
            r.Crt("Screen_Program", new Vector3(4.6f, 0f, -14f), 90f, lines, interactive: true, loop: true);
            for (int i = 0; i < 2; i++)
            {
                EnemySpawn staff = r.Figure($"Hider_Staff_{i}", i == 0 ? "labworker" : "nurse", new Vector3(11.2f, 0f, -15.4f + i * 1.2f), 135f, variant: i + 1);
                staff.hides = true;
                staff.hideLine = CivilianBrain.LineBeg;
                staff.foundLine = "We're sheltering. We're only sheltering.";
                EditorUtility.SetDirty(staff);
            }
        }

        /// <summary>G8: offices and records, two clerks, the paper (no fire in the demo: rank 11 is out). Since v2 at x 6..14, z -22..-16, south-west of the hall, with a door to the staff break room G11.</summary>
        private static void G8(DemoRoom r)
        {
            for (int i = 0; i < 3; i++) r.Prop($"Desk_{i}", new Vector3(8.5f + i * 2.1f, 0f, -18.5f), new Vector3(1.4f, 0.75f, 0.7f), 0.25f);
            for (int i = 0; i < 3; i++) r.Prop($"Shelves_{i}", new Vector3(8.5f + i * 2.1f, 0f, -21.6f), new Vector3(1.8f, 2f, 0.4f), 0.2f);
            r.Prop("Copier", new Vector3(13.3f, 0f, -20.3f), new Vector3(0.6f, 1.1f, 0.7f), 0.5f);
            r.Figure("Clerk_A", "labworker", new Vector3(9.5f, 0f, -19.6f), 270f, variant: 1);
            r.Figure("Clerk_B", "labworker", new Vector3(12.2f, 0f, -20.3f), 250f, variant: 3);
            r.Sign("Sign_Name", "RECORDS", new Vector3(10.2f, 2f, -16.17f), Vector3.back, new Vector2(1.2f, 0.3f), 40);
        }

        /// <summary>G9: the security office: the monitors, the chair and its scan C7, the logbook L5, the release (beat 23).</summary>
        private static void G9(DemoRoom r)
        {
            r.Prop("Desk", new Vector3(31f, 0f, -14.8f), new Vector3(3.4f, 0.75f, 0.8f), 0.2f); // 3.4 m since v2: 1.3 m to the east wall and the door to G9b
            r.CrtWall("Screen_Monitors", new Vector3(31f, 0.8f, -15.75f), 5, 2, new List<TapeLine>
            {
                new TapeLine { at = 0.5f, speaker = "MONITOR", text = "PLACEHOLDER: her face on every monitor." },
            }, faceZ: 0.24f);
            GameObject chair = r.Prop("Security_Chair", new Vector3(31f, 0f, -13.4f), new Vector3(0.6f, 1f, 0.6f), 0.15f);
            r.Scan("Scan_Security_Chair", chair.transform.position + Vector3.up * 0.4f, "demo_c7_chair", "PLACEHOLDER (C7): a man's hands on a row of switches. A wedding ring. A cold coffee.");
            r.Item("L5_Logbook", DemoItems.L5(), new Vector3(29.6f, 0.78f, -14.8f), new Vector3(0.24f, 0.03f, 0.32f));
            r.Switch("Release_Switch", new Vector3(33.6f, 1.2f, -12.5f), Released, "Release the sector C door?", "release the sector C door",
                "Somewhere in the hall, the sheeting on the sector C door drops.");
            SaveAssets.SavePoint("Save_G9", new Vector3(33.4f, 0f, -10f), 270f).transform.SetParent(r.Props.transform, true);
            r.Sign("Sign_Comms", "COMMS. AUTHORISED PERSONNEL.", new Vector3(33.83f, 2f, -13f), Vector3.left, new Vector2(1.6f, 0.3f), 36); // the door to G9b (v2)
        }

        /// <summary>
        /// U1: the unfinished wing, a save machine; at its far end the Crown beat (24): the grab tears a uniform off, the items
        /// over the bare limit fall. Then the cut to B1, the brain fight (DemoBrain, 2026-11-09); the exit lines (cutscene 10)
        /// play past it, in B2.
        /// </summary>
        private static void U1(DemoRoom r)
        {
            SaveAssets.SavePoint("Save_U1", new Vector3(15f, 0f, -18f), 90f).transform.SetParent(r.Props.transform, true);
            for (int i = 0; i < 4; i++)
            {
                // The fourth sheet hangs at the far end since v2: it hung in front of the door to G14 (x 26, z -23).
                Vector3 at = i == 3 ? new Vector3(25f, 1.8f, -27.3f) : new Vector3(16f + i * 3f, 1.8f, -21f - (i % 2) * 2f);
                r.Box(r.Props, $"Sheeting_{i}", at, new Vector3(2f, 3.6f, 0.04f), DemoRoom.Colour("sheeting", new Color(0.7f, 0.72f, 0.7f)));
            }
            r.Prop("Work_Light_A", new Vector3(17f, 0f, -26f), new Vector3(0.3f, 1.8f, 0.3f), 0.8f);
            r.Prop("Cement_Mixer", new Vector3(24f, 0f, -26.5f), new Vector3(1f, 1.2f, 1f), 0.35f);
            r.Prop("Ladder", new Vector3(14.6f, 0f, -24f), new Vector3(0.5f, 2.6f, 0.1f), 0.5f);
            var crown = new List<ScriptStep>
            {
                ScriptStep.LockHer(),
                ScriptStep.Radio("", "u1_grab", "[The sheeting moves with no wind. Something takes the Crown.]"),
                ScriptStep.Outfit(GlorifiedBody.Interaction.OutfitChange.NudeId), // round 20: the grab tears the uniform off
                ScriptStep.Wait(1f),
            };
            // The way on is the brain sector (DemoBrain), then the coda: the bulletin, the title and the end lie at its end.
            crown.Add(ScriptStep.Raise(DemoCoda.CrownGone));
            crown.Add(ScriptStep.MoveHer(DemoBrain.EntryB1));
            crown.Add(ScriptStep.FreeHer());
            r.Beat("U1_Crown", "demo_u1_crown", new Vector3(20f, 1f, -25.5f), new Vector3(10f, 2f, 3f), crown.ToArray());
            DemoV2.EncoreInU1(r, new Vector3(20f, 1f, -25.5f), new Vector3(10f, 2f, 3f)); // cutscene 09, layer 4: "Encore." if she read the card
        }
    }
}
#endif
