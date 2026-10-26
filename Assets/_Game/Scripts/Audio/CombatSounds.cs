using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;

namespace GlorifiedBody.Audio
{
    /// <summary>
    /// The hits on bodies (the audio sweep: a hit on flesh, her hurt, a fall were silent). Every hit of the hit bus on a
    /// figure: a flesh hit, or her hurt when it is Lucy. A body that dies falls with a thud a moment later. The
    /// short's shocks have their zap, and a burn's ticks sound at most every half second. Placeholders (Sfx).
    /// </summary>
    public static class CombatSounds
    {
        public const float SameVictimGap = 0.15f;
        public const float BurnGap = 0.5f;
        public const float FallDelay = 0.7f;

        private static readonly Dictionary<int, float> lastHit = new Dictionary<int, float>();

        public static void OnHit(HitInfo hit)
        {
            if (hit == null || hit.Victim == null) return;
            if (hit.DamageType == DamageType.ShortShock || hit.DamageType == DamageType.Debug) return;
            Health health = hit.Victim.GetComponentInParent<Health>();
            if (health == null) return;
            bool lucy = health.CompareTag("Player");
            if (!lucy && !health.CompareTag("Enemy")) return;
            LockOnTargetable target = health.GetComponent<LockOnTargetable>();
            if (target != null && target.IsProp) return;
            int id = health.GetInstanceID();
            float gap = hit.DamageType == DamageType.PsychicFire ? BurnGap : SameVictimGap;
            if (lastHit.TryGetValue(id, out float last) && Time.time - last < gap && !hit.WasFatal) return;
            lastHit[id] = Time.time;
            Vector3 at = hit.HitPoint != Vector3.zero ? hit.HitPoint : health.transform.position + Vector3.up * 1.2f;
            if (lucy) Sfx.Play(Sfx.LucyHurt, at, 0.9f);
            else Sfx.Play(Sfx.FleshHit, at, 0.8f);
            if (hit.WasFatal && health.IsDead && health.DeathFrame == Time.frameCount && AudioHost.Instance != null)
                AudioHost.Instance.StartCoroutine(Fall(health));
        }

        private static IEnumerator Fall(Health body)
        {
            yield return new WaitForSeconds(FallDelay);
            if (body != null) Sfx.Play(Sfx.BodyFall, body.transform.position + Vector3.up * 0.2f, 0.9f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => lastHit.Clear();
    }
}
