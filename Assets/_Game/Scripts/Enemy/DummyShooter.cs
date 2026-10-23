using UnityEngine;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The training room dummy: a shooter with the canon telegraph (design §3.1). It aims with a laser line
    /// that follows Lucy, then the aim locks (it cannot aim again), a circle spins and shrinks to the moment it
    /// fires, and the bullet goes to the locked point. The circle is on the HUD, over the locked point (her), with
    /// an edge arrow when the shooter is outside the frame (PlayerHud, Bruno 2026-10-20: the camera may not show
    /// the enemy, and the circle is her heads-up to dodge). The difficulty rule limits how many shooters
    /// aim at once (ShooterScheduler). A blast staggers it, a lift stops it. Numbers are PROPOSED.
    /// The FSM soldier (SoldierBrain) uses the same gun: HoldFire keeps it from a new aim while the soldier
    /// moves ("move or shoot, never both", Bruno 2026-10-21), and RequireLineOfSight stops an aim through a wall.
    /// The training dummies leave both off.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class DummyShooter : MonoBehaviour, IStaggerable
    {
        public enum State { Idle, Aiming, Telegraph, Cooldown, Staggered, Dead }

        [SerializeField] private string enemyId = "dummy_shooter";
        [SerializeField, Min(1f)] private float range = 10f;
        [SerializeField, Min(0f)] private float aimSeconds = 0.5f;
        [SerializeField, Min(0.1f)] private float telegraphSeconds = 1.0f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 2.5f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 12f;
        [SerializeField, Min(0f)] private float projectileDamage = 15f;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool requireLineOfSight;
        [SerializeField] private LayerMask sightBlockers = 1; // Default: the room

        private Health health;
        private Transform player;
        private LineRenderer laser;
        private float timer;
        private Vector3 aimPoint;

        public State Current { get; private set; } = State.Idle;
        public bool IsDead => Current == State.Dead;
        public bool HoldsSlot => Current == State.Aiming || Current == State.Telegraph;
        public Vector3 AimPoint => aimPoint;
        public float TelegraphProgress => Current == State.Telegraph ? 1f - Mathf.Clamp01(timer / telegraphSeconds) : 0f;
        public int ShotsFired { get; private set; }
        public string EnemyId => enemyId;
        public float Range => range;
        /// <summary>No new aim while true (the soldier moves). An aim already started goes on to its shot.</summary>
        public bool HoldFire { get; set; }
        public bool RequireLineOfSight { get => requireLineOfSight; set => requireLineOfSight = value; }
        /// <summary>Raised on a stagger with its seconds (the soldier stops for that long).</summary>
        public event System.Action<float> OnStaggered;
        public event System.Action OnAimStarted;
        public event System.Action OnAimEnded;
        /// <summary>A shot leaves the muzzle: the origin and the direction (the gun's flash, smoke and sound).</summary>
        public event System.Action<Vector3, Vector3> OnFired;
        /// <summary>The muzzle of a gun model (EnemyGun). Without one, a point in front of the chest.</summary>
        public Transform MuzzleOverride { get; set; }
        public Vector3 Muzzle => MuzzleOverride != null ? MuzzleOverride.position : transform.position + Vector3.up * Body.MuzzleHeight + transform.forward * 0.5f;

        private void Awake()
        {
            health = GetComponent<Health>();
            laser = MakeLine("Laser", new Color(1f, 0.1f, 0.1f), 0.03f, 2);
            laser.enabled = false;
        }

        private void OnEnable()
        {
            health.OnDeath ??= new UnityEngine.Events.UnityEvent();
            health.OnDeath.AddListener(Die);
            if (!IsDead) Current = State.Idle; // a fresh cycle, no slot held
        }

        private void OnDisable()
        {
            health.OnDeath?.RemoveListener(Die);
            // Off means out of the fight: the slot goes back and the aim resets.
            if (!IsDead) CancelAim(State.Idle, 0f);
        }

        private LineRenderer MakeLine(string name, Color color, float width, int points)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            var line = obj.AddComponent<LineRenderer>();
            line.positionCount = points;
            line.startWidth = width;
            line.endWidth = width;
            line.useWorldSpace = true;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader != null) line.material = new Material(shader) { color = color };
            line.startColor = color;
            line.endColor = color;
            return line;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || IsDead) return;

            TelekinesisLift lift = GetComponent<TelekinesisLift>();
            if (lift != null && lift.IsLifted)
            {
                if (HoldsSlot) CancelAim(State.Cooldown, cooldownSeconds * 0.5f);
                return;
            }

            if (player == null)
            {
                GameObject playerObject = GameObject.FindWithTag(playerTag);
                player = playerObject != null ? playerObject.transform : null;
                if (player == null) return;
            }

            Vector3 chest = player.position + Vector3.up * Body.ChestHeight;
            float distance = Vector3.Distance(transform.position, player.position);
            if (!HoldFire || HoldsSlot) FaceYaw(player.position);

            switch (Current)
            {
                case State.Idle:
                    if (!HoldFire && distance <= range && PlayerAlive() && Sees(chest) && ShooterScheduler.TryAcquire(this))
                    {
                        Current = State.Aiming;
                        timer = aimSeconds;
                        laser.enabled = true;
                        RunTelemetry.Event("enemy_aim", enemyId);
                        OnAimStarted?.Invoke();
                    }
                    break;

                case State.Aiming:
                    aimPoint = chest;
                    DrawLaser();
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        Current = State.Telegraph;
                        timer = telegraphSeconds;
                        RunTelemetry.Event("enemy_telegraph", enemyId);
                    }
                    break;

                case State.Telegraph:
                    DrawLaser();
                    timer -= dt;
                    if (timer <= 0f) Fire();
                    break;

                case State.Cooldown:
                case State.Staggered:
                    timer -= dt;
                    if (timer <= 0f) Current = State.Idle;
                    break;
            }
        }

        /// <summary>A clear line from the muzzle to her chest, when the shooter needs one.</summary>
        public bool Sees(Vector3 chest)
        {
            if (!requireLineOfSight) return true;
            Vector3 from = Muzzle;
            return !Physics.Linecast(from, chest, sightBlockers, QueryTriggerInteraction.Ignore);
        }

        private bool PlayerAlive()
        {
            Health playerHealth = player != null ? player.GetComponentInChildren<Health>() : null;
            return playerHealth != null && !playerHealth.IsDead;
        }

        private void FaceYaw(Vector3 target)
        {
            Vector3 to = target - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), 240f * Time.deltaTime);
        }

        private void DrawLaser()
        {
            laser.SetPosition(0, Muzzle);
            laser.SetPosition(1, aimPoint);
        }

        private void Fire()
        {
            Vector3 origin = Muzzle;
            EnemyProjectile.Launch(origin, aimPoint - origin, projectileSpeed, projectileDamage, gameObject, enemyId);
            ShotsFired++;
            OnFired?.Invoke(origin, (aimPoint - origin).normalized);
            RunTelemetry.Event("enemy_fired", enemyId);
            CancelAim(State.Cooldown, cooldownSeconds);
        }

        private void CancelAim(State next, float seconds)
        {
            bool wasAiming = HoldsSlot;
            if (HoldsSlot) ShooterScheduler.Release(this);
            laser.enabled = false;
            Current = next;
            timer = seconds;
            if (wasAiming) OnAimEnded?.Invoke();
        }

        public void Stagger(float seconds)
        {
            if (IsDead || seconds <= 0f) return;
            CancelAim(State.Staggered, seconds);
            RunTelemetry.Event("enemy_staggered", $"{enemyId}:{seconds:0.00}");
            OnStaggered?.Invoke(seconds);
        }

        private void Die()
        {
            if (IsDead) return;
            CancelAim(State.Dead, 0f);
            GameObject playerObject = GameObject.FindWithTag(playerTag);
            PsychicStats stats = playerObject != null ? playerObject.GetComponent<PsychicStats>() : null;
            if (stats != null) stats.NotifyKill(enemyId);
            else RunTelemetry.Event("enemy_killed", enemyId);
        }
    }
}
