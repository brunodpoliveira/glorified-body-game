using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using GlorifiedBody.Audio;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Interaction;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Story
{
    /// <summary>
    /// The moment the site codes rotate (DEMO_PLAN §10.2, Bruno, 2026-11-08: "the code rotation puzzle is another
    /// backtracker; so the player needs to make progress by grabbing the card, using it to unlock some previously unlocked
    /// doors, then the code is rotated"). It counts the card doors that the card opened. At opensBeforeRotation it waits
    /// delaySeconds, plays the PA call and raises rotatedFlag: every card door not yet open refuses the card until a
    /// terminal enrols it (LockedDoor, CodeTerminal). An open door stays open. The count reads the doors, not a counter,
    /// so a load of a save gives the same answer. One for each card.
    /// </summary>
    public sealed class CodeRotation : MonoBehaviour
    {
        public string cardItemId = "";
        public string rotatedFlag = "";
        [Min(1)] public int opensBeforeRotation = 2;
        [Tooltip("The time from the last door that counts to the PA call: she is through the door when it speaks.")]
        [Min(0f)] public float delaySeconds = 2f;
        public string paId = "pa_1b_rotate";
        [TextArea] public string paLine = "Site codes rotate at this time. Re-enrol your card at a security station.";

        private List<LockedDoor> doors;
        private float dueAt = -1f;

        /// <summary>The card doors open now.</summary>
        public int Opened => Doors.Count(d => d != null && d.IsOpen);

        public bool Rotated => !string.IsNullOrEmpty(rotatedFlag) && StoryFlags.Has(rotatedFlag);

        /// <summary>The card doors of the world (all the rooms are loaded: one world).</summary>
        public IReadOnlyList<LockedDoor> Doors => doors ??= FindObjectsByType<LockedDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(d => d.keyItemId == cardItemId).ToList();

        private void Update()
        {
            if (Rotated || string.IsNullOrEmpty(cardItemId)) { dueAt = -1f; return; }
            if (dueAt < 0f)
            {
                if (Opened >= opensBeforeRotation) dueAt = Time.time + delaySeconds;
                return;
            }
            if (Time.time < dueAt) return;
            dueAt = -1f;
            Rotate();
        }

        /// <summary>The PA call and the flag (the tests, and the count).</summary>
        public void Rotate()
        {
            if (Rotated) return;
            StoryFlags.Raise(rotatedFlag);
            if (!string.IsNullOrEmpty(paLine)) Voice.PA(paId, paLine);
            RunTelemetry.Event("codes_rotate", $"{cardItemId} after {Opened}");
        }
    }
}
