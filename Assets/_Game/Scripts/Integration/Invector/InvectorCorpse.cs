using Invector.vCharacterController;
using Invector.vCharacterController.AI;
using UnityEngine;

namespace GlorifiedBody.Integration.Invector
{
    /// <summary>
    /// Corpse handling for Invector AI. Dead enemies only: live enemies keep vControlAI and the
    /// FSM enabled (see CLAUDE.md, Invector Policy).
    /// </summary>
    public static class InvectorCorpse
    {
        /// <summary>
        /// Turns off vAIMotor.RemoveComponents. That strip runs when the death animation ends and
        /// destroys the root capsule, rigidbody, animator and every MonoBehaviour. Our components
        /// require some of them, so part of the destroys fail, and vAIMotor.Update then reads the
        /// destroyed capsule every frame (MissingReferenceException).
        /// </summary>
        public static void DisableComponentStripping(GameObject enemy)
        {
            if (enemy != null && enemy.TryGetComponent(out vControlAI ai))
            {
                ai.removeComponentsAfterDie = false;
            }
        }

        /// <summary>
        /// True when vRagdoll drives the body. Checks vRagdoll.state too, because
        /// vControlAI.ragdolled clears after senselessTime while the body still lies in ragdoll.
        /// </summary>
        public static bool IsRagdolled(GameObject enemy)
        {
            if (enemy == null)
            {
                return false;
            }

            if (enemy.TryGetComponent(out vRagdoll ragdoll) && ragdoll.isActive && ragdoll.state == vRagdoll.RagdollState.ragdolled)
            {
                return true;
            }

            return enemy.TryGetComponent(out vControlAI ai) && ai.ragdolled;
        }

        /// <summary>True when the vControlAI motor component is enabled.</summary>
        public static bool IsMotorEnabled(GameObject enemy)
        {
            return enemy != null && enemy.TryGetComponent(out vControlAI ai) && ai.enabled;
        }

        /// <summary>
        /// Disables the AI motor and FSM on a dead enemy. vControlAIShooter derives from
        /// vControlAI, so the shooter controller stops too. Returns true when the corpse was
        /// ragdolled at that moment.
        /// </summary>
        public static bool ShutdownMotor(GameObject enemy)
        {
            if (enemy == null)
            {
                return false;
            }

            if (enemy.TryGetComponent(out global::Invector.vCharacterController.AI.FSMBehaviour.vFSMBehaviourController fsm))
            {
                fsm.enabled = false;
            }

            if (!enemy.TryGetComponent(out vControlAI ai))
            {
                return false;
            }

            bool ragdolled = IsRagdolled(enemy);
            ai.enabled = false;
            return ragdolled;
        }
    }
}
