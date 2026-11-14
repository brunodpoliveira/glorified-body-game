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
    /// so a load of a save gives the same answer. One for each card. A try at the door of the way on rotates them at once
    /// (LockedDoor.rotatesOnTry), so the player cannot pass the puzzle by going there first.
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
        [Tooltip("The writer's call 1C (T5_pa.md, round 138), one PA line after another. Empty: paLine.")]
        public string[] paLines = new string[0];

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

        /// <summary>
        /// The codes rotate now (the door of the way on, LockedDoor.rotatesOnTry): the rotation of that card plays its PA
        /// call, or with none the flag goes up alone.
        /// </summary>
        public static void RotateNow(string cardItemId, string rotatedFlag)
        {
            foreach (CodeRotation r in FindObjectsByType<CodeRotation>(FindObjectsSortMode.None))
                if (r.cardItemId == cardItemId) { r.Rotate(); return; }
            StoryFlags.Raise(rotatedFlag);
        }

        /// <summary>The lines of the call, one after another (the calls of FireAlarm do the same).</summary>
        private System.Collections.IEnumerator Call()
        {
            for (int i = 0; i < paLines.Length; i++)
            {
                string id = $"{paId}_{i:00}";
                Voice.PA(id, paLines[i]);
                PaSaid++;
                yield return new WaitForSeconds(Voice.PaDelay + ScriptedSequence.LineSeconds(id, paLines[i]) + ScriptedSequence.LineGap);
            }
        }

        /// <summary>The PA lines said (the tests).</summary>
        public int PaSaid { get; private set; }

        /// <summary>The PA call and the flag (the tests, and the count).</summary>
        public void Rotate()
        {
            if (Rotated) return;
            StoryFlags.Raise(rotatedFlag);
            if (paLines != null && paLines.Length > 0) StartCoroutine(Call());
            else if (!string.IsNullOrEmpty(paLine)) Voice.PA(paId, paLine);
            RunTelemetry.Event("codes_rotate", $"{cardItemId} after {Opened}");
        }
    }
}
