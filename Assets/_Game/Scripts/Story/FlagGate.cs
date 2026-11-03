using UnityEngine;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Story
{
    /// <summary>
    /// Objects that a story flag shows or hides: a gate sealed until code silver ends, a QRF squad that comes after the
    /// alarm, a body that is there after a beat. Its children are active when the flag is up (showWhenRaised), or
    /// when it is down. It follows every change, and a load (StoryFlags.Set).
    /// </summary>
    public sealed class FlagGate : MonoBehaviour
    {
        public string flag = "";
        public bool showWhenRaised = true;
        [Tooltip("A group of figures in a room of the world scene: it changes only where she cannot see it, or as she enters its room (2026-11-02).")]
        public bool waitUnseen;

        public bool Showing { get; private set; }
        public bool Waiting { get; private set; }

        private GlorifiedBody.Rooms.Room room;

        private void Awake()
        {
            room = GetComponentInParent<GlorifiedBody.Rooms.Room>();
            Apply();
        }

        private void OnEnable()
        {
            StoryFlags.OnChanged += OnFlag;
            GlorifiedBody.Rooms.RoomTracker.OnChanged += OnRoomChanged;
        }

        private void OnDisable()
        {
            StoryFlags.OnChanged -= OnFlag;
            GlorifiedBody.Rooms.RoomTracker.OnChanged -= OnRoomChanged;
        }

        private void OnFlag(string changed)
        {
            if (changed != null && changed != flag) return;
            // A load (changed null) applies at once. In a room she sees (hers or next door) the figures do not pop up in view.
            if (changed != null && waitUnseen && room != null && GlorifiedBody.Rooms.RoomTracker.IsDrawn(room)) Waiting = true;
            else Apply();
        }

        private void OnRoomChanged(GlorifiedBody.Rooms.Room from, GlorifiedBody.Rooms.Room to)
        {
            if (Waiting && (to == room || !GlorifiedBody.Rooms.RoomTracker.IsDrawn(room))) Apply();
        }

        public void Apply()
        {
            Waiting = false;
            Showing = StoryFlags.Has(flag) == showWhenRaised;
            for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(Showing);
        }
    }
}
