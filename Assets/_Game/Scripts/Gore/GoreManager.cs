using GlorifiedBody.Combat;
using UnityEngine;

namespace GlorifiedBody.Gore
{
    public sealed class GoreManager : MonoBehaviour
    {
        [SerializeField] private GoreSettings settings;
        [SerializeField] private BloodFeedbackController feedbackController;

        public static GoreManager Instance { get; private set; }
        public GoreSettings Settings => settings;
        public GoreTier CurrentTier => settings != null ? settings.CurrentGoreTier : GoreTier.Off;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Duplicate GoreManager on {name}; disabling this instance.", this);
                enabled = false;
                return;
            }

            Instance = this;

            if (settings != null)
            {
                settings = Instantiate(settings);
            }

            if (feedbackController == null)
            {
                feedbackController = GetComponentInChildren<BloodFeedbackController>();
            }
            feedbackController?.Configure(settings);
        }

        private void OnEnable()
        {
            HitEventBus.OnHit += HandleHit;
            Debug.Log($"[Gore] Enabled. Tier={CurrentTier}. Keys: F8 Off, F9 Low, F11 Full, F7 Clear, F12 ToggleVerbose, PageDown DebugDismember.");
        }

        private void OnDisable()
        {
            HitEventBus.OnHit -= HandleHit;
            Debug.Log("[Gore] Disabled. HitEventBus subscription removed.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SetTier(GoreTier tier)
        {
            if (settings == null)
            {
                Debug.LogWarning($"[Gore] Cannot set tier to {tier}: settings missing.", this);
                return;
            }

            settings.CurrentGoreTier = tier;
            feedbackController?.Configure(settings);
            if (tier == GoreTier.Off)
            {
                ClearEffects();
            }

            Debug.Log($"[Gore] Tier set to {tier}.");
        }

        public void CycleTier()
        {
            GoreTier next = CurrentTier == GoreTier.Off ? GoreTier.Low :
                CurrentTier == GoreTier.Low ? GoreTier.Full : GoreTier.Off;
            SetTier(next);
        }

        public void ClearEffects()
        {
            feedbackController?.ClearAll();
        }

        public void ToggleDebugLogging()
        {
            if (settings == null)
            {
                Debug.LogWarning("[Gore] Cannot toggle verbose logging: settings missing.", this);
                return;
            }

            settings.EnableDebugLogging = !settings.EnableDebugLogging;
            Debug.Log($"[Gore] Verbose hit logging {(settings.EnableDebugLogging ? "enabled" : "disabled")}.");
        }

        private void HandleHit(HitInfo hit)
        {
            if (hit == null || settings == null || settings.CurrentGoreTier == GoreTier.Off || !IsSupportedHit(hit))
            {
                if (settings != null && settings.EnableDebugLogging && hit != null)
                {
                    Debug.Log($"[Gore] Hit ignored. Tier={settings.CurrentGoreTier}, Type={hit.DamageType}, Victim={hit.Victim?.name ?? "null"}.");
                }
                return;
            }

            if (settings.EnableDebugLogging)
            {
                Debug.Log($"[Gore] Hit accepted. Type={hit.DamageType}, Damage={hit.DamageAmount:0.#}, Victim={hit.Victim?.name ?? "null"}, Point={hit.HitPoint}, Tier={settings.CurrentGoreTier}.");
            }

            feedbackController?.Play(hit, settings);
        }

        private static bool IsSupportedHit(HitInfo hit)
        {
            return hit.DamageType == DamageType.PlayerMelee
                || hit.DamageType == DamageType.EnemyMelee
                || hit.DamageType == DamageType.PlayerBullet
                || hit.DamageType == DamageType.EnemyBullet
                || hit.DamageType == DamageType.TimeDilationMelee
                || hit.DamageType == DamageType.Divekick
                || hit.DamageType == DamageType.Slam
                || hit.DamageType == DamageType.AudioPulse
                || hit.DamageType == DamageType.SonicBoom
                || hit.DamageType == DamageType.Throw
                || hit.DamageType == DamageType.Grenade
                || hit.DamageType == DamageType.EnemyProjectile
                || hit.DamageType == DamageType.PropImpact
                || hit.DamageType == DamageType.FallingHazard;
        }
    }
}
