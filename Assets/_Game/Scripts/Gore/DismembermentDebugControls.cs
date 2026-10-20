using GlorifiedBody.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GlorifiedBody.Gore
{
    public sealed class DismembermentDebugControls : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float nearestEnemyRadius = 1000f;

        private void Awake()
        {
            Debug.Log($"[DismembermentDebug] Active on {name}. Press PageDown or use menu GlorifiedBody/Phase 5/Debug Dismember Nearest Enemy.");
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.pageDownKey.wasPressedThisFrame)
            {
                Debug.Log("[DismembermentDebug] PageDown pressed.");
                TriggerNearestDismemberment(force: true);
            }
        }

        public bool TriggerNearestDismemberment(bool force)
        {
            DismembermentController controller = FindNearestController(out string findReason);
            if (controller == null)
            {
                Debug.Log($"[DismembermentDebug] Skipped: {findReason}.");
                return false;
            }

            Debug.Log($"[DismembermentDebug] Target={controller.name}. Forcing Gore Full and raising debug hit.");
            GoreManager.Instance?.SetTier(GoreTier.Full);
            GameObject attacker = GameObject.FindGameObjectWithTag("Player");
            if (!controller.TryBuildDebugHit(attacker != null ? attacker : gameObject, out HitInfo hit, out string reason, force))
            {
                Debug.Log($"[DismembermentDebug] Skipped on {controller.name}: {reason}.");
                return false;
            }

            HitEventBus.Raise(hit);
            Debug.Log($"[DismembermentDebug] Hit raised on {controller.name}. Zone={hit.HitZone}, Damage={hit.DamageAmount:0.#}, Force={force}.");
            return true;
        }

        private DismembermentController FindNearestController(out string reason)
        {
            DismembermentController[] controllers = FindObjectsByType<DismembermentController>(FindObjectsSortMode.None);
            if (controllers.Length == 0)
            {
                reason = "no controller found";
                return null;
            }

            Vector3 origin = GetPlayerOrigin();
            DismembermentController nearest = null;
            float nearestDistance = nearestEnemyRadius * nearestEnemyRadius;
            bool sawConfiguredController = false;

            foreach (DismembermentController controller in controllers)
            {
                if (controller == null || !controller.isActiveAndEnabled)
                {
                    continue;
                }

                if (!controller.HasConfiguredZone)
                {
                    continue;
                }

                sawConfiguredController = true;
                float distance = (controller.transform.position - origin).sqrMagnitude;
                if (distance <= nearestDistance)
                {
                    nearest = controller;
                    nearestDistance = distance;
                }
            }

            reason = sawConfiguredController ? $"no configured controller in range {nearestEnemyRadius:0.#}" : "no configured zone";
            return nearest;
        }

        private static Vector3 GetPlayerOrigin()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.transform.position : Vector3.zero;
        }
    }
}
