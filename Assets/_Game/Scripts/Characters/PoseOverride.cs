using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace GlorifiedBody.Characters
{
    /// <summary>
    /// One clip over the Animator's own controller, for a pose the controller does not have: the fear clips of the
    /// civilians (RFab presets afraid, scared_01, scared_02), the pain of Lucy at the start of the short. A small
    /// PlayableGraph with one clip drives the Animator while it plays. When it ends, the controller takes the body
    /// back. A clip can loop, or play once and stop by itself. On unscaled time it plays through a freeze
    /// (Time.timeScale 0).
    /// </summary>
    public sealed class PoseOverride : MonoBehaviour
    {
        private Animator animator;
        private PlayableGraph graph;
        private AnimationClipPlayable playable;
        private bool once;
        private bool unscaled;
        private AnimatorUpdateMode modeBefore;

        /// <summary>The clip that plays now, or null.</summary>
        public AnimationClip Current { get; private set; }
        public bool Playing => graph.IsValid() && Current != null;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        /// <summary>Plays the clip over the controller. The same clip again does nothing (it keeps its time).</summary>
        public void Play(AnimationClip clip, bool loop, bool unscaledTime = false)
        {
            if (clip == null || animator == null) return;
            if (Playing && Current == clip && once == !loop) return;
            Stop();
            Current = clip;
            once = !loop;
            unscaled = unscaledTime;
            graph = PlayableGraph.Create($"{name}_Pose");
            graph.SetTimeUpdateMode(unscaled ? DirectorUpdateMode.UnscaledGameTime : DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
            playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            if (once) playable.SetDuration(clip.length);
            output.SetSourcePlayable(playable);
            if (unscaled)
            {
                modeBefore = animator.updateMode;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
            graph.Play();
        }

        /// <summary>Gives the body back to the controller.</summary>
        public void Stop()
        {
            if (graph.IsValid()) graph.Destroy();
            if (unscaled && animator != null) animator.updateMode = modeBefore;
            unscaled = false;
            Current = null;
        }

        private void Update()
        {
            if (!Playing || !once) return;
            if (playable.GetTime() >= Current.length) Stop();
        }

        private void OnDisable() => Stop();
    }
}
