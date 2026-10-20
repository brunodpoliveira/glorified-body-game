using UnityEngine;

namespace GlorifiedBody.Pooling
{
    public sealed class PooledObject : MonoBehaviour
    {
        private PoolService owner;

        public GameObject SourcePrefab { get; private set; }
        public int SpawnVersion { get; private set; }

        public void Initialize(PoolService poolService, GameObject sourcePrefab)
        {
            owner = poolService;
            SourcePrefab = sourcePrefab;
        }

        public void MarkSpawned()
        {
            SpawnVersion++;
        }

        public void MarkReturned()
        {
            SpawnVersion++;
        }

        public void ReturnToPool()
        {
            owner?.Return(this);
        }
    }
}
