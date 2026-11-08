using UnityEngine;
using GlorifiedBody.Audio;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Interaction;

namespace GlorifiedBody.Story
{
    /// <summary>
    /// A switch on a wall (2026-10-31, the demo: the sector C release in the security office, beat 23). Interact asks
    /// its question; a yes raises the story flag (the saves keep it), and a message says what it did. Once up, the
    /// switch is spent.
    /// </summary>
    public sealed class FlagSwitch : Interactable
    {
        public string flag = "";
        public string question = "Use the switch?";
        public string hint = "use the switch";
        public string doneText = "";
        [Tooltip("The speaker of the done line (\"PA\" for a call on the speakers). Empty: the subtitle's own label for no speaker.")]
        public string doneSpeaker = "";
        [Tooltip("It works only when she carries this item (the door of the car waits for the sunglasses). Empty: always.")]
        public string requiredItem = "";
        [Tooltip("It works only when this flag is up; before that it only shows waitingText (the release in G9: WAITING FOR NET). Empty: always.")]
        public string requiredFlag = "";
        public string waitingText = "";

        private bool Waiting => !string.IsNullOrEmpty(requiredFlag) && !GlorifiedBody.Saving.StoryFlags.Has(requiredFlag);

        private bool HasItem
        {
            get
            {
                if (string.IsNullOrEmpty(requiredItem)) return true;
                GameObject her = GameObject.FindWithTag("Player");
                ItemInventory items = her != null ? her.GetComponent<ItemInventory>() : null;
                return items != null && items.Has(requiredItem);
            }
        }

        public override bool Available => base.Available && !string.IsNullOrEmpty(flag) && !GlorifiedBody.Saving.StoryFlags.Has(flag) && HasItem;
        public override string Hint => hint;
        public override InteractKind KindFor(Interactor who) => Waiting ? InteractKind.Message : InteractKind.Ask;
        public override string TextFor(Interactor who) => Waiting ? waitingText : question;

        public override string Confirm(Interactor who)
        {
            if (Waiting) return "waiting:" + flag;
            GlorifiedBody.Saving.StoryFlags.Raise(flag);
            Sfx.Play(Sfx.DoorOpen, transform.position, 0.8f);
            if (!string.IsNullOrEmpty(doneText)) GlorifiedBody.Subtitles.SubtitleService.Say("switch", doneSpeaker, doneText, 4f);
            RunTelemetry.Event("switch", flag);
            return "switch:" + flag;
        }
    }
}
