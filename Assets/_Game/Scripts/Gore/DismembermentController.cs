using System.Collections.Generic;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using UnityEngine;

namespace GlorifiedBody.Gore
{
    public sealed class DismembermentController : MonoBehaviour
    {
        [SerializeField] private GoreManager goreManager;
        [SerializeField] private Health health;
        [SerializeField] private MonoBehaviour enemyTargetSource;
        [SerializeField] private DismembermentCleanup cleanup;
        [SerializeField] private List<DismembermentZone> zones = new();
        [SerializeField] private bool logDebug;

        private IEnemyTarget enemyTarget;
        private bool fallbackHeadDismembered;
        public bool HasConfiguredZone => GetFirstAvailableZone() != null || zones.Count > 0;

        private void Awake()
        {
            CacheReferences();
            RefreshZones();
        }

        private void OnEnable()
        {
            CacheReferences();
            RefreshZones();
            HitEventBus.OnHit += HandleHit;
            Debug.Log($"[Dismemberment] Enabled on {name}. Zones={zones.Count}, HasAvailableZone={HasConfiguredZone}, Health={(health != null ? health.name : "missing")}.");
        }

        private void OnDisable()
        {
            HitEventBus.OnHit -= HandleHit;
            Debug.Log($"[Dismemberment] Disabled on {name}.");
        }

