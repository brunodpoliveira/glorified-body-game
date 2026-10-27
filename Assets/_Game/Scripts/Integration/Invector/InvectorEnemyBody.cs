using System.Collections;
using Invector;
using Invector.vCharacterController.AI;
using Invector.vCharacterController.AI.FSMBehaviour;
using UnityEngine;
using UnityEngine.AI;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;

namespace GlorifiedBody.Integration.Invector
{
    /// <summary>
    /// The Invector AI body (vControlAI: NavMeshAgent, root motion, sight) behind IEnemyBody, for SoldierBrain.
    /// Our Health is the real one (the powers hit it): the Invector health is kept out of reach, and our death
    /// kills the Invector body (the AI stops; EnemyRagdoll takes the body in the same frame: every death plays out
    /// physically, 2026-10-27, so the vendor death clip never shows). The lift suspends the body: the
    /// FSM stops, the agent and the physics go off, and the drop puts it back on the NavMesh. Track: the alerted
    /// soldier keeps her as a fixed target (Warspite's rule), so Invector never forgets her.
    /// </summary>
    [RequireComponent(typeof(Health))] // not vControlAI: Invector removes it after the death animation
    public sealed class InvectorEnemyBody : MonoBehaviour, IEnemyBody
    {
        private const float MovingSpeed = 0.15f; // metres per second
        private const float SpeedWindow = 0.12f;  // seconds: the body moves on physics steps, not every frame
        private const float TrackCheckSeconds = 0.35f;
        private const float CorpseSeconds = 1.5f; // the death animation, then the corpse lies still
        private float nextTrackCheck;
        private Transform tracked;
        private bool stopRequested;
        private bool braced_;

        private vControlAI ai;
        private vFSMBehaviourController fsm;
        private NavMeshAgent agent;
        private Rigidbody body;
        private Health health;
        private Vector3 windowStart;
        private float windowTime;
        private float speed;
        private bool suspended;

        public float Speed => speed;
        public bool Suspended => suspended;
        /// <summary>The name of the FSM state now ("Idle", "Engage", "Search"), for the tests and the debug line.</summary>
        public string FsmState => fsm != null && fsm.currentState != null ? fsm.currentState.name : "none";
        public bool FsmStopped => fsm != null && fsm.isStopped;

