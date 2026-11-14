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
                    new Door('S', 8, "G13"), new Door('S', 30, "G10"), new Door('N', 18, "G16"), new Door('E', 3, "G18", width: 2.4f)), // the glass doors of the Dining Hall (2026-11-10)
                Room("G3", "THE DONOR WALL", 8, 6, 16, 12, 3.5f, G3, new Door('S', 12, "G2")),
                Room("G4", "THE CHAPEL", 24, 6, 32, 14, 4f, G4, new Door('S', 28, "G2")),
                // The floorplan v2 (2026-11-05) moved two side rooms off G5's walls: G13 takes G6's old place (it needs G2 and G7), G10
                // takes G8's (it needs G2 and G5). G6 is east of the hall under the cafeteria now, G8 south-west of the hall beside U1.
                // The round foyer (rounds 147 and 148; Bruno, 2026-11-14: "the foyer should be in the middle of the map ... the other rooms
                // connected to it like spokes on a wheel"; "yes, i like the round foyer in g5"). The reception hall is round now.
                Room("G5", "THE FOYER", 12, -16, 28, 0, 5f, G5, new Door('N', 20, "G2", fire: true), new Door('W', -12, "G7"), new Door('E', -12, "G9"), new Door('S', 20, "U1", flag: Released),
                    new Door('E', -3.5f, "G10"), new Door('E', -7, "G6"), new Door('S', 13, "G8"), new Door('S', 27, "G12"), new Door('W', -4, "G13")),
                Room("G6", "PUBLIC BATHROOMS", 28, -9, 36, -5, 3f, G6, new Door('W', -7, "G5")),
                Room("G7", "FAMILY ROOMS", 4, -16, 12, -9, 3f, G7, new Door('E', -12, "G5"), new Door('N', 8, "G13")),
                Room("G8", "OFFICES AND RECORDS", 6, -22, 14, -16, 3f, G8, new Door('N', 13, "G5"), new Door('W', -19, "G11")),
                Room("G9", "SECURITY OFFICE", 28, -16, 34, -9, 3f, G9, new Door('W', -12, "G5"), new Door('E', -14.5f, "G9b")),
                Room("U1", "UNFINISHED WING", 14, -28, 26, -16, 4f, U1, new Door('N', 20, "G5"), new Door('E', -23, "G14")),
            };
            rooms.AddRange(DemoV2.PublicRooms()); // the floorplan v2 (2026-11-05): G9b to G15
            rooms.AddRange(DemoCoda.Rooms());     // the coda (2026-11-06): X1, R1, R2, R3
            rooms.AddRange(DemoBrain.Rooms());    // the brain sector (2026-11-09): B1, B2
            rooms.AddRange(DemoDining.Rooms());   // the Dining Hall (2026-11-10): G18, G19
            rooms.AddRange(DemoHalon.Rooms());    // the halon room (2026-11-10): G17b, G17
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
            DemoArt.Place(r, "Art_Gear_G1", "A5", new Vector3(2.5f, 1.4f, 0.5f), "the gear"); // round 138: the art texts are audio logs
        }

        /// <summary>G2: the concourse, two soldier pairs in the second half, the planters as cover, the mission statement.</summary>
        private static void G2(DemoRoom r)
        {
            for (int i = 0; i < 6; i++)
            {
                float x = 9f + i * 4.5f;
                r.Planter($"Planter_{i}", new Vector3(x, 0f, 3f + (i % 2 == 0 ? 0.8f : -0.8f)), new Vector3(1.2f, 0.95f, 1.2f)); // she moves them (§10.5)
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
            DemoDining.GlassDoors(r); // the words over the glass doors of the Dining Hall
            DemoArt.Place(r, "Art_Concourse", "A4", new Vector3(18f, 1.4f, 5.4f), "the concourse"); // under the motto, by the granite
            r.Sign("Sign_Mission", DemoPlan.Sign("G2, the concourse", "SO THAT HUMANITY ENDURES."), new Vector3(20f, 4.2f, 5.83f), Vector3.back, new Vector2(6f, 0.8f), 72);
            r.Sign("Sign_Directory", "THE WARD  -  2ND FLOOR\nTHE CHAPEL\nRECEPTION\nSECURITY", new Vector3(5.17f, 1.6f, 4.6f), Vector3.right, new Vector2(1.2f, 1.2f), 36);
        }

        /// <summary>G3: the donor wall, the plaque, the scan C6 (beat 18). A safe room.</summary>
        private static void G3(DemoRoom r)
        {
            DemoForeground.G3(r); // the wall alone in the frame (2026-11-14)
            r.Box(r.Props, "Donor_Wall", new Vector3(12f, 1.75f, 11.83f), new Vector3(7.6f, 3.5f, 0.04f), DemoRoom.Colour("donor_wall", new Color(0.55f, 0.32f, 0.36f)));
            r.Sign("Sign_Plaque", DemoPlan.Sign("G3, the donor wall", "FOR THOSE WHO GAVE THE BODY."), new Vector3(12f, 1.2f, 11.79f), Vector3.back, new Vector2(2.2f, 0.35f), 40);
            DemoArt.Place(r, "Art_Palms", "A1", new Vector3(10f, 1.4f, 11.4f), "the palms");
            DemoArt.Place(r, "Art_Tree", "A2", new Vector3(14.2f, 1.4f, 11.4f), "the tree");
            r.Prop("Bench", new Vector3(12f, 0f, 8f), new Vector3(2f, 0.45f, 0.45f));
            r.Capsule("Capsule_G3", DrugType.Nalcon, new Vector3(9f, 0.15f, 7f));
            r.Scan("Scan_Donor_Wall", new Vector3(12f, 1.4f, 11.6f), "demo_c6_donor_wall", DemoPlan.ScanCard("C6", "PLACEHOLDER (C6): a hand pressed into wet paint, a larger hand on top of it."));
        }

        /// <summary>G4: the chapel, a nurse who prays and does not stop (a hider never found), the prayer card L8, the verse (beat 19).</summary>
        private static void G4(DemoRoom r)
        {
            DemoForeground.G4(r); // the candle stand in the foreground (2026-11-14)
            for (int i = 0; i < 6; i++)
                r.Prop($"Pew_{i}", new Vector3(i % 2 == 0 ? 26.2f : 29.8f, 0f, 8f + (i / 2) * 1.6f), new Vector3(2.6f, 0.5f, 0.5f), 0.1f);
            r.Prop("Lectern", new Vector3(28f, 0f, 12.6f), new Vector3(0.6f, 1.1f, 0.5f), 0.15f);
            r.Capsule("Capsule_G4", DrugType.NalconLift, new Vector3(25f, 0.15f, 13.2f));
            r.Box(r.Props, "Gear", new Vector3(28f, 2.6f, 13.83f), new Vector3(1.2f, 1.2f, 0.04f), DemoRoom.Grey(0.9f));
            r.Sign("Sign_Verse", DemoPlan.Sign("G4, the chapel, under the gear", "IT IS SOWN IN WEAKNESS. IT IS RAISED IN POWER."), new Vector3(28f, 1.7f, 13.83f), Vector3.back, new Vector2(3.4f, 0.4f), 40);
            DemoArt.Place(r, "Art_Verse", "A3", new Vector3(28f, 1.4f, 13.4f), "the verse");
            EnemySpawn nurse = r.Figure("Praying_Nurse", "nurse", new Vector3(29.8f, 0f, 11.4f), 0f, variant: 0);
            nurse.hides = true;
            nurse.foundAt = 0f; // she prays on, found or not
            nurse.hideLine = "Tonight we pray for the candidates in surgery, for the children of the programme.";
            EditorUtility.SetDirty(nurse);
            r.Item("L8_Prayer_Card", DemoItems.L8(), new Vector3(26.2f, 0.52f, 9.6f), new Vector3(0.1f, 0.02f, 0.16f));
        }

        /// <summary>G5: the reception hall, the fight, the sheeted door to sector C (a gate the security office opens, beat 20).</summary>
        // ── G5, the round foyer (rounds 147 and 148; Bruno, 2026-11-14) ─────────────────────────────

        public static readonly Vector3 FoyerCentre = new Vector3(20f, 0f, -8f);
        public const float FoyerRadius = 7.5f, FoyerCorridor = 0.9f;

        /// <summary>The lift car in the north-west corner of the square, behind the ring (the art puzzle's locked place).</summary>
        public static readonly Vector3 LiftCar = new Vector3(13f, 0f, -2.34f);
        public const string PlinthsFlag = "demo_g5_plinths", LiftCarName = "Lift_Car_G5", LiftLandingName = "Lift_Landing_W2";

        /// <summary>The spoke doors of the foyer on G5's walls (plan metres): G2, G13, G7, G10, G6, G9, G8, U1, G12, and the lift car.</summary>
        public static readonly Vector3[] FoyerSpokes =
        {
            new Vector3(20f, 0f, 0f), new Vector3(12f, 0f, -4f), new Vector3(12f, 0f, -12f), new Vector3(28f, 0f, -3.5f), new Vector3(28f, 0f, -7f),
            new Vector3(28f, 0f, -12f), new Vector3(13f, 0f, -16f), new Vector3(20f, 0f, -16f), new Vector3(27f, 0f, -16f), LiftCar,
        };

        /// <summary>
        /// G5, the foyer: the hub of floor 1, round (the picture of round 147: a circular lift lobby, curved frosted panels, chrome
        /// columns lit from below, a lit skylight with a cross grid, a dark round floor). The ring stands in the square of the old
        /// reception hall. The corners behind it are solid, with a short corridor from each of the nine spoke doors to the ring
        /// (the camera coverage skips a floor point under a solid thing). Modesty stands in the centre under the skylight, larger
        /// than life, with her art log (A8). The fight of the hall and the sheeted door of sector C stay.
        /// </summary>
        private static void G5(DemoRoom r)
        {
            Vector3 c = FoyerCentre;
            float h = r.Spec.Height;
            Material panel = DemoRoom.Grey(0.62f), fill = DemoRoom.Grey(0.2f), chrome = DemoRoom.Colour("chrome", new Color(0.78f, 0.8f, 0.84f));
            // The corners: solid cells of 0.5 m outside the ring, except the corridors of the spokes; one box for each run of cells.
            const float cell = 0.5f;
            for (float z = r.Spec.Z0 + cell * 0.5f; z < r.Spec.Z1; z += cell)
            {
                float runStart = float.NaN;
                for (float x = r.Spec.X0 + cell * 0.5f; x < r.Spec.X1 + cell; x += cell)
                {
                    bool solid = x < r.Spec.X1 && FoyerSolid(new Vector3(x, 0f, z));
                    if (solid && float.IsNaN(runStart)) runStart = x;
                    if (!solid && !float.IsNaN(runStart))
                    {
                        float x0 = runStart - cell * 0.5f, x1 = x - cell * 0.5f;
                        r.Box(r.Room, $"Foyer_Fill_{z:0.0}_{x0:0.0}", new Vector3((x0 + x1) * 0.5f, h * 0.5f, z), new Vector3(x1 - x0, h, cell), fill);
                        runStart = float.NaN;
                    }
                }
            }
            // The ring of frosted panels, open at the spokes.
            for (int i = 0; i < 48; i++)
            {
                float deg = i * 7.5f;
                if (SpokeNear(deg, 8.5f)) continue;
                Vector3 dir = Quaternion.Euler(0f, deg, 0f) * Vector3.forward;
                GameObject piece = r.Box(r.Room, $"Foyer_Panel_{i:00}", c + dir * FoyerRadius + Vector3.up * h * 0.5f, new Vector3(1.0f, h, 0.12f), panel);
                piece.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
            }
            // Eight chrome columns in the free arcs between the spokes.
            foreach (float deg in new[] { 20f, 40f, 100f, 160f, 200f, 270f, 322f, 340f }) // the free arcs (0 north, 90 east)
            {
                Vector3 dir = Quaternion.Euler(0f, deg, 0f) * Vector3.forward;
                if (SpokeNear(deg, 7f)) continue;
                r.Box(r.Props, $"Foyer_Column_{deg:000}", c + dir * 5.8f + Vector3.up * h * 0.5f, new Vector3(0.36f, h, 0.36f), chrome);
            }
            // The skylight: a lit disc under the ceiling with a cross grid.
            GameObject sky = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sky.name = "Foyer_Skylight";
            sky.transform.SetParent(r.Room.transform, false);
            sky.transform.position = c + Vector3.up * (h - 0.06f);
            sky.transform.localScale = new Vector3(5f, 0.02f, 5f);
            Object.DestroyImmediate(sky.GetComponent<Collider>());
            sky.GetComponent<Renderer>().sharedMaterial = DemoRoom.Colour("skylight", new Color(1f, 0.97f, 0.88f));
            for (int g = -2; g <= 2; g++)
            {
                Object.DestroyImmediate(r.Box(r.Room, $"Foyer_Grid_X{g}", c + new Vector3(g * 0.95f, h - 0.1f, 0f), new Vector3(0.06f, 0.05f, 5f), fill).GetComponent<Collider>());
                Object.DestroyImmediate(r.Box(r.Room, $"Foyer_Grid_Z{g}", c + new Vector3(0f, h - 0.1f, g * 0.95f), new Vector3(5f, 0.05f, 0.06f), fill).GetComponent<Collider>());
            }
            // Modesty (A8), larger than life, PLACEHOLDER: the plinth, the body under the veil, the head, the veil's fall.
            Material marble = DemoRoom.Colour("marble", new Color(0.92f, 0.9f, 0.86f));
            r.Box(r.Props, "Modesty_Plinth", c + Vector3.up * 0.5f, new Vector3(1.4f, 1f, 1.4f), marble);
            r.Box(r.Props, "Modesty_Body", c + Vector3.up * 2.05f, new Vector3(0.75f, 2.1f, 0.6f), marble);
            r.Box(r.Props, "Modesty_Head", c + new Vector3(0f, 3.35f, 0.04f), new Vector3(0.4f, 0.48f, 0.42f), marble);
            r.Sign("Sign_Modesty", "MODESTY. AFTER ANTONIO CORRADINI, 1752.", c + new Vector3(0f, 0.55f, -0.72f), Vector3.back, new Vector2(1.2f, 0.18f), 18);
            DemoArt.Place(r, "Art_Modesty", "A8", c + new Vector3(0f, 1.4f, -0.9f), "the statue"); // round 148: her art log on Modesty
            // Two rows of waiting chairs on the south side (the bases of the art puzzle take the room of the old desk).
            int n = 0;
            foreach (Vector3 at in new[] { new Vector3(21.6f, 0f, -12.4f), new Vector3(18.4f, 0f, -12.4f) })
                r.Prop($"Waiting_Chairs_{n++}", at, new Vector3(1.4f, 0.5f, 0.5f), 0.3f);
            ArtPuzzle(r);
            Lift(r);
            r.Figure("Soldier_A", "soldier_rifle", new Vector3(22f, 0f, -12f), 0f);
            r.Figure("Soldier_B", "soldier_pistol", new Vector3(17f, 0f, -13f), 20f);
            r.Figure("Lab_Pistol_A", "labworker_pistol", new Vector3(24f, 0f, -7f), 300f);
            r.Figure("Lab_Pistol_B", "labworker_pistol", new Vector3(14f, 0f, -7f), 60f);
            r.Figure("Civilian_A", "labworker", new Vector3(20.9f, 0f, -4.6f), 200f, variant: 0); // inside the ring since the foyer
            r.Figure("Civilian_B", "nurse", new Vector3(18.6f, 0f, -11.2f), 30f, variant: 3);
            // The sheeted door: plastic in the doorway until the security office releases sector C.
            GameObject sheet = MenuLoopSetup.EnsureChild(r.Story, "Gate_Sheeting");
            FlagGate gate = MenuLoopSetup.EnsureComponent<FlagGate>(sheet);
            gate.flag = Released;
            gate.showWhenRaised = false;
            r.Box(sheet, "Sheeting", new Vector3(20f, 1.2f, -16.1f), new Vector3(1.8f, 2.4f, 0.1f), DemoRoom.Colour("sheeting", new Color(0.7f, 0.72f, 0.7f)));
            r.Sign("Sign_Sector_C", DemoPlan.Sign("U1, the sheeted door", "SECTOR C. CONSTRUCTION. ACCESS: SECURITY OFFICE."), new Vector3(20f, 2.7f, -15.83f), Vector3.forward, new Vector2(2.6f, 0.35f), 32);
            r.Capsule("Capsule_G5", DrugType.Nalcon, new Vector3(17.3f, 0.15f, -3.2f));
            // The cameras of the foyer (Bruno, 2026-11-14: "at ground level looking up, at least one angle to better emphasize both the
            // statue at the center + the skylight on top"). Two on the floor, on the spokes of G2 and U1, looking up at Modesty with the
            // skylight behind her; one from the east side of the ring on the lift and base I. Each zone lies in front of its camera.
            r.ExtraCamera("low_north", c + new Vector3(0f, 0.3f, 5.2f), c + Vector3.up * 4.2f, 70f, c + new Vector3(0f, 1.5f, 3f), new Vector3(2f, 3f, 2.6f));
            r.ExtraCamera("low_south", c + new Vector3(0f, 0.25f, -5f), c + Vector3.up * 4.2f, 70f, c + new Vector3(0f, 1.5f, -3f), new Vector3(2f, 3f, 2.6f));
            // Its zone: the floor between the camera and base I (the copy on base I hides what lies behind it).
            r.ExtraCamera("lift", c + new Vector3(4.5f, 1.6f, 2f), c + new Vector3(-5.8f, 1.2f, 4.7f), 60f, c + new Vector3(-2.3f, 1.5f, 3f), new Vector3(1.2f, 3f, 2.4f));
        }

        // ── The art puzzle and the lift (rounds 147 and 148; Bruno, 2026-11-14: "build the art puzzle and the lift too") ──

        /// <summary>The bases I to IV around Modesty, clockwise from the north-west.</summary>
        public static readonly Vector3[] PlinthBases =
        {
            new Vector3(16.8f, 0f, -4.8f), new Vector3(23.2f, 0f, -4.8f), new Vector3(23.2f, 0f, -11.2f), new Vector3(16.8f, 0f, -11.2f),
        };

        /// <summary>
        /// The four copies, oldest first (Puritas, Tuccia, Modesty, the Veiled Christ; T13, A6 to A9), each a prop on a wheeled
        /// plinth that mk_orzabal carries, as the planters. They start on the wrong bases. A PlinthPuzzle raises PlinthsFlag when
        /// each stands on its base.
        /// </summary>
        private static void ArtPuzzle(DemoRoom r)
        {
            Material brass = DemoRoom.Colour("brass", new Color(0.72f, 0.6f, 0.32f));
            var bases = new Transform[4];
            string[] numerals = { "I", "II", "III", "IV" };
            for (int i = 0; i < 4; i++)
            {
                GameObject plate = r.Box(r.Props, "Plinth_Base_" + numerals[i], PlinthBases[i] + Vector3.up * 0.015f, new Vector3(0.9f, 0.03f, 0.9f), brass);
                Object.DestroyImmediate(plate.GetComponent<Collider>());
                bases[i] = plate.transform;
                Vector3 toCentre = (FoyerCentre - PlinthBases[i]).normalized;
                r.Sign("Sign_Base_" + numerals[i], numerals[i], PlinthBases[i] - toCentre * 0.62f + Vector3.up * 0.18f, -toCentre, new Vector2(0.3f, 0.16f), 20);
            }
            // The right order, and where each copy starts (out of order).
            var copies = new Transform[4];
            copies[0] = StatueCopy(r, "Copy_Puritas", "A6", PlinthBases[2], CopyShape.Bust).transform;
            copies[1] = StatueCopy(r, "Copy_Tuccia", "A7", PlinthBases[3], CopyShape.Sieve).transform;
            copies[2] = StatueCopy(r, "Copy_Modesty", "A8", PlinthBases[1], CopyShape.Standing).transform;
            copies[3] = StatueCopy(r, "Copy_Veiled_Christ", "A9", PlinthBases[0], CopyShape.Lying).transform;
            GameObject holder = MenuLoopSetup.EnsureChild(r.Story, "Plinth_Puzzle");
            holder.transform.position = FoyerCentre;
            PlinthPuzzle puzzle = MenuLoopSetup.EnsureComponent<PlinthPuzzle>(holder);
            puzzle.flag = PlinthsFlag;
            puzzle.bases = bases;
            puzzle.copies = copies;
            EditorUtility.SetDirty(puzzle);
        }

        private enum CopyShape { Bust, Sieve, Standing, Lying }

        /// <summary>A copy on a wheeled plinth: one prop (1.8 m, its centre at 0.9 m) that mk_orzabal carries, and her art log on it.</summary>
        private static GameObject StatueCopy(DemoRoom r, string name, string logId, Vector3 floorPoint, CopyShape shape)
        {
            GameObject root = MenuLoopSetup.EnsureChild(r.Props, name);
            root.transform.SetPositionAndRotation(floorPoint + Vector3.up * 0.9f, Quaternion.identity);
            BoxCollider box = MenuLoopSetup.EnsureComponent<BoxCollider>(root);
            box.center = Vector3.zero;
            box.size = new Vector3(0.7f, 1.8f, 0.7f);
            Material marble = DemoRoom.Colour("marble", new Color(0.92f, 0.9f, 0.86f)), wood = DemoRoom.Grey(0.22f);
            void Look(PrimitiveType type, string part, Vector3 local, Vector3 scale, Material m, Vector3 euler)
            {
                GameObject g = MenuLoopSetup.EnsureChildPrimitive(root, part, type, local, scale);
                g.transform.rotation = Quaternion.Euler(euler);
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.GetComponent<Renderer>().sharedMaterial = m;
            }
            Look(PrimitiveType.Cube, "Plinth", new Vector3(0f, -0.6f, 0f), new Vector3(0.7f, 0.6f, 0.7f), wood, Vector3.zero);
            switch (shape)
            {
                case CopyShape.Bust: // Puritas: a veiled bust
                    Look(PrimitiveType.Capsule, "Shoulders", new Vector3(0f, -0.05f, 0f), new Vector3(0.55f, 0.25f, 0.35f), marble, Vector3.zero);
                    Look(PrimitiveType.Sphere, "Head", new Vector3(0f, 0.35f, 0f), new Vector3(0.3f, 0.38f, 0.32f), marble, Vector3.zero);
                    break;
                case CopyShape.Sieve: // Tuccia: standing, the sieve held up
                    Look(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.2f, 0f), new Vector3(0.36f, 0.5f, 0.3f), marble, Vector3.zero);
                    Look(PrimitiveType.Sphere, "Head", new Vector3(0f, 0.78f, 0f), new Vector3(0.18f, 0.22f, 0.19f), marble, Vector3.zero);
                    Look(PrimitiveType.Cylinder, "Sieve", new Vector3(0.22f, 0.5f, 0.1f), new Vector3(0.26f, 0.02f, 0.26f), marble, new Vector3(70f, 0f, 0f));
                    break;
                case CopyShape.Standing: // Modesty: standing, the veil to the feet
                    Look(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.2f, 0f), new Vector3(0.38f, 0.52f, 0.32f), marble, Vector3.zero);
                    Look(PrimitiveType.Sphere, "Head", new Vector3(0f, 0.8f, 0f), new Vector3(0.19f, 0.23f, 0.2f), marble, Vector3.zero);
                    break;
                case CopyShape.Lying: // the Veiled Christ: lying on a bier, the head on a cushion
                    Look(PrimitiveType.Capsule, "Body", new Vector3(0f, -0.12f, 0f), new Vector3(0.3f, 0.32f, 0.25f), marble, new Vector3(0f, 0f, 90f));
                    Look(PrimitiveType.Cube, "Cushion", new Vector3(-0.28f, -0.2f, 0f), new Vector3(0.16f, 0.08f, 0.26f), marble, Vector3.zero);
                    break;
            }
            var target = MenuLoopSetup.EnsureComponent<GlorifiedBody.Combat.LockOnTargetable>(root);
            var so = new SerializedObject(target);
            so.FindProperty("fallbackHeight").floatValue = 0f; // the lock point is the centre
            so.FindProperty("isProp").boolValue = true;
            so.FindProperty("heavy").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            Rigidbody body = MenuLoopSetup.EnsureComponent<Rigidbody>(root);
            body.mass = 60f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ; // on its wheels, upright
            UnityEngine.AI.NavMeshObstacle obstacle = MenuLoopSetup.EnsureComponent<UnityEngine.AI.NavMeshObstacle>(root);
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.size = box.size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            MenuLoopSetup.EnsureComponent<MovableProp>(root);
            ArtLog art = DemoArt.Place(r, "Art_" + name, logId, root.transform.position + Vector3.up * 0.2f, "the copy");
            if (art != null) art.transform.SetParent(root.transform, true);
            return root;
        }

        /// <summary>
        /// The lift on the north-west arc of the ring: a gate that stays shut until the plinths are in order (FlagGate), the car
        /// behind it with the reward (two capsules and a delivery note, PLACEHOLDER words), and the ride up to the landing in W2
        /// (LiftRide; DemoWard builds the landing).
        /// </summary>
        private static void Lift(DemoRoom r)
        {
            Vector3 c = FoyerCentre;
            Vector3 dir = LiftCar - c;
            dir.y = 0f;
            dir.Normalize();
            Vector3 mouth = c + dir * FoyerRadius;
            Material chrome = DemoRoom.Colour("chrome", new Color(0.78f, 0.8f, 0.84f));
            GameObject gateObj = MenuLoopSetup.EnsureChild(r.Story, "Gate_Lift");
            FlagGate gate = MenuLoopSetup.EnsureComponent<FlagGate>(gateObj);
            gate.flag = PlinthsFlag;
            gate.showWhenRaised = false;
            GameObject leaf = r.Box(gateObj, "Lift_Door", mouth + Vector3.up * DemoRoom.DoorHeight * 0.5f, new Vector3(1.8f, DemoRoom.DoorHeight, 0.12f), chrome);
            leaf.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
            DemoRoom.Carve(leaf);
            r.Sign("Sign_Lift", "LIFT", mouth - dir * 0.08f + Vector3.up * (DemoRoom.DoorHeight + 0.35f), -dir, new Vector2(0.8f, 0.24f), 30);
            // The car: the arrival point (facing the foyer), the reward, the button.
            GameObject arrive = MenuLoopSetup.EnsureChild(r.Story, LiftCarName);
            arrive.transform.SetPositionAndRotation(LiftCar + Vector3.up * 0.05f, Quaternion.LookRotation(-dir, Vector3.up));
            r.Capsule("Capsule_Lift_Edgar", DrugType.NalconMind, LiftCar + new Vector3(-0.4f, 0.15f, 0.3f));
            r.Capsule("Capsule_Lift_Warspite", DrugType.Nalcon, LiftCar + new Vector3(-0.1f, 0.15f, 0.5f));
            r.Item("Lift_Delivery_Note", DemoItems.Log("l38_the_copies", "A DELIVERY NOTE",
                "PLACEHOLDER (the writer): the delivery note of the four copies for the foyer, signed for at the front desk."),
                LiftCar + new Vector3(0.35f, 0.9f, 0.45f), new Vector3(0.2f, 0.02f, 0.28f));
            GameObject button = MenuLoopSetup.EnsureChild(r.Props, "Lift_Button_G5");
            button.transform.position = LiftCar + dir * 0.6f + Vector3.up * 1.2f;
            LiftRide ride = MenuLoopSetup.EnsureComponent<LiftRide>(button);
            ride.destination = LiftLandingName;
            ride.requiredFlag = PlinthsFlag;
            ride.thing = "the lift up";
            ride.range = 1.6f;
            EditorUtility.SetDirty(ride);
        }

        /// <summary>A spoke of the foyer within this many degrees (0 is north, the angle turns east).</summary>
        private static bool SpokeNear(float deg, float within)
        {
            foreach (Vector3 s in FoyerSpokes)
            {
                Vector3 d = s - FoyerCentre;
                float a = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                if (Mathf.Abs(Mathf.DeltaAngle(a, deg)) < within) return true;
            }
            return false;
        }

        /// <summary>A point of G5 outside the ring and away from every spoke corridor (the corners are solid).</summary>
        public static bool FoyerSolid(Vector3 p)
        {
            Vector3 d = p - FoyerCentre;
            d.y = 0f;
            if (d.magnitude <= FoyerRadius + 0.1f) return false;
            foreach (Vector3 s in FoyerSpokes)
            {
                Vector3 a = s, b = FoyerCentre;
                Vector3 ab = b - a, ap = p - a;
                ab.y = ap.y = 0f;
                float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude);
                Vector3 nearest = a + ab * t;
                nearest.y = p.y;
                if (Vector3.Distance(nearest, p) < FoyerCorridor + 0.25f) return false;
            }
            return true;
        }

        // ── G13b, the main entrance (2026-11-14) ─────────────────────────────

        /// <summary>
        /// G13b: the main entrance of the Memorial, off the admissions office. A passage 8 m long under a triangular roof of glass
        /// blocks on concrete A-frames (Bruno's pictures of 2026-11-14), the steel shutters down at its west end (the lockdown of code
        /// silver), daylight through the slots. Its camera looks down the passage from the admissions end, a trolley in the
        /// foreground. It never opens in the demo.
        /// </summary>
        public static void G13b(DemoRoom r)
        {
            float x0 = r.Spec.X0, x1 = r.Spec.X1, z0 = r.Spec.Z0, z1 = r.Spec.Z1, mid = (z0 + z1) * 0.5f;
            const float eave = 2.9f, ridge = 4.6f;
            Material glass = DemoRoom.Colour("glass_block", new Color(0.72f, 0.8f, 0.84f)), concrete = DemoRoom.Grey(0.36f), steel = DemoRoom.Grey(0.16f);
            float half = (z1 - z0) * 0.5f, slope = Mathf.Atan2(ridge - eave, half) * Mathf.Rad2Deg, side = Mathf.Sqrt(half * half + (ridge - eave) * (ridge - eave));
            // The glass roof: two slopes from the eaves to the ridge, a grid of mullions on them.
            foreach (float s in new[] { -1f, 1f })
            {
                GameObject slopeObj = r.Box(r.Room, s < 0 ? "Roof_Glass_South" : "Roof_Glass_North", new Vector3((x0 + x1) * 0.5f, (eave + ridge) * 0.5f, mid + s * half * 0.5f), new Vector3(x1 - x0, 0.06f, side), glass);
                slopeObj.transform.rotation = Quaternion.Euler(s * slope, 0f, 0f);
                Object.DestroyImmediate(slopeObj.GetComponent<Collider>());
            }
            // The concrete A-frames every 2 m, and the walls up to the eaves in glass block.
            for (float x = x0 + 1f; x < x1; x += 2f)
                foreach (float s in new[] { -1f, 1f })
                {
                    GameObject leg = r.Box(r.Props, $"Frame_{x:0.0}_{(s < 0 ? "S" : "N")}", new Vector3(x, (eave + ridge) * 0.5f - 0.05f, mid + s * half * 0.5f), new Vector3(0.22f, 0.22f, side), concrete);
                    leg.transform.rotation = Quaternion.Euler(s * slope, 0f, 0f);
                    Object.DestroyImmediate(leg.GetComponent<Collider>());
                    r.Box(r.Props, $"Frame_Post_{x:0.0}_{(s < 0 ? "S" : "N")}", new Vector3(x, eave * 0.5f, mid + s * (half - 0.16f)), new Vector3(0.22f, eave, 0.22f), concrete);
                }
            foreach (float s in new[] { -1f, 1f })
                Object.DestroyImmediate(r.Box(r.Room, s < 0 ? "Glass_Wall_South" : "Glass_Wall_North", new Vector3((x0 + x1) * 0.5f, eave * 0.5f + 0.4f, mid + s * (half - 0.04f)), new Vector3(x1 - x0 - 0.4f, eave - 0.8f, 0.04f), glass).GetComponent<Collider>());
            // The shutters at the west end, down: steel slats and the daylight in the slots.
            for (int i = 0; i < 12; i++)
                r.Box(r.Props, $"Shutter_Slat_{i:00}", new Vector3(x0 + 0.2f, 0.12f + i * 0.24f, mid), new Vector3(0.1f, 0.2f, z1 - z0 - 0.6f), steel);
            for (int i = 0; i < 11; i++)
                Object.DestroyImmediate(r.Box(r.Room, $"Shutter_Light_{i:00}", new Vector3(x0 + 0.14f, 0.24f + i * 0.24f, mid), new Vector3(0.02f, 0.04f, z1 - z0 - 0.7f), DemoRoom.Colour("daylight", new Color(1f, 0.98f, 0.9f))).GetComponent<Collider>());
            r.Sign("Sign_Lockdown", "LOCKDOWN. SHELTER IN PLACE.", new Vector3(x0 + 0.3f, 3.2f, mid), Vector3.right, new Vector2(2.2f, 0.36f), 36);
            // The trolley in the foreground of the camera, and the camera down the passage.
            r.Prop("Trolley", new Vector3(x1 - 1.4f, 0f, z0 + 0.6f), new Vector3(1.2f, 0.9f, 0.6f), 0.4f);
            // Its zone: the passage from 2.6 m in front of the camera (where the frame reaches the floor) to the shutters.
            float near = x1 - 3.1f;
            // Low, near the floor (Bruno, 2026-11-14: "lower closer to the ground"): the glass roof fills the top of the frame.
            r.ExtraCamera("axis", new Vector3(x1 - 0.5f, 0.4f, mid), new Vector3(x0, 1.9f, mid), 64f, new Vector3((x0 + near) * 0.5f, 1.5f, mid), new Vector3(near - x0, 3f, z1 - z0));
        }

        /// <summary>G6: the public bathrooms, a clerk who hides in a stall ("Occupied."). Since v2 at x 28..36, z -9..-5, east of the hall. A toilet that flushes (the hider answers it once), a faucet.</summary>
        private static void G6(DemoRoom r)
        {
            for (int i = 0; i < 4; i++) r.Prop($"Stall_Wall_{i}", new Vector3(29.5f + i * 1.5f, 0f, -5.95f), new Vector3(0.06f, 2f, 1.9f), 0.35f);
            DemoV2.Toilet(r, "Toilet", new Vector3(30.25f, 0f, -5.45f), Vector3.back, answer: "Occupied.");
            DemoV2.Toilet(r, "Toilet_B", new Vector3(33.25f, 0f, -5.45f), Vector3.back, works: false);
            EnemySpawn clerk = r.Figure("Hider_Clerk", "labworker", new Vector3(31.75f, 0f, -5.6f), 0f, variant: 0);
            clerk.hides = true;
            clerk.hideLine = BarkBookSetup.Scripted("clerk", "G6, in a toilet stall, Lucy at the door", 0, "Occupied.");
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
                staff.hideLine = "Please. Please. I'm sheltering, I'm sheltering in place."; // not in barks.csv since round 135
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
            // The IT note on the clerk's monitor (DEMO_PLAN §10.1, a source of the order of the dark room's units).
            r.Box(r.Props, "Clerk_Monitor", new Vector3(8.5f, 0.94f, -18.75f), new Vector3(0.5f, 0.38f, 0.08f), DemoRoom.Grey(0.12f));
            r.Sign("IT_Note", "AFTER AN OUTAGE: NET. THEN REC. THEN ACCESS. NEVER ACCESS FIRST. - T.", new Vector3(8.5f, 1.2f, -18.69f), Vector3.forward, new Vector2(0.5f, 0.2f), 18);
        }

        /// <summary>G9: the security office: the monitors, the chair and its scan C7, the logbook L5, the release (beat 23).</summary>
        private static void G9(DemoRoom r)
        {
            r.Prop("Desk", new Vector3(31f, 0f, -14.8f), new Vector3(3.4f, 0.75f, 0.8f), 0.2f); // 3.4 m since v2: 1.3 m to the east wall and the door to G9b
            GameObject wall = r.CrtWall("Screen_Monitors", new Vector3(31f, 0.8f, -15.75f), 5, 2, new List<TapeLine>
            {
                new TapeLine { at = 0.5f, speaker = "MONITOR", text = "PLACEHOLDER: her face on every monitor." },
            }, faceZ: 0.24f);
            // The dark room (DEMO_PLAN §10.1, 2026-11-09): when the lights go out the monitors are the only light. Each face
            // keeps its own colour in the dark (Emissive), and five lights on the dark layer light the room in front of them.
            foreach (Renderer face in wall.GetComponentsInChildren<Renderer>(true))
                if (face.name.StartsWith("CrtFace_")) { face.gameObject.isStatic = false; face.gameObject.AddComponent<GlorifiedBody.Rendering.Emissive>(); }
            for (int i = 0; i < 5; i++)
            {
                var lightObj = new GameObject("Monitor_Light_" + i);
                lightObj.transform.SetParent(r.Props.transform, false);
                lightObj.transform.position = new Vector3(29.9f + i * 0.55f, 1.3f, -14.9f);
                Light l = lightObj.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 5f;
                l.intensity = 1.2f;
                l.color = new Color(0.55f, 0.85f, 0.7f);
                l.shadows = LightShadows.None;
                l.renderingLayerMask = (int)GlorifiedBody.Rendering.Darkness.DarkLayer; // they light only the dark
            }
            GameObject chair = r.Prop("Security_Chair", new Vector3(31f, 0f, -13.4f), new Vector3(0.6f, 1f, 0.6f), 0.15f);
            r.Scan("Scan_Security_Chair", chair.transform.position + Vector3.up * 0.4f, "demo_c7_chair", DemoPlan.ScanCard("C7", "PLACEHOLDER (C7): a man's hands on a row of switches. A wedding ring. A cold coffee."));
            r.Item("L5_Logbook", DemoItems.L5(), new Vector3(29.6f, 0.78f, -14.8f), new Vector3(0.24f, 0.03f, 0.32f));
            DemoNight.G9Roster(r); // L29
            FlagSwitch release = r.Switch("Release_Switch", new Vector3(33.6f, 1.2f, -12.5f), Released, "Release the sector C door?", "release the sector C door",
                "Somewhere in the hall, the sheeting on the sector C door drops.");
            release.requiredFlag = DemoDark.UnitsUp; // the ACCESS console is dead until the units run (§10.1)
            release.waitingText = "WAITING FOR NET.";
            DemoDining.Release(r, release); // and the duty key (beat 23, round 60)
            UnityEditor.EditorUtility.SetDirty(release);
            SaveAssets.SavePoint("Save_G9", new Vector3(33.4f, 0f, -10f), 270f).transform.SetParent(r.Props.transform, true);
            r.Sign("Sign_Comms", DemoPlan.Sign("G9, the door to G9b", "COMMS. AUTHORIZED PERSONNEL."), new Vector3(33.83f, 2f, -13f), Vector3.left, new Vector2(1.6f, 0.3f), 36); // the door to G9b (v2)
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
