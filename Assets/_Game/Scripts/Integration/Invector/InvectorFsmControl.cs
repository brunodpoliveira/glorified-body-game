using Invector.vCharacterController.AI.FSMBehaviour;
using UnityEngine;

namespace GlorifiedBody.Integration.Invector
{
    /// <summary>
    /// Pauses and resumes the Invector FSM on an enemy without touching Invector types outside
    /// this namespace. EnemyLogic pauses it while a rusher sprints at the player, so the cover
    /// FSM does not issue slower move commands between EnemyLogic's commands.
    /// </summary>
    public static class InvectorFsmControl
    {
        /// <summary>Stops the FSM. Returns true when this call stopped a running FSM.</summary>
        public static bool TryPause(GameObject enemy)
        {
            if (enemy == null || !enemy.TryGetComponent(out vFSMBehaviourController fsm) || fsm.isStopped)
            {
                return false;
            }

            fsm.isStopped = true;
            return true;
        }

        /// <summary>Starts the FSM again after <see cref="TryPause"/>.</summary>
        public static void Resume(GameObject enemy)
        {
            if (enemy != null && enemy.TryGetComponent(out vFSMBehaviourController fsm))
            {
                fsm.isStopped = false;
            }
        }
    }
}
