using UnityEngine;

namespace GlorifiedBody.Gore.ThatIndividual
{
    /// <summary>
    /// Tuning and effect lists for <see cref="ThatIndividualGoreDriver"/>. The effect prefabs are sanitized
    /// variants of the vendor prefabs, made by GlorifiedBody.Editor.Gore.ThatIndividualGoreBuilder.
    /// </summary>
    [CreateAssetMenu(menuName = "GlorifiedBody/ThatIndividual Gore Profile", fileName = "DA_ThatIndividualGoreProfile")]
    public sealed class ThatIndividualGoreProfile : ScriptableObject
    {
        [Header("Backend")]
        [Tooltip("SkinGore: skin wounds only. ThatIndividual: sprays and stains only (SkinGoreDriver is disabled). Both: SkinGore wounds plus ThatIndividual sprays and stains.")]
        public GoreBackend backend = GoreBackend.Both;
        [Tooltip("Set the legacy procedural blood (BloodFeedbackController) caps to 0 while ThatIndividual runs, so blood does not double up.")]
        public bool muteLegacyBlood = true;

        [Header("Sprays (at the hit point, along the hit direction)")]
        public GameObject[] lightSprays;
        public GameObject[] heavySprays;
        public GameObject[] fatalBursts;
        [Tooltip("Extra layer on fatal heavy hits: meat chunks, skull parts, gushers.")]
        public GameObject[] fatalHeavyExtras;

        [Header("Surface stains (flat vendor stain quads)")]
        public GameObject[] floorStains;
        public GameObject[] wallStains;
        [Tooltip("URP decal prefabs (animated bleed). Used only when the active renderer has a DecalRendererFeature.")]
        public GameObject[] wallDecals;

        [Header("Look")]
        [Tooltip("Comic scale on sprays. Above 1 reads louder.")]
        public float sprayScale = 1.35f;
        public Vector2 floorStainScale = new(1.1f, 1.8f);
        public Vector2 wallStainScale = new(1.4f, 2.2f);
        [Tooltip("Stain tint. The vendor uses a near-black glossy red that reads as a hole in our flat lighting. Applied by the builder to copies of the stain materials.")]
        public Color stainColor = new(0.36f, 0f, 0.02f, 1f);
        [Tooltip("Spray tint (flat, unlit). The vendor spray shaders are lit and read grey or near-black in our lighting. Applied by the builder to copies of the billboard spray materials. Mesh particles (meat, skull) keep the vendor look.")]
        public Color sprayColor = new(0.85f, 0.02f, 0.04f, 1f);
        [Tooltip("Spray colour where the mask is thin (edges, mist).")]
        public Color sprayEdgeColor = new(0.45f, 0f, 0.02f, 1f);
        [Tooltip("Spray particle alpha scale for mist and puff layers (soft, so they do not hide the arena).")]
        [Range(0f, 1f)] public float mistAlpha = 0.45f;
        [Tooltip("Spray scale when the player is the victim. Smaller so the screen stays readable.")]
        public float playerVictimScale = 0.6f;
        [Tooltip("Upward tilt added to the spray direction (0 = flat, 1 = 45 degrees).")]
        public float sprayLift = 0.35f;

        [Header("Lifetimes (scaled seconds)")]
        public float lightLifetime = 1.2f;
        public float heavyLifetime = 2f;
        public float fatalLifetime = 3f;
        public float stainLifetime = 6f;
        [Tooltip("Particle lifetimes in the sanitized variants are clamped to this, so nothing outlives its pool slot.")]
        public float maxParticleLifetime = 6f;

        [Header("Budget (GTX 1050)")]
        [Tooltip("A fatal heavy kill takes 3 slots. 24 covers 8 kills in one frame before the ring cuts a live burst.")]
        [Min(1)] public int maxLiveEffects = 24;
        [Min(1)] public int maxLiveStains = 24;
        [Tooltip("Five kills in one frame can pick the same prefab five times. Prewarm covers that without growing the pool.")]
        [Min(0)] public int prewarmPerPrefab = 5;
        [Tooltip("Low tier keeps this share of the caps.")]
        [Range(0.1f, 1f)] public float lowTierBudget = 0.4f;

        [Header("Rules")]
        [Tooltip("Non-fatal hits at or above this damage count as heavy.")]
        public float heavyDamageThreshold = 60f;
        [Range(0f, 1f)] public float lightWallStainChance = 0.25f;
        [Range(0f, 1f)] public float heavyWallStainChance = 0.7f;
        public float floorRayHeight = 1.5f;
        public float floorRayDistance = 5f;
        [Tooltip("How far blood flies to a wall behind the victim.")]
        public float wallRayDistance = 6f;
        public float surfaceOffset = 0.02f;
        public LayerMask surfaceMask = 1; // Default
    }
}
