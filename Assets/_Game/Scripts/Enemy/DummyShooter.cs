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
    /// Attack kinds (Bruno, 2026-10-24): the weapon decides the ranged attack (a pistol shot, a 3-round rifle
    /// burst, a grenade that arcs to her feet and bursts after a short fuse), and a soldier close to her strikes in
    /// melee instead. Every kind follows the same rules: a difficulty slot, the telegraph circle, the time from
    /// the laser to the attack (the difficulty: easy 1.5 s, normal 1.0 s, hard 0.8 s), then a rest. The HUD shows
    /// the kind as an icon next to the circle and on the edge arrow.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class DummyShooter : MonoBehaviour, IStaggerable
    {
        public enum State { Idle, Aiming, Telegraph, Firing, Cooldown, Staggered, Dead }
        public enum Weapon { Pistol, Rifle, GrenadeLauncher, GasLauncher, ArmourLaunchers }
        public enum Attack { None, Shot, Burst, Grenade, Melee, Gas, Rocket }

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
        [SerializeField] private bool useDifficultyTiming = true;

        [Header("Weapon and melee (PROPOSED)")]
        [SerializeField] private Weapon weapon = Weapon.Pistol;
        [SerializeField, Min(1)] private int burstRounds = 3;
        [SerializeField, Min(0.02f)] private float burstSpacing = 0.1f;
        [SerializeField, Min(0f)] private float burstRoundDamage = 8f;
        [SerializeField, Min(0f)] private float grenadeDamage = 35f;
        [SerializeField, Min(0.5f)] private float grenadeRadius = 2.5f;
        [SerializeField, Min(0.2f)] private float grenadeFlightSeconds = 0.9f;
        [SerializeField, Min(0f)] private float grenadeFuseSeconds = 0.5f;
        [SerializeField, Min(0f)] private float grenadeMinDistance = 3.5f; // closer, the grenadier would hit himself
        [SerializeField, Min(1f)] private float rocketSpeed = 10f;
        [SerializeField, Min(0f)] private float rocketDamage = 40f;
        [SerializeField, Min(0.5f)] private float rocketRadius = 2f;
        [SerializeField, Min(0.5f)] private float gasRadius = 2.5f;
        [SerializeField, Min(0.5f)] private float gasSeconds = 6f;
        [SerializeField, Min(0f)] private float gasDamagePerSecond = 6f;
        [SerializeField] private bool canMelee;
        [SerializeField, Min(0.5f)] private float meleeRange = 1.8f;   // an attack starts as melee within this
        [SerializeField, Min(0.5f)] private float meleeReach = 2.2f;   // the strike lands within this
        [SerializeField, Range(10f, 180f)] private float meleeArc = 70f; // degrees each side of his facing
        [SerializeField, Min(0f)] private float meleeDamage = 20f;

        private Health health;
        private Transform player;
        private LineRenderer laser;
        private float timer;
        private float telegraphLength = 1f;
        private Vector3 aimPoint;
        private int roundsLeft;
        private bool nextIsRocket; // the power armour alternates frag grenades and rockets

        public State Current { get; private set; } = State.Idle;
        public bool IsDead => Current == State.Dead;
        public bool HoldsSlot => Current == State.Aiming || Current == State.Telegraph || Current == State.Firing;
        public Vector3 AimPoint => aimPoint;
        public float TelegraphProgress => Current == State.Telegraph ? 1f - Mathf.Clamp01(timer / telegraphLength) : 0f;
        /// <summary>Attacks made (a burst counts once, a melee strike too).</summary>
        public int ShotsFired { get; private set; }
        /// <summary>Bullets and grenades that left the gun.</summary>
        public int RoundsFired { get; private set; }
        public int MeleeStrikes { get; private set; }
        public int MeleeHits { get; private set; }
        /// <summary>The attack of the aim now, or of the last aim (the HUD icon).</summary>
        public Attack CurrentAttack { get; private set; } = Attack.None;
        public Weapon WeaponKind { get => weapon; set => weapon = value; }
        public bool CanMelee { get => canMelee; set => canMelee = value; }
        public float MeleeRange => meleeRange;
        public float GrenadeRadius => grenadeRadius;
        public float GrenadeMinDistance => grenadeMinDistance;
        /// <summary>The ranged attack of a weapon.</summary>
        public static Attack RangedAttackOf(Weapon w)
        {
            switch (w)
            {
                case Weapon.Rifle: return Attack.Burst;
                case Weapon.GrenadeLauncher: return Attack.Grenade;
                case Weapon.GasLauncher: return Attack.Gas;
                case Weapon.ArmourLaunchers: return Attack.Grenade; // and Rocket, one after the other
                default: return Attack.Shot;
            }
        }

        /// <summary>A grenade of either kind: it arcs to the floor, and it needs room.</summary>
        public static bool IsLobbed(Attack a) => a == Attack.Grenade || a == Attack.Gas;
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
        /// <summary>A melee strike: true when it hit her.</summary>
        public event System.Action<bool> OnMelee;
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
            if ((lift != null && lift.IsLifted) || RagdollBodies.IsDown(this))
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
                    Attack next = ChooseAttack(distance);
                    if (!HoldFire && next != Attack.None && PlayerAlive() && Sees(chest) && ShooterScheduler.TryAcquire(this))
                    {
                        CurrentAttack = next;
                        SplitTiming(out float aim, out telegraphLength);
                        Current = State.Aiming;
                        timer = aim;
                        aimPoint = AimFor(next, chest);
                        laser.enabled = next != Attack.Melee; // the laser is the gun's aim; a strike has none
                        RunTelemetry.Event("enemy_aim", $"{enemyId}:{next}");
                        OnAimStarted?.Invoke();
                    }
                    break;

                case State.Aiming:
                    aimPoint = AimFor(CurrentAttack, chest);
                    DrawLaser();
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        Current = State.Telegraph;
                        timer = telegraphLength;
                        RunTelemetry.Event("enemy_telegraph", $"{enemyId}:{CurrentAttack}");
                    }
                    break;

                case State.Telegraph:
                    DrawLaser();
                    timer -= dt;
                    if (timer <= 0f) Fire();
                    break;

                case State.Firing:
                    timer -= dt;
                    if (timer <= 0f) FireRound();
                    break;

                case State.Cooldown:
                case State.Staggered:
                    timer -= dt;
                    if (timer <= 0f) Current = State.Idle;
                    break;
            }
        }

        /// <summary>The attack for her distance now: melee when close (if it may), else the weapon's, in its range.</summary>
        private Attack ChooseAttack(float distance)
        {
            if (canMelee && distance <= meleeRange) return Attack.Melee;
            if (distance > range) return Attack.None;
            Attack ranged = RangedAttackOf(weapon);
            if (weapon == Weapon.ArmourLaunchers && nextIsRocket) ranged = Attack.Rocket;
            if ((IsLobbed(ranged) || ranged == Attack.Rocket) && distance < grenadeMinDistance) return Attack.None;
            return ranged;
        }

        /// <summary>The gun aims at her chest, a grenade at the floor under her.</summary>
        private Vector3 AimFor(Attack attack, Vector3 chest) => IsLobbed(attack) ? new Vector3(chest.x, player.position.y, chest.z) : chest;

        /// <summary>The time from the laser to the attack: the difficulty's, one third laser, two thirds circle.</summary>
        private void SplitTiming(out float aim, out float telegraph)
        {
            float total = aimSeconds + telegraphSeconds;
            if (useDifficultyTiming && GlorifiedBody.Systems.DifficultyManager.Instance != null)
            {
                total = GlorifiedBody.Systems.DifficultyManager.Instance.AttackSeconds;
            }
            float laserShare = aimSeconds / Mathf.Max(0.01f, aimSeconds + telegraphSeconds);
            aim = total * laserShare;
            telegraph = Mathf.Max(0.1f, total - aim);
        }

        private float RestSeconds => useDifficultyTiming && GlorifiedBody.Systems.DifficultyManager.Instance != null
            ? GlorifiedBody.Systems.DifficultyManager.Instance.RestSeconds
            : cooldownSeconds;

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
            if (!laser.enabled) return;
            laser.SetPosition(0, Muzzle);
            laser.SetPosition(1, aimPoint);
        }

        private void Fire()
        {
            ShotsFired++;
            RunTelemetry.Event("enemy_fired", $"{enemyId}:{CurrentAttack}");
            switch (CurrentAttack)
            {
                case Attack.Melee:
                    Strike();
                    CancelAim(State.Cooldown, RestSeconds);
                    return;
                case Attack.Burst:
                    // The rounds go 0.1 s apart to the locked point: one dodge clears the burst.
                    laser.enabled = false;
                    roundsLeft = burstRounds;
                    Current = State.Firing;
                    FireRound();
                    return;
                default:
                    roundsLeft = 1;
                    FireRound();
                    return;
            }
        }

        /// <summary>One bullet of a shot or a burst, or the grenade.</summary>
        private void FireRound()
        {
            Vector3 origin = Muzzle;
            Vector3 direction = (aimPoint - origin).normalized;
            if (CurrentAttack == Attack.Grenade)
            {
                EnemyGrenade.Launch(origin, aimPoint, grenadeFlightSeconds, grenadeFuseSeconds, grenadeDamage, grenadeRadius, gameObject, enemyId);
            }
            else if (CurrentAttack == Attack.Gas)
            {
                EnemyGrenade.LaunchGas(origin, aimPoint, grenadeFlightSeconds, grenadeFuseSeconds, gasDamagePerSecond, gasRadius, gasSeconds, gameObject, enemyId);
            }
            else if (CurrentAttack == Attack.Rocket)
            {
                EnemyRocket.Launch(origin, aimPoint - origin, rocketSpeed, rocketDamage, rocketRadius, gameObject, enemyId);
            }
            else
            {
                float damage = CurrentAttack == Attack.Burst ? burstRoundDamage : projectileDamage;
                EnemyProjectile.Launch(origin, aimPoint - origin, projectileSpeed, damage, gameObject, enemyId);
            }
            RoundsFired++;
            roundsLeft--;
            if (weapon == Weapon.ArmourLaunchers) nextIsRocket = CurrentAttack == Attack.Grenade;
            OnFired?.Invoke(origin, direction);
            if (roundsLeft > 0)
            {
                Current = State.Firing;
                timer = burstSpacing;
            }
            else
            {
                CancelAim(State.Cooldown, RestSeconds);
            }
        }

        /// <summary>The melee strike: it lands when she is still within reach and in front of him.</summary>
        private void Strike()
        {
            MeleeStrikes++;
            bool hit = false;
            Health playerHealth = player != null ? player.GetComponentInChildren<Health>() : null;
            if (playerHealth != null && !playerHealth.IsDead)
            {
                Vector3 to = player.position - transform.position;
                to.y = 0f;
                bool inReach = to.magnitude <= meleeReach;
                bool inFront = to.sqrMagnitude < 0.01f || Vector3.Angle(transform.forward, to) <= meleeArc;
                if (inReach && inFront && !playerHealth.IsInvulnerable)
                {
                    hit = true;
                    MeleeHits++;
                    GlorifiedBody.UI.DeathMenuUI.LastKillerId = enemyId;
                    playerHealth.TakeDamage(meleeDamage);
                    HitEventBus.Raise(new HitInfo
                    {
                        Attacker = gameObject,
                        Victim = playerHealth.gameObject,
                        HitTransform = player,
                        HitPoint = player.position + Vector3.up * Body.ChestHeight,
                        HitNormal = -to.normalized,
                        DamageAmount = meleeDamage,
                        DamageType = DamageType.EnemyMelee,
                        WasFatal = playerHealth.IsDead
                    });
                    RunTelemetry.Event("player_hit", $"{enemyId}:melee:{meleeDamage:0}");
                }
            }
            RunTelemetry.Event("enemy_melee", $"{enemyId}:{(hit ? "hit" : "miss")}");
            OnMelee?.Invoke(hit);
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
