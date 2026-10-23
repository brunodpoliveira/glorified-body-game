using Invector.vCharacterController.AI.FSMBehaviour;
using GlorifiedBody.Enemy;

namespace GlorifiedBody.Integration.Invector
{
    /// <summary>
    /// The fight state of the soldier FSM: SoldierBrain runs "move or shoot, never both" while the state is on.
    /// It shows in Invector's FSM window as "Glorified Body/Move Or Shoot". Its execution type must hold
    /// OnStateUpdate and OnStateExit (the factory sets both).
    /// </summary>
    public class GbEngageAction : vStateAction
    {
        public override string categoryName => "Glorified Body/";
        public override string defaultName => "Move Or Shoot";

        public override void DoAction(vIFSMBehaviourController fsmBehaviour, vFSMComponentExecutionType executionType = vFSMComponentExecutionType.OnStateUpdate)
        {
            if (fsmBehaviour == null) return;
            SoldierBrain brain = fsmBehaviour.gameObject.GetComponent<SoldierBrain>();
            if (brain == null) return;
            if (executionType == vFSMComponentExecutionType.OnStateExit) brain.Disengage();
            else brain.TickEngage();
        }
    }
}
