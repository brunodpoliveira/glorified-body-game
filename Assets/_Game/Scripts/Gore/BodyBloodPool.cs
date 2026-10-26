using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GlorifiedBody.Combat;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Gore.ThatIndividual;

namespace GlorifiedBody.Gore
{
    /// <summary>
    /// Blood pools under a dead body (Bruno, 2026-10-24: "blood should pool on a dead enemy; extra feedback").
    /// After a fatal hit on a figure with an animator, it waits until the hips lie still, then lays a flat stain
    /// (the profile's bodyPoolMaterials, our unlit stain shader) on the floor under the hips and grows it slowly.
    /// The pool stays for the room, with no limit on the number (Bruno, 2026-10-24: "they stay for the whole room,
    /// no limits"). A dragged body leaves its pool behind. Gore Low: a smaller pool. Off: none. Sizes PROPOSED.
    /// </summary>
    public sealed class BodyBloodPool : MonoBehaviour
    {
        private const float StartSize = 0.3f;
        private const float GrowSeconds = 5f;
        private const float StillSpeed = 0.05f;   // m/s at the hips
        private const float MaxWaitSeconds = 8f;
        private const int FloorMask = 1;          // Default: the room

        private static readonly List<GameObject> pools = new List<GameObject>();
        private static readonly HashSet<GameObject> bodies = new HashSet<GameObject>();

        public static int Count { get { pools.RemoveAll(p => p == null); return pools.Count; } }
        public static IReadOnlyList<GameObject> Pools => pools;

        private void OnEnable() => HitEventBus.OnHit += OnHit;
        private void OnDisable() => HitEventBus.OnHit -= OnHit;

        private void OnHit(HitInfo hit)
        {
            if (hit == null || !hit.WasFatal || hit.Victim == null || hit.Victim.CompareTag("Player")) return;
            if (GoreManager.Instance == null || GoreManager.Instance.CurrentTier == GoreTier.Off) return;
            if (!bodies.Add(hit.Victim)) return; // one pool for one body
            Animator animator = hit.Victim.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;
            StartCoroutine(Pool(hit.Victim, animator.GetBoneTransform(HumanBodyBones.Hips)));
        }

        private IEnumerator Pool(GameObject body, Transform hips)
        {
            if (hips == null) yield break;
            // The death clip or the ragdoll: wait until the hips lie still.
            Vector3 last = hips.position;
            float still = 0f, waited = 0f;
            yield return new WaitForSeconds(0.5f);
            while (body != null && still < 0.5f && waited < MaxWaitSeconds)
            {
                yield return null;
                if (hips == null) yield break;
                float speed = (hips.position - last).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
                last = hips.position;
                still = speed < StillSpeed ? still + Time.deltaTime : 0f;
                waited += Time.deltaTime;
            }
            if (body == null || hips == null) yield break;
            if (!Physics.Raycast(hips.position + Vector3.up * 0.3f, Vector3.down, out RaycastHit floor, 3f, FloorMask, QueryTriggerInteraction.Ignore)) yield break;

            ThatIndividualGoreProfile profile = ThatIndividualGoreDriver.Instance != null ? ThatIndividualGoreDriver.Instance.Profile : null;
            Material[] materials = profile != null ? profile.bodyPoolMaterials : null;
            if (materials == null || materials.Length == 0) yield break;
            Material material = materials[Random.Range(0, materials.Length)];
            if (material == null) yield break;

            var pool = GameObject.CreatePrimitive(PrimitiveType.Quad);
            pool.name = "BodyBloodPool";
            Destroy(pool.GetComponent<Collider>());
            Renderer renderer = pool.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // A small lift per pool: two pools that overlap do not fight in depth.
            pool.transform.SetPositionAndRotation(floor.point + floor.normal * (0.012f + 0.002f * (pools.Count % 4)),
                Quaternion.LookRotation(-floor.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            pools.Add(pool);
            float full = (GoreManager.Instance != null && GoreManager.Instance.CurrentTier == GoreTier.Low ? 0.8f : 1.25f) * Random.Range(0.85f, 1.15f);
            RunTelemetry.Event("gore_body_pool", $"{body.name}:{full:0.0}m");
            float t = 0f;
            while (t < GrowSeconds && pool != null)
            {
                t += Time.deltaTime;
                float size = Mathf.Lerp(StartSize, full, Mathf.SmoothStep(0f, 1f, t / GrowSeconds));
                pool.transform.localScale = new Vector3(size, size * 0.85f, 1f);
                yield return null;
            }
        }

        /// <summary>A pool that a load puts back, at its full size. The body has its pool: no hit grows another.</summary>
        public static GameObject Restore(Material material, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (material == null) return null;
            GameObject pool = FloorStains.Make(material, position, rotation, scale, "BodyBloodPool");
            pools.Add(pool);
            return pool;
        }

        /// <summary>A body that a load puts back already has its pool.</summary>
        public static void MarkPooled(GameObject body)
        {
            if (body != null) bodies.Add(body);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            pools.Clear();
            bodies.Clear();
        }
    }
}
