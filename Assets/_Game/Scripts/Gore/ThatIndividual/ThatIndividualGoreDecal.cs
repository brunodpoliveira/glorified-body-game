using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GlorifiedBody.Gore.ThatIndividual
{
    /// <summary>
    /// Pooled URP decal with a ThatIndividual animated-blood material. Replaces the vendor
    /// animatedBloodStarter, which runs once and then disables itself (it cannot be pooled).
    /// Each pooled instance owns one material copy, made once in Awake, so a replay does not allocate.
    /// </summary>
    [RequireComponent(typeof(DecalProjector))]
    public sealed class ThatIndividualGoreDecal : MonoBehaviour
    {
        private static readonly int AnimStartTimeId = Shader.PropertyToID("_animStartTime");

        [SerializeField, Min(0.05f)] private float fadeOutTime = 1f;

        private DecalProjector projector;
        private Material instanceMaterial;
        private float lifetime;
        private float startTime;

        private void Awake()
        {
            projector = GetComponent<DecalProjector>();
            if (projector.material != null)
            {
                instanceMaterial = new Material(projector.material);
                projector.material = instanceMaterial;
            }
        }

        private void OnDestroy()
        {
            if (instanceMaterial != null)
            {
                Destroy(instanceMaterial);
            }
        }

        public void Play(float size, float lifetimeSeconds)
        {
            lifetime = Mathf.Max(0.1f, lifetimeSeconds);
            startTime = Time.time;
            projector.size = new Vector3(size, size, Mathf.Max(0.3f, size * 0.5f));
            projector.fadeFactor = 1f;
            if (instanceMaterial != null && instanceMaterial.HasProperty(AnimStartTimeId))
            {
                instanceMaterial.SetFloat(AnimStartTimeId, Time.time);
            }
        }

        private void Update()
        {
            float remaining = lifetime - (Time.time - startTime);
            projector.fadeFactor = Mathf.Clamp01(remaining / fadeOutTime);
        }
    }
}
