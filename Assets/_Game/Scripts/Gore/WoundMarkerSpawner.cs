using System.Collections;
using GlorifiedBody.Combat;
using GlorifiedBody.Pooling;
using UnityEngine;

namespace GlorifiedBody.Gore
{
    public sealed class WoundMarkerSpawner : MonoBehaviour
    {
        [SerializeField] private PoolService poolService;
        [SerializeField] private GameObject woundMarkerPrefab;
        [SerializeField, Min(0)] private int prewarmCount = 48;

        private int maxActive = 48;
        private static Material woundMarkerMaterial;

        private void Awake()
        {
            if (poolService == null)
            {
                poolService = GetComponent<PoolService>();
            }
            poolService?.Prewarm(woundMarkerPrefab, prewarmCount, transform);
        }

        public void Configure(int maxActiveMarkers)
        {
            maxActive = Mathf.Max(0, maxActiveMarkers);
            poolService?.Prewarm(woundMarkerPrefab, maxActive, transform);
        }

        public void Spawn(HitInfo hit, GoreSettings settings)
        {
            if (hit == null || settings == null || poolService == null || woundMarkerPrefab == null || maxActive <= 0)
            {
                return;
            }

            PooledObject pooledObject = poolService.Get(woundMarkerPrefab, maxActive, transform);
            if (pooledObject == null)
            {
                return;
            }

            Vector3 normal = hit.HitNormal.sqrMagnitude > 0.0001f ? hit.HitNormal.normalized : Vector3.up;
            pooledObject.transform.SetPositionAndRotation(hit.HitPoint + normal * settings.DecalSurfaceOffset * 1.5f, Quaternion.LookRotation(normal, Vector3.up));
            float size = Random.Range(settings.WoundMarkerSizeRange.x, settings.WoundMarkerSizeRange.y);
            pooledObject.transform.localScale = Vector3.one * size;
            ApplyReadableMaterial(pooledObject);

            if (hit.HitTransform != null)
            {
                pooledObject.transform.SetParent(hit.HitTransform, true);
            }

            if (settings.WoundMarkerLifetime > 0f)
            {
                StartCoroutine(ReturnAfterLifetime(pooledObject, pooledObject.SpawnVersion, settings.WoundMarkerLifetime));
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

        private static void ApplyReadableMaterial(PooledObject pooledObject)
        {
            Renderer renderer = pooledObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.sharedMaterial = GetWoundMarkerMaterial();
        }

        private static Material GetWoundMarkerMaterial()
        {
            if (woundMarkerMaterial != null)
            {
                return woundMarkerMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            woundMarkerMaterial = new Material(shader)
            {
                name = "P2WoundMarker_Runtime",
                color = new Color(0.42f, 0f, 0.02f, 1f)
            };
            return woundMarkerMaterial;
        }
    }
}
