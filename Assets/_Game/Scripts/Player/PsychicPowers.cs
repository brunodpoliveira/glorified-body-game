using System;
using System.Collections.Generic;
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
    /// The three powers and the circle charge (design §2). Keys 1, 2, 3 select mk_warspite (the blast), mk_erebus
    /// (the fire), mk_orzabal (the telekinesis; Bruno's names of 2026-10-22, the code keeps Blast, Fire, Telekinesis).
    /// Hold Charge: she stands still, turns to the locked target (LockOnController) and the circle fills over
    /// fullChargeTime. Release: the power fires at the target with that charge. Cost and damage scale with the
    /// charge. The power fires by itself when the circle fills (Bruno, 2026-11-06: "when the circle for the power is full
    /// the power is unleashed automatically"). A press of the defend key during a charge cancels it, with no MK spent.
    /// A full SHORT bar: the attack press or the defend press starts the short at once (Bruno,
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
        private TankController tank;
        private float defendHeldFor;
        private bool shieldLocked; // the shield's circle holds her feet

        public IReadOnlyList<PowerDefinition> Powers => powers;
        public int SelectedIndex { get; private set; }
        public PowerDefinition Selected => powers != null && powers.Length > 0 ? powers[Mathf.Clamp(SelectedIndex, 0, powers.Length - 1)] : null;
        public float Charge { get; private set; }
        /// <summary>The circle of the floor shield (the defend key held with mk_orzabal), 0..1.</summary>
        public float ShieldCharge { get; private set; }
        /// <summary>The circle the HUD shows: the attack's or the shield's.</summary>
        public float ShownCharge => Mathf.Max(Charge, ShieldCharge);

        /// <summary>The MK boost (Bruno, 2026-10-22): at half the bar of the selected power or more, attacks do 2x damage and charge 2x as fast.</summary>
        public const float BoostThreshold = 0.5f;
        public const float BoostMultiplier = 2f;
        public bool IsBoosted => stats != null && stats.Nalcon >= PsychicStats.MaxNalcon * BoostThreshold - 0.001f;
        /// <summary>The damage scale of a power now: the MK boost times her level's (ExperienceRules).</summary>
        public float DamageScale => (IsBoosted ? BoostMultiplier : 1f) * (stats != null ? stats.LevelDamageScale : 1f);
        public bool IsCharging => lockOn != null && lockOn.IsCharging && !holdAfterLaunch && !(shortController != null && shortController.IsActive);
        /// <summary>The enemy she holds in the air with telekinesis, while a launch is possible.</summary>
        public TelekinesisLift HeldLift => activeLift != null && activeLift.CanLaunch ? activeLift : null;
        private string lastResult = "";
        /// <summary>The last result: "fired", "slam", "launch:...", "catch...", "drug:...", "too_heavy", "no_nalcon"...</summary>
        public string LastResult
        {
            get => lastResult;
            private set
            {
                lastResult = value ?? "";
                OnResult?.Invoke(lastResult);
            }
        }
        /// <summary>Each result as it happens (the power sounds, GlorifiedBody.Audio.PowerSounds).</summary>
        public event Action<string> OnResult;
        /// <summary>Time.time of the last result (the HUD shows "TOO HEAVY" for a moment).</summary>
        public float LastResultTime { get; private set; } = -10f;

        public event Action OnChanged;
        public event Action<PowerDefinition, float, LockOnTargetable> OnPowerFired;

        private void Awake()
        {
            stats = GetComponent<PsychicStats>();
            drugs = GetComponent<DrugInventory>();
            lockOn = GetComponent<LockOnController>();
            shortController = GetComponent<ShortController>();
            tank = GetComponent<TankController>();
            if (powers == null || powers.Length == 0)
            {
                powers = Resources.LoadAll<PowerDefinition>("Powers");
                Array.Sort(powers, (a, b) => a.kind.CompareTo(b.kind));
            }
            if (Selected != null) stats.Selected = Selected.kind;
            SyncProps();
        }

        /// <summary>Props join the target switch only while telekinesis is selected.</summary>
        private void SyncProps()
        {
            if (lockOn != null) lockOn.IncludeProps = Selected != null && Selected.kind == PowerKind.Telekinesis;
        }

        /// <summary>mk_edgar comes with the story: until this flag, key 4 does nothing and its bar and capsule stay hidden.</summary>
        public const string EdgarFlag = "mk_edgar_unlocked";
        public static bool EdgarUnlocked => GlorifiedBody.Saving.StoryFlags.Has(EdgarFlag);

        public void Select(int index)
        {
            if (powers == null || powers.Length == 0) return;
            index = Mathf.Clamp(index, 0, powers.Length - 1);
            if (index == SelectedIndex) return;
            if (powers[index].kind == PowerKind.Domination && !EdgarUnlocked) return;
            SelectedIndex = index;
            stats.Selected = Selected.kind;
            SyncProps();
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
            if (GameInput.GetActionDown(InputManager.GameAction.PowerDomination, KeyCode.Alpha4)) Select(PowerKind.Domination);
            // The drug key opens the drug choice where the room has one (InventoryUI); else it takes a drug.
            if (GameInput.GetActionDown(InputManager.GameAction.UseDrug, KeyCode.Q) && (OpenDrugMenu == null || !OpenDrugMenu())) UseDrug();

            // A full SHORT bar (Bruno, 2026-10-20): the attack press or the defend press starts the short at once.
            // That press does nothing else: no charge, no throw, no catch.
            bool attackDown = GameInput.GetActionDown(InputManager.GameAction.Charge, KeyCode.Mouse0);
            bool defendDown = GameInput.GetActionDown(InputManager.GameAction.Defend, KeyCode.Mouse1);
            if ((attackDown || defendDown) && TryStartShort(0f))
            {
                attackDown = false;
                defendDown = false;
            }

            // The cancel (Bruno, 2026-11-06): the defend press during a charge does only this.
            if (defendDown && Charge > 0f && IsCharging)
            {
                Result("cancel", Charge);
                EndAttackCharge();
                defendDown = false;
                defendAfterCancel = true;
            }
            if (!GameInput.GetAction(InputManager.GameAction.Defend, KeyCode.Mouse1)) defendAfterCancel = false;

            // The drop (Bruno, 2026-11-07: "right clicking should cancel the lift and drop the object gently on the ground"): the
            // defend press on a box or a crate in the air does only this, for no MK. Not on an enemy or on the slab: a misclick
            // in a fight must not lose them ("the player might gently drop an enemy by accident"), so that press still catches.
            TelekinesisLift dropped = defendDown && Selected.kind == PowerKind.Telekinesis ? HeldLift : null;
            if (dropped != null && dropped.IsBox && dropped.Release(gameObject))
            {
                Result("drop", 0f);
                defendDown = false;
                defendAfterCancel = true; // the key of this drop starts no shield and no carry: it must rise first
            }

            // mk_orzabal needs no charge to lift (Bruno, 2026-11-07: "the moment the player selects and triggers it it lifts the
            // selected target; it is only when the object is in mid-air that the charge is meaningul").
            if (Selected.kind == PowerKind.Telekinesis && HeldLift == null)
            {
                if (attackDown) TryLift();
                if (attackDown || IsCharging) EndAttackCharge();
            }
            lockOn.HeldThing = HeldLift != null ? HeldLift.transform : null;
            lockOn.WheelRaises = HeldLift != null && Selected.kind == PowerKind.Telekinesis;

            // LMB attacks, RMB defends (Bruno, 2026-10-20). Telekinesis with an enemy in the air (Bruno,
            // 2026-10-22): the attack key charges as usual; a release throws it by the charge, a full charge slams it
            // (Release). The defend press catches a bullet near her and sends it back.
            if (holdAfterLaunch && !GameInput.GetAction(InputManager.GameAction.Charge, KeyCode.Mouse0)) holdAfterLaunch = false;
            // mk_edgar: the defend press sends the one she holds to a switch she cannot reach.
            if (defendDown && Selected.kind == PowerKind.Domination && Domination.Current != null)
                Result(Domination.Current.SendToSwitch(transform.position, Selected.range) ? "dom_sent" : "dom_no_switch", 0f);
            if (defendDown && Selected.kind == PowerKind.Telekinesis)
            {
                catchPressUntil = Time.time + CatchBuffer;
            }
            if (Time.time <= catchPressUntil && TryCatchBullet())
            {
                catchPressUntil = -1f;
            }

            UpdateSendOffer();
            UpdateShield(dt);
            UpdateHeld();

            // The bar filled during a held charge (a hit): the short starts now, not at the release (Bruno,
            // 2026-10-25: "shock should trigger the moment player presses attack"). The charge is lost.
            if (wasCharging && TryStartShort(Charge))
            {
                Charge = 0f;
                wasCharging = false;
                OnChanged?.Invoke();
            }

            bool charging = IsCharging;
            UpdateDomination(ref charging);
            if (charging)
            {
                float before = Charge;
                float rate = IsBoosted && Selected.kind != PowerKind.Domination ? BoostMultiplier : 1f; // the MK boost charges 2x as fast
                Charge = Mathf.Min(1f, Charge + dt * rate / Selected.fullChargeTime);
                if (!Mathf.Approximately(before, Charge)) OnChanged?.Invoke();
                if (Charge >= 1f)
                {
                    float full = Charge;
                    EndAttackCharge();
                    Release(full);
                    OnChanged?.Invoke();
                    return;
                }
            }
            else if (wasCharging)
            {
                Release(Charge);
                Charge = 0f;
                OnChanged?.Invoke();
            }
            wasCharging = charging;
        }

        // ── mk_edgar ──────────────────────────────────────────────────

        private IDominable kneeling;
        private LockOnTargetable kneelingTarget;
        private bool dominationBroken;
        private LockOnTargetable refusedTarget;

        /// <summary>
        /// The charge of mk_edgar (Bruno, 2026-11-06): the target goes down on its knees and holds still while the circle fills. A
        /// target that it cannot take ends the charge at once, at no cost. A hit on her, or the target out of her reach, breaks it.
        /// </summary>
        private void UpdateDomination(ref bool charging)
        {
            if (!charging || Selected.kind != PowerKind.Domination)
            {
                EndKneel();
                dominationBroken = false;
                refusedTarget = null;
                return;
            }
            LockOnTargetable target = lockOn.CurrentTarget;
            string why = dominationBroken ? "dom_broken" : Domination.Refusal(target, transform.position, Selected.range, stats.CanSpendMk(PowerKind.Domination, Selected.nalconCost));
            if (why == null && kneeling != null && target != kneelingTarget) why = "dom_broken";
            // A floater: the circle goes on, and it will do nothing (below). She may waste her MK (Bruno, 2026-11-07), and the HUD does not warn her.
            if (kneeling == null && IsFloating(target) && why != "dom_far" && why != "dom_none" && why != "dom_broken") return;
            // She cannot take this one now, but a part circle still stuns it: the circle goes on with no kneel. The HUD says why.
            if (kneeling == null && (why == "dom_not_hurt" || why == "dom_boss" || why == "no_nalcon"))
            {
                if (refusedTarget != target) Result(why, Charge);
                refusedTarget = target;
                return;
            }
            if (why != null)
            {
                dominationBroken = false;
                Result(why, Charge);
                EndKneel();
                EndAttackCharge();
                charging = false;
                return;
            }
            if (kneeling != null) return;
            kneeling = target.GetComponentInParent<IDominable>();
            kneelingTarget = target;
            kneeling.BeginKneel();
            RunTelemetry.Event("dom_kneel", target.name);
        }

        private void EndKneel()
        {
            if (kneeling is MonoBehaviour figure && figure != null) kneeling.EndKneel();
            kneeling = null;
            kneelingTarget = null;
        }

        /// <summary>
        /// Only the full circle dominates. A part circle stuns the target, and it costs by the charge, as mk_erebus does (Bruno,
        /// 2026-11-07: "edgar/mind control on non-full charge also stuns the enemy like the fire power"). Under the least charge: a fizzle.
        /// </summary>
        private bool ReleaseDomination(float charge)
        {
            PowerDefinition power = Selected;
            // On a floater it does nothing, but it can be used (Bruno, 2026-11-07: "the player will need to be smart with resource
            // allocation and not squander their powers like this"): the MK goes, with no stun and no mind.
            if (IsFloating(lockOn.CurrentTarget) && charge >= power.minCharge)
            {
                EndKneel();
                if (!stats.TrySpendMk(power.kind, power.nalconCost * charge)) return Result("no_nalcon", charge);
                LastResult = "dom_nothing";
                LastResultTime = Time.time;
                RunTelemetry.Event("dom_nothing", $"{lockOn.TargetName}:{charge:0.00}");
                OnPowerFired?.Invoke(power, charge, lockOn.CurrentTarget);
                OnChanged?.Invoke();
                return true;
            }
            if (charge < 0.999f)
            {
                LockOnTargetable stunned = lockOn.CurrentTarget;
                EndKneel();
                if (charge < power.minCharge || stunned == null) return Result("fizzle", charge);
                if (!stats.TrySpendMk(power.kind, power.nalconCost * charge)) return Result("no_nalcon", charge);
                stunned.GetComponentInParent<IStaggerable>()?.Stagger(power.staggerSeconds * charge);
                LastResult = "dom_stun";
                LastResultTime = Time.time;
                RunTelemetry.Event("dom_stun", $"{stunned.name}:{power.staggerSeconds * charge:0.0}s");
                OnPowerFired?.Invoke(power, charge, stunned);
                OnChanged?.Invoke();
                return true;
            }
            if (kneeling == null) return Result(Domination.Refusal(lockOn.CurrentTarget, transform.position, power.range, stats.CanSpendMk(power.kind, power.nalconCost)) ?? "fizzle", charge);
            if (!stats.TrySpendMk(power.kind, power.nalconCost))
            {
                EndKneel();
                return Result("no_nalcon", charge);
            }
            IDominable mind = kneeling;
            LockOnTargetable target = kneelingTarget;
            kneeling = null;
            kneelingTarget = null;
            Domination.Take(mind, gameObject);
            lockOn.SetTarget(null);
            LastResult = "dominated";
            LastResultTime = Time.time;
            RunTelemetry.Event("dominated", target.name);
            OnPowerFired?.Invoke(power, charge, target);
            OnChanged?.Invoke();
            return true;
        }

        private static bool IsFloating(LockOnTargetable target)
        {
            TelekinesisLift lift = target != null ? target.GetComponentInParent<TelekinesisLift>() : null;
            return lift != null && lift.IsLifted;
        }

        /// <summary>The switch that the defend press would send her mind to now (Bruno, 2026-11-07: the order had no sign on screen). The HUD shows it.</summary>
        public GlorifiedBody.Interaction.SentSwitch OfferedSwitch { get; private set; }
        private const float OfferCheckSeconds = 0.25f; // a path query is not for every frame
        private float nextOfferCheck;

        private void UpdateSendOffer()
        {
            Dominated mind = Domination.Current;
            bool can = mind != null && Selected.kind == PowerKind.Domination && mind.SentTo == null && !mind.Snapping;
            if (can && Time.time < nextOfferCheck) return;
            nextOfferCheck = Time.time + OfferCheckSeconds;
            GlorifiedBody.Interaction.SentSwitch offer = can ? mind.SwitchInReach(transform.position, Selected.range) : null;
            if (offer == OfferedSwitch) return;
            if (OfferedSwitch != null) OfferedSwitch.Offer(false);
            OfferedSwitch = offer;
            if (OfferedSwitch != null) OfferedSwitch.Offer(true);
        }

        private void OnHitDuringDomination(float amount)
        {
            if (kneeling != null) dominationBroken = true;
        }

        private void OnEnable()
        {
            Health health = GetComponent<Health>(); // PsychicStats may wake after this
            health.OnDamaged ??= new UnityEngine.Events.UnityEvent<float>();
            health.OnDamaged.AddListener(OnHitDuringDomination);
        }

        private void OnDisable() => GetComponent<Health>()?.OnDamaged?.RemoveListener(OnHitDuringDomination);

        /// <summary>The circle empties and her feet go free. The attack key must rise before a new charge.</summary>
        private void EndAttackCharge()
        {
            Charge = 0f;
            wasCharging = false;
            lockOn.EndCharge();
            OnChanged?.Invoke();
        }

        private bool defendAfterCancel; // the key of a cancel is still down: it starts no floor shield

        public const float ShieldHoldDelay = 0.2f; // s: a shorter press is only the catch, PROPOSED

        /// <summary>
        /// The floor shield (2026-10-31, design §2.3; Bruno, 2026-10-20: "RMB hold = the floor shield, the defensive
        /// full charge"). With mk_orzabal selected and nothing in the air, the defend key held past ShieldHoldDelay fills
        /// the circle (she stands still and faces her target, as for an attack). At a full circle a slab of the floor in
        /// front of her bends up into a standing wall (FloorShield), for the power's full cost. It stays where it rose until
        /// it breaks (Bruno, 2026-11-08), so another may rise while it stands: each needs a new press of the defend key.
        /// A release before the full circle costs nothing.
        /// </summary>
        private void UpdateShield(float dt)
        {
            bool held = GameInput.GetAction(InputManager.GameAction.Defend, KeyCode.Mouse1);
            bool can = held && !defendAfterCancel && Selected.kind == PowerKind.Telekinesis && activeLift == null
                       && !IsCharging && !(shortController != null && shortController.IsActive) && !stats.Health.IsDead;
            if (!can)
            {
                EndShieldCharge();
                return;
            }
            defendHeldFor += dt;
            if (defendHeldFor < ShieldHoldDelay) return;
            if (TryCarry(Selected))
            {
                defendHeldFor = 0f;
                return;
            }
            if (!shieldLocked && tank != null)
            {
                tank.LockMovement(true);
                shieldLocked = true;
            }
            if (tank != null && lockOn.CurrentTarget != null) tank.FaceTowards(lockOn.CurrentTarget.GetLockPoint(), 540f);
            float before = ShieldCharge;
            ShieldCharge = Mathf.Min(1f, ShieldCharge + dt * (IsBoosted ? BoostMultiplier : 1f) / Selected.fullChargeTime);
            if (!Mathf.Approximately(before, ShieldCharge)) OnChanged?.Invoke();
            if (ShieldCharge >= 0.999f)
            {
                TryRaiseShield();
                EndShieldCharge();
                defendAfterCancel = true; // the next slab needs a new press
            }
        }

        private void EndShieldCharge()
        {
            defendHeldFor = 0f;
            if (ShieldCharge > 0f)
            {
                ShieldCharge = 0f;
                OnChanged?.Invoke();
            }
            if (shieldLocked)
            {
                shieldLocked = false;
                if (tank != null) tank.LockMovement(false); // the lock counts: the charge keeps its own
            }
        }

        public const float CarryHeight = 0.8f;        // m it rises, PROPOSED
        public const float CarryRise = 0.25f;         // s
        public const float CarryMetresPerUnit = 0.02f; // m for each unit of the mouse axes, times Mouse Sensitivity, PROPOSED
        public const float CarryMetresPerWheel = 6f;   // m for each unit of the wheel axis, PROPOSED
        private const float CarryFrontCone = 60f;      // degrees each side of her facing, PROPOSED
        private const float CarrySightHeight = 2f;     // m over her feet: the level of the line that a wall stops
        private const float CarryCostPerMetre = 3f;    // degrees of her facing a metre is worth, PROPOSED
        private bool carrying;

        /// <summary>She carries a prop with the defend key held (Bruno, 2026-11-06: hold to select and lift, the mouse moves it, release to drop).</summary>
        public bool Carrying => carrying && HeldLift != null;

        /// <summary>
        /// The defend key held with mk_orzabal lifts a prop, for the cost of any lift (Bruno, 2026-11-07: "lift should cost the same
        /// no matter what is being lifted"). The prop is the target she has switched to, or with no enemy locked, the nearest one in
        /// front of her. With an enemy locked the key still makes the floor shield. A throw from the carry costs by its charge.
        /// </summary>
        private bool TryCarry(PowerDefinition power)
        {
            LockOnTargetable locked = lockOn.CurrentTarget;
            LockOnTargetable prop = locked != null ? (locked.IsProp ? locked : null) : PropInFront(power.range);
            if (prop == null || !prop.IsAlive || Vector3.Distance(transform.position, prop.GetLockPoint()) > power.range) return false;
            if (prop.IsHeavy)
            {
                Result("too_heavy", 0f);
                return false;
            }
            EndShieldCharge();
            if (!stats.TrySpendMk(power.kind, LiftCost))
            {
                Result("no_nalcon", 0f);
                defendAfterCancel = true; // no shield from this key either: she asked for the box
                return true;
            }
            EnsurePropBody(prop.gameObject);
            activeLift = TelekinesisLift.Apply(prop.gameObject, gameObject, CarryHeight, CarryRise, 0f, power.baseDamage * DamageScale);
            if (activeLift == null) return false;
            activeLift.StaysWhenSheTurns = true;
            carrying = true;
            if (tank != null) tank.LockMovement(true);
            LastResult = "carry";
            LastResultTime = Time.time;
            RunTelemetry.Event("tk_carry", prop.name);
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>A crate of the rooms has no Rigidbody until its first lift: from then on it falls and rests like a box.</summary>
        private static void EnsurePropBody(GameObject prop)
        {
            if (prop.TryGetComponent(out Rigidbody _)) return;
            prop.AddComponent<Rigidbody>().mass = 30f;
        }

        private LockOnTargetable PropInFront(float range)
        {
            LockOnTargetable best = null;
            float bestCost = float.MaxValue;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            int walls = LayerMask.GetMask("Default");
            foreach (LockOnTargetable candidate in TargetRegistry.All)
            {
                if (candidate == null || !candidate.IsProp || !candidate.Selectable || candidate.IsHeavy) continue;
                Vector3 point = candidate.GetLockPoint();
                Vector3 to = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
                float angle = Vector3.Angle(forward, to);
                if (to.magnitude > range || angle > CarryFrontCone) continue;
                // A wall stops her reach. A low rail does not: the line runs level over it.
                Vector3 high = Vector3.up * CarrySightHeight;
                if (Physics.Linecast(transform.position + high, new Vector3(point.x, transform.position.y, point.z) + high, walls, QueryTriggerInteraction.Ignore)) continue;
                float cost = angle + to.magnitude * CarryCostPerMetre; // what she faces, then what is near
                if (cost >= bestCost) continue;
                best = candidate;
                bestCost = cost;
            }
            return best;
        }

        /// <summary>The mouse and the wheel move what she holds, seen from the fixed camera: the mouse left and right, up is away, the wheel is the height.</summary>
        private void UpdateHeld()
        {
            TelekinesisLift lift = HeldLift;
            if (liftTurnUntil >= 0f)
            {
                if (lift == null || carrying) liftTurnUntil = -1f;
                else if (Time.time < liftTurnUntil) { if (tank != null) tank.FaceTowards(lift.transform.position, LiftTurnSpeed); }
                else
                {
                    liftTurnUntil = -1f;
                    lift.StaysWhenSheTurns = false; // from now on it turns with her, so she aims the throw with the tank turn
                }
            }
            bool mine = lift != null && Selected.kind == PowerKind.Telekinesis;
            if (tank != null) tank.MouseTurnPaused = mine;
            if (carrying)
            {
                if (lift == null) EndCarry();
                else if (!mine || !GameInput.GetAction(InputManager.GameAction.Defend, KeyCode.Mouse1))
                {
                    lift.Release(gameObject);
                    EndCarry();
                    LastResult = "carry_drop";
                    OnChanged?.Invoke();
                    return;
                }
                else if (tank != null) tank.FaceTowards(lift.transform.position, 540f);
            }
            if (!mine) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            float x = GameInput.GetAxis(GameInput.AxisLookX), y = GameInput.GetAxis(GameInput.AxisLookY), wheel = GameInput.GetAxis(GameInput.AxisScroll);
            if (Mathf.Approximately(x, 0f) && Mathf.Approximately(y, 0f) && Mathf.Approximately(wheel, 0f)) return;
            float scale = CarryMetresPerUnit * (SettingsManager.Instance != null ? SettingsManager.Instance.MouseSensitivity : 3f);
            Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
            Vector3 away = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            lift.SteerBy((right * x + away * y) * scale, wheel * CarryMetresPerWheel, Selected.range);
        }

        private void EndCarry()
        {
            carrying = false;
            defendAfterCancel = true; // the key of this carry starts no shield and no new carry: it must rise first
            if (tank != null) tank.LockMovement(false);
        }

        /// <summary>The full circle of the shield: the slab tears out and rises. Tests call it directly.</summary>
        public bool TryRaiseShield()
        {
            PowerDefinition power = Selected;
            if (power == null || power.kind != PowerKind.Telekinesis || activeLift != null) return false;
            if (!stats.CanSpendMk(power.kind, power.nalconCost)) return Result("no_nalcon", 1f);
            FloorShield shield = FloorShield.Tear(gameObject);
            if (shield == null) return Result("no_floor", 1f);
            stats.TrySpendMk(power.kind, power.nalconCost);
            LastResult = "shield";
            LastResultTime = Time.time;
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>The release of the circle: short, fizzle, no target, no nalcon, or the attack.</summary>
        public bool Release(float charge)
        {
            PowerDefinition power = Selected;
            if (power == null) return false;
            // The bar filled during the charge (damage taken): the release starts the short, whatever the charge.
            if (TryStartShort(charge)) return false;
            if (power.kind == PowerKind.Domination) return ReleaseDomination(charge);
            // Telekinesis: with nothing in the air it lifts, at no charge. With a thing in the air a full charge slams it, anything less throws it.
            if (power.kind == PowerKind.Telekinesis) return HeldLift == null ? TryLift() : charge >= 0.999f ? TrySlam() : TryLaunch(charge);
            if (charge < power.minCharge)
            {
                return Result("fizzle", charge);
            }

            LockOnTargetable target = lockOn.CurrentTarget;
            if (target == null || !target.IsAlive || Vector3.Distance(transform.position, target.GetLockPoint()) > power.range
                || (target.IsProp && power.kind != PowerKind.Telekinesis))
            {
                return Result("no_target", charge);
            }
            float cost = power.nalconCost * charge;
            float scale = DamageScale; // the boost is read before the cost lowers the bar
            if (!stats.TrySpendMk(power.kind, cost))
            {
                return Result("no_nalcon", charge);
            }

            FireAt(power, charge, target, scale);
            return true;
        }

        public const float LiftCost = 10f;        // MK for any lift of mk_orzabal, PROPOSED
        private const float LiftTurnSeconds = 0.35f; // she turns to what she lifts, and it does not swing with her in this time
        private const float LiftTurnSpeed = 720f;  // degrees a second
        private float liftTurnUntil = -1f;

        /// <summary>The lift of mk_orzabal: the locked target rises now, for LiftCost. She turns to it as it rises.</summary>
        private bool TryLift()
        {
            PowerDefinition power = Selected;
            if ((shortController != null && shortController.IsActive) || stats.Health.IsDead) return false;
            LockOnTargetable target = lockOn.CurrentTarget;
            if (target == null || !target.IsAlive || Vector3.Distance(transform.position, target.GetLockPoint()) > power.range) return Result("no_target", 0f);
            // Too heavy to lift (the power armour): no lift and no cost.
            if (target.IsHeavy) return Result("too_heavy", 0f);
            TelekinesisLift flying = target.GetComponentInParent<TelekinesisLift>();
            if (flying != null && flying.IsLifted) return false; // a throw of it is on its way
            float scale = DamageScale; // the boost is read before the cost lowers the bar
            if (!stats.TrySpendMk(power.kind, LiftCost)) return Result("no_nalcon", 0f);
            FireAt(power, 1f, target, scale);
            if (HeldLift != null)
            {
                HeldLift.StaysWhenSheTurns = true;
                liftTurnUntil = Time.time + LiftTurnSeconds;
            }
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
            LastResultTime = Time.time;
            RunTelemetry.Event("power_" + what, $"{Selected.kind}:{charge:0.00}");
            OnChanged?.Invoke();
            return false;
        }

        /// <summary>Applies the power to a target with a charge in 0..1, with the MK boost of the moment. Tests call this directly.</summary>
        public void FireAt(PowerDefinition power, float charge, LockOnTargetable target) => FireAt(power, charge, target, DamageScale);

        /// <summary>Applies the power with a damage scale (DamageScale: 2 under the MK boost, times her level's).</summary>
        public void FireAt(PowerDefinition power, float charge, LockOnTargetable target, float damageScale)
        {
            charge = Mathf.Clamp01(charge);
            GameObject victim = target.gameObject;
            Health health = victim.GetComponent<Health>() ?? victim.GetComponentInParent<Health>();
            float damage = power.baseDamage * charge * damageScale;
            LastScale = damageScale;
            Vector3 point = target.GetLockPoint();
            Vector3 normal = (transform.position - point).normalized;

            // A shield from the front stops mk_warspite and mk_erebus (Bruno, 2026-10-24). The MK is spent.
            IFrontGuard guard = power.kind != PowerKind.Telekinesis ? victim.GetComponentInParent<IFrontGuard>() : null;
            if (guard != null && guard.Blocks(transform.position))
            {
                guard.Blocked(point, power.kind.ToString());
                LastScale = damageScale;
                Result("blocked", charge);
                return;
            }

            // Cover in the way takes the power (Bruno, 2026-10-24: "cover only enemies, on purpose"): a chest-high
            // barricade stops mk_warspite and mk_erebus until it breaks. mk_orzabal reaches over it. The MK is spent.
            if (power.kind != PowerKind.Telekinesis)
            {
                Vector3 eye = transform.position + Vector3.up * Body.ChestHeight;
                // Hidden, he crouches: the line goes to his hips. To the lock point it passed just over a low planter (2026-11-06).
                Vector3 aim = target.InCover ? target.transform.position + Vector3.up * CoverLineHeight : point;
                Breakable cover = Breakables.CoverOnLine(eye, aim, out Vector3 coverPoint);
                if (cover != null)
                {
                    Vector3 along = (aim - eye).normalized;
                    float onCover = power.kind == PowerKind.Blast ? damage : charge >= 0.999f ? damage * 0.5f : 0f;
                    if (onCover > 0f)
                    {
                        cover.Damage(onCover, power.kind == PowerKind.Blast ? DamageType.PsychicBlast : DamageType.PsychicFire, coverPoint, along);
                        Breakables.Splash(coverPoint, CoverSplashRadius, onCover * 0.3f, power.kind == PowerKind.Blast ? DamageType.PsychicBlast : DamageType.PsychicFire, cover);
                    }
                    RunTelemetry.Event("cover_took", $"{power.kind}:{charge:0.00}:{cover.Id}:{cover.Health:0}");
                    Result("cover", charge);
                    return;
                }
                // A thing that does not break stops it too, while he hides behind it. He stands to fire: then it reaches him.
                if (target.InCover && Physics.Linecast(eye, aim, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
                {
                    RunTelemetry.Event("cover_took", $"{power.kind}:{charge:0.00}:solid");
                    Result("cover", charge);
                    return;
                }
            }

            switch (power.kind)
            {
                case PowerKind.Blast:
                    health?.TakeDamage(damage);
                    // The room around him takes it too: all the destruction is collateral (Bruno, 2026-10-24).
                    Breakables.Splash(point, BlastSplashRadius + BlastSplashPerCharge * charge, damage * BlastSplashShare, DamageType.PsychicBlast);
                    RaiseHit(victim, health, point, normal, damage, DamageType.PsychicBlast, charge);
                    victim.GetComponentInParent<IStaggerable>()?.Stagger(power.staggerSeconds * charge);
                    // A full circle knocks him down (Bruno, 2026-10-24: "for a while (2.5 seconds)"); not a heavy.
                    if (charge >= 0.999f && !target.IsHeavy)
                    {
                        Vector3 away = Vector3.ProjectOnPlane(point - transform.position, Vector3.up).normalized;
                        victim.GetComponentInParent<IRagdollBody>()?.KnockDown(BlastKnockdownSeconds, away * BlastKnockdownPush + Vector3.up * 1.5f);
                    }
                    break;

                case PowerKind.Fire:
                    // Bruno, 2026-10-22: "fire only damages on full charge; anything less just stuns them". A full
                    // charge is a heat hit and the burn; a part charge does no damage and breaks the aim for
                    // staggerSeconds times the charge.
                    if (charge >= 0.999f)
                    {
                        float heat = damage * 0.5f;
                        health?.TakeDamage(heat);
                        RaiseHit(victim, health, point, normal, heat, DamageType.PsychicFire, charge);
                        BurningStatus.Ignite(health != null ? health.gameObject : victim, gameObject, power.burnPerSecond * damageScale, power.burnSeconds);
                        Breakables.Splash(point, CoverSplashRadius, heat * BlastSplashShare, DamageType.PsychicFire);
                    }
                    else
                    {
                        victim.GetComponentInParent<IStaggerable>()?.Stagger(power.staggerSeconds * charge);
                        RunTelemetry.Event("fire_stun", $"{victim.name}:{power.staggerSeconds * charge:0.0}s");
                    }
                    break;

                case PowerKind.Telekinesis:
                    if (target.IsHeavy)
                    {
                        Result("too_heavy", charge);
                        return;
                    }
                    if (target.IsProp) EnsurePropBody(victim);
                    activeLift = TelekinesisLift.Apply(health != null ? health.gameObject : victim, gameObject, power.liftHeight, power.liftSeconds, power.holdSeconds, damage);
                    break;
            }

            LastResult = "fired";
            RunTelemetry.Event("power_fired", $"{power.kind}:{charge:0.00}:{target.name}");
            OnPowerFired?.Invoke(power, charge, target);
            OnChanged?.Invoke();
        }

        /// <summary>The drug choice of the room (InventoryUI sets it). True when it opened.</summary>
        public static Func<bool> OpenDrugMenu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDrugMenu() => OpenDrugMenu = null;

        private const float CoverLineHeight = 0.5f;      // m over his feet, while he hides
        public const float BlastKnockdownSeconds = 2.5f; // Bruno, 2026-10-24
        public const float BlastKnockdownPush = 4f;      // m/s away from her, PROPOSED
        // The collateral damage of a power (the breakable cover, 2026-10-24), PROPOSED.
        public const float BlastSplashRadius = 1.2f;     // m around the lock point
        public const float BlastSplashPerCharge = 0.8f;  // m more at a full circle
        public const float BlastSplashShare = 0.6f;      // of the power's damage, at the centre
        public const float CoverSplashRadius = 0.8f;     // m around the point where the cover took it

        private void RaiseHit(GameObject victim, Health health, Vector3 point, Vector3 normal, float damage, DamageType type, float charge = 0f)
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
                WasFatal = health != null && health.IsDead,
                Charge = charge
            });
        }

        /// <summary>
        /// The drug key without the drug choice (a room with no InventoryUI, the bots): the drug she needs most that
        /// she carries. The SHORT capsule while a short runs or the bar is half full or more, else the capsule of the
        /// selected power, then of the lowest bar, else the second wind pill when the second wind is used. PROPOSED.
        /// </summary>
        public bool UseDrug()
        {
            DrugType? need = MostNeededDrug();
            return need.HasValue ? UseDrug(need.Value) : Result("drug_none", 0f);
        }

        /// <summary>The drug she carries that has the most work now (the rule of UseDrug), or null. The drug choice opens on it.</summary>
        public DrugType? MostNeededDrug()
        {
            bool shortNeed = (shortController != null && shortController.IsActive) || stats.Short >= PsychicStats.MaxShort * 0.5f;
            var order = new List<DrugType>();
            if (shortNeed) order.Add(DrugType.Red);
            PowerKind selected = Selected != null ? Selected.kind : PowerKind.Blast;
            order.Add(Capsules.Of(selected));
            var others = new List<PowerKind> { PowerKind.Blast, PowerKind.Fire, PowerKind.Telekinesis, PowerKind.Domination };
            others.Remove(selected);
            others.Sort((a, b) => stats.Mk(a).CompareTo(stats.Mk(b)));
            foreach (PowerKind kind in others) order.Add(Capsules.Of(kind));
            if (!shortNeed) order.Add(DrugType.Red);
            order.Add(DrugType.DFelon);
            foreach (DrugType type in order)
            {
                if (drugs.Has(type) && NeedOf(type) == null) return type;
            }
            return null;
        }

        /// <summary>The SHORT capsule takes this share of the SHORT bar away (Bruno, 2026-10-30: "decrease short"). PROPOSED.</summary>
        public const float ShortCapsuleShare = 0.5f;

        /// <summary>
        /// Uses one drug of the type (the inventory screen calls it). Each drug does one thing (Bruno, 2026-10-30):
        /// a capsule fills the bar of its power, the SHORT capsule takes half the SHORT bar away and ends a short that runs, the
        /// second wind pill gives the second wind back. No drug gives HP: only feeding does (Feeding). A drug that
        /// has nothing to do is refused and kept ("mk_full", "short_empty", "second_wind_ready").
        /// </summary>
        public bool UseDrug(DrugType wanted)
        {
            // In the beat between her death and the death screen a drug must not bring her back (found 2026-10-26).
            if (stats.Health.IsDead) return Result("dead", 0f);
            string refusal = NeedOf(wanted);
            if (refusal != null) return Result(refusal, 0f);
            if (!drugs.TryUse(wanted, out DrugType used)) return Result("drug_none", 0f);

            switch (used)
            {
                case DrugType.Nalcon:
                case DrugType.NalconFire:
                case DrugType.NalconLift:
                case DrugType.NalconMind:
                    stats.FillMk(Capsules.PowerOf(used).Value);
                    break;
                case DrugType.Red:
                    shortController?.End(byDrug: true);
                    stats.AddShort(-PsychicStats.MaxShort * ShortCapsuleShare);
                    break;
                case DrugType.DFelon:
                    stats.Health.RearmSecondWind();
                    break;
            }
            LastResult = "drug:" + used;
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Why the drug would do nothing now, or null when it has work to do.</summary>
        public string NeedOf(DrugType type)
        {
            switch (type)
            {
                case DrugType.Nalcon:
                case DrugType.NalconFire:
                case DrugType.NalconLift:
                case DrugType.NalconMind:
                    if (type == DrugType.NalconMind && !EdgarUnlocked) return "unknown";
                    return stats.Mk(Capsules.PowerOf(type).Value) >= PsychicStats.MaxNalcon - 0.001f ? "mk_full" : null;
                case DrugType.Red: return stats.Short <= 0.001f && (shortController == null || !shortController.IsActive) ? "short_empty" : null;
                case DrugType.DFelon:
                    Health h = stats.Health;
                    return !h.IsSecondWindEnabled ? "no_second_wind" : h.IsSecondWindAvailable ? "second_wind_ready" : null;
                default: return null;
            }
        }

        /// <summary>The damage scale of the last power fired (2 under the MK boost). Tests read it.</summary>
        public float LastScale { get; private set; } = 1f;

        /// <summary>
        /// A full charge on the enemy she holds in the air (Bruno, 2026-10-22): it slams into the floor with the
        /// power's full damage (2x under the MK boost). Costs the power's nalcon, like a full charge. PROPOSED.
        /// </summary>
        public bool TrySlam()
        {
            TelekinesisLift lift = HeldLift;
            PowerDefinition power = Selected;
            if (lift == null || power == null) return false;
            float scale = DamageScale;
            if (!stats.TrySpendMk(power.kind, power.nalconCost)) return Result("no_nalcon", 1f);
            if (!lift.Slam(power.baseDamage * scale)) return false;
            LastScale = scale;
            LastResult = "slam";
            RunTelemetry.Event("power_slam", $"{lift.name}:{power.baseDamage * scale:0}");
            OnChanged?.Invoke();
            return true;
        }

        public const float LaunchAimCone = 25f;   // degrees each side of her facing, PROPOSED
        public const float ThrowSlowest = 0.6f, ThrowFastest = 1.4f; // the speed and the free flight of a throw, by its charge, PROPOSED
        public const float CatchRadius = 2.5f;     // metres from her chest, PROPOSED
        public const float CatchNalconCost = 10f;  // PROPOSED
        public const float GrenadeCatchNalconCost = 15f; // PROPOSED
        public const float RocketCatchNalconCost = 25f;  // Bruno, 2026-10-24: "yes, for more MK"
        public const float CatchBuffer = 0.2f;     // seconds an early RMB press stays valid, PROPOSED

        /// <summary>
        /// Throws the enemy or the prop she holds in the air. She aims with her facing: the living enemy inside
        /// LaunchAimCone of her forward, nearest first, else the nearest other enemy in range, else straight ahead.
        /// Both take the damage (a prop has no HP). The charge sets the cost, the damage and the speed (Bruno, 2026-11-07: "a throw
        /// should cost relative to the power the player put into it"). A short press counts as the least charge. After a prop throw
        /// the lock goes back to the nearest enemy.
        /// </summary>
        public bool TryLaunch(float charge)
        {
            TelekinesisLift lift = HeldLift;
            PowerDefinition power = Selected;
            if (lift == null || power == null) return false;
            charge = Mathf.Clamp(charge, power.minCharge, 1f);
            float scale = DamageScale; // the boost is read before the cost lowers the bar
            if (!stats.TrySpendMk(power.kind, power.nalconCost * charge)) return Result("no_nalcon", charge);
            float range = power.range;
            LockOnTargetable victim = FindAimedTarget(lift.transform, range) ?? FindLaunchTarget(lift.transform, range);
            Vector3 destination = victim != null
                ? victim.GetLockPoint()
                : lift.transform.position + Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * 8f;
            Health victimHealth = victim != null ? victim.GetComponent<Health>() ?? victim.GetComponentInParent<Health>() : null;
            LastScale = scale;
            if (!lift.Launch(destination, victimHealth, power.baseDamage * charge * scale, Mathf.Lerp(ThrowSlowest, ThrowFastest, charge))) return false;
            LockOnTargetable thrown = lift.GetComponent<LockOnTargetable>();
            bool prop = thrown != null && thrown.IsProp;
            if (prop && lockOn.CurrentTarget == thrown) lockOn.SetTarget(null);
            LastResult = (prop ? "launch_prop:" : "launch:") + (victim != null ? victim.name : "free");
            LastResultTime = Time.time;
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
                if (candidate == null || !candidate.IsAlive || !candidate.Selectable || candidate.IsAlly || candidate.IsProp) continue;
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
        /// telegraph says when the bullet comes. A grenade in the air is caught the same way (Bruno, 2026-10-24):
        /// it flies back and bursts on the soldiers, for GrenadeCatchNalconCost. A rocket too, for RocketCatchNalconCost.
        /// A rocket goes first, then a grenade, then a bullet: the bigger threat inside the radius.
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
            // The bigger threat first, inside the catch radius: a rocket, then a grenade, then a bullet.
            EnemyRocket rocket = null;
            float rocketDistance = CatchRadius;
            foreach (EnemyRocket r in FindObjectsByType<EnemyRocket>(FindObjectsSortMode.None))
            {
                if (r == null || !r.Catchable) continue;
                float distance = Vector3.Distance(chest, r.transform.position);
                if (distance >= rocketDistance) continue;
                rocket = r;
                rocketDistance = distance;
            }
            if (rocket != null)
            {
                if (!stats.TrySpendMk(PowerKind.Telekinesis, RocketCatchNalconCost)) return Result("no_nalcon", 0f);
                rocket.Return(gameObject);
                LastResult = "catch_rocket";
                LastResultTime = Time.time;
                OnChanged?.Invoke();
                return true;
            }
            EnemyGrenade grenade = null;
            float grenadeDistance = CatchRadius;
            foreach (EnemyGrenade g in FindObjectsByType<EnemyGrenade>(FindObjectsSortMode.None))
            {
                if (g == null || !g.Catchable) continue;
                float distance = Vector3.Distance(chest, g.transform.position);
                if (distance >= grenadeDistance) continue;
                grenade = g;
                grenadeDistance = distance;
            }
            if (grenade != null)
            {
                if (!stats.TrySpendMk(PowerKind.Telekinesis, GrenadeCatchNalconCost)) return Result("no_nalcon", 0f);
                grenade.Return(gameObject);
                LastResult = "catch_grenade";
                LastResultTime = Time.time;
                RunTelemetry.Event("grenade_caught_by_her", grenade.KillerId);
                OnChanged?.Invoke();
                return true;
            }
            if (best == null) return false;
            if (!stats.TrySpendMk(PowerKind.Telekinesis, CatchNalconCost)) return Result("no_nalcon", 0f);
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
                if (candidate == null || !candidate.IsAlive || !candidate.Selectable || candidate.IsAlly || candidate.IsProp) continue;
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
