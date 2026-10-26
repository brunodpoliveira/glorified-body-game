using GlorifiedBody.Interaction;
using GlorifiedBody.UI;
using UnityEngine;

namespace GlorifiedBody.Saving
{
    /// <summary>
    /// The save machine of a save room (Bruno, 2026-10-25: "only in save rooms"). Interact asks "Save your
    /// progress?"; a yes opens the slot menu (SaveMenuUI) over the stopped game. A load starts her in front of it.
    /// The machine's look is a placeholder.
    /// </summary>
    public sealed class SavePoint : Interactable, ISpot
    {
        public const float SpawnDistance = 1.0f;

        public string SpotId => name;

        public Pose SpawnPose
        {
            get
            {
                Vector3 front = transform.forward;
                front.y = 0f;
                if (front.sqrMagnitude < 0.01f) front = Vector3.forward;
                front.Normalize();
                return new Pose(transform.position + front * SpawnDistance, Quaternion.LookRotation(front, Vector3.up));
            }
        }

        public void MarkReached() { }

        public override string Hint => "save your progress";

        public override InteractKind KindFor(Interactor who) => InteractKind.Ask;

        public override string TextFor(Interactor who) => "Save your progress?";

        public override string Confirm(Interactor who)
        {
            SaveMenuUI.OpenSave(this);
            return "save_menu:" + SpotId;
        }
    }
}
