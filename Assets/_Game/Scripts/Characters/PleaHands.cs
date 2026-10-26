using UnityEngine;

namespace GlorifiedBody.Characters
{
    /// <summary>
    /// Both hands up in a plea, the palms to the threat (Bruno, 2026-10-25: "hands raised in a plea could be one of
    /// the poses"). Over the clip that plays (the floor sit or the kneel), after the Animator and the wall lean: the
    /// arm muscles of the humanoid pose go to the plea values (HumanPoseHandler), so one set of numbers fits every
    /// rig. It eases in and out. The numbers are PROPOSED (picked from the probe renders).
    /// </summary>
    [DefaultExecutionOrder(10)] // after WallLean; Tremble runs after it and shakes the raised hands
    public sealed class PleaHands : MonoBehaviour
    {
        /// <summary>
        /// The plea: Unity muscle names and values (-1..1), the arm ones the same for both sides. Picked from the
        /// PoseProbe renders over the floor sit (2026-10-25, set "E"): the hands apart at the face, 0.4 m in front and
        /// 0.14 m over the shoulders, the palms out, the head up to her. The first pick ("B+head", the hands at the
        /// chest) sank to the knees in the game: the sit and the lean hunch the back. On this rig a negative Arm
        /// Front-Back brings the arm forward.
        /// </summary>
        public static readonly (string muscle, float value)[] Plea =
        {
            ("Shoulder Down-Up", 0.3f),
            ("Arm Down-Up", 0.6f),
            ("Arm Front-Back", -0.1f),
            ("Arm Twist In-Out", 0.6f),
            ("Forearm Stretch", 0.3f),
            ("Forearm Twist In-Out", -0.8f),
            ("Hand Down-Up", 0.6f),
            ("Hand In-Out", 0f),
            ("Head Nod Down-Up", 0.8f),
            ("Neck Nod Down-Up", 0.8f),
        };

        private const float EasePerSecond = 3f;

        private Animator animator;
        private Transform hips;
        private HumanPoseHandler handler;
        private HumanPose pose;
        private int[] indices;
        private float[] values;

        /// <summary>The brain sets it: true while it begs with this pose.</summary>
        public bool On { get; set; }
        public float Weight { get; private set; }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator != null && animator.isHuman && animator.avatar != null)
            {
                handler = new HumanPoseHandler(animator.avatar, animator.transform);
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            }
            Resolve(Plea, out indices, out values);
        }

        private void OnDestroy() => handler?.Dispose();

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || handler == null) return;
            Weight = Mathf.MoveTowards(Weight, On ? 1f : 0f, dt * EasePerSecond);
            if (Weight <= 0.001f || !animator.enabled) return;
            Apply(handler, ref pose, indices, values, Weight, hips);
        }

        /// <summary>Muscle names to indices: a whole name as it is, else both sides ("Left ", "Right " before it).</summary>
        public static void Resolve((string muscle, float value)[] set, out int[] indices, out float[] values)
        {
            var i = new System.Collections.Generic.List<int>();
            var v = new System.Collections.Generic.List<float>();
            foreach ((string muscle, float value) in set)
            {
                int whole = System.Array.IndexOf(HumanTrait.MuscleName, muscle); // a muscle with no side (the head, the neck)
                if (whole >= 0)
                {
                    i.Add(whole);
                    v.Add(value);
                    continue;
                }
                foreach (string side in new[] { "Left ", "Right " })
                {
                    int index = System.Array.IndexOf(HumanTrait.MuscleName, side + muscle);
                    if (index < 0) continue;
                    i.Add(index);
                    v.Add(value);
                }
            }
            indices = i.ToArray();
            values = v.ToArray();
        }

        /// <summary>The pose of now, the arm muscles moved to the values by the weight (the probe uses it too).</summary>
        public static void Apply(HumanPoseHandler handler, ref HumanPose pose, int[] indices, float[] values, float weight, Transform hips)
        {
            // SetHumanPose also places the body, and not where it was: a figure away from the world's origin lost its
            // body 16 m off (found 2026-10-25). The hips keep their place; only the muscles change.
            Vector3 at = hips != null ? hips.position : Vector3.zero;
            Quaternion turn = hips != null ? hips.rotation : Quaternion.identity;
            handler.GetHumanPose(ref pose);
            for (int k = 0; k < indices.Length; k++)
                pose.muscles[indices[k]] = Mathf.Lerp(pose.muscles[indices[k]], values[k], weight);
            handler.SetHumanPose(ref pose);
            if (hips != null) hips.SetPositionAndRotation(at, turn);
        }
    }
}
