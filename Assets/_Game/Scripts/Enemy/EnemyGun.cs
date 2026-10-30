using UnityEngine;
using GlorifiedBody.Core;
using GlorifiedBody.Combat;

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
    /// A knife (2026-10-27) uses the same hand: its tip is the muzzle, it has no flash, and its strike comes down
    /// from over the shoulder. The armed lab worker holds its pistol in both hands (twoHanded) and the gun shakes
    /// (shakeDegrees): no fighter, a scientist with a gun.
    /// </summary>
    [RequireComponent(typeof(DummyShooter))]
    [DefaultExecutionOrder(30)] // after Tremble (20): a shaking hand carries its gun (the armed lab worker)
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
        [SerializeField] private bool twoHanded;                       // a pistol in both hands
        [SerializeField, Range(0f, 15f)] private float shakeDegrees;    // the gun's tremble, PROPOSED
        [SerializeField, Min(0.1f)] private float shakeFrequency = 7f;  // noise steps per second

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
        private Transform leftGrip, leftUpper, leftLower, leftHand;
        private Transform rightUpper, rightLower, rightKnuckle, leftKnuckle;
        private Vector3 stockLocal; // the butt of a long gun, in the gun's space
        private bool shouldered;
        private Vector3 heldAim;
        private const float RaiseRate = 14f; // per second
        private IRagdollBody ragdoll;
        private SoldierBrain brain;
        private HitReactions reactions;

        // A long gun sits on the right shoulder (Bruno, 2026-10-24: "the right arm is clearly misaligned ... the left
        // arm is too high up"): the butt in the shoulder pocket, then both hands go to the gun. PROPOSED numbers.
        private const float PocketIn = 0.08f, PocketForward = 0.07f, PocketDown = 0.05f; // metres from the shoulder joint
        private const float LowReadyDown = 35f, LowReadyIn = 20f; // degrees: the gun ahead and down, across the body

        public int Shots { get; private set; }
        public float LastFireTime { get; private set; } = -10f;
        public int AimClicks { get; private set; }
        public int MeleeSwings { get; private set; }
        public Transform Muzzle => muzzle;
        public Transform Model => gun;
        public bool TwoHanded => twoHanded;
        public float ShakeDegrees => shakeDegrees;
        /// <summary>The gun's tremble in the last frame, in degrees (0 when still). For the tests.</summary>
        public float LastShake { get; private set; }

        /// <summary>The armed lab worker's hold: both hands on the pistol, and the gun shakes by up to the degrees.</summary>
        public void SetHold(bool bothHands, float shake)
        {
            twoHanded = bothHands;
            shakeDegrees = shake;
        }

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
            // The gun follows the SFX slider and the shared 3D curve (2026-10-26).
            if (source != null) GlorifiedBody.Audio.GameAudio.Bind(source, GlorifiedBody.Audio.SoundChannel.Sfx, 1f);
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
            // One flash for each shot: the vendor rifle's first flash loops (its own script stopped it), so it
            // stayed on after the first burst (Bruno, 2026-10-24).
            if (flashes != null)
                foreach (ParticleSystem flash in flashes)
                    if (flash != null)
                        foreach (ParticleSystem ps in flash.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            ParticleSystem.MainModule main = ps.main;
                            main.loop = false;
                        }
            // A long gun is held with both hands: the left hand goes to the vendor's grip point on the gun.
            if (animator != null && animator.isHuman && (upperBodyId != 1 || twoHanded) && gun != null)
            {
                leftGrip = FindDeep(gun, "leftHandIK");
                leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                leftLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                leftKnuckle = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
                rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                rightLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                rightKnuckle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                shouldered = upperBodyId != 1 && rightUpper != null && rightLower != null && hand != null && muzzle != null && FindStock(out stockLocal);
            }
            ragdoll = GetComponent<IRagdollBody>();
            brain = GetComponent<SoldierBrain>();
            reactions = GetComponent<HitReactions>();
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>The butt of the gun: the rear end of its meshes along the barrel, at the meshes' middle.</summary>
        private bool FindStock(out Vector3 stock)
        {
            stock = Vector3.zero;
            Vector3 barrel = Quaternion.Inverse(gun.rotation) * muzzle.forward;
            bool any = false;
            float rear = float.MaxValue;
            Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
            foreach (MeshFilter filter in gun.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<Renderer>() == null) continue;
                Bounds b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 local = gun.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                    rear = Mathf.Min(rear, Vector3.Dot(local, barrel));
                    any = true;
                }
            }
            if (!any) return false;
            Vector3 middle = (min + max) * 0.5f;
            stock = middle + barrel * (rear - Vector3.Dot(middle, barrel));
            return true;
        }

        /// <summary>
        /// Two-bone reach with a bend hint: the elbow goes to the side of the hint, the hand to the target (or as
        /// near as the arm reaches). Then the hand turns so its knuckles point along the wanted line.
        /// </summary>
        public static void Reach(Transform upper, Transform lower, Transform hand, Transform knuckle, Vector3 target, Vector3 bendHint, Vector3 fingers)
        {
            if (upper == null || lower == null || hand == null) return;
            Vector3 a = upper.position, b = lower.position, c = hand.position;
            float l1 = (b - a).magnitude, l2 = (c - b).magnitude;
            if (l1 < 0.0001f || l2 < 0.0001f) return;
            Vector3 toTarget = target - a;
            if (toTarget.sqrMagnitude < 0.000001f) return;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(l1 - l2) + 0.001f, l1 + l2 - 0.001f);
            Vector3 dir = toTarget.normalized;
            Vector3 bend = bendHint - Vector3.Dot(bendHint, dir) * dir;
            if (bend.sqrMagnitude < 0.000001f) bend = (b - a) - Vector3.Dot(b - a, dir) * dir;
            bend.Normalize();
            float along = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
            Vector3 elbow = a + dir * along + bend * Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - along * along));
            upper.rotation = Quaternion.FromToRotation(b - a, elbow - a) * upper.rotation;
            b = lower.position;
            lower.rotation = Quaternion.FromToRotation(hand.position - b, a + dir * d - b) * lower.rotation;
            if (knuckle != null && fingers.sqrMagnitude > 0.0001f)
                hand.rotation = Quaternion.FromToRotation(knuckle.position - hand.position, fingers) * hand.rotation;
        }

        // The fire panic (HitReactions.Writhing): the hands leave the gun to beat at the flames, the gun stays in the right hand.
        private bool HandsFree => (ragdoll != null && ragdoll.IsRagdolled) || (health != null && health.IsDead)
            || (brain != null && brain.Current == SoldierBrain.Phase.Care) || (reactions != null && reactions.Writhing);

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
            else if (shouldered && !HandsFree)
            {
                Shoulder(flatForward);
                return;
            }
            else if (shooter.HoldsSlot && shooter.CurrentAttack != DummyShooter.Attack.Melee && !(health != null && health.IsDead))
            {
                aim = shooter.AimPoint - hand.position;
            }
            else
            {
                aim = Quaternion.AngleAxis(restDownDegrees, Vector3.Cross(Vector3.up, flatForward)) * flatForward;
            }
            if (aim.sqrMagnitude < 0.0001f) aim = transform.forward;
            aim = Shake(aim);
            Point(aim);
            gun.position = hand.position + aim.normalized * gripForward;
            if (leftGrip != null && !HandsFree) Reach(leftUpper, leftLower, leftHand, null, leftGrip.position, -transform.up - transform.right * 0.3f, Vector3.zero);
        }

        /// <summary>The tremble of a shaky hold: noise turns of the aim, up to shakeDegrees on each axis.</summary>
        private Vector3 Shake(Vector3 aim)
        {
            LastShake = 0f;
            if (shakeDegrees <= 0f || (health != null && health.IsDead) || HandsFree) return aim;
            float t = Time.time * shakeFrequency;
            float yaw = (Mathf.PerlinNoise(t, 3.7f) - 0.5f) * 2f * shakeDegrees;
            float pitch = (Mathf.PerlinNoise(11.3f, t) - 0.5f) * 2f * shakeDegrees;
            LastShake = Mathf.Max(Mathf.Abs(yaw), Mathf.Abs(pitch));
            Vector3 side = Vector3.Cross(Vector3.up, aim);
            if (side.sqrMagnitude < 0.0001f) side = transform.right;
            return Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, side.normalized) * aim;
        }

        /// <summary>
        /// The vendor model's barrel is not its own forward: the muzzle's forward is the barrel (Invector shoots
        /// along it). Turn the gun so the muzzle looks along the aim.
        /// </summary>
        private void Point(Vector3 aim)
        {
            Quaternion muzzleLocal = muzzle != null ? Quaternion.Inverse(gun.rotation) * muzzle.rotation : Quaternion.identity;
            gun.rotation = Quaternion.LookRotation(aim.normalized, Vector3.up) * Quaternion.Inverse(muzzleLocal);
        }

        /// <summary>
        /// A long gun on the shoulder: the butt in the right shoulder pocket, the barrel along the aim (or at a low
        /// ready, ahead, down and across the body), the right hand on the grip (the vendor gun's root), the left
        /// hand on the vendor's grip point. The elbows hang down. The animation keeps the legs and the body.
        /// </summary>
        private void Shoulder(Vector3 flatForward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, flatForward);
            Vector3 pocket = rightUpper.position - right * PocketIn + flatForward * PocketForward - Vector3.up * PocketDown;
            Vector3 aim = shooter.HoldsSlot ? shooter.AimPoint - pocket
                : Quaternion.AngleAxis(-LowReadyIn, Vector3.up) * Quaternion.AngleAxis(LowReadyDown, right) * flatForward;
            if (aim.sqrMagnitude < 0.0001f) aim = flatForward;
            // The gun rises to the aim and drops to the low ready in about a fifth of a second, not in one frame.
            heldAim = heldAim.sqrMagnitude < 0.5f ? aim.normalized : Vector3.Slerp(heldAim, aim.normalized, 1f - Mathf.Exp(-RaiseRate * Time.deltaTime));
            aim = heldAim;
            Point(aim);
            gun.position += pocket - gun.TransformPoint(stockLocal);
            Vector3 gunRight = Vector3.Cross(Vector3.up, aim).normalized;
            Vector3 gunUp = Vector3.Cross(aim, gunRight);
            Reach(rightUpper, rightLower, hand, rightKnuckle, gun.position - aim * gripForward, -Vector3.up + right * 0.5f - flatForward * 0.2f, aim * 0.8f - gunUp * 0.5f);
            if (leftGrip != null)
                Reach(leftUpper, leftLower, leftHand, leftKnuckle, leftGrip.position, -Vector3.up - right * 0.4f, aim * 0.7f + gunRight * 0.5f + gunUp * 0.2f);
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
