using UnityEngine;

namespace GlorifiedBody.Rendering
{
    /// <summary>Placeholder flames (the boxes of G17): each tongue grows and shrinks on its own noise, and a light follows them. Game time.</summary>
    public sealed class Flicker : MonoBehaviour
    {
        public Transform[] tongues = new Transform[0];
        public Light glow;
        public float baseIntensity = 2.5f;

        private Vector3[] sizes;

        private void Awake()
        {
            sizes = new Vector3[tongues.Length];
            for (int i = 0; i < tongues.Length; i++) sizes[i] = tongues[i] != null ? tongues[i].localScale : Vector3.one;
        }

        private void Update()
        {
            float t = Time.time * 6f;
            for (int i = 0; i < tongues.Length; i++)
            {
                if (tongues[i] == null) continue;
                float k = 0.6f + 0.5f * Mathf.PerlinNoise(t + i * 3.1f, i * 0.7f);
                tongues[i].localScale = new Vector3(sizes[i].x, sizes[i].y * k, sizes[i].z);
            }
            if (glow != null) glow.intensity = baseIntensity * (0.75f + 0.5f * Mathf.PerlinNoise(t * 0.8f, 9.3f));
        }
    }
}
