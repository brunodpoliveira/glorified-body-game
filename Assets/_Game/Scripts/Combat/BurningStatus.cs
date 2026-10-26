using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Core;

namespace GlorifiedBody.Combat
{
    /// <summary>
    /// FIRE leaves this on the victim: damage over time in ticks, each tick a hit on the bus. A new burn
    /// refreshes the timer and keeps the stronger rate. Placeholder look (Bruno, 2026-10-20, so the burn is
    /// visible by eye): the victim turns orange and small flames rise off it. The final effect comes with the art.
    /// </summary>
    public sealed class BurningStatus : MonoBehaviour
    {
        public const float TickSeconds = 0.5f;
        private const float FlameInterval = 0.12f;
        private static readonly Color BurnTint = new Color(1f, 0.45f, 0.1f);

        private Health health;
        private GameObject attacker;
        private float damagePerSecond;
        private float remaining;
        private float tickTimer;
        private float flameTimer;
        private readonly List<Renderer> tinted = new List<Renderer>();
        private readonly List<Color> originalColors = new List<Color>();

        public bool IsBurning => remaining > 0f;
        public float Remaining => remaining;
        /// <summary>True while the placeholder tint is on the victim (tests and bots read it).</summary>
        public bool IsTinted => tinted.Count > 0;

        public static BurningStatus Ignite(GameObject victim, GameObject attacker, float damagePerSecond, float seconds)
        {
            if (victim == null) return null;
            BurningStatus burn = victim.GetComponent<BurningStatus>() ?? victim.AddComponent<BurningStatus>();
            burn.health = victim.GetComponent<Health>() ?? victim.GetComponentInParent<Health>();
            burn.attacker = attacker;
            burn.damagePerSecond = Mathf.Max(burn.IsBurning ? burn.damagePerSecond : 0f, damagePerSecond);
            burn.remaining = Mathf.Max(burn.remaining, seconds);
            burn.ApplyTint();
            // The flames crackle while it burns (placeholder, 2026-10-26).
            if (burn.crackle == null) burn.crackle = GlorifiedBody.Audio.Sfx.Loop(GlorifiedBody.Audio.Sfx.BurnLoop, victim, 0.5f);
            else if (!burn.crackle.isPlaying) burn.crackle.Play();
            return burn;
        }

        private AudioSource crackle;

        private void Update()
        {
            if (!IsBurning)
            {
                if (crackle != null && crackle.isPlaying) crackle.Stop();
                return;
            }
            float dt = Time.deltaTime;
            remaining -= dt;
            tickTimer += dt;
            flameTimer += dt;
            if (flameTimer >= FlameInterval)
            {
                flameTimer -= FlameInterval;
                Flame();
            }
            if (remaining <= 0f)
            {
                RemoveTint();
                return;
            }
            if (tickTimer < TickSeconds) return;
            tickTimer -= TickSeconds;

            if (health == null || health.IsDead)
            {
                remaining = 0f;
                RemoveTint();
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

        private void OnDestroy()
        {
            RemoveTint();
        }

        private void ApplyTint()
        {
            if (tinted.Count > 0) return;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r is LineRenderer || r.material == null || !r.material.HasProperty("_BaseColor") && !r.material.HasProperty("_Color")) continue;
                tinted.Add(r);
                originalColors.Add(r.material.color);
                r.material.color = BurnTint;
            }
        }

        private void RemoveTint()
        {
            for (int i = 0; i < tinted.Count; i++)
            {
                if (tinted[i] != null && tinted[i].material != null) tinted[i].material.color = originalColors[i];
            }
            tinted.Clear();
            originalColors.Clear();
        }

        /// <summary>A small orange sphere that rises off the victim and fades out in 0.35 s.</summary>
        private void Flame()
        {
            Vector3 at = transform.position + Vector3.up * Random.Range(0.3f, 1.6f) + Random.insideUnitSphere * 0.35f;
            var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flame.name = "BurnFlame";
            var collider = flame.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            flame.transform.position = at;
            flame.transform.localScale = Vector3.one * Random.Range(0.12f, 0.25f);
            var renderer = flame.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (renderer != null && shader != null) renderer.material = new Material(shader) { color = new Color(1f, Random.Range(0.3f, 0.7f), 0.05f) };
            flame.AddComponent<RisingFlame>();
            Destroy(flame, 0.35f);
        }

        private sealed class RisingFlame : MonoBehaviour
        {
            private void Update() => transform.position += Vector3.up * (1.2f * Time.deltaTime);
        }
    }
}
