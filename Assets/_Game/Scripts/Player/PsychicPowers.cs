using System;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// The three powers and the circle charge (design §2). Keys 1, 2, 3 select BLAST, FIRE, TELEKINESIS.
    /// Hold Charge: she stands still, turns to the locked target (LockOnController) and the circle fills over
    /// fullChargeTime. Release: the power fires at the target with that charge. Cost and damage scale with the
    /// charge. A full SHORT bar turns the release into a short instead. Q uses the drug of the selected power.
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

        public IReadOnlyList<PowerDefinition> Powers => powers;
        public int SelectedIndex { get; private set; }
        public PowerDefinition Selected => powers != null && powers.Length > 0 ? powers[Mathf.Clamp(SelectedIndex, 0, powers.Length - 1)] : null;
        public float Charge { get; private set; }
        public bool IsCharging => lockOn != null && lockOn.IsCharging && !(shortController != null && shortController.IsActive);
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

        /// <summary>The release of the circle: fizzle, short, no target, no nalcon, or the attack.</summary>
        public bool Release(float charge)
        {
            PowerDefinition power = Selected;
            if (power == null) return false;
            if (charge < power.minCharge)
            {
                return Result("fizzle", charge);
            }

            if (stats.IsShortFull && shortController != null)
            {
                Result("short", charge);
                shortController.Begin();
                return false;
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
                    float initial = damage * 0.5f;
                    health?.TakeDamage(initial);
                    RaiseHit(victim, health, point, normal, initial, DamageType.PsychicFire);
                    BurningStatus.Ignite(health != null ? health.gameObject : victim, gameObject, power.burnPerSecond * charge, power.burnSeconds);
                    break;

                case PowerKind.Telekinesis:
                    TelekinesisLift.Apply(health != null ? health.gameObject : victim, gameObject, power.liftHeight, power.liftSeconds, power.holdSeconds, damage);
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
    }
}
