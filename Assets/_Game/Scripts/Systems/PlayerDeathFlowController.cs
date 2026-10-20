using System.Collections;
using System;
using UnityEngine;
using UnityEngine.Events;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Systems
{
    public sealed class PlayerDeathFlowController : MonoBehaviour
    {
        [SerializeField] private Health playerHealth;
        [SerializeField] private ImpactFrameController impactFrameController;
        [SerializeField, Min(0f)] private float freezeDuration = 0.12f;
        [SerializeField, Min(0f)] private float recoveryDuration = 0.35f;
        [SerializeField, Min(0f)] private float gameOverDelay = 0.45f;

        public static event Action OnPlayerDeathBeat;
        private Coroutine deathRoutine;
        private Health subscribedHealth;

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Update()
        {
            Bind();
        }

        private void Bind()
        {
            if (playerHealth == null)
            {
                // The player is the active object tagged Player. Its Health may sit on a child.
                GameObject playerObject = GameObject.FindWithTag("Player");
                playerHealth = playerObject != null ? playerObject.GetComponentInChildren<Health>() : null;
            }

            if (playerHealth == subscribedHealth)
            {
                return;
            }

            Unbind();
            subscribedHealth = playerHealth;
            if (subscribedHealth == null)
            {
                return;
            }

            subscribedHealth.OnDeath ??= new UnityEvent();
            subscribedHealth.OnDeath.AddListener(HandlePlayerDeath);
        }

        private void Unbind()
        {
            if (subscribedHealth == null)
            {
                return;
            }

            subscribedHealth.OnDeath?.RemoveListener(HandlePlayerDeath);
            subscribedHealth = null;
        }

        private void HandlePlayerDeath()
        {
            RunTelemetry.Event("player_died", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            if (deathRoutine != null)
            {
                return;
            }

            if (impactFrameController == null)
            {
                impactFrameController = ImpactFrameController.Instance ?? FindFirstObjectByType<ImpactFrameController>();
            }

            OnPlayerDeathBeat?.Invoke();
            Debug.Log("[DeathFlow] Player death beat triggered.");
            impactFrameController?.RequestImpactFrame(freezeDuration, recoveryDuration);
            deathRoutine = StartCoroutine(GameOverAfterBeat());
        }

        private IEnumerator GameOverAfterBeat()
        {
            float endAt = Time.unscaledTime + gameOverDelay;
            while (Time.unscaledTime < endAt)
            {
                yield return null;
            }

            GameManager.Instance?.TriggerGameOver();
            deathRoutine = null;
        }
    }
}
