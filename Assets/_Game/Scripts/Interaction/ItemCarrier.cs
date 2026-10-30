using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;

namespace GlorifiedBody.Interaction
{
    /// <summary>
    /// A figure that carries an item (2026-10-27, the design sweep's rank 6: the ward sister carries the stairwell
    /// card). The item is an ItemPickup of the room, hidden (inactive) until the carrier dies. Then, when the body
    /// has fallen, it lies on the floor beside the body, on the side she is on, and the Interact key takes it.
    /// The room keeps the pickup at its own path, so the saves treat it as any other item: taken stays taken. A load
    /// kills the carrier again quietly (RoomGore), so the item comes back beside the body when she did not take it.
    /// EnemySpawn adds this to the figure when its marker names a drop. Numbers PROPOSED.
    /// The item shows on the carrier while it lives (Bruno, 2026-10-29: a carrier "should" show what it carries): a
    /// smaller copy of the pickup, unlit like every pickup, hangs on the belt at the right hip. It goes when the item
    /// drops.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class ItemCarrier : MonoBehaviour
    {
        public const float FallSeconds = 1.2f; // the body falls first (EnemyRagdoll), then the item shows
        public const float BesideBody = 0.5f;     // metres from the hips, her side first
        public const float FarBesideBody = 0.85f; // the second ring when no limb leaves room at the first
        public const float ClearRadius = 0.2f;    // no limb this close to the item
        public const float BeltScale = 0.7f;      // the copy on the belt, a share of the pickup's size
        public const string CarriedName = "CarriedItem";

        [SerializeField] private ItemPickup drop;

        private Health health;
        private float height = 0.1f; // the pickup's height over the floor, as the room built it
        private bool dropped;
        private GameObject carried;

        public ItemPickup Drop => drop;
        public bool Dropped => dropped;
        /// <summary>The copy of the item on the belt, or null.</summary>
        public GameObject Carried => carried;
        public bool ShowsItem => carried != null && carried.activeSelf;

        public void Bind(ItemPickup pickup)
        {
            drop = pickup;
            if (drop != null) height = Mathf.Max(0.02f, drop.transform.position.y);
            if (drop != null && carried == null && health != null && !health.IsDead) HangOnBelt();
        }

        private void Start()
        {
            if (drop != null && carried == null && !health.IsDead) HangOnBelt();
        }

        /// <summary>The smaller copy of the pickup on the belt: at the right hip, a little forward, flat against the body.</summary>
        private void HangOnBelt()
        {
            Animator animator = GetComponentInChildren<Animator>();
            Transform hips = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            MeshFilter mesh = drop.GetComponent<MeshFilter>();
            Renderer look = drop.GetComponent<Renderer>();
            if (hips == null || mesh == null || look == null) return;
            carried = new GameObject(CarriedName);
            carried.layer = gameObject.layer;
            carried.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
            MeshRenderer shown = carried.AddComponent<MeshRenderer>();
            shown.sharedMaterial = look.sharedMaterial;
            shown.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Vector3 outward = (transform.right * 0.8f + transform.forward * 0.6f).normalized;
            carried.transform.SetParent(hips, false);
            carried.transform.position = hips.position + transform.right * 0.16f + transform.forward * 0.08f + Vector3.up * 0.08f; // at the belt line
            // The card's thin side faces out from the hip, its long edge level.
            carried.transform.rotation = Quaternion.LookRotation(Vector3.up, outward); // the pickup is a flat box: x long, y thin, z wide
            Vector3 world = drop.transform.lossyScale * BeltScale, parent = hips.lossyScale;
            carried.transform.localScale = new Vector3(world.x / parent.x, world.y / parent.y, world.z / parent.z);
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            if (drop != null) height = Mathf.Max(0.02f, drop.transform.position.y);
        }

        private void OnEnable()
        {
            health.OnDeath ??= new UnityEngine.Events.UnityEvent();
            health.OnDeath.AddListener(OnDeath);
        }

        private void OnDisable() => health.OnDeath?.RemoveListener(OnDeath);

        private void OnDeath()
        {
            if (dropped || drop == null)
            {
                if (carried != null) carried.SetActive(false);
                return;
            }
            StartCoroutine(DropAfterFall());
        }

        private IEnumerator DropAfterFall()
        {
            yield return new WaitForSeconds(FallSeconds);
            DropNow();
        }

        /// <summary>The item shows on the floor beside the body now (a dead one with no body: where it stood).</summary>
        public void DropNow()
        {
            if (carried != null) carried.SetActive(false); // off the belt
            if (dropped || drop == null) return; // taken already, or removed by a load
            if (drop.Taken) return;
            dropped = true;
            Vector3 at = FreeSpot(BodyPoint());
            float floor = at.y;
            if (Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out RaycastHit ground, 3f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore))
                floor = ground.point.y;
            drop.transform.position = new Vector3(at.x, floor + height, at.z);
            drop.gameObject.SetActive(true);
            GlorifiedBody.Audio.Sfx.Play(GlorifiedBody.Audio.Sfx.Pickup, drop.transform.position, 0.6f);
            RunTelemetry.Event("item_dropped", $"{name}:{(drop.item != null ? drop.item.itemId : "?")}");
        }

