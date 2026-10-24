using UnityEngine;

namespace GlorifiedBody.Destruction
{
    /// <summary>The sounds and the materials of the breaks (Resources/Destruction/destruction_fx, made by BreakableAssets).</summary>
    [CreateAssetMenu(menuName = "GlorifiedBody/Destruction Fx")]
    public sealed class DestructionFxSettings : ScriptableObject
    {
        public AudioClip woodBreak;
        public AudioClip metalBreak;
        public AudioClip glassBreak;
        public AudioClip plasterBreak;
        public AudioClip plasticBreak;
        public AudioClip chip;
        public AudioClip sparks;
        [Tooltip("URP Particles/Unlit, alpha blended: dust and chips.")]
        public Material dust;
        [Tooltip("URP Particles/Unlit, additive: sparks and glass glints.")]
        public Material glow;
        [Range(0f, 1f)] public float volume = 0.9f;
    }
}
