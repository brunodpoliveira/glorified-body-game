using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Rooms
{
    /// <summary>
    /// A room of the world scene (2026-11-02, Bruno: "i want all the map to be built and loaded up in one go, we just
    /// cull rendering"). The demo is one scene: each room is a root with this component, its id ("W1"), its title, its
    /// map floor, its plan rectangle (local to the root, in metres) and the rooms behind its doorways. The registry
    /// finds the room of a point. A scene with no Room (a test room) has no rooms: the callers then fall back to the
    /// scene as the room.
    /// </summary>
    public sealed class Room : MonoBehaviour
    {
        public string id = "";
        public string title = "";
        public string floor = "";
        [Tooltip("The plan rectangle, local to this root: x0, z0, x1, z1 (metres).")]
        public Vector4 rect;
        [Min(0.5f)] public float height = 3f;
        [Tooltip("How far under the root the floor goes (a stair: its foot), metres.")]
        [Min(0f)] public float below = 1.5f;
        [Tooltip("The rooms behind its doorways (their ids).")]
        public string[] neighbours = new string[0];

        /// <summary>A way out of the room: the middle of its doorway on the floor (local to the root), the way out, its width.</summary>
        [System.Serializable]
        public struct Exit
        {
            public Vector3 mouth;
            public Vector3 outward;
            public float width;
            public string to;
        }

        [Tooltip("Its ways out. The camera of the floor in front of each one shows it (Bruno, 2026-11-05: \"i also need to see the exits\").")]
        public Exit[] exits = new Exit[0];

        private static readonly List<Room> all = new List<Room>();
        private static readonly Dictionary<string, Room> byId = new Dictionary<string, Room>();

        public static IReadOnlyList<Room> All => all;
        /// <summary>True when the scene has rooms (the world scene of the demo).</summary>
        public static bool Any => all.Count > 0;

        public static Room Of(string roomId) => roomId != null && byId.TryGetValue(roomId, out Room r) ? r : null;

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
            if (!string.IsNullOrEmpty(id)) byId[id] = this;
        }

        private void OnDisable()
        {
            all.Remove(this);
            if (!string.IsNullOrEmpty(id) && byId.TryGetValue(id, out Room r) && r == this) byId.Remove(id);
        }

        /// <summary>The floor's height in the world (the root's y).</summary>
        public float BaseY => transform.position.y;

        /// <summary>The middle of the floor, in the world.</summary>
        public Vector3 Centre => transform.position + new Vector3((rect.x + rect.z) * 0.5f, 0f, (rect.y + rect.w) * 0.5f);

        /// <summary>The point is over this room's floor, within its height (a little under the floor counts: a slope, a step).</summary>
        public bool Contains(Vector3 p, float margin = 0f)
        {
            Vector3 local = p - transform.position;
            return local.x >= rect.x - margin && local.x <= rect.z + margin && local.z >= rect.y - margin && local.z <= rect.w + margin
                && local.y >= -below && local.y <= height + 0.5f;
        }

        public bool IsNeighbour(string other)
        {
            foreach (string n in neighbours) if (n == other) return true;
            return false;
        }

        /// <summary>The room of the point, or null. Ties at a doorway go to the first room in the list.</summary>
        public static Room At(Vector3 p)
        {
            foreach (Room r in all) if (r.Contains(p)) return r;
            return null;
        }

        /// <summary>
        /// Two points can hear each other: the same room, or rooms that share a doorway (Bruno, 2026-11-02: enemies chase
        /// her through the doors, so a shot is heard next door). Outside the world scene (no rooms) the answer is yes, and
        /// the callers' radius decides.
        /// </summary>
        public static bool Hear(Vector3 a, Vector3 b)
        {
            if (!Any) return true;
            Room ra = At(a), rb = At(b);
            if (ra == null || rb == null) return true;
            return ra == rb || ra.IsNeighbour(rb.id);
        }

        /// <summary>The same room (or no rooms in the scene). A witness who only hears, a hider's whisper, a carer's search.</summary>
        public static bool Same(Vector3 a, Vector3 b)
        {
            if (!Any) return true;
            Room ra = At(a), rb = At(b);
            return ra == null || rb == null || ra == rb;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            all.Clear();
            byId.Clear();
        }
    }
}
