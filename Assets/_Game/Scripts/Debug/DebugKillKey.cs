using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.Systems;

namespace GlorifiedBody.DebugTools
{
    /// <summary>
    /// Manual check of the death flow: one key kills the player. F-keys are reserved and never rebound.
    /// </summary>
    public sealed class DebugKillKey : MonoBehaviour
    {
        [SerializeField] private KeyCode key = KeyCode.F4;

        private void Update()
        {
            if (!Input.GetKeyDown(key)) return;
            GameManager gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying) return;

            GameObject player = GameObject.FindWithTag("Player");
            Health health = player != null ? player.GetComponentInChildren<Health>() : null;
            if (health == null || health.IsDead) return;

            health.SetSecondWindEnabled(false);
            health.SetRuntimeInvulnerable(false);
            health.TakeDamage(health.MaxHealth * 10f);
            Debug.Log($"[DebugKillKey] {key}: player killed.");
        }
    }
}
