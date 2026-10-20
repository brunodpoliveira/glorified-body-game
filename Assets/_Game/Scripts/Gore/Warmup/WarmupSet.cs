using UnityEngine;

namespace GlorifiedBody.Gore.Warmup
{
    /// <summary>
    /// What FirstUseWarmup prepares during a level load: recorded shader variant collections and the
    /// prefabs whose materials are drawn once off-screen (enemies, gore effects, hit FX, hazards).
    /// The asset lives in Prefabs/Gore/Resources/Warmup. Build: GlorifiedBody.Editor.Gore.WarmupBuilder.Build
    /// </summary>
    [CreateAssetMenu(menuName = "GlorifiedBody/Gore/Warmup Set")]
    public sealed class WarmupSet : ScriptableObject
    {
        public const string ResourcePath = "Warmup/WarmupSet";

        [Tooltip("Shader variants recorded from playthroughs (GoreTest, Level01).")]
        public ShaderVariantCollection[] collections = System.Array.Empty<ShaderVariantCollection>();

        [Tooltip("Prefabs whose meshes and materials are drawn once by an off-screen camera.")]
        public GameObject[] drawPrefabs = System.Array.Empty<GameObject>();

        [Tooltip("Draw the prefabs off-screen. This warms GPU pipeline states for the real render passes.")]
        public bool drawOffscreen = true;
    }
}
