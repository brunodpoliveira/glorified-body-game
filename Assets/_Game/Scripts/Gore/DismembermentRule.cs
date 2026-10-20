using System.Collections.Generic;
using GlorifiedBody.Combat;
using UnityEngine;

namespace GlorifiedBody.Gore
{
    [CreateAssetMenu(menuName = "GlorifiedBody/Gore/Dismemberment Rule", fileName = "DA_DismembermentRule")]
    public sealed class DismembermentRule : ScriptableObject
    {
        [SerializeField] private GoreTier requiredTier = GoreTier.Full;
        [SerializeField, Min(0f)] private float minimumDamageAmount = 10f;
        [SerializeField] private bool fatalForPrototype = true;
        [SerializeField] private List<DamageType> allowedDamageTypes = new()
        {
            DamageType.PlayerBullet,
            DamageType.PlayerMelee,
            DamageType.TimeDilationMelee,
            DamageType.Divekick,
            DamageType.Slam,
            DamageType.SonicBoom,
            DamageType.Throw,
            DamageType.Grenade,
            DamageType.PropImpact,
            DamageType.FallingHazard
        };

        public GoreTier RequiredTier => requiredTier;
        public float MinimumDamageAmount => minimumDamageAmount;
        public bool FatalForPrototype => fatalForPrototype;

        public bool Allows(HitInfo hit, GoreTier currentTier, out string reason)
        {
            if (hit == null)
            {
                reason = "hit missing";
                return false;
            }

            if (currentTier != requiredTier)
            {
                reason = $"gore tier {currentTier} is not {requiredTier}";
                return false;
            }

            if (!allowedDamageTypes.Contains(hit.DamageType))
            {
                reason = $"damage type {hit.DamageType} not allowed";
                return false;
            }

            if (hit.DamageAmount < minimumDamageAmount)
            {
                reason = $"damage {hit.DamageAmount:0.#} below {minimumDamageAmount:0.#}";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
