#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Story;

namespace GlorifiedBody.Editor.Demo
{
    /// <summary>
    /// B1, the brain sector, in the demo (DEMO_PLAN beat 25, round 37: "the demo waits for it", CANON round 52; Bruno,
    /// 2026-11-09: "get started with all 5 items"). The fight of BRAIN TEST (BrainTestSetup.DressSector and Wire, the
    /// same sector), on its own map floor, east of the coda. She comes to it by a cut after the Crown beat of U1 (the
    /// grabber takes the Crown, the HUD of the fight stays). Its death opens the north doorway to B2, the builders' door,
    /// where the exit lines of cutscene 10 play, then the cut to the car park X1.
    /// </summary>
    public static class DemoBrain
    {
        public const string Sector = "B1", BuildersDoor = "B2";
        public const string EntryB1 = "Entry_B1";
        public const string DeadFlag = "demo_brain_dead";

        public static List<RoomSpec> Rooms() => new List<RoomSpec>
        {
            Spec(Sector, "THE BRAIN SECTOR", 92, 0, 110, 12, 6f, B1, new Door('N', 101, BuildersDoor)),
            Spec(BuildersDoor, "THE BUILDERS' DOOR", 99, 12, 103, 15, 3.2f, B2, new Door('S', 101, Sector)),
        };

        private static RoomSpec Spec(string id, string title, float x0, float z0, float x1, float z1, float height, System.Action<DemoRoom> dress, params Door[] doors)
        {
            var spec = new RoomSpec { Id = id, Title = title, Floor = DemoItems.FloorBrain, Music = DemoPublic.Music, X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Height = height, Dress = dress };
            spec.Doors.AddRange(doors);
            return spec;
        }

        private static void B1(DemoRoom r)
        {
            GameObject entry = MenuLoopSetup.EnsureChild(r.Room, EntryB1);
            entry.transform.SetPositionAndRotation(new Vector3(101f, 0.05f, 1.5f), Quaternion.identity); // by the south wall, under the eye, facing in
            BrainTestSetup.DressSector(r, BuildersDoor);
        }

        /// <summary>The fight's wiring: after every room is built (DemoWorld.BuildAll), as BRAIN TEST does it.</summary>
        public static void Wire(Dictionary<string, DemoRoom> rooms)
        {
            if (!rooms.TryGetValue(Sector, out DemoRoom sector)) return;
            BrainTestSetup.Wire(sector, DeadFlag, ""); // the Crown went in U1 (DemoPublic.U1 raises CrownGone)
        }

        /// <summary>B2: past the leaf the brain held. The exit lines of cutscene 10 (Stevens: "Go."), then the car park.</summary>
        private static void B2(DemoRoom r)
        {
            var exit = new List<ScriptStep> { ScriptStep.LockHer() };
            exit.AddRange(DemoScripts.Steps("cutscenes/10_the_exit.md", "cs10"));
            exit.Add(ScriptStep.MoveHer(DemoCoda.EntryX1));
            exit.Add(ScriptStep.FreeHer());
            r.Beat("B2_Exit", "demo_b2_exit", Vector3.zero, Vector3.zero, exit.ToArray());
        }
    }
}
#endif
