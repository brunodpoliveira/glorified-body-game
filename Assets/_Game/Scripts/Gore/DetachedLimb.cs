using System.Collections;
using GlorifiedBody.Combat;
using UnityEngine;

namespace GlorifiedBody.Gore
{
    public sealed class DetachedLimb : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float impulse = 6f;
        [SerializeField, Min(0f)] private float upwardImpulse = 1.5f;
        [SerializeField, Min(0f)] private float torqueImpulse = 2f;

        private Rigidbody body;
        private DismembermentCleanup cleanup;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        public void Initialize(HitInfo hit, float lifetime, DismembermentCleanup ownerCleanup)
        {
            cleanup = ownerCleanup;

            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }

            if (body != null)
            {
                Vector3 direction = GetImpulseDirection(hit);
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.AddForce(direction * impulse + Vector3.up * upwardImpulse, ForceMode.Impulse);
                body.AddTorque(Random.onUnitSphere * torqueImpulse, ForceMode.Impulse);
            }

            if (lifetime > 0f)
            {
                StartCoroutine(DestroyAfterLifetime(lifetime));
            }
        }

        private static Vector3 GetImpulseDirection(HitInfo hit)
        {
            if (hit != null && hit.HitNormal.sqrMagnitude > 0.0001f)
            {
                return hit.HitNormal.normalized;
            }

            if (hit != null && hit.Attacker != null)
            {
                Vector3 fromAttacker = hit.HitPoint - hit.Attacker.transform.position;
                if (fromAttacker.sqrMagnitude > 0.0001f)
                {
                    return fromAttacker.normalized;
                }
            }

            return Random.onUnitSphere;
        }

        private IEnumerator DestroyAfterLifetime(float lifetime)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, lifetime));
            cleanup?.Forget(gameObject);
            Destroy(gameObject);
        }
    }
}
