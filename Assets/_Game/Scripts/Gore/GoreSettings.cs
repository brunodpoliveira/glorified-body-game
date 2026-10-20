using UnityEngine;

namespace GlorifiedBody.Gore
{
    [CreateAssetMenu(menuName = "GlorifiedBody/Gore Settings", fileName = "DA_GoreSettings")]
    public sealed class GoreSettings : ScriptableObject
    {
        [SerializeField] private GoreTier currentGoreTier = GoreTier.Full;
        [SerializeField, Min(0)] private int maxActiveBloodParticles = 32;
        [SerializeField, Min(0)] private int maxActiveBloodDecals = 48;
        [SerializeField, Min(0)] private int maxActiveWoundMarkers = 48;
        [SerializeField, Min(0f)] private float bloodDecalLifetime = 0f;
        [SerializeField, Min(0f)] private float bloodParticleLifetime = 2f;
        [SerializeField, Min(0f)] private float woundMarkerLifetime = 0f;
        [SerializeField, Range(0f, 1f)] private float lowTierDecalChance = 0.25f;
        [SerializeField, Range(0f, 1f)] private float fullTierDecalChance = 0.75f;
        [SerializeField, Min(0f)] private float decalSurfaceOffset = 0.015f;
        [SerializeField, Min(0f)] private float decalRandomRotationRange = 180f;
        [SerializeField] private Vector2 decalSizeRange = new(0.55f, 1.35f);
        [SerializeField] private Vector2 woundMarkerSizeRange = new(0.12f, 0.2f);
        [SerializeField] private bool enableDebugLogging;

        public GoreTier CurrentGoreTier { get => currentGoreTier; set => currentGoreTier = value; }
        public int MaxActiveBloodParticles => maxActiveBloodParticles;
        public int MaxActiveBloodDecals => maxActiveBloodDecals;
        public int MaxActiveWoundMarkers => maxActiveWoundMarkers;
        public float BloodDecalLifetime => bloodDecalLifetime;
        public float BloodParticleLifetime => bloodParticleLifetime;
        public float WoundMarkerLifetime => woundMarkerLifetime;
        public float LowTierDecalChance => lowTierDecalChance;
        public float FullTierDecalChance => fullTierDecalChance;
        public float DecalSurfaceOffset => decalSurfaceOffset;
        public float DecalRandomRotationRange => decalRandomRotationRange;
        public Vector2 DecalSizeRange => decalSizeRange;
        public Vector2 WoundMarkerSizeRange => woundMarkerSizeRange;
        public bool EnableDebugLogging { get => enableDebugLogging; set => enableDebugLogging = value; }

        public float GetDecalChance()
        {
            return currentGoreTier == GoreTier.Full ? fullTierDecalChance :
                currentGoreTier == GoreTier.Low ? lowTierDecalChance : 0f;
        }

        public float GetDecalLifetime()
        {
            return currentGoreTier == GoreTier.Low ? bloodDecalLifetime * 0.5f : bloodDecalLifetime;
        }

        public int GetMaxActiveBloodDecals()
        {
            if (currentGoreTier == GoreTier.Off)
            {
                return 0;
            }

            return currentGoreTier == GoreTier.Low
                ? Mathf.Max(1, Mathf.CeilToInt(maxActiveBloodDecals * 0.35f))
                : maxActiveBloodDecals;
        }

        private void OnValidate()
        {
            maxActiveBloodParticles = Mathf.Max(0, maxActiveBloodParticles);
            maxActiveBloodDecals = Mathf.Max(0, maxActiveBloodDecals);
            maxActiveWoundMarkers = Mathf.Max(0, maxActiveWoundMarkers);
            bloodDecalLifetime = Mathf.Max(0f, bloodDecalLifetime);
            bloodParticleLifetime = Mathf.Max(0f, bloodParticleLifetime);
            woundMarkerLifetime = Mathf.Max(0f, woundMarkerLifetime);
            decalSurfaceOffset = Mathf.Max(0f, decalSurfaceOffset);
            decalRandomRotationRange = Mathf.Max(0f, decalRandomRotationRange);

            if (decalSizeRange.y < decalSizeRange.x)
            {
                decalSizeRange.y = decalSizeRange.x;
            }

            if (woundMarkerSizeRange.y < woundMarkerSizeRange.x)
            {
                woundMarkerSizeRange.y = woundMarkerSizeRange.x;
            }
        }
    }
}
