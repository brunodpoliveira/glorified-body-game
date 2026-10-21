using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;

namespace GlorifiedBody.Characters
{
    /// <summary>
    /// Drives an enemy's Animator (the "Enemy" controller of CreateCharacterAssets) from the dummy shooter, the
    /// health and the telekinesis lift. Aiming while it holds a shot slot, Stagger on the stagger, Airborne in
    /// the air, Dead at death. No walk yet: the FSM AI brings it (Bruno, 2026-10-21: an enemy moves or shoots,
    /// never both at once).
    /// </summary>
    public sealed class EnemyAnimator : MonoBehaviour
    {
        public static readonly int SpeedHash = Animator.StringToHash("Speed");
        public static readonly int AimingHash = Animator.StringToHash("Aiming");
        public static readonly int StaggerHash = Animator.StringToHash("Stagger");
        public static readonly int AirborneHash = Animator.StringToHash("Airborne");
        public static readonly int DeadHash = Animator.StringToHash("Dead");

        private DummyShooter shooter;
        private Health health;
        private Animator animator;
        private DummyShooter.State lastState = DummyShooter.State.Idle;

        public Animator Animator => animator;

        private void Awake()
        {
            shooter = GetComponent<DummyShooter>();
            health = GetComponent<Health>() ?? GetComponentInChildren<Health>();
        }

        private void Update()
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                animator = GetComponentInChildren<Animator>();
                if (animator == null) return;
            }
            if (shooter == null) shooter = GetComponent<DummyShooter>();

            animator.SetFloat(SpeedHash, 0f);
            bool aiming = shooter != null && shooter.isActiveAndEnabled && shooter.HoldsSlot;
            animator.SetBool(AimingHash, aiming);
            if (shooter != null)
            {
                if (shooter.Current == DummyShooter.State.Staggered && lastState != DummyShooter.State.Staggered) animator.SetTrigger(StaggerHash);
                lastState = shooter.Current;
            }
            TelekinesisLift lift = GetComponent<TelekinesisLift>();
            animator.SetBool(AirborneHash, lift != null && lift.IsLifted);
            animator.SetBool(DeadHash, health != null && health.IsDead);
        }
    }
}
