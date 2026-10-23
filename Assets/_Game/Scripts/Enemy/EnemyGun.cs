using UnityEngine;
using GlorifiedBody.Core;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// The gun in a soldier's right hand (Bruno, 2026-10-23: "add the guns, bullets, effects (muzzle, smoke,
    /// impact), sounds of it"). The model and its particles come from the vendor's AI handgun, with its scripts
    /// removed (InvectorEnemyFactory). The hand bones of our T19 rigs do not match the vendor's grip, so the gun is
    /// not parented to the bone: every frame it sits at the hand and points along the aim (at the locked point
    /// during an aim, ahead and a little down otherwise). The telegraph gun (DummyShooter) shoots from its muzzle.
    /// On the aim: a click and the aim pose. On the shot: the flash particles, the smoke, a short light, a fire
    /// sound, the shot animation.
    /// </summary>
    [RequireComponent(typeof(DummyShooter))]
    public sealed class EnemyGun : MonoBehaviour
    {
        [SerializeField] private Transform gun;
        [SerializeField] private Transform muzzle;
        [SerializeField] private ParticleSystem[] flashes;
        [SerializeField] private ParticleSystem smoke;
        [SerializeField] private Light flashLight;
        [SerializeField] private AudioSource source;
        [SerializeField, Min(0f)] private float gripForward = 0.06f; // metres from the palm along the aim
        [SerializeField, Min(0f)] private float lightSeconds = 0.06f;
        [SerializeField, Range(0f, 45f)] private float restDownDegrees = 25f;

        private static readonly int UpperBodyId = Animator.StringToHash("UpperBody_ID");
        private static readonly int IsAimingId = Animator.StringToHash("IsAiming");
        private static readonly int CanAimId = Animator.StringToHash("CanAim");
        private static readonly int ShotId = Animator.StringToHash("Shot_ID");
        private static readonly int ShootId = Animator.StringToHash("Shoot");

        private DummyShooter shooter;
        private Animator animator;
        private Transform hand;
        private Health health;
        private float lightOffAt = -1f;
        private bool hasUpperBody, hasAiming, hasCanAim, hasShotId, hasShoot;

        public int Shots { get; private set; }
        public float LastFireTime { get; private set; } = -10f;
        public int AimClicks { get; private set; }
        public Transform Muzzle => muzzle;
        public Transform Model => gun;

        public void Bind(Transform gunRoot, Transform muzzlePoint, ParticleSystem[] flashSystems, ParticleSystem smokeSystem, Light light, AudioSource audioSource)
        {
            gun = gunRoot;
            muzzle = muzzlePoint;
            flashes = flashSystems;
            smoke = smokeSystem;
            flashLight = light;
            source = audioSource;
        }

        private void Awake()
        {
            shooter = GetComponent<DummyShooter>();
            health = GetComponent<Health>();
            animator = GetComponent<Animator>();
            if (animator != null && animator.isHuman) hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (animator != null)
            {
                foreach (AnimatorControllerParameter p in animator.parameters)
                {
                    if (p.nameHash == UpperBodyId) hasUpperBody = true;
                    else if (p.nameHash == IsAimingId) hasAiming = true;
                    else if (p.nameHash == CanAimId) hasCanAim = true;
                    else if (p.nameHash == ShotId) hasShotId = true;
                    else if (p.nameHash == ShootId) hasShoot = true;
                }
            }
            if (muzzle != null) shooter.MuzzleOverride = muzzle;
            if (flashLight != null) flashLight.enabled = false;
        }

        private void OnEnable()
        {
            shooter.OnAimStarted += OnAimStarted;
            shooter.OnAimEnded += OnAimEnded;
            shooter.OnFired += OnFired;
        }

        private void OnDisable()
        {
            shooter.OnAimStarted -= OnAimStarted;
            shooter.OnAimEnded -= OnAimEnded;
            shooter.OnFired -= OnFired;
        }

        private void Start()
        {
            // The pistol set of the vendor animator: the upper body holds a handgun.
            if (animator != null && hasUpperBody) animator.SetFloat(UpperBodyId, 1f);
            if (animator != null && hasShotId) animator.SetFloat(ShotId, 1f);
        }

        private void LateUpdate()
        {
            if (gun == null) return;
            if (flashLight != null && flashLight.enabled && Time.time >= lightOffAt) flashLight.enabled = false;
            if (hand == null) return;

            Vector3 aim;
            if (shooter.HoldsSlot && !(health != null && health.IsDead))
            {
                aim = shooter.AimPoint - hand.position;
            }
            else
            {
                Vector3 ahead = transform.forward;
                ahead.y = 0f;
                aim = Quaternion.AngleAxis(restDownDegrees, Vector3.Cross(Vector3.up, ahead.normalized)) * ahead.normalized;
            }
            if (aim.sqrMagnitude < 0.0001f) aim = transform.forward;
            // The vendor model's barrel is not its own forward: the muzzle's forward is the barrel (Invector shoots
            // along it). Turn the gun so the muzzle looks along the aim.
            Quaternion muzzleLocal = muzzle != null ? Quaternion.Inverse(gun.rotation) * muzzle.rotation : Quaternion.identity;
            gun.rotation = Quaternion.LookRotation(aim.normalized, Vector3.up) * Quaternion.Inverse(muzzleLocal);
            gun.position = hand.position + aim.normalized * gripForward;
        }

        private void OnAimStarted()
        {
            AimClicks++;
            ShotEffects fx = ShotEffects.Load();
            if (source != null && fx != null && fx.aimClip != null) source.PlayOneShot(fx.aimClip, 0.7f);
            SetAim(true);
        }

        private void OnAimEnded() => SetAim(false);

        private void SetAim(bool on)
        {
            if (animator == null) return;
            if (hasAiming) animator.SetBool(IsAimingId, on);
            if (hasCanAim) animator.SetBool(CanAimId, on);
        }

        private void OnFired(Vector3 origin, Vector3 direction)
        {
            Shots++;
            LastFireTime = Time.time;
            if (flashes != null)
            {
                foreach (ParticleSystem flash in flashes) if (flash != null) flash.Play(true);
            }
            if (smoke != null) smoke.Play(true);
            if (flashLight != null)
            {
                flashLight.enabled = true;
                lightOffAt = Time.time + lightSeconds;
            }
            ShotEffects fx = ShotEffects.Load();
            if (source != null && fx != null && fx.fireClips != null && fx.fireClips.Length > 0)
            {
                AudioClip clip = fx.fireClips[Random.Range(0, fx.fireClips.Length)];
                source.pitch = Random.Range(0.95f, 1.05f);
                if (clip != null) source.PlayOneShot(clip);
            }
            if (animator != null && hasShoot) animator.SetTrigger(ShootId);
        }
    }
}
