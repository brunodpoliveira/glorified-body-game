using System.Collections;
using UnityEngine;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
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
                    yield return new WaitForSecondsRealtime(0.35f);
                    c.Pad.ReleaseAll();
                    yield return new WaitForSecondsRealtime(0.6f);
                    break;
                case "switch":
                    c.Pad.Tap(InputManager.GameAction.SwitchTarget);
                    yield return new WaitForSecondsRealtime(0.2f);
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
                    yield return new WaitForSecondsRealtime(0.25f);
                    LockOnTargetable other = NearestLivingTarget(c.Lucy.position, c.Powers != null && c.Powers.HeldLift != null ? c.Powers.HeldLift.transform : null);
                    if (other != null) yield return TurnTo(c, other.transform.position, 8f, 1.2f);
                    c.Pad.Tap(InputManager.GameAction.Charge);
                    yield return new WaitForSecondsRealtime(0.3f);
                    break;
                case "catch":
                    c.Pad.Tap(InputManager.GameAction.PowerTelekinesis);
                    yield return null;
                    yield return CatchOneBullet(c, 20f);
                    break;
                case "capsule":
                    yield return WalkTo(c, c.Director.CapsulePoint, run: false, radius: 0.35f);
                    yield return new WaitForSecondsRealtime(0.2f);
                    int before = c.Drugs != null ? c.Drugs.Count(DrugType.Nalcon) : -1;
                    c.Pad.Tap(InputManager.GameAction.UseDrug);
                    yield return new WaitForSecondsRealtime(0.2f);
                    Debug.Log($"[TutorialDrive] capsule: at {c.Lucy.position}, capsule {(GameObject.Find("Capsule") != null ? "still there" : "taken")}, nalcon drugs before the use {before}, result '{c.Powers?.LastResult}'.");
                    break;
                case "log":
                {
                    yield return WalkTo(c, c.Director.LogPoint, run: false, radius: 0.3f);
                    ItemPickup log = FindPickup(c.Director.LogItemId);
                    if (log == null) break;
                    yield return TurnTo(c, log.transform.position, 4f);
                    c.Pad.Tap(InputManager.GameAction.Interact); // "Take it?"
                    yield return new WaitForSecondsRealtime(0.3f);
                    c.Moment?.Invoke("log_confirm");
                    c.Pad.Tap(InputManager.GameAction.Interact); // take: the page opens
                    yield return new WaitForSecondsRealtime(0.5f);
                    c.Moment?.Invoke("log_reader");
                    c.Pad.Tap(InputManager.GameAction.Interact); // close the page
                    yield return new WaitForSecondsRealtime(0.3f);
                    break;
                }
                case "scan_image":
                    yield return ScanAt(c, c.Director.BarsPoint, c.Director.DoorScanId, 1.0f);
                    break;
                case "scan_video":
                    yield return ScanAt(c, c.Director.MonitorsPoint, c.Director.MonitorsScanId, -1f);
                    break;
                case "short":
                    c.Pad.Tap(InputManager.GameAction.Charge);
                    yield return new WaitForSecondsRealtime(1.2f);
                    c.Pad.Tap(InputManager.GameAction.UseDrug);
                    yield return new WaitForSecondsRealtime(0.3f);
                    break;
                default:
                    yield return new WaitForSecondsRealtime(0.5f);
                    break;
            }
        }

        /// <summary>
        /// Holds and releases the direction keys like a hand, not like a switch: a key released stays released
        /// for longer than the double-tap window (two presses of a direction key inside 0.4 s are a dodge: the
        /// first walk driver dashed her sideways on every threshold crossing), and a turn key stays held until
        /// the aim is nearly exact.
        /// </summary>
        private sealed class KeyHand
        {
            private const float ReleaseHold = 0.45f; // over TankController.DefaultDoubleTapWindow
            private readonly VirtualPad pad;
            private readonly bool[] held = new bool[16];
            private readonly float[] releasedAt = new float[16];

            public KeyHand(VirtualPad pad)
            {
                this.pad = pad;
                for (int i = 0; i < releasedAt.Length; i++) releasedAt[i] = -10f;
            }

            public void Set(InputManager.GameAction action, bool wanted)
            {
                int i = (int)action;
                if (held[i] == wanted) return;
                if (wanted && Time.realtimeSinceStartup - releasedAt[i] < ReleaseHold) return;
                if (!wanted) releasedAt[i] = Time.realtimeSinceStartup;
                held[i] = wanted;
                pad.SetHeld(action, wanted);
            }

            public void Release()
            {
                for (int i = 0; i < held.Length; i++)
                {
                    if (!held[i]) continue;
                    held[i] = false;
                    releasedAt[i] = Time.realtimeSinceStartup;
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
            yield return TurnTo(c, target.FocusPoint, 4f);
            PsychicScanner scanner = c.Lucy.GetComponent<PsychicScanner>();
            c.Pad.Tap(InputManager.GameAction.Scan);
            yield return new WaitForSecondsRealtime(0.2f);
            if (holdSeconds >= 0f)
            {
                yield return new WaitForSecondsRealtime(holdSeconds);
                c.Moment?.Invoke("scan_" + scanId);
                c.Pad.Tap(InputManager.GameAction.Scan);
                yield return new WaitForSecondsRealtime(0.3f);
            }
            else
            {
                float t0 = Time.realtimeSinceStartup;
                bool shot = false;
                while (scanner != null && scanner.IsViewing && Time.realtimeSinceStartup - t0 < 8f)
                {
                    if (!shot && Time.realtimeSinceStartup - t0 > 1.5f)
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
            foreach (ItemPickup pickup in ItemPickup.All)
            {
                if (pickup != null && pickup.item != null && pickup.item.itemId == itemId) return pickup;
            }
            return null;
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
            float t0 = Time.realtimeSinceStartup;
            var hand = new KeyHand(c.Pad);
            int turning = 0;
            while (Time.realtimeSinceStartup - t0 < timeout)
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
            float t0 = Time.realtimeSinceStartup;
            Vector3 start = c.Lucy.position;
            var hand = new KeyHand(c.Pad);
            int turning = 0;
            while (Time.realtimeSinceStartup - t0 < timeout && Flat(c.Lucy.position, point) > radius)
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
            if (left > radius) Debug.LogWarning($"[TutorialDrive] WalkTo {point} timed out: from {start} to {c.Lucy.position}, {left:0.00} m left, facing {SignedYawTo(c, point):0}°.");
            yield return null;
        }

        /// <summary>Holds the attack until the circle is full, then releases (the power fires at the lock).</summary>
        public static IEnumerator HoldChargeFull(Context c, float timeout = 5f)
        {
            float t0 = Time.realtimeSinceStartup;
            c.Pad.SetHeld(InputManager.GameAction.Charge, true);
            while (Time.realtimeSinceStartup - t0 < timeout && (c.Powers == null || c.Powers.Charge < 0.999f)) yield return null;
            yield return null;
            c.Pad.SetHeld(InputManager.GameAction.Charge, false);
            yield return null;
            yield return null;
        }

        /// <summary>Waits for a bullet near her chest and presses the defend key: the catch.</summary>
        public static IEnumerator CatchOneBullet(Context c, float timeout)
        {
            float t0 = Time.realtimeSinceStartup;
            int bulletsSeen = 0, taps = 0;
            float closest = float.MaxValue;
            while (Time.realtimeSinceStartup - t0 < timeout)
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
                if (candidate == null || !candidate.IsAlive || !candidate.Selectable) continue;
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
