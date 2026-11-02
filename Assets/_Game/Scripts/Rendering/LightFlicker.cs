using UnityEngine;

namespace GlorifiedBody.Rendering
{
    /// <summary>
    /// T26 switch 7, a live layer: a tired fluorescent tube. Steady most of the time, then a short stutter (two to
    /// six dips) every few seconds; the panel's emission follows the light. Disabled = steady at the full value.
    /// </summary>
    public sealed class LightFlicker : MonoBehaviour
    {
        public Light target;
        public Renderer emitter;
        public float minGap = 2.5f, maxGap = 6f;
        [Range(0f, 1f)] public float dipTo = 0.25f;
        public int seed = 7;

        private float baseIntensity;
        private Color baseEmission;
        private MaterialPropertyBlock block;
        private System.Random rng;
        private float nextStutter, stutterEnd, nextToggle;
        private bool dim;
        /// <summary>True during a stutter (the bot waits for a steady tube before a still).</summary>
        public bool Stuttering => isActiveAndEnabled && Time.time < stutterEnd + 0.05f;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            if (target == null) target = GetComponent<Light>();
            baseIntensity = target != null ? target.intensity : 1f;
            if (emitter != null && emitter.sharedMaterial != null && emitter.sharedMaterial.HasProperty(EmissionId)) baseEmission = emitter.sharedMaterial.GetColor(EmissionId);
            block = new MaterialPropertyBlock();
            rng = new System.Random(seed);
        }

        private void OnEnable()
        {
            nextStutter = Time.time + 1f;
        }

        private void OnDisable()
        {
            Set(1f);
        }

        private void Update()
        {
            float now = Time.time;
            if (now >= nextStutter && now > stutterEnd)
            {
                stutterEnd = now + 0.15f + (float)rng.NextDouble() * 0.5f;
                nextStutter = stutterEnd + minGap + (float)rng.NextDouble() * (maxGap - minGap);
                nextToggle = now;
            }
            if (now < stutterEnd)
            {
                if (now >= nextToggle)
                {
                    dim = !dim;
                    nextToggle = now + 0.03f + (float)rng.NextDouble() * 0.08f;
                }
                Set(dim ? dipTo + (float)rng.NextDouble() * 0.15f : 1f);
            }
            else
            {
                // the hum of a tube: a one percent ripple
                Set(0.985f + 0.015f * Mathf.Sin(now * 37f));
            }
        }

        private void Set(float k)
        {
            if (target != null) target.intensity = baseIntensity * k;
            if (emitter != null && block != null)
            {
                emitter.GetPropertyBlock(block);
                block.SetColor(EmissionId, baseEmission * k);
                emitter.SetPropertyBlock(block);
            }
        }
    }
}
