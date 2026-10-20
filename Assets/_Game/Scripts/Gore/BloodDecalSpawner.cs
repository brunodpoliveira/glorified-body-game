using System.Collections;
using GlorifiedBody.Combat;
using GlorifiedBody.Pooling;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GlorifiedBody.Gore
{
    public sealed class BloodDecalSpawner : MonoBehaviour
    {
        [SerializeField] private PoolService poolService;
        [SerializeField] private GameObject bloodDecalPrefab;
        [SerializeField] private Material bloodDecalMaterial;
        [SerializeField, Min(0)] private int prewarmCount = 48;
        [SerializeField, Min(0f)] private float floorSpillRayHeight = 1.5f;
        [SerializeField, Min(0f)] private float floorSpillRayDistance = 4f;
        [SerializeField] private LayerMask floorSpillLayer = -1;

        private int maxActive = 48;
        private static Material fallbackBloodDecalMaterial;

        private void Awake()
        {
            if (poolService == null)
            {
                poolService = GetComponent<PoolService>();
            }
            poolService?.Prewarm(bloodDecalPrefab, prewarmCount, transform);
        }

        public void Configure(int maxActiveDecals)
        {
            maxActive = Mathf.Max(0, maxActiveDecals);
            poolService?.Prewarm(bloodDecalPrefab, maxActive, transform);
        }

        public void Spawn(HitInfo hit, GoreSettings settings)
        {
            if (hit == null || settings == null || poolService == null || bloodDecalPrefab == null || maxActive <= 0)
            {
                return;
            }

            SpawnAt(hit.HitPoint, hit.HitNormal, settings, 1f);
        }

        public void SpawnFloorSpill(HitInfo hit, GoreSettings settings)
        {
            if (hit == null || settings == null || poolService == null || bloodDecalPrefab == null || maxActive <= 0)
            {
                return;
            }

            float spillChance = settings.CurrentGoreTier == GoreTier.Full ? 1f : settings.GetDecalChance();
            if (Random.value > spillChance)
            {
                return;
            }

            Vector3 origin = hit.HitPoint + Vector3.up * floorSpillRayHeight;
            if (!TryFindFloorSpill(origin, floorSpillRayHeight + floorSpillRayDistance, hit.Victim != null ? hit.Victim.transform : null, out RaycastHit floorHit))
            {
                return;
            }

            SpawnAt(floorHit.point, floorHit.normal, settings, 1.65f);
        }

        private bool TryFindFloorSpill(Vector3 origin, float distance, Transform victimRoot, out RaycastHit floorHit)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, floorSpillLayer, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.normal.y < 0.45f)
                {
                    continue;
                }

                if (victimRoot != null && hit.collider.transform.IsChildOf(victimRoot))
                {
                    continue;
                }

                floorHit = hit;
                return true;
            }

            floorHit = default;
            return false;
        }

        private void SpawnAt(Vector3 hitPoint, Vector3 hitNormal, GoreSettings settings, float sizeMultiplier)
        {
            PooledObject pooledObject = poolService.Get(bloodDecalPrefab, maxActive, transform);
            if (pooledObject == null)
            {
                return;
            }

            Vector3 normal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.up;
            Vector3 position = hitPoint + normal * settings.DecalSurfaceOffset;
            Quaternion rotation = Quaternion.LookRotation(-normal, Vector3.up) *
                Quaternion.Euler(0f, 0f, Random.Range(-settings.DecalRandomRotationRange, settings.DecalRandomRotationRange));

            pooledObject.transform.SetPositionAndRotation(position, rotation);

            DecalProjector projector = pooledObject.GetComponent<DecalProjector>();
            if (projector == null)
            {
                projector = pooledObject.gameObject.AddComponent<DecalProjector>();
            }

            if (projector == null)
            {
                poolService.Return(pooledObject);
                return;
            }

            Material decalMaterial = bloodDecalMaterial != null ? bloodDecalMaterial : GetFallbackBloodDecalMaterial();
            if (decalMaterial == null)
            {
                Debug.LogWarning("[Gore] Blood decal skipped: no valid decal material/shader available.");
                poolService.Return(pooledObject);
                return;
            }

            projector.material = decalMaterial;
            projector.drawDistance = 80f;
            float size = Random.Range(settings.DecalSizeRange.x, settings.DecalSizeRange.y) * Mathf.Max(0.1f, sizeMultiplier);
            projector.size = new Vector3(size, size, 0.08f);
            projector.pivot = new Vector3(0f, 0f, 0.04f);

            float lifetime = settings.GetDecalLifetime();
            if (lifetime > 0f)
            {
                StartCoroutine(ReturnAfterLifetime(pooledObject, pooledObject.SpawnVersion, lifetime));
            }
        }

        public void Clear()
        {
            poolService?.ClearActive();
        }

        private IEnumerator ReturnAfterLifetime(PooledObject pooledObject, int spawnVersion, float lifetime)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, lifetime));
            poolService?.ReturnIfVersion(pooledObject, spawnVersion);
        }

        private static Material GetFallbackBloodDecalMaterial()
        {
            if (fallbackBloodDecalMaterial != null)
            {
                return fallbackBloodDecalMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Decal") ?? Shader.Find("Shader Graphs/Decal");
            if (shader == null)
            {
                return null;
            }

            fallbackBloodDecalMaterial = new Material(shader)
            {
                name = "P2BloodDecal_Runtime",
                color = new Color(0.65f, 0f, 0.03f, 1f)
            };
            return fallbackBloodDecalMaterial;
        }
    }
}
