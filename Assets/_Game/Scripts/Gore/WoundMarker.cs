using System.Collections;
using UnityEngine;

namespace GlorifiedBody.Gore
{
    public sealed class WoundMarker : MonoBehaviour
    {
        private DismembermentCleanup cleanup;

        public void Initialize(Transform attachTo, float lifetime, DismembermentCleanup ownerCleanup)
        {
            cleanup = ownerCleanup;

            if (attachTo != null)
            {
                transform.SetParent(attachTo, true);
            }

            if (lifetime > 0f)
            {
                StartCoroutine(DestroyAfterLifetime(lifetime));
            }
        }

        private IEnumerator DestroyAfterLifetime(float lifetime)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, lifetime));
            cleanup?.Forget(gameObject);
            Destroy(gameObject);
        }
    }
}
