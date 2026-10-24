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
    /// sound, the shot animation. The weapon (DummyShooter.WeaponKind) picks the sound set and the vendor
    /// animator's weapon set (upperBodyId, shotId). A melee strike plays the vendor attack and a swing or hit sound.
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
        [SerializeField] private int upperBodyId = 1; // the vendor weapon's own ids: handgun 1, rifle 2, shotgun 3
        [SerializeField] private int shotId = 1;

        private static readonly int UpperBodyId = Animator.StringToHash("UpperBody_ID");
        private static readonly int IsAimingId = Animator.StringToHash("IsAiming");
        private static readonly int CanAimId = Animator.StringToHash("CanAim");
        private static readonly int ShotId = Animator.StringToHash("Shot_ID");
        private static readonly int ShootId = Animator.StringToHash("Shoot");
        private static readonly int WeakAttackId = Animator.StringToHash("WeakAttack");

        private DummyShooter shooter;
        private Animator animator;
        private Transform hand;
        private Health health;
        private float lightOffAt = -1f;
        private float swingStart = -10f;
        private const float SwingSeconds = 0.35f;
        private bool hasUpperBody, hasAiming, hasCanAim, hasShotId, hasShoot, hasWeakAttack;

        public int Shots { get; private set; }
        public float LastFireTime { get; private set; } = -10f;
        public int AimClicks { get; private set; }
        public int MeleeSwings { get; private set; }
        public Transform Muzzle => muzzle;
        public Transform Model => gun;

        public void Bind(Transform gunRoot, Transform muzzlePoint, ParticleSystem[] flashSystems, ParticleSystem smokeSystem, Light light, AudioSource audioSource, int upperBody = 1, int shot = 1)
        {
            upperBodyId = upperBody;
            shotId = shot;
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
                    else if (p.nameHash == WeakAttackId) hasWeakAttack = true;
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
            shooter.OnMelee += OnMelee;
        }

        private void OnDisable()
        {
            shooter.OnAimStarted -= OnAimStarted;
            shooter.OnAimEnded -= OnAimEnded;
            shooter.OnFired -= OnFired;
            shooter.OnMelee -= OnMelee;
        }

        private void Start()
        {
            // The pistol set of the vendor animator: the upper body holds a handgun.
            if (animator != null && hasUpperBody) animator.SetFloat(UpperBodyId, upperBodyId);
            if (animator != null && hasShotId) animator.SetFloat(ShotId, shotId);
        }

        private void LateUpdate()
        {
            if (gun == null) return;
            if (flashLight != null && flashLight.enabled && Time.time >= lightOffAt) flashLight.enabled = false;
            if (hand == null) return;

            Vector3 aim;
            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;
            flatForward = flatForward.sqrMagnitude > 0.0001f ? flatForward.normalized : Vector3.forward;
            float swing = (Time.time - swingStart) / SwingSeconds;
            bool windUp = shooter.HoldsSlot && shooter.CurrentAttack == DummyShooter.Attack.Melee;
            if (swing >= 0f && swing < 1f && !(health != null && health.IsDead))
            {
                // The strike: the gun comes down from over the shoulder, butt first, to the front.
                aim = Vector3.Slerp(Vector3.up * 0.9f + flatForward * 0.1f, flatForward * 0.7f + Vector3.down * 0.7f, Mathf.SmoothStep(0f, 1f, swing));
            }
            else if (windUp && !(health != null && health.IsDead))
            {
                // The wind-up: the gun rises over the shoulder while the circle closes (the telegraph of a strike).
                float up = shooter.Current == DummyShooter.State.Telegraph ? shooter.TelegraphProgress : 0f;
                aim = Vector3.Slerp(flatForward, Vector3.up * 0.9f + flatForward * 0.1f, 0.3f + 0.7f * up);
            }
            else if (shooter.HoldsSlot && shooter.CurrentAttack != DummyShooter.Attack.Melee && !(health != null && health.IsDead))
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
            if (shooter.CurrentAttack == DummyShooter.Attack.Melee) return; // a strike has no aim pose and no click
            AimClicks++;
            ShotEffects fx = ShotEffects.Load();
            AudioClip aim = fx == null ? null
                : shooter.WeaponKind == DummyShooter.Weapon.Rifle && fx.rifleAimClip != null ? fx.rifleAimClip
                : shooter.WeaponKind != DummyShooter.Weapon.Pistol && fx.launcherAimClip != null ? fx.launcherAimClip
                : fx.aimClip;
            if (source != null && aim != null) source.PlayOneShot(aim, 0.7f);
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
            AudioClip[] set = fx == null ? null
                : shooter.CurrentAttack == DummyShooter.Attack.Rocket && fx.rocketFireClips != null && fx.rocketFireClips.Length > 0 ? fx.rocketFireClips
                : DummyShooter.IsLobbed(shooter.CurrentAttack) && fx.launcherFireClips != null && fx.launcherFireClips.Length > 0 ? fx.launcherFireClips
                : shooter.WeaponKind == DummyShooter.Weapon.Rifle && fx.rifleFireClips != null && fx.rifleFireClips.Length > 0 ? fx.rifleFireClips
                : fx.fireClips;
            if (source != null && set != null && set.Length > 0)
            {
                AudioClip clip = set[Random.Range(0, set.Length)];
                source.pitch = Random.Range(0.95f, 1.05f);
                if (clip != null) source.PlayOneShot(clip);
            }
            if (animator != null && hasShoot) animator.SetTrigger(ShootId);
        }

        private void OnMelee(bool hit)
        {
            MeleeSwings++;
            swingStart = Time.time;
            if (animator != null && hasWeakAttack) animator.SetTrigger(WeakAttackId);
            ShotEffects fx = ShotEffects.Load();
            if (fx != null) ShotEffects.PlayAt(hit ? fx.meleeHitClips : fx.meleeSwingClips, transform.position + Vector3.up * 1.2f);
        }
    }
}
