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
        public enum Weapon { Pistol, Rifle, GrenadeLauncher, GasLauncher, ArmourLaunchers, Knife, Shotgun, ArmourGuns } // Knife: melee only (2026-10-27); Shotgun, ArmourGuns: the Dining Hall (round 122)
        public enum Attack { None, Shot, Burst, Grenade, Melee, Gas, Rocket, Spread, Sweep }

        // The shotgun (round 122, PROPOSED: "a short wide cone instead of a line, close range only"): the cone shows in the
        // aim and the circle, then the pellets fly inside it. Numbers PROPOSED.
        public const float ConeHalfAngle = 18f;
        public const int Pellets = 7;
        public const float PelletDamage = 5f;
        // The autofire of the power armour (round 122, PROPOSED: "the laser line sweeps across her, the ring fills, then a
        // long burst follows the sweep. Cover breaks the burst"). Numbers PROPOSED.
        public const float SweepHalfWidth = 1.6f;   // m each side of her
        public const float SweepPeriod = 1.2f;      // s for one sweep across and back
        public const int SweepRounds = 12;
        public const float SweepSpacing = 0.08f;
        public const float SweepRoundDamage = 5f;

        /// <summary>The writer's lines as a soldier fires the knock-out gas (barks.csv, soldier_gas_1 to 3, PROPOSED).</summary>
        public static readonly string[] SoldierGasLines =
        {
            "Knock-out going in. Masks.", "Gas out. Put her to sleep, then we walk in.", "She breathes this she's done. Keep her in it.",
        };

        [SerializeField] private string enemyId = "dummy_shooter";
        [SerializeField, Min(1f)] private float range = 10f;
        [SerializeField, Min(0f)] private float aimSeconds = 0.5f;
        [SerializeField, Min(0.1f)] private float telegraphSeconds = 1.0f;
        public const float MeleeWindowShare = 0.5f; // a strike's warning is half a shot's, PROPOSED
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
        [Tooltip("A shaky shooter (the armed lab worker): the locked point lands up to this far off her chest, in metres.")]
        [SerializeField, Min(0f)] private float aimSpread;

        private Health health;
        private Transform player;
        private Transform herTransform;

        /// <summary>
        /// What the gun hunts. Null: her. mk_edgar turns guns (2026-11-06): a dominated figure aims at its colleagues, and they aim
        /// at it. It changes only between aims.
        /// </summary>
        public void SetTarget(Transform target)
        {
            player = target != null ? target : HerTransform();
        }

        private Transform HerTransform()
        {
            if (herTransform == null)
            {
                GameObject her = GameObject.FindWithTag(playerTag);
                herTransform = her != null ? her.transform : null;
            }
            return herTransform;
        }

        /// <summary>The gun aims at her now: not at a place (suppression), not at a dominated figure, not a gun in her service. The HUD warns only of these.</summary>
        public bool AimsAtHer => !Suppressing && (player == null || player == HerTransform());
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
        /// <summary>She stands in melee reach now, by the gun's own measure (the brain asks this: 2026-11-03, the brain and
        /// the gun measured 1.80 m two ways, the brain stopped to strike, the gun chose nothing, twelve times).</summary>
        public bool HerInReach => canMelee && player != null && Vector3.Distance(transform.position, player.position) <= meleeRange;
        public float AimSpread { get => aimSpread; set => aimSpread = Mathf.Max(0f, value); }
        /// <summary>How far the last lock landed off her chest (the spread), in metres.</summary>
        public float LastMissDistance { get; private set; }
        public float GrenadeRadius => grenadeRadius;
        public float GrenadeMinDistance => grenadeMinDistance;
        /// <summary>The nearest a ranged attack of this weapon fires: a grenade or a rocket not under 3.5 m (it would hit him).</summary>
        public float MinAttackDistance
        {
            get
            {
                Attack ranged = RangedAttackOf(weapon);
                return IsLobbed(ranged) || ranged == Attack.Rocket || weapon == Weapon.ArmourLaunchers ? grenadeMinDistance : 0f;
            }
        }
        /// <summary>The ranged attack of a weapon.</summary>
        public static Attack RangedAttackOf(Weapon w)
        {
            switch (w)
            {
                case Weapon.Rifle: return Attack.Burst;
                case Weapon.GrenadeLauncher: return Attack.Grenade;
                case Weapon.GasLauncher: return Attack.Gas;
                case Weapon.ArmourLaunchers: return Attack.Grenade; // and Rocket, one after the other
                case Weapon.Knife: return Attack.None; // a knife only strikes
                case Weapon.Shotgun: return Attack.Spread;
                case Weapon.ArmourGuns: return Attack.Sweep;
                default: return Attack.Shot;
            }
        }

        /// <summary>A grenade of either kind: it arcs to the floor, and it needs room.</summary>
        public static bool IsLobbed(Attack a) => a == Attack.Grenade || a == Attack.Gas;
        public string EnemyId => enemyId;

        /// <summary>
        /// The killer id of a class with a weapon: "guard_pistol", "soldier_rifle", "soldier_grenades", "soldier_gas",
        /// "power_armour_launchers". The death screen looks for this id first, then the class alone.
        /// </summary>
        public static string KillerIdFor(string classId, Weapon weapon)
        {
            string w = weapon switch
            {
                Weapon.Rifle => "rifle",
                Weapon.GrenadeLauncher => "grenades",
                Weapon.GasLauncher => "gas",
                Weapon.ArmourLaunchers => "launchers",
                Weapon.Knife => "knife",
                Weapon.Shotgun => "shotgun",
                Weapon.ArmourGuns => "guns",
                _ => "pistol"
            };
            return classId + "_" + w;
        }
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
        /// <summary>
        /// Suppression (Bruno, 2026-11-06, DEMO_PLAN §13.6): the place to fire at, while she hides. Null: the gun hunts her as
        /// usual. It takes no slot of the difficulty: the slots count the guns on her, and this one points at a place. The
        /// laser and the tracer are its warning. A bullet still hurts her if she walks into it.
        /// </summary>
        public Vector3? SuppressPoint { get; set; }
        /// <summary>The aim now running is at a place, not at her.</summary>
        public bool Suppressing { get; private set; }
        public int SuppressShots { get; private set; }
        private const float SuppressSpread = 0.6f; // m around the place, PROPOSED

        /// <summary>He fires over cover: the gun hangs low behind it until the aim raises it, so the line counts from the shoulder.</summary>
        public bool FromCover { get; set; }
        public Vector3 Muzzle => MuzzleOverride != null ? MuzzleOverride.position : transform.position + Vector3.up * Body.MuzzleHeight + transform.forward * 0.5f;

        private void Awake()
        {
            health = GetComponent<Health>();
            laser = MakeLine("Laser", new Color(1f, 0.1f, 0.1f), 0.03f, 2);
            laser.enabled = false;
            cone = MakeLine("Cone", new Color(1f, 0.1f, 0.1f), 0.03f, 3); // the shotgun's wedge: one edge, the muzzle, the other edge
            cone.enabled = false;
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
            if (!HoldFire || HoldsSlot) FaceYaw(Suppressing ? aimPoint : SuppressPoint ?? player.position);

            switch (Current)
            {
                case State.Idle:
                    if (SuppressPoint.HasValue && !HoldFire && distance > meleeRange && PlayerAlive() && TrySuppress(SuppressPoint.Value)) break;
                    Attack next = ChooseAttack(distance);
                    if (!HoldFire && next != Attack.None && PlayerAlive() && Sees(chest) && ShooterScheduler.TryAcquire(this))
                    {
                        CurrentAttack = next;
                        SplitTiming(out float aim, out telegraphLength);
                        if (next == Attack.Melee)
                        {
                            // A strike gives less warning than a shot (Bruno, 2026-10-24: "melee attack should have
                            // smaller window than projectile attack").
                            aim *= MeleeWindowShare;
                            telegraphLength = Mathf.Max(0.1f, telegraphLength * MeleeWindowShare);
                        }
                        Current = State.Aiming;
                        timer = aim;
                        sweepClock = 0f;
                        sweepCentre = chest;
                        sweepMin = sweepMax = 0f;
                        aimPoint = AimFor(next, chest);
                        laser.enabled = next != Attack.Melee && next != Attack.Spread; // the laser is the gun's aim; a strike has none; the shotgun shows its cone
                        RunTelemetry.Event("enemy_aim", $"{enemyId}:{next}");
                        OnAimStarted?.Invoke();
                    }
                    break;

                case State.Aiming:
                    if (!Suppressing) aimPoint = AimFor(CurrentAttack, chest);
                    DrawLaser();
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        Current = State.Telegraph;
                        timer = telegraphLength;
                        LockWithSpread();
                        RunTelemetry.Event("enemy_telegraph", $"{enemyId}:{CurrentAttack}");
                    }
                    break;

                case State.Telegraph:
                    if (CurrentAttack == Attack.Sweep && !Suppressing) aimPoint = AimFor(CurrentAttack, chest);
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

        /// <summary>The aim at a place: the same laser and the same time as an aim at her, with no slot and no circle on her.</summary>
        private bool TrySuppress(Vector3 place)
        {
            Attack attack = RangedAttackOf(weapon);
            float reach = Vector3.Distance(transform.position, place);
            if (attack == Attack.None || reach > range || ((IsLobbed(attack) || attack == Attack.Rocket) && reach < grenadeMinDistance) || !Sees(place)) return false;
            CurrentAttack = attack;
            Suppressing = true;
            SuppressShots++;
            SplitTiming(out float aim, out telegraphLength);
            Current = State.Aiming;
            timer = aim;
            aimPoint = IsLobbed(attack) ? new Vector3(place.x, player.position.y, place.z) : place;
            laser.enabled = true;
            RunTelemetry.Event("enemy_suppress", $"{enemyId}:{place.x:0.0},{place.z:0.0}");
            OnAimStarted?.Invoke();
            return true;
        }

        /// <summary>The attack for her distance now: melee when close (if it may), else the weapon's, in its range.</summary>
        private Attack ChooseAttack(float distance)
        {
            if (canMelee && distance <= meleeRange) return Attack.Melee;
            if (distance > range) return Attack.None;
            Attack ranged = RangedAttackOf(weapon);
            if (ranged == Attack.None) return Attack.None;
            if (weapon == Weapon.ArmourLaunchers && nextIsRocket) ranged = Attack.Rocket;
            if ((IsLobbed(ranged) || ranged == Attack.Rocket) && distance < grenadeMinDistance) return Attack.None;
            return ranged;
        }

        /// <summary>The gun aims at her chest, a grenade at the floor under her.</summary>
        private Vector3 AimFor(Attack attack, Vector3 chest)
        {
            if (IsLobbed(attack)) return new Vector3(chest.x, player.position.y, chest.z);
            if (attack == Attack.Sweep)
            {
                // The laser sweeps across her, side to side, through the aim and the circle.
                sweepClock += Time.deltaTime;
                return chest + SweepSide(chest) * (Mathf.Sin(sweepClock * 2f * Mathf.PI / SweepPeriod) * SweepHalfWidth);
            }
            return chest;
        }

        private Vector3 SweepSide(Vector3 at)
        {
            Vector3 line = at - Muzzle;
            line.y = 0f;
            return line.sqrMagnitude < 0.0001f ? transform.right : Vector3.Cross(Vector3.up, line.normalized);
        }

        private LineRenderer cone;
        private float sweepClock;
        private Vector3 sweepCentre;
        /// <summary>The widest sideways travel of the sweep laser in the last aim (a test reads it), m.</summary>
        public float LastSweepSpan { get; private set; }
        private float sweepMin, sweepMax;
        public bool ConeShown => cone != null && cone.enabled;

        /// <summary>
        /// The lock of a shaky shooter: the point moves off her chest, square to the line of fire, up to aimSpread.
        /// A shot that misses still flies to the locked point (the telegraph shows where). Gun shots only.
        /// </summary>
        private void LockWithSpread()
        {
            LastMissDistance = 0f;
            float spread = Suppressing ? Mathf.Max(aimSpread, SuppressSpread) : aimSpread;
            if (spread <= 0f || (CurrentAttack != Attack.Shot && CurrentAttack != Attack.Burst)) return;
            Vector3 line = aimPoint - Muzzle;
            if (line.sqrMagnitude < 0.0001f) return;
            Vector3 side = Vector3.Cross(Vector3.up, line).normalized;
            Vector2 offset = Random.insideUnitCircle * spread;
            aimPoint += side * offset.x + Vector3.up * offset.y;
            LastMissDistance = offset.magnitude;
            RunTelemetry.Event("enemy_aim_spread", $"{enemyId}:{LastMissDistance:0.00}");
        }

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
            Vector3 from = FromCover ? transform.position + Vector3.up * Body.MuzzleHeight : Muzzle;
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
            if (CurrentAttack == Attack.Spread && !Suppressing)
            {
                // The wedge: the muzzle, the two edges at the gun's range.
                Vector3 to = aimPoint - Muzzle;
                to.y = 0f;
                if (to.sqrMagnitude < 0.0001f) to = transform.forward;
                Vector3 along = to.normalized * range;
                cone.enabled = true;
                cone.SetPosition(0, Muzzle + Quaternion.Euler(0f, -ConeHalfAngle, 0f) * along);
                cone.SetPosition(1, Muzzle);
                cone.SetPosition(2, Muzzle + Quaternion.Euler(0f, ConeHalfAngle, 0f) * along);
                return;
            }
            if (!laser.enabled) return;
            laser.SetPosition(0, Muzzle);
            laser.SetPosition(1, aimPoint);
            if (CurrentAttack == Attack.Sweep)
            {
                float off = Vector3.Dot(aimPoint - sweepCentre, SweepSide(sweepCentre));
                sweepMin = Mathf.Min(sweepMin, off);
                sweepMax = Mathf.Max(sweepMax, off);
                LastSweepSpan = sweepMax - sweepMin;
            }
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
                case Attack.Spread:
                {
                    // The pellets fan out inside the cone (round 122, PROPOSED).
                    cone.enabled = false;
                    Vector3 origin = Muzzle;
                    Vector3 to = aimPoint - origin;
                    for (int i = 0; i < Pellets; i++)
                    {
                        float a = Mathf.Lerp(-ConeHalfAngle, ConeHalfAngle, Pellets > 1 ? i / (float)(Pellets - 1) : 0.5f);
                        EnemyProjectile.Launch(origin, Quaternion.Euler(0f, a, 0f) * to, projectileSpeed, PelletDamage, gameObject, enemyId);
                        RoundsFired++;
                    }
                    OnFired?.Invoke(origin, to.normalized);
                    CancelAim(State.Cooldown, RestSeconds);
                    return;
                }
                case Attack.Sweep:
                    // The long burst follows the sweep: the rounds walk across her from one side to the other.
                    laser.enabled = false;
                    sweepCentre = player != null ? player.position + Vector3.up * Body.ChestHeight : aimPoint;
                    roundsLeft = SweepRounds;
                    Current = State.Firing;
                    FireRound();
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
                Bark.Say(gameObject, SoldierGasLines[Random.Range(0, SoldierGasLines.Length)]); // the knock-out gas (barks.csv, soldier_gas_*, PROPOSED)
            }
            else if (CurrentAttack == Attack.Rocket)
            {
                EnemyRocket.Launch(origin, aimPoint - origin, rocketSpeed, rocketDamage, rocketRadius, gameObject, enemyId);
            }
            else if (CurrentAttack == Attack.Sweep)
            {
                float t = 1f - (roundsLeft - 1) / (float)Mathf.Max(1, SweepRounds - 1); // 0 at the first round, 1 at the last
                Vector3 at = sweepCentre + SweepSide(sweepCentre) * Mathf.Lerp(-SweepHalfWidth, SweepHalfWidth, t);
                direction = (at - origin).normalized;
                EnemyProjectile.Launch(origin, at - origin, projectileSpeed, SweepRoundDamage, gameObject, enemyId);
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
                timer = CurrentAttack == Attack.Sweep ? SweepSpacing : burstSpacing;
            }
            else
            {
                CancelAim(State.Cooldown, RestSeconds);
            }
        }

        /// <summary>A kick or a bang on a door (FireDoor): the strike's swing and sound, no hit on her.</summary>
        public void SwingAtDoor() => OnMelee?.Invoke(false);

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
                    playerHealth.TakeDamage(GlorifiedBody.Systems.DifficultyManager.EnemyDamage(meleeDamage));
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
            Suppressing = false;
            laser.enabled = false;
            if (cone != null) cone.enabled = false;
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
