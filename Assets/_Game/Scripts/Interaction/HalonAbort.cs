namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// The EMERGENCY STOP on the halon panel inside G17 (DEMO_PLAN §10.4; round 142: a press button, the sign
    /// "HALON EMERGENCY STOP. PRESS TO STOP DISCHARGE."). It works only while the horn sounds.
    /// </summary>
    public sealed class HalonAbort : Interactable
    {
        public HalonSystem system;

        public override bool Available => base.Available && system != null && system.Current == HalonSystem.State.Horn;
        public override string Hint => "press EMERGENCY STOP"; // round 142: the button is the EMERGENCY STOP
        public override InteractKind KindFor(Interactor who) => InteractKind.Act;
        public override string TextFor(Interactor who) => "";
        public override string Confirm(Interactor who) => system != null && system.Abort() ? "halon_abort" : "halon_none";
    }
}
