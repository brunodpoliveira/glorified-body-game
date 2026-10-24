using UnityEngine;
using UnityEngine.AI;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;

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
    /// </summary>
    [RequireComponent(typeof(DummyShooter), typeof(Health))]
    public sealed class SoldierBrain : MonoBehaviour
    {
        public enum Phase { Idle, Move, Shoot, Hold, Dead }

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
        /// <summary>Frames in which the body moved while the gun held a slot. The rule wants zero.</summary>
        public int MovedWhileShootingFrames { get; private set; }
        public int Moves { get; private set; }
        public int ShootPhases { get; private set; }
        public IEnemyBody Body => body;
        public DummyShooter Gun => gun;
        public bool Alerted { get; private set; }
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
            gun.HoldFire = true;
            body?.Stop();
        }

        /// <summary>Stops the soldier for some seconds (a stagger, a drop). The gun lost its aim already.</summary>
        public void Hold(float seconds)
        {
            if (Current == Phase.Dead) return;
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
            if (lift != null && lift.IsLifted)
            {
                if (!suspendedByLift)
                {
                    suspendedByLift = true;
                    body.Suspend(true);
                    gun.HoldFire = true;
                    Current = Phase.Hold;
                    RunTelemetry.Event("soldier_lifted", name);
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
            if (!Engaged && Current != Phase.Hold)
            {
                gun.HoldFire = true;
                if (Current != Phase.Idle && !gun.HoldsSlot) Current = Phase.Idle;
            }
        }

        /// <summary>The FSM fight state calls it every frame.</summary>
        public void TickEngage()
        {
            lastEngageFrame = Time.frameCount;
            if (body == null || Current == Phase.Dead || Current == Phase.Hold || suspendedByLift) return;
            float dt = Time.deltaTime;

            switch (Current)
            {
                case Phase.Idle:
                    StartMove();
                    break;

                case Phase.Move:
                    gun.HoldFire = true;
                    timer -= dt;
                    bool arrived = Time.time - moveStarted >= minMoveSeconds && Flat(transform.position, Spot) <= arriveRadius;
                    bool close = gun.CanMelee && Distance() <= gun.MeleeRange;
                    if (close)
                    {
                        StartShoot(); // she came close: stop and strike
                    }
                    else if (timer <= 0f || arrived)
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
