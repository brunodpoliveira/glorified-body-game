using UnityEngine;

namespace GlorifiedBody.Characters
{
    /// <summary>
    /// The back flat on a wall, to look smaller with no cover (Bruno, 2026-10-25: "an anim where they lean against a
    /// wall (like they're trying to make themselves smaller profile but have no cover available)"; study A, option 1:
    /// a pose by code, "let's test then refine"). Over the clip that plays (a self-hug or a shaking stance), after the
    /// Animator: the skeleton moves back from the NavMesh edge to the wall (the agent cannot reach it), the spine and
    /// the chest bend back onto the wall plane, and the head turns away from the threat. It eases in and out. The
    /// numbers are PROPOSED.
    /// </summary>
    public sealed class WallLean : MonoBehaviour
    {
        public const float MaxWallGap = 1.0f;     // m from the root: a wall farther than this is no wall to lean on
        private const float BackDepth = 0.17f;    // m from the wall plane to the hips and the chest bones
        private const float Slide = 0.08f;        // m the hips slide down
        private const float MaxBend = 28f;        // degrees of the spine back onto the wall
        private const float HeadTurn = 35f;       // degrees away from the threat
        private const float EasePerSecond = 2.5f;

        private Animator animator;
        private Transform hips, spine, chest, head;
        private Vector3 wallPoint, wallNormal;
        private Transform threat;
        private float weight;
        private bool on;

        public bool Leaning => on;
        public float Weight => weight;
        public Vector3 WallNormal => wallNormal;
        /// <summary>The distance of the hips bone from the wall plane after the last pose, in m (the tests).</summary>
        public float LastHipsGap { get; private set; } = float.MaxValue;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
                head = animator.GetBoneTransform(HumanBodyBones.Head);
            }
        }

        /// <summary>
        /// The nearest upright wall around the point: 16 level rays at the hips' height on the Default layer, within
        /// MaxWallGap. The normal points out of the wall, into the room. (The nearest NavMesh edge failed: on the
        /// edge itself its normal pointed at another wall, 2026-10-25.)
        /// </summary>
        public static bool TryFindWall(Vector3 at, out Vector3 point, out Vector3 normal) => TryFindWall(at, out point, out normal, out _);

        /// <summary>The same, with the reason of a failure (the telemetry).</summary>
        public static bool TryFindWall(Vector3 at, out Vector3 point, out Vector3 normal, out string why)
        {
            point = at;
            normal = Vector3.forward;
            why = "no wall within " + MaxWallGap.ToString("0.0") + " m";
            Vector3 from = at + Vector3.up * 0.9f;
            int walls = LayerMask.GetMask("Default");
            float best = float.MaxValue;
            for (int i = 0; i < 16; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                if (!Physics.Raycast(from, direction, out RaycastHit hit, MaxWallGap, walls, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(hit.normal.y) > 0.3f) continue; // a desk top or a slope, no wall
                Vector3 flatNormal = hit.normal;
                flatNormal.y = 0f;
                flatNormal.Normalize();
                // The distance to the wall plane, not along the ray: a slanted ray reads a far point of the same wall.
                float gap = Vector3.Dot(from - hit.point, flatNormal);
                if (gap <= 0f || gap >= best) continue;
                best = gap;
                point = hit.point;
                normal = flatNormal;
                why = $"{hit.collider.name} gap {gap:0.00} m";
            }
            return best <= MaxWallGap;
        }

        /// <summary>Leans on the wall (its point and its normal into the room). The head turns away from the threat.</summary>
        public void Begin(Vector3 point, Vector3 normal, Transform awayFrom)
        {
            wallPoint = point;
            wallNormal = normal;
            threat = awayFrom;
            on = true;
        }

        public void End() => on = false;

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || hips == null) return;
            weight = Mathf.MoveTowards(weight, on ? 1f : 0f, dt * EasePerSecond);
            if (weight <= 0f) { LastHipsGap = float.MaxValue; return; }

            // The whole skeleton back to the wall, and a little down.
            float gap = Vector3.Dot(hips.position - wallPoint, wallNormal);
            hips.position += (-wallNormal * Mathf.Max(0f, gap - BackDepth) - Vector3.up * Slide) * weight;
            LastHipsGap = Vector3.Dot(hips.position - wallPoint, wallNormal);

            // The spine back until the chest touches the wall plane.
            if (spine != null && chest != null)
            {
                float chestGap = Vector3.Dot(chest.position - wallPoint, wallNormal) - BackDepth;
                float lever = Mathf.Max(0.2f, Vector3.Distance(spine.position, chest.position));
                float bend = Mathf.Clamp(Mathf.Atan2(chestGap, lever) * Mathf.Rad2Deg, -MaxBend, MaxBend) * weight;
                Vector3 axis = Vector3.Cross(Vector3.up, -wallNormal);
                spine.rotation = Quaternion.AngleAxis(bend, axis) * spine.rotation;
            }

            // The face away from her, along the wall.
            if (head != null && threat != null)
            {
                Vector3 toThreat = threat.position - transform.position;
                float side = Vector3.Dot(toThreat, Vector3.Cross(Vector3.up, wallNormal)) >= 0f ? -1f : 1f;
                head.rotation = Quaternion.AngleAxis(side * HeadTurn * weight, Vector3.up) * head.rotation;
            }
        }
    }
}
