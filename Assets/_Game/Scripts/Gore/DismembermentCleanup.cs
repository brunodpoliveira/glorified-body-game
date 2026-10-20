using System.Collections.Generic;
using UnityEngine;

namespace GlorifiedBody.Gore
{
    public sealed class DismembermentCleanup : MonoBehaviour
    {
        private readonly HashSet<GameObject> spawnedObjects = new();

        private void OnDisable()
        {
            ClearSpawned();
        }

        private void OnDestroy()
        {
            ClearSpawned();
        }

        public void Track(GameObject spawnedObject)
        {
            if (spawnedObject != null)
            {
                spawnedObjects.Add(spawnedObject);
            }
        }

        public void Forget(GameObject spawnedObject)
        {
            if (spawnedObject != null)
            {
                spawnedObjects.Remove(spawnedObject);
            }
        }

        public void ClearSpawned()
        {
            if (spawnedObjects.Count == 0)
            {
                return;
            }

            GameObject[] snapshot = new GameObject[spawnedObjects.Count];
            spawnedObjects.CopyTo(snapshot);
            spawnedObjects.Clear();

            foreach (GameObject spawnedObject in snapshot)
            {
                if (spawnedObject != null)
                {
                    Destroy(spawnedObject);
                }
            }
        }
    }
}
