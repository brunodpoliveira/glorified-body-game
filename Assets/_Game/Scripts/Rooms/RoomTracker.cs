using System;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Rooms
{
    /// <summary>
    /// Her room in the world scene (2026-11-02): every frame it finds the Room under her feet. A change raises
    /// OnChanged: the map, the music, the story beats of a room, the fate flags and the retry point listen. It keeps the
    /// rooms she was in (Visited, saved) and culls the drawing: the shell and the props of a room show only when she is
    /// in it or next door (a doorway shows the next room). The colliders stay, so a soldier who chases her through two
    /// rooms walks on a floor. On the world root of the demo scene.
    /// </summary>
    public sealed class RoomTracker : MonoBehaviour
    {
        public static RoomTracker Instance { get; private set; }
        /// <summary>From, to (from is null at the start).</summary>
        public static event Action<Room, Room> OnChanged;

        private static readonly HashSet<string> visited = new HashSet<string>();
        public static IReadOnlyCollection<string> Visited => visited;
        public static bool WasIn(string roomId) => visited.Contains(roomId);

        public static Room Current { get; private set; }
        public static string CurrentId => Current != null ? Current.id : "";
        public static int Changes { get; private set; }

        private Transform her;
        private readonly Dictionary<Room, List<Renderer>> drawn = new Dictionary<Room, List<Renderer>>();
        private readonly HashSet<Renderer> culled = new HashSet<Renderer>();

        private void Awake()
        {
            Instance = this;
            Current = null;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (her == null)
            {
                GameObject lucy = GameObject.FindWithTag("Player");
                if (lucy == null) return;
                her = lucy.transform;
            }
            Vector3 p = her.position + Vector3.up * 0.1f;
            if (Current != null && Current.Contains(p)) return; // a doorway belongs to the room she came from until she is through
            Room now = Room.At(p);
            if (now == null || now == Current) return;
            Enter(now);
        }

        /// <summary>Makes the room hers now (a load puts her in a room; the tests). Raises OnChanged.</summary>
        public void Enter(Room room)
        {
            Room from = Current;
            Current = room;
            visited.Add(room.id);
            Changes++;
            Cull();
            RunTelemetry.Event("room", (from != null ? from.id : "-") + " -> " + room.id);
            OnChanged?.Invoke(from, room);
        }

        /// <summary>Puts the visited rooms of a save back (the map shows them).</summary>
        public static void SetVisited(IEnumerable<string> rooms)
        {
            visited.Clear();
            if (rooms != null) foreach (string r in rooms) if (!string.IsNullOrEmpty(r)) visited.Add(r);
        }

        /// <summary>The room is drawn now: hers, or next door.</summary>
        public static bool IsDrawn(Room room) => Current == null || room == Current || Current.IsNeighbour(room.id);

        private void Cull()
        {
            foreach (Room room in Room.All)
            {
                if (!drawn.TryGetValue(room, out List<Renderer> list))
                {
                    list = new List<Renderer>();
                    foreach (string part in new[] { "[Room]", "[Props]" })
                    {
                        Transform t = room.transform.Find(part);
                        if (t == null) continue;
                        // The walls and lintels stay drawn (2026-11-05): two rooms share a wall line and one of them builds the
                        // piece (WallLedger). Culled with its builder, the piece left a hole in the other room's wall.
                        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                            if (!r.name.StartsWith("Wall_") && !r.name.StartsWith("Lintel_")) list.Add(r);
                    }
                    drawn[room] = list;
                }
                bool show = IsDrawn(room);
                foreach (Renderer r in list)
                {
                    if (r == null) continue;
                    if (show)
                    {
                        // Only what the cull turned off comes back: a renderer another system hid stays hidden.
                        if (culled.Remove(r)) r.enabled = true;
                    }
                    else if (r.enabled)
                    {
                        r.enabled = false;
                        culled.Add(r);
                    }
                }
            }
        }

        /// <summary>Renderers the cull turns off now (the tests).</summary>
        public int CulledCount => culled.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            OnChanged = null;
            visited.Clear();
            Current = null;
            Changes = 0;
        }
    }
}
