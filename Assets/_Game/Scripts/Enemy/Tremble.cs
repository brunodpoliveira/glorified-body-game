using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;

namespace GlorifiedBody.Enemy
{
    /// <summary>
    /// A fear tremble over the animation (Bruno, 2026-10-25: "not sure if we can add a tremble to their animation").
    /// No clip: small fast noise turns on the spine, the neck, the head, the arms and the hands, after the Animator
    /// pose, every frame. The brain sets the amount (0 = still, 1 = full). It eases in and out. Off on a dead, limp
    /// or lifted body. Numbers PROPOSED.
    /// </summary>
    [DefaultExecutionOrder(20)] // after the pose scripts (WallLean, PleaHands): it shakes the pose they make
    public sealed class Tremble : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float frequency = 9f;   // noise steps per second
        [SerializeField, Min(0f)] private float maxDegrees = 4f;    // at amount 1, on a bone of weight 1
        [SerializeField, Min(0.1f)] private float easePerSecond = 2f;

        private static readonly (HumanBodyBones bone, float weight)[] Bones =
        {
            (HumanBodyBones.Spine, 0.4f), (HumanBodyBones.Chest, 0.5f), (HumanBodyBones.Neck, 0.6f), (HumanBodyBones.Head, 0.8f),
            (HumanBodyBones.LeftUpperArm, 0.7f), (HumanBodyBones.RightUpperArm, 0.7f),
            (HumanBodyBones.LeftLowerArm, 0.6f), (HumanBodyBones.RightLowerArm, 0.6f),
            (HumanBodyBones.LeftHand, 1.2f), (HumanBodyBones.RightHand, 1.2f),
        };

        private Transform[] bones;
        private float[] weights;
        private float seed;
        private Health health;
        private float current;

        /// <summary>The amount the brain asks for, 0 to 1.</summary>
        public float Amount { get; set; }
        /// <summary>The largest turn of the last frame, in degrees (0 when still). For the tests.</summary>
        public float LastDegrees { get; private set; }
        /// <summary>A jolt runs now (a shock of the short).</summary>
        public bool Jolting => Time.time < joltUntil;

        private const float JoltAmount = 3f;     // 3 x maxDegrees: the body seizes
        private const float JoltFrequency = 2.5f; // x frequency
        private float joltUntil;

        /// <summary>The body seizes for the seconds: a shock of the short stuns it (Bruno, 2026-10-25). No ease-in.</summary>
        public void Jolt(float seconds) => joltUntil = Mathf.Max(joltUntil, Time.time + seconds);

        private void Awake()
        {
            health = GetComponent<Health>();
            seed = Random.value * 100f;
            Animator animator = GetComponent<Animator>();
            var found = new System.Collections.Generic.List<Transform>();
            var w = new System.Collections.Generic.List<float>();
            if (animator != null && animator.isHuman)
            {
                foreach ((HumanBodyBones bone, float weight) in Bones)
                {
                    Transform t = animator.GetBoneTransform(bone);
                    if (t == null) continue;
                    found.Add(t);
                    w.Add(weight);
                }
            }
            bones = found.ToArray();
            weights = w.ToArray();
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            current = Mathf.MoveTowards(current, Mathf.Clamp01(Amount), dt * easePerSecond);
            LastDegrees = 0f;
            bool jolt = Jolting;
            float amount = jolt ? JoltAmount : current;
            if (amount <= 0.001f || bones.Length == 0) return;
            if ((health != null && health.IsDead) || RagdollBodies.IsDown(this)) return;
            TelekinesisLift lift = GetComponent<TelekinesisLift>();
            if (lift != null && lift.IsLifted) return;

            float t = Time.time * frequency * (jolt ? JoltFrequency : 1f);
            for (int i = 0; i < bones.Length; i++)
            {
                float degrees = maxDegrees * weights[i] * amount;
                float x = (Mathf.PerlinNoise(seed + i * 7.1f, t) - 0.5f) * 2f * degrees;
                float y = (Mathf.PerlinNoise(seed + i * 3.3f + 50f, t) - 0.5f) * 2f * degrees;
                float z = (Mathf.PerlinNoise(seed + i * 5.7f + 90f, t) - 0.5f) * 2f * degrees;
                bones[i].localRotation *= Quaternion.Euler(x, y, z);
                LastDegrees = Mathf.Max(LastDegrees, Mathf.Abs(x), Mathf.Abs(y), Mathf.Abs(z));
            }
        }
    }
}
