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
    /// Floor 2 of the demo, the ward (the demo plan §3.1): W1, W1B and W2 to W12, in greybox. The plan coordinates (metres, x east,
    /// z north) put the rooms side by side as the map shows them. The way: W1, W1B, W2, W5, W7, W8, W9, W11, back to W9, W12,
    /// down. Side rooms: W3, W4, W6, W10, and since 2026-11-05 W13 to W17 (DemoV2, the floorplan v2). The beats of §4 (1 to 16) run as sequences; the cutscenes play as subtitles
    /// from the writers' scripts. Batch: tools/unity.sh exec GlorifiedBody.Editor.Demo.DemoWard.BuildAll
    /// </summary>
    public static class DemoWard
    {
        public const string Music = "ward";
        public const string CodeSilver = "code_silver";

        public static List<RoomSpec> Rooms()
        {
            var rooms = new List<RoomSpec>
            {
                Room("W1", "RECOVERY ROOM", 0, 19, 8, 26, W1, new Door('S', 4, "W1B")),
                Room("W1B", "TREATMENT ROOM", 1, 14, 7, 19, W1B, new Door('N', 4, "W1"), new Door('S', 4, "W2", fire: true, jammed: true, leverFlag: ScientistDone)),
                Room("W2", "NORTH CORRIDOR", 0, 11, 24, 14, W2, new Door('N', 4, "W1B", fire: true), new Door('N', 13, "W3"), new Door('N', 22, "W4"), new Door('S', 1.5f, "W5", fire: true, pocket: -1),
                    new Door('W', 12.75f, "W14"), new Door('E', 12.75f, "W15")),
                Room("W3", "PATIENT ROOMS", 9, 14, 17, 19, W3, new Door('S', 13, "W2")),
                Room("W4", "SHOWER ROOM", 19, 14, 25, 20, W4, new Door('S', 22, "W2")),
                Room("W5", "SOUTH CORRIDOR", 0, -9, 3, 11, W5, new Door('N', 1.5f, "W2", fire: true), new Door('W', 5, "W6"), new Door('E', 1.5f, "W7", fire: true), new Door('E', -6, "W9", fire: true, jamFlag: CodeSilver, leverRoom: "W9")),
                Room("W6", "STAFF LOCKERS", -7, 2, 0, 8, W6, new Door('E', 5, "W5")),
                Room("W7", "THERAPY GYM", 3, -2, 11, 5, W7, new Door('W', 1.5f, "W5", fire: true), new Door('E', 1.5f, "W8")),
                Room("W8", "INDUCTION SUITE", 11, -2, 17, 5, W8, new Door('W', 1.5f, "W7"), new Door('S', 14, "W9")),
                Room("W9", "NURSES' STATION", 3, -10, 17, -2, W9, new Door('N', 14, "W8"), new Door('W', -6, "W5", fire: true, jamFlag: CodeSilver, leverRoom: "W9"), new Door('E', -7, "W10"), new Door('S', 8, "W11"), new Door('S', 15, "W12"),
                    new Door('E', -3, "", width: DemoNight.YardWindowWidth)), // the window on the yard (beat 14, 2026-11-10): an opening, the glass in DemoNight.Yard
                Room("W10", "RESEARCH OFFICE", 17, -10, 23, -4, W10, new Door('W', -7, "W9"), new Door('S', 20, "W13")),
                Room("W11", "MAPPING LAB", 3, -18, 13, -10, W11, new Door('N', 8, "W9"), new Door('S', 5.25f, "W16"), new Door('S', 11.25f, "W17")),
                Room("W12", "STAIRWELL", 13, -14, 17, -10, W12, new Door('N', 15, "W9"), new Door('S', 15, "S1"), new Door('E', -12, "W13")),
            };
            rooms.AddRange(DemoV2.WardRooms()); // the floorplan v2 (2026-11-05): W13 to W17
            return rooms;
        }

        private static RoomSpec Room(string id, string title, float x0, float z0, float x1, float z1, System.Action<DemoRoom> dress, params Door[] doors)
        {
            var spec = new RoomSpec { Id = id, Title = title, Floor = DemoItems.Floor2, Music = Music, X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Dress = dress };
            spec.Doors.AddRange(doors);
            return spec;
        }

        /// <summary>The ward is a part of the world scene: this builds the whole world (DemoWorld.BuildAll).</summary>
        public static void BuildAll() => DemoWorld.BuildAll();

        // ── The rooms ─────────────────────────────────────────────────

        private const string ScientistDone = "demo_w1b_scientist";
        private const string CapsulesFall = "demo_w1b_capsules";
        private const string ChartDone = "demo_w2_chart";

        /// <summary>
        /// The feed opens by a cutscene (Bruno, 2026-11-05: "the unlocking oif the eating/feeding should be a cutscene; i missed
        /// the log note so i had to do the entire demo without healing"). A skip still opens it. The chart goes to her items.
        /// </summary>
        private static List<ScriptStep> Chart() => new List<ScriptStep>
        {
            ScriptStep.LockHer(),
            ScriptStep.Play(DemoScripts.Fmv("cutscenes/02b_the_chart.md", "02b_the_chart", "cs02b")),
            ScriptStep.Give(DemoItems.L9().itemId),
            ScriptStep.Raise(Feeding.UnlockFlag),
            ScriptStep.FreeHer(),
        };

        /// <summary>
        /// W1: she wakes (beat 1), Stevens on the speaker and the printout (2). The quiet room: no enemy and no beat until she
        /// leaves it (Bruno, 2026-11-05: "a mini-tutorial/safe space, one where the player has a chance to situate themsevles
        /// and acclimatize to the controls before moving on towards their firswt combat").
        /// </summary>
        private static void W1(DemoRoom r)
        {
            r.Prop("Bed", new Vector3(5.6f, 0f, 24.6f), new Vector3(2.1f, 0.6f, 1f), 0.55f);
            r.Prop("Strap_Burned", new Vector3(5.6f, 0.6f, 24.6f), new Vector3(0.1f, 0.05f, 1.02f), 0.05f);
            r.Prop("Drip_Stand", new Vector3(7f, 0f, 25.4f), new Vector3(0.1f, 1.8f, 0.1f), 0.7f);
            r.Prop("Cabinet", new Vector3(1f, 0f, 25.4f), new Vector3(0.8f, 1.1f, 0.5f), 0.25f);
            MenuLoopSetup.EnsurePickup("Capsule_W1", DrugType.Nalcon, new Vector3(1f, 1.3f, 25.2f)); // the capsule of the power she starts with
            r.Prop("Screen_By_Door", new Vector3(2.2f, 1.2f, 19.2f), new Vector3(0.8f, 0.5f, 0.06f), 0.6f);
            r.Prop("Printer", new Vector3(2.2f, 0f, 19.5f), new Vector3(0.5f, 0.9f, 0.4f), 0.35f);
            r.Item("L1_Printout", DemoItems.Plan(), new Vector3(2.2f, 0.95f, 19.5f), new Vector3(0.24f, 0.02f, 0.32f));
            r.PlaceLucy(new Vector3(3.9f, 0f, 24.2f), 180f);
            var wake = new List<ScriptStep>
            {
                ScriptStep.Raise(Feeding.LockFlag),  // round 18: the feed starts locked
                ScriptStep.SetShort(88f),            // round 8: she starts nearly full on the SHORT
                ScriptStep.LockHer(),
            };
            wake.Add(ScriptStep.Play(DemoNight.WithTitle(DemoScripts.Fmv("cutscenes/01_the_wake.md", "01_the_wake", "cs01")))); // FMV (Bruno, 2026-11-02), the card RAVEN first (round 104)
            wake.Add(ScriptStep.FreeHer());
            // The game opens on the cutscene, black from the first frame (Bruno, 2026-11-04).
            r.Beat("W1_Wake", "demo_w1_wake", Vector3.zero, Vector3.zero, wake.ToArray()).startsBlack = true;
        }

        /// <summary>
        /// W1B: the scientist and the collar computer (beat 3, cutscene 02). Bruno, 2026-11-05: "add a second room that the
        /// first scientist will be in, not at the corridor". He works at a bed behind a screen, so no line from W1 reaches him.
        /// A line across the room stops her a step past the doorway, he walks out and talks (the FMV), and he attacks after the
        /// last line. The door to W2 stays shut, with the three behind it. Her lever opens it after the talk.
        /// </summary>
        private static void W1B(DemoRoom r)
        {
            r.Prop("Treatment_Bed", new Vector3(6.5f, 0f, 15.6f), new Vector3(1f, 0.6f, 2f), 0.55f);
            r.Prop("Sleeper_Under_Sheet", new Vector3(6.5f, 0.6f, 15.6f), new Vector3(0.6f, 0.22f, 1.7f), 0.75f); // "a patient who sleeps" (the script)
            r.Prop("Dressing_Cart", new Vector3(6.4f, 0f, 17f), new Vector3(0.7f, 0.9f, 0.4f), 0.45f);
            r.Prop("Privacy_Screen", new Vector3(5.6f, 0f, 17.5f), new Vector3(2.8f, 1.8f, 0.06f), 0.65f);
            GameObject scientist = MenuLoopSetup.EnsureChild(r.Figures, "Group_Scientist");
            EnemySpawn knife = r.Figure("Scientist_Knife", "labworker_knife", new Vector3(5.6f, 0f, 16f), 90f, parent: scientist);
            knife.heldUntilFlag = ScientistDone;
            EditorUtility.SetDirty(knife);
            // The capsules roll off his tray in the talk (cutscene 02, note 1, Bruno: "yes add the capsules"). They show under
            // the cut, so nothing pops up in view, and she can take them after the fight. One for each of the other two powers.
            GameObject tray = MenuLoopSetup.EnsureChild(r.Story, "Gate_Capsules");
            FlagGate fallen = MenuLoopSetup.EnsureComponent<FlagGate>(tray);
            fallen.flag = CapsulesFall;
            fallen.showWhenRaised = true;
            foreach (var capsule in new[] { ("Capsule_W1B_A", DrugType.NalconFire, new Vector3(4.7f, 0.15f, 15.6f)), ("Capsule_W1B_B", DrugType.NalconLift, new Vector3(3.5f, 0.15f, 15.9f)) })
                ItemAssets.Pickup(tray, capsule.Item1, ItemAssets.Drug(capsule.Item2), PrimitiveType.Capsule, capsule.Item3, Vector3.one * 0.3f, Quaternion.Euler(0f, 0f, 90f));
            var meet = new List<ScriptStep>
            {
                ScriptStep.LockHer(),
                ScriptStep.Approach(scientist, new Vector3(4.3f, 0f, 16f)),
                ScriptStep.Wait(1.8f),
                ScriptStep.Raise(CapsulesFall),
                ScriptStep.Play(DemoScripts.Fmv("cutscenes/02_the_scientist.md", "02_the_scientist", "cs02")),
                ScriptStep.Approach(scientist, new Vector3(4.1f, 0f, 16.6f)), // "he steps closer. Louder. Worse."
                ScriptStep.Wait(0.5f),
                ScriptStep.FreeHer(),
                ScriptStep.AlertAlone(scientist), // he calls nobody: the three behind the door stay unaware
            };
            r.Beat("W1B_Scientist", ScientistDone, new Vector3(4f, 1f, 17.9f), new Vector3(6f, 2f, 0.6f), meet.ToArray()); // wall to wall: no way past it
            // The talk has its own camera on the west wall: her, him, both doors. The cell cameras split the room down the line she
            // walks, and one stood in the bay with the screen between it and her.
            r.ExtraCamera("talk", new Vector3(1.35f, 2.75f, 16.5f), new Vector3(4.6f, 1f, 17.5f), 72f, new Vector3(3.9f, 1.5f, 17.35f), new Vector3(2.6f, 3f, 3.3f));
        }

        /// <summary>W2: three armed scientists outside (beat 4), then her care chart: a cutscene opens the feed (5, cutscene 02b).</summary>
        private static void W2(DemoRoom r)
        {
            CardDoor(r, "W3", new Vector3(14.3f, 1.2f, 13.82f), Vector3.back); // the code rotation: W3 opens with the card
            // One pistol and two knives (Bruno, 2026-11-05: "helps to escalate better"), behind the shut door of W1B.
            GameObject three = MenuLoopSetup.EnsureChild(r.Figures, "Group_Three");
            r.Figure("Scientist_A", "labworker_pistol", new Vector3(6.5f, 0f, 12.2f), 300f, parent: three);
            r.Figure("Scientist_B", "labworker_knife", new Vector3(8.5f, 0f, 13f), 280f, parent: three);
            r.Figure("Scientist_C", "labworker_knife", new Vector3(10.5f, 0f, 12.2f), 270f, parent: three);
            // The chart: when the three are dead, or in W5 when she runs past them (W5_Chart). The bodies fall first.
            var chart = new List<ScriptStep> { ScriptStep.WaitDead(three), ScriptStep.Wait(1.5f), ScriptStep.StopIfFlag(ChartDone) };
            chart.AddRange(Chart());
            r.Beat("W2_Chart", ChartDone, Vector3.zero, Vector3.zero, chart.ToArray());
            r.Sign("Sign_Pain", "YOUR PAIN IS SEEN. ASK FOR RELIEF.", new Vector3(18f, 1.9f, 13.83f), Vector3.back, new Vector2(2.6f, 0.5f));
            r.Prop("Bench_A", new Vector3(16f, 0f, 11.4f), new Vector3(1.6f, 0.45f, 0.45f));
            r.Prop("Cart", new Vector3(20f, 0f, 11.5f), new Vector3(0.9f, 0.9f, 0.5f), 0.45f);
            PropAssets.Crate("Extinguisher", new Vector3(23f, 0f, 11.5f), 0f, 0.4f); // a throw for telekinesis
        }

        /// <summary>W3: the patient, "Is mine done?" (beat 6), her chart (L2), the scan C8 on her.</summary>
        private static void W3(DemoRoom r)
        {
            r.Prop("Bed", new Vector3(14.5f, 0f, 17.8f), new Vector3(2.1f, 0.55f, 1f), 0.55f);
            EnemySpawn patient = r.Figure("Patient", "patient", new Vector3(13.8f, 0.55f, 17.8f), 90f);
            patient.line = "Is mine done?";
            patient.scanImage = TutorialSetup.EnsureScanImage();
            patient.scanCaption = "PLACEHOLDER (C8): a hand signs a form. A gloved hand holds the page flat.";
            EditorUtility.SetDirty(patient);
            r.Item("L2_Chart", DemoItems.L2(), new Vector3(16.1f, 0.6f, 17.8f), new Vector3(0.24f, 0.03f, 0.32f));
            r.Prop("Curtain_Rail", new Vector3(12.6f, 2.2f, 17.8f), new Vector3(0.05f, 0.05f, 2.4f), 0.6f);
            r.Capsule("Capsule_W3", DrugType.Nalcon, new Vector3(10f, 0.15f, 15f));
        }

        /// <summary>W4: the long mirror (beat 7), the showers, a nurse who hides in a stall (a hider).</summary>
        private static void W4(DemoRoom r)
        {
            r.Box(r.Props, "Mirror", new Vector3(19.17f, 1.4f, 17f), new Vector3(0.04f, 1f, 3.6f), DemoRoom.Colour("mirror", new Color(0.62f, 0.68f, 0.72f)));
            for (int i = 0; i < 3; i++) r.Prop($"Shower_{i}", new Vector3(24.6f, 0f, 15.2f + i * 1.6f), new Vector3(0.2f, 2.1f, 0.06f), 0.5f);
            // Two stalls in the north-west corner: the nurse hides in the second.
            r.Prop("Stall_Wall_A", new Vector3(20.6f, 0f, 18.9f), new Vector3(0.06f, 2f, 2.2f), 0.35f);
            r.Prop("Stall_Wall_B", new Vector3(22.1f, 0f, 18.9f), new Vector3(0.06f, 2f, 2.2f), 0.35f);
            EnemySpawn nurse = r.Figure("Hider_Nurse", "nurse", new Vector3(21.35f, 0f, 19.4f), 0f, variant: 0);
            nurse.hides = true;
            nurse.hideLine = "It is sown in weakness. It is raised in power. It is sown in weakness.";
            nurse.foundLine = "I'm not here. Please. I'm not here.";
            EditorUtility.SetDirty(nurse);
            r.Prop("Laundry_Cart", new Vector3(23.6f, 0f, 14.8f), new Vector3(0.7f, 0.9f, 0.5f), 0.4f);
            r.Capsule("Capsule_W4", DrugType.NalconFire, new Vector3(21.5f, 0.15f, 14.8f));
            // The fixtures (round 30): a toilet in the stall west of the nurse's, a sink under the long mirror (the Crown, the slit).
            DemoV2.Toilet(r, "Toilet", new Vector3(19.9f, 0f, 19.4f), Vector3.back);
            r.Prop("Sink", new Vector3(19.4f, 0f, 17f), new Vector3(0.5f, 0.85f, 0.6f), 0.55f);
            DemoV2.Faucet(r, "Faucet", new Vector3(19.4f, 0.85f, 17f), Vector3.right);
        }

        /// <summary>W5: two guards (beat 8), code silver and the fire door (9), the scan C5 on a guard.</summary>
        private static void W5(DemoRoom r)
        {
            r.Figure("Guard_A", "guard_pistol", new Vector3(1.6f, 0f, -1.5f), 0f);
            EnemySpawn second = r.Figure("Guard_B", "guard_pistol", new Vector3(1.2f, 0f, -3.5f), 0f);
            second.scanImage = TutorialSetup.EnsureScanImage();
            second.scanCaption = "PLACEHOLDER (C5): \"Don't let her touch you.\"";
            EditorUtility.SetDirty(second);
            r.Prop("Gurney", new Vector3(2.4f, 0f, 7f), new Vector3(0.7f, 0.8f, 1.9f), 0.5f);
            r.Prop("Wall_Phone", new Vector3(0.05f, 1.3f, 8.5f), new Vector3(0.1f, 0.25f, 0.18f), 0.6f);
            r.Sign("Sign_Fire_Door", "FIRE DOOR. KEEP CLEAR.", new Vector3(2.83f, 2.45f, -6f), Vector3.left, new Vector2(1.4f, 0.3f), 40);
            r.Beat("W5_Chart", ChartDone, new Vector3(1.5f, 1f, 10.1f), new Vector3(3f, 2f, 0.6f), Chart().ToArray()); // she ran past the three: the feed still opens
            var silver = new List<ScriptStep> { ScriptStep.Wait(1.5f) };
            silver.AddRange(DemoScripts.Steps("cutscenes/03_code_silver.md", "cs03", 5));
            silver.Add(ScriptStep.Raise(CodeSilver)); // the fire door to W9 shuts: the way on is through the gym
            r.Beat("W5_Code_Silver", "demo_w5_code_silver", new Vector3(1.5f, 1f, 7.5f), new Vector3(3f, 2f, 2f), silver.ToArray());
        }

        /// <summary>W6: the lockers and the guard uniform, the card L3, a save machine (beat 10), the poster.</summary>
        private static void W6(DemoRoom r)
        {
            r.Locker("Locker_Uniform", new Vector3(-4f, 0f, 7.7f));
            for (int i = 0; i < 4; i++)
                if (i != 1) r.Prop($"Locker_{i}", new Vector3(-6f + i * 1f, 0f, 7.7f), new Vector3(0.9f, 1.9f, 0.55f), 0.08f);
            r.Item("L3_Card", DemoItems.L3(), new Vector3(-2.2f, 1.2f, 7.4f), new Vector3(0.2f, 0.28f, 0.02f));
            SaveAssets.SavePoint("Save_W6", new Vector3(-6.4f, 0f, 3f), 90f).transform.SetParent(r.Props.transform, true);
            r.Sign("Sign_Hard_Days", "THANK YOU FOR THE HARD DAYS.", new Vector3(-3.5f, 2.35f, 7.83f), Vector3.back, new Vector2(2.4f, 0.4f));
            r.Prop("Bench", new Vector3(-3.5f, 0f, 5f), new Vector3(2f, 0.45f, 0.4f));
            r.Capsule("Capsule_W6", DrugType.NalconLift, new Vector3(-1f, 0.15f, 3f));
            r.Prop("Kettle", new Vector3(-6.6f, 0.9f, 5.2f), new Vector3(0.2f, 0.25f, 0.2f), 0.6f);
            r.Prop("Kettle_Shelf", new Vector3(-6.6f, 0f, 5.2f), new Vector3(0.6f, 0.9f, 0.5f), 0.2f);
            // The fixtures (round 30): a toilet in a stall in the south-east corner, a sink by it.
            r.Prop("Stall_Wall", new Vector3(-1.7f, 0f, 2.95f), new Vector3(0.06f, 2f, 1.9f), 0.35f);
            DemoV2.Toilet(r, "Toilet", new Vector3(-0.9f, 0f, 2.5f), Vector3.forward);
            r.Prop("Sink", new Vector3(-3f, 0f, 2.3f), new Vector3(0.5f, 0.85f, 0.6f), 0.55f);
            DemoV2.Faucet(r, "Faucet", new Vector3(-3f, 0.85f, 2.3f), Vector3.forward);
        }

        /// <summary>W7: the CRT wall plays the rehearsal tape when she walks in (beat 11), the dummy and its scan C1.</summary>
        private static void W7(DemoRoom r)
        {
            var lines = new List<TapeLine>();
            float at = 1f;
            foreach ((string speaker, string text) in DemoScripts.Lines("cutscenes/04_the_tape.md"))
            {
                lines.Add(new TapeLine { at = at, speaker = speaker, text = text });
                at += Mathf.Max(3f, Subtitles.SubtitleService.ReadSeconds(text) + 0.8f);
            }
            GameObject wall = r.CrtWall("Screen_Crt_Wall", new Vector3(6.5f, 0f, 4.7f), 6, 4, lines);
            r.Beat("W7_Tape", "demo_w7_tape", new Vector3(7f, 1f, 1.5f), new Vector3(6f, 2f, 5f), ScriptStep.Screen(wall, wait: true));
            r.Prop("Bars_Rail_A", new Vector3(7f, 0.9f, -0.6f), new Vector3(3.2f, 0.05f, 0.05f), 0.7f);
            r.Prop("Bars_Rail_B", new Vector3(7f, 0.9f, 0.2f), new Vector3(3.2f, 0.05f, 0.05f), 0.7f);
            r.Prop("Dummy_Rack", new Vector3(10.4f, 0f, 3.8f), new Vector3(0.5f, 1.8f, 1.6f), 0.35f);
            GameObject dummy = r.Prop("Padded_Dummy", new Vector3(9.5f, 0f, -1.2f), new Vector3(0.5f, 1.7f, 0.4f), 0.55f);
            r.Scan("Scan_Dummy", dummy.transform.position + Vector3.up * 1.2f, "demo_c1_dummy", "PLACEHOLDER (C1): the same dummy on a rack in a store room, with a price tag.");
            r.Prop("Camera_Tripod", new Vector3(3.6f, 0f, -1.5f), new Vector3(0.3f, 1.5f, 0.3f), 0.3f);
        }

        /// <summary>W8: the induction machine, the sign, a technician behind the chair (a hider), the scan C9 (beat 12).</summary>
        private static void W8(DemoRoom r)
        {
            GameObject chair = r.Box(r.Props, "Induction_Chair", new Vector3(14f, 0.55f, 2.2f), new Vector3(0.8f, 0.4f, 2f), DemoRoom.Grey(0.6f));
            chair.transform.rotation = Quaternion.Euler(-12f, 0f, 0f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                r.Prop($"Machine_Arm_{i}", new Vector3(14f + Mathf.Cos(a) * 1.1f, 0f, 2.2f + Mathf.Sin(a) * 1.1f), new Vector3(0.08f, 2.4f, 0.08f), 0.65f);
            }
            r.Prop("Machine_Ring", new Vector3(14f, 2.35f, 2.2f), new Vector3(2.4f, 0.08f, 2.4f), 0.6f);
            EnemySpawn tech = r.Figure("Hider_Technician", "labworker", new Vector3(15.3f, 0f, 3.8f), 45f, variant: 0);
            tech.hides = true;
            tech.hideLine = "It's a pain protocol. It's only for the pain. I only ran the pain.";
            tech.foundLine = "Please. I only ran the pain.";
            EditorUtility.SetDirty(tech);
            r.Prop("Monitor_Cart", new Vector3(12f, 0f, 3.8f), new Vector3(0.6f, 1.2f, 0.5f), 0.35f);
            r.Sign("Sign_Induction", "INDUCTION SUITE 2. PAIN MANAGEMENT PROTOCOL IN EFFECT.\nTHE PATIENT MAY NOT REMEMBER THE SESSION. THIS IS NORMAL.", new Vector3(14f, 2.3f, 4.83f), Vector3.back, new Vector2(3.6f, 0.6f), 32);
            r.Scan("Scan_Chair", new Vector3(14f, 1f, 2.2f), "demo_c9_chair", "PLACEHOLDER (C9): \"You're doing so well.\"");
        }

        /// <summary>W9: Marta (beat 13, her scan C2, her fate), the window (14), the shut fire door.</summary>
        private static void W9(DemoRoom r)
        {
            CardDoor(r, "W10", new Vector3(16.82f, 1.2f, -5.7f), Vector3.left); // the code rotation: W10 opens with the card
            r.Prop("Counter", new Vector3(11f, 0f, -6f), new Vector3(3f, 1.05f, 1.6f), 0.1f);
            DemoDark.HubUnits(r); // a source of the order of the dark room (§10.1)
            r.Item("L19_Dictaphone", DemoItems.L19(), new Vector3(10.2f, 1.08f, -5.7f), new Vector3(0.08f, 0.04f, 0.16f)); // the incident review (round 44)
            r.Prop("Counter_Monitors", new Vector3(11f, 1.05f, -6f), new Vector3(1.2f, 0.4f, 0.2f), 0.35f);
            DemoNight.W9Email(r); // L24
            DemoNight.Yard(r); // the window on the yard and the two vans (beat 14; Bruno, 2026-11-10: "add placeholder vans / background")
            EnemySpawn marta = r.Figure("Marta", "nurse", new Vector3(4f, 0f, -8.6f), 45f, variant: 1);
            marta.fate = "marta";
            marta.scanImage = TutorialSetup.EnsureScanImage();
            marta.scanCaption = "PLACEHOLDER (C2): small hands on the armrests; gloved hands hold the wrists down. \"Again.\"";
            EditorUtility.SetDirty(marta);
            var talk = new List<ScriptStep>();
            talk.AddRange(DemoScripts.Steps("cutscenes/05_marta.md", "cs05"));
            r.Beat("W9_Marta", "demo_w9_marta", new Vector3(6f, 1f, -7.5f), new Vector3(4f, 2f, 4f), talk.ToArray());
        }

        /// <summary>W10: the research office, the session note L7 (side room).</summary>
        private static void W10(DemoRoom r)
        {
            r.Prop("Desk_A", new Vector3(19f, 0f, -5.2f), new Vector3(1.4f, 0.75f, 0.7f), 0.25f);
            r.Prop("Desk_B", new Vector3(21.5f, 0f, -5.2f), new Vector3(1.4f, 0.75f, 0.7f), 0.25f);
            r.Prop("Filing", new Vector3(22.6f, 0f, -9.4f), new Vector3(0.6f, 1.3f, 0.5f), 0.2f);
            r.Box(r.Props, "Pinboard", new Vector3(20f, 1.6f, -4.17f), new Vector3(1.6f, 0.9f, 0.04f), DemoRoom.Colour("pinboard", new Color(0.4f, 0.32f, 0.22f)));
            r.Item("L7_Session_Note", DemoItems.L7(), new Vector3(19f, 0.78f, -5.2f), new Vector3(0.22f, 0.02f, 0.3f));
        }

        /// <summary>W11: the ward sister with the card, two armed lab workers, the small chairs, the whiteboard L4, the scan C4 (beat 16).</summary>
        private static void W11(DemoRoom r)
        {
            // Two groups of three, 1.8 m from the south wall (v2: the NavMesh agent, 0.5 m, reaches the doors to W16 and W17 behind them).
            for (int i = 0; i < 6; i++)
                r.Prop($"Small_Chair_{i}", new Vector3(4.5f + i * 1.5f + (i >= 3 ? 0.6f : 0f), 0f, -16.2f), new Vector3(0.6f, 0.7f, 0.9f), 0.5f);
            r.Box(r.Props, "Whiteboard", new Vector3(3.17f, 1.5f, -13.5f), new Vector3(0.04f, 1f, 2.2f), DemoRoom.Grey(0.85f)); // 0.17 off the line: the wall is 0.3 m thick on it (2026-11-05)
            r.Item("L4_Whiteboard", DemoItems.L4(), new Vector3(3.22f, 1.1f, -13.5f), new Vector3(0.05f, 0.2f, 0.3f));
            r.Prop("Bench", new Vector3(11.4f, 0f, -10.65f), new Vector3(2f, 0.9f, 0.6f), 0.3f); // the mapping bench (L20)
            r.Item("L20_Recorder", DemoItems.L20(), new Vector3(11.4f, 0.93f, -10.65f), new Vector3(0.14f, 0.05f, 0.1f)); // the calibration note (round 49)
            r.Scan("Scan_Small_Chair", new Vector3(6f, 0.8f, -16.2f), "demo_c4_chair", "PLACEHOLDER (C4): \"Imagine your hand.\"");
            // The ward sister carries the stairwell card (ItemCarrier): a hidden pickup she drops at her death.
            ItemPickup card = r.Item("Stair_Card", DemoItems.StairCard(), new Vector3(11f, 0.05f, -13f), new Vector3(0.1f, 0.02f, 0.15f));
            card.gameObject.SetActive(false);
            EnemySpawn sister = r.Figure("Ward_Sister", "nurse", new Vector3(11.5f, 0f, -12.5f), 200f, variant: 2);
            sister.drop = card;
            EditorUtility.SetDirty(sister);
            r.Figure("Lab_Pistol_A", "labworker_pistol", new Vector3(7f, 0f, -12f), 180f);
            r.Figure("Lab_Pistol_B", "labworker_pistol", new Vector3(10f, 0f, -14.5f), 160f);
            var lab = new List<ScriptStep>();
            lab.AddRange(DemoScripts.Steps("cutscenes/06_the_lab.md", "cs06"));
            r.Beat("W11_Lab", "demo_w11_lab", new Vector3(8f, 1f, -11f), new Vector3(4f, 2f, 1.6f), lab.ToArray());
        }

        public const string CodesRotated = "demo_codes_rotated", CardEnrolled = "demo_card_enrolled";

        /// <summary>A door that the ward sister's card opens, with its reader on this side (the code rotation).</summary>
        public static LockedDoor CardDoor(DemoRoom r, string to, Vector3 readerAt, Vector3 facing)
        {
            GameObject door = r.LockedLeaf(r.DoorTo(to), DemoItems.StairCard().itemId, "the ward sister's card");
            return r.CodeLock(door, readerAt, facing, CodesRotated, CardEnrolled);
        }

        /// <summary>
        /// W12: the stairwell door, locked by the card (beat 15); the scan C3 shows the card. The code rotation (DEMO_PLAN
        /// §10.2; Bruno, 2026-11-08: a backtracker, the code 8675309, the doors W3 and W10): the card opens W3 and W10, and
        /// after two doors the PA call 1B rotates the codes. A try here before that rotates them at once. Then the reader
        /// refuses the card until the terminal of W13 enrols it with the code on the wall of W14.
        /// </summary>
        private static void W12(DemoRoom r)
        {
            GameObject door = r.LockedLeaf(r.DoorTo("S1"), DemoItems.StairCard().itemId, "the ward sister's card");
            r.CodeLock(door, new Vector3(16.3f, 1.2f, -13.82f), Vector3.forward, CodesRotated, CardEnrolled).rotatesOnTry = true;
            var rotation = MenuLoopSetup.EnsureChild(r.Story, "Code_Rotation").AddComponent<CodeRotation>();
            rotation.cardItemId = DemoItems.StairCard().itemId;
            rotation.rotatedFlag = CodesRotated;
            rotation.opensBeforeRotation = 2;
            rotation.paLine = "Site codes rotate at this time. Re-enrol your card at a security station."; // the writer's call 1B (T5_pa.md)
            EditorUtility.SetDirty(rotation);
            r.Scan("Scan_Stair_Door", new Vector3(15f, 1.2f, -13.7f), "demo_c3_door", "PLACEHOLDER (C3): a white card on a lanyard, against red scrubs.");
            r.Prop("Stairs_Down", new Vector3(13.8f, 0f, -11f), new Vector3(1.2f, 0.4f, 1.8f), 0.3f);
            r.Prop("Handrail", new Vector3(13.1f, 0.9f, -12f), new Vector3(0.05f, 0.05f, 3f), 0.7f); // the west wall: the east wall opens to W13 (v2)
        }
    }
}
#endif
