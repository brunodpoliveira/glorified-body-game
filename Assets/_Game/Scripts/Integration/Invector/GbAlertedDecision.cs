using Invector.vCharacterController.AI.FSMBehaviour;
using GlorifiedBody.Enemy;

namespace GlorifiedBody.Integration.Invector
{
    /// <summary>
    /// True when the soldier is alerted (SoldierBrain.Alerted: it saw Lucy, took a hit, or heard an alert). It shows
    /// in Invector's FSM window as "Glorified Body/Is Alerted".
    /// </summary>
    public class GbAlertedDecision : vStateDecision
    {
        public override string categoryName => "Glorified Body/";
        public override string defaultName => "Is Alerted";

        public override bool Decide(vIFSMBehaviourController fsmBehaviour)
        {
            SoldierBrain brain = fsmBehaviour != null ? fsmBehaviour.gameObject.GetComponent<SoldierBrain>() : null;
            return brain != null && brain.Alerted;
        }
    }
}
