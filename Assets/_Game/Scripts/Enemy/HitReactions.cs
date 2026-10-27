using UnityEngine;
using UnityEngine.AI;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The body's answer to a hit that does not kill (Bruno, 2026-10-27, option D of the active ragdoll study, then
    /// "we should use invector clips for everyone, for all animations that are applicable; we only generate the ones
    /// that invector does not have already"). Every number is PROPOSED.
    /// - A flinch: the vendor hit reaction of the shooter animator (IEnemyBody.Flinch), from the side of the attacker.
    ///   A small one plays on the upper body (the legs walk on). A big one (a strong hit) plays on the whole body.
    /// - A stagger step after a small hit: the body moves a little the way the hit pushed it (on the NavMesh). The clip
    ///   of a big hit moves the body itself.
    /// - A leg hit: a limp for a few seconds (slower animation, the hips dip on each step).
    /// - A shock of the short: a spasm. The animator stops (the body locks) and the spine, the head and the arms shake
    ///   fast. Invector has no clip for it.
    /// - A fire: a panic for 3 s, and the figure does not attack (a stun). Big hits from changing sides, and over them
    ///   the torso twists, the head shakes and the arms beat at the flames (Bruno picked both: "A + B"). Invector
    ///   has no clip for it.
    /// A heavy only flinches small (no step, no limp, no panic). A civilian in a fear pose keeps its pose and jolts
    /// (Tremble). A body that is down, held or dead does nothing here: the ragdoll owns it (EnemyRagdoll).
    /// It runs before the gun (EnemyGun): the gun and the hands on it follow the moved torso.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(Health))]
    public sealed class HitReactions : MonoBehaviour
    {
        public enum Zone { None, Head, Chest, Stomach, Arm, Leg }

        public const float ReactionGap = 0.35f;     // s between two flinches (a rifle burst gives one)
        public const float StepSeconds = 0.25f;
        public const float LimpSeconds = 3f;
        public const float LimpAnimatorSpeed = 0.7f;
        public const float SpasmSeconds = 0.6f;     // a shock of the short
        public const float WritheSeconds = 3f;      // the panic at the start of a burn
        public const float FireFlinchGap = 0.8f;    // s between two big hits of the panic
        public const float StrongDamage = 30f;

        [SerializeField] private bool heavy;

        public Zone LastZone { get; private set; } = Zone.None;
        /// <summary>The last reaction: small_front, big_left, ..., flash, spasm, jolt, fire.</summary>
        public string LastClip { get; private set; } = "";
        public bool LastBig { get; private set; }
        /// <summary>The vendor's HitDirection of the last flinch: 0 the front, 90 the right, -90 the left, 180 the back.</summary>
        public int LastDirection { get; private set; }
        public int Flinches { get; private set; }
        public float LastStep { get; private set; }
        /// <summary>The metres the last stagger step moved the body (the AI's own walk not counted).</summary>
        public float StepTravelled { get; private set; }
        public bool Limping => Time.time < limpUntil;
        public bool Spasming => Time.time < spasmUntil;
        public bool Writhing => Time.time < writheUntil;
        public static int TotalFlinches { get; private set; }

        private Health health;
        private Animator animator;
        private IEnemyBody body;
        private PoseOverride pose;
        private Tremble tremble;
        private NavMeshAgent agent;
        private Transform head, neck, hips, spine, chest;
        private Transform leftUpper, leftLower, leftHand, rightUpper, rightLower, rightHand;
        private float nextReaction, limpUntil, spasmUntil, writheUntil, writheStart, stepUntil, nextFireFlinch;
        private Vector3 stepVelocity;
        private float limpPhase;
        private Vector3 lastPosition;
        private bool burnSeen;
        private bool speedChanged;
        private Transform[] spasmBones;
        private float[] spasmDegrees;
        private Vector3[] spasmFrom, spasmTo;
        private float spasmTick;
        private float flailSeed;

        /// <summary>The factory marks a heavy (editor code).</summary>
        public void SetHeavy(bool value) => heavy = value;

        private void Awake()
        {
            health = GetComponent<Health>();
            animator = GetComponentInChildren<Animator>();
            body = GetComponent<IEnemyBody>();
            agent = GetComponent<NavMeshAgent>();
            tremble = GetComponentInChildren<Tremble>();
            if (animator != null)
            {
                if (animator.isHuman)
                {
                    head = animator.GetBoneTransform(HumanBodyBones.Head);
                    neck = animator.GetBoneTransform(HumanBodyBones.Neck) ?? head;
                    hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                    spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                    chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? spine;
                    leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                    leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                    rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    // The spasm: degrees of shake per bone (the arms more than the spine).
                    spasmBones = new[] { spine, chest, neck, head, leftUpper, rightUpper, leftLower, rightLower };
                    spasmDegrees = new[] { 4f, 6f, 6f, 10f, 14f, 14f, 20f, 20f };
                    spasmFrom = new Vector3[spasmBones.Length];
                    spasmTo = new Vector3[spasmBones.Length];
                }
            }
            flailSeed = Random.value * 10f;
            lastPosition = transform.position;
        }

        private void OnEnable() => HitEventBus.OnHit += OnHit;

        private void OnDisable()
        {
            HitEventBus.OnHit -= OnHit;
            RestoreSpeed();
        }

        private bool Busy => health == null || health.IsDead || RagdollBodies.IsDown(this) || animator == null || !animator.enabled;
        private bool InPose
        {
            get
            {
                // A brain adds the PoseOverride at its first pose: look again while there is none.
                if (pose == null && animator != null) pose = animator.GetComponent<PoseOverride>();
                return pose != null && pose.Playing;
            }
        }

        private void OnHit(HitInfo hit)
        {
            if (hit == null || hit.Victim == null || hit.WasFatal) return;
            if (hit.Victim != gameObject && !hit.Victim.transform.IsChildOf(transform)) return;
            if (Busy) return;
            Vector3 from = hit.Attacker != null ? hit.Attacker.transform.position : hit.HitPoint + hit.HitNormal;

            if (hit.DamageType == DamageType.ShortShock)
            {
                Spasm();
                return;
            }
            if (hit.DamageAmount <= 0.01f && hit.DamageType != DamageType.PsychicFire) return;
            // A fire that burns (the ignition, the ticks): the panic below is the reaction, not a flinch a tick.
            if (hit.DamageType == DamageType.PsychicFire && hit.DamageAmount > 0.01f) return;
            if (InPose)
            {
                // A civilian's fear pose holds its body: it jolts and keeps the pose.
                if (Time.time < nextReaction) return;
                nextReaction = Time.time + ReactionGap;
                tremble?.Jolt(0.4f);
                Count(ZoneOf(hit.HitPoint), "jolt", false, 0);
                return;
            }
            if (hit.DamageType == DamageType.PsychicFire && hit.DamageAmount <= 0.01f)
            {
                // A part-charge fire: a flash in the face, no burn. He recoils from it.
                React(from, false, Zone.Head, "flash");
                return;
            }
            // A full mk_warspite knocks a figure down (PsychicPowers): the fall is the reaction.
            if (!heavy && hit.DamageType == DamageType.PsychicBlast && hit.Charge >= 0.999f) return;

            Zone zone = ZoneOf(hit.HitPoint);
            bool big = !heavy && (hit.DamageAmount >= StrongDamage || hit.DamageType == DamageType.Explosion
                                  || hit.DamageType == DamageType.EnemyRocket || hit.DamageType == DamageType.Grenade
                                  || (hit.DamageType == DamageType.PsychicBlast && hit.Charge >= 0.5f));
            if (!React(from, big, zone, null)) return;
            if (heavy) return;
            if (!big) Step(hit);
            if (zone == Zone.Leg) limpUntil = Time.time + LimpSeconds;
        }

        private bool React(Vector3 from, bool big, Zone zone, string name)
        {
            if (Time.time < nextReaction || body == null) return false;
            nextReaction = Time.time + ReactionGap;
            int direction = DirectionOf(from);
            body.Flinch(from, big);
            Count(zone, name ?? $"{(big ? "big" : "small")}_{DirectionName(direction)}", big, direction);
            return true;
        }

        private void Count(Zone zone, string name, bool big, int direction)
        {
            LastZone = zone;
            LastClip = name;
            LastBig = big;
            LastDirection = direction;
            Flinches++;
            TotalFlinches++;
            RunTelemetry.Event("flinch", $"{this.name}:{name}");
        }

        /// <summary>The vendor's HitAngle rule, for the tests and the telemetry.</summary>
        public int DirectionOf(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            if (angle <= 45f && angle >= -45f) return 0;
            if (angle > 45f && angle < 135f) return 90;
            if (angle < -45f && angle > -135f) return -90;
            return 180;
        }

        private static string DirectionName(int direction) => direction == 0 ? "front" : direction == 90 ? "right" : direction == -90 ? "left" : "back";

        private void Spasm()
        {
            spasmUntil = Time.time + SpasmSeconds;
            spasmTick = 0f;
            if (spasmTo != null) for (int i = 0; i < spasmTo.Length; i++) spasmTo[i] = Vector3.zero;
            Count(Zone.Chest, "spasm", false, 0);
        }

        /// <summary>The zone of a world point on this body, by the bones (the head, the legs, the arms, the belly).</summary>
        public Zone ZoneOf(Vector3 point)
        {
            if (hips == null || head == null) return Zone.Chest;
            float neckY = neck.position.y;
            if (point.y >= neckY - 0.02f) return Zone.Head;
            if (point.y < hips.position.y - 0.08f) return Zone.Leg;
            Vector3 local = transform.InverseTransformPoint(point);
            float shoulder = leftUpper != null && rightUpper != null ? 0.5f * Vector3.Distance(leftUpper.position, rightUpper.position) : 0.18f;
            if (Mathf.Abs(local.x) > shoulder * 0.95f) return Zone.Arm;
            float belly = Mathf.Lerp(hips.position.y, chest.position.y, 0.6f);
            return point.y < belly ? Zone.Stomach : Zone.Chest;
        }

        /// <summary>A short step the way the hit pushed (away from the attacker), held on the NavMesh.</summary>
        private void Step(HitInfo hit)
        {
            Vector3 dir = hit.Attacker != null ? Flat(transform.position - hit.Attacker.transform.position) : Flat(-hit.HitNormal);
            if (dir.sqrMagnitude < 0.0001f) return;
            float distance = hit.DamageType == DamageType.EnemyMelee || hit.DamageType == DamageType.PlayerMelee ? 0.35f : 0.18f;
            stepVelocity = dir.normalized * (distance / StepSeconds);
            stepUntil = Time.time + StepSeconds;
            LastStep = distance;
            StepTravelled = 0f;
        }

        private void Update()
        {
            if (health != null && health.IsDead)
            {
                RestoreSpeed();
                return;
            }
            // A fire: the panic at the start of the burn, and the figure does not attack.
            BurningStatus burn = GetComponent<BurningStatus>();
            bool burning = burn != null && burn.IsBurning;
            if (burning && !burnSeen && !heavy && !Busy)
            {
                writheStart = Time.time;
                writheUntil = Time.time + WritheSeconds;
                nextFireFlinch = Time.time;
                GetComponent<IStaggerable>()?.Stagger(WritheSeconds);
                RunTelemetry.Event("writhe", name);
            }
            burnSeen = burning;
            if (Writhing && !Busy && !InPose && body != null && Time.time >= nextFireFlinch)
            {
                // A big hit from a new side each time: he staggers this way and that.
                nextFireFlinch = Time.time + FireFlinchGap;
                Vector3 side = Quaternion.Euler(0f, 90f * Random.Range(0, 4), 0f) * transform.forward;
                body.Flinch(transform.position + side, true);
                Count(Zone.Chest, "fire", true, DirectionOf(transform.position + side));
            }

            // The spasm locks the body, the limp slows the walk.
            float goal = Busy ? 1f : Spasming ? 0f : Limping ? LimpAnimatorSpeed : 1f;
            if (speedChanged || goal != 1f) SetSpeed(goal);
        }

        private void RestoreSpeed()
        {
            if (speedChanged) SetSpeed(1f);
        }

        private void SetSpeed(float speed)
        {
            if (body != null) body.SetAnimatorSpeed(speed);
            else if (animator != null) animator.speed = speed;
            speedChanged = speed != 1f;
        }

        private void LateUpdate()
        {
            if (Busy)
            {
                stepUntil = 0f;
                lastPosition = transform.position;
                return;
            }
            float dt = Time.deltaTime;
            // The stagger step, after the AI moved the body this frame.
            if (Time.time < stepUntil && dt > 0f)
            {
                Vector3 delta = stepVelocity * dt;
                Vector3 to = transform.position + delta;
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    if (NavMesh.Raycast(transform.position, to, out NavMeshHit blocked, NavMesh.AllAreas)) to = blocked.position;
                    Vector3 before = transform.position;
                    transform.position = new Vector3(to.x, transform.position.y, to.z);
                    agent.nextPosition = transform.position;
                    StepTravelled += Flat(transform.position - before).magnitude;
                }
                else
                {
                    transform.position = to;
                    StepTravelled += delta.magnitude;
                }
            }
            // The limp: the hips dip and roll on each step while the body moves.
            if (Limping && hips != null && dt > 0f)
            {
                float moved = Flat(transform.position - lastPosition).magnitude;
                limpPhase += moved / 0.7f * Mathf.PI;
                float dip = Mathf.Abs(Mathf.Sin(limpPhase));
                hips.position += Vector3.down * (0.05f * dip);
                hips.rotation = Quaternion.AngleAxis(6f * Mathf.Sin(limpPhase), transform.forward) * hips.rotation;
            }
            lastPosition = transform.position;

            if (Spasming) Shake();
            if (Writhing) Flail();
        }

        /// <summary>The spasm: every bone of the list takes a new small turn 24 times a second (the locked pose shakes).</summary>
        private void Shake()
        {
            if (spasmBones == null) return;
            spasmTick -= Time.deltaTime;
            bool next = spasmTick <= 0f;
            if (next) spasmTick = 1f / 24f;
            float left = spasmUntil - Time.time;
            float fade = Mathf.Clamp01(left / 0.1f);
            float blend = 1f - Mathf.Clamp01(spasmTick * 24f);
            for (int i = 0; i < spasmBones.Length; i++)
            {
                Transform b = spasmBones[i];
                if (b == null) continue;
                if (next)
                {
                    spasmFrom[i] = spasmTo[i];
                    spasmTo[i] = Random.insideUnitSphere * spasmDegrees[i];
                }
                Vector3 e = Vector3.Lerp(spasmFrom[i], spasmTo[i], blend) * fade;
                b.localRotation *= Quaternion.Euler(e);
            }
        }

        /// <summary>
        /// The fire panic over the big hits: the torso twists and bends, the head shakes, the arms beat at the flames.
        /// A gun's hands go back to the gun after this (EnemyGun runs later), so a soldier writhes with his gun.
        /// </summary>
        private void Flail()
        {
            if (chest == null || spine == null) return;
            float t = Time.time - writheStart + flailSeed;
            float w = Mathf.Clamp01((Time.time - writheStart) / 0.2f) * Mathf.Clamp01((writheUntil - Time.time) / 0.3f);
            Vector3 up = transform.up, forward = transform.forward, right = transform.right;
            float twist = 22f * Mathf.Sin(t * 2f * Mathf.PI * 1.6f) + 8f * (Mathf.PerlinNoise(t * 3f, 0.3f) - 0.5f);
            float bend = 10f * Mathf.Sin(t * 2f * Mathf.PI * 2.3f + 1f);
            spine.rotation = Quaternion.AngleAxis(twist * 0.4f * w, up) * Quaternion.AngleAxis(bend * 0.5f * w, right) * spine.rotation;
            chest.rotation = Quaternion.AngleAxis(twist * 0.6f * w, up) * Quaternion.AngleAxis(bend * 0.5f * w, right) * chest.rotation;
            if (head != null) head.rotation = Quaternion.AngleAxis(16f * Mathf.Sin(t * 2f * Mathf.PI * 3f) * w, up) * head.rotation;
            Arm(leftUpper, leftLower, leftHand, -1f, t, w, up, forward, right);
            Arm(rightUpper, rightLower, rightHand, 1f, t + 0.37f, w, up, forward, right);
        }

        /// <summary>One arm of the flail: the upper arm swings up and out, the forearm bends back to the body.</summary>
        private static void Arm(Transform upper, Transform lower, Transform hand, float side, float t, float w, Vector3 up, Vector3 forward, Vector3 right)
        {
            if (upper == null || lower == null || hand == null || w <= 0f) return;
            float lift = 35f + 30f * Mathf.Sin(t * 2f * Mathf.PI * 2.7f);          // degrees over the horizontal
            float reach = 25f * Mathf.Sin(t * 2f * Mathf.PI * 1.9f + side);        // forward and back
            Vector3 aim = Quaternion.AngleAxis(reach, up) * (right * side * Mathf.Cos(lift * Mathf.Deg2Rad) + up * Mathf.Sin(lift * Mathf.Deg2Rad));
            Vector3 now = lower.position - upper.position;
            upper.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(now, aim), w) * upper.rotation;
            float fold = 0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 3.4f);
            Vector3 foreAim = (aim + (up * 0.6f - right * side * 0.8f + forward * 0.4f) * fold).normalized;
            Vector3 foreNow = hand.position - lower.position;
            lower.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(foreNow, foreAim), w) * lower.rotation;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
