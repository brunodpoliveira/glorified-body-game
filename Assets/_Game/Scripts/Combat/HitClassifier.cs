using UnityEngine;
using GlorifiedBody.Core;

namespace GlorifiedBody.Combat
{
    /// <summary>
    /// Static utility for classifying hits as actor impacts vs. object impacts.
    /// Uses Health component and tags — no Invector dependency.
    /// </summary>
    public static class HitClassifier
    {
        public static bool IsActorHit(HitInfo hit)
        {
            return hit != null && IsActor(hit.Victim);
        }

        public static bool IsActor(GameObject candidate)
        {
            if (candidate == null) return false;

            if (candidate.CompareTag("Player") || candidate.CompareTag("Enemy"))
            {
                return true;
            }

            return candidate.GetComponentInChildren<Health>() != null
                || candidate.GetComponentInParent<Health>() != null;
        }

        public static bool IsObjectImpact(HitInfo hit)
        {
            if (hit == null || IsActorHit(hit)) return false;

            return hit.DamageType == DamageType.PlayerBullet
                || hit.DamageType == DamageType.EnemyBullet
                || hit.DamageType == DamageType.EnemyProjectile
                || hit.DamageType == DamageType.PlayerMelee
                || hit.DamageType == DamageType.EnemyMelee
                || hit.DamageType == DamageType.TimeDilationMelee
                || hit.DamageType == DamageType.Divekick
                || hit.DamageType == DamageType.Slam
                || hit.DamageType == DamageType.SonicBoom
                || hit.DamageType == DamageType.Throw
                || hit.DamageType == DamageType.Grenade
                || hit.DamageType == DamageType.PropImpact
                || hit.DamageType == DamageType.FallingHazard
                || hit.DamageType == DamageType.Explosion;
        }
    }
}
