using System;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Rendering;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Rooms
{
    /// <summary>
    /// A room whose lights go out (DEMO_PLAN §10.1, the dark room: "She enters G9. The lights go out in G9 and the
    /// closet."). On her first entry, or when its flag is up at the start (a load), the room goes dark for good: its
    /// floor, walls, props and the wall pieces on its lines leave the sun (Darkness). A figure inside it, Lucy too, is
    /// dark while it stays in the room. The monitors and their lights belong to the puzzle (UpsBank).
    /// </summary>
    [RequireComponent(typeof(Room))]
    public sealed class DarkRoom : MonoBehaviour
    {
        [Tooltip("The room goes dark when she first enters it.")]
        public bool darkOnEnter = true;
        [Tooltip("Raised when it goes dark; up at the start (a load), it starts dark.")]
        public string darkFlag = "";
        [Tooltip("Other rooms that go dark with this one (the closet behind the office).")]
        public Room[] alsoDark = new Room[0];

        public const float FigureCheckSeconds = 0.2f;

        public bool IsDark { get; private set; }
        public event Action OnDark;

        private Room room;
        private readonly List<Renderer> shell = new List<Renderer>();
        private readonly HashSet<GameObject> figuresInside = new HashSet<GameObject>();
        private float nextCheck;

        private void Awake() => room = GetComponent<Room>();

        private void OnEnable() => RoomTracker.OnChanged += OnRoomChanged;

        private void OnDisable() => RoomTracker.OnChanged -= OnRoomChanged;

        private void Start()
        {
            if (!string.IsNullOrEmpty(darkFlag) && StoryFlags.Has(darkFlag)) SetDark();
        }

        private void OnRoomChanged(Room from, Room to)
        {
            if (darkOnEnter && !IsDark && (to == room || Array.IndexOf(alsoDark, to) >= 0)) SetDark();
        }

        /// <summary>The lights go out (the tests call it too).</summary>
        public void SetDark()
        {
            if (IsDark) return;
            IsDark = true;
            var rooms = new List<Room> { room };
            foreach (Room other in alsoDark) if (other != null) rooms.Add(other);
            foreach (Room r in rooms)
            {
                foreach (string part in new[] { "[Room]", "[Props]" })
                {
                    Transform t = r.transform.Find(part);
                    if (t != null) shell.AddRange(t.GetComponentsInChildren<Renderer>(true));
                }
            }
            // The wall pieces on its lines that another room built (WallLedger).
            foreach (Room builder in Room.All)
            {
                if (rooms.Contains(builder)) continue;
                Transform t = builder.transform.Find("[Room]");
                if (t == null) continue;
                foreach (Renderer w in t.GetComponentsInChildren<Renderer>(true))
                    if ((w.name.StartsWith("Wall_") || w.name.StartsWith("Lintel_")) && rooms.Exists(r => r.Contains(w.bounds.center, 0.25f))) shell.Add(w);
            }
            foreach (Renderer r in shell) Darkness.Set(r, true);
            if (!string.IsNullOrEmpty(darkFlag)) StoryFlags.Raise(darkFlag);
            RunTelemetry.Event("room_dark", room.id);
            OnDark?.Invoke();
            nextCheck = 0f;
        }

        private bool Inside(Vector3 p)
        {
            if (room.Contains(p)) return true;
            foreach (Room other in alsoDark) if (other != null && other.Contains(p)) return true;
            return false;
        }

        private void Update()
        {
            if (!IsDark || Time.time < nextCheck) return;
            nextCheck = Time.time + FigureCheckSeconds;
            // Lucy and every figure with an animator: dark inside, lit outside.
            var now = new HashSet<GameObject>();
            GameObject lucy = GameObject.FindWithTag("Player");
            if (lucy != null && Inside(lucy.transform.position + Vector3.up * 0.1f)) now.Add(lucy);
            foreach (Animator a in FindObjectsByType<Animator>(FindObjectsSortMode.None))
            {
                if (lucy != null && a.transform.IsChildOf(lucy.transform)) continue; // her own model
                if (Inside(a.transform.position + Vector3.up * 0.1f)) now.Add(a.gameObject);
            }
            foreach (GameObject f in now) if (figuresInside.Add(f)) Darkness.Set(f, true);
            figuresInside.RemoveWhere(f =>
            {
                if (f != null && now.Contains(f)) return false;
                if (f != null) Darkness.Set(f, false);
                return true;
            });
        }

        /// <summary>Lucy or a figure is in the dark now (the tests).</summary>
        public bool HasInside(GameObject figure) => figuresInside.Contains(figure);
    }
}
