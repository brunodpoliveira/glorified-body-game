using System;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// The three powers and the circle charge (design §2). Keys 1, 2, 3 select BLAST, FIRE, TELEKINESIS.
    /// Hold Charge: she stands still, turns to the locked target (LockOnController) and the circle fills over
    /// fullChargeTime. Release: the power fires at the target with that charge. Cost and damage scale with the
    /// charge. A full SHORT bar: the attack press or the defend press starts the short at once (Bruno,
    /// 2026-10-20), and a release with a full bar starts it too. Q uses the drug of the selected power.
    /// </summary>
    [RequireComponent(typeof(PsychicStats), typeof(DrugInventory), typeof(LockOnController))]
    public sealed class PsychicPowers : MonoBehaviour
    {
        [SerializeField] private PowerDefinition[] powers;

        private PsychicStats stats;
        private DrugInventory drugs;
        private LockOnController lockOn;
        private ShortController shortController;
        private bool wasCharging;
        private TelekinesisLift activeLift;
        private bool holdAfterLaunch; // the press that launched must not start a charge
        private float catchPressUntil = -1f; // an RMB press counts for CatchBuffer seconds (the bullet may still be on its way)

        public IReadOnlyList<PowerDefinition> Powers => powers;
        public int SelectedIndex { get; private set; }
        public PowerDefinition Selected => powers != null && powers.Length > 0 ? powers[Mathf.Clamp(SelectedIndex, 0, powers.Length - 1)] : null;
        public float Charge { get; private set; }
        public bool IsCharging => lockOn != null && lockOn.IsCharging && !holdAfterLaunch && !(shortController != null && shortController.IsActive);
        /// <summary>The enemy she holds in the air with telekinesis, while a launch is possible.</summary>
        public TelekinesisLift HeldLift => activeLift != null && activeLift.CanLaunch ? activeLift : null;
        public string LastResult { get; private set; } = "";

        public event Action OnChanged;
        public event Action<PowerDefinition, float, LockOnTargetable> OnPowerFired;

        private void Awake()
        {
            stats = GetComponent<PsychicStats>();
            drugs = GetComponent<DrugInventory>();
            lockOn = GetComponent<LockOnController>();
            shortController = GetComponent<ShortController>();
            if (powers == null || powers.Length == 0)
            {
                powers = Resources.LoadAll<PowerDefinition>("Powers");
                Array.Sort(powers, (a, b) => a.kind.CompareTo(b.kind));
            }
        }

        public void Select(int index)
        {
            if (powers == null || powers.Length == 0) return;
            index = Mathf.Clamp(index, 0, powers.Length - 1);
            if (index == SelectedIndex) return;
            SelectedIndex = index;
            RunTelemetry.Event("power_selected", Selected.kind.ToString());
            OnChanged?.Invoke();
        }

        public void Select(PowerKind kind)
        {
            for (int i = 0; i < powers.Length; i++)
            {
                if (powers[i].kind == kind) { Select(i); return; }
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || Selected == null) return;

            if (GameInput.GetActionDown(InputManager.GameAction.PowerBlast, KeyCode.Alpha1)) Select(PowerKind.Blast);
            if (GameInput.GetActionDown(InputManager.GameAction.PowerFire, KeyCode.Alpha2)) Select(PowerKind.Fire);
            if (GameInput.GetActionDown(InputManager.GameAction.PowerTelekinesis, KeyCode.Alpha3)) Select(PowerKind.Telekinesis);
            if (GameInput.GetActionDown(InputManager.GameAction.UseDrug, KeyCode.Q)) UseDrug();

            // A full SHORT bar (Bruno, 2026-10-20): the attack press or the defend press starts the short at once.
            // That press does nothing else: no charge, no throw, no catch.
            bool attackDown = GameInput.GetActionDown(InputManager.GameAction.Charge, KeyCode.Mouse0);
            bool defendDown = GameInput.GetActionDown(InputManager.GameAction.Defend, KeyCode.Mouse1);
            if ((attackDown || defendDown) && TryStartShort(0f))
            {
                attackDown = false;
                defendDown = false;
            }

            // LMB attacks, RMB defends (Bruno, 2026-10-20). Telekinesis: the attack press with an enemy in the air
            // throws it (no charge, and the press that threw does not start one). The defend press catches a
            // bullet near her and sends it back. The defend hold (the floor shield) comes later.
            if (holdAfterLaunch && !GameInput.GetAction(InputManager.GameAction.Charge, KeyCode.Mouse0)) holdAfterLaunch = false;
            if (attackDown && Selected.kind == PowerKind.Telekinesis && HeldLift != null)
            {
                if (TryLaunch()) holdAfterLaunch = true;
            }
            if (defendDown && Selected.kind == PowerKind.Telekinesis)
            {
                catchPressUntil = Time.time + CatchBuffer;
            }
            if (Time.time <= catchPressUntil && TryCatchBullet())
            {
                catchPressUntil = -1f;
            }

            bool charging = IsCharging;
            if (charging)
            {
                float before = Charge;
                Charge = Mathf.Min(1f, Charge + dt / Selected.fullChargeTime);
                if (!Mathf.Approximately(before, Charge)) OnChanged?.Invoke();
            }
            else if (wasCharging)
            {
                Release(Charge);
                Charge = 0f;
                OnChanged?.Invoke();
            }
            wasCharging = charging;
        }

        /// <summary>The release of the circle: short, fizzle, no target, no nalcon, or the attack.</summary>
        public bool Release(float charge)
        {
            PowerDefinition power = Selected;
            if (power == null) return false;
            // The bar filled during the charge (damage taken): the release starts the short, whatever the charge.
            if (TryStartShort(charge)) return false;
            if (charge < power.minCharge)
            {
                return Result("fizzle", charge);
            }

            LockOnTargetable target = lockOn.CurrentTarget;
            if (target == null || !target.IsAlive || Vector3.Distance(transform.position, target.GetLockPoint()) > power.range)
            {
                return Result("no_target", charge);
            }

            float cost = power.nalconCost * charge;
            if (!stats.TrySpendNalcon(cost))
            {
                return Result("no_nalcon", charge);
            }

            FireAt(power, charge, target);
            return true;
        }

        /// <summary>A full SHORT bar starts the short (design §2.8). True when the short started now.</summary>
        private bool TryStartShort(float charge)
        {
            if (shortController == null || shortController.IsActive || !stats.IsShortFull || stats.Health.IsDead) return false;
            Result("short", charge);
            shortController.Begin();
            return shortController.IsActive;
        }

        private bool Result(string what, float charge)
        {
            LastResult = what;
            RunTelemetry.Event("power_" + what, $"{Selected.kind}:{charge:0.00}");
            OnChanged?.Invoke();
            return false;
        }

        /// <summary>Applies the power to a target with a charge in 0..1. Tests call this directly.</summary>
        public void FireAt(PowerDefinition power, float charge, LockOnTargetable target)
        {
            charge = Mathf.Clamp01(charge);
            GameObject victim = target.gameObject;
            Health health = victim.GetComponent<Health>() ?? victim.GetComponentInParent<Health>();
            float damage = power.baseDamage * charge;
            Vector3 point = target.GetLockPoint();
            Vector3 normal = (transform.position - point).normalized;

            switch (power.kind)
            {
                case PowerKind.Blast:
                    health?.TakeDamage(damage);
                    RaiseHit(victim, health, point, normal, damage, DamageType.PsychicBlast);
                    victim.GetComponentInParent<IStaggerable>()?.Stagger(power.staggerSeconds * charge);
                    break;

                case PowerKind.Fire:
                    // Bruno, 2026-10-20: only a full charge ignites. A part charge is a flash: a heat hit and the
                    // shooter loses its aim for staggerSeconds times the charge (option c, PROPOSED numbers).
                    bool fullCharge = charge >= 0.999f;
                    float heat = fullCharge ? damage * 0.5f : damage;
                    health?.TakeDamage(heat);
                    RaiseHit(victim, health, point, normal, heat, DamageType.PsychicFire);
                    if (fullCharge) BurningStatus.Ignite(health != null ? health.gameObject : victim, gameObject, power.burnPerSecond, power.burnSeconds);
                    else victim.GetComponentInParent<IStaggerable>()?.Stagger(power.staggerSeconds * charge);
                    break;

                case PowerKind.Telekinesis:
                    activeLift = TelekinesisLift.Apply(health != null ? health.gameObject : victim, gameObject, power.liftHeight, power.liftSeconds, power.holdSeconds, damage);
                    break;
            }

            LastResult = "fired";
            RunTelemetry.Event("power_fired", $"{power.kind}:{charge:0.00}:{target.name}");
            OnPowerFired?.Invoke(power, charge, target);
            OnChanged?.Invoke();
        }

        private void RaiseHit(GameObject victim, Health health, Vector3 point, Vector3 normal, float damage, DamageType type)
        {
            HitEventBus.Raise(new HitInfo
            {
                Attacker = gameObject,
                Victim = health != null ? health.gameObject : victim,
                HitTransform = victim.transform,
                HitPoint = point,
                HitNormal = normal,
                DamageAmount = damage,
                DamageType = type,
                WasFatal = health != null && health.IsDead
            });
        }

        /// <summary>Uses the drug of the selected power (or any other): ends a short, refills HP and nalcon.</summary>
        public bool UseDrug()
        {
            PowerDefinition power = Selected;
            DrugType wanted = power != null ? power.drug : DrugType.Nalcon;
            if (!drugs.TryUse(wanted, out DrugType used))
            {
                return Result("drug_none", 0f);
            }

            shortController?.End(byDrug: true);
            stats.RefillAll();
            LastResult = "drug:" + used;
            OnChanged?.Invoke();
            return true;
        }

        public const float LaunchAimCone = 25f;   // degrees each side of her facing, PROPOSED
        public const float CatchRadius = 2.5f;     // metres from her chest, PROPOSED
        public const float CatchNalconCost = 10f;  // PROPOSED
        public const float CatchBuffer = 0.2f;     // seconds an early RMB press stays valid, PROPOSED

        /// <summary>
        /// Throws the enemy she holds in the air. She aims with her facing: the living enemy inside LaunchAimCone
        /// of her forward, nearest first, else the nearest other enemy in range, else straight ahead. Both take
        /// the damage. Free: the lift was paid.
        /// </summary>
        public bool TryLaunch()
        {
            TelekinesisLift lift = HeldLift;
            if (lift == null) return false;
            PowerDefinition power = Selected;
            float range = power != null ? power.range : 12f;
            LockOnTargetable victim = FindAimedTarget(lift.transform, range) ?? FindLaunchTarget(lift.transform, range);
            Vector3 destination = victim != null
                ? victim.GetLockPoint()
                : lift.transform.position + Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * 8f;
            Health victimHealth = victim != null ? victim.GetComponent<Health>() ?? victim.GetComponentInParent<Health>() : null;
            if (!lift.Launch(destination, victimHealth)) return false;
            LastResult = "launch:" + (victim != null ? victim.name : "free");
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>The living enemy inside the aim cone of her facing, the nearest one. Null when none.</summary>
        private LockOnTargetable FindAimedTarget(Transform flyer, float range)
        {
            LockOnTargetable best = null;
            float bestDistance = range;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            foreach (LockOnTargetable candidate in TargetRegistry.All)
            {
                if (candidate == null || !candidate.IsAlive || !candidate.Selectable) continue;
                if (candidate.transform == flyer || candidate.transform.IsChildOf(flyer)) continue;
                Vector3 to = Vector3.ProjectOnPlane(candidate.GetLockPoint() - transform.position, Vector3.up);
                float distance = to.magnitude;
                if (distance >= bestDistance || Vector3.Angle(forward, to) > LaunchAimCone) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>
        /// The defensive telekinesis, first form (Bruno, 2026-10-20): a bullet within CatchRadius of her chest
        /// stops and flies back at its shooter, twice as hard. Costs CatchNalconCost. A timing move: the
        /// telegraph says when the bullet comes.
        /// </summary>
        public bool TryCatchBullet()
        {
            Vector3 chest = transform.position + Vector3.up * Body.ChestHeight;
            EnemyProjectile best = null;
            float bestDistance = CatchRadius;
            foreach (EnemyProjectile bullet in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
            {
                if (bullet == null || !bullet.Catchable) continue;
                float distance = Vector3.Distance(chest, bullet.transform.position);
                if (distance >= bestDistance) continue;
                best = bullet;
                bestDistance = distance;
            }
            if (best == null) return false;
            if (!stats.TrySpendNalcon(CatchNalconCost)) return Result("no_nalcon", 0f);
            best.Return(gameObject);
            LastResult = "catch";
            RunTelemetry.Event("bullet_caught", best.KillerId);
            OnChanged?.Invoke();
            return true;
        }

        private static LockOnTargetable FindLaunchTarget(Transform flyer, float range)
        {
            LockOnTargetable best = null;
            float bestDistance = range;
            foreach (LockOnTargetable candidate in TargetRegistry.All)
            {
                if (candidate == null || !candidate.IsAlive || !candidate.Selectable) continue;
                if (candidate.transform == flyer || candidate.transform.IsChildOf(flyer)) continue;
                float distance = Vector3.Distance(flyer.position, candidate.GetLockPoint());
                if (distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }
    }
}
