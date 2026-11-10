using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Rooms;

namespace GlorifiedBody.Audio
{
    /// <summary>
    /// A room's own fight track (DEMO_PLAN §12, round 103, the Dining Hall: "It starts when she goes through the glass doors
    /// and the fireteam sees her. It ends with the last soldier, and the horror score comes back in the kitchen"). It starts
    /// when she is in the room and one of its figures is alerted. It ends when the last one dies, or when she leaves the
    /// room, and the room's music decides again (MusicDirector.Hold(null)).
    /// </summary>
    public sealed class RoomScore : MonoBehaviour
    {
        public string roomId = "";
        [Tooltip("The figures of the fight (their EnemySpawn markers become the figures under it at load).")]
        public GameObject figures;
        public AudioClip clip;
        public const string PlaceholderMusic = "Audio/Music/_placeholder/dining_hall";
        public const float CheckSeconds = 0.25f;

        public bool Playing { get; private set; }
        public bool Ended { get; private set; }

        private float nextCheck;

        private void Update()
        {
            if (Ended || Time.time < nextCheck) return;
            nextCheck = Time.time + CheckSeconds;
            bool here = RoomTracker.CurrentId == roomId;
            int alive = 0;
            bool alerted = false;
            if (figures != null)
                foreach (SoldierBrain brain in figures.GetComponentsInChildren<SoldierBrain>())
                {
                    Health health = brain.GetComponent<Health>();
                    if (health != null && health.IsDead) continue;
                    alive++;
                    if (brain.Alerted) alerted = true;
                }
            if (!Playing && here && alerted && alive > 0)
            {
                Playing = true;
                MusicDirector.Score(clip != null ? clip : Resources.Load<AudioClip>(PlaceholderMusic), fight: true);
                RunTelemetry.Event("room_score", roomId + ":start");
            }
            else if (Playing && (alive == 0 || !here))
            {
                Playing = false;
                Ended = alive == 0;
                MusicDirector.Hold(null);
                RunTelemetry.Event("room_score", roomId + (Ended ? ":the last one" : ":she left"));
            }
        }

        private void OnDestroy()
        {
            if (Playing) MusicDirector.Hold(null); // the scene goes in the fight: the music system lives on (BrainFight, 2026-11-09)
        }
    }
}
