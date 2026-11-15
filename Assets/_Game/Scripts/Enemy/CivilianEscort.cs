using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The escort (Bruno, round 160: "i wonder if a soldier can 'drag' a living civvie out of danger easily as well. the
    /// soldier would be doing this pose as they retreat; looking forward, gun raised one arm, free arm covering and pushing
    /// the civvie to safety"; DEMO_PLAN §3.3 item 8). A soldier in a fight, with a teammate to cover him, takes a living
    /// civilian near her out of danger: he goes between her and the civilian, then the civilian walks away from her and he
    /// walks backward behind it, facing her, the gun in his right hand, his left hand on the civilian's shoulder
    /// (EnemyGun.FreeHandTo). It ends at a spot SafeDistance from her, or when the time is up. A hurt, a death, a lift, a
    /// shock or her too close ends it (SoldierBrain.StopCare). The civilian walks by itself: no ragdoll (the plan's read).
    /// His barks are the writer's (round 162): "escorts a civilian" at the start, "civilians present" while one is near
    /// her in the fight, with the CIV tag of the civilian. He holds his fire while he leads ("move or shoot, never both").
    /// Only the soldiers do it. Numbers PROPOSED.
    /// </summary>
    [RequireComponent(typeof(SoldierBrain))]
    public sealed class CivilianEscort : MonoBehaviour
    {
        public enum Step { None, GoTo, Lead }

        public const float ScanSeconds = 0.5f;
        public const float CivilianReach = 8f;   // m from him to the civilian
        public const float DangerReach = 9f;     // m from her: the civilian is in danger
        public const float TooClose = 2.5f;      // m from her: too late, she has it
        public const float SafeDistance = 11f;   // m from her: the civilian is safe
        public const float StandOff = 0.45f;      // m from the civilian, on her side: within his reach
        public const float AheadMost = 1.0f, AheadAgain = 0.8f; // m: the civilian waits this far ahead of him, and walks again this near
        public const float PushAhead = 0.6f;      // m: his point leads the civilian along the way, so he keeps walking
        public const float BackSurface = 0.15f;   // m from the civilian's chest bone to its back
        public const float GoToSeconds = 5f, LeadSeconds = 16f, CooldownSeconds = 20f;
        public const float CoverReach = 15f;     // m: a teammate this near covers him

        public Step Current { get; private set; }
        public bool Active => Current != Step.None;
        public CivilianBrain Civilian { get; private set; }
        public int Escorts { get; private set; }
        public int Delivered { get; private set; }
        public static int Total { get; private set; }

        private static readonly HashSet<CivilianBrain> escorted = new HashSet<CivilianBrain>();
        private static CivilianBrain[] civilians = new CivilianBrain[0];
        private static float civiliansAt = -9f;

        private SoldierBrain brain;
        private Health health;
        private EnemyGun gun;
        private Transform her;
        private Vector3 safe;
        private float stepUntil, nextScan, cooldownUntil, restrafeAt;
        private ObstacleAvoidanceType? civilianAvoidance;
        private bool civilianWaits;

        private void Awake()
        {
            brain = GetComponent<SoldierBrain>();
            health = GetComponent<Health>();
            gun = GetComponent<EnemyGun>();
            nextScan = Time.time + Random.value * ScanSeconds; // the soldiers look in different frames
        }

        private void OnEnable()
        {
            health.OnDamaged ??= new UnityEngine.Events.UnityEvent<float>();
            health.OnDamaged.AddListener(OnHurt);
        }

        private void OnDisable()
        {
            health.OnDamaged?.RemoveListener(OnHurt);
            Stop("disabled");
        }

        private void OnHurt(float amount) => Stop("hurt");

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;
            if (her == null)
            {
                GameObject lucy = GameObject.FindWithTag("Player");
                if (lucy == null) return;
                her = lucy.transform;
            }
            if (Current == Step.None)
            {
                if (Time.time < nextScan) return;
                nextScan = Time.time + ScanSeconds;
                Scan();
                return;
            }
            if (Civilian == null || IsGone(Civilian)) { Stop("civilian gone"); return; }
            if (Flat(Civilian.transform.position, her.position) < 1.5f) { Stop("she is on it"); return; }
            if (Time.time > stepUntil) { Stop("time"); return; }
            if (Current == Step.GoTo) GoTo();
            else Lead();
        }

        private void LateUpdate()
        {
            // The free hand on the civilian's near shoulder while he leads it (EnemyGun reaches it after the gun's own hand).
            if (gun == null) return;
            if (Current != Step.Lead || Civilian == null) { gun.FreeHandTo = null; return; }
            // The hand on the civilian's back, on his side: the chest's height, the body's surface (about 0.15 m from its middle).
            Animator a = Civilian.GetComponent<Animator>();
            Transform chest = a != null && a.isHuman ? (a.GetBoneTransform(HumanBodyBones.UpperChest) ?? a.GetBoneTransform(HumanBodyBones.Chest)) : null;
            Vector3 middle = chest != null ? chest.position : Civilian.transform.position + Vector3.up * 1.3f;
            Vector3 toHim = Vector3.ProjectOnPlane(transform.position - middle, Vector3.up).normalized;
            gun.FreeHandTo = middle + toHim * BackSurface;
        }

        // ── The choice ────────────────────────────────────────────────

        private void Scan()
        {
            if (Time.time < cooldownUntil || !brain.Alerted || !brain.CanCare || health.IsDead || RagdollBodies.IsDown(this)) return;
            if (Time.time - civiliansAt > 1f)
            {
                civilians = FindObjectsByType<CivilianBrain>(FindObjectsSortMode.None);
                civiliansAt = Time.time;
            }
            CivilianBrain best = null;
            float bestDistance = CivilianReach;
            int near = 0;
            foreach (CivilianBrain c in civilians)
            {
                if (c == null || IsGone(c) || c.Hides || escorted.Contains(c)) continue;
                float toHer = Flat(c.transform.position, her.position);
                if (toHer > DangerReach || !GlorifiedBody.Rooms.Room.Same(c.transform.position, transform.position)) continue;
                near++;
                float d = Flat(c.transform.position, transform.position);
                if (toHer < TooClose || !c.CanCare || d >= bestDistance) continue;
                best = c;
                bestDistance = d;
            }
            if (near == 0) return;
            BarkVoice voice = BarkVoice.Of(gameObject);
            // "Folks, floor, now..." to a group, "Ma'am, down..." to one (barks.csv, civilians present, round 162).
            voice?.SayNow(BarkSituation.CiviliansPresent, 25f, 0.5f, 2.4f, near > 1 ? "group" : (best != null ? best.CivKind : ""));
            if (best == null || !Covered() || !SafeSpot(best, out safe)) return;
            if (!brain.TryBeginCare()) return;
            if (!best.TryBeginCare()) { brain.EndCare(); return; }
            Civilian = best;
            escorted.Add(best);
            Escorts++;
            Total++;
            Current = Step.GoTo;
            stepUntil = Time.time + GoToSeconds;
            best.Body.Stop();
            RunTelemetry.Event("escort_start", $"{name}:{best.name}");
            voice?.SayNow(BarkSituation.EscortsCivilian, 10f, 1f, 2.4f, best.CivKind);
        }

        /// <summary>Another fighter in the fight near him: someone covers the escort.</summary>
        private bool Covered()
        {
            foreach (SoldierBrain other in FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None))
                if (other != brain && other.Alerted && other.Current != SoldierBrain.Phase.Dead && Flat(other.transform.position, transform.position) <= CoverReach)
                    return true;
            return false;
        }

        /// <summary>A spot on the NavMesh away from her, SafeDistance from her or as far as the room lets it, with a route.</summary>
        private bool SafeSpot(CivilianBrain c, out Vector3 spot)
        {
            spot = Vector3.zero;
            Vector3 from = c.transform.position;
            Vector3 away = Vector3.ProjectOnPlane(from - her.position, Vector3.up);
            away = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
            float start = Flat(from, her.position), bestGain = 3f;
            var path = new NavMeshPath();
            foreach (float angle in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f })
                foreach (float metres in new[] { SafeDistance - start + 1f, 7f, 5f })
                {
                    if (metres < 2f) continue;
                    Vector3 point = from + Quaternion.Euler(0f, angle, 0f) * away * metres;
                    if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
                    float gain = Flat(hit.position, her.position) - start;
                    if (gain <= bestGain) continue;
                    if (!NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    spot = hit.position;
                    bestGain = gain;
                }
            return bestGain > 3f;
        }

        // ── The escort ────────────────────────────────────────────────

        private void GoTo()
        {
            Vector3 toHer = Vector3.ProjectOnPlane(her.position - Civilian.transform.position, Vector3.up).normalized;
            Vector3 meet = Civilian.transform.position + toHer * StandOff;
            if (Flat(transform.position, meet) < 0.6f)
            {
                Current = Step.Lead;
                stepUntil = Time.time + LeadSeconds;
                // The two agents would keep 0.8 m apart: the civilian gives way to him while he leads it.
                if (Civilian.TryGetComponent(out NavMeshAgent agent)) { civilianAvoidance = agent.obstacleAvoidanceType; agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance; }
                Civilian.Body.MoveTo(safe, MoveSpeed.Walk);
                RunTelemetry.Event("escort_lead", $"{name}:{Civilian.name}");
                return;
            }
            if (Time.time >= restrafeAt)
            {
                restrafeAt = Time.time + 0.3f;
                brain.Body.MoveTo(meet, MoveSpeed.Sprint);
            }
            Civilian.Body.Face(transform.position - Civilian.transform.position);
        }

        private void Lead()
        {
            if (Flat(Civilian.transform.position, her.position) >= SafeDistance || Flat(Civilian.transform.position, safe) < 0.8f)
            {
                Delivered++;
                Stop("safe");
                return;
            }
            if (Time.time < restrafeAt) return;
            restrafeAt = Time.time + 0.25f;
            // He walks backward behind the civilian, on her side of it, facing her; the civilian walks to the spot.
            Vector3 toHer = Vector3.ProjectOnPlane(her.position - Civilian.transform.position, Vector3.up).normalized;
            Vector3 way = Vector3.ProjectOnPlane(safe - Civilian.transform.position, Vector3.up);
            way = way.sqrMagnitude > 0.01f ? way.normalized : -toHer;
            brain.Body.StrafeTo(Civilian.transform.position + toHer * StandOff + way * PushAhead, her.position - transform.position);
            // The civilian walks at his pace: his hand stays on it (2026-11-15: a running civilian left his hand 3 m behind).
            float gap = Flat(transform.position, Civilian.transform.position);
            if (gap > AheadMost) civilianWaits = true;
            else if (gap < AheadAgain) civilianWaits = false;
            if (civilianWaits) Civilian.Body.Stop();
            else Civilian.Body.MoveTo(safe, MoveSpeed.Walk);
        }

        /// <summary>Ends the escort (the brain calls it on a hurt, a lift, a shock, a door, a retreat). The civilian is free again.</summary>
        public void Stop(string why)
        {
            if (!Active) return;
            RunTelemetry.Event("escort_stop", $"{name}:{(Civilian != null ? Civilian.name : "?")}:{why}");
            Current = Step.None;
            if (gun != null) gun.FreeHandTo = null;
            if (Civilian != null)
            {
                if (civilianAvoidance.HasValue && Civilian.TryGetComponent(out NavMeshAgent agent)) agent.obstacleAvoidanceType = civilianAvoidance.Value;
                civilianAvoidance = null;
                escorted.Remove(Civilian);
                if (!IsGone(Civilian)) Civilian.EndCare();
            }
            Civilian = null;
            cooldownUntil = Time.time + CooldownSeconds;
            brain.EndCare();
            if (brain.Body != null && !health.IsDead) brain.Body.Stop();
        }

        private static bool IsGone(CivilianBrain c) => c.Current == CivilianBrain.Phase.Dead || c.IsDeadFigure || RagdollBodies.IsDown(c);

        private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            escorted.Clear();
            civilians = new CivilianBrain[0];
            civiliansAt = -9f;
            Total = 0;
        }
    }
}
