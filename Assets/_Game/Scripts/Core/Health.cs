using System;
using UnityEngine;
using UnityEngine.Events;

namespace GlorifiedBody.Core
{
    /// <summary>
    /// Generic health component for any entity.
    /// Handles damage, healing, death, and events.
    /// </summary>
    public class Health : MonoBehaviour
    {
        // ── Static events ────────────────────────────────────────────────────
        /// <summary>
        /// Fired when a Health component on a GameObject tagged "Enemy" becomes active.
        /// Allows systems like L3KillRefund to subscribe to future spawns without polling.
        /// </summary>
        public static event Action<Health> OnEnemyHealthCreated;

        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;
        [SerializeField] private bool destroyOnDeath = true;
        [SerializeField] private float destroyDelay = 0f;
        [SerializeField] private bool invulnerable = false;
        [SerializeField] private bool isPlayerHealth = false;
        private bool runtimeInvulnerable = false;

        [Header("Second Wind")]
        [SerializeField] private bool enableSecondWind = false;
        // Unscaled seconds from the triggering hit; must span the Second Wind
        // freeze + recovery ramp (0.8 + 0.4) plus the live reposition window.
        [SerializeField] private float secondWindInvulnerabilityDuration = 3.2f;
        private bool secondWindAvailable = true;
        private bool secondWindActive = false;
        private float secondWindInvulnerableUntil = -1f;

        [Header("Events")]
        public UnityEvent<float> OnDamaged;
        public UnityEvent<float> OnHealed;
        public UnityEvent OnDeath;
        public UnityEvent OnSecondWind;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float HealthPercent => currentHealth / maxHealth;
        public bool IsDead { get; private set; }
        /// <summary>The frame of the death: a "fatal" hit on a body dead before that frame is not the kill.</summary>
        public int DeathFrame { get; private set; } = -1;
        public bool IsInvulnerable => invulnerable || runtimeInvulnerable || IsSecondWindInvulnerable;
        public bool IsSecondWindEnabled => enableSecondWind;
        public bool IsSecondWindAvailable => secondWindAvailable;
        public bool IsSecondWindActive => secondWindActive;
        // Game time (2026-10-26): a pause or the short's freeze must not use up the 2.5 s (they ran on real time).
        public bool IsSecondWindInvulnerable => secondWindInvulnerableUntil > Time.time;
        public float SecondWindInvulnerabilityRemaining => Mathf.Max(0f, secondWindInvulnerableUntil - Time.time);

        void Awake()
        {
            currentHealth = maxHealth;
            IsDead = false;
            secondWindAvailable = enableSecondWind;
            secondWindActive = false;

            if (CompareTag("Enemy"))
            {
                OnEnemyHealthCreated?.Invoke(this);
            }
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || IsInvulnerable) return;

            currentHealth -= amount;
            currentHealth = Mathf.Max(currentHealth, 0);

            OnDamaged?.Invoke(amount);

            if (currentHealth <= 0)
            {
                if (enableSecondWind && secondWindAvailable)
                {
                    currentHealth = 1f;
                    secondWindAvailable = false;
                    secondWindActive = true;
                    secondWindInvulnerableUntil = Time.time + secondWindInvulnerabilityDuration;
                    OnSecondWind?.Invoke();
                    return;
                }

                Die();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;

            currentHealth += amount;
            currentHealth = Mathf.Min(currentHealth, maxHealth);

            OnHealed?.Invoke(amount);
        }

        public void SetHealth(float amount)
        {
            currentHealth = Mathf.Clamp(amount, 0, maxHealth);
            if (currentHealth <= 0 && !IsDead)
            {
                Die();
            }
        }

        public void SetMaxHealth(float amount, bool fillToMax = true)
        {
            maxHealth = Mathf.Max(1f, amount);
            currentHealth = fillToMax ? maxHealth : Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        /// <summary>Full HP again (a drug). The second wind stays used: only RearmSecondWind gives it back.</summary>
        public void ReviveToFull()
        {
            IsDead = false;
            currentHealth = maxHealth;
            secondWindActive = false;
            secondWindInvulnerableUntil = -1f;
            runtimeInvulnerable = false;
            OnHealed?.Invoke(maxHealth);
        }

        /// <summary>The second wind pill (Bruno, 2026-10-20, a rare item, coded later): one more second wind.</summary>
        public void RearmSecondWind()
        {
            if (!enableSecondWind) return;
            secondWindAvailable = true;
        }

        private void Die()
        {
            if (IsDead) return;

            secondWindActive = false;
            secondWindInvulnerableUntil = -1f;
            IsDead = true;
            DeathFrame = Time.frameCount;
            OnDeath?.Invoke();

            bool shouldDestroy = destroyOnDeath && !isPlayerHealth;
            if (shouldDestroy)
            {
                Destroy(gameObject, destroyDelay);
            }
        }

        public bool IsCritical(float threshold = 0.25f)
        {
            return HealthPercent <= threshold;
        }

        public void SetInvulnerable(bool value)
        {
            invulnerable = value;
        }

        public void SetRuntimeInvulnerable(bool value)
        {
            runtimeInvulnerable = value;
        }

        public void SetSecondWindEnabled(bool value)
        {
            enableSecondWind = value;
            if (value && !IsDead)
                secondWindAvailable = true;
            else if (!value)
                ClearSecondWindActive();
        }

        public void SetSecondWindInvulnerabilityDuration(float value)
        {
            secondWindInvulnerabilityDuration = Mathf.Max(0f, value);
        }

        public void ClearSecondWindActive()
        {
            secondWindActive = false;
            secondWindInvulnerableUntil = -1f;
        }
    }
}
