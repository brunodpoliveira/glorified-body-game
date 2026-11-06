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
    /// The coda (Bruno, 2026-11-06: "the mk edgar can be done as suggested by the writer"; the writer's cutscenes/11_the_coda.md, first pass):
    /// after U1 the car park X1 with the car and the glove box, the bulletin, then the service station: the shop R1 (the poster beat, the
    /// flashback, mk_edgar), the service bay R2 and the toll yard R3 (a puzzle of mk_edgar each), the title. The rooms stand apart on their
    /// own map floor, and she comes to them by a cut. This demo is not the final demo: cheap boxes.
    /// </summary>
    public static class DemoCoda
    {
        public const string CrownGone = GlorifiedBody.UI.PlayerHudFlags.CrownGone, ReadoutOn = GlorifiedBody.UI.PlayerHudFlags.ReadoutOn;
        public const string CarDoor = "demo_car_door", ShutterFlag = "demo_r2_shutter", BarrierFlag = "demo_r3_barrier", SunglassesId = "sunglasses";
        public const string EntryX1 = "Entry_X1", EntryR1 = "Entry_R1";
        private const string File = "cutscenes/11_the_coda.md";

        public static List<RoomSpec> Rooms() => new List<RoomSpec>
        {
            Spec("X1", "CAR PARK", 60, 0, 72, 10, 4f, X1),
            Spec("R1", "THE SHOP", 60, 20, 68, 26, 3f, R1, new Door('E', 21, "R2")),
            Spec("R2", "SERVICE BAY", 68, 18, 76, 26, 3.5f, R2, new Door('W', 21, "R1"), new Door('E', 20, "R3")),
            Spec("R3", "TOLL YARD", 76, 18, 86, 26, 4f, R3, new Door('W', 20, "R2")),
        };

        private static RoomSpec Spec(string id, string title, float x0, float z0, float x1, float z1, float height, System.Action<DemoRoom> dress, params Door[] doors)
        {
            var spec = new RoomSpec { Id = id, Title = title, Floor = DemoItems.FloorRoad, Music = DemoPublic.Music, X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Height = height, Dress = dress };
            spec.Doors.AddRange(doors);
            return spec;
        }

        private static void Entry(DemoRoom r, string name, Vector3 at, float yaw)
        {
            GameObject entry = MenuLoopSetup.EnsureChild(r.Room, name);
            entry.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        }

        /// <summary>X1: rain and box cars (rain later). The black car, its glove box, the door that waits for the sunglasses.</summary>
        private static void X1(DemoRoom r)
        {
            Entry(r, EntryX1, new Vector3(61.5f, 0.05f, 1.5f), 45f);
            // She starts by the south wall. The lane runs east along it, then north to the car (the first layout started her inside a car).
            foreach (float x in new[] { 62.5f, 65.3f }) r.Prop($"Car_West_{x:0}", new Vector3(x, 0f, 4.8f), new Vector3(1.8f, 1.3f, 2.8f), 0.25f);
            foreach (float x in new[] { 62f, 64.8f, 67.6f, 70.4f }) r.Prop($"Car_North_{x:0}", new Vector3(x, 0f, 8.8f), new Vector3(1.8f, 1.3f, 2.2f), 0.25f);
            r.Prop("Car_Human", new Vector3(70.4f, 0f, 4.8f), new Vector3(1.9f, 1.3f, 4.6f), 0.05f);
            r.Sign("Sign_Plate", "HUMAN", new Vector3(70.4f, 0.7f, 2.48f), Vector3.back, new Vector2(1f, 0.25f), 48);
            r.Prop("Glove_Box", new Vector3(68.9f, 0f, 3.9f), new Vector3(0.5f, 0.9f, 0.5f), 0.15f);
            r.Item("Sunglasses", DemoItems.Sunglasses(), new Vector3(68.9f, 0.95f, 3.75f), new Vector3(0.2f, 0.04f, 0.1f));
            r.Capsule("Tin_Unknown", DrugType.NalconMind, new Vector3(68.9f, 1f, 4.05f));
            FlagSwitch door = r.Switch("Car_Door", new Vector3(69.4f, 1f, 5.6f), CarDoor, "Get in?", "get in", "");
            door.requiredItem = SunglassesId;
            EditorUtility.SetDirty(door);
            List<ScriptStep> lines = DemoScripts.Steps(File, "cs11");
            r.Beat("X1_Clothes", "demo_x1_clothes", new Vector3(68f, 1f, 4.6f), new Vector3(2f, 2f, 5f),
                ScriptStep.Radio("", "cs11_clothes", "[Folded clothes on the seat, a cap on top. She wears them. The glove box is open.]"));
            var readout = new List<ScriptStep> { ScriptStep.WaitItem(SunglassesId), ScriptStep.Raise(ReadoutOn) };
            if (lines.Count > 0) readout.Add(lines[0]);
            r.Beat("X1_Readout", "demo_x1_readout", Vector3.zero, Vector3.zero, readout.ToArray());
            r.Beat("X1_Drive", "demo_x1_drive", Vector3.zero, Vector3.zero,
                ScriptStep.WaitFlag(CarDoor),
                ScriptStep.LockHer(),
                ScriptStep.Radio("", "cs11_s10", "[The tail lights go away along the river road, toward the bridge.]"),
                ScriptStep.ShowBulletin(),
                ScriptStep.MoveHer(EntryR1),
                ScriptStep.FreeHer());
        }

        /// <summary>R1: the shop. The attendant behind the counter, the sheet on the till, the poster beat, the flashback, mk_edgar.</summary>
        private static void R1(DemoRoom r)
        {
            Entry(r, EntryR1, new Vector3(64f, 0.05f, 20.8f), 0f);
            r.Prop("Counter", new Vector3(63f, 0f, 24.6f), new Vector3(5f, 0.95f, 0.7f), 0.3f);
            r.Prop("Till", new Vector3(64f, 0.95f, 24.6f), new Vector3(0.4f, 0.3f, 0.3f), 0.15f);
            r.Prop("Shelf_A", new Vector3(61.2f, 0f, 22f), new Vector3(0.5f, 1.6f, 2.4f), 0.45f);
            r.Prop("Wall_Phone", new Vector3(62f, 1.3f, 25.9f), new Vector3(0.25f, 0.3f, 0.1f), 0.6f);
            r.Sign("Sign_Shop", "SERVICE. ROAD CLOSED AT THE BRIDGE.", new Vector3(64f, 2.3f, 25.83f), Vector3.back, new Vector2(3.4f, 0.4f), 36);
            EnemySpawn attendant = r.Figure("Attendant", "labworker", new Vector3(64f, 0f, 25.4f), 180f, variant: 0);
            attendant.hides = true;
            attendant.foundAt = 0f;
            EditorUtility.SetDirty(attendant);

            List<ScriptStep> lines = DemoScripts.Steps(File, "cs11");
            ScriptStep Line(int i) => i < lines.Count ? lines[i] : ScriptStep.Wait(0.1f);
            var beat = new List<ScriptStep> { ScriptStep.LockHer() };
            for (int i = 1; i <= 6; i++) beat.Add(Line(i));
            beat.Add(ScriptStep.Kneel("Attendant"));
            beat.Add(Line(7));
            beat.Add(ScriptStep.Play(Flashback()));
            beat.Add(Line(8));
            beat.Add(ScriptStep.Raise(PsychicPowers.EdgarFlag));
            beat.Add(ScriptStep.TakeMind("Attendant"));
            beat.Add(Line(9));
            beat.Add(Line(10));
            beat.Add(Line(11));
            beat.Add(ScriptStep.Radio("", "cs11_hint_key", "Key 4: mk_edgar. A full circle takes a mind. It costs most of the bar."));
            beat.Add(ScriptStep.FreeHer());
            r.Beat("R1_Poster", "demo_r1_poster", new Vector3(64f, 1f, 22.4f), new Vector3(8f, 2f, 1f), beat.ToArray());
        }

        /// <summary>R2: the mechanic behind the rail, the shutter's switch behind him, a spare by the back door.</summary>
        private static void R2(DemoRoom r)
        {
            const float rail = 22.5f;
            r.Box(r.Room, "Rail", new Vector3(72f, 0.5f, rail), new Vector3(8f, 1f, 0.3f), DemoRoom.Grey(0.3f));
            r.Prop("Car_On_Lift", new Vector3(71f, 0.8f, 19.4f), new Vector3(1.8f, 1.2f, 1.6f), 0.25f);
            r.Sign("Sign_Rail", "SHUTTER CONTROL. ASK A MEMBER OF STAFF.", new Vector3(72f, 0.75f, rail - 0.17f), Vector3.back, new Vector2(3.2f, 0.3f), 30);
            Shutter(r);
            Sent(r, "Switch_Shutter", new Vector3(74.5f, 0f, 25.5f), ShutterFlag);
            GameObject figures = r.Figures;
            EnemySpawn mechanic = r.Figure("Mechanic", "labworker", new Vector3(72f, 0f, 24.4f), 180f, variant: 1);
            mechanic.hides = true;
            mechanic.foundAt = 0f;
            mechanic.hideLine = "We're closed. Hey. Lady, we're closed.";
            EditorUtility.SetDirty(mechanic);
            Spare(r, "Mechanic", "labworker", new Vector3(69f, 0f, 25.3f), ShutterFlag, true);
            SaveAssets.SavePoint("Save_R2", new Vector3(68.6f, 0f, 19f), 90f).transform.SetParent(r.Props.transform, true);
            r.Beat("R2_Hint", "demo_r2_hint", new Vector3(70f, 1f, 21f), new Vector3(1f, 2f, 3f),
                ScriptStep.Radio("", "cs11_hint_send", "The defend key sends the one you hold to a switch."));
        }

        /// <summary>R3: the guard behind the rail, the barrier's switch in the booth behind him, two capsules by the door, then the title.</summary>
        private static void R3(DemoRoom r)
        {
            const float rail = 22.5f;
            r.Box(r.Room, "Rail", new Vector3(81f, 0.5f, rail), new Vector3(10f, 1f, 0.3f), DemoRoom.Grey(0.3f));
            r.Prop("Booth", new Vector3(84.6f, 0f, 25.3f), new Vector3(1.8f, 2.4f, 1.2f), 0.35f);
            r.Sign("Sign_Booth", "BARRIER OPERATED BY SECURITY ONLY.", new Vector3(84.6f, 1.9f, 24.68f), Vector3.back, new Vector2(1.7f, 0.3f), 24);
            Sent(r, "Switch_Barrier", new Vector3(83f, 0f, 25.5f), BarrierFlag);
            r.Capsule("Capsule_R3_A", DrugType.Nalcon, new Vector3(76.8f, 0.15f, 18.6f));
            r.Capsule("Capsule_R3_B", DrugType.NalconMind, new Vector3(77.6f, 0.15f, 18.6f));
            r.Figure("Guard_Booth", "guard_pistol", new Vector3(81f, 0f, 24.6f), 180f);
            Spare(r, "Guard_Booth", "guard_pistol", new Vector3(77f, 0f, 25.3f), BarrierFlag, false);
            GameObject barrier = MenuLoopSetup.EnsureChild(r.Story, "Gate_Barrier");
            FlagGate gate = MenuLoopSetup.EnsureComponent<FlagGate>(barrier);
            gate.flag = BarrierFlag;
            gate.showWhenRaised = false;
            r.Box(barrier, "Barrier", new Vector3(85.6f, 1f, 21f), new Vector3(0.1f, 0.15f, 5f), DemoRoom.Colour("barrier", new Color(0.85f, 0.2f, 0.15f)));
            r.Beat("R3_Title", "demo_r3_title", Vector3.zero, Vector3.zero,
                ScriptStep.WaitFlag(BarrierFlag),
                ScriptStep.LockHer(),
                ScriptStep.Radio("", "cs11_s11", "[The barrier rises. The tail lights cross the bridge, snow in them, the city ahead.]"),
                ScriptStep.EndDemo());
        }

        private static void Shutter(DemoRoom r)
        {
            GameObject shutter = MenuLoopSetup.EnsureChild(r.Story, "Gate_Shutter");
            FlagGate gate = MenuLoopSetup.EnsureComponent<FlagGate>(shutter);
            gate.flag = ShutterFlag;
            gate.showWhenRaised = false;
            DemoRoom.Carve(r.Box(shutter, "Shutter", new Vector3(76f, 1.3f, 20f), new Vector3(0.2f, 2.6f, 2f), DemoRoom.Colour("shutter", new Color(0.55f, 0.12f, 0.1f))));
        }

        private static void Sent(DemoRoom r, string name, Vector3 at, string flag)
        {
            GameObject lever = MenuLoopSetup.EnsureChild(r.Props, name);
            lever.transform.position = at;
            GameObject post = MenuLoopSetup.EnsureChildPrimitive(lever, "Post", PrimitiveType.Cube, new Vector3(0f, 0.6f, 0f), new Vector3(0.3f, 1.2f, 0.2f));
            post.GetComponent<Renderer>().sharedMaterial = DemoRoom.Colour("sent_switch", new Color(0.8f, 0.2f, 0.15f));
            GameObject stand = MenuLoopSetup.EnsureChild(lever, "Stand");
            stand.transform.position = at + Vector3.back * 0.9f;
            SentSwitch sent = MenuLoopSetup.EnsureComponent<SentSwitch>(lever);
            sent.flag = flag;
            sent.point = stand.transform;
            sent.lamp = post.GetComponent<Renderer>();
            EditorUtility.SetDirty(sent);
        }

        private static void Spare(DemoRoom r, string watched, string kind, Vector3 at, string flag, bool hider)
        {
            EnemySpawn spare = r.Figure("Spare_" + watched, kind, at, 180f);
            spare.hides = hider;
            spare.foundAt = 0f;
            spare.gameObject.SetActive(false);
            EditorUtility.SetDirty(spare);
            GameObject watcher = MenuLoopSetup.EnsureChild(r.Figures, "Watch_" + watched);
            SpareFigure component = MenuLoopSetup.EnsureComponent<SpareFigure>(watcher);
            component.watched = watched;
            component.doneFlag = flag;
            component.spares = new[] { spare };
            EditorUtility.SetDirty(component);
        }

        /// <summary>The flashback, first pass: black cards, hard cuts, the French lines as subtitles with what each still shows.</summary>
        public static CutsceneAsset Flashback()
        {
            const string id = "11_the_coda_flashback";
            System.IO.Directory.CreateDirectory(ScriptTestSetup.CutscenesFolder);
            string path = ScriptTestSetup.CutscenesFolder + "/cutscene_" + id + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<CutsceneAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CutsceneAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.cutsceneId = id;
            asset.music = null;
            Texture2D black = DemoV2.Still("cs07b_black");
            AudioClip metronome = DemoV2.Sound("cs07b_f2");
            int n = 0;
            CutsceneAsset.Shot Shot(float seconds, string text, AudioClip sound = null) => new CutsceneAsset.Shot
            {
                still = black, seconds = seconds, sound = sound, panFrom = new Vector3(0f, 0f, 1f), panTo = new Vector3(0f, 0f, 1f),
                speaker = "", lineId = $"cs11f_{n++:00}", text = text,
            };
            asset.shots = new[]
            {
                Shot(2f, "Regarde-le.", metronome),
                Shot(2.4f, "[Short grass to a straight edge, then only sky. Far off, a man in a dark overcoat.]  Il ne te connaît pas."),
                Shot(2.4f, "[A raised forearm in a white glove, the palm out.]  Les faibles, on leur demande."),
                Shot(2.4f, "[His knees in the grass. His hat has fallen.]  Les forts, on les casse d'abord."),
                Shot(2.4f, "[His shoes walk through the grass, at the speed of the metronome.]  Il ne pleure pas."),
                Shot(2.2f, "[The edge. Grass, and nobody. The wind.]  Encore."),
            };
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
#endif
