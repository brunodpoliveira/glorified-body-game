using GlorifiedBody.Combat;
using UnityEngine;

namespace GlorifiedBody.Gore
{
    public sealed class BloodFeedbackController : MonoBehaviour
    {
        [SerializeField] private BloodParticleSpawner particleSpawner;
        [SerializeField] private BloodDecalSpawner decalSpawner;
        [SerializeField] private WoundMarkerSpawner woundMarkerSpawner;

        private void Awake()
        {
            if (particleSpawner == null)
            {
                particleSpawner = GetComponentInChildren<BloodParticleSpawner>();
            }

            if (decalSpawner == null)
            {
                decalSpawner = GetComponentInChildren<BloodDecalSpawner>();
            }

            if (woundMarkerSpawner == null)
            {
                woundMarkerSpawner = GetComponentInChildren<WoundMarkerSpawner>();
            }
        }

        public void Configure(GoreSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            particleSpawner?.Configure(settings.MaxActiveBloodParticles);
            decalSpawner?.Configure(settings.GetMaxActiveBloodDecals());
            woundMarkerSpawner?.Configure(settings.MaxActiveWoundMarkers);
        }

        public void Play(HitInfo hit, GoreSettings settings)
        {
            if (hit == null || settings == null || settings.CurrentGoreTier == GoreTier.Off)
            {
                return;
            }

            bool actorHit = HitClassifier.IsActorHit(hit);
            if (actorHit)
            {
                particleSpawner?.Spawn(hit, settings);
                if (settings.CurrentGoreTier == GoreTier.Full)
                {
                    woundMarkerSpawner?.Spawn(hit, settings);
                }

                if (Random.value <= settings.GetDecalChance())
                {
                    decalSpawner?.Spawn(hit, settings);
                }

                decalSpawner?.SpawnFloorSpill(hit, settings);
            }
        }

        public void ClearAll()
        {
            particleSpawner?.Clear();
            decalSpawner?.Clear();
            woundMarkerSpawner?.Clear();
        }
    }
}