        public bool TryBuildDebugHit(GameObject attacker, out HitInfo hit, out string reason, bool force = false)
        {
            CacheReferences();
            RefreshZones();

            DismembermentZone zone = GetFirstAvailableZone();
            if (zone == null)
            {
                hit = null;
                reason = zones.Count == 0 ? "no configured zone" : "zone already dismembered";
                Debug.Log($"[Dismemberment] Debug hit build failed on {name}: {reason}.");
                return false;
            }

            DismembermentRule rule = zone.Rule;
            if (rule == null)
            {
                hit = null;
                reason = "rule missing";
                Debug.Log($"[Dismemberment] Debug hit build failed on {name}: {reason} for zone {zone.ZoneId}.");
                return false;
            }

            float damage = Mathf.Max(rule.MinimumDamageAmount, 0f);
            Transform socket = zone.LimbSocket;
            Vector3 hitPoint = socket != null ? socket.position : transform.position + Vector3.up;
            Vector3 normal = attacker != null && (hitPoint - attacker.transform.position).sqrMagnitude > 0.0001f
                ? (hitPoint - attacker.transform.position).normalized
                : transform.forward;

            hit = new HitInfo
            {
                Attacker = attacker,
                Victim = gameObject,
                HitCollider = zone.GetComponent<Collider>(),
                HitTransform = zone.transform,
                HitPoint = hitPoint,
                HitNormal = normal,
                DamageAmount = damage,
                DamageType = DamageType.PlayerBullet,
                WasFatal = rule.FatalForPrototype,
                HitZone = zone.ZoneId
            };

            GoreTier tier = GetCurrentTier();
            if (!force && !rule.Allows(hit, tier, out reason))
            {
                hit = null;
                Debug.Log($"[Dismemberment] Debug hit build blocked on {name}: {reason}.");
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void HandleHit(HitInfo hit)
        {
            if (hit == null || !IsHitForThisEnemy(hit))
            {
                return;
            }

            DismembermentZone zone = ResolveZoneForHit(hit);
            if (zone == null)
            {
                if (TryExecuteFatalSlamHeadFallback(hit))
                {
                    return;
                }

                Log($"Dismember skipped on {name}: no matching zone for hit zone '{hit.HitZone}' transform '{hit.HitTransform?.name ?? "null"}'.");
                return;
            }

            if (zone.IsDismembered)
            {
                Log($"Dismember skipped on {name}: {zone.ZoneId} already dismembered.");
                return;
            }

            DismembermentRule rule = zone.Rule;
            if (rule == null)
            {
                Log($"Dismember skipped on {name}: rule missing for {zone.ZoneId}.");
                return;
            }

            GoreTier tier = GetCurrentTier();
            if (!rule.Allows(hit, tier, out string reason))
            {
                Log($"Dismember skipped on {name}: {reason}.");
                return;
            }

            if (rule.FatalForPrototype && !CanRouteFatalDismemberment(hit))
            {
                Log($"Dismember skipped on {name}: fatal dismemberment requires a fatal hit. Type={hit.DamageType}, Damage={hit.DamageAmount:0.#}.");
                return;
            }

            ExecuteDismemberment(zone, hit, rule);
        }

        private DismembermentZone ResolveZoneForHit(HitInfo hit)
        {
            if (IsFatalSlamHit(hit))
            {
                DismembermentZone headZone = GetAvailableZoneById("Head");
                if (headZone != null)
                {
                    Debug.Log($"[Dismemberment] Fatal slam selected Head zone on {name}.");
                    return headZone;
                }

                if (!HasZoneById("Head") && GetFirstAvailableZone() != null)
                {
                    Debug.Log($"[Dismemberment] Fatal slam fallback will use available zone assets for Head on {name}; Head zone missing.");
                    return null;
                }
            }

            return ResolveZone(hit);
        }

        private void ExecuteDismemberment(DismembermentZone zone, HitInfo hit, DismembermentRule rule)
        {
            CacheReferences();
            zone.MarkDismembered();
            SpawnDetachedLimb(zone, hit);
            SpawnWoundMarker(zone, hit);

            Log($"Dismembered {zone.ZoneId} on {name}.");
            Debug.Log($"[Dismemberment] Dismembered {zone.ZoneId} on {name}. Fatal={rule.FatalForPrototype}, DetachedPrefab={(zone.DetachedLimbPrefab != null ? zone.DetachedLimbPrefab.name : "missing")}, WoundPrefab={(zone.WoundMarkerPrefab != null ? zone.WoundMarkerPrefab.name : "missing")}.");

            if (rule.FatalForPrototype && CanRouteFatalDismemberment(hit))
            {
                RouteFatalDismemberment();
            }
        }

        private bool TryExecuteFatalSlamHeadFallback(HitInfo hit)
        {
            if (!IsFatalSlamHit(hit) || fallbackHeadDismembered || HasZoneById("Head"))
            {
                return false;
            }

            DismembermentZone templateZone = ResolveZone(hit) ?? GetFirstAvailableZone();
            if (templateZone == null)
            {
                return false;
            }

            DismembermentRule rule = templateZone.Rule;
            if (rule == null)
            {
                return false;
            }

            GoreTier tier = GetCurrentTier();
            if (!rule.Allows(hit, tier, out string reason))
            {
                Log($"Dismember skipped on {name}: fatal slam head fallback blocked: {reason}.");
                return false;
            }

            fallbackHeadDismembered = true;
            Vector3 position = ResolveFallbackHeadPosition(hit);
            Quaternion rotation = transform.rotation;
            SpawnDetachedLimbAt(templateZone, hit, position, rotation);
            SpawnWoundMarkerAt(templateZone, position, rotation, null);

            Debug.Log($"[Dismemberment] Dismembered Head on {name} using fallback assets from {templateZone.ZoneId}. Add HeadZone by running Phase 5 setup for authored placement.");

            if (rule.FatalForPrototype && CanRouteFatalDismemberment(hit))
            {
                RouteFatalDismemberment();
            }

            return true;
        }

        private void RouteFatalDismemberment()
        {
            CacheReferences();

            Health targetHealth = enemyTarget != null ? enemyTarget.Health : health;
            if (targetHealth != null && !targetHealth.IsDead)
            {
                targetHealth.TakeDamage(Mathf.Max(targetHealth.CurrentHealth, targetHealth.MaxHealth));
            }
        }

        private void SpawnDetachedLimb(DismembermentZone zone, HitInfo hit)
        {
            if (zone.DetachedLimbPrefab == null)
            {
                return;
            }

            Transform socket = zone.LimbSocket;
            Vector3 position = socket != null ? socket.position : hit.HitPoint;
            Quaternion rotation = socket != null ? socket.rotation : transform.rotation;
            GameObject instance = Instantiate(zone.DetachedLimbPrefab, position, rotation);
            cleanup?.Track(instance);

            DetachedLimb detachedLimb = instance.GetComponent<DetachedLimb>();
            detachedLimb?.Initialize(hit, zone.CleanupLifetime, cleanup);
        }

        private void SpawnDetachedLimbAt(DismembermentZone zone, HitInfo hit, Vector3 position, Quaternion rotation)
        {
            if (zone.DetachedLimbPrefab == null)
            {
                return;
            }

            GameObject instance = Instantiate(zone.DetachedLimbPrefab, position, rotation);
            cleanup?.Track(instance);

            DetachedLimb detachedLimb = instance.GetComponent<DetachedLimb>();
            detachedLimb?.Initialize(hit, zone.CleanupLifetime, cleanup);
        }

        private void SpawnWoundMarker(DismembermentZone zone, HitInfo hit)
        {
            if (zone.WoundMarkerPrefab == null)
            {
                return;
            }

            Transform socket = zone.LimbSocket;
            Vector3 position = socket != null ? socket.position : hit.HitPoint;
            Quaternion rotation = socket != null ? socket.rotation : transform.rotation;
            GameObject instance = Instantiate(zone.WoundMarkerPrefab, position, rotation);
            cleanup?.Track(instance);

            WoundMarker woundMarker = instance.GetComponent<WoundMarker>();
            woundMarker?.Initialize(socket, zone.CleanupLifetime, cleanup);
        }

        private void SpawnWoundMarkerAt(DismembermentZone zone, Vector3 position, Quaternion rotation, Transform attachTo)
        {
            if (zone.WoundMarkerPrefab == null)
            {
                return;
            }

            GameObject instance = Instantiate(zone.WoundMarkerPrefab, position, rotation);
            cleanup?.Track(instance);

            WoundMarker woundMarker = instance.GetComponent<WoundMarker>();
            woundMarker?.Initialize(attachTo, zone.CleanupLifetime, cleanup);
        }

        private DismembermentZone ResolveZone(HitInfo hit)
        {
            foreach (DismembermentZone zone in zones)
            {
                if (zone != null && zone.Matches(hit))
                {
                    return zone;
                }
            }

            return null;
        }

        private DismembermentZone GetFirstAvailableZone()
        {
            RefreshZones();
            foreach (DismembermentZone zone in zones)
            {
                if (zone != null && !zone.IsDismembered)
                {
                    return zone;
                }
            }

            return null;
        }

        private DismembermentZone GetAvailableZoneById(string zoneId)
        {
            RefreshZones();
            foreach (DismembermentZone zone in zones)
            {
                if (zone != null
                    && !zone.IsDismembered
                    && string.Equals(zone.ZoneId, zoneId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return zone;
                }
            }

            return null;
        }

        private bool HasZoneById(string zoneId)
        {
            RefreshZones();
            foreach (DismembermentZone zone in zones)
            {
                if (zone != null && string.Equals(zone.ZoneId, zoneId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsFatalSlamHit(HitInfo hit)
        {
            if (hit == null || hit.DamageType != DamageType.Slam)
            {
                return false;
            }

            Health targetHealth = enemyTarget != null ? enemyTarget.Health : health;
            return hit.WasFatal || (targetHealth != null && targetHealth.IsDead);
        }

        private static bool CanRouteFatalDismemberment(HitInfo hit)
        {
            return hit != null && (hit.WasFatal || hit.DamageType == DamageType.Debug);
        }

        private Vector3 ResolveFallbackHeadPosition(HitInfo hit)
        {
            if (hit != null && !string.IsNullOrWhiteSpace(hit.HitZone) && hit.HitZone.Equals("Head", System.StringComparison.OrdinalIgnoreCase))
            {
                return hit.HitPoint.sqrMagnitude > 0.001f ? hit.HitPoint : transform.position + Vector3.up * 1.75f;
            }

            return transform.position + Vector3.up * 1.75f;
        }

        private bool IsHitForThisEnemy(HitInfo hit)
        {
            if (hit.Victim == gameObject || hit.HitTransform == transform)
            {
                return true;
            }

            Transform hitTransform = hit.HitTransform != null ? hit.HitTransform : hit.HitCollider != null ? hit.HitCollider.transform : null;
            if (hitTransform != null && hitTransform.IsChildOf(transform))
            {
                return true;
            }

            Transform root = enemyTarget != null ? enemyTarget.Root : null;
            return root != null && (hit.Victim == root.gameObject || hitTransform == root);
        }

        private GoreTier GetCurrentTier()
        {
            goreManager ??= GoreManager.Instance;

            return goreManager != null ? goreManager.CurrentTier : GoreTier.Off;
        }

        private void CacheReferences()
        {
            goreManager ??= GoreManager.Instance;

            if (enemyTarget == null)
            {
                enemyTarget = ResolveEnemyTarget();
            }

            if (health == null)
            {
                health = enemyTarget != null ? enemyTarget.Health : GetComponentInParent<Health>();
            }

            cleanup ??= GetComponent<DismembermentCleanup>();
            if (cleanup == null)
            {
                cleanup = gameObject.AddComponent<DismembermentCleanup>();
            }
        }

        private IEnemyTarget ResolveEnemyTarget()
        {
            foreach (MonoBehaviour behaviour in GetComponentsInParent<MonoBehaviour>(true))
            {
                if (behaviour is IEnemyTarget target)
                {
                    enemyTargetSource = behaviour;
                    return target;
                }
            }

            return null;
        }

        private void RefreshZones()
        {
            zones.RemoveAll(zone => zone == null);
            foreach (DismembermentZone zone in GetComponentsInChildren<DismembermentZone>(true))
            {
                if (!zones.Contains(zone))
                {
                    zones.Add(zone);
                }
            }
        }

        private void Log(string message)
        {
            if (logDebug)
            {
                Debug.Log(message);
            }
        }
    }
}
