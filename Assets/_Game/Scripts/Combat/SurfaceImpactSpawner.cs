using UnityEngine;

namespace GlorifiedBody.Combat
{
    /// <summary>
    /// Spawns environment impact FX (bullet-hit puffs, sparks, wall decals) on NON-actor hits.
    /// DamageRouter already handles actor hits (blood/hit feedback); this fills the gap for shots
    /// that strike walls, columns, and props. Assign your own or Invector's shipped prefabs
    /// (e.g. Explosion_Air, dustSplash, smoke, vDecal_ConcreteA) — this component holds plain
    /// GameObject references, so it never takes an Invector code dependency.
    ///
    /// Opt-in: add it to a scene manager object. It self-subscribes to HitEventBus.
    /// </summary>
    public sealed class SurfaceImpactSpawner : MonoBehaviour
    {
        [Header("Effect Prefabs (assign Invector or custom)")]
        [Tooltip("Impact/spark burst at the hit point (e.g. Invector impactMetal).")]
        [SerializeField] private GameObject impactVfxPrefab;
        [Tooltip("Smoke puff at the hit point (e.g. Invector smoke.prefab).")]
        [SerializeField] private GameObject smokePrefab;
        [Tooltip("Optional decal spawned flat against the surface. Leave empty: ImpactFeedbackController draws the bullet chips.")]
        [SerializeField] private GameObject decalPrefab;

        [Header("Tuning")]
        [Tooltip("Uniformly scales the spawned spark + smoke (crank up for an over-the-top look).")]
        [SerializeField] private float vfxScale = 1f;
        [Tooltip("Scales the decal size independently.")]
        [SerializeField] private float decalScale = 1f;
        [SerializeField] private float vfxLifetime = 3f;
        [SerializeField] private float smokeLifetime = 4f;
        [Tooltip("Seconds a decal stays. Capped at 8 s; 0 or less also means 8 s (decals are never permanent).")]
        [SerializeField] private float decalLifetime = 12f;
        [Tooltip("Skip repeated impacts closer together than this, to avoid VFX spam.")]
        [SerializeField] private float minSpacing = 0.15f;
        [SerializeField] private float decalSurfaceOffset = 0.01f;

        // Same rules as the bullet chips: at most 64 decals, the oldest goes first, gone after about 8 s.
        private const int MaxDecals = 64;
        private const float MaxDecalLifetime = 8f;
        private readonly System.Collections.Generic.Queue<GameObject> liveDecals = new();

        private Vector3 lastPoint = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

        private void OnEnable()
        {
            HitEventBus.OnHit += HandleHit;
        }

        private void OnDisable()
        {
            HitEventBus.OnHit -= HandleHit;
        }

        private void HandleHit(HitInfo hit)
        {
            if (hit == null)
            {
                return;
            }

            // Actor hits (enemies/player) are DamageRouter's job — we only decorate the environment.
            if (HitClassifier.IsActorHit(hit))
            {
                return;
            }

            if ((hit.HitPoint - lastPoint).sqrMagnitude < minSpacing * minSpacing)
            {
                return;
            }
            lastPoint = hit.HitPoint;

            Vector3 normal = hit.HitNormal.sqrMagnitude > 0.0001f ? hit.HitNormal.normalized : Vector3.up;
            Quaternion facing = Quaternion.LookRotation(normal);

            if (impactVfxPrefab != null)
            {
                GameObject vfx = Instantiate(impactVfxPrefab, hit.HitPoint, facing);
                ApplyScale(vfx, vfxScale);
                if (vfxLifetime > 0f) Destroy(vfx, vfxLifetime);
            }

            if (smokePrefab != null)
            {
                GameObject smoke = Instantiate(smokePrefab, hit.HitPoint + normal * 0.05f, facing);
                ApplyScale(smoke, vfxScale);
                if (smokeLifetime > 0f) Destroy(smoke, smokeLifetime);
            }

            if (decalPrefab != null)
            {
                // A Unity Quad's visible face normal is local -Z, so aim -Z down the surface
                // normal (LookRotation(-normal)) or the decal shows its back and looks inverted.
                Quaternion decalFacing = Quaternion.LookRotation(-normal, Vector3.up);
                GameObject decal = Instantiate(decalPrefab, hit.HitPoint + normal * decalSurfaceOffset, decalFacing);
                ApplyScale(decal, decalScale);
                if (hit.HitTransform != null) decal.transform.SetParent(hit.HitTransform, worldPositionStays: true);
                Destroy(decal, decalLifetime > 0f ? Mathf.Min(decalLifetime, MaxDecalLifetime) : MaxDecalLifetime);
                liveDecals.Enqueue(decal);
                while (liveDecals.Count > MaxDecals)
                {
                    GameObject old = liveDecals.Dequeue();
                    if (old != null) Destroy(old);
                }
            }
        }

        private static void ApplyScale(GameObject go, float scale)
        {
            if (!Mathf.Approximately(scale, 1f))
            {
                go.transform.localScale *= scale;
            }
        }
    }
}