        private void Awake()
        {
            ai = GetComponent<vControlAI>();
            fsm = GetComponent<vFSMBehaviourController>();
            agent = GetComponent<NavMeshAgent>();
            body = GetComponent<Rigidbody>();
            health = GetComponent<Health>();
            windowStart = transform.position;
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

        private void Update()
        {
            if (!IsDead && !suspended && Time.time >= nextTrackCheck)
            {
                nextTrackCheck = Time.time + TrackCheckSeconds;
                KeepTrack();
            }
            windowTime += Time.deltaTime;
            if (windowTime < SpeedWindow) return;
            Vector3 step = transform.position - windowStart;
            step.y = 0f;
            speed = step.magnitude / windowTime;
            windowStart = transform.position;
            windowTime = 0f;
        }

        public bool IsDead => ai == null || ai.isDead || health.IsDead;
        public bool HasTarget => ai != null && ai.currentTarget != null && ai.currentTarget.transform != null;
        public bool CanSeeTarget => HasTarget && ai.targetInLineOfSight;
        public Vector3 TargetPosition => HasTarget ? ai.lastTargetPosition : transform.position + transform.forward * 4f;
        public bool IsMoving => speed > MovingSpeed;
        public bool IsInDestination => ai != null && ai.isInDestination;

        public void MoveTo(Vector3 point, MoveSpeed speed)
        {
            if (IsDead || suspended) return;
            stopRequested = false;
            ai.MoveTo(point, speed == MoveSpeed.Sprint ? vAIMovementSpeed.Sprinting : speed == MoveSpeed.Run ? vAIMovementSpeed.Running : vAIMovementSpeed.Walking);
        }

        public void Crouch(bool crouched)
        {
            if (ai == null || IsDead) return;
            ai.isCrouching = crouched;
        }

        public void StrafeTo(Vector3 point, Vector3 facing)
        {
            if (IsDead || suspended || ai == null) return;
            stopRequested = false;
            ai.StrafeMoveTo(point, facing, vAIMovementSpeed.Walking);
        }

        public void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        /// <summary>
        /// The vendor's own path (vAIMotor.TriggerDamageRection): HitDirection from the side of the point (vendor
        /// HitAngle: 0 the front, 90 the right, -90 the left, 180 the back), ReactionID 0 small (the UpperBody layer)
        /// or 1 big (the FullBody layer), then TriggerReaction and ResetState.
        /// </summary>
        public void Flinch(Vector3 from, bool big)
        {
            if (IsDead || suspended) return;
            Animator animator = GetComponent<Animator>();
            if (animator == null || !animator.enabled) return;
            int direction = (int)transform.HitAngle(from);
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name == "HitDirection") animator.SetInteger("HitDirection", direction);
                else if (p.name == "ReactionID") animator.SetInteger("ReactionID", big ? 1 : 0);
            }
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name == "TriggerReaction") animator.SetTrigger("TriggerReaction");
                else if (p.name == "ResetState") animator.SetTrigger("ResetState");
            }
        }

        /// <summary>The vendor motor writes animator.speed from its animatorSpeed in every frame (vAIMotor.UpdateAnimator).</summary>
        public void SetAnimatorSpeed(float speed)
        {
            if (ai != null) ai.animatorSpeed = speed;
            Animator animator = GetComponent<Animator>();
            if (animator != null) animator.speed = speed;
        }

        public void Stop()
        {
            if (ai == null || IsDead) return;
            stopRequested = true;
            ai.Stop();
        }

        public void Suspend(bool value)
        {
            if (suspended == value || ai == null) return;
            suspended = value;
            if (fsm != null) fsm.isStopped = value;
            if (value)
            {
                ai.Stop();
                if (agent != null) agent.enabled = false;
                if (body != null)
                {
                    // A kinematic body takes no velocity: a braced one (the aim) is kinematic already.
                    if (!body.isKinematic) body.linearVelocity = Vector3.zero;
                    body.isKinematic = true;
                }
            }
            else
            {
                braced_ = false;
                if (body != null) body.isKinematic = false;
                if (agent != null)
                {
                    agent.enabled = true;
                    if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas)) agent.Warp(hit.position);
                }
            }
        }

        /// <summary>
        /// Warspite's rule (EnemyLogic.SyncInvectorTarget): once alerted, a fixed target on her, pushed again when
        /// Invector clears it or marks it lost. A new target restarts Invector's path, so a stopped body stops again.
        /// </summary>
        private void KeepTrack()
        {
            if (tracked == null) return;
            vAITarget current = ai.currentTarget;
            if (current != null && current.transform == tracked && !current.isLost) return;
            var target = new vAITarget(tracked) { isFixedTarget = true };
            ai.SetCurrentTarget(target, true);
            if (stopRequested) ai.Stop();
        }

        public void Track(Transform target)
        {
            tracked = target;
            if (!IsDead && !suspended) KeepTrack();
        }

        public void Brace(bool braced)
        {
            if (suspended || IsDead || body == null || braced == braced_) return;
            braced_ = braced;
            if (braced) body.linearVelocity = Vector3.zero; // before the switch: a kinematic body takes no velocity
            body.isKinematic = braced;
        }


        private void OnDeath()
        {
            if (ai == null || ai.isDead) return;
            // A limp body (EnemyRagdoll) stays as it falls: no agent, no root physics. Else the lift's body lands.
            if (suspended && !RagdollBodies.IsDown(this)) Suspend(false);
            ai.TakeDamage(new vDamage(int.MaxValue / 2, true));
            StartCoroutine(LayDown());
        }

        /// <summary>After the death animation: no agent, no physics, no capsule. The corpse stays.</summary>
        private IEnumerator LayDown()
        {
            yield return new WaitForSeconds(CorpseSeconds);
            if (agent != null) agent.enabled = false;
            if (body != null) body.isKinematic = true;
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule != null) capsule.enabled = false;
        }
    }
}
