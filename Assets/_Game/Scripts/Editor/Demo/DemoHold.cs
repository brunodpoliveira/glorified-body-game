#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Story;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>
    /// The Hold, map v3 (DEMO_PLAN §5.0, §6 beats 1 to 5e, §10.6; rounds 118, 119 and 123): a clear box for one patient
    /// that hangs in a black hall, the start of the demo. It replaces W1 and W1B. Nine rooms of play and the tube:
    ///   H1 the cell (the upright restraint), H2 treatment (two scientists, L35), H3 the guard post (two guards, L9),
    ///   H4 the Theatre and its booth (the nurse, L36, the fire alarm or the glass), H5 the airlock (the gurney, the
    ///   voice), HT the tube over the gap (the ride, then EXTEND), H6 the landing (EXTEND, the card door, the scan C11),
    ///   H7 the gallery (the arena), H8 the save room, H9 the office of Dr. Colton (the hidden door, L1), out into W2.
    /// The places and sizes follow the writer's game/act1_map_v3.py (PROPOSED), with three changes for play: the
    /// gallery H7 is 6 m deep (2 m in the drawing: too narrow for a fight), so H6, the box and the hall stand 5 m
    /// further north, and the tube is a room of its own (HT) so the cameras see the ride.
    /// Greybox: the clear walls are opaque grey, the hall is black boxes with no collider.
    /// </summary>
    public static class DemoHold
    {
        // Flags (the saves keep them).
        public const string Free = "hold_free";
        public const string Lockdown = "hold_lockdown";
        public const string WakeDone = "demo_h1_wake";
        public const string MeetDone = "demo_h2_scientist";
        public const string CapsulesFall = "demo_h2_capsules";
        public const string GuardsOpen = "hold_h3_open";
        public const string GuardsDone = "demo_h3_guards";
        public const string RunnerCowered = "hold_h2_cowered";
        public const string Alarm = "hold_h4_alarm";
        public const string GlassBroken = "hold_h4_glass_broken";
        public const string TapePlay = "hold_l36_play";
        public const string TapeDone = "demo_h4_tape";
        public const string TubeOut = "hold_tube_out";
        public const string CardScanned = "hold_c11";
        public const string VoiceUrge = "demo_h5_voice_urge";
        public const string VoiceFed = "demo_h5_voice_fed";
        public const string VoiceHeard = "hold_voice";
        public const string OfficeShut = "hold_h9_shut";
        public const string Music = DemoWard.Music;

        public const string BoothSign = "THEATRE CONTROL. AUTHORISED STAFF ONLY. DOOR RELEASES ON FIRE ALARM."; // DEMO_PLAN §7 (the writer, round 125)
        public static readonly string[] Ids = { "H1", "H2", "H3", "H4", "H5", "HT", "H6", "H7", "H8", "H9" };

        // The places (metres, the ward's frame: x east, z north). The writer's drawing, H6 and the box 5 m north.
        public const float BoxZ0 = 33f, BoxZ1 = 38f, DoorZ = 35.5f, TubeZ0 = 34.3f, TubeZ1 = 36.7f, TubeWidth = 2.4f, TubeHeight = 2.9f; // 0.1 m of lintel under the 3 m ceiling: a camera of H6 saw over the doorway into the tube
        public static readonly Vector3 LucyStart = new Vector3(25.05f, 0f, 35.5f);

        public static List<RoomSpec> Rooms() => new List<RoomSpec>
        {
            Spec("H1", "THE CELL", 21, BoxZ0, 27, BoxZ1, H1, new Door('W', DoorZ, "H2")),
            Spec("H2", "TREATMENT", 15, BoxZ0, 21, BoxZ1, H2, new Door('E', DoorZ, "H1"),
                new Door('W', DoorZ, "H3", fire: true, jammed: true, leverRoom: "none", unbreakable: true)),
            Spec("H3", "GUARD POST", 9, BoxZ0, 15, BoxZ1, H3, new Door('E', DoorZ, "H2", fire: true, jammed: true, leverRoom: "none", unbreakable: true),
                new Door('W', DoorZ, "H5"), new Door('N', 12, "H4")),
            Spec("H4", "THE THEATRE", 9, BoxZ1, 15, 43, H4, new Door('S', 12, "H3")),
            Spec("H5", "AIRLOCK", 4, BoxZ0, 9, BoxZ1, H5, new Door('E', DoorZ, "H3"), new Door('W', DoorZ, "HT", width: TubeWidth, height: TubeHeight)),
            Spec("HT", "THE TUBE", -2, TubeZ0, 4, TubeZ1, HT, new Door('E', DoorZ, "H5", width: TubeWidth, height: TubeHeight),
                new Door('W', DoorZ, "H6", width: TubeWidth, height: TubeHeight)),
            Spec("H6", "LANDING", -6, 29, -2, 37, H6, new Door('E', DoorZ, "HT", width: TubeWidth, height: TubeHeight), new Door('S', -4, "H7")),
            Spec("H7", "THE GALLERY", -6, 23, 14, 29, H7, new Door('N', -4, "H6"), new Door('S', 4, "H8", fire: true)),
            Spec("H8", "SAVE ROOM", 1, 19, 7, 23, H8, new Door('N', 4, "H7", fire: true),
                new Door('S', 4, "H9", fire: true, jammed: true, leverRoom: "H8", unbreakable: true, jamFlag: OfficeShut)),
            Spec("H9", "DR. COLTON'S OFFICE", 1, 14, 7, 19, H9, new Door('N', 4, "H8", fire: true, jammed: true, leverRoom: "H8", unbreakable: true, jamFlag: OfficeShut),
                new Door('S', 4, "W2")),
        };

        private static RoomSpec Spec(string id, string title, float x0, float z0, float x1, float z1, System.Action<DemoRoom> dress, params Door[] doors)
        {
            var spec = new RoomSpec { Id = id, Title = title, Floor = DemoItems.Floor2, Music = Music, X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Dress = dress };
            spec.Doors.AddRange(doors);
            return spec;
        }

        private static GameObject NoCollider(GameObject obj)
        {
            foreach (Collider c in obj.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            return obj;
        }

        private static GameObject Visual(DemoRoom r, GameObject parent, string name, Vector3 centre, Vector3 size, Color colour, string id) =>
            NoCollider(r.Box(parent, name, centre, size, DemoRoom.Colour(id, colour)));

        // ── H1, the cell ──────────────────────────────────────────────

        /// <summary>
        /// The cell (beat 1, 1b; cutscene 01 version 3): the upright frame, the drain line, the cage, the steel tray with the
        /// morning dose (one WARSPITE). The game opens black on cutscene 01 (Bruno, 2026-11-04), then she is in the frame:
        /// the Interact key pressed again and again frees her (UprightRestraint). The camera of the frame looks at her from
        /// the front, a little low (cutscene 01, "Breaking free", item 1).
        /// </summary>
        private static void H1(DemoRoom r)
        {
            Vector3 f = LucyStart;
            GameObject frame = MenuLoopSetup.EnsureChild(r.Props, "Restraint_Frame");
            Color steel = new Color(0.82f, 0.83f, 0.86f);
            NoCollider(r.Box(frame, "Frame_Base", f + new Vector3(0.25f, 0.03f, 0f), new Vector3(1.0f, 0.06f, 1.0f), DemoRoom.Colour("frame_steel", steel))); // she stands on it
            r.Box(frame, "Frame_Back", f + new Vector3(0.42f, 1.05f, 0f), new Vector3(0.1f, 2.0f, 0.8f), DemoRoom.Colour("frame_steel", steel));
            Visual(r, frame, "Frame_Brace", f + new Vector3(0.3f, 1.62f, 0f), new Vector3(0.18f, 0.3f, 0.45f), new Color(0.3f, 0.3f, 0.32f), "frame_pad");
            Visual(r, frame, "Filter_Box", f + new Vector3(0.38f, 1.2f, 0.52f), new Vector3(0.16f, 0.24f, 0.16f), new Color(0.85f, 0.55f, 0.12f), "filter_amber");
            var straps = new List<GameObject>();
            foreach ((string n, float y) in new[] { ("Strap_Wrists", 1.05f), ("Strap_Chest", 1.3f), ("Strap_Hips", 0.92f) })
                straps.Add(Visual(r, frame, n, f + new Vector3(-0.12f, y, 0f), new Vector3(0.05f, 0.08f, 0.62f), new Color(0.12f, 0.12f, 0.13f), "strap"));
            GameObject line = Visual(r, frame, "Drain_Line", f + new Vector3(0.15f, 1.38f, 0.3f), new Vector3(0.03f, 0.03f, 0.5f), new Color(0.75f, 0.9f, 0.95f), "drain_line");
            line.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);
            GameObject cage = Visual(r, frame, "Crown_Cage", f + new Vector3(0f, 1.78f, 0f), new Vector3(0.42f, 0.36f, 0.42f), new Color(0.55f, 0.56f, 0.5f), "wire_cage");
            cage.isStatic = false;
            UprightRestraint restraint = MenuLoopSetup.EnsureComponent<UprightRestraint>(frame);
            restraint.freeFlag = Free;
            restraint.lockdownFlag = Lockdown;
            restraint.straps = straps.ToArray();
            restraint.drainLine = line;
            restraint.cage = cage.transform;
            restraint.cageFallen = f + new Vector3(-0.7f, 0.15f, 0.75f);
            restraint.point = MenuLoopSetup.EnsureChild(frame, "Point").transform;
            restraint.point.position = f + new Vector3(-0.05f, 0f, 0f); // under her: in reach whatever way she faces
            restraint.range = 1.2f;
            foreach (Transform t in frame.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;
            EditorUtility.SetDirty(restraint);

            r.Prop("Steel_Tray", new Vector3(23.6f, 0f, 37.2f), new Vector3(0.6f, 0.85f, 0.45f), 0.7f);
            r.Capsule("Capsule_H1", DrugType.Nalcon, new Vector3(23.6f, 1.0f, 37.2f)); // the morning dose: the first power back
            r.Prop("Drain_Cart", new Vector3(26.3f, 0f, 37.3f), new Vector3(0.6f, 1.0f, 0.5f), 0.45f);
            r.PlaceLucy(f, 270f);

            var wake = new List<ScriptStep>
            {
                ScriptStep.Raise(Feeding.LockFlag), // round 18: the feed starts locked
                ScriptStep.SetShort(90f),           // round 119: the SHORT bar about 90 percent (PROPOSED)
                ScriptStep.LockHer(),
                ScriptStep.Play(DemoNight.WithTitle(DemoScripts.Fmv("cutscenes/01_the_wake.md", "01_the_wake", "cs01"))),
                ScriptStep.FreeHer(),
                ScriptStep.Restraint(frame),        // the frame holds her: every MK bar at zero, the Interact key frees her
            };
            r.Beat("H1_Wake", WakeDone, Vector3.zero, Vector3.zero, wake.ToArray()).startsBlack = true;
            // The frame's camera: in front of her, a little low, the frame and the door to H2 in view.
            r.ExtraCamera("frame", new Vector3(22.0f, 1.05f, 34.2f), f + new Vector3(0f, 1.2f, 0f), 60f, f + new Vector3(-0.2f, 1.5f, 0f), new Vector3(1.4f, 3f, 1.4f));
        }

        // ── H2, treatment ─────────────────────────────────────────────

        /// <summary>
        /// Two scientists (beat 2, cutscene 02 staging 3). The first works at the bed behind the privacy screen, walks out
        /// and talks when she stops a step past the doorway, then attacks with the scalpel. The second stands at the bench
        /// by the door to H3; when the first dies he runs to that door, it does not open (the Hold is sealed), and he
        /// cowers there (CivilianBrain.SetEscape). He carries the airlock card: asked before the scan C11 he denies it,
        /// after it he gives it. The capsules of the tray fall in the talk. The recorder L35 on the bench.
        /// </summary>
        private static void H2(DemoRoom r)
        {
            r.Prop("Treatment_Bed", new Vector3(16.3f, 0f, 37.1f), new Vector3(2f, 0.6f, 1f), 0.55f);
            r.Prop("Sleeper_Under_Sheet", new Vector3(16.3f, 0.6f, 37.1f), new Vector3(1.7f, 0.22f, 0.6f), 0.75f);
            r.Prop("Dressing_Cart", new Vector3(17.2f, 0f, 37.6f), new Vector3(0.5f, 0.9f, 0.4f), 0.45f);
            r.Prop("Privacy_Screen", new Vector3(17.9f, 0f, 36.9f), new Vector3(0.06f, 1.8f, 2.1f), 0.65f);
            r.Prop("Bench", new Vector3(16.5f, 0f, 33.45f), new Vector3(2.4f, 0.9f, 0.5f), 0.4f);
            r.Item("L35_Recorder", DemoItems.L35(), new Vector3(17.2f, 0.95f, 33.45f), new Vector3(0.16f, 0.06f, 0.1f));

            GameObject scientist = MenuLoopSetup.EnsureChild(r.Figures, "Group_Scientist");
            EnemySpawn knife = r.Figure("Scientist_Knife", "labworker_knife", new Vector3(16.9f, 0f, 36.2f), 90f, parent: scientist);
            knife.heldUntilFlag = MeetDone;
            EditorUtility.SetDirty(knife);

            // The second scientist and his card.
            ItemPickup card = r.Item("Airlock_Card", DemoItems.AirlockCard(), new Vector3(15.9f, 0.05f, 34.1f), new Vector3(0.1f, 0.02f, 0.15f));
            GameObject runTo = MenuLoopSetup.EnsureChild(r.Story, "Runner_Door");
            runTo.transform.position = new Vector3(15.55f, 0f, 35.5f);
            EnemySpawn runner = r.Figure("Scientist_Runner", "labworker", new Vector3(15.9f, 0f, 34.0f), 270f, variant: 0);
            runner.drop = card;
            runner.escapeTo = runTo.transform;
            runner.escapeLines = new[] { Bark("h2_runner_1"), Bark("h2_runner_2") };
            runner.cowerFlag = RunnerCowered;
            runner.giveAfterFlag = CardScanned;
            runner.denyLine = Bark("h2_deny_card");
            runner.giveLine = Bark("h2_give_card");
            EditorUtility.SetDirty(runner);

            // The capsules roll off the tray in the talk: one EREBUS, one ORZABAL.
            GameObject tray = MenuLoopSetup.EnsureChild(r.Story, "Gate_Capsules");
            FlagGate fallen = MenuLoopSetup.EnsureComponent<FlagGate>(tray);
            fallen.flag = CapsulesFall;
            fallen.showWhenRaised = true;
            foreach (var capsule in new[] { ("Capsule_H2_Erebus", DrugType.NalconFire, new Vector3(18.6f, 0.15f, 35.0f)), ("Capsule_H2_Orzabal", DrugType.NalconLift, new Vector3(18.1f, 0.15f, 34.4f)) })
                ItemAssets.Pickup(tray, capsule.Item1, ItemAssets.Drug(capsule.Item2), PrimitiveType.Capsule, capsule.Item3, Vector3.one * 0.3f, Quaternion.Euler(0f, 0f, 90f));

            var meet = new List<ScriptStep>
            {
                ScriptStep.LockHer(),
                ScriptStep.Approach(scientist, new Vector3(18.7f, 0f, 35.9f)),
                ScriptStep.Wait(1.8f),
                ScriptStep.Raise(CapsulesFall),
                ScriptStep.Play(DemoScripts.Fmv("cutscenes/02_the_scientist.md", "02_the_scientist", "cs02")),
                ScriptStep.Approach(scientist, new Vector3(19.1f, 0f, 35.7f)), // "he steps closer. Louder. Worse."
                ScriptStep.Wait(0.5f),
                ScriptStep.FreeHer(),
                ScriptStep.AlertAlone(scientist),
            };
            r.Beat("H2_Scientist", MeetDone, new Vector3(20.1f, 1f, 35.5f), new Vector3(0.6f, 2f, 5f), meet.ToArray()); // wall to wall
            // The talk's camera on the south wall, east of the screen: her in the doorway from H1 (the exit rule), him as he comes round the screen.
            r.ExtraCamera("talk", new Vector3(19.2f, 2.8f, 33.3f), new Vector3(19.8f, 0.8f, 36.8f), 72f, new Vector3(19.75f, 1.5f, 36.1f), new Vector3(2.3f, 3f, 3.8f)); // the south strip by the camera: the cells
        }

        // ── H3, the guard post ────────────────────────────────────────

        private static void H3(DemoRoom r)
        {
            GameObject guards = MenuLoopSetup.EnsureChild(r.Figures, "Group_Guards");
            foreach ((string n, Vector3 at) in new[] { ("Guard_H3_A", new Vector3(12.6f, 0f, 34.4f)), ("Guard_H3_B", new Vector3(12.2f, 0f, 36.7f)) })
            {
                EnemySpawn g = r.Figure(n, "guard_shield", at, 90f, parent: guards);
                g.heldUntilFlag = GuardsOpen;
                g.askLine = Bark("h3_deny_card");
                g.askItemId = DemoItems.AirlockCard().itemId;
                g.askItemName = "the airlock card";
                EditorUtility.SetDirty(g);
            }
            r.Prop("Desk", new Vector3(10.2f, 0f, 37.2f), new Vector3(1.4f, 0.75f, 0.7f), 0.35f);
            r.Item("L9_Chart", DemoItems.L9(), new Vector3(10.2f, 0.78f, 37.2f), new Vector3(0.24f, 0.03f, 0.32f)); // the text log of the Hold (round 118)
            r.Prop("Monitor_Wall", new Vector3(12f, 1.0f, 33.25f), new Vector3(3.2f, 1.1f, 0.2f), 0.12f); // every room of the box
            r.Prop("Chair", new Vector3(10.4f, 0f, 36.4f), new Vector3(0.5f, 0.9f, 0.5f), 0.3f);
        }

        // ── H4, the Theatre and its booth ─────────────────────────────

        /// <summary>
        /// The Theatre (beat 4, §10.6): the surgery table under the arms of the Font, the drapes, the booth with its glass
        /// front. The nurse is locked in the booth. Two solutions: a full circle of mk_erebus on her (the glass front lets
        /// the powers through, as the speaking grille) or on the drapes starts the fire alarm and the door releases
        /// (FireAlarm, GlassDoor.openFlag); or two blasts of mk_warspite break the glass door. In the booth: the console
        /// of the tape L36 (it unlocks the feed) and the second wind pill.
        /// </summary>
        private static void H4(DemoRoom r)
        {
            r.Prop("Surgery_Table", new Vector3(11.4f, 0f, 39.7f), new Vector3(2.0f, 0.85f, 0.8f), 0.7f);
            GameObject drapes = r.Box(r.Props, "Drapes", new Vector3(11.4f, 0.92f, 39.7f), new Vector3(2.1f, 0.12f, 0.95f), DemoRoom.Colour("drapes", new Color(0.5f, 0.68f, 0.62f)));
            Target(drapes);
            foreach ((string n, float x) in new[] { ("Font_Arm_A", 10.8f), ("Font_Arm_B", 12.0f) })
                Visual(r, r.Props, n, new Vector3(x, 2.1f, 39.7f), new Vector3(0.12f, 1.2f, 0.12f), new Color(0.7f, 0.72f, 0.75f), "font_steel");
            Visual(r, r.Props, "Font_Ring", new Vector3(11.4f, 2.75f, 39.7f), new Vector3(1.8f, 0.12f, 0.6f), new Color(0.7f, 0.72f, 0.75f), "font_steel");

            // The booth: the north strip, a glass front at z 41.2 with the door at x 13.6.
            const float front = 41.2f, doorX = 13.6f, doorW = 1.0f;
            Color glass = new Color(0.62f, 0.74f, 0.8f);
            foreach ((string n, float a, float b) in new[] { ("Booth_Glass_W", 9f, doorX - doorW * 0.5f), ("Booth_Glass_E", doorX + doorW * 0.5f, 15f) })
            {
                GameObject pane = r.Box(r.Room, n, new Vector3((a + b) * 0.5f, 1.5f, front), new Vector3(b - a, 3f, 0.08f), DemoRoom.Colour("booth_glass", glass));
                pane.layer = 2; // Ignore Raycast: her powers and her lock go through the glass (the speaking grille); her body does not
                DemoRoom.Carve(pane); // the NavMesh bakes the Default layer only: the pane carves its own hole
            }
            GameObject door = r.Box(r.Props, "Booth_Door", new Vector3(doorX, 1.15f, front), new Vector3(doorW, 2.3f, 0.06f), DemoRoom.Colour("booth_door", new Color(0.7f, 0.8f, 0.86f)));
            door.isStatic = false;
            DemoRoom.Carve(door);
            Target(door);
            GlassDoor gd = MenuLoopSetup.EnsureComponent<GlassDoor>(door);
            gd.leaf = door.transform;
            gd.openOffset = new Vector3(doorW + 0.05f, 0f, 0f);
            gd.openFlag = Alarm;
            gd.brokenFlag = GlassBroken;
            gd.lockedText = BoothSign; // DEMO_PLAN §7
            r.Sign("Sign_Booth_Door", BoothSign, new Vector3(doorX, 2.45f, front - 0.06f), Vector3.back, new Vector2(1.0f, 0.22f), 16);
            gd.range = 1.4f;
            GameObject crack = Visual(r, r.Props, "Booth_Door_Crack", new Vector3(doorX, 1.3f, front - 0.05f), new Vector3(0.7f, 0.04f, 0.01f), new Color(0.95f, 0.95f, 0.95f), "glass_crack");
            crack.transform.rotation = Quaternion.Euler(0f, 0f, 35f);
            crack.isStatic = false;
            gd.crack = crack;
            GameObject shards = MenuLoopSetup.EnsureChild(r.Props, "Booth_Door_Shards");
            for (int i = 0; i < 6; i++)
                Visual(r, shards, "Shard_" + i, new Vector3(doorX - 0.4f + i * 0.16f, 0.02f, front - 0.3f + (i % 3) * 0.2f), new Vector3(0.14f, 0.02f, 0.1f), glass, "booth_glass");
            shards.isStatic = false;
            gd.shards = shards;
            EditorUtility.SetDirty(gd);

            EnemySpawn nurse = r.Figure("Nurse_Booth", "nurse", new Vector3(10.6f, 0f, 42.3f), 180f, variant: 1);
            nurse.hides = true;
            nurse.hideLine = Bark("h4_booth_1");
            nurse.foundLine = Bark("h4_booth_2");
            nurse.foundAt = 2.5f;
            nurse.askLine = Bark("h4_ask_card");
            nurse.askItemId = DemoItems.AirlockCard().itemId;
            nurse.askItemName = "the airlock card";
            EditorUtility.SetDirty(nurse);

            r.Prop("Booth_Console", new Vector3(12.6f, 0f, 42.65f), new Vector3(1.6f, 0.85f, 0.5f), 0.25f);
            r.Prop("Booth_Monitors", new Vector3(12.6f, 0.85f, 42.85f), new Vector3(1.6f, 0.5f, 0.12f), 0.08f);
            r.Switch("Console_L36", new Vector3(12.6f, 0.95f, 42.5f), TapePlay, "Play the tape?", "play the tape", "").range = 1.0f; // not through the glass
            r.Prop("Drug_Cabinet", new Vector3(14.4f, 0f, 42.7f), new Vector3(0.6f, 0.9f, 0.4f), 0.6f);
            r.Capsule("Second_Wind_H4", DrugType.DFelon, new Vector3(14.4f, 1.05f, 42.7f));
            var tape = new List<ScriptStep>
            {
                ScriptStep.WaitFlag(TapePlay),
                ScriptStep.LockHer(),
                ScriptStep.Play(DemoScripts.Cards(DemoScripts.Lines(DemoItems.HoldLines, "L36"), "l36_incident_tape", "l36")),
                ScriptStep.Give(DemoItems.L36().itemId),
                ScriptStep.Raise(FirstFeed.TapeFlag),
                ScriptStep.Raise(Feeding.UnlockFlag), // round 118, CANON: the video log unlocks the feed
                ScriptStep.FreeHer(),
            };
            r.Beat("H4_Tape", TapeDone, Vector3.zero, Vector3.zero, tape.ToArray());

            // The fire alarm of the room: red lamps, and the drapes burn when the fire was on them.
            GameObject alarmObj = MenuLoopSetup.EnsureChild(r.Story, "Fire_Alarm_H4");
            alarmObj.transform.position = new Vector3(12f, 0f, 40.5f);
            FireAlarm alarm = MenuLoopSetup.EnsureComponent<FireAlarm>(alarmObj);
            alarm.flag = Alarm;
            alarm.size = new Vector3(6f, 3.5f, 5f);
            alarm.paLines = DemoScripts.Lines("lines/T5_pa.md", "Call F", "*").ConvertAll(l => l.text).ToArray(); // round 125: call F
            GameObject lamps = MenuLoopSetup.EnsureChild(r.Props, "Alarm_Lamps");
            foreach (float x in new[] { 9.4f, 14.6f })
                Visual(r, lamps, "Alarm_Lamp_" + x.ToString("0.0"), new Vector3(x, 2.7f, 38.25f), new Vector3(0.25f, 0.18f, 0.12f), new Color(1f, 0.1f, 0.05f), "alarm_red");
            lamps.isStatic = false;
            alarm.lamps = lamps;
            GameObject flames = MenuLoopSetup.EnsureChild(r.Props, "Drape_Flames");
            for (int i = 0; i < 4; i++)
                Visual(r, flames, "Flame_" + i, new Vector3(10.7f + i * 0.45f, 1.3f, 39.7f), new Vector3(0.3f, 0.7f, 0.05f), i % 2 == 0 ? new Color(1f, 0.45f, 0.08f) : new Color(1f, 0.8f, 0.2f), "flame_" + (i % 2));
            flames.isStatic = false;
            alarm.flames = flames;
            EditorUtility.SetDirty(alarm);

            // Her first feed after the tape is cutscene 02b (FirstFeed). One watcher for the whole game, here.
            GameObject watcher = MenuLoopSetup.EnsureChild(r.Story, "First_Feed");
            MenuLoopSetup.EnsureComponent<FirstFeed>(watcher).cutscene = DemoScripts.Fmv("cutscenes/02b_the_chart.md", "02b_the_chart", "cs02b");
        }

        /// <summary>A lock target that does not lift and is not a prop (the box wall of G17): the powers reach it, the auto-lock may pick it.</summary>
        private static void Target(GameObject obj)
        {
            obj.isStatic = false;
            LockOnTargetable target = MenuLoopSetup.EnsureComponent<LockOnTargetable>(obj);
            var so = new SerializedObject(target);
            so.FindProperty("fallbackHeight").floatValue = 0f;
            so.FindProperty("isProp").boolValue = false;
            so.FindProperty("heavy").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── H5, the airlock ───────────────────────────────────────────

        /// <summary>
        /// The airlock (beats 5 and 5c): the outer door opens on the gap; the steel gurney that cannot break (GurneyRide);
        /// the red lamps of the lockdown; a spare ORZABAL capsule (PROPOSED: so the ride cannot be lost to a spent bar);
        /// the voice when she stands here with the card (KNIGHT, round 123): the urge and cutscene 02b with the feed
        /// locked, the other line with the feed unlocked. Once.
        /// </summary>
        private static void H5(DemoRoom r)
        {
            GameObject gurney = new GameObject("Steel_Gurney");
            gurney.transform.SetParent(r.Props.transform, false);
            gurney.transform.SetPositionAndRotation(new Vector3(6.4f, 0f, DoorZ), Quaternion.Euler(0f, 270f, 0f));
            gurney.layer = 2; // Ignore Raycast: the gurney's own sweeps skip it; her feet still find the deck
            BoxCollider body = gurney.AddComponent<BoxCollider>();
            body.center = new Vector3(0f, 0.375f, 0f);
            body.size = new Vector3(0.75f, 0.75f, 2.0f);
            Material steel = DemoRoom.Colour("gurney_steel", new Color(0.78f, 0.8f, 0.84f));
            GameObject deck = RoomKit.Box(gurney, "Deck", gurney.transform.position + Vector3.up * 0.7f, new Vector3(0.75f, 0.1f, 2.0f));
            deck.transform.rotation = gurney.transform.rotation;
            deck.GetComponent<Renderer>().sharedMaterial = steel;
            NoCollider(deck);
            foreach (Vector3 leg in new[] { new Vector3(-0.3f, 0f, -0.9f), new Vector3(0.3f, 0f, -0.9f), new Vector3(-0.3f, 0f, 0.9f), new Vector3(0.3f, 0f, 0.9f) })
            {
                GameObject l = RoomKit.Box(gurney, "Leg", gurney.transform.TransformPoint(leg + Vector3.up * 0.33f), new Vector3(0.05f, 0.66f, 0.05f));
                l.GetComponent<Renderer>().sharedMaterial = steel;
                NoCollider(l);
            }
            foreach (Transform t in gurney.GetComponentsInChildren<Transform>()) { t.gameObject.isStatic = false; t.gameObject.layer = 2; }
            MenuLoopSetup.EnsureComponent<Unity.AI.Navigation.NavMeshModifier>(gurney).ignoreFromBuild = true;
            UnityEngine.AI.NavMeshObstacle carve = gurney.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            carve.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            carve.center = body.center;
            carve.size = body.size;
            carve.carving = true;
            GurneyRide ride = gurney.AddComponent<GurneyRide>();
            ride.deck = 0.75f;
            ride.range = 1.7f;
            EditorUtility.SetDirty(ride);

            r.Capsule("Capsule_H5_Orzabal", DrugType.NalconLift, new Vector3(8.4f, 0.15f, 37.4f));
            GameObject red = MenuLoopSetup.EnsureChild(r.Story, "Gate_Lockdown");
            FlagGate gate = MenuLoopSetup.EnsureComponent<FlagGate>(red);
            gate.flag = Lockdown;
            gate.showWhenRaised = true;
            Visual(r, red, "Lockdown_Lamp", new Vector3(4.2f, 2.75f, DoorZ), new Vector3(0.1f, 0.15f, 2.6f), new Color(1f, 0.08f, 0.05f), "alarm_red");
            var glow = new GameObject("Lockdown_Light");
            glow.transform.SetParent(red.transform, false);
            glow.transform.position = new Vector3(5.2f, 2.4f, DoorZ);
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.15f, 0.1f);
            light.range = 6f;
            light.intensity = 1.5f;

            // The voice (§10.6, H1_the_hold.md): the first step into the airlock with the card. Two sequences, one plays.
            string card = DemoItems.AirlockCard().itemId;
            var urge = new List<ScriptStep> { ScriptStep.WaitItem(card), ScriptStep.WaitRoom("H5"), ScriptStep.StopIfFlag(Feeding.UnlockFlag), ScriptStep.Raise(VoiceHeard), ScriptStep.LockHer() };
            urge.AddRange(DemoScripts.Steps(DemoItems.HoldLines, "The voice", "The script, the urge", "h5_urge"));
            urge.Add(ScriptStep.Feast(DemoScripts.Fmv("cutscenes/02b_the_chart.md", "02b_the_chart", "cs02b")));
            urge.Add(ScriptStep.FreeHer());
            r.Beat("H5_Voice_Urge", VoiceUrge, Vector3.zero, Vector3.zero, urge.ToArray());
            var fed = new List<ScriptStep> { ScriptStep.WaitItem(card), ScriptStep.WaitRoom("H5"), ScriptStep.StopUnlessFlag(Feeding.UnlockFlag), ScriptStep.StopIfFlag(VoiceHeard), ScriptStep.Raise(VoiceHeard) };
            fed.AddRange(DemoScripts.Steps(DemoItems.HoldLines, "The voice", "The script, the other line", "h5_fed"));
            r.Beat("H5_Voice_Fed", VoiceFed, Vector3.zero, Vector3.zero, fed.ToArray());
        }

        // ── HT, the tube over the gap ─────────────────────────────────

        /// <summary>
        /// The gap between the airlock and the landing: the tube is pulled back, so the floor of this room shows only when
        /// EXTEND brings the tube out (a FlagGate). The side walls stay as invisible rails. A fall into the dark puts her
        /// back in the airlock (PitReturn). The NavMesh leaves the tube out: no enemy follows her across.
        /// </summary>
        private static void HT(DemoRoom r)
        {
            GameObject tube = MenuLoopSetup.EnsureChild(r.Story, "Gate_Tube");
            FlagGate gate = MenuLoopSetup.EnsureComponent<FlagGate>(tube);
            gate.flag = TubeOut;
            gate.showWhenRaised = true;
            Transform floor = r.Room.transform.Find("Floor");
            if (floor != null)
            {
                floor.name = "Tube_Floor";
                floor.SetParent(tube.transform, true);
                MenuLoopSetup.EnsureComponent<Unity.AI.Navigation.NavMeshModifier>(floor.gameObject).ignoreFromBuild = true;
                floor.GetComponent<Renderer>().sharedMaterial = DemoRoom.Colour("tube_floor", new Color(0.55f, 0.62f, 0.66f));
            }
            for (float x = -1.7f; x < 4f; x += 0.6f)
                Visual(r, tube, "Rib_" + x.ToString("0.0"), new Vector3(x, 1.5f, TubeZ1 - 0.05f), new Vector3(0.06f, 3f, 0.06f), new Color(0.7f, 0.82f, 0.88f), "tube_rib");
            foreach (Transform child in r.Room.transform)
            {
                if (child.name.StartsWith("Wall_N") || child.name.StartsWith("Wall_S"))
                    Object.DestroyImmediate(child.GetComponent<Renderer>()); // a rail she cannot see: she rides straight across
            }
            var pitObj = new GameObject("Pit_Return");
            pitObj.transform.SetParent(r.Story.transform, false);
            pitObj.transform.position = new Vector3(1f, -1.1f, DoorZ);
            PitReturn pit = pitObj.AddComponent<PitReturn>();
            pit.size = new Vector2(6.2f, TubeWidth);
            pit.floorAbove = 1.1f;
            pit.guide = 0f;
            var back = new GameObject("Pit_Back");
            back.transform.SetParent(pitObj.transform, false);
            back.transform.position = new Vector3(8.2f, 0f, 34.0f);
            pit.back = back.transform;
            EditorUtility.SetDirty(pit);
        }

        // ── H6, the landing ───────────────────────────────────────────

        private static void H6(DemoRoom r)
        {
            r.Prop("Tube_Collar", new Vector3(-2.35f, 0f, 37f - 0.2f), new Vector3(0.4f, 3f, 0.3f), 0.5f);
            r.Switch("Extend_Button", new Vector3(-2.35f, 1.2f, 33.6f), TubeOut, "Extend the tube?", "extend the tube", "");
            r.LockedLeaf(r.DoorTo("H7"), DemoItems.AirlockCard().itemId, "the airlock card");
            r.Scan("Scan_C11", new Vector3(-3.3f, 1.2f, 29.2f), "c11_card_reader",
                "PLACEHOLDER (C11): a gloved hand in a light blue cuff clips a white card to a lanyard. Behind it, out of focus, the glass of the box and the steel of a treatment bench.",
                CardScanned);
        }

        // ── H7, the gallery ───────────────────────────────────────────

        /// <summary>The arena (beat 5d): the three armed scientists of W2 (round 118) and a guard with a shield, crates to throw.</summary>
        private static void H7(DemoRoom r)
        {
            GameObject arena = MenuLoopSetup.EnsureChild(r.Figures, "Group_Arena");
            r.Figure("Scientist_A", "labworker_pistol", new Vector3(9.5f, 0f, 26.4f), 270f, parent: arena);
            r.Figure("Scientist_B", "labworker_knife", new Vector3(6.0f, 0f, 24.4f), 270f, parent: arena);
            r.Figure("Scientist_C", "labworker_knife", new Vector3(6.5f, 0f, 27.8f), 270f, parent: arena);
            r.Figure("Guard_H7", "guard_shield", new Vector3(11.5f, 0f, 25.0f), 270f, parent: arena);
            PropAssets.Crate("Crate_H7_A", new Vector3(-0.5f, 0f, 26.6f), 10f, 0.5f);
            PropAssets.Crate("Crate_H7_B", new Vector3(2.5f, 0f, 24.0f), 40f, 0.5f);
            PropAssets.Crate("Crate_H7_C", new Vector3(4.0f, 0f, 28.2f), 0f, 0.5f);
            Visual(r, r.Props, "Control_Window", new Vector3(4f, 1.8f, 28.84f), new Vector3(16f, 1.1f, 0.02f), new Color(0.05f, 0.06f, 0.09f), "hall_window");
            r.Prop("Console_Row", new Vector3(4f, 0f, 28.5f), new Vector3(10f, 0.85f, 0.5f), 0.25f);
        }

        // ── H8, the save room ─────────────────────────────────────────

        private static void H8(DemoRoom r)
        {
            SaveAssets.SavePoint("Save_H8", new Vector3(6.4f, 0f, 21f), 270f).transform.SetParent(r.Props.transform, true);
            r.Prop("Cot", new Vector3(1.9f, 0f, 21.2f), new Vector3(0.8f, 0.45f, 1.9f), 0.5f);
            r.Prop("Sink", new Vector3(2.0f, 0f, 19.4f), new Vector3(0.5f, 0.85f, 0.4f), 0.55f);
            r.Capsule("Capsule_H8_Warspite", DrugType.Nalcon, new Vector3(5.2f, 0.15f, 22.4f));
            r.Capsule("Capsule_H8_Erebus", DrugType.NalconFire, new Vector3(5.6f, 0.15f, 22.4f));
            r.Capsule("Capsule_H8_Orzabal", DrugType.NalconLift, new Vector3(6.0f, 0.15f, 22.4f));
        }

        // ── H9, the office of Dr. Colton ──────────────────────────────

        /// <summary>
        /// The office (beat 5e): the hidden door from the save room has a push bar on the Hold side only (a lever on the
        /// H8 face); when she is in the office it shuts behind her and jams (OfficeShut): a wall again. The printer holds
        /// Stevens' sheet L1. The desk: the plan of his trip, a photo face down.
        /// </summary>
        private static void H9(DemoRoom r)
        {
            r.Prop("Desk", new Vector3(4f, 0f, 16.6f), new Vector3(2.0f, 0.75f, 0.9f), 0.12f);
            r.Prop("Trip_Plan", new Vector3(3.6f, 0.75f, 16.6f), new Vector3(0.3f, 0.01f, 0.22f), 0.85f);
            r.Prop("Photo_Face_Down", new Vector3(4.6f, 0.75f, 16.8f), new Vector3(0.15f, 0.02f, 0.2f), 0.2f);
            r.Prop("Printer", new Vector3(6.4f, 0f, 18.4f), new Vector3(0.5f, 0.9f, 0.4f), 0.35f);
            r.Item("L1_Printout", DemoItems.Plan(), new Vector3(6.4f, 0.95f, 18.4f), new Vector3(0.24f, 0.02f, 0.32f));
            r.Prop("Lacquer_Panel", new Vector3(2.2f, 0f, 18.8f), new Vector3(1.6f, 2.6f, 0.1f), 0.05f);
            r.Beat("H9_Shut", "demo_h9_shut", Vector3.zero, Vector3.zero, ScriptStep.Wait(1.2f), ScriptStep.Raise(OfficeShut));
        }

        // ── After all rooms ───────────────────────────────────────────

        /// <summary>
        /// The wiring across rooms (after every room is built): the guards of H3 open the door of H2 when the first
        /// scientist is dead and the second had time to run and cower (round 118: "when he cowers, the guards of H3 open
        /// the door and come for her"), and the black hall around the box.
        /// </summary>
        public static void Wire(Dictionary<string, DemoRoom> rooms)
        {
            foreach (FireDoor door in rooms["H2"].Root.transform.parent.GetComponentsInChildren<FireDoor>(true))
                if (door.name == "FireDoor_H2_H3" || door.name == "FireDoor_H3_H2")
                {
                    door.openFlag = GuardsOpen;
                    EditorUtility.SetDirty(door);
                }
            GameObject scientist = rooms["H2"].Figures.transform.Find("Group_Scientist").gameObject;
            GameObject guards = rooms["H3"].Figures.transform.Find("Group_Guards").gameObject;
            GameObject first = guards.transform.GetChild(0).gameObject;
            var come = new List<ScriptStep>
            {
                ScriptStep.WaitDead(scientist),
                ScriptStep.Wait(6f), // he runs, he hits the door, he cowers
                ScriptStep.Raise(GuardsOpen),
                ScriptStep.Wait(0.8f),
                ScriptStep.Say(first, "h3_enter", Bark("h3_enter")),
                ScriptStep.Alert(guards),
            };
            rooms["H2"].Beat("H3_Guards_Come", GuardsDone, Vector3.zero, Vector3.zero, come.ToArray());
            Hall(rooms["HT"]); // under the tube: drawn when the gap is in view
        }

        /// <summary>The black hall of the Hold (§5.0): no window, about 12 m high, the box hangs in it. Visual only.</summary>
        private static void Hall(DemoRoom r)
        {
            GameObject hall = MenuLoopSetup.EnsureChild(r.Root, "[Hall]");
            Color black = new Color(0.02f, 0.02f, 0.025f);
            const float x0 = -6.3f, x1 = 28.3f, z0 = 29f, z1 = 44f, y0 = -9f, y1 = 9f;
            Visual(r, hall, "Hall_Pit", new Vector3((x0 + x1) * 0.5f, y0, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.2f, z1 - z0), black, "hall_black");
            Visual(r, hall, "Hall_North", new Vector3((x0 + x1) * 0.5f, 0f, z1), new Vector3(x1 - x0, y1 - y0, 0.2f), black, "hall_black");
            Visual(r, hall, "Hall_East", new Vector3(x1, 0f, (z0 + z1) * 0.5f), new Vector3(0.2f, y1 - y0, z1 - z0), black, "hall_black");
            Visual(r, hall, "Hall_South", new Vector3((-2f + x1) * 0.5f, 0f, 29.3f), new Vector3(x1 + 2f, y1 - y0, 0.2f), black, "hall_black");
            Visual(r, hall, "Hall_Top", new Vector3((x0 + x1) * 0.5f, y1, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.2f, z1 - z0), black, "hall_black");
            Visual(r, hall, "Hall_West", new Vector3(x0, 0f, (z0 + z1) * 0.5f), new Vector3(0.2f, y1 - y0, z1 - z0), black, "hall_black");
            foreach (float x in new[] { 5f, 25f })
                foreach (float z in new[] { 33.2f, 42.8f })
                    Visual(r, hall, $"Cable_{x:0}_{z:0}", new Vector3(x, 6f, z), new Vector3(0.05f, 6f, 0.05f), new Color(0.4f, 0.4f, 0.42f), "hall_cable");
            foreach (Renderer rend in hall.GetComponentsInChildren<Renderer>()) rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ── The writer's barks ────────────────────────────────────────

        private static Dictionary<string, string> barks;

        /// <summary>A line of game/barks.csv by its situation id (h2_runner_1 ...). Missing: a PLACEHOLDER with the id.</summary>
        public static string Bark(string id)
        {
            if (barks == null)
            {
                barks = new Dictionary<string, string>();
                string path = System.IO.Path.GetFullPath(DemoScripts.WriterGame + "/barks.csv");
                if (System.IO.File.Exists(path))
                    foreach (string[] row in DeathScreenSetup.ReadCsv(System.IO.File.ReadAllText(path)))
                        if (row.Length >= 3 && !barks.ContainsKey(row[1].Trim())) barks[row[1].Trim()] = row[2].Trim();
            }
            return barks.TryGetValue(id, out string line) ? line : "PLACEHOLDER " + id;
        }
    }
}
#endif
