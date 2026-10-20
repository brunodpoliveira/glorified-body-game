using UnityEngine;

namespace GlorifiedBody.Gore.ThatIndividual
{
    /// <summary>
    /// Root of one pooled ThatIndividual effect (a sanitized vendor particle prefab).
    /// Caches its particle systems once, so a replay does not allocate.
    /// </summary>
    public sealed class ThatIndividualGoreEffect : MonoBehaviour
    {
        private ParticleSystem[] systems;
        private ParticleSystem rootSystem;
        private Vector3 baseScale = Vector3.one;

        private void Awake()
        {
            Cache();
        }

        private void Cache()
        {
            if (systems != null)
            {
                return;
            }

            // Inactive children are layers the builder switched off (puddles). They stay off.
            systems = GetComponentsInChildren<ParticleSystem>(false);
            rootSystem = GetComponent<ParticleSystem>();
            baseScale = transform.localScale;
        }

        public void Play(float scale)
        {
            Cache();
            transform.localScale = baseScale * scale;
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Clear(false);
            }

            if (rootSystem != null)
            {
                rootSystem.Play(true);
                return;
            }

            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Play(false);
            }
        }

        public void StopAndClear()
        {
            Cache();
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
