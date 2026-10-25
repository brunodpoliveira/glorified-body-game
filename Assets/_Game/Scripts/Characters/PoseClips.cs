using UnityEngine;

namespace GlorifiedBody.Characters
{
    /// <summary>
    /// The clips that PoseOverride plays (Resources/Characters/pose_clips, made by
    /// CreateCharacterAssets.BuildPoseClips). All are RFab motion presets, Humanoid, so every T19 class can play
    /// them: the fear clips on the lab worker (2026-10-25, Bruno: "yes, buy it"), the pain and the sit on Lucy
    /// (Bruno: "the main character plays an anim of pain on the head").
    /// </summary>
    [CreateAssetMenu(menuName = "GlorifiedBody/Pose Clips")]
    public sealed class PoseClips : ScriptableObject
    {
        public const string ResourcePath = "Characters/pose_clips";

        [Tooltip("Lucy at the start of the short: the migraine hits (Bruno, 2026-10-25). The preset frustrated_02: " +
                 "PainFrom to PainFrom + the freeze holds a hand on the head, then the body hunches over it.")]
        public AnimationClip lucyPain;
        [Tooltip("On the floor, the knees up (the preset hurt): a civilian slides down a wall to it before it begs.")]
        public AnimationClip sit;

        /// <summary>The second of lucyPain where the hand reaches the head (frames checked, 2026-10-25).</summary>
        public const float PainFrom = 0.5f;
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
