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
    /// and freezes when it settles, so the drag of CorpseCare still moves it as one piece. The bones (rigidbodies,
    /// colliders, CharacterJoints) come from InvectorEnemyFactory (Unity's ragdoll builder) on the BodyPart layer,
    /// which hits the room and other ragdolls but not her or the capsules. While he is animated the bones are
    /// kinematic and their colliders are off, so nothing changes for the rest of the game.
    ///
    /// The bones stay children of the root. The root moves only in one step that puts every bone back where it
    /// was (SnapRootToHips), so the lock point, the short and the explosions find him where his body lies.
    /// The stand-up is Invector's method (vRagdoll): play the clip, turn the root to match the lying body, and
    /// blend from the ragdoll pose to the clip. SoldierBrain suspends the Invector body while IsDown.
    /// Numbers PROPOSED.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class EnemyRagdoll : MonoBehaviour, IRagdollBody
    {
        public enum State { Animated, Held, Down, StandingUp, Corpse }

        private const float SettleSpeed = 0.4f;     // m/s at the hips: the body lies still
        private const float SnapSeconds = 0.2f;     // how often the root follows the lying body
        private const float BlendSeconds = 0.3f;    // from the ragdoll pose to the stand-up clip
        private const float DefaultStandSeconds = 1.6f;
        private const float MaxStandSeconds = 3f;
        private const float GraceSeconds = 3f;      // he stands up after this even if the body still moves
        private const int FloorMask = 1;            // Default: the room

        public const string StandFromBack = "StandUp@FromBack";
        public const string StandFromBelly = "StandUp@FromBelly";

        [SerializeField] private Rigidbody[] parts = new Rigidbody[0]; // the hips first, parents before children

        public State Current { get; private set; } = State.Animated;
        public bool IsDown => Current != State.Animated;
        public bool IsRagdolled => Current == State.Held || Current == State.Down || Current == State.Corpse;
        public bool Available => parts != null && parts.Length > 1 && hips != null && animator != null;
        public string StandUpClip { get; private set; } = "";
        public int Downs { get; private set; }
        public int StandUps { get; private set; }
        public Transform Hips => hips;
        public IReadOnlyList<Rigidbody> Parts => parts;
        public static int TotalDowns { get; private set; }

        private Animator animator;
        private Health health;
        private CapsuleCollider capsule;
        private Rigidbody rootBody;
        private Transform hips, head, leftFoot, rightFoot;
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

        /// <summary>The factory sets the bones (editor code).</summary>
        public void Bind(Rigidbody[] bones) => parts = bones;

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
            }
            if (parts == null) parts = new Rigidbody[0];
            colliders = new Collider[parts.Length];
            for (int i = 0; i < parts.Length; i++) colliders[i] = parts[i] != null ? parts[i].GetComponent<Collider>() : null;
            storedPosition = new Vector3[parts.Length];
            storedRotation = new Quaternion[parts.Length];
            if (hips != null) hipsForwardLocal = hips.InverseTransformDirection(transform.forward);
            SetBones(limp: false, hipsHeld: false);
        }

        // ── IRagdollBody ───────────────────────────────────────────────

        public bool KnockDown(float seconds, Vector3 velocity)
        {
            if (!Available || Current == State.Held || Current == State.Corpse) return false;
            GoLimp(hipsHeld: false);
            SetVelocity(velocity);
            EnterDown(seconds);
            RunTelemetry.Event("ragdoll_down", $"{name}:{seconds:0.0}s");
            return true;
        }

        public bool BeginHold()
        {
            if (!Available || Current == State.Corpse || (health != null && health.IsDead)) return false;
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

        // ── The states ────────────────────────────────────────────────

        private void EnterDown(float seconds)
        {
            Current = State.Down;
            downStart = Time.time;
            downUntil = Time.time + Mathf.Max(0f, seconds);
            nextSnap = Time.time + SnapSeconds;
            settled = 0f;
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
                        if (settled > 0.6f || Time.time - downStart > 6f) Freeze();
                    }
                    else if (Time.time >= downUntil && (settled > 0.15f || Time.time > downUntil + GraceSeconds))
                    {
                        StandUp();
                    }
                    break;
                }
                case State.StandingUp:
                    if (health != null && health.IsDead) { Current = State.Animated; break; } // the death clip takes over
                    if (aligned && Time.time - standStart >= standSeconds) Finish();
                    break;
            }
        }

        private void LateUpdate()
        {
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

        // ── The bones ─────────────────────────────────────────────────

        private void GoLimp(bool hipsHeld)
        {
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
