using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Destruction;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The fight of an FSM soldier: "move or shoot, never both" (Bruno, 2026-10-21). The Invector FSM decides
    /// when the soldier fights (it sees Lucy) and calls TickEngage every frame of that state. The brain then
    /// alternates two phases:
    ///   Move: the gun holds fire, the body walks to a firing spot at preferredRange from Lucy, off to one side
    ///         (flankDegrees), for a few seconds or until it arrives.
    ///   Shoot: the body stops, the gun may aim (laser, lock, the shrinking circle, the shot: design §3.1) when it
    ///         sees her, she is in range and the difficulty gives a slot (ShooterScheduler). After the shot, or
    ///         when no shot starts within shootWaitSeconds, it moves again.
    /// The body never moves while the gun holds a slot (aim or telegraph). A stagger stops it for its seconds.
    /// A lift (TelekinesisLift) suspends the body: no navigation, no physics, the gun holds fire.
    /// Awareness (Bruno, 2026-10-23: "on project warspite, the enemies weren't this dumb"): a soldier is unaware
    /// until it sees Lucy, takes a hit, hears a power hit near it, or hears an alert (EnemyAlert: a fellow soldier's
    /// sight, hit or shot). Then it stays alerted in the room, as Warspite's enemies did: it always knows where she
    /// is (the body tracks her as a fixed target), it calls the others, and it walks to a spot with a clear line
    /// to her. All numbers are PROPOSED.
    /// In the fight it runs, never walks, and sprints to a far spot (Bruno, 2026-10-24: "the enemies move too
    /// slowly ... they don't look like they're in combat at all"). When she comes within melee range during a move,
    /// it stops and the gun picks a melee strike (DummyShooter: the same slot and telegraph as a shot).
    /// Morale (EnemyMorale, design §11.4 and §11.5): a colleague's death shocks it (Shock: it stops, loses its
    /// aim, flinches, barks) and adds fear; a guard at its limit retreats (Retreat: it sprints away from her, then
    /// cowers in a crouch and never attacks again). CorpseCare: it tends a dead colleague (Phase.Care).
    /// </summary>
    [RequireComponent(typeof(DummyShooter), typeof(Health))]
    public sealed class SoldierBrain : MonoBehaviour
    {
        public enum Phase { Idle, Move, Shoot, Hold, Dead, Care, Retreat, Cower }

        [SerializeField, Min(1f)] private float preferredRange = 6f;
        [SerializeField] private Vector2 moveSeconds = new Vector2(1.5f, 3f);
        [SerializeField, Min(0.5f)] private float shootWaitSeconds = 3f;
        [SerializeField, Range(0f, 90f)] private float flankDegrees = 50f;
        [SerializeField, Min(0f)] private float recoverSeconds = 0.6f; // after a drop from the lift
        [SerializeField, Min(0f)] private float sprintOver = 8f;       // sprints to a spot farther than this
        [SerializeField, Min(0f)] private float minMoveSeconds = 0.6f;  // a new path needs a moment before "arrived" counts
        [SerializeField, Min(0.1f)] private float arriveRadius = 0.7f;
        [SerializeField, Min(1f)] private float alertRadius = 25f;
        [SerializeField, Min(1f)] private float hearPowerRadius = 12f;

        private IEnemyBody body;
        private DummyShooter gun;
        private Health health;
        private float timer;
        private int shotsAtShootStart;
        private int lastEngageFrame = -10;
        private bool suspendedByLift;
        private Vector3 shootPosition;
        private float moveStarted;
        private bool aimStarted;
        private bool driftLogged;
        private Transform player;

        public Phase Current { get; private set; } = Phase.Idle;
        public bool Engaged => Time.frameCount - lastEngageFrame <= 2;
        public Vector3 Spot { get; private set; }
        /// <summary>The barricade he stands behind, or null.</summary>
        public Breakable Cover { get; private set; }

        private const float CoverStandOff = 0.9f;    // m behind the barricade's middle
        private const float CoverMinRange = 2.5f;    // m from her: closer is no cover
        private const float CoverExtraRange = 4f;    // m beyond the preferred range
        private const float CoverSearch = 14f;       // m he walks for cover at most
        private const float CoverWalkSeconds = 6f;   // s the walk to cover may take
        private const float CoverArriveRadius = 1f;  // m: the vendor AI stops about 0.8 m short of its point
        private static readonly Dictionary<Breakable, SoldierBrain> coverClaims = new Dictionary<Breakable, SoldierBrain>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCoverClaims() => coverClaims.Clear();
        /// <summary>Frames in which the body moved while the gun held a slot. The rule wants zero.</summary>
        public int MovedWhileShootingFrames { get; private set; }
        public int Moves { get; private set; }
        public int ShootPhases { get; private set; }
        public IEnemyBody Body => body;
        public DummyShooter Gun => gun;
        public bool Alerted { get; private set; }
        public int ShockCount { get; private set; }
        public string RetreatReason { get; private set; } = "";
        /// <summary>Free to tend a body: alive, not retreating or cowering, not held, no aim.</summary>
        public bool CanCare => Current != Phase.Dead && Current != Phase.Retreat && Current != Phase.Cower
            && Current != Phase.Hold && Current != Phase.Care && !suspendedByLift && !gun.HoldsSlot;
        public bool Retreating => Current == Phase.Retreat || Current == Phase.Cower;
        public string AlertReason { get; private set; } = "";
        /// <summary>Where she is: her real position once alerted, else the last seen position.</summary>
        public Vector3 TargetPosition => Alerted && player != null ? player.position : body.TargetPosition;

        private void Awake()
        {
            body = GetComponent<IEnemyBody>();
            gun = GetComponent<DummyShooter>();
            health = GetComponent<Health>();
            gun.HoldFire = true;
            gun.RequireLineOfSight = true;
            gun.OnStaggered += Hold;
        }

        private void OnEnable()
        {
            health.OnDeath ??= new UnityEngine.Events.UnityEvent();
            health.OnDeath.AddListener(OnDeath);
            health.OnDamaged ??= new UnityEngine.Events.UnityEvent<float>();
            health.OnDamaged.AddListener(OnHurt);
            EnemyAlert.OnAlert += OnAlertHeard;
            HitEventBus.OnHit += OnHitHeard;
            gun.OnFired += OnOwnShot;
        }

        private void OnDisable()
        {
            health.OnDeath?.RemoveListener(OnDeath);
            health.OnDamaged?.RemoveListener(OnHurt);
            EnemyAlert.OnAlert -= OnAlertHeard;
            HitEventBus.OnHit -= OnHitHeard;
            gun.OnFired -= OnOwnShot;
        }

        // ── Awareness ─────────────────────────────────────────────────

        /// <summary>The soldier knows where Lucy is from now on, and calls the others when it is the first to know.</summary>
        public void Alert(string reason, bool callOthers)
        {
            if (Alerted || Current == Phase.Dead) return;
            if (player == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                player = p != null ? p.transform : null;
                if (player == null) return;
            }
            Alerted = true;
            AlertReason = reason;
            body.Track(player);
            RunTelemetry.Event("soldier_alerted", $"{name}:{reason}");
            if (callOthers) EnemyAlert.Raise(transform.position, alertRadius, reason);
        }

        private void OnAlertHeard(Vector3 origin, float radius, string reason)
        {
            if (Alerted || Current == Phase.Dead) return;
            if (Flat(transform.position, origin) <= radius) Alert("heard " + reason, callOthers: false);
        }

        private void OnHurt(float amount)
        {
            if (health.IsDead) return;
            Alert("hurt", callOthers: true);
        }

        private void OnOwnShot(Vector3 origin, Vector3 direction)
        {
            EnemyAlert.Raise(transform.position, alertRadius, "gunshot");
        }

        /// <summary>A power that hits something near it is loud: a blast on a crate, a burn on a fellow soldier.</summary>
        private void OnHitHeard(HitInfo hit)
        {
            if (Alerted || Current == Phase.Dead || hit.Attacker == null || !hit.Attacker.CompareTag("Player")) return;
            if (Flat(transform.position, hit.HitPoint) <= hearPowerRadius) Alert("power", callOthers: true);
        }

        private void OnDestroy()
        {
            if (gun != null) gun.OnStaggered -= Hold;
        }

        private void OnDeath()
        {
            Current = Phase.Dead;
            ReleaseCover();
            gun.HoldFire = true;
            body?.Stop();
        }

        /// <summary>Stops the soldier for some seconds (a stagger, a drop). The gun lost its aim already.</summary>
        public void Hold(float seconds)
        {
            if (Current == Phase.Dead) return;
            if (Current == Phase.Retreat || Current == Phase.Cower)
            {
                gun.HoldFire = true;
                return; // a frightened one does not come back to the fight
            }
            if (Current == Phase.Care) GetComponent<CorpseCare>()?.Stop("held");
            Current = Phase.Hold;
            timer = Mathf.Max(timer, seconds);
            gun.HoldFire = true;
            body?.Stop();
        }

        private void Update()
        {
            if (body == null || Current == Phase.Dead) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            TelekinesisLift lift = GetComponent<TelekinesisLift>();
            // Lifted, or limp on the floor and standing up (EnemyRagdoll): the body does nothing.
            if ((lift != null && lift.IsLifted) || RagdollBodies.IsDown(this))
            {
                if (!suspendedByLift)
                {
                    suspendedByLift = true;
                    body.Suspend(true);
                    gun.HoldFire = true;
                    if (Current == Phase.Care) GetComponent<CorpseCare>()?.Stop("down");
                    if (!Retreating) Current = Phase.Hold; // a frightened one stays frightened after the fall
                    RunTelemetry.Event(lift != null && lift.IsLifted ? "soldier_lifted" : "soldier_down", name);
                }
                timer = recoverSeconds;
                return;
            }
            if (suspendedByLift)
            {
                suspendedByLift = false;
                body.Suspend(false);
                Hold(recoverSeconds);
            }

            if (!Alerted && body.CanSeeTarget) Alert("sight", callOthers: true);

            if (Current == Phase.Retreat)
            {
                gun.HoldFire = true;
                timer -= dt;
                if (timer <= 0f || (timer < 7.4f && Flat(transform.position, Spot) <= 1f)) // Invector's own arrival flag lies
                {
                    Current = Phase.Cower;
                    body.Stop();
                    body.Crouch(true);
                    RunTelemetry.Event("soldier_cower", name);
                }
                return;
            }
            if (Current == Phase.Cower)
            {
                gun.HoldFire = true;
                body.Stop();
                body.Face(transform.position - TargetPosition); // its back to her, head down
                return;
            }

            if (Current == Phase.Hold)
            {
                body.Stop();
                timer -= dt;
                if (timer <= 0f) Current = Phase.Idle;
            }

            // The rule: no step while the gun aims or counts down. Measured from the start of the aim: more than
            // 5 cm off that point is a step.
            if (gun.HoldsSlot)
            {
                if (!aimStarted)
                {
                    aimStarted = true;
                    driftLogged = false;
                    shootPosition = transform.position;
                    body.Brace(true);
                }
                else if (Flat(transform.position, shootPosition) > 0.05f)
                {
                    MovedWhileShootingFrames++;
                    if (!driftLogged)
                    {
                        driftLogged = true;
                        Debug.LogWarning($"[SoldierBrain] {name} drifted {Flat(transform.position, shootPosition):0.00} m during its aim (gun {gun.Current}, phase {Current}).");
                    }
                }
                body.Stop();
            }
            else if (aimStarted)
            {
                aimStarted = false;
                body.Brace(false);
            }

            // Out of the fight state (the FSM searches or idles): the gun holds fire after its shot.
            if (!Engaged && Current != Phase.Hold && Current != Phase.Care)
            {
                gun.HoldFire = true;
                if (Current != Phase.Idle && !gun.HoldsSlot) Current = Phase.Idle;
            }
        }

        // ── Morale and care ───────────────────────────────────────────

        /// <summary>A colleague died in sight: it stops, loses its aim, flinches and barks.</summary>
        public void Shock(float seconds, bool gruesome)
        {
            if (Current == Phase.Dead || Retreating) return;
            ShockCount++;
            if (Current == Phase.Care) GetComponent<CorpseCare>()?.Stop("shock");
            gun.Stagger(seconds); // the aim breaks; OnStaggered holds the body for the seconds
            Hold(seconds);
            body?.Flinch();
            // PLACEHOLDER lines: the writer owns the barks.
            string[] lines = gruesome ? new[] { "OH GOD...", "WHAT DID SHE DO TO HIM?", "JESUS!" } : new[] { "NO!", "MAN DOWN!" };
            Bark.Say(gameObject, lines[Random.Range(0, lines.Length)]);
            RunTelemetry.Event("soldier_shocked", $"{name}:{seconds:0.0}s{(gruesome ? ":gruesome" : "")}");
        }

        /// <summary>Its fear reached its rank's limit: it runs from her and cowers, for the rest of the room.</summary>
        public void Retreat(string reason)
        {
            if (Current == Phase.Dead || Retreating) return;
            if (Current == Phase.Care) GetComponent<CorpseCare>()?.Stop("retreat");
            RetreatReason = reason;
            gun.HoldFire = true;
            if (gun.HoldsSlot) gun.Stagger(0.1f); // drop the aim
            // The aim braced the body (kinematic): the retreat phase skips the release, and a braced body runs in
            // place (Bruno, 2026-10-24).
            if (aimStarted)
            {
                aimStarted = false;
                body.Brace(false);
            }
            Current = Phase.Retreat;
            Spot = FleeSpot();
            body.Crouch(false);
            body.MoveTo(Spot, MoveSpeed.Sprint);
            timer = 8f;
            Bark.Say(gameObject, "FALL BACK! FALL BACK!"); // PLACEHOLDER line
            RunTelemetry.Event("soldier_retreat", $"{name}:{reason} to {Spot.x:0.0},{Spot.z:0.0}");
        }

        /// <summary>The NavMesh spot farthest from her, within 14 m of it.</summary>
        private Vector3 FleeSpot()
        {
            Vector3 her = TargetPosition;
            Vector3 best = transform.position;
            float bestScore = Flat(best, her);
            for (int i = 0; i < 16; i++)
            {
                foreach (float r in new[] { 6f, 10f, 14f })
                {
                    Vector3 candidate = transform.position + Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward * r;
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;
                    float score = Flat(hit.position, her);
                    if (score > bestScore) { bestScore = score; best = hit.position; }
                }
            }
            return best;
        }

        public bool TryBeginCare()
        {
            if (!CanCare) return false;
            Current = Phase.Care;
            gun.HoldFire = true;
            return true;
        }

        public void EndCare()
        {
            if (Current == Phase.Care) Current = Phase.Idle;
        }

        /// <summary>The FSM fight state calls it every frame.</summary>
        public void TickEngage()
        {
            lastEngageFrame = Time.frameCount;
            if (body == null || Current == Phase.Dead || Current == Phase.Hold || suspendedByLift) return;
            if (Current == Phase.Care || Retreating) { gun.HoldFire = true; return; }
            float dt = Time.deltaTime;

            switch (Current)
            {
                case Phase.Idle:
                    StartMove();
                    break;

                case Phase.Move:
                    gun.HoldFire = true;
                    timer -= dt;
                    bool arrived = Time.time - moveStarted >= minMoveSeconds && Flat(transform.position, Spot) <= (Cover != null ? CoverArriveRadius : arriveRadius);
                    bool close = gun.CanMelee && Distance() <= gun.MeleeRange;
                    if (close)
                    {
                        StartShoot(); // she came close: stop and strike
                    }
                    // A walk to cover runs to the end (CoverWalkSeconds at most): no shot from the open on the way.
                    else if ((timer <= 0f && (Cover == null || Time.time - moveStarted >= CoverWalkSeconds)) || arrived)
                    {
                        if (ClearLine() && Distance() <= gun.Range) StartShoot();
                        else StartMove();
                    }
                    break;

                case Phase.Shoot:
                    body.Stop();
                    // The walk carries the body for a moment after the stop: no aim until it stands.
                    gun.HoldFire = body.IsMoving && !gun.HoldsSlot;
                    timer -= dt;
                    bool fired = gun.ShotsFired > shotsAtShootStart && !gun.HoldsSlot;
                    if (fired || (timer <= 0f && !gun.HoldsSlot)) StartMove();
                    break;
            }
        }

        /// <summary>The FSM leaves the fight state.</summary>
        public void Disengage()
        {
            gun.HoldFire = true;
            if (Current == Phase.Move || Current == Phase.Shoot) Current = Phase.Idle;
        }

        private float Distance() => Flat(transform.position, TargetPosition);

        /// <summary>A clear line from the gun to her chest now (the gun turns to her, so the field of view does not count).</summary>
        private bool ClearLine() => gun.Sees(TargetPosition + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight);

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void StartMove()
        {
            if (gun.HoldsSlot) return; // the shot first, then the legs
            gun.HoldFire = true;
            Vector3 spot = PickSpot();
            Spot = spot;
            bool sprint = (spot - transform.position).magnitude > sprintOver;
            body.MoveTo(spot, sprint ? MoveSpeed.Sprint : MoveSpeed.Run);
            timer = Random.Range(moveSeconds.x, moveSeconds.y);
            moveStarted = Time.time;
            Current = Phase.Move;
            Moves++;
            RunTelemetry.Event("soldier_move", $"{name}:{spot.x:0.0},{spot.z:0.0}{(sprint ? " sprint" : " run")}");
        }

        /// <summary>
        /// A firing spot at preferredRange from her (or closer), off to one side. It prefers a spot with a clear line
        /// to her chest, the nearest such one; without one, a random flank spot.
        /// </summary>
        private Vector3 PickSpot()
        {
            Vector3 target = TargetPosition;
            Vector3 chest = target + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight;
            if (TryCoverSpot(target, chest, out Vector3 covered)) return covered;
            Vector3 away = transform.position - target;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            away.Normalize();
            int blockers = LayerMask.GetMask("Default");
            float start = Random.Range(-flankDegrees, flankDegrees);
            Vector3 best = Vector3.zero;
            float bestCost = float.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                float turn = start + i * 45f;
                foreach (float range in new[] { preferredRange, preferredRange * 0.65f })
                {
                    Vector3 candidate = target + Quaternion.Euler(0f, turn, 0f) * away * range;
                    if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;
                    if (Physics.Linecast(hit.position + Vector3.up * GlorifiedBody.Characters.Body.MuzzleHeight, chest, blockers, QueryTriggerInteraction.Ignore)) continue;
                    float cost = Flat(transform.position, hit.position) + (i == 0 ? 0f : 0.5f);
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        best = hit.position;
                    }
                }
            }
            if (bestCost < float.MaxValue) return best;
            Vector3 fallback = target + Quaternion.Euler(0f, start, 0f) * away * preferredRange;
            if (NavMesh.SamplePosition(fallback, out NavMeshHit near, 2.5f, NavMesh.AllAreas)) fallback = near.position;
            return fallback;
        }

        /// <summary>
        /// Cover first (Bruno, 2026-10-24: "cover only enemies, on purpose"): a spot behind an intact chest-high
        /// barricade, on its far side from her, with a line over it from the muzzle to her chest. Her powers stop on
        /// the barricade until it breaks; then he moves on. One soldier for each barricade. Numbers PROPOSED.
        /// </summary>
        private bool TryCoverSpot(Vector3 target, Vector3 chest, out Vector3 spot)
        {
            spot = Vector3.zero;
            int blockers = LayerMask.GetMask("Default");
            Breakable best = null;
            float bestCost = float.MaxValue;
            foreach (Breakable cover in Breakable.All)
            {
                if (cover == null || !cover.IsCover) continue;
                if (coverClaims.TryGetValue(cover, out SoldierBrain owner) && owner != null && owner != this && owner.Current != Phase.Dead) continue;
                Vector3 centre = cover.Bounds.center;
                Vector3 away = centre - target;
                away.y = 0f;
                float range = away.magnitude;
                if (range < CoverMinRange || range > preferredRange + CoverExtraRange) continue;
                away /= range;
                Vector3 candidate = new Vector3(centre.x, transform.position.y, centre.z) + away * CoverStandOff;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.7f, NavMesh.AllAreas)) continue;
                if (Flat(hit.position, candidate) > 0.6f) continue;
                if (Physics.Linecast(hit.position + Vector3.up * GlorifiedBody.Characters.Body.MuzzleHeight, chest, blockers, QueryTriggerInteraction.Ignore)) continue;
                float cost = Flat(transform.position, hit.position);
                if (cost > CoverSearch || cost >= bestCost) continue;
                best = cover;
                bestCost = cost;
                spot = hit.position;
            }
            if (best == null)
            {
                ReleaseCover();
                return false;
            }
            if (best != Cover)
            {
                ReleaseCover();
                coverClaims[best] = this;
                Cover = best;
                RunTelemetry.Event("soldier_cover", $"{name}:{best.Id}");
            }
            return true;
        }

        private void ReleaseCover()
        {
            if (Cover != null && coverClaims.TryGetValue(Cover, out SoldierBrain owner) && owner == this) coverClaims.Remove(Cover);
            Cover = null;
        }

        private void StartShoot()
        {
            body.Stop();
            shotsAtShootStart = gun.ShotsFired;
            timer = shootWaitSeconds;
            Current = Phase.Shoot;
            ShootPhases++;
            RunTelemetry.Event("soldier_shoot", name);
        }
    }
}
