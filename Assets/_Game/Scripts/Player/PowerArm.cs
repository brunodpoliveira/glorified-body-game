using UnityEngine;
using GlorifiedBody.Characters;
using GlorifiedBody.Core;

namespace GlorifiedBody.Player
{
    /// <summary>
    /// One pose for every power (DEMO_PLAN §1, CANON 2026-11-05: "she lifts one arm, the palm out. With mk_orzabal the arm
    /// follows the floater"). Bruno, 2026-11-09, with three stills of a man who stops bullets with one raised hand: "her arm
    /// is obviously going through her head; she's still doing the rest of the pistol shot pose ... i asked for this type of
    /// pose". So the whole stance, not one arm over the animation:
    ///   the aim arm (the side of the target) reaches out near the height of the shoulder, on its own side of the body, the
    ///     elbow almost straight, the hand bent back with the palm to the target and the fingers open. It follows the aim (the
    ///     held thing, else the locked target, else ahead), but only inside that range: a target high over her (the brain at
    ///     its eye) does not bring the arm over her head;
    ///   the other arm hangs at her side;
    ///   the spine, the chest, the neck and the head lose the twist and the lean of the animation: she stands square to it.
    /// The muscles of the humanoid pose (as PleaHands), so the numbers fit every model she wears. A small solver aims each
    /// arm each frame (it measures the arm and steps two muscles). It eases in and out over the animation while she charges
    /// a power or holds a thing in the air. PsychicPowers adds it. PROPOSED.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class PowerArm : MonoBehaviour
    {
        public const float EasePerSecond = 6f;
        private const float Probe = 0.05f;
        /// <summary>The aim arm's range, in degrees in her frame: the pitch, and the yaw toward the arm's own side.</summary>
        public const float PitchDown = -25f, PitchUp = 12f, YawAcross = -10f, YawOut = 75f;
        /// <summary>The yaw (degrees) past the middle that makes her change arms.</summary>
        public const float SwitchYaw = 12f;

        // The aim arm: the forearm straight, the palm down, the wrist straight, the fingers open (after "Right " or "Left ").
        // The hand bends up after the pose, on the bone (BendHandUp): the wrist muscle barely moved the mitten hands of the
        // models (2026-11-09, the side shot).
        private static readonly (string muscle, float value)[] AimFixed =
        {
            ("Shoulder Down-Up", 0.15f), ("Shoulder Front-Back", 0f), ("Arm Twist In-Out", 0f), ("Forearm Stretch", 1f),
            ("Forearm Twist In-Out", 0.7f), ("Hand Down-Up", 0f), ("Hand In-Out", 0f),
            ("Thumb 1 Stretched", 0.3f), ("Thumb 2 Stretched", 0.6f), ("Thumb 3 Stretched", 0.6f), ("Thumb Spread", 0.2f),
            ("Index 1 Stretched", 0.9f), ("Index 2 Stretched", 0.9f), ("Index 3 Stretched", 0.9f), ("Index Spread", 0f),
            ("Middle 1 Stretched", 0.9f), ("Middle 2 Stretched", 0.9f), ("Middle 3 Stretched", 0.9f), ("Middle Spread", 0f),
            ("Ring 1 Stretched", 0.9f), ("Ring 2 Stretched", 0.9f), ("Ring 3 Stretched", 0.9f), ("Ring Spread", 0f),
            ("Little 1 Stretched", 0.9f), ("Little 2 Stretched", 0.9f), ("Little 3 Stretched", 0.9f), ("Little Spread", 0f),
        };
        // The other arm, at rest: the forearm a little bent, the hand straight, the fingers relaxed.
        private static readonly (string muscle, float value)[] RestFixed =
        {
            ("Shoulder Down-Up", 0f), ("Shoulder Front-Back", 0f), ("Arm Twist In-Out", 0f), ("Forearm Stretch", 0.8f),
            ("Forearm Twist In-Out", 0f), ("Hand Down-Up", 0f), ("Hand In-Out", 0f),
            ("Index 1 Stretched", 0.3f), ("Index 2 Stretched", 0.3f), ("Index 3 Stretched", 0.3f),
            ("Middle 1 Stretched", 0.3f), ("Middle 2 Stretched", 0.3f), ("Middle 3 Stretched", 0.3f),
            ("Ring 1 Stretched", 0.3f), ("Ring 2 Stretched", 0.3f), ("Ring 3 Stretched", 0.3f),
            ("Little 1 Stretched", 0.3f), ("Little 2 Stretched", 0.3f), ("Little 3 Stretched", 0.3f),
        };
        // Square to the target: the twist and the lean of the animation go.
        private static readonly string[] Upright =
        {
            "Spine Front-Back", "Spine Left-Right", "Spine Twist Left-Right", "Chest Front-Back", "Chest Left-Right",
            "Chest Twist Left-Right", "UpperChest Front-Back", "UpperChest Left-Right", "UpperChest Twist Left-Right",
            "Neck Nod Down-Up", "Neck Tilt Left-Right", "Neck Turn Left-Right", "Head Nod Down-Up", "Head Tilt Left-Right",
            "Head Turn Left-Right",
        };

        private sealed class Side
        {
            public int[] aimIndex, restIndex;
            public float[] aimValue, restValue;
            public int downUp, frontBack;
            public float aimDu = 0.3f, aimFb = -0.6f, restDu = -0.6f, restFb = 0f;
            public Transform upperArm, lowerArm, hand;
            public float outward; // +1 right, -1 left: the yaw toward this arm's own side
        }

        public float Weight { get; private set; }
        /// <summary>The angle between the aim arm (shoulder to hand) and its aim inside its range, in degrees (the tests).</summary>
        public float AimError { get; private set; } = 180f;
        public Vector3 AimPoint { get; private set; }
        public Vector2 AimAngles { get; private set; }
        /// <summary>The hands now (the tests): the aim hand, the other hand, and the head.</summary>
        public Vector3 AimHand => arm.hand.position;
        public Vector3 RestHand => Other.hand.position;
        public Vector3 Head => head != null ? head.position : transform.position + Vector3.up * 1.6f;
        public Vector3 AimShoulder => arm.upperArm.position;
        /// <summary>The bend of the aim elbow in degrees (0 straight), and the reach as a share of the arm's length (the tests).</summary>
        public float ElbowBend => arm.lowerArm != null ? Vector3.Angle(arm.lowerArm.position - arm.upperArm.position, arm.hand.position - arm.lowerArm.position) : 0f;
        public float Reach => arm.lowerArm != null
            ? Vector3.Distance(arm.upperArm.position, arm.hand.position) / (Vector3.Distance(arm.upperArm.position, arm.lowerArm.position) + Vector3.Distance(arm.lowerArm.position, arm.hand.position))
            : 1f;
        /// <summary>The arm she raises now: true for the left one (the tests).</summary>
        public bool LeftArm => arm == left;

        private PsychicPowers powers;
        private LockOnController lockOn;
        private Health health;
        private Animator animator;
        private HumanPoseHandler handler;
        private HumanPose pose;
        private Transform hips, head;
        private readonly Side right = new Side { outward = 1f }, left = new Side { outward = -1f };
        private Side arm;
        private int[] uprightIndex;
        private float[] basePose;

        private Side Other => arm == right ? left : right;

        private void Awake()
        {
            powers = GetComponent<PsychicPowers>();
            lockOn = GetComponent<LockOnController>();
            health = GetComponentInChildren<Health>();
            foreach ((Side side, string name) in new[] { (right, "Right "), (left, "Left ") })
            {
                Resolve(AimFixed, name, out side.aimIndex, out side.aimValue);
                Resolve(RestFixed, name, out side.restIndex, out side.restValue);
                side.downUp = System.Array.IndexOf(HumanTrait.MuscleName, name + "Arm Down-Up");
                side.frontBack = System.Array.IndexOf(HumanTrait.MuscleName, name + "Arm Front-Back");
            }
            var upright = new System.Collections.Generic.List<int>();
            foreach (string m in Upright)
            {
                int i = System.Array.IndexOf(HumanTrait.MuscleName, m);
                if (i >= 0) upright.Add(i);
            }
            uprightIndex = upright.ToArray();
            arm = right;
        }

        // The muscles of one side by name; a name the humanoid does not have (a finger of a rig with none) is skipped.
        private static void Resolve((string muscle, float value)[] set, string side, out int[] indices, out float[] values)
        {
            var i = new System.Collections.Generic.List<int>();
            var v = new System.Collections.Generic.List<float>();
            foreach ((string muscle, float value) in set)
            {
                int index = System.Array.IndexOf(HumanTrait.MuscleName, side + muscle);
                if (index < 0) continue;
                i.Add(index);
                v.Add(value);
            }
            indices = i.ToArray();
            values = v.ToArray();
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
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            right.upperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            right.hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            right.lowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            left.lowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
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
            Side wanted = side < -SwitchYaw ? left : side > SwitchYaw ? right : arm;
            if (wanted != arm) { arm = wanted; Weight = Mathf.Min(Weight, 0.3f); } // a short ease into the other arm

            Vector3 at = hips != null ? hips.position : Vector3.zero;
            Quaternion turn = hips != null ? hips.rotation : Quaternion.identity;
            handler.GetHumanPose(ref pose);
            if (basePose == null || basePose.Length != pose.muscles.Length) basePose = new float[pose.muscles.Length];
            System.Array.Copy(pose.muscles, basePose, pose.muscles.Length);

            // The aim inside the arm's range, then one step of the solver for each arm.
            Vector2 raw = Angles(AimPoint - arm.upperArm.position);
            float yawOut = raw.x * arm.outward;
            AimAngles = new Vector2(Mathf.Clamp(yawOut, YawAcross, YawOut) * arm.outward, Mathf.Clamp(raw.y, PitchDown, PitchUp));
            Solve(arm, true, AimAngles);
            Side rest = Other;
            Solve(rest, false, new Vector2(12f * rest.outward, -78f)); // down at her side, a little out and forward

            // The pose of this frame, by the weight over the animation.
            Compose(Weight);
            if (hips != null) hips.SetPositionAndRotation(at, turn);
            BendHandUp();
            AimError = Vector3.Angle(arm.hand.position - arm.upperArm.position, Direction(AimAngles));
        }

        // One Gauss-Newton step on the two arm muscles of a side: the arm's direction (shoulder to hand) toward the wanted
        // yaw and pitch. The probes measure how each muscle moves it on this rig.
        private void Solve(Side s, bool aiming, Vector2 want)
        {
            ref float du = ref (aiming ? ref s.aimDu : ref s.restDu);
            ref float fb = ref (aiming ? ref s.aimFb : ref s.restFb);
            Vector2 now = Measure(s, du, fb);
            Vector2 dDu = (Measure(s, du + Probe, fb) - now) / Probe;
            Vector2 dFb = (Measure(s, du, fb + Probe) - now) / Probe;
            Vector2 error = new Vector2(Mathf.DeltaAngle(now.x, want.x), want.y - now.y);
            float det = dDu.x * dFb.y - dFb.x * dDu.y;
            if (Mathf.Abs(det) <= 1e-3f) return;
            float stepDu = (error.x * dFb.y - dFb.x * error.y) / det;
            float stepFb = (dDu.x * error.y - error.x * dDu.y) / det;
            du = Mathf.Clamp(du + Mathf.Clamp(stepDu, -0.2f, 0.2f) * 0.7f, -1f, 1f);
            fb = Mathf.Clamp(fb + Mathf.Clamp(stepFb, -0.2f, 0.2f) * 0.7f, -1f, 1f);
        }

        // The arm of one side at two muscle values, the rest of the stance at full weight: its direction as yaw and pitch.
        private Vector2 Measure(Side s, float du, float fb)
        {
            Vector3 at = hips != null ? hips.position : Vector3.zero;
            Quaternion turn = hips != null ? hips.rotation : Quaternion.identity;
            float keepDu = s == arm ? s.aimDu : s.restDu, keepFb = s == arm ? s.aimFb : s.restFb;
            if (s == arm) { s.aimDu = du; s.aimFb = fb; } else { s.restDu = du; s.restFb = fb; }
            Compose(1f);
            if (s == arm) { s.aimDu = keepDu; s.aimFb = keepFb; } else { s.restDu = keepDu; s.restFb = keepFb; }
            if (hips != null) hips.SetPositionAndRotation(at, turn);
            return Angles(s.hand.position - s.upperArm.position);
        }

        // The stance over the animation by a weight: the torso and head square, the aim arm, the other arm at rest.
        private void Compose(float weight)
        {
            for (int i = 0; i < pose.muscles.Length; i++) pose.muscles[i] = basePose[i];
            foreach (int i in uprightIndex) pose.muscles[i] = Mathf.Lerp(basePose[i], 0f, weight);
            Side rest = Other;
            Set(arm.aimIndex, arm.aimValue, weight);
            Set(rest.restIndex, rest.restValue, weight);
            pose.muscles[arm.downUp] = Mathf.Lerp(basePose[arm.downUp], arm.aimDu, weight);
            pose.muscles[arm.frontBack] = Mathf.Lerp(basePose[arm.frontBack], arm.aimFb, weight);
            pose.muscles[rest.downUp] = Mathf.Lerp(basePose[rest.downUp], rest.restDu, weight);
            pose.muscles[rest.frontBack] = Mathf.Lerp(basePose[rest.frontBack], rest.restFb, weight);
            handler.SetHumanPose(ref pose);
        }

        /// <summary>Where the fingers of the aim hand point now (the tests): up, for the stop of a hand.</summary>
        public Vector3 Fingers { get; private set; }

        // The stop of a hand: the fingers from the line of the arm to straight up (the part of world up square to the arm),
        // the palm to the target. On the bone, after the pose, by the weight.
        private void BendHandUp()
        {
            if (arm.lowerArm == null) return;
            Vector3 along = (arm.hand.position - arm.lowerArm.position).normalized;
            Vector3 fingersLocal = Quaternion.Inverse(arm.hand.rotation) * along; // the wrist is straight: the fingers go on the arm's line
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, along);
            if (up.sqrMagnitude < 1e-4f) return; // the arm straight up: nothing to bend to
            Quaternion bend = Quaternion.FromToRotation(along, up.normalized);
            arm.hand.rotation = Quaternion.Slerp(arm.hand.rotation, bend * arm.hand.rotation, Weight);
            Fingers = arm.hand.rotation * fingersLocal;
        }

        private void Set(int[] indices, float[] values, float weight)
        {
            for (int k = 0; k < indices.Length; k++) pose.muscles[indices[k]] = Mathf.Lerp(basePose[indices[k]], values[k], weight);
        }

        private Vector3 Target()
        {
            if (lockOn != null && lockOn.HeldThing != null) return lockOn.HeldThing.position; // mk_orzabal: the arm follows the floater
            if (lockOn != null && lockOn.CurrentTarget != null) return lockOn.CurrentTarget.GetLockPoint();
            return transform.position + Vector3.up * Body.ChestHeight + transform.forward * 3f;
        }

        private Vector2 Angles(Vector3 world)
        {
            Vector3 local = transform.InverseTransformDirection(world.normalized);
            return new Vector2(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg);
        }

        private Vector3 Direction(Vector2 yawPitch)
        {
            float yaw = yawPitch.x * Mathf.Deg2Rad, pitch = yawPitch.y * Mathf.Deg2Rad;
            return transform.TransformDirection(new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch)));
        }
    }
}
