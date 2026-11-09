using UnityEngine;
using GlorifiedBody.Characters;
using GlorifiedBody.Core;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// One pose for every power (DEMO_PLAN §1, CANON 2026-11-05: "she lifts one arm, the palm out. With mk_orzabal the arm
    /// follows the floater"). While she charges a power, or holds a thing in the air, her right arm rises to the aim: the
    /// held thing, else the locked target, else straight ahead. The arm on the target's side: the right one, or the left one
    /// for a target on her left (2026-11-08: a floater to her left was out of the right arm's reach). The arm muscles of the humanoid pose (as PleaHands), so one
    /// set of numbers fits every model she wears: the forearm straight, the hand bent back with the palm out. Two muscles
    /// aim it, Arm Down-Up and Arm Front-Back, by a small solver each frame (it measures the arm and steps the muscles). It
    /// eases in and out over the animation. PsychicPowers adds it. PROPOSED.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class PowerArm : MonoBehaviour
    {
        public const float EasePerSecond = 6f;
        private const float Probe = 0.05f;

        // The fixed part of the pose: Unity muscle names (after "Right " or "Left ") and values (-1..1).
        private static readonly (string muscle, float value)[] Fixed =
        {
            ("Shoulder Down-Up", 0.25f),
            ("Arm Twist In-Out", 0f),
            ("Forearm Stretch", 1f),
            ("Forearm Twist In-Out", -0.6f),
            ("Hand Down-Up", -0.7f),
            ("Hand In-Out", 0f),
        };
        /// <summary>The yaw (degrees) past the middle that makes her change arms.</summary>
        public const float SwitchYaw = 12f;

        private sealed class Side
        {
            public int[] fixedIndex;
            public float[] fixedValue;
            public int downUp, frontBack;
            public float du = 0.3f, fb = -0.6f;
            public Transform upperArm, hand;
        }

        public float Weight { get; private set; }
        /// <summary>The angle between her arm (shoulder to hand) and the line to the aim, in degrees (the tests).</summary>
        public float AimError { get; private set; } = 180f;
        public Vector3 AimPoint { get; private set; }
        /// <summary>The two aiming muscles now, and the aim as yaw and pitch in her frame (the tests).</summary>
        public Vector2 Muscles => new Vector2(arm.du, arm.fb);
        public Vector2 AimAngles { get; private set; }

        private PsychicPowers powers;
        private LockOnController lockOn;
        private Health health;
        private Animator animator;
        private HumanPoseHandler handler;
        private HumanPose pose;
        private Transform hips;
        private readonly Side right = new Side(), left = new Side();
        private Side arm;
        private float[] basePose;

        /// <summary>The arm she raises now: true for the left one (the tests).</summary>
        public bool LeftArm => arm == left;

        private void Awake()
        {
            powers = GetComponent<PsychicPowers>();
            lockOn = GetComponent<LockOnController>();
            health = GetComponentInChildren<Health>();
            foreach ((Side side, string name) in new[] { (right, "Right "), (left, "Left ") })
            {
                var named = new (string, float)[Fixed.Length];
                for (int i = 0; i < Fixed.Length; i++) named[i] = (name + Fixed[i].muscle, Fixed[i].value);
                PleaHands.Resolve(named, out side.fixedIndex, out side.fixedValue);
                side.downUp = System.Array.IndexOf(HumanTrait.MuscleName, name + "Arm Down-Up");
                side.frontBack = System.Array.IndexOf(HumanTrait.MuscleName, name + "Arm Front-Back");
            }
            arm = right;
        }

        private void OnDestroy() => handler?.Dispose();

        private bool Bind()
        {
            Animator now = GetComponentInChildren<Animator>();
            if (now == animator && handler != null) return true;
            handler?.Dispose();
            handler = null;
            animator = now;
            if (animator == null || !animator.isHuman || animator.avatar == null) return false;
            handler = new HumanPoseHandler(animator.avatar, animator.transform);
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            right.upperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            right.hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            left.upperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            left.hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            return right.upperArm != null && right.hand != null && left.upperArm != null && left.hand != null;
        }

        private bool Active => powers != null && (powers.ShownCharge > 0f || (lockOn != null && lockOn.HeldThing != null));

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Weight = Mathf.MoveTowards(Weight, Active && (health == null || !health.IsDead) ? 1f : 0f, dt * EasePerSecond);
            if (Weight <= 0.001f || !Bind() || !animator.enabled) return;

            AimPoint = Target();
            // The arm on the target's side, with a dead zone in the middle.
            float side = Angles(AimPoint - (right.upperArm.position + left.upperArm.position) * 0.5f).x;
            Side want0 = side < -SwitchYaw ? left : side > SwitchYaw ? right : arm;
            if (want0 != arm) { arm = want0; Weight = Mathf.Min(Weight, 0.3f); } // a short ease into the other arm
            Vector3 at = hips != null ? hips.position : Vector3.zero;
            Quaternion turn = hips != null ? hips.rotation : Quaternion.identity;
            handler.GetHumanPose(ref pose);
            if (basePose == null || basePose.Length != pose.muscles.Length) basePose = new float[pose.muscles.Length];
            System.Array.Copy(pose.muscles, basePose, pose.muscles.Length);

            // One step of the solver: the arm at (du, fb), and a probe on each muscle, measured as yaw and pitch in her frame.
            Vector2 want = Angles(AimPoint - Shoulder());
            AimAngles = want;
            Vector2 now = Measure(arm.du, arm.fb);
            Vector2 dDu = (Measure(arm.du + Probe, arm.fb) - now) / Probe;
            Vector2 dFb = (Measure(arm.du, arm.fb + Probe) - now) / Probe;
            Vector2 error = new Vector2(Mathf.DeltaAngle(now.x, want.x), want.y - now.y);
            float det = dDu.x * dFb.y - dFb.x * dDu.y;
            if (Mathf.Abs(det) > 1e-3f)
            {
                float stepDu = (error.x * dFb.y - dFb.x * error.y) / det;
                float stepFb = (dDu.x * error.y - error.x * dDu.y) / det;
                arm.du = Mathf.Clamp(arm.du + Mathf.Clamp(stepDu, -0.2f, 0.2f) * 0.7f, -1f, 1f);
                arm.fb = Mathf.Clamp(arm.fb + Mathf.Clamp(stepFb, -0.2f, 0.2f) * 0.7f, -1f, 1f);
            }

            // The pose of this frame, by the weight over the animation.
            for (int i = 0; i < pose.muscles.Length; i++) pose.muscles[i] = basePose[i];
            for (int k = 0; k < arm.fixedIndex.Length; k++) pose.muscles[arm.fixedIndex[k]] = Mathf.Lerp(basePose[arm.fixedIndex[k]], arm.fixedValue[k], Weight);
            pose.muscles[arm.downUp] = Mathf.Lerp(basePose[arm.downUp], arm.du, Weight);
            pose.muscles[arm.frontBack] = Mathf.Lerp(basePose[arm.frontBack], arm.fb, Weight);
            handler.SetHumanPose(ref pose);
            if (hips != null) hips.SetPositionAndRotation(at, turn);
            AimError = Vector3.Angle(arm.hand.position - Shoulder(), AimPoint - Shoulder());
        }

        private Vector3 Shoulder() => arm.upperArm.position;

        private Vector3 Target()
        {
            if (lockOn != null && lockOn.HeldThing != null) return lockOn.HeldThing.position; // mk_orzabal: the arm follows the floater
            if (lockOn != null && lockOn.CurrentTarget != null) return lockOn.CurrentTarget.GetLockPoint();
            return transform.position + Vector3.up * Body.ChestHeight + transform.forward * 3f;
        }

        // The arm at these two muscles, full weight: the direction shoulder to hand as yaw and pitch (degrees) in her frame.
        private Vector2 Measure(float downUpValue, float frontBackValue)
        {
            Vector3 at = hips != null ? hips.position : Vector3.zero;
            Quaternion turn = hips != null ? hips.rotation : Quaternion.identity;
            for (int i = 0; i < pose.muscles.Length; i++) pose.muscles[i] = basePose[i];
            for (int k = 0; k < arm.fixedIndex.Length; k++) pose.muscles[arm.fixedIndex[k]] = arm.fixedValue[k];
            pose.muscles[arm.downUp] = downUpValue;
            pose.muscles[arm.frontBack] = frontBackValue;
            handler.SetHumanPose(ref pose);
            if (hips != null) hips.SetPositionAndRotation(at, turn);
            return Angles(arm.hand.position - Shoulder());
        }

        private Vector2 Angles(Vector3 world)
        {
            Vector3 local = transform.InverseTransformDirection(world.normalized);
            return new Vector2(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg);
        }
    }
}
