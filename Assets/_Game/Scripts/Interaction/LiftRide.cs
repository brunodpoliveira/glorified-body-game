using System.Collections;
using UnityEngine;
using GlorifiedBody.Audio;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// The lift of the foyer (rounds 147 and 148: the locked place of the art puzzle, and the shortcut): the Interact key in
    /// the car or at the landing, with no question, rides to the other end: black, she stands at the object named
    /// destination, the picture comes back. It works only while the flag is up (the sorted plinths release the lift from the
    /// lockdown). Before that the key shows the lockdown message. Like a Dark Souls lift, it takes her back up to the ward.
    /// </summary>
    public sealed class LiftRide : Interactable
    {
        [Tooltip("The object she stands at after the ride (by name: the two ends are on two floors of the world).")]
        public string destination = "";
        public string requiredFlag = "";
        [Tooltip("The words of the hint: \"take the lift up\".")]
        public string thing = "the lift";
        [TextArea] public string lockedText = "The lift does not answer. A screen over the button: LOCKDOWN.";

        /// <summary>The rides (the tests).</summary>
        public int Rides { get; private set; }

        private bool Open => string.IsNullOrEmpty(requiredFlag) || StoryFlags.Has(requiredFlag);

        public override string Hint => "take " + thing;
        public override InteractKind KindFor(Interactor who) => Open ? InteractKind.Act : InteractKind.Message;
        public override string TextFor(Interactor who) => Open ? "" : lockedText;

        public override string Confirm(Interactor who)
        {
            if (!Open) return "lift_locked:" + name;
            GameObject place = GameObject.Find(destination);
            TankController tank = who != null ? who.GetComponent<TankController>() : null;
            if (place == null || tank == null) return "lift_none:" + name;
            StartCoroutine(Ride(tank, place.transform));
            return "lift:" + name;
        }

        private IEnumerator Ride(TankController tank, Transform place)
        {
            Rides++;
            Sfx.Play(Sfx.DoorClose, transform.position, 0.8f);
            GlorifiedBody.UI.ScreenFade.Cover();
            tank.Teleport(place.position, place.eulerAngles.y);
            RunTelemetry.Event("lift_ride", $"{name}>{destination}");
            yield return new WaitForSeconds(0.6f);
            Sfx.Play(Sfx.DoorOpen, place.position, 0.8f);
            GlorifiedBody.UI.ScreenFade.In(0.6f);
        }
    }
}
