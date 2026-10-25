using System;
using System.Collections;
using UnityEngine;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Destruction;
using GlorifiedBody.Enemy;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// THE SHORT (design §2.8). When the SHORT bar is full, the next power starts a short: a migraine, and
    /// electric shocks fly everywhere out of her control. They do huge damage around her, and they take about
    /// half of her HP before she stabilises. A drug ends the short at once and refills her. Numbers are PROPOSED.
    /// Bruno, 2026-10-20: it lasts 16 s. She can walk, slowly: no run, no dodge, no powers (TankController.SlowWalk).
    /// The HP drain spreads over the 16 s, and each shock does the same damage to the others as before.
    /// One enemy at a time (Bruno, 2026-10-22, design §11.4: "i want the enemies to see their colleagues dying in
    /// a gruesome manner"): every shock hits one victim, the nearest live enemy in the radius, and stays on it
    /// until it dies or leaves the radius. Then the next nearest. A shock-immune target (the bosses) is never
    /// a victim.
    /// Bruno, 2026-10-25: "the freeze/pain anim on the player is only at the start of the shock, to indicate to the
    /// player that the shock status has commenced"; "the shock should also stun the victim"; "placeholder zap, then
    /// real sound". So the start freezes the game for a moment while Lucy plays a pain clip (unscaled time), and
    /// every shock draws an arc from her to the victim, plays the zap there, stuns it and jolts its body.
    /// </summary>
    [RequireComponent(typeof(PsychicStats))]
    public sealed class ShortController : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float duration = 16f;
        [SerializeField, Min(0.1f)] private float shockInterval = 0.4f;
        [SerializeField, Min(1f)] private float shockRadius = 6f;
        [SerializeField, Min(0f)] private float shockDamageToOthers = 12f;
        [SerializeField, Range(0f, 1f)] private float selfHpFraction = 0.5f;

        private const float SurgeRadius = 5f; // the lamps the short's start pops, PROPOSED
        private const float StartFreeze = 0.6f;    // s of frozen game at the start, PROPOSED
        private const float StartRecovery = 0.2f;  // s back to full speed
        public const string ZapPath = "Audio/short_zap"; // Resources: the placeholder zap (ffmpeg, 2026-10-25)
        /// <summary>A shock stuns its victim a little longer than the gap to the next shock: a stun with no break.</summary>
        public float StunSeconds => shockInterval + 0.2f;

        private PsychicStats stats;
        private Health health;
        private TankController tank; // optional: the slow walk during the short
        private float pendingSelf; // Self damage that a dodge frame blocked. Lands at the next shock.

        public bool IsActive { get; private set; }
        public float Remaining { get; private set; }
        public int ShocksFired { get; private set; }
        /// <summary>The enemy the shocks hit now. Null when nobody is in the radius.</summary>
        public Health Victim { get; private set; }
        public float Duration => duration;
        /// <summary>The start's freeze and pain ran this many times (the tests).</summary>
        public int StartPains { get; private set; }
        public int Zaps { get; private set; }
        public int Arcs { get; private set; }

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
            Victim = null;
            pendingSelf = 0f;
            stats.RegenPaused = true;
            if (tank != null) tank.SetSlowWalk(true);
            RunTelemetry.Event("short_begin", $"hp={health.CurrentHealth:0}");
            // The surge pops the lamps near her (the breakable cover, 2026-10-24, PROPOSED).
            Breakables.Surge(transform.position + Vector3.up * 1.5f, SurgeRadius);
            StartPain();
            OnShortBegan?.Invoke();
            StartCoroutine(Run());
        }

        /// <summary>The migraine hits: the game freezes for a moment, and Lucy's pain clip plays through it.</summary>
        private void StartPain()
        {
            StartPains++;
            ImpactFrameController.Instance?.RequestImpactFrame(StartFreeze, StartRecovery);
            PoseClips clips = PoseClips.Load();
            Animator animator = GetComponentInChildren<Animator>();
            if (clips == null || clips.lucyPain == null || animator == null) return;
            PoseOverride pose = animator.GetComponent<PoseOverride>() ?? animator.gameObject.AddComponent<PoseOverride>();
            pose.Play(clips.lucyPain, loop: false, unscaledTime: true);
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
            if (!CanShock(Victim, origin)) Victim = NearestVictim(origin);
            Health other = Victim;
            if (other != null)
            {
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
                // Who it hits (Bruno: "as of now it is not clear who the shock is damaging"): the arc, the zap, the
                // stun, the jolt.
                Vector3 victimChest = other.transform.position + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight;
                ShockArc.Show(origin + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight, victimChest);
                Arcs++;
                if (PlayZap(victimChest)) Zaps++;
                if (!other.IsDead)
                {
                    other.GetComponentInParent<IStaggerable>()?.Stagger(StunSeconds);
                    Animator body = other.GetComponentInChildren<Animator>();
                    if (body != null && body.isHuman)
                    {
                        Tremble tremble = body.GetComponent<Tremble>() ?? body.gameObject.AddComponent<Tremble>();
                        tremble.Jolt(StunSeconds);
                    }
                }
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
            if (other != null) Spark(other.transform.position + Vector3.up * 1.2f); // the spark on the victim shows who it hits
            RunTelemetry.Event("short_shock", $"{ShocksFired} hp={health.CurrentHealth:0} hit={(other != null ? other.name : "none")}");
        }

        /// <summary>A live enemy in the radius that the shocks can hurt.</summary>
        private bool CanShock(Health other, Vector3 origin)
        {
            if (other == null || other == health || other.IsDead) return false;
            if ((other.transform.position - origin).sqrMagnitude > shockRadius * shockRadius) return false;
            LockOnTargetable target = other.GetComponent<LockOnTargetable>() ?? other.GetComponentInParent<LockOnTargetable>();
            return target == null || !target.IsShockImmune;
        }

        private Health NearestVictim(Vector3 origin)
        {
            Health best = null;
            float bestDistance = float.MaxValue;
            foreach (Collider col in Physics.OverlapSphere(origin, shockRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                Health other = col.GetComponentInParent<Health>();
                if (other == best || !CanShock(other, origin)) continue;
                float distance = (other.transform.position - origin).sqrMagnitude;
                if (distance >= bestDistance) continue;
                best = other;
                bestDistance = distance;
            }
            return best;
        }

        private static AudioClip zap;
        private static bool zapLoaded;

        /// <summary>The zap at the victim (a 3D one-shot). False when the clip is missing.</summary>
        private static bool PlayZap(Vector3 at)
        {
            if (!zapLoaded)
            {
                zap = Resources.Load<AudioClip>(ZapPath);
                zapLoaded = true;
            }
            if (zap == null) return false;
            var obj = new GameObject("ShortZap");
            obj.transform.position = at;
            var source = obj.AddComponent<AudioSource>();
            source.clip = zap;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.minDistance = 1.5f;
            source.maxDistance = 30f;
            source.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
            source.Play();
            Destroy(obj, zap.length + 0.1f);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetZap()
        {
            zap = null;
            zapLoaded = false;
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
