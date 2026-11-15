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
    /// A set piece cut by event (2026-11-15, the Dining Hall, Bruno: "Still B", "Yes, cut it"): when
    /// Resources/Audio/Music/_setpiece/&lt;room id&gt;/ has the files, the director plays them (MusicDirector.SetPiece): the
    /// entry on the next combat bar line, the fight loop, the armour (HeavyArrival cues it), and at the last one the end
    /// section, after which the normal states come back on its last bar line. With no files: the single track, as before.
    /// </summary>
    public sealed class RoomScore : MonoBehaviour
    {
        public string roomId = "";
        [Tooltip("The figures of the fight (their EnemySpawn markers become the figures under it at load).")]
        public GameObject figures;
        public AudioClip clip;
        public const string PlaceholderMusic = "Audio/Music/_placeholder/dining_hall";
        public const float CheckSeconds = 0.25f;
        [Tooltip("The tempo of the set piece files (the Dining Hall, T44 take B: 165.00 BPM).")]
        public float setPieceBpm = 165f;

        public bool Playing { get; private set; }
        public bool Ended { get; private set; }

        private float nextCheck;
        private SetPieceMusic kit;
        private bool kitLoaded;

        /// <summary>The set piece files of this room, or null (the single track then).</summary>
        public SetPieceMusic Kit
        {
            get
            {
                if (!kitLoaded) { kit = SetPieceMusic.Load(roomId, setPieceBpm); kitLoaded = true; }
                return kit;
            }
        }

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
                if (Kit != null) MusicDirector.SetPiece(Kit, ArmourCame());
                else MusicDirector.Score(clip != null ? clip : Resources.Load<AudioClip>(PlaceholderMusic), fight: true);
                RunTelemetry.Event("room_score", roomId + ":start");
            }
            else if (Playing && (alive == 0 || !here))
            {
                Playing = false;
                Ended = alive == 0;
                // The last one: the set piece plays its end section and hands back by itself. She left: it stops.
                if (Ended && Kit != null) MusicDirector.SetPieceCue(SetPieceEvent.End);
                else MusicDirector.Hold(null);
                RunTelemetry.Event("room_score", roomId + (Ended ? ":the last one" : ":she left"));
            }
        }

        /// <summary>The armour of this squad came before (a load after its arrival): the entry goes to the armour loop.</summary>
        private bool ArmourCame()
        {
            if (figures == null) return false;
            foreach (GlorifiedBody.Story.HeavyArrival a in FindObjectsByType<GlorifiedBody.Story.HeavyArrival>(FindObjectsSortMode.None))
                if (a.squad == figures.transform && (a.Arrived || GlorifiedBody.Saving.StoryFlags.Has(a.flag))) return true;
            return false;
        }

        private void OnDestroy()
        {
            if (Playing) MusicDirector.Hold(null); // the scene goes in the fight: the music system lives on (BrainFight, 2026-11-09)
        }
    }
}
