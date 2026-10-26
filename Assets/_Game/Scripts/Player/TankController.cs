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
    /// Run is a held modifier. Dodge is a short dash with invulnerability frames and a cooldown. The dash is
    /// directional (Bruno, 2026-10-20): Dodge plus a held direction key, or a double tap on a direction key.
    /// Forward and Back dash along the facing, TurnLeft and TurnRight dash sideways. No direction = a step back.
    /// The press is forgiving (Bruno, 2026-10-21: "hard to trigger the dodge at the sides"): a press inside the
    /// cooldown waits up to dodgeBufferTime and fires when the dodge is ready, a direction key up to dodgeAimWindow
    /// after Space still aims the dash, and with two keys held the one pressed last wins.
    /// Movement locks (the circle charge, a stun) stop the walk but keep the facing control of others.
    /// The slow walk (the short, Bruno 2026-10-20): walk and back step at slowWalkScale, no run, no dodge.
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
        [SerializeField, Range(0.1f, 1f)] private float slowWalkScale = 0.6f; // the short's walk, PROPOSED

        [Header("Dodge")]
        [SerializeField, Min(0f)] private float dodgeDistance = 2.5f;
        [SerializeField, Min(0.05f)] private float dodgeDuration = 0.35f;
        [SerializeField, Min(0f)] private float dodgeInvulnerableTime = 0.3f;
        [SerializeField, Min(0f)] private float dodgeCooldown = 0.8f;
        // The scene builder sets these three on the kept component (a changed default never lands by itself).
        public const float DefaultDoubleTapWindow = 0.4f;
        public const float DefaultDodgeAimWindow = 0.1f;
        public const float DefaultDodgeBufferTime = 0.5f;
        [SerializeField, Min(0.05f)] private float doubleTapWindow = DefaultDoubleTapWindow;
        [SerializeField, Min(0f)] private float dodgeAimWindow = DefaultDodgeAimWindow;
        [SerializeField, Min(0f)] private float dodgeBufferTime = DefaultDodgeBufferTime;

        [Header("Gravity")]
        [SerializeField] private float gravity = -20f;

        private CharacterController controller;
        private Health health;
        private float verticalVelocity;
        private float dodgeTimer;
        private float dodgeCooldownTimer;
        private Vector3 dodgeDirection;
        private int movementLocks;
        private readonly float[] lastTapTime = { -10f, -10f, -10f, -10f }; // Forward, Back, TurnLeft, TurnRight
        private readonly float[] lastDownTime = { -10f, -10f, -10f, -10f }; // the same four keys: when each went down
        private readonly bool[] heldKeys = new bool[4];
        private int newDirectionKey = -1; // the direction key that went down this frame, else -1
        private bool dodgeRequested;
        private bool dodgeRequestAimed;
        private DodgeDirection dodgeRequestDirection;
        private float dodgeRequestTime = -10f;
        private float dodgeStartTime = -10f;
        private static readonly DodgeDirection[] KeyDirections = { DodgeDirection.Forward, DodgeDirection.Back, DodgeDirection.Left, DodgeDirection.Right };

        public enum DodgeDirection { Back, Forward, Left, Right }
        public DodgeDirection LastDodge { get; private set; }

        public float MoveInput { get; private set; }
        public float TurnInput { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsDodging => dodgeTimer > 0f;
        public bool MovementLocked => movementLocks > 0;
        /// <summary>The short: a slow walk, no run, no dodge.</summary>
        public bool SlowWalk { get; private set; }
        public float SlowWalkScale => slowWalkScale;
        public bool DodgeReady => dodgeCooldownTimer <= 0f && !IsDodging && !SlowWalk;
        public float Yaw => transform.eulerAngles.y;
        public Vector3 PlanarVelocity { get; private set; }
        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float TurnSpeed => turnSpeed;
        public float DodgeDistance => dodgeDistance;
        public float DodgeCooldown => dodgeCooldown;
        public float DoubleTapWindow => doubleTapWindow;
        public float DodgeAimWindow => dodgeAimWindow;
        public float DodgeBufferTime => dodgeBufferTime;
        /// <summary>A dodge press waits for the dodge to be ready (up to dodgeBufferTime).</summary>
        public bool DodgePending => dodgeRequested;

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

        /// <summary>The slow walk of the short: walk and back step at slowWalkScale, no run, no dodge.</summary>
        public void SetSlowWalk(bool slow)
        {
            SlowWalk = slow;
            if (slow) IsRunning = false;
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

        /// <summary>
        /// Moves Lucy to a point at once (bots and tests). The controller is disabled for the jump. Her root
        /// stands on her feet, so the point snaps down to the floor under it (triggers do not count).
        /// </summary>
        public void Teleport(Vector3 position, float yaw)
        {
            controller.enabled = false;
            if (Physics.Raycast(position + Vector3.up, Vector3.down, out RaycastHit floorHit, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                position.y = floorHit.point.y + 0.02f;
            }
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Physics.SyncTransforms();
            controller.enabled = true;
            controller.Move(Vector3.down * 0.05f); // one tiny step, so trigger volumes register the overlap
            verticalVelocity = 0f;
        }

        /// <summary>The dodge in the direction of the held keys (the key pressed last wins). No key = a step back.</summary>
        public bool TryDodge()
        {
            return TryDodge(TryHeldDirection(out DodgeDirection held) ? held : DodgeDirection.Back);
        }

        public bool TryDodge(DodgeDirection direction)
        {
            if (!DodgeReady || MovementLocked) return false;
            dodgeTimer = dodgeDuration;
            dodgeCooldownTimer = dodgeCooldown;
            dodgeStartTime = Time.time;
            SetDodgeDirection(direction);

            if (health != null && dodgeInvulnerableTime > 0f)
            {
                health.SetRuntimeInvulnerable(true);
                StartCoroutine(EndInvulnerability(dodgeInvulnerableTime));
            }
            RunTelemetry.Event("dodge", direction.ToString().ToLowerInvariant());
            GlorifiedBody.Audio.Sfx.Play(GlorifiedBody.Audio.Sfx.Dodge, transform.position + Vector3.up * 0.8f, 0.7f);
            OnDodge?.Invoke();
            return true;
        }

        private void SetDodgeDirection(DodgeDirection direction)
        {
            LastDodge = direction;
            switch (direction)
            {
                case DodgeDirection.Forward: dodgeDirection = transform.forward; break;
                case DodgeDirection.Left: dodgeDirection = -transform.right; break;
                case DodgeDirection.Right: dodgeDirection = transform.right; break;
                default: dodgeDirection = -transform.forward; break;
            }
        }

        /// <summary>
        /// A dodge press (Space, or a double tap with its direction) waits up to dodgeBufferTime for the dodge to
        /// be ready, so a press right after a dash is not lost to the cooldown (Bruno, 2026-10-21: "sometimes the
        /// player just rotates or walks a bit forward instead of dodging"). A press during a movement lock (the
        /// charge) is dropped, as before.
        /// </summary>
        private void RequestDodge(bool aimed, DodgeDirection direction)
        {
            dodgeRequested = true;
            dodgeRequestAimed = aimed;
            dodgeRequestDirection = direction;
            dodgeRequestTime = Time.time;
        }

        /// <summary>
        /// A direction key that goes down while a press waits aims that press. One that goes down up to
        /// dodgeAimWindow after the dash started turns the dash that way: Space one frame before A is a dodge to
        /// the left, not a step back.
        /// </summary>
        private void AimLateKey(DodgeDirection direction)
        {
            if (dodgeRequested)
            {
                dodgeRequestAimed = true;
                dodgeRequestDirection = direction;
            }
            else if (IsDodging && Time.time - dodgeStartTime <= dodgeAimWindow && direction != LastDodge)
            {
                SetDodgeDirection(direction);
                RunTelemetry.Event("dodge_aim", direction.ToString().ToLowerInvariant());
            }
        }

        private void ServeDodgeRequest()
        {
            if (!dodgeRequested) return;
            // The charge and the short have no dodge: the press is dropped, not kept for after them.
            if (MovementLocked || SlowWalk || Time.time - dodgeRequestTime > dodgeBufferTime)
            {
                dodgeRequested = false;
                return;
            }
            if (!DodgeReady) return;
            dodgeRequested = false;
            DodgeDirection direction = dodgeRequestAimed ? dodgeRequestDirection
                : TryHeldDirection(out DodgeDirection held) ? held : DodgeDirection.Back;
            TryDodge(direction);
        }

        /// <summary>The held direction key that went down last. False when none is held.</summary>
        private bool TryHeldDirection(out DodgeDirection direction)
        {
            int best = -1;
            for (int i = 0; i < heldKeys.Length; i++)
            {
                if (heldKeys[i] && (best < 0 || lastDownTime[i] > lastDownTime[best])) best = i;
            }
            direction = best >= 0 ? KeyDirections[best] : DodgeDirection.Back;
            return best >= 0;
        }

        /// <summary>A second press of the same direction key inside doubleTapWindow is a dodge that way.</summary>
        private bool DoubleTap(InputManager.GameAction action, KeyCode fallback, int slot)
        {
            if (!GameInput.GetActionDown(action, fallback)) return false;
            float now = Time.time;
            bool isDouble = now - lastTapTime[slot] <= doubleTapWindow;
            lastTapTime[slot] = isDouble ? -10f : now;
            return isDouble;
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
                bool aimed = TryHeldDirection(out DodgeDirection held);
                RequestDodge(aimed, held);
            }
            else if (!MovementLocked)
            {
                if (DoubleTap(InputManager.GameAction.Forward, KeyCode.W, 0)) RequestDodge(true, DodgeDirection.Forward);
                else if (DoubleTap(InputManager.GameAction.Back, KeyCode.S, 1)) RequestDodge(true, DodgeDirection.Back);
                else if (DoubleTap(InputManager.GameAction.TurnLeft, KeyCode.A, 2)) RequestDodge(true, DodgeDirection.Left);
                else if (DoubleTap(InputManager.GameAction.TurnRight, KeyCode.D, 3)) RequestDodge(true, DodgeDirection.Right);
                else if (newDirectionKey >= 0) AimLateKey(KeyDirections[newDirectionKey]);
            }
            ServeDodgeRequest();

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
                if (SlowWalk) speed *= slowWalkScale;
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
                Array.Clear(heldKeys, 0, heldKeys.Length);
                newDirectionKey = -1;
                return;
            }

            bool forward = GameInput.GetAction(InputManager.GameAction.Forward, KeyCode.W);
            bool back = GameInput.GetAction(InputManager.GameAction.Back, KeyCode.S);
            bool left = GameInput.GetAction(InputManager.GameAction.TurnLeft, KeyCode.A);
            bool right = GameInput.GetAction(InputManager.GameAction.TurnRight, KeyCode.D);
            heldKeys[0] = forward;
            heldKeys[1] = back;
            heldKeys[2] = left;
            heldKeys[3] = right;
            // When each key went down: with two keys held, the dodge takes the one pressed last.
            newDirectionKey = -1;
            if (GameInput.GetActionDown(InputManager.GameAction.Forward, KeyCode.W)) { lastDownTime[0] = Time.time; newDirectionKey = 0; }
            if (GameInput.GetActionDown(InputManager.GameAction.Back, KeyCode.S)) { lastDownTime[1] = Time.time; newDirectionKey = 1; }
            if (GameInput.GetActionDown(InputManager.GameAction.TurnLeft, KeyCode.A)) { lastDownTime[2] = Time.time; newDirectionKey = 2; }
            if (GameInput.GetActionDown(InputManager.GameAction.TurnRight, KeyCode.D)) { lastDownTime[3] = Time.time; newDirectionKey = 3; }
            MoveInput = (forward ? 1f : 0f) - (back ? 1f : 0f);
            TurnInput = (right ? 1f : 0f) - (left ? 1f : 0f);
            IsRunning = MoveInput > 0f && !SlowWalk && GameInput.GetAction(InputManager.GameAction.Run, KeyCode.LeftShift);
        }
    }
}
