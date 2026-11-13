using UnityEngine;
using GlorifiedBody.Audio;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// The upright restraint of the cell H1 (round 118, CANON: "the player starts restrained and must press the interact
    /// button repeatedly to set themselves free"; round 119, CANON: the drain line in the neck empties every MK bar, the
    /// earthed wire cage over the Crown grounds it). The wake sequence holds her in it (StepKind.Restraint): her feet
    /// stop and every MK bar goes to zero. Each Interact press pulls against the straps; the straps give one by one
    /// (the writer's cutscene 01, "Breaking free": 12 presses, no time limit, PROPOSED). At the last press the line tears
    /// out, the cage falls, the free flag and the lockdown flag go up (the Hold seals by protocol, DEMO_PLAN §5.0).
    /// </summary>
    public sealed class UprightRestraint : Interactable
    {
        public const int PressesNeeded = 12; // PROPOSED (cutscene 01, version 3)

        public string freeFlag = "hold_free";
        public string lockdownFlag = "hold_lockdown";
        [Tooltip("The straps, in the order they give (the wrists, the chest, the hips).")]
        public GameObject[] straps = new GameObject[0];
        public GameObject drainLine;
        public Transform cage;
        [Tooltip("Where the cage lies after it falls.")]
        public Vector3 cageFallen;

        public bool Holding { get; private set; }
        public bool Broken { get; private set; }
        public int Presses { get; private set; }

        private TankController tank;

        public override int Priority => 10;
        public override bool Available => base.Available && Holding;
        public override string Hint => "pull against the straps";
        public override InteractKind KindFor(Interactor who) => InteractKind.Act;
        public override string TextFor(Interactor who) => "";

        private void Start()
        {
            if (StoryFlags.Has(freeFlag)) ShowBroken();
        }

        /// <summary>She is strapped in: her feet stop and the drain line empties every MK bar.</summary>
        public void Hold(GameObject her)
        {
            if (Holding || Broken || her == null) return;
            tank = her.GetComponent<TankController>();
            tank?.LockMovement(true);
            her.GetComponent<PsychicStats>()?.EmptyMk();
            Holding = true;
            Presses = 0;
            RunTelemetry.Event("restraint_hold", name);
        }

        public override string Confirm(Interactor who)
        {
            if (!Holding) return "restraint:free";
            Presses++;
            int given = Mathf.Min(straps.Length, Presses * straps.Length / PressesNeeded);
            for (int i = 0; i < straps.Length; i++) if (straps[i] != null) straps[i].SetActive(i >= given);
            Sfx.Play(Sfx.DoorLocked, Point + Vector3.up * 0.4f, 0.35f + 0.03f * Presses); // the frame creaks (placeholder)
            RunTelemetry.Event("restraint_pull", Presses.ToString());
            if (Presses >= PressesNeeded) Break();
            return "restraint:" + Presses;
        }

        /// <summary>The last press: the line tears out, the cage falls, she steps down, the Hold seals.</summary>
        public void Break()
        {
            if (Broken) return;
            Holding = false;
            Broken = true;
            ShowBroken();
            tank?.LockMovement(false);
            Sfx.Play(Sfx.DoorClose, Point + Vector3.up * 1.6f, 1f); // the cage rings on the floor (placeholder)
            StoryFlags.Raise(freeFlag);
            if (!string.IsNullOrEmpty(lockdownFlag)) StoryFlags.Raise(lockdownFlag);
            RunTelemetry.Event("restraint_broken", name);
        }

        /// <summary>Tests and bots: every restraint that holds her lets go, as after the last press.</summary>
        public static void FreeAll()
        {
            foreach (UprightRestraint r in FindObjectsByType<UprightRestraint>(FindObjectsSortMode.None))
                if (r.Holding) r.ForceBreak();
        }

        /// <summary>Tests and bots: she is free at once, as after the last press.</summary>
        public void ForceBreak()
        {
            while (Holding && Presses < PressesNeeded - 1) Presses++;
            Break();
        }

        private void ShowBroken()
        {
            Broken = true;
            foreach (GameObject strap in straps) if (strap != null) strap.SetActive(false);
            if (drainLine != null) drainLine.SetActive(false);
            if (cage != null) cage.SetPositionAndRotation(cageFallen, Quaternion.Euler(0f, 20f, 80f));
        }
    }
}
