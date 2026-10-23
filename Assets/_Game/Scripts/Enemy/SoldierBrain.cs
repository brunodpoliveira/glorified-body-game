using UnityEngine;
using UnityEngine.AI;
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
    /// A lift (TelekinesisLift) suspends the body: no navigation, no physics, the gun holds fire. All numbers are
    /// PROPOSED.
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
        [SerializeField, Min(0f)] private float runOver = 8f;          // runs to a spot farther than this
        [SerializeField, Min(0f)] private float minMoveSeconds = 0.6f;  // a new path needs a moment before "arrived" counts
        [SerializeField, Min(0.1f)] private float arriveRadius = 0.7f;

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

        public Phase Current { get; private set; } = Phase.Idle;
        public bool Engaged => Time.frameCount - lastEngageFrame <= 2;
        public Vector3 Spot { get; private set; }
        /// <summary>Frames in which the body moved while the gun held a slot. The rule wants zero.</summary>
        public int MovedWhileShootingFrames { get; private set; }
        public int Moves { get; private set; }
        public int ShootPhases { get; private set; }
        public IEnemyBody Body => body;
        public DummyShooter Gun => gun;

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
        }

        private void OnDisable()
        {
            health.OnDeath?.RemoveListener(OnDeath);
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
                    shootPosition = transform.position;
                }
                else if (Flat(transform.position, shootPosition) > 0.05f) MovedWhileShootingFrames++;
                body.Stop();
            }
            else aimStarted = false;

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
                    if (timer <= 0f || arrived)
                    {
                        if (body.CanSeeTarget && Distance() <= gun.Range) StartShoot();
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

        private float Distance() => Flat(transform.position, body.TargetPosition);

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void StartMove()
        {
            if (gun.HoldsSlot) return; // the shot first, then the legs
            gun.HoldFire = true;
            Vector3 target = body.TargetPosition;
            Vector3 away = transform.position - target;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            float turn = Random.Range(-flankDegrees, flankDegrees);
            Vector3 spot = target + Quaternion.Euler(0f, turn, 0f) * away.normalized * preferredRange;
            if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) spot = hit.position;
            Spot = spot;
            bool run = (spot - transform.position).magnitude > runOver;
            body.MoveTo(spot, run);
            timer = Random.Range(moveSeconds.x, moveSeconds.y);
            moveStarted = Time.time;
            Current = Phase.Move;
            Moves++;
            RunTelemetry.Event("soldier_move", $"{name}:{spot.x:0.0},{spot.z:0.0}{(run ? " run" : "")}");
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
