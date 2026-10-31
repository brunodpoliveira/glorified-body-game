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

        public override bool Available => base.Available && !string.IsNullOrEmpty(flag) && !GlorifiedBody.Saving.StoryFlags.Has(flag);
        public override string Hint => hint;
        public override InteractKind KindFor(Interactor who) => InteractKind.Ask;
        public override string TextFor(Interactor who) => question;

        public override string Confirm(Interactor who)
        {
            GlorifiedBody.Saving.StoryFlags.Raise(flag);
            Sfx.Play(Sfx.DoorOpen, transform.position, 0.8f);
            if (!string.IsNullOrEmpty(doneText)) GlorifiedBody.Subtitles.SubtitleService.Say("switch", "", doneText, 4f);
            RunTelemetry.Event("switch", flag);
            return "switch:" + flag;
        }
    }
}
