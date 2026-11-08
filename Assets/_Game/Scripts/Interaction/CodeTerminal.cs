using UnityEngine;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A security terminal that enrols a card after the site codes rotate (DEMO_PLAN §10.2, the code rotation, Bruno's
    /// puzzle; CANON round 52: "the details stand as written"). Without the card it says PRESENT CARD. With the card, after
    /// the rotation, the Interact key opens the keypad (KeypadUI): one digit for each digit of the code (Bruno, 2026-11-08: 8675309). The right code raises enrolledFlag and the
    /// card door's reader turns green (LockedDoor). A wrong code costs nothing: CODE NOT RECOGNISED, try again at once
    /// (CANON round 40: no guard, no alarm, no lock). The words of the screens are PROPOSED.
    /// </summary>
    public sealed class CodeTerminal : Interactable
    {
        public string cardItemId = "";
        public string code = "8675309";
        public string rotatedFlag = "";
        public string enrolledFlag = "";

        public const string PresentText = "PRESENT CARD. ENTER SITE CODE.";
        public const string ValidText = "CARD VALID. NO ACTION REQUIRED.";
        public const string EnrolledText = "CARD ENROLLED.";
        public const string WrongText = "CODE NOT RECOGNISED. CARD NOT ENROLLED.";

        /// <summary>The codes entered here, right or wrong (the tests).</summary>
        public int Tries { get; private set; }

        public bool HasCard(Interactor who) => who != null && who.Inventory != null && who.Inventory.Has(cardItemId);
        public bool Rotated => string.IsNullOrEmpty(rotatedFlag) || StoryFlags.Has(rotatedFlag);
        public bool Enrolled => !string.IsNullOrEmpty(enrolledFlag) && StoryFlags.Has(enrolledFlag);

        public override string Hint => "use the terminal";

        public override InteractKind KindFor(Interactor who) => HasCard(who) && Rotated && !Enrolled ? InteractKind.Act : InteractKind.Message;

        public override string TextFor(Interactor who) => !HasCard(who) ? PresentText : Enrolled ? EnrolledText : !Rotated ? ValidText : "ENTER SITE CODE.";

        public override string Confirm(Interactor who)
        {
            if (KindFor(who) != InteractKind.Act) return "terminal:" + name;
            return GlorifiedBody.UI.KeypadUI.Open(this) ? "keypad:" + name : "keypad_failed:" + name;
        }

        /// <summary>A code from the keypad. True when it is the code: the card is enrolled.</summary>
        public bool TryCode(string digits)
        {
            Tries++;
            bool right = digits == code;
            RunTelemetry.Event("terminal_code", right ? "enrolled" : "wrong");
            if (right && !string.IsNullOrEmpty(enrolledFlag)) StoryFlags.Raise(enrolledFlag);
            GlorifiedBody.Audio.Sfx.Ui(right ? GlorifiedBody.Audio.Sfx.UiConfirm : GlorifiedBody.Audio.Sfx.Refuse); // the world sound waits while the keypad is open
            return right;
        }
    }
}
