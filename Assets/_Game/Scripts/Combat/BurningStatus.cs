using UnityEngine;
using GlorifiedBody.Core;

namespace GlorifiedBody.Combat
{
    /// <summary>
    /// FIRE leaves this on the victim: damage over time in ticks, each tick a hit on the bus. A new burn
    /// refreshes the timer and keeps the stronger rate.
    /// </summary>
    public sealed class BurningStatus : MonoBehaviour
    {
        public const float TickSeconds = 0.5f;

        private Health health;
        private GameObject attacker;
        private float damagePerSecond;
        private float remaining;
        private float tickTimer;

        public bool IsBurning => remaining > 0f;
        public float Remaining => remaining;

        public static BurningStatus Ignite(GameObject victim, GameObject attacker, float damagePerSecond, float seconds)
        {
            if (victim == null) return null;
            BurningStatus burn = victim.GetComponent<BurningStatus>() ?? victim.AddComponent<BurningStatus>();
            burn.health = victim.GetComponent<Health>() ?? victim.GetComponentInParent<Health>();
            burn.attacker = attacker;
            burn.damagePerSecond = Mathf.Max(burn.IsBurning ? burn.damagePerSecond : 0f, damagePerSecond);
            burn.remaining = Mathf.Max(burn.remaining, seconds);
            return burn;
        }

        private void Update()
        {
            if (!IsBurning) return;
            float dt = Time.deltaTime;
            remaining -= dt;
            tickTimer += dt;
            if (tickTimer < TickSeconds) return;
            tickTimer -= TickSeconds;

            if (health == null || health.IsDead)
            {
                remaining = 0f;
                return;
            }

            float damage = damagePerSecond * TickSeconds;
            health.TakeDamage(damage);
            HitEventBus.Raise(new HitInfo
            {
                Attacker = attacker,
                Victim = health.gameObject,
                HitTransform = health.transform,
                HitPoint = health.transform.position + Vector3.up,
                HitNormal = Vector3.up,
                DamageAmount = damage,
                DamageType = DamageType.PsychicFire,
                WasFatal = health.IsDead
            });
        }
    }
}
