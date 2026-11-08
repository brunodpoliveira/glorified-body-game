using System;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Cameras;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Rooms
{
    /// <summary>
    /// Her room in the world scene (2026-11-02): every frame it finds the Room under her feet. A change raises
    /// OnChanged: the map, the music, the story beats of a room, the fate flags and the retry point listen. It keeps the
    /// rooms she was in (Visited, saved) and culls the drawing: the shell and the props of a room show only when the
    /// camera of the moment can see the room (FixedCamera.sees, from RoomSight), or when she is in it (Bruno, 2026-11-07:
    /// "cull rendering to what the player can see; this is easy due to the fixed camera angles"). A camera with no list
    /// shows her room and the rooms next door. A wall or a lintel shows when a room on its line shows. The colliders stay,
    /// so a soldier who chases her through two rooms walks on a floor. On the world root of the demo scene.
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
        private readonly List<(Renderer renderer, Room[] rooms)> walls = new List<(Renderer, Room[])>();
        private bool wallsFound;
        private FixedCamera viewCamera;
        private static readonly HashSet<string> shown = new HashSet<string>();

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
            // A cut to another camera shows other rooms.
            FixedCamera cut = FixedCameraDirector.Instance != null ? FixedCameraDirector.Instance.Current : null;
            if (cut != viewCamera && Current != null) Cull();
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

        /// <summary>The room is drawn now: the camera sees it, or she is in it.</summary>
        public static bool IsDrawn(Room room) => Current == null || shown.Contains(room.id);

        /// <summary>The ids of the rooms drawn now (the tests).</summary>
        public static IReadOnlyCollection<string> Shown => shown;

        private void Cull()
        {
            viewCamera = FixedCameraDirector.Instance != null ? FixedCameraDirector.Instance.Current : null;
            shown.Clear();
            shown.Add(Current.id);
            if (viewCamera != null && viewCamera.sees != null && viewCamera.sees.Length > 0) foreach (string id in viewCamera.sees) shown.Add(id);
            else foreach (string id in Current.neighbours) shown.Add(id);
            if (!wallsFound) FindWalls();
            foreach (Room room in Room.All)
            {
                if (!drawn.TryGetValue(room, out List<Renderer> list))
                {
                    list = new List<Renderer>();
                    foreach (string part in new[] { "[Room]", "[Props]" })
                    {
                        Transform t = room.transform.Find(part);
                        if (t == null) continue;
                        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                            if (!IsWall(r)) list.Add(r);
                    }
                    drawn[room] = list;
                }
                bool show = IsDrawn(room);
                foreach (Renderer r in list) Show(r, show);
            }
            foreach ((Renderer r, Room[] rooms) in walls)
            {
                bool show = rooms.Length == 0;
                foreach (Room room in rooms) show |= IsDrawn(room);
                Show(r, show);
            }
        }

        private void Show(Renderer r, bool show)
        {
            if (r == null) return;
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

        private static bool IsWall(Renderer r) => r.name.StartsWith("Wall_") || r.name.StartsWith("Lintel_");

        /// <summary>
        /// Two rooms share a wall line and one of them builds the piece (WallLedger, 2026-11-05: culled with its builder only,
        /// the piece left a hole in the other room). So a piece belongs to every room whose line it stands on.
        /// </summary>
        private void FindWalls()
        {
            wallsFound = true;
            const float margin = 0.25f;
            foreach (Room builder in Room.All)
            {
                Transform shell = builder.transform.Find("[Room]");
                if (shell == null) continue;
                foreach (Renderer r in shell.GetComponentsInChildren<Renderer>(true))
                {
                    if (!IsWall(r)) continue;
                    Bounds b = r.bounds;
                    var owners = new List<Room>();
                    foreach (Room room in Room.All)
                    {
                        Vector3 local = b.center - room.transform.position;
                        bool plan = local.x >= room.rect.x - margin && local.x <= room.rect.z + margin && local.z >= room.rect.y - margin && local.z <= room.rect.w + margin;
                        bool height = b.max.y > room.BaseY - room.below && b.min.y < room.BaseY + room.height + 0.5f;
                        if (plan && height) owners.Add(room);
                    }
                    if (!owners.Contains(builder)) owners.Add(builder);
                    walls.Add((r, owners.ToArray()));
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
            shown.Clear();
            Current = null;
            Changes = 0;
        }
    }
}
