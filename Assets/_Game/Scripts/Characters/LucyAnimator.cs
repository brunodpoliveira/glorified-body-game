using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Player;

namespace GlorifiedBody.Characters
{
    /// <summary>
    /// Drives Lucy's Animator (the "Lucy" controller of CreateCharacterAssets) from the tank controller, the
    /// lock-on and the health. Speed 0 idle, 0.5 walk, 1 run, 0.3 the slow walk of the short. Back = the walk
    /// backwards. Dodge, Charging and Dead are the other states. A hit that does not kill plays the vendor's small hit
    /// from the side of the shot on the upper body ("Hit", "HitDirection": Bruno, 2026-10-27). The model is a child, so the Animator is found
    /// in the children (and found again if the model changes).
    /// </summary>
    [RequireComponent(typeof(TankController))]
    public sealed class LucyAnimator : MonoBehaviour
    {
        public static readonly int SpeedHash = Animator.StringToHash("Speed");
        public static readonly int BackHash = Animator.StringToHash("Back");
        public static readonly int DodgeHash = Animator.StringToHash("Dodge");
        public static readonly int ChargingHash = Animator.StringToHash("Charging");
        public static readonly int DeadHash = Animator.StringToHash("Dead");
        public static readonly int HitHash = Animator.StringToHash("Hit");
        public static readonly int HitDirectionHash = Animator.StringToHash("HitDirection");
        public const float HitGap = 0.35f; // s between two flinches (a rifle burst gives one)

        private TankController tank;
        private LockOnController lockOn;
        private Health health;
        private Animator animator;

        public Animator Animator => animator;
        public float Speed { get; private set; }
        public int Hits { get; private set; }
        /// <summary>The side of the last flinch: 0 the front, 90 the right, -90 the left, 180 the back.</summary>
        public int LastHitDirection { get; private set; }

        private float nextHit;
        private Animator checkedAnimator;
        private bool hasHit;

        private void Awake()
        {
            tank = GetComponent<TankController>();
            lockOn = GetComponent<LockOnController>();
            health = GetComponent<Health>() ?? GetComponentInChildren<Health>();
        }

        private void OnEnable()
        {
            tank.OnDodge += OnDodge;
            HitEventBus.OnHit += OnHit;
        }

        private void OnDisable()
        {
            tank.OnDodge -= OnDodge;
            HitEventBus.OnHit -= OnHit;
        }

        private void OnDodge()
        {
            if (animator != null) animator.SetTrigger(DodgeHash);
        }

        private void OnHit(HitInfo hit)
        {
            if (hit == null || hit.Victim == null || hit.WasFatal || hit.DamageAmount <= 0.01f) return;
            if (hit.Victim != gameObject && !hit.Victim.transform.IsChildOf(transform)) return;
            if (animator == null || !animator.isActiveAndEnabled || (health != null && health.IsDead) || Time.time < nextHit) return;
            if (checkedAnimator != animator)
            {
                checkedAnimator = animator;
                hasHit = false;
                foreach (AnimatorControllerParameter p in animator.parameters) hasHit |= p.nameHash == HitHash;
            }
            if (!hasHit) return;
            nextHit = Time.time + HitGap;
            Vector3 from = hit.Attacker != null ? hit.Attacker.transform.position : hit.HitPoint + hit.HitNormal;
            LastHitDirection = DirectionOf(from);
            animator.SetInteger(HitDirectionHash, LastHitDirection);
            animator.SetTrigger(HitHash);
            Hits++;
        }

        /// <summary>The vendor's HitAngle rule: the side of the point, in steps of 90 degrees.</summary>
        public int DirectionOf(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            if (angle <= 45f && angle >= -45f) return 0;
            if (angle > 45f && angle < 135f) return 90;
            if (angle < -45f && angle > -135f) return -90;
            return 180;
        }

        private void Update()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                animator = GetComponentInChildren<Animator>();
                if (animator == null) return;
            }

            float wanted = 0f;
            if (!tank.IsDodging && Mathf.Abs(tank.MoveInput) > 0.01f)
            {
                wanted = tank.IsRunning ? 1f : 0.5f;
                if (tank.SlowWalk) wanted = 0.3f;
            }
            Speed = wanted;
            animator.SetFloat(SpeedHash, wanted, 0.08f, Time.deltaTime);
            animator.SetBool(BackHash, tank.MoveInput < -0.01f);
            animator.SetBool(ChargingHash, lockOn != null && lockOn.IsCharging);
            animator.SetBool(DeadHash, health != null && health.IsDead);
        }
    }
}
