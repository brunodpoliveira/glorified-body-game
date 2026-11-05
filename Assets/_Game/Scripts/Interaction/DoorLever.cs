using UnityEngine;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// The lever of a fire door (2026-11-03, Bruno: "a lever that closes+ locks/jams the door shut"). One on each side of the
    /// doorway. Her Interact on an open door's lever shuts and jams the door (not with a body in the doorway); on a jammed
    /// door's lever it opens the door again (Bruno: "yes"). The enemies never pull it: a jammed fire door stays shut for them
    /// ("that's why guards can't open them even if they engage the lever on the other side"). The handle shows the state:
    /// up open, down jammed.
    /// </summary>
    public sealed class DoorLever : Interactable
    {
        public FireDoor door;
        [Tooltip("The handle that turns: up when the door is open, down when it is jammed.")]
        public Transform handle;
        public float upAngle = 25f, downAngle = 155f; // about the wall's horizontal: up and out, down and out

        public override bool Available => base.Available && door != null && door.Current != FireDoor.State.Broken
            && (string.IsNullOrEmpty(door.leverFlag) || GlorifiedBody.Saving.StoryFlags.Has(door.leverFlag));

        public override string Hint => door != null && door.Current == FireDoor.State.Jammed ? "open the fire door" : "jam the fire door";

        public override InteractKind KindFor(Interactor who) => Refused() ? InteractKind.Message : InteractKind.Act;

        public override string TextFor(Interactor who) => Refused() ? "Something is in the way." : "";

        public override string Confirm(Interactor who)
        {
            if (door == null) return "none:" + name;
            if (door.Current == FireDoor.State.Jammed)
            {
                door.Open("lucy");
                return "opened:" + door.name;
            }
            return door.Jam("lucy") ? "jammed:" + door.name : "blocked:" + door.name;
        }

        private bool Refused() => door != null && door.Current == FireDoor.State.Open && door.Blocked();

        private void LateUpdate()
        {
            if (handle == null || door == null) return;
            float angle = door.Current == FireDoor.State.Open ? upAngle : downAngle;
            handle.localRotation = Quaternion.Slerp(handle.localRotation, Quaternion.Euler(angle, 0f, 0f), 1f - Mathf.Exp(-Time.deltaTime * 14f));
        }
    }
}
