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
    /// kills the Invector body (its death animation, then it removes its physics). A hit on our Health makes the
    /// body face the player even out of sight. The lift suspends the body: the FSM stops, the agent and the
    /// physics go off, and the drop puts it back on the NavMesh.
    /// </summary>
    [RequireComponent(typeof(vControlAI), typeof(Health))]
    public sealed class InvectorEnemyBody : MonoBehaviour, IEnemyBody
    {
        private const float MovingSpeed = 0.15f; // metres per second
        private const float SpeedWindow = 0.12f;  // seconds: the body moves on physics steps, not every frame

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
            health.OnDamaged ??= new UnityEngine.Events.UnityEvent<float>();
            health.OnDamaged.AddListener(OnDamaged);
        }

        private void OnDisable()
        {
            health.OnDeath?.RemoveListener(OnDeath);
            health.OnDamaged?.RemoveListener(OnDamaged);
        }

        private void Update()
        {
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

        public void MoveTo(Vector3 point, bool run)
        {
            if (IsDead || suspended) return;
            ai.MoveTo(point, run ? vAIMovementSpeed.Running : vAIMovementSpeed.Walking);
        }

        public void Stop()
        {
            if (ai == null || IsDead) return;
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
                if (body != null) body.isKinematic = false;
                if (agent != null)
                {
                    agent.enabled = true;
                    if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas)) agent.Warp(hit.position);
                }
            }
        }

        private void OnDamaged(float amount)
        {
            if (IsDead || HasTarget) return;
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) ai.SetCurrentTarget(player.transform, true);
        }

        private void OnDeath()
        {
            if (ai == null || ai.isDead) return;
            if (suspended) Suspend(false);
            ai.TakeDamage(new vDamage(int.MaxValue / 2, true));
        }
    }
}
