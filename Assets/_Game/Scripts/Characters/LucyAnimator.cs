using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.Player;

namespace GlorifiedBody.Characters
{
    /// <summary>
    /// Drives Lucy's Animator (the "Lucy" controller of CreateCharacterAssets) from the tank controller, the
    /// lock-on and the health. Speed 0 idle, 0.5 walk, 1 run, 0.3 the slow walk of the short. Back = the walk
    /// backwards. Dodge, Charging and Dead are the other states. The model is a child, so the Animator is found
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

        private TankController tank;
        private LockOnController lockOn;
        private Health health;
        private Animator animator;

        public Animator Animator => animator;
        public float Speed { get; private set; }

        private void Awake()
        {
            tank = GetComponent<TankController>();
            lockOn = GetComponent<LockOnController>();
            health = GetComponent<Health>() ?? GetComponentInChildren<Health>();
        }

        private void OnEnable()
        {
            tank.OnDodge += OnDodge;
        }

        private void OnDisable()
        {
            tank.OnDodge -= OnDodge;
        }

        private void OnDodge()
        {
            if (animator != null) animator.SetTrigger(DodgeHash);
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
