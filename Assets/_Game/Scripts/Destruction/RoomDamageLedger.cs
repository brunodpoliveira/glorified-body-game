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
    /// session and clears the ledger. A save takes every room (Export), a load gives them back (Import). Keys: the scene name and the builder's id.
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

        // ── The save game ─────────────────────────────────────────────

        /// <summary>
        /// Every room as it is now, for a save: what the rooms kept, with this visit's damage on top (the visit
        /// counts at a save, as if she left the room).
        /// </summary>
        public static GlorifiedBody.Saving.LedgerState Export()
        {
            var state = new GlorifiedBody.Saving.LedgerState();
            var names = new HashSet<string>(committed.Keys);
            names.UnionWith(pending.Keys);
            foreach (string name in names)
            {
                var merged = new Room();
                if (committed.TryGetValue(name, out Room kept)) Merge(merged, kept);
                if (pending.TryGetValue(name, out Room visit)) Merge(merged, visit);
                var room = new GlorifiedBody.Saving.LedgerRoom { room = name };
                foreach (var pair in merged.breakables)
                {
                    var b = new GlorifiedBody.Saving.LedgerBreakable { id = pair.Key, broken = pair.Value.broken, health = pair.Value.health };
                    if (pair.Value.pieces != null)
                        foreach (Pose? p in pair.Value.pieces) b.pieces.Add(ToSaved("", p));
                    room.breakables.Add(b);
                }
                foreach (var pair in merged.greebles) room.greebles.Add(ToSaved(pair.Key, pair.Value));
                state.rooms.Add(room);
            }
            return state;
        }

        /// <summary>A load: the rooms become what the save kept, and no visit is open (the next scene load enters fresh).</summary>
        public static void Import(GlorifiedBody.Saving.LedgerState state)
        {
            committed.Clear();
            pending.Clear();
            current = null;
            if (state == null) return;
            foreach (GlorifiedBody.Saving.LedgerRoom saved in state.rooms)
            {
                Room room = Get(committed, saved.room);
                foreach (GlorifiedBody.Saving.LedgerBreakable b in saved.breakables)
                {
                    var record = new Record { broken = b.broken, health = b.health };
                    if (b.pieces.Count > 0)
                    {
                        record.pieces = new List<Pose?>();
                        foreach (GlorifiedBody.Saving.LedgerPose p in b.pieces) record.pieces.Add(p.has ? new Pose(p.position, p.rotation) : (Pose?)null);
                    }
                    room.breakables[b.id] = record;
                }
                foreach (GlorifiedBody.Saving.LedgerPose g in saved.greebles) room.greebles[g.id] = new Pose(g.position, g.rotation);
            }
        }

        private static void Merge(Room into, Room from)
        {
            foreach (var pair in from.breakables) into.breakables[pair.Key] = pair.Value;
            foreach (var pair in from.greebles) into.greebles[pair.Key] = pair.Value;
        }

        private static GlorifiedBody.Saving.LedgerPose ToSaved(string id, Pose? pose) => new GlorifiedBody.Saving.LedgerPose
        {
            id = id,
            has = pose.HasValue,
            position = pose.HasValue ? pose.Value.position : Vector3.zero,
            rotation = pose.HasValue ? pose.Value.rotation : Quaternion.identity,
        };
    }
}
