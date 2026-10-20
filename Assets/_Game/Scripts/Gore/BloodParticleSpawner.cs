using System.Collections;
using GlorifiedBody.Combat;
using GlorifiedBody.Pooling;
using UnityEngine;

namespace GlorifiedBody.Gore
{
    public sealed class BloodParticleSpawner : MonoBehaviour
    {
        [SerializeField] private PoolService poolService;
        [SerializeField] private GameObject bloodParticlePrefab;
        [SerializeField, Min(0)] private int prewarmCount = 32;

        private int maxActive = 32;
        private static Material bloodParticleMaterial;

        private void Awake()
        {
            if (poolService == null)
            {
                poolService = GetComponent<PoolService>();
            }
            poolService?.Prewarm(bloodParticlePrefab, prewarmCount, transform);
        }

        public void Configure(int maxActiveParticles)
        {
            maxActive = Mathf.Max(0, maxActiveParticles);
            poolService?.Prewarm(bloodParticlePrefab, maxActive, transform);
        }

        public void Spawn(HitInfo hit, GoreSettings settings)
        {
            if (hit == null || settings == null || poolService == null || bloodParticlePrefab == null || maxActive <= 0)
            {
                return;
            }

            PooledObject pooledObject = poolService.Get(bloodParticlePrefab, maxActive, transform);
            if (pooledObject == null)
            {
                return;
            }

            Transform pooledTransform = pooledObject.transform;
            pooledTransform.SetPositionAndRotation(hit.HitPoint, GetHitRotation(hit));

            ParticleSystem particles = pooledObject.GetComponent<ParticleSystem>();
            if (particles == null)
            {
                particles = pooledObject.gameObject.AddComponent<ParticleSystem>();
            }

            if (particles == null)
            {
                poolService.Return(pooledObject);
                return;
            }

            try
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ConfigureParticleSystem(particles, settings.BloodParticleLifetime);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"Blood particle settings fallback on {pooledObject.name}: {exception.Message}");
            }

            particles.Play(true);

            if (settings.BloodParticleLifetime > 0f)
            {
                StartCoroutine(ReturnAfterLifetime(pooledObject, pooledObject.SpawnVersion, settings.BloodParticleLifetime));
            }
        }

        public void Clear()
        {
            poolService?.ClearActive();
        }

        private static void ConfigureParticleSystem(ParticleSystem particles, float lifetime)
        {
            ParticleSystem.MainModule main = particles.main;
            main.duration = Mathf.Max(0.1f, lifetime);
            main.startLifetime = Mathf.Max(0.1f, lifetime * 0.45f);
            main.startSpeed = 4.5f;
            main.startSize = 0.16f;
            main.startColor = new Color(1f, 0f, 0.08f, 1f);
            main.loop = false;
            main.playOnAwake = false;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.05f;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = GetBloodParticleMaterial();
        }

        private static Material GetBloodParticleMaterial()
        {
            if (bloodParticleMaterial != null)
            {
                return bloodParticleMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            bloodParticleMaterial = new Material(shader)
            {
                name = "P2BloodParticle_Runtime"
            };
            bloodParticleMaterial.color = Color.white;
            return bloodParticleMaterial;
        }

        private static Quaternion GetHitRotation(HitInfo hit)
        {
            Vector3 direction = hit.HitNormal.sqrMagnitude > 0.0001f ? hit.HitNormal.normalized : Vector3.zero;
            if (direction == Vector3.zero && hit.Attacker != null && hit.Victim != null)
            {
                direction = (hit.Victim.transform.position - hit.Attacker.transform.position).normalized;
            }

            return direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction, Vector3.up) : Quaternion.identity;
        }

        private IEnumerator ReturnAfterLifetime(PooledObject pooledObject, int spawnVersion, float lifetime)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, lifetime));
            poolService?.ReturnIfVersion(pooledObject, spawnVersion);
        }
    }
}
