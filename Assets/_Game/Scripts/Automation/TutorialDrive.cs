using System.Collections;
using UnityEngine;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Destruction;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;
using GlorifiedBody.Tutorial;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// Plays the tutorial the way a player does, through the virtual pad: the walk, the run, the dodge, the
    /// target switch, the three faculties, the catch, the capsule, the short. The tutorial test and the tutorial
    /// bot share it. Each Drive does one attempt of a step; the caller waits for the director to move on and
    /// tries again when it did not.
    /// </summary>
    public static class TutorialDrive
    {
        public const float StepTimeout = 30f;

        public sealed class Context
        {
            public VirtualPad Pad;
            public TutorialDirector Director;
            public Transform Lucy;
            public TankController Tank;
            public PsychicPowers Powers;
            public DrugInventory Drugs;
            /// <summary>Called at the moments worth a shot (the question, the open log, the scan): the bot shoots them.</summary>
            public System.Action<string> Moment;

            public static Context From(VirtualPad pad, TutorialDirector director)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                return new Context
                {
                    Pad = pad,
                    Director = director,
                    Lucy = player != null ? player.transform : null,
                    Tank = player != null ? player.GetComponent<TankController>() : null,
                    Powers = player != null ? player.GetComponent<PsychicPowers>() : null,
                    Drugs = player != null ? player.GetComponent<DrugInventory>() : null
                };
            }
        }

        /// <summary>One attempt of the step with the given id.</summary>
        public static IEnumerator Drive(Context c, string stepId)
        {
            switch (stepId)
            {
                case "walk":
                    yield return WalkTo(c, c.Director.BarsPoint, run: false, radius: c.Director.ReachRadius - 0.3f);
                    break;
                case "run":
                    yield return WalkTo(c, c.Director.MonitorsPoint, run: true, radius: c.Director.ReachRadius - 0.3f);
                    break;
                case "dodge":
                    c.Pad.SetHeld(InputManager.GameAction.TurnLeft, true);
                    yield return null;
                    yield return null;
                    c.Pad.Tap(InputManager.GameAction.Dodge);
                    yield return BotClock.Wait(0.35f);
                    c.Pad.ReleaseAll();
                    yield return BotClock.Wait(0.6f);
                    break;
                case "switch":
                    c.Pad.Tap(InputManager.GameAction.SwitchTarget);
                    yield return BotClock.Wait(0.2f);
                    break;
                case "blast":
                    c.Pad.Tap(InputManager.GameAction.PowerBlast);
                    yield return null;
                    yield return HoldChargeFull(c);
                    break;
                case "fire":
                    c.Pad.Tap(InputManager.GameAction.PowerFire);
                    yield return null;
                    yield return HoldChargeFull(c);
                    break;
                case "lift_throw":
                    c.Pad.Tap(InputManager.GameAction.PowerTelekinesis);
                    yield return null;
                    yield return HoldChargeFull(c);
                    yield return BotClock.Wait(0.25f);
                    LockOnTargetable other = NearestLivingTarget(c.Lucy.position, c.Powers != null && c.Powers.HeldLift != null ? c.Powers.HeldLift.transform : null);
                    if (other != null) yield return TurnTo(c, other.transform.position, 8f, 1.2f);
                    c.Pad.Tap(InputManager.GameAction.Charge);
                    yield return BotClock.Wait(0.3f);
                    break;
                case "slam":
                    c.Pad.Tap(InputManager.GameAction.PowerTelekinesis);
                    yield return null;
                    if (c.Powers == null || c.Powers.HeldLift == null)
                    {
                        yield return HoldChargeFull(c); // lift the lock target
                        yield return BotClock.Wait(0.5f);
                    }
                    yield return HoldChargeFull(c); // a full circle on the floater: the slam
                    yield return BotClock.Wait(0.6f);
                    break;
                case "prop":
                {
                    c.Pad.Tap(InputManager.GameAction.PowerTelekinesis);
                    yield return null;
                    if (c.Powers == null || c.Powers.HeldLift == null)
                    {
                        // The switch reaches the crate only with mk_orzabal selected.
                        LockOnController lockOn = c.Lucy.GetComponent<LockOnController>();
                        for (int i = 0; i < 6 && lockOn != null && (lockOn.CurrentTarget == null || !lockOn.CurrentTarget.IsProp); i++)
                        {
                            c.Pad.Tap(InputManager.GameAction.SwitchTarget);
                            yield return BotClock.Wait(0.15f);
                        }
                        if (lockOn == null || lockOn.CurrentTarget == null || !lockOn.CurrentTarget.IsProp)
                        {
                            Debug.LogWarning($"[TutorialDrive] prop: the switch did not reach the crate (lock {lockOn?.TargetName}).");
                            break;
                        }
                        // A part charge lifts it for less MK: the slam before may have emptied the bar.
                        yield return WaitForNalcon(c, 12f, 12f);
                        yield return HoldCharge(c, 0.35f);
                        yield return BotClock.Wait(0.5f);
                    }
                    if (c.Powers == null || c.Powers.HeldLift == null) break;
                    LockOnTargetable dummy = NearestLivingTarget(c.Lucy.position, c.Powers.HeldLift.transform);
                    if (dummy != null) yield return TurnTo(c, dummy.transform.position, 8f, 1.5f);
                    c.Pad.Tap(InputManager.GameAction.Charge);
                    yield return BotClock.Wait(0.6f);
                    c.Moment?.Invoke("prop_thrown");
                    break;
                }
                case "catch":
                    c.Pad.Tap(InputManager.GameAction.PowerTelekinesis);
                    yield return null;
                    yield return CatchOneBullet(c, 20f);
                    break;
                case "grenade":
                {
                    // Stand 4.6 m from the gun (it lobs no grenade closer than 3.5 m), wait for the grenade, run 3.4 m.
                    yield return WalkTo(c, c.Director.MonitorsPoint, run: true, radius: 0.4f);
                    float t0 = BotClock.Now;
                    EnemyGrenade grenade = null;
                    while (grenade == null && BotClock.Now - t0 < 20f)
                    {
                        grenade = System.Linq.Enumerable.FirstOrDefault(Object.FindObjectsByType<EnemyGrenade>(FindObjectsSortMode.None), g => g.Catchable && !g.IsGas);
                        yield return null;
                    }
                    if (grenade == null) { Debug.LogWarning("[TutorialDrive] grenade: no grenade in 20 s."); break; }
                    c.Moment?.Invoke("grenade_ring");
                    yield return WalkTo(c, new Vector3(-2.6f, 0f, 12.6f), run: true, radius: 0.4f, timeout: 3f);
                    yield return BotClock.Wait(1.2f);
                    break;
                }
                case "shield":
                {
                    // Behind the shield dummy (it faces south), lock it, a full blast.
                    EnemyShield shield = c.Director.Shield;
                    if (shield == null) { Debug.LogWarning("[TutorialDrive] shield: no shield dummy."); break; }
                    Vector3 behind = shield.transform.position - shield.transform.forward * 2.4f;
                    yield return WalkTo(c, behind, run: true, radius: 0.4f);
                    LockOnController lockOn = c.Lucy.GetComponent<LockOnController>();
                    LockOnTargetable target = shield.GetComponent<LockOnTargetable>();
                    for (int i = 0; i < 6 && lockOn != null && lockOn.CurrentTarget != target; i++)
                    {
                        c.Pad.Tap(InputManager.GameAction.SwitchTarget);
                        yield return BotClock.Wait(0.15f);
                    }
                    if (lockOn != null && lockOn.CurrentTarget != target) Debug.LogWarning($"[TutorialDrive] shield: the switch did not reach it (lock {lockOn.TargetName}).");
                    c.Pad.Tap(InputManager.GameAction.PowerBlast);
                    yield return null;
                    yield return WaitForNalcon(c, 25f, 12f);
                    yield return HoldChargeFull(c);
                    yield return BotClock.Wait(0.3f);
                    c.Moment?.Invoke("shield_flanked");
                    // Back past the north end of the dummy row: a straight walk to the capsule runs into Target_1.
                    yield return WalkTo(c, new Vector3(-1.2f, 0f, 15.6f), run: false, radius: 0.5f);
                    break;
                }
                case "cover":
                {
                    // Down the lane west of the parallel bars, east past the bars' south posts (z 11.5), then south of
                    // the barricade: lock Target_3 behind it and blast until the barricade breaks. Back the same way.
                    Breakable cover = c.Director.Cover;
                    if (cover == null) { Debug.LogWarning("[TutorialDrive] cover: no barricade."); break; }
                    yield return WalkTo(c, new Vector3(-1.2f, 0f, 11.0f), run: true, radius: 0.4f);
                    yield return WalkTo(c, new Vector3(2.2f, 0f, 11.1f), run: false, radius: 0.4f);
                    LockOnController lockOn = c.Lucy.GetComponent<LockOnController>();
                    GameObject behindObject = GameObject.Find("Target_3");
                    LockOnTargetable behind = behindObject != null ? behindObject.GetComponent<LockOnTargetable>() : null;
                    for (int i = 0; i < 6 && lockOn != null && behind != null && lockOn.CurrentTarget != behind; i++)
                    {
                        c.Pad.Tap(InputManager.GameAction.SwitchTarget);
                        yield return BotClock.Wait(0.15f);
                    }
                    if (lockOn != null && lockOn.CurrentTarget != behind) Debug.LogWarning($"[TutorialDrive] cover: the switch did not reach Target_3 (lock {lockOn.TargetName}).");
                    c.Pad.Tap(InputManager.GameAction.PowerBlast);
                    yield return null;
                    for (int i = 0; i < 5 && !cover.IsBroken; i++)
                    {
                        yield return WaitForNalcon(c, 25f, 12f);
                        yield return HoldChargeFull(c);
                        yield return BotClock.Wait(0.4f);
                        Debug.Log($"[TutorialDrive] cover: blast {i + 1}, result '{c.Powers?.LastResult}', barricade {cover.Health:0}/{cover.MaxHealth:0}.");
                    }
                    c.Moment?.Invoke("cover_broken");
                    yield return WalkTo(c, new Vector3(-1.2f, 0f, 11.0f), run: false, radius: 0.4f);
                    yield return WalkTo(c, new Vector3(-1.2f, 0f, 15.6f), run: false, radius: 0.5f);
                    break;
                }
                case "capsule":
                {
                    // The capsule asks first now (Bruno, 2026-10-22: pills are grabbables, not walk-over).
                    ItemPickup capsule = FindPickup("nalcon");
                    if (capsule != null)
                    {
                        yield return WalkTo(c, capsule.Point, run: false, radius: 0.8f);
                        yield return TakeFacing(c, capsule);
                    }
                    yield return BotClock.Wait(0.2f);
                    int before = c.Drugs != null ? c.Drugs.Count(DrugType.Nalcon) : -1;
                    c.Pad.Tap(InputManager.GameAction.UseDrug);
                    yield return BotClock.Wait(0.2f);
                    Debug.Log($"[TutorialDrive] capsule: at {c.Lucy.position}, capsule {(GameObject.Find("Capsule") != null ? "still there" : "taken")}, nalcon drugs before the use {before}, result '{c.Powers?.LastResult}'.");
                    break;
                }
                case "log":
                {
                    yield return WalkTo(c, c.Director.LogPoint, run: false, radius: 0.3f);
                    ItemPickup log = FindPickup(c.Director.LogItemId);
                    if (log == null) break;
                    yield return TurnTo(c, log.Point, 4f);
                    c.Pad.Tap(InputManager.GameAction.Interact); // "Take it?"
                    yield return BotClock.Wait(0.3f);
                    c.Moment?.Invoke("log_confirm");
                    c.Pad.Tap(InputManager.GameAction.Interact); // take: the page opens
                    yield return BotClock.Wait(0.5f);
                    c.Moment?.Invoke("log_reader");
                    c.Pad.Tap(InputManager.GameAction.Interact); // close the page
                    yield return BotClock.Wait(0.3f);
                    break;
                }
                case "scan_image":
                    yield return ViaWestLane(c, c.Director.DoorPoint);
                    yield return ScanAt(c, c.Director.DoorPoint, c.Director.DoorScanId, 1.0f);
                    break;
                case "key":
                {
                    yield return WalkTo(c, c.Director.KeyPoint, run: false, radius: 0.3f);
                    ItemPickup key = FindPickup(c.Director.KeyItemId);
                    if (key != null) yield return TakeFacing(c, key);
                    break;
                }
                case "door":
                {
                    yield return ViaWestLane(c, c.Director.DoorPoint);
                    yield return WalkTo(c, c.Director.DoorPoint, run: false, radius: 0.3f);
                    if (c.Director.Door == null) break;
                    yield return TurnTo(c, c.Director.Door.Point, 4f);
                    c.Pad.Tap(InputManager.GameAction.Interact); // "Use the test key?"
                    yield return BotClock.Wait(0.3f);
                    c.Moment?.Invoke("door_confirm");
                    c.Pad.Tap(InputManager.GameAction.Interact); // yes: the leaf slides
                    yield return BotClock.Wait(1.2f);
                    c.Moment?.Invoke("door_open");
                    break;
                }
                case "inventory":
                    c.Pad.Tap(InputManager.GameAction.Inventory); // the room stops
                    yield return BotClock.Wait(0.4f);
                    c.Moment?.Invoke("inventory");
                    c.Pad.Tap(InputManager.GameAction.TurnRight);
                    yield return BotClock.Wait(0.3f);
                    c.Moment?.Invoke("inventory_next");
                    c.Pad.Tap(InputManager.GameAction.Inventory); // closed
                    yield return BotClock.Wait(0.3f);
                    break;
                case "scan_video":
                    yield return ScanAt(c, c.Director.MonitorsPoint, c.Director.MonitorsScanId, -1f);
                    break;
                case "short":
                    c.Pad.Tap(InputManager.GameAction.Charge);
                    yield return BotClock.Wait(1.2f);
                    c.Pad.Tap(InputManager.GameAction.UseDrug);
                    yield return BotClock.Wait(0.3f);
                    break;
                default:
                    yield return BotClock.Wait(0.5f);
                    break;
            }
        }

        /// <summary>
        /// Holds and releases the direction keys like a hand, not like a switch: a key released stays released
        /// for longer than the double-tap window (two presses of a direction key inside 0.4 s are a dodge: the
        /// first walk driver dashed her sideways on every threshold crossing), and a turn key stays held until
        /// the aim is nearly exact. The release times are shared by every hand: two walks in a row (the west
        /// lane, then the door) used two hands, and the second pressed the turn key again inside the window (a
        /// dodge in the bot run of 2026-10-22, late).
        /// </summary>
        private sealed class KeyHand
        {
            private const float ReleaseHold = 0.45f; // over TankController.DefaultDoubleTapWindow
            private readonly VirtualPad pad;
            private static readonly int ActionCount = System.Enum.GetValues(typeof(InputManager.GameAction)).Length;
            private static readonly float[] releasedAt = NewReleaseTimes();
            private readonly bool[] held = new bool[ActionCount];

            private static float[] NewReleaseTimes()
            {
                var times = new float[ActionCount];
                for (int i = 0; i < times.Length; i++) times[i] = -10f;
                return times;
            }

            public KeyHand(VirtualPad pad)
            {
                this.pad = pad;
            }

            public void Set(InputManager.GameAction action, bool wanted)
            {
                int i = (int)action;
                if (held[i] == wanted) return;
                if (wanted && BotClock.Now - releasedAt[i] < ReleaseHold) return;
                if (!wanted) releasedAt[i] = BotClock.Now;
                held[i] = wanted;
                pad.SetHeld(action, wanted);
            }

            public void Release()
            {
                for (int i = 0; i < held.Length; i++)
                {
                    if (!held[i]) continue;
                    held[i] = false;
                    releasedAt[i] = BotClock.Now;
                }
                pad.ReleaseAll();
            }
        }

        /// <summary>
        /// Walks her to a point, turns her to the scan target, presses Scan. A picture closes after holdSeconds
        /// with a second press; a clip (holdSeconds below zero) plays to its end.
        /// </summary>
        public static IEnumerator ScanAt(Context c, Vector3 standPoint, string scanId, float holdSeconds)
        {
            yield return WalkTo(c, standPoint, run: false, radius: 0.35f);
            Scannable target = FindScannable(scanId);
            if (target == null) yield break;
            // The walk stops up to 0.35 m short, on the side she came from: from the west lane that left her
            // 1.55 m from the door focus, past the 1.5 m reach. Close in to 80 percent of the reach.
            if (Flat(c.Lucy.position, target.FocusPoint) > target.range * 0.85f)
            {
                yield return WalkTo(c, target.FocusPoint, run: false, radius: target.range * 0.8f);
            }
            yield return TurnTo(c, target.FocusPoint, 4f);
            PsychicScanner scanner = c.Lucy.GetComponent<PsychicScanner>();
            c.Pad.Tap(InputManager.GameAction.Scan);
            yield return BotClock.Wait(0.2f);
            if (holdSeconds >= 0f)
            {
                yield return BotClock.Wait(holdSeconds);
                c.Moment?.Invoke("scan_" + scanId);
                c.Pad.Tap(InputManager.GameAction.Scan);
                yield return BotClock.Wait(0.3f);
            }
            else
            {
                float t0 = BotClock.Now;
                bool shot = false;
                while (scanner != null && scanner.IsViewing && BotClock.Now - t0 < 8f)
                {
                    if (!shot && BotClock.Now - t0 > 1.5f)
                    {
                        c.Moment?.Invoke("scan_" + scanId);
                        shot = true;
                    }
                    yield return null;
                }
            }
            if (scanner != null && scanner.LastClosedId != scanId) Debug.LogWarning($"[TutorialDrive] scan {scanId}: last result '{scanner.LastResult}', focus {(scanner.Focus != null ? scanner.Focus.scanId : "none")}, Lucy at {c.Lucy.position} facing {c.Lucy.eulerAngles.y:0}.");
        }

        public static ItemPickup FindPickup(string itemId)
        {
            foreach (Interactable target in Interactable.All)
            {
                if (target is ItemPickup pickup && pickup.item != null && pickup.item.itemId == itemId) return pickup;
            }
            return null;
        }

        /// <summary>Turns her to a pickup and takes it: Interact asks, Interact again says yes.</summary>
        public static IEnumerator TakeFacing(Context c, ItemPickup pickup)
        {
            yield return TurnTo(c, pickup.Point, 4f);
            c.Pad.Tap(InputManager.GameAction.Interact);
            yield return BotClock.Wait(0.3f);
            c.Pad.Tap(InputManager.GameAction.Interact);
            yield return BotClock.Wait(0.3f);
        }

        public static Scannable FindScannable(string scanId)
        {
            foreach (Scannable target in Scannable.All)
            {
                if (target != null && target.scanId == scanId) return target;
            }
            return null;
        }

        /// <summary>Turns her toward a point with the turn keys, within the tolerance in degrees.</summary>
        public static IEnumerator TurnTo(Context c, Vector3 point, float toleranceDegrees = 6f, float timeout = 5f)
        {
            float t0 = BotClock.Now;
            var hand = new KeyHand(c.Pad);
            int turning = 0;
            while (BotClock.Now - t0 < timeout)
            {
                float signed = SignedYawTo(c, point);
                if (turning == 0 && Mathf.Abs(signed) <= toleranceDegrees) break;
                if (turning != 0 && Mathf.Abs(signed) <= 2f) break;
                turning = signed < 0f ? -1 : 1;
                hand.Set(InputManager.GameAction.TurnLeft, turning < 0);
                hand.Set(InputManager.GameAction.TurnRight, turning > 0);
                yield return null;
            }
            hand.Release();
            yield return null;
        }

        /// <summary>Walks (or runs) her to a point: the turn keys keep her aimed, the forward key moves her, until the point is within the radius.</summary>
        public static IEnumerator WalkTo(Context c, Vector3 point, bool run, float radius, float timeout = 14f)
        {
            float t0 = BotClock.Now;
            Vector3 start = c.Lucy.position;
            var hand = new KeyHand(c.Pad);
            int turning = 0;
            while (BotClock.Now - t0 < timeout && Flat(c.Lucy.position, point) > radius)
            {
                float signed = SignedYawTo(c, point);
                // Start a turn past 10 degrees, keep it until 2 degrees (no flutter around one threshold).
                if (turning == 0 && Mathf.Abs(signed) > 10f) turning = signed < 0f ? -1 : 1;
                if (turning != 0 && (Mathf.Abs(signed) <= 2f || Mathf.Sign(signed) != turning)) turning = 0;
                hand.Set(InputManager.GameAction.TurnLeft, turning < 0);
                hand.Set(InputManager.GameAction.TurnRight, turning > 0);
                bool ahead = Mathf.Abs(signed) < 60f;
                hand.Set(InputManager.GameAction.Forward, ahead);
                hand.Set(InputManager.GameAction.Run, run && ahead);
                yield return null;
            }
            hand.Release();
            float left = Flat(c.Lucy.position, point);
            if (left > radius)
            {
                var near = new System.Collections.Generic.List<string>();
                foreach (Collider col in Physics.OverlapSphere(c.Lucy.position + Vector3.up * 0.6f, 0.9f, ~0, QueryTriggerInteraction.Ignore))
                    if (!col.transform.IsChildOf(c.Lucy)) near.Add($"{col.name}({LayerMask.LayerToName(col.gameObject.layer)})");
                Debug.LogWarning($"[TutorialDrive] WalkTo {point} timed out: from {start} to {c.Lucy.position}, {left:0.00} m left, facing {SignedYawTo(c, point):0}°, near: {string.Join(", ", near)}.");
            }
            yield return null;
        }

        /// <summary>
        /// The lane west of the parallel bars. A walk from the stands to the door goes through it first: the
        /// straight line from the north passed close to the bars' south posts and stopped there twice.
        /// </summary>
        public static readonly Vector3 WestLane = new Vector3(-2.5f, 0f, 12.4f);

        /// <summary>From the north half of the gym, walks her to the west lane first.</summary>
        public static IEnumerator ViaWestLane(Context c, Vector3 destination)
        {
            if (c.Lucy.position.z > WestLane.z + 0.5f && destination.z < WestLane.z)
            {
                yield return WalkTo(c, WestLane, run: false, radius: 0.4f);
            }
        }

        /// <summary>Waits until her MK reaches the amount (the regen refills it), up to the timeout.</summary>
        public static IEnumerator WaitForNalcon(Context c, float amount, float timeout)
        {
            PsychicStats stats = c.Lucy.GetComponent<PsychicStats>();
            float t0 = BotClock.Now;
            while (stats != null && stats.Nalcon < amount && BotClock.Now - t0 < timeout) yield return null;
        }

        /// <summary>Holds the attack until the circle reaches the level, then releases.</summary>
        public static IEnumerator HoldCharge(Context c, float level, float timeout = 5f)
        {
            float t0 = BotClock.Now;
            c.Pad.SetHeld(InputManager.GameAction.Charge, true);
            while (BotClock.Now - t0 < timeout && (c.Powers == null || c.Powers.Charge < level)) yield return null;
            c.Pad.SetHeld(InputManager.GameAction.Charge, false);
            yield return null;
            yield return null;
        }

        /// <summary>Holds the attack until the circle is full, then releases (the power fires at the lock).</summary>
        public static IEnumerator HoldChargeFull(Context c, float timeout = 5f)
        {
            float t0 = BotClock.Now;
            c.Pad.SetHeld(InputManager.GameAction.Charge, true);
            while (BotClock.Now - t0 < timeout && (c.Powers == null || c.Powers.Charge < 0.999f)) yield return null;
            yield return null;
            c.Pad.SetHeld(InputManager.GameAction.Charge, false);
            yield return null;
            yield return null;
        }

        /// <summary>Waits for a bullet near her chest and presses the defend key: the catch.</summary>
        public static IEnumerator CatchOneBullet(Context c, float timeout)
        {
            float t0 = BotClock.Now;
            int bulletsSeen = 0, taps = 0;
            float closest = float.MaxValue;
            while (BotClock.Now - t0 < timeout)
            {
                if (c.Powers != null && c.Powers.LastResult == "catch") yield break;
                Vector3 chest = c.Lucy.position + Vector3.up * Body.ChestHeight;
                foreach (EnemyProjectile bullet in Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
                {
                    if (bullet == null || !bullet.Catchable) continue;
                    bulletsSeen++;
                    float distance = Vector3.Distance(chest, bullet.transform.position);
                    closest = Mathf.Min(closest, distance);
                    if (distance < PsychicPowers.CatchRadius * 0.85f)
                    {
                        c.Pad.Tap(InputManager.GameAction.Defend);
                        taps++;
                        break;
                    }
                }
                yield return null;
            }
            DummyShooter shooter = c.Director != null ? c.Director.Shooter : null;
            string shooterState = shooter != null ? $"{shooter.name} enabled={shooter.enabled} state={shooter.Current} shots={shooter.ShotsFired} alive={!shooter.IsDead} at {shooter.transform.position} {Vector3.Distance(shooter.transform.position, c.Lucy.position):0.0} m away" : "no shooter";
            Debug.LogWarning($"[TutorialDrive] catch timed out: Lucy at {c.Lucy.position}, bullet frames seen {bulletsSeen}, closest {closest:0.00} m, defend taps {taps}, result '{c.Powers?.LastResult}', nalcon {(c.Powers != null ? c.Powers.GetComponent<PsychicStats>().Nalcon : -1f):0}; {shooterState}");
        }

        public static LockOnTargetable NearestLivingTarget(Vector3 from, Transform exclude)
        {
            LockOnTargetable best = null;
            float bestDistance = float.MaxValue;
            foreach (LockOnTargetable candidate in TargetRegistry.All)
            {
                if (candidate == null || !candidate.IsAlive || !candidate.Selectable || candidate.IsProp) continue;
                if (exclude != null && (candidate.transform == exclude || candidate.transform.IsChildOf(exclude))) continue;
                float distance = Flat(from, candidate.transform.position);
                if (distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        private static float SignedYawTo(Context c, Vector3 point)
        {
            Vector3 to = point - c.Lucy.position;
            to.y = 0f;
            Vector3 forward = c.Lucy.forward;
            forward.y = 0f;
            return Vector3.SignedAngle(forward, to, Vector3.up);
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
