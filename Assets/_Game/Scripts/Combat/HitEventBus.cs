using System;
using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Combat
{
    /// <summary>
    /// Static event bus for all confirmed combat hits.
    /// Every hit raises OnHit exactly once per unique hit per frame (duplicate suppression).
    /// No visual system subscribes to Health.OnDamaged directly — all feedback routes through here.
    /// </summary>
    public static class HitEventBus
    {
        public static event Action<HitInfo> OnHit;

        private const int MaxRecentHits = 64;
        private static readonly List<RecentHit> RecentHits = new();

        public static void Raise(HitInfo hit)
        {
            if (hit == null || IsDuplicate(hit, 1))
            {
                return;
            }

            Remember(hit);
            OnHit?.Invoke(hit);
        }

        public static bool WasSimilarHitRaised(HitInfo hit, int frameWindow = 1, bool ignoreDamageType = false)
        {
            return hit != null && IsDuplicate(hit, Mathf.Max(0, frameWindow), ignoreDamageType);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Clears the deduplication cache. For use in EditMode tests only.
        /// </summary>
        public static void ResetForTesting() => RecentHits.Clear();
#endif

        private static bool IsDuplicate(HitInfo hit, int frameWindow, bool ignoreDamageType = false)
        {
            int frame = Time.frameCount;
            for (int i = RecentHits.Count - 1; i >= 0; i--)
            {
                RecentHit recent = RecentHits[i];
                if (frame - recent.Frame > frameWindow)
                {
                    continue;
                }

                if (recent.Matches(hit, ignoreDamageType))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Remember(HitInfo hit)
        {
            RecentHits.Add(new RecentHit(hit, Time.frameCount));
            if (RecentHits.Count > MaxRecentHits)
            {
                RecentHits.RemoveAt(0);
            }
        }

        private readonly struct RecentHit
        {
            private readonly GameObject attacker;
            private readonly GameObject victim;
            private readonly DamageType damageType;
            private readonly float damageAmount;
            private readonly Vector3 hitPoint;

            public readonly int Frame;

            public RecentHit(HitInfo hit, int frame)
            {
                attacker = hit.Attacker;
                victim = hit.Victim;
                damageType = hit.DamageType;
                damageAmount = hit.DamageAmount;
                hitPoint = hit.HitPoint;
                Frame = frame;
            }

            public bool Matches(HitInfo hit, bool ignoreDamageType)
            {
                return attacker == hit.Attacker
                    && victim == hit.Victim
                    && (ignoreDamageType || damageType == hit.DamageType)
                    && Mathf.Approximately(damageAmount, hit.DamageAmount)
                    && Vector3.SqrMagnitude(hitPoint - hit.HitPoint) <= 0.0004f;
            }
        }
    }
}
