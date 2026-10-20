using System;
using System.Collections;
using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// Tank controls (Galerians): Forward and Back move along the facing, TurnLeft and TurnRight rotate the yaw.
    /// Input is relative to Lucy, never to the camera, so a camera cut never changes what a key does.
    /// Run is a held modifier. Dodge is a short dash with invulnerability frames and a cooldown.
    /// Movement locks (the circle charge, a stun) stop the walk but keep the facing control of others.
    /// Tunings are PROPOSED (2026-10-20): Bruno decides after the first playtest.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class TankController : MonoBehaviour
    {
        [Header("Walk (m/s, deg/s)")]
        [SerializeField, Min(0f)] private float walkSpeed = 1.6f;
        [SerializeField, Min(0f)] private float backSpeed = 1.0f;
        [SerializeField, Min(0f)] private float runSpeed = 3.6f;
        [SerializeField, Min(0f)] private float turnSpeed = 150f;
        [SerializeField, Min(0f)] private float runTurnSpeed = 110f;

        [Header("Dodge")]
        [SerializeField, Min(0f)] private float dodgeDistance = 2.5f;
        [SerializeField, Min(0.05f)] private float dodgeDuration = 0.35f;
        [SerializeField, Min(0f)] private float dodgeInvulnerableTime = 0.3f;
        [SerializeField, Min(0f)] private float dodgeCooldown = 0.8f;

        [Header("Gravity")]
        [SerializeField] private float gravity = -20f;

        private CharacterController controller;
        private Health health;
        private float verticalVelocity;
        private float dodgeTimer;
        private float dodgeCooldownTimer;
        private Vector3 dodgeDirection;
        private int movementLocks;

        public float MoveInput { get; private set; }
        public float TurnInput { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsDodging => dodgeTimer > 0f;
        public bool MovementLocked => movementLocks > 0;
        public bool DodgeReady => dodgeCooldownTimer <= 0f && !IsDodging;
        public float Yaw => transform.eulerAngles.y;
        public Vector3 PlanarVelocity { get; private set; }
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float TurnSpeed => turnSpeed;
        public float DodgeDistance => dodgeDistance;
        public float DodgeCooldown => dodgeCooldown;

        public event Action OnDodge;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            // At a very high frame rate (batch mode, no vsync) a walk step is under 0.001 m. The controller's
            // default minMoveDistance drops such steps and she stands still. Zero keeps every step.
            controller.minMoveDistance = 0f;
            health = GetComponent<Health>() ?? GetComponentInChildren<Health>();
        }

        /// <summary>A lock stops the walk and the turn keys. Nested locks count. The charge uses this.</summary>
        public void LockMovement(bool locked)
        {
            movementLocks = Mathf.Max(0, movementLocks + (locked ? 1 : -1));
            if (movementLocks > 0)
            {
                MoveInput = 0f;
                TurnInput = 0f;
                IsRunning = false;
            }
        }

        /// <summary>Turns the yaw toward a world point at degreesPerSecond. Returns the remaining angle in degrees.</summary>
        public float FaceTowards(Vector3 worldPoint, float degreesPerSecond)
        {
            Vector3 to = worldPoint - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return 0f;
            Quaternion wanted = Quaternion.LookRotation(to.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, degreesPerSecond * Time.deltaTime);
            return Quaternion.Angle(transform.rotation, wanted);
        }

        /// <summary>Moves Lucy to a point at once (bots and tests). The controller is disabled for the jump.</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Physics.SyncTransforms();
            controller.enabled = true;
            controller.Move(Vector3.down * 0.05f); // one tiny step, so trigger volumes register the overlap
            verticalVelocity = 0f;
        }

        public bool TryDodge()
        {
            if (!DodgeReady || MovementLocked) return false;
            dodgeTimer = dodgeDuration;
            dodgeCooldownTimer = dodgeCooldown;
            // Forward held = a dash ahead. Otherwise a step back, the horror default.
            dodgeDirection = MoveInput > 0.5f ? transform.forward : -transform.forward;
            if (health != null && dodgeInvulnerableTime > 0f)
            {
                health.SetRuntimeInvulnerable(true);
                StartCoroutine(EndInvulnerability(dodgeInvulnerableTime));
            }
            RunTelemetry.Event("dodge", MoveInput > 0.5f ? "forward" : "back");
            OnDodge?.Invoke();
            return true;
        }

        private IEnumerator EndInvulnerability(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (health != null) health.SetRuntimeInvulnerable(false);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            dodgeCooldownTimer -= dt;

            ReadInput();
            if (GameInput.GetActionDown(InputManager.GameAction.Dodge, KeyCode.Space))
            {
                TryDodge();
            }

            Vector3 planar;
            if (IsDodging)
            {
                dodgeTimer -= dt;
                planar = dodgeDirection * (dodgeDistance / dodgeDuration);
            }
            else if (MovementLocked)
            {
                planar = Vector3.zero;
            }
            else
            {
                float turnRate = IsRunning ? runTurnSpeed : turnSpeed;
                transform.Rotate(0f, TurnInput * turnRate * dt, 0f, Space.World);
                float speed = MoveInput > 0f ? (IsRunning ? runSpeed : walkSpeed) : MoveInput < 0f ? -backSpeed : 0f;
                planar = transform.forward * speed;
            }

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;

            controller.Move(planar * dt + Vector3.up * (verticalVelocity * dt));
            PlanarVelocity = planar;
        }

        private void ReadInput()
        {
            if (MovementLocked)
            {
                MoveInput = 0f;
                TurnInput = 0f;
                IsRunning = false;
                return;
            }

            bool forward = GameInput.GetAction(InputManager.GameAction.Forward, KeyCode.W);
            bool back = GameInput.GetAction(InputManager.GameAction.Back, KeyCode.S);
            bool left = GameInput.GetAction(InputManager.GameAction.TurnLeft, KeyCode.A);
            bool right = GameInput.GetAction(InputManager.GameAction.TurnRight, KeyCode.D);
            MoveInput = (forward ? 1f : 0f) - (back ? 1f : 0f);
            TurnInput = (right ? 1f : 0f) - (left ? 1f : 0f);
            IsRunning = MoveInput > 0f && GameInput.GetAction(InputManager.GameAction.Run, KeyCode.LeftShift);
        }
    }
}
