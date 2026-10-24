using System.Collections;
using Invector;
using Invector.vCharacterController.AI;
using Invector.vCharacterController.AI.FSMBehaviour;
using UnityEngine;
using UnityEngine.AI;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;

namespace GlorifiedBody.Integration.Invector
{
    /// <summary>
    /// The Invector AI body (vControlAI: NavMeshAgent, root motion, sight) behind IEnemyBody, for SoldierBrain.
    /// Our Health is the real one (the powers hit it): the Invector health is kept out of reach, and our death
    /// kills the Invector body (its death animation, then the corpse lies still). The lift suspends the body: the
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
                    body.isKinematic = true;
                    body.linearVelocity = Vector3.zero;
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
            body.isKinematic = braced;
            if (braced) body.linearVelocity = Vector3.zero;
        }


        private void OnDeath()
        {
            if (ai == null || ai.isDead) return;
            if (suspended) Suspend(false);
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
