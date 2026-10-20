using Invector.vCharacterController.AI.FSMBehaviour;
using UnityEngine;

namespace GlorifiedBody.Integration.Invector
{
    /// <summary>
    /// Restores the missing script of the "SafeDistance?" decision node in Invector's
    /// FSM_ShooterCover asset. The original script is not in the project, so the editor logged
    /// "The referenced script (Unknown) on this Behaviour is missing!" each time it loaded the FSM.
    /// No transition uses this node, so it never runs. The meta file keeps the old GUID, and the
    /// fields match the serialized node data. We restore the script instead of removing the node
    /// because the FSM asset is vendor data.
    /// </summary>
    public class FsmSafeDistanceDecision : vStateDecision
    {
        public override string categoryName => "Detection/";
        public override string defaultName => "Safe Distance (unused)";

        [Tooltip("Unused. Kept so the serialized node data loads.")]
        public int checkDistance;

        [Tooltip("Unused. Kept so the serialized node data loads.")]
        public float value = 10f;

        public override bool Decide(vIFSMBehaviourController fsmBehaviour)
        {
            return false;
        }
    }
}
