using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The soldier's ragdoll (Bruno, 2026-10-24): the lift holds him by the hips with the limbs loose, a throw and
    /// a full mk_warspite knock him down, and he stands up with the vendor's stand-up clips. A dead body stays down
    /// and freezes when it settles. The drag of CorpseCare makes it limp again: a hand holds it by the shoulders and
    /// the legs trail on the floor; when the drag ends it falls, settles and freezes again (Bruno, 2026-10-25: "when
    /// dragging the body, the ragdoll should reactivate, and once the body is setup to cpr mode, freeze the ragdoll
    /// again"). The bones (rigidbodies,
    /// colliders, CharacterJoints) come from InvectorEnemyFactory (Unity's ragdoll builder) on the BodyPart layer,
    /// which hits the room and other ragdolls but not her or the capsules. While he is animated the bones are
    /// kinematic and their colliders are off, so nothing changes for the rest of the game.
    ///
    /// The bones stay children of the root. The root moves only in one step that puts every bone back where it
    /// was (SnapRootToHips), so the lock point, the short and the explosions find him where his body lies.
    /// The stand-up is Invector's method (vRagdoll): play the clip, turn the root to match the lying body, and
    /// blend from the ragdoll pose to the clip. SoldierBrain suspends the Invector body while IsDown.
    /// Numbers PROPOSED.
    ///
    /// Every death plays out physically (Bruno, 2026-10-27: no death clip). The body goes limp in the frame of the
    /// death, keeps its speed, takes the push of the hit that killed it at the hit bone, and its muscles hold the last
    /// pose for a moment and fade (the legs first, so the knees buckle). A shock convulses the body, a fire makes it
    /// writhe. A heavy has a ragdoll for its death only (DeathOnly).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class EnemyRagdoll : MonoBehaviour, IRagdollBody
    {
        public enum State { Animated, Held, Down, StandingUp, Corpse, Dragged }

        /// <summary>The kind of a physical death: the push and the throes follow it.</summary>
        public enum DeathKind { Plain, Blast, Shock, Burn }

        private const float SettleSpeed = 0.4f;     // m/s at the hips: the body lies still
        private const float HandSpeed = 3f;         // m/s: the drag hand goes to the carer's grip, no jerk
        private const float SnapSeconds = 0.2f;     // how often the root follows the lying body
        private const float BlendSeconds = 0.3f;    // from the ragdoll pose to the stand-up clip
        private const float DefaultStandSeconds = 1.6f;
        private const float MaxStandSeconds = 3f;
        private const float GraceSeconds = 3f;      // he stands up after this even if the body still moves
        private const int FloorMask = 1;            // Default: the room

        // The physical death (PROPOSED numbers): rad/s² of a muscle for each radian off its pose, the seconds the
        // muscles take to fade (the legs in LegFadeShare of it), the throes of a shock and a fire.
        private const float MuscleStrength = 380f;
        private const float MuscleFadeSeconds = 1.0f;
        private const float BlastFadeSeconds = 0.3f;
        private const float LegFadeShare = 0.55f;
        private const float ShockSeconds = 1.6f;
        private const float BurnSeconds = 3.2f;
        private const float ShockTorque = 260f;
        private const float BurnTorque = 120f;

        public const string StandFromBack = "StandUp@FromBack";
        public const string StandFromBelly = "StandUp@FromBelly";

        [SerializeField] private Rigidbody[] parts = new Rigidbody[0]; // the hips first, parents before children
        [SerializeField] private bool deathOnly; // a heavy: it never falls alive, its death falls (Bruno, 2026-10-27)

        public State Current { get; private set; } = State.Animated;
        public bool IsDown => Current != State.Animated;
        public bool IsRagdolled => Current == State.Held || Current == State.Down || Current == State.Corpse || Current == State.Dragged;
        public bool Available => parts != null && parts.Length > 1 && hips != null && animator != null;
        public string StandUpClip { get; private set; } = "";
        public int Downs { get; private set; }
        public int StandUps { get; private set; }
        public Transform Hips => hips;
        /// <summary>The chest part (the drag holds it), or null.</summary>
        public Rigidbody Torso => torso;
        /// <summary>The point the drag hand holds now (between the shoulders).</summary>
        public Vector3 GripPoint => dragJoint != null ? torso.transform.TransformPoint(dragJoint.anchor) : ShoulderPoint();
        public IReadOnlyList<Rigidbody> Parts => parts;
        public static int TotalDowns { get; private set; }
        /// <summary>A heavy: no knockdown, no lift, no drag. Only its death goes limp.</summary>
        public bool DeathOnly => deathOnly;
        /// <summary>The death went limp from the living pose (not a body that was already down).</summary>
        public bool DiedFalling { get; private set; }
        public DeathKind LastDeath { get; private set; } = DeathKind.Plain;
        /// <summary>The push of the killing hit, m/s at the hit bone.</summary>
        public float LastDeathPush { get; private set; }
        public string LastDeathBone { get; private set; } = "";
        /// <summary>The muscles or the throes still act.</summary>
        public bool InThroes => Current == State.Down && Time.time < throesUntil;
        public static int FallingDeaths { get; private set; }

        private Animator animator;
        private Health health;
        private CapsuleCollider capsule;
        private Rigidbody rootBody;
        private Transform hips, head, leftFoot, rightFoot, leftShoulder, rightShoulder;
        private Rigidbody torso;
        private Rigidbody hand;              // the drag: a kinematic point the torso hangs on
        private ConfigurableJoint dragJoint;
        private Vector3 handGoal;
        private Collider[] colliders;
        private Vector3 hipsForwardLocal;   // the root's forward in the hips' frame: up means he lies on his back
        private float downStart, downUntil, nextSnap, settled;
        private float standStart, standSeconds, blendStart;
        private int alignFrames;
        private bool aligned;
        private Vector3[] storedPosition;
        private Quaternion[] storedRotation;
        private Vector3 storedHead, storedFeet;
        private readonly HashSet<int> severed = new HashSet<int>();
        // The physical death.
        private int[] parentIndex;
        private bool[] legPart;
        private Quaternion[] muscleTarget;
        private Vector3[] throeAxis;
        private bool[] hinge; // a knee or an elbow
        private float throesStart, throesUntil, muscleFade, nextThroe;
        private DeathKind throesKind;
        private HitInfo lastHit;
        private int lastHitFrame = -10;
        private bool deathPending;
        private Vector3 lastRootPosition, rootVelocity;

        /// <summary>The factory sets the bones (editor code).</summary>
        public void Bind(Rigidbody[] bones) => parts = bones;

        /// <summary>The factory marks a heavy (editor code).</summary>
        public void SetDeathOnly(bool value) => deathOnly = value;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            health = GetComponent<Health>();
            capsule = GetComponent<CapsuleCollider>();
            rootBody = GetComponent<Rigidbody>();
            if (animator != null && animator.isHuman)
            {
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                head = animator.GetBoneTransform(HumanBodyBones.Head);
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            }
            if (parts == null) parts = new Rigidbody[0];
            // The torso is the part on the chest bone (HumanoidRagdoll puts it there, else on the spine).
            Transform chestBone = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine) : null;
            foreach (Rigidbody part in parts) if (part != null && part.transform == chestBone) torso = part;
            colliders = new Collider[parts.Length];
            for (int i = 0; i < parts.Length; i++) colliders[i] = parts[i] != null ? parts[i].GetComponent<Collider>() : null;
            storedPosition = new Vector3[parts.Length];
            storedRotation = new Quaternion[parts.Length];
            if (hips != null) hipsForwardLocal = hips.InverseTransformDirection(transform.forward);
            BuildMuscles();
            SetBones(limp: false, hipsHeld: false);
            lastRootPosition = transform.position;
        }

        private void OnEnable()
        {
            HitEventBus.OnHit += OnHit;
            if (health != null)
            {
                health.OnDeath ??= new UnityEngine.Events.UnityEvent();
                health.OnDeath.AddListener(OnDied);
            }
        }

        private void OnDisable()
        {
            HitEventBus.OnHit -= OnHit;
            if (health != null) health.OnDeath?.RemoveListener(OnDied);
        }

        /// <summary>The parent part of each part, and which parts are legs (they fade first).</summary>
        private void BuildMuscles()
        {
            parentIndex = new int[parts.Length];
            legPart = new bool[parts.Length];
            muscleTarget = new Quaternion[parts.Length];
            throeAxis = new Vector3[parts.Length];
            hinge = new bool[parts.Length];
            var legs = new HashSet<Transform>();
            if (animator != null && animator.isHuman)
            {
                foreach (HumanBodyBones b in new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                                                     HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot })
                {
                    Transform t = animator.GetBoneTransform(b);
                    if (t != null) legs.Add(t);
                }
            }
            for (int i = 0; i < parts.Length; i++)
            {
                parentIndex[i] = -1;
                if (parts[i] == null) continue;
                legPart[i] = legs.Contains(parts[i].transform);
                // HumanoidRagdoll gives a hinge (a knee, an elbow) a swing of a few degrees only.
                hinge[i] = parts[i].TryGetComponent(out CharacterJoint joint) && joint.swing1Limit.limit <= 10f;
                for (Transform t = parts[i].transform.parent; t != null && parentIndex[i] < 0; t = t.parent)
                {
                    for (int j = 0; j < i; j++) if (parts[j] != null && parts[j].transform == t) { parentIndex[i] = j; break; }
                }
            }
        }

        // ── IRagdollBody ───────────────────────────────────────────────

        public bool KnockDown(float seconds, Vector3 velocity)
        {
            // A dead body falls by DieFalling (the physical death), not by a knockdown.
            if (!Available || deathOnly || (health != null && health.IsDead) || Current == State.Held || Current == State.Corpse || Current == State.Dragged) return false;
            GoLimp(hipsHeld: false);
            SetVelocity(velocity);
            EnterDown(seconds);
            RunTelemetry.Event("ragdoll_down", $"{name}:{seconds:0.0}s");
            return true;
        }

        public bool BeginHold()
        {
            if (!Available || deathOnly || Current == State.Corpse || (health != null && health.IsDead)) return false;
            if (Current != State.Animated) SnapRootToHips(); // the lift starts where the body lies
            GoLimp(hipsHeld: true);
            Current = State.Held;
            RunTelemetry.Event("ragdoll_held", name);
            return true;
        }

        public void EndHold(Vector3 velocity, float seconds)
        {
            if (Current != State.Held) return;
            SetBones(limp: true, hipsHeld: false);
            SetVelocity(velocity);
            EnterDown(seconds);
            RunTelemetry.Event("ragdoll_dropped", $"{name}:{seconds:0.0}s");
        }

        public void Sever(Transform bone)
        {
            if (bone == null) return;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null || (parts[i].transform != bone && !parts[i].transform.IsChildOf(bone))) continue;
                severed.Add(i);
                if (colliders[i] != null) colliders[i].enabled = false;
                parts[i].mass = 0.05f; // it hangs on its joint without weight
            }
        }

        // ── The drag (CorpseCare) ─────────────────────────────────────

        /// <summary>
        /// A carer takes the dead body: the bones go limp from the pose they have, and a hand holds the torso
        /// between the shoulders where they lie. DragTo moves the hand, EndDrag lets the body fall.
        /// </summary>
        public bool BeginDrag()
        {
            if (!Available || deathOnly || torso == null || health == null || !health.IsDead) return false;
            if (Current == State.Held || Current == State.Dragged || InThroes) return false;
            SnapRootToHips();
            GoLimp(hipsHeld: false);
            Vector3 grip = ShoulderPoint();
            if (hand == null)
            {
                hand = new GameObject(name + "_DragHand").AddComponent<Rigidbody>();
                hand.isKinematic = true;
            }
            hand.transform.position = grip;
            hand.position = grip;
            handGoal = grip;
            dragJoint = torso.gameObject.AddComponent<ConfigurableJoint>();
            dragJoint.autoConfigureConnectedAnchor = false;
            dragJoint.connectedBody = hand;
            dragJoint.anchor = torso.transform.InverseTransformPoint(grip);
            dragJoint.connectedAnchor = Vector3.zero;
            dragJoint.xMotion = dragJoint.yMotion = dragJoint.zMotion = ConfigurableJointMotion.Locked;
            Current = State.Dragged;
            nextSnap = Time.time + SnapSeconds;
            RunTelemetry.Event("ragdoll_dragged", name);
            return true;
        }

        /// <summary>The hand goes to this point (the carer's grip) at HandSpeed.</summary>
        public void DragTo(Vector3 grip) => handGoal = grip;

        /// <summary>The carer lets go: the body falls, settles and freezes again.</summary>
        public void EndDrag()
        {
            if (Current != State.Dragged) return;
            if (dragJoint != null) Destroy(dragJoint);
            dragJoint = null;
            EnterDown(0f, knockdown: false);
            RunTelemetry.Event("ragdoll_drag_end", name);
        }

        private void FixedUpdate()
        {
            Throes();
            if (Current != State.Dragged || hand == null) return;
            hand.MovePosition(Vector3.MoveTowards(hand.position, handGoal, HandSpeed * Time.fixedDeltaTime));
        }

        private void OnDestroy()
        {
            if (hand != null) Destroy(hand.gameObject);
        }

        private Vector3 ShoulderPoint()
        {
            if (leftShoulder != null && rightShoulder != null) return 0.5f * (leftShoulder.position + rightShoulder.position);
            return torso != null ? torso.position : transform.position;
        }

        // ── The states ────────────────────────────────────────────────

        /// <summary>
        /// A dead body that a load puts back (SaveSystem): the bones already hold the saved pose. A body that lay
        /// still freezes at once. One saved in its fall (a drag, a throw) goes limp and settles from that pose.
        /// </summary>
        public bool RestoreCorpse(bool settled)
        {
            if (!Available || health == null || !health.IsDead) return false;
            GoLimp(hipsHeld: false);
            if (settled)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] == null) continue;
                    parts[i].isKinematic = true;
                    if (colliders[i] != null) colliders[i].enabled = false;
                }
                Current = State.Corpse;
            }
            else
            {
                EnterDown(0f, knockdown: false);
            }
            RunTelemetry.Event("ragdoll_restored", $"{name}:{(settled ? "corpse" : "falls")}");
            return true;
        }

        private void EnterDown(float seconds, bool knockdown = true)
        {
            Current = State.Down;
            downStart = Time.time;
            downUntil = Time.time + Mathf.Max(0f, seconds);
            nextSnap = Time.time + SnapSeconds;
            settled = 0f;
            if (!knockdown) return;
            Downs++;
            TotalDowns++;
        }

        private void Update()
        {
            switch (Current)
            {
                case State.Down:
                {
                    if (Time.time >= nextSnap)
                    {
                        nextSnap = Time.time + SnapSeconds;
                        SnapRootToHips();
                    }
                    Rigidbody hipsBody = parts[0];
                    settled = hipsBody.linearVelocity.magnitude < SettleSpeed ? settled + Time.deltaTime : 0f;
                    bool dead = health != null && health.IsDead;
                    if (dead)
                    {
                        // A body in its throes does not freeze yet.
                        if (Time.time >= throesUntil && (settled > 0.6f || Time.time - downStart > 6f + (throesUntil - downStart))) Freeze();
                    }
                    else if (Time.time >= downUntil && (settled > 0.15f || Time.time > downUntil + GraceSeconds))
                    {
                        StandUp();
                    }
                    break;
                }
                case State.Dragged:
                    // The root follows the body, so the lock point and CorpseCare find it where it is.
                    if (Time.time >= nextSnap)
                    {
                        nextSnap = Time.time + SnapSeconds;
                        SnapRootToHips();
                    }
                    break;
                case State.StandingUp:
                    if (health != null && health.IsDead) { Current = State.Animated; break; } // the death clip takes over
                    if (aligned && Time.time - standStart >= standSeconds) Finish();
                    break;
            }
        }

        private void LateUpdate()
        {
            // The death: in LateUpdate, so the muscles keep the pose the animator gave in this frame.
            if (deathPending)
            {
                deathPending = false;
                if (Current == State.Animated || Current == State.StandingUp) DieFalling(Time.frameCount - lastHitFrame <= 1 ? lastHit : null);
            }
            if (Current == State.Animated && Time.deltaTime > 0f)
            {
                rootVelocity = (transform.position - lastRootPosition) / Time.deltaTime;
                lastRootPosition = transform.position;
            }
            if (Current != State.StandingUp) return;
            if (!aligned)
            {
                // The animator posed the first frame of the clip: turn and move the root so the clip's body lies
                // where the ragdoll lies (Invector's vRagdoll). Until then the ragdoll pose stays on screen.
                if (++alignFrames >= 2)
                {
                    Vector3 lying = Flat(storedHead - storedFeet);
                    Vector3 clip = Flat(head.position - FeetMid());
                    if (lying.sqrMagnitude > 0.01f && clip.sqrMagnitude > 0.01f)
                        transform.rotation = Quaternion.FromToRotation(clip.normalized, lying.normalized) * transform.rotation;
                    Vector3 shift = Flat(storedPosition[0] - hips.position);
                    transform.position += shift;
                    aligned = true;
                    blendStart = Time.time;
                    standSeconds = (Time.time - standStart) + ClipLength();
                }
                ShowStored(1f);
                return;
            }
            float amount = 1f - (Time.time - blendStart) / BlendSeconds;
            if (amount > 0f) ShowStored(amount);
        }

        private void StandUp()
        {
            SnapRootToHips();
            for (int i = 0; i < parts.Length; i++)
            {
                storedPosition[i] = parts[i].transform.position;
                storedRotation[i] = parts[i].transform.rotation;
            }
            storedHead = head.position;
            storedFeet = FeetMid();
            bool onBack = hips.TransformDirection(hipsForwardLocal).y > 0f;
            SetBones(limp: false, hipsHeld: false);
            animator.enabled = true;
            StandUpClip = onBack ? StandFromBack : StandFromBelly;
            int layer = FindLayer(StandUpClip);
            if (layer >= 0) animator.Play(StandUpClip, layer, 0f);
            else StandUpClip = "";
            Current = State.StandingUp;
            standStart = Time.time;
            standSeconds = DefaultStandSeconds;
            alignFrames = 0;
            aligned = false;
            RunTelemetry.Event("ragdoll_stand", $"{name}:{(StandUpClip == "" ? "none" : StandUpClip)}");
        }

        private void Finish()
        {
            Current = State.Animated;
            if (capsule != null) capsule.enabled = true;
            StandUps++;
            RunTelemetry.Event("ragdoll_up", name);
        }

        /// <summary>A dead body settled: it freezes in its pose, the bones become one piece with the root.</summary>
        private void Freeze()
        {
            SnapRootToHips();
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].isKinematic = true;
                if (colliders[i] != null) colliders[i].enabled = false;
            }
            Current = State.Corpse;
            RunTelemetry.Event("ragdoll_corpse", name);
        }

        // ── The physical death ────────────────────────────────────────

        private void OnHit(HitInfo hit)
        {
            if (hit == null || hit.Victim == null) return;
            if (hit.Victim != gameObject && !hit.Victim.transform.IsChildOf(transform)) return;
            lastHit = hit;
            lastHitFrame = Time.frameCount;
        }

        private void OnDied() => deathPending = true;

        /// <summary>
        /// The figure dies standing: the bones go limp from the living pose, keep the walk's speed, and the hit bone
        /// takes the push of the killing hit. The muscles hold the pose and fade, the legs first.
        /// </summary>
        private void DieFalling(HitInfo hit)
        {
            if (!Available) return;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                int p = parentIndex[i];
                muscleTarget[i] = p >= 0 ? Quaternion.Inverse(parts[p].rotation) * parts[i].rotation : parts[i].rotation;
            }
            GoLimp(hipsHeld: false);
            Vector3 carried = Vector3.ClampMagnitude(new Vector3(rootVelocity.x, 0f, rootVelocity.z), 6f);
            foreach (Rigidbody part in parts)
            {
                if (part == null || part.isKinematic) continue;
                part.linearVelocity = carried;
                part.angularVelocity = Vector3.zero;
            }

            DeathKind kind = KindOf(hit);
            Vector3 direction = PushDirection(hit);
            float speed = PushSpeed(hit, kind);
            Vector3 point = hit != null ? hit.HitPoint : (torso != null ? torso.worldCenterOfMass : hips.position);
            Rigidbody hitPart = NearestPart(point);
            // The whole body takes a share; the hit bone takes the rest at the hit point (it turns the body).
            float bodyShare = kind == DeathKind.Blast ? 0.75f : 0.3f;
            Vector3 lift = kind == DeathKind.Blast ? Vector3.up * (0.25f * speed) : Vector3.zero;
            foreach (Rigidbody part in parts)
                if (part != null && !part.isKinematic) part.linearVelocity += direction * (speed * bodyShare) + lift;
            if (hitPart != null)
                hitPart.AddForceAtPosition(direction * (speed * (1f - bodyShare) * hitPart.mass * 2f), point, ForceMode.Impulse);

            throesKind = kind;
            throesStart = Time.time;
            muscleFade = kind == DeathKind.Blast ? BlastFadeSeconds : MuscleFadeSeconds;
            throesUntil = Time.time + (kind == DeathKind.Burn ? BurnSeconds : kind == DeathKind.Shock ? ShockSeconds : muscleFade);
            nextThroe = 0f;
            EnterDown(0f, knockdown: false);
            DiedFalling = true;
            LastDeath = kind;
            LastDeathPush = speed;
            LastDeathBone = hitPart != null ? hitPart.name : "";
            FallingDeaths++;
            RunTelemetry.Event("death_fall", $"{name}:{kind}:{speed:0.0}m/s@{LastDeathBone}");
        }

        private DeathKind KindOf(HitInfo hit)
        {
            BurningStatus burn = GetComponent<BurningStatus>();
            if (hit == null) return burn != null && burn.IsBurning ? DeathKind.Burn : DeathKind.Plain;
            switch (hit.DamageType)
            {
                case DamageType.ShortShock: return DeathKind.Shock;
                case DamageType.PsychicFire: return DeathKind.Burn;
                case DamageType.Explosion:
                case DamageType.Grenade:
                case DamageType.EnemyRocket: return DeathKind.Blast;
                case DamageType.PsychicBlast: return hit.Charge >= 0.999f ? DeathKind.Blast : DeathKind.Plain;
            }
            return burn != null && burn.IsBurning ? DeathKind.Burn : DeathKind.Plain;
        }

        /// <summary>Away from the attacker, else against the hit normal, else backward.</summary>
        private Vector3 PushDirection(HitInfo hit)
        {
            Vector3 d = Vector3.zero;
            if (hit != null && hit.Attacker != null) d = Flat(transform.position - hit.Attacker.transform.position);
            if (d.sqrMagnitude < 0.01f && hit != null) d = Flat(-hit.HitNormal);
            if (d.sqrMagnitude < 0.01f) d = -transform.forward;
            return d.normalized;
        }

        /// <summary>The push in m/s by the kind of hit (PROPOSED).</summary>
        private static float PushSpeed(HitInfo hit, DeathKind kind)
        {
            if (kind == DeathKind.Blast) return hit != null && hit.DamageType == DamageType.PsychicBlast ? 6f : 7f;
            if (kind == DeathKind.Shock || kind == DeathKind.Burn) return 0.6f;
            if (hit == null) return 0.8f;
            switch (hit.DamageType)
            {
                case DamageType.PsychicBlast: return 2.5f + 2f * Mathf.Clamp01(hit.Charge);
                case DamageType.PsychicTelekinesis:
                case DamageType.Throw:
                case DamageType.PropImpact: return 3.5f;
                case DamageType.EnemyMelee:
                case DamageType.PlayerMelee: return 2.5f;
                default: return 1.8f; // a bullet
            }
        }

        private Rigidbody NearestPart(Vector3 point)
        {
            Rigidbody best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null || severed.Contains(i)) continue;
                float d = (parts[i].worldCenterOfMass - point).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = parts[i]; }
            }
            return best;
        }

        /// <summary>The muscles hold the death pose and fade; a shock convulses, a fire writhes.</summary>
        private void Throes()
        {
            if (Current != State.Down || Time.time >= throesUntil || muscleTarget == null) return;
            float t = Time.time - throesStart;
            bool newThroe = Time.time >= nextThroe;
            if (newThroe) nextThroe = Time.time + (throesKind == DeathKind.Shock ? 0.07f : 0.45f);
            for (int i = 1; i < parts.Length; i++)
            {
                int p = parentIndex[i];
                Rigidbody body = parts[i];
                if (p < 0 || body == null || body.isKinematic || severed.Contains(i)) continue;
                Rigidbody parent = parts[p];
                Vector3 torque = Vector3.zero;
                float fade = Mathf.Clamp01(1f - t / (muscleFade * (legPart[i] ? LegFadeShare : 1f)));
                float strength = fade * fade * MuscleStrength;
                if (strength > 0.5f)
                {
                    Quaternion target = parent.rotation * muscleTarget[i];
                    (target * Quaternion.Inverse(body.rotation)).ToAngleAxis(out float angle, out Vector3 axis);
                    if (angle > 180f) angle -= 360f;
                    if (Mathf.Abs(angle) > 0.01f && axis.sqrMagnitude > 0.5f) torque += axis.normalized * (angle * Mathf.Deg2Rad * strength);
                    torque -= (body.angularVelocity - parent.angularVelocity) * (1.6f * Mathf.Sqrt(strength));
                }
                if (throesKind == DeathKind.Shock || throesKind == DeathKind.Burn)
                {
                    float span = throesKind == DeathKind.Shock ? ShockSeconds : BurnSeconds;
                    float left = Mathf.Clamp01(1f - t / span);
                    if (newThroe) throeAxis[i] = Random.onUnitSphere;
                    // No jerk on a knee or an elbow: a jerk there slammed the shin into its limits and bent it sideways
                    // (Bruno, 2026-10-27). The hips, the shoulders and the spine convulse, the shins and forearms follow.
                    // A heavy (the death-only armour) jerks half as hard: its long limbs broke the knee limits.
                    if (!hinge[i]) torque += throeAxis[i] * ((throesKind == DeathKind.Shock ? ShockTorque : BurnTorque) * left * (deathOnly ? 0.5f : 1f));
                }
                if (torque.sqrMagnitude > 0f) body.AddTorque(torque, ForceMode.Acceleration);
            }
        }

        // ── The bones ─────────────────────────────────────────────────

        private void GoLimp(bool hipsHeld)
        {
            // A pose over the controller (a flinch, a fear pose) must not drive the limp bones.
            if (animator != null && animator.TryGetComponent(out GlorifiedBody.Characters.PoseOverride pose)) pose.Halt();
            if (animator != null) animator.enabled = false;
            if (capsule != null) capsule.enabled = false;
            if (rootBody != null)
            {
                if (!rootBody.isKinematic) rootBody.linearVelocity = Vector3.zero;
                rootBody.isKinematic = true;
            }
            SetBones(limp: true, hipsHeld: hipsHeld);
        }

        private void SetBones(bool limp, bool hipsHeld)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                Rigidbody part = parts[i];
                if (part == null) continue;
                part.isKinematic = !limp || (hipsHeld && i == 0);
                if (colliders[i] != null) colliders[i].enabled = limp && !severed.Contains(i);
            }
        }

        private void SetVelocity(Vector3 velocity)
        {
            foreach (Rigidbody part in parts)
            {
                if (part == null || part.isKinematic) continue;
                part.linearVelocity = velocity;
                part.angularVelocity = Random.insideUnitSphere * 2f;
            }
        }

        /// <summary>The root moves under the hips, on the floor. Every bone keeps its place and its velocity.</summary>
        public void SnapRootToHips()
        {
            if (hips == null) return;
            Vector3 at = hips.position;
            float y = transform.position.y;
            if (Physics.Raycast(at + Vector3.up * 0.2f, Vector3.down, out RaycastHit floor, 4f, FloorMask, QueryTriggerInteraction.Ignore)) y = floor.point.y;
            Vector3 target = new Vector3(at.x, y, at.z);
            if ((target - transform.position).sqrMagnitude < 0.0004f) return;
            for (int i = 0; i < parts.Length; i++)
            {
                storedPosition[i] = parts[i].transform.position;
                storedRotation[i] = parts[i].transform.rotation;
            }
            transform.position = target;
            for (int i = 0; i < parts.Length; i++) parts[i].transform.SetPositionAndRotation(storedPosition[i], storedRotation[i]);
        }

        private void ShowStored(float amount)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                Transform t = parts[i].transform;
                t.rotation = Quaternion.Slerp(t.rotation, storedRotation[i], amount);
            }
            hips.position = Vector3.Lerp(hips.position, storedPosition[0], amount);
        }

        private int FindLayer(string state)
        {
            int hash = Animator.StringToHash(state);
            for (int layer = 0; layer < animator.layerCount; layer++)
                if (animator.HasState(layer, hash)) return layer;
            return -1;
        }

        private float ClipLength()
        {
            if (StandUpClip == "") return 0.4f;
            int layer = FindLayer(StandUpClip);
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);
            float length = info.IsName(StandUpClip) ? info.length : DefaultStandSeconds;
            return Mathf.Clamp(length, 0.8f, MaxStandSeconds);
        }

        private Vector3 FeetMid() => leftFoot != null && rightFoot != null ? 0.5f * (leftFoot.position + rightFoot.position) : transform.position;

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
