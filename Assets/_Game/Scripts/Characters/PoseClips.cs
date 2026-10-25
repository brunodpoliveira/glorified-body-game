using UnityEngine;

namespace GlorifiedBody.Characters
{
    /// <summary>
    /// The clips that PoseOverride plays (Resources/Characters/pose_clips, made by
    /// CreateCharacterAssets.BuildPoseClips). The fear clips are RFab motion presets on the lab worker
    /// (2026-10-25, Bruno: "yes, buy it"), Humanoid, so every T19 class can play them. The pain is a vendor clip
    /// (a placeholder: no preset shows an electric seizure, RFab gap 13).
    /// </summary>
    [CreateAssetMenu(menuName = "GlorifiedBody/Pose Clips")]
    public sealed class PoseClips : ScriptableObject
    {
        public const string ResourcePath = "Characters/pose_clips";

        [Tooltip("Lucy at the start of the short: the migraine hits (Bruno, 2026-10-25).")]
        public AnimationClip lucyPain;
        [Tooltip("A startle, once: a hand to the chest, then back.")]
        public AnimationClip afraid;
        [Tooltip("A shaking stance, the knees bent, the hands near the chest. A loop.")]
        public AnimationClip scared01;
        [Tooltip("A self-hug, the knees bent. A loop.")]
        public AnimationClip scared02;

        private static PoseClips cached;
        private static bool loaded;

        public static PoseClips Load()
        {
            if (!loaded)
            {
                cached = Resources.Load<PoseClips>(ResourcePath);
                loaded = true;
            }
            return cached;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            cached = null;
            loaded = false;
        }
    }
}