        /// <summary>
        /// A floor point beside the body that no limb covers and she can walk to: around the hips, at BesideBody and
        /// then FarBesideBody, her side first, then turned away from it (the first record put the card under his legs).
        /// </summary>
        private Vector3 FreeSpot(Vector3 hips)
        {
            Vector3 toHer = Vector3.forward;
            GameObject lucy = GameObject.FindWithTag("Player");
            if (lucy != null)
            {
                Vector3 d = lucy.transform.position - hips;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f) toHer = d.normalized;
            }
            var limbs = Limbs();
            foreach (float distance in new[] { BesideBody, FarBesideBody })
            {
                foreach (float turn in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f })
                {
                    Vector3 point = hips + Quaternion.Euler(0f, turn, 0f) * toHer * distance;
                    if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) continue;
                    if (Covered(hit.position, limbs)) continue;
                    return hit.position;
                }
            }
            // No free point: the nearest NavMesh point on her side.
            Vector3 fallback = hips + toHer * BesideBody;
            return NavMesh.SamplePosition(fallback, out NavMeshHit near, 1.5f, NavMesh.AllAreas) ? near.position : fallback;
        }

        private static readonly (HumanBodyBones from, HumanBodyBones to)[] Segments =
        {
            (HumanBodyBones.Hips, HumanBodyBones.Chest), (HumanBodyBones.Chest, HumanBodyBones.Head),
            (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg), (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
            (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg), (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
            (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm), (HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
            (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm), (HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
        };

        /// <summary>The body's limbs as bone segments (the bones, not the colliders: a loaded corpse has its colliders off).</summary>
        private System.Collections.Generic.List<(Vector3 a, Vector3 b)> Limbs()
        {
            var list = new System.Collections.Generic.List<(Vector3 a, Vector3 b)>();
            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return list;
            foreach ((HumanBodyBones from, HumanBodyBones to) in Segments)
            {
                Transform a = animator.GetBoneTransform(from), b = animator.GetBoneTransform(to);
                if (a != null && b != null) list.Add((a.position, b.position));
            }
            return list;
        }

        /// <summary>A limb lies within ClearRadius of the point, seen from above.</summary>
        private static bool Covered(Vector3 point, System.Collections.Generic.List<(Vector3 a, Vector3 b)> limbs)
        {
            Vector2 p = new Vector2(point.x, point.z);
            foreach ((Vector3 a3, Vector3 b3) in limbs)
            {
                Vector2 a = new Vector2(a3.x, a3.z), b = new Vector2(b3.x, b3.z);
                Vector2 ab = b - a;
                float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                if (Vector2.Distance(p, a + ab * t) < ClearRadius) return true;
            }
            return false;
        }

        /// <summary>The hips of the body (where it lies), else the root.</summary>
        private Vector3 BodyPoint()
        {
            Animator animator = GetComponentInChildren<Animator>();
            Transform hips = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            return hips != null ? hips.position : transform.position;
        }
    }
}
