using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// THE SHORT (design §2.8). When the SHORT bar is full, the next power starts a short: a migraine, and
    /// electric shocks fly everywhere out of her control. They do huge damage around her, and they take about
    /// half of her HP before she stabilises. A drug ends the short at once and refills her. Numbers are PROPOSED.
    /// Bruno, 2026-10-20: it lasts 16 s. She can walk, slowly: no run, no dodge, no powers (TankController.SlowWalk).
    /// The HP drain spreads over the 16 s, and each shock does the same damage to the others as before.
    /// </summary>
    [RequireComponent(typeof(PsychicStats))]
    public sealed class ShortController : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float duration = 16f;
        [SerializeField, Min(0.1f)] private float shockInterval = 0.4f;
        [SerializeField, Min(1f)] private float shockRadius = 6f;
        [SerializeField, Min(0f)] private float shockDamageToOthers = 12f;
        [SerializeField, Range(0f, 1f)] private float selfHpFraction = 0.5f;

        private PsychicStats stats;
        private Health health;
        private TankController tank; // optional: the slow walk during the short
        private readonly List<Health> seen = new List<Health>();
        private float pendingSelf; // Self damage that a dodge frame blocked. Lands at the next shock.

        public bool IsActive { get; private set; }
        public float Remaining { get; private set; }
        public int ShocksFired { get; private set; }
        public float Duration => duration;

        public event Action OnShortBegan;
        public event Action OnShortEnded;

        private void Awake()
        {
            stats = GetComponent<PsychicStats>();
            health = GetComponent<Health>();
            tank = GetComponent<TankController>();
        }

        private void OnDisable()
        {
            if (tank != null) tank.SetSlowWalk(false);
        }

        public void Begin()
        {
            if (IsActive || health.IsDead) return;
            IsActive = true;
            Remaining = duration;
            ShocksFired = 0;
            pendingSelf = 0f;
            stats.RegenPaused = true;
            if (tank != null) tank.SetSlowWalk(true);
            RunTelemetry.Event("short_begin", $"hp={health.CurrentHealth:0}");
            OnShortBegan?.Invoke();
            StartCoroutine(Run());
        }

        /// <summary>Ends the short. A drug ends it early and refills her (the caller refills).</summary>
        public void End(bool byDrug)
        {
            if (!IsActive) return;
            IsActive = false;
            Remaining = 0f;
            stats.RegenPaused = false;
            if (tank != null) tank.SetSlowWalk(false);
            stats.ResetShort();
            RunTelemetry.Event("short_end", byDrug ? "drug" : "timeout");
            OnShortEnded?.Invoke();
        }

        private IEnumerator Run()
        {
            int shocks = Mathf.Max(1, Mathf.RoundToInt(duration / shockInterval));
            float selfPerShock = health.MaxHealth * selfHpFraction / shocks;
            while (IsActive && Remaining > 0f)
            {
                Shock(selfPerShock);
                yield return new WaitForSeconds(shockInterval);
                Remaining -= shockInterval;
            }

            if (IsActive) End(byDrug: false);
        }

        private void Shock(float selfDamage)
        {
            ShocksFired++;
            Vector3 origin = transform.position;
            seen.Clear();
            foreach (Collider col in Physics.OverlapSphere(origin, shockRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                Health other = col.GetComponentInParent<Health>();
                if (other == null || other == health || other.IsDead || seen.Contains(other)) continue;
                seen.Add(other);
                other.TakeDamage(shockDamageToOthers);
                HitEventBus.Raise(new HitInfo
                {
                    Attacker = gameObject,
                    Victim = other.gameObject,
                    HitTransform = other.transform,
                    HitPoint = other.transform.position + Vector3.up,
                    HitNormal = (other.transform.position - origin).normalized,
                    DamageAmount = shockDamageToOthers,
                    DamageType = DamageType.ShortShock,
                    WasFatal = other.IsDead
                });
            }

            // The migraine: it never kills her by itself, it stops at 1 HP. Invulnerable frames (the second wind)
            // block a shock, so a shock that lands in them waits for the next one. The total stays about half her HP.
            if (health.IsInvulnerable)
            {
                pendingSelf += selfDamage;
            }
            else
            {
                float self = Mathf.Min(selfDamage + pendingSelf, health.CurrentHealth - 1f);
                pendingSelf = 0f;
                if (self > 0f) health.TakeDamage(self);
            }

            for (int i = 0; i < 3; i++) Spark(origin + UnityEngine.Random.insideUnitSphere * (shockRadius * 0.5f) + Vector3.up);
            RunTelemetry.Event("short_shock", $"{ShocksFired} hp={health.CurrentHealth:0} hit={seen.Count}");
        }

        private static void Spark(Vector3 at)
        {
            var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.name = "ShortSpark";
            var collider = spark.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            spark.transform.position = at;
            spark.transform.localScale = Vector3.one * 0.2f;
            var renderer = spark.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (renderer != null && shader != null) renderer.material = new Material(shader) { color = new Color(1f, 0.95f, 0.3f) };
            Destroy(spark, 0.25f);
        }
    }
}
