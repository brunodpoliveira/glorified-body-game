using UnityEngine;

namespace GlorifiedBody.Gore
{
    /// <summary>
    /// Drives SkinGoreURP wounds from HitEventBus, gated by the current gore tier.
    /// SkinGoreURP (MIT, github.com/SuperWhimsy/SkinGoreURP) is imported at Assets/SkinGore.
    ///
    /// Setup:
    ///   1. Put a SkinGoreRenderer on each enemy (assign its skinned mesh + a decal material;
    ///      ideally give the mesh a non-overlapping UV2).
    ///   2. Add this component to a scene manager object.
    /// </summary>
    public sealed class SkinGoreDriver : MonoBehaviour
    {
        [Header("Wound Tuning")]
        [SerializeField] private float baseRadius = 0.12f;
        [SerializeField] private float baseStrength = 1f;
        [Tooltip("Damage that maps to a full-strength wound; smaller hits scale down.")]
        [SerializeField] private float damageForFullWound = 40f;

        private void OnEnable()
        {
            GlorifiedBody.Combat.HitEventBus.OnHit += HandleHit;
        }

        private void OnDisable()
        {
            GlorifiedBody.Combat.HitEventBus.OnHit -= HandleHit;
        }

        private void HandleHit(GlorifiedBody.Combat.HitInfo hit)
        {
            if (hit == null || hit.Victim == null)
            {
                return;
            }

            // Respect the gore tier: no wounds when gore is Off.
            if (GoreManager.Instance != null && GoreManager.Instance.CurrentTier == GoreTier.Off)
            {
                return;
            }

            float strength = baseStrength * Mathf.Clamp01(hit.DamageAmount / Mathf.Max(1f, damageForFullWound));
            if (strength <= 0f)
            {
                return;
            }

            ApplyWound(hit.Victim, hit.HitPoint, baseRadius, strength);
        }

        private void ApplyWound(GameObject victim, Vector3 worldPoint, float radius, float strength)
        {
            // SkinGoreRenderer lives in the imported SkinGoreURP asset (global namespace).
            foreach (global::SkinGoreRenderer renderer in victim.GetComponentsInChildren<global::SkinGoreRenderer>(true))
            {
                if (renderer != null)
                {
                    renderer.AddDamage(worldPoint, radius, strength);
                }
            }
        }
    }
}
