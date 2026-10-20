using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Pooling
{
    /// <summary>
    /// Object pooling service. The sole path for spawning pooled prefabs.
    /// Never call Instantiate() directly for pooled prefab types.
    /// </summary>
    public sealed class PoolService : MonoBehaviour
    {
        private readonly Dictionary<GameObject, Queue<PooledObject>> inactiveByPrefab = new();
        private readonly Dictionary<GameObject, int> activeCountByPrefab = new();
        private readonly HashSet<PooledObject> activeObjects = new();

        public int ActiveCount => activeObjects.Count;

        public void Prewarm(GameObject prefab, int count, Transform parent = null)
        {
            if (prefab == null || count <= 0) return;

            Queue<PooledObject> queue = GetQueue(prefab);
            while (queue.Count < count)
            {
                PooledObject pooledObject = Create(prefab, parent);
                pooledObject.gameObject.SetActive(false);
                queue.Enqueue(pooledObject);
            }
        }

        public PooledObject Get(GameObject prefab, int maxActive, Transform parent = null)
        {
            if (prefab == null || maxActive <= 0 || GetActiveCount(prefab) >= maxActive)
            {
                RecycleOne(prefab);
                if (GetActiveCount(prefab) >= maxActive)
                {
                    return null;
                }
            }

            Queue<PooledObject> queue = GetQueue(prefab);
            PooledObject pooledObject = queue.Count > 0 ? queue.Dequeue() : Create(prefab, parent);
            pooledObject.transform.SetParent(parent != null ? parent : transform, false);
            pooledObject.MarkSpawned();
            pooledObject.gameObject.SetActive(true);
            activeObjects.Add(pooledObject);
            activeCountByPrefab[prefab] = GetActiveCount(prefab) + 1;
            return pooledObject;
        }

        public void Return(PooledObject pooledObject)
        {
            if (pooledObject == null) return;

            if (!activeObjects.Remove(pooledObject)) return;

            if (pooledObject.SourcePrefab != null)
            {
                activeCountByPrefab[pooledObject.SourcePrefab] = Mathf.Max(0, GetActiveCount(pooledObject.SourcePrefab) - 1);
            }

            pooledObject.MarkReturned();
            pooledObject.gameObject.SetActive(false);
            if (isActiveAndEnabled && transform != null && gameObject.activeInHierarchy)
            {
                pooledObject.transform.SetParent(transform, false);
            }

            if (pooledObject.SourcePrefab != null)
            {
                GetQueue(pooledObject.SourcePrefab).Enqueue(pooledObject);
            }
        }

        public void ReturnIfVersion(PooledObject pooledObject, int spawnVersion)
        {
            if (pooledObject != null && pooledObject.SpawnVersion == spawnVersion)
            {
                Return(pooledObject);
            }
        }

        public void ClearActive()
        {
            PooledObject[] snapshot = new PooledObject[activeObjects.Count];
            activeObjects.CopyTo(snapshot);
            foreach (PooledObject pooledObject in snapshot)
            {
                Return(pooledObject);
            }
        }

        private Queue<PooledObject> GetQueue(GameObject prefab)
        {
            if (!inactiveByPrefab.TryGetValue(prefab, out Queue<PooledObject> queue))
            {
                queue = new Queue<PooledObject>();
                inactiveByPrefab.Add(prefab, queue);
            }

            return queue;
        }

        private int GetActiveCount(GameObject prefab)
        {
            return activeCountByPrefab.TryGetValue(prefab, out int count) ? count : 0;
        }

        private void RecycleOne(GameObject prefab)
        {
            PooledObject candidate = null;
            foreach (PooledObject pooledObject in activeObjects)
            {
                if (pooledObject != null && pooledObject.SourcePrefab == prefab)
                {
                    candidate = pooledObject;
                    break;
                }
            }

            if (candidate != null)
            {
                Return(candidate);
            }
        }

        private PooledObject Create(GameObject prefab, Transform parent)
        {
            GameObject instance = Instantiate(prefab, parent != null ? parent : transform);
            PooledObject pooledObject = instance.GetComponent<PooledObject>() ?? instance.AddComponent<PooledObject>();
            pooledObject.Initialize(this, prefab);
            return pooledObject;
        }
    }
}
