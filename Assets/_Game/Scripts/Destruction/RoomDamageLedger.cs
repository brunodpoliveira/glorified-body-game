using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GlorifiedBody.Destruction
{
    /// <summary>
    /// What broke in each room (Bruno, 2026-10-24: "broken room stays broken"). A visit writes its damage as
    /// pending. When she leaves for another room, the pending damage becomes the room's state, and the next visit
    /// starts from it: the broken pieces lie where they fell, the items where they settled. A restart of the same
    /// room (after a death) drops the pending damage: the room comes back as she found it. The main menu ends the
    /// session and clears the ledger. The save game reads it later. Keys: the scene name and the builder's id.
    /// </summary>
    public static class RoomDamageLedger
    {
        public sealed class Record
        {
            public bool broken;
            public float health;
            public List<Pose?> pieces;
        }

        private sealed class Room
        {
            public readonly Dictionary<string, Record> breakables = new Dictionary<string, Record>();
            public readonly Dictionary<string, Pose> greebles = new Dictionary<string, Pose>();
        }

        public const string MenuScene = "Main_Menu";

        private static readonly Dictionary<string, Room> committed = new Dictionary<string, Room>();
        private static readonly Dictionary<string, Room> pending = new Dictionary<string, Room>();
        private static string current;

        public static string CurrentRoom => current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            committed.Clear();
            pending.Clear();
            current = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Enter(scene.name);
        }

        /// <summary>A room loads (the scene loader calls it before any Start). Public for the EditMode tests.</summary>
        public static void Enter(string room)
        {
            if (room == MenuScene)
            {
                committed.Clear();
                pending.Clear();
            }
            else if (room == current)
            {
                pending.Remove(room); // a restart: as she found it
            }
            else if (current != null && pending.TryGetValue(current, out Room left))
            {
                Room state = Get(committed, current);
                foreach (var pair in left.breakables) state.breakables[pair.Key] = pair.Value;
                foreach (var pair in left.greebles) state.greebles[pair.Key] = pair.Value;
                pending.Remove(current);
            }
            current = room;
        }

        public static void Clear()
        {
            committed.Clear();
            pending.Clear();
        }

        public static int BrokenIn(string room)
        {
            int count = 0;
            if (committed.TryGetValue(room, out Room state))
                foreach (Record r in state.breakables.Values) if (r.broken) count++;
            return count;
        }

        private static Room Get(Dictionary<string, Room> set, string room)
        {
            if (!set.TryGetValue(room, out Room state))
            {
                state = new Room();
                set[room] = state;
            }
            return state;
        }

        private static string RoomOf(Component c) => c.gameObject.scene.name;

        private static Record Pending(Breakable b)
        {
            Room room = Get(pending, RoomOf(b));
            if (!room.breakables.TryGetValue(b.Id, out Record record))
            {
                record = new Record { health = b.MaxHealth };
                room.breakables[b.Id] = record;
            }
            return record;
        }

        /// <summary>The room's state when she entered (what the last visits left).</summary>
        public static Record Find(Breakable b)
        {
            return committed.TryGetValue(RoomOf(b), out Room room) && room.breakables.TryGetValue(b.Id, out Record record) ? record : null;
        }

        public static bool FindGreeble(Greeble g, out Pose pose)
        {
            pose = default;
            return committed.TryGetValue(RoomOf(g), out Room room) && room.greebles.TryGetValue(g.Id, out pose);
        }

        public static void NoteHealth(Breakable b) => Pending(b).health = b.Health;

        public static void NoteBroken(Breakable b)
        {
            Record record = Pending(b);
            record.broken = true;
            record.health = 0f;
            record.pieces ??= new List<Pose?>();
            while (record.pieces.Count < b.Pieces.Count) record.pieces.Add(null);
        }

        public static void NotePiece(Breakable b, int index, Pose pose)
        {
            Record record = Pending(b);
            record.pieces ??= new List<Pose?>();
            while (record.pieces.Count <= index) record.pieces.Add(null);
            record.pieces[index] = pose;
        }

        public static void NoteGreeble(Greeble g, Pose pose) => Get(pending, RoomOf(g)).greebles[g.Id] = pose;
    }
}
