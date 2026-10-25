using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace GlorifiedBody.Characters
{
    /// <summary>
    /// One clip over the Animator's own controller, for a pose the controller does not have: the fear clips of the
    /// civilians (RFab presets afraid, scared_01, scared_02, hurt), the head pain of Lucy at the start of the short
    /// (frustrated_02). A small PlayableGraph drives the Animator while a clip plays. When it stops, the controller
    /// takes the body back. A clip can loop, or play once and stop by itself. It can play a segment of the clip
    /// (from, length). A new clip can blend over the old one (fade), as the slide down a wall. On unscaled time it
    /// plays through a freeze (Time.timeScale 0).
    /// </summary>
    public sealed class PoseOverride : MonoBehaviour
    {
        private Animator animator;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable current;
        private AnimationClipPlayable previous;
        private bool once;
        private bool unscaled;
        private AnimatorUpdateMode modeBefore;
        private double segmentFrom;
        private double segmentEnd; // < 0: the whole clip
        private float fadeTotal, fadeLeft;

        /// <summary>The clip that plays now, or null.</summary>
        public AnimationClip Current { get; private set; }
        public bool Playing => graph.IsValid() && Current != null;
        /// <summary>The time in the clip now (the tests).</summary>
        public double Time => current.IsValid() ? current.GetTime() : 0d;
        public bool Fading => fadeLeft > 0f;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        /// <summary>
        /// Plays the clip over the controller. The same clip again does nothing (it keeps its time). With a fade, a
        /// clip that plays now blends out while the new one blends in.
        /// </summary>
        public void Play(AnimationClip clip, bool loop, bool unscaledTime = false, float fade = 0f, float from = 0f, float length = -1f)
        {
            if (clip == null || animator == null) return;
            if (Playing && Current == clip && once == !loop) return;
            bool blend = fade > 0f && Playing && unscaled == unscaledTime;
            if (!blend)
            {
                Stop();
                Build(unscaledTime);
            }
            else
            {
                if (previous.IsValid())
                {
                    mixer.DisconnectInput(0);
                    previous.Destroy();
                }
                mixer.DisconnectInput(1);
                previous = current;
                mixer.ConnectInput(0, previous, 0, 1f);
            }
            current = AnimationClipPlayable.Create(graph, clip);
            current.SetApplyFootIK(false);
            current.SetTime(from);
            mixer.ConnectInput(1, current, 0, blend ? 0f : 1f);
            if (!blend) mixer.SetInputWeight(0, 0f);
            fadeTotal = fadeLeft = blend ? fade : 0f;
            Current = clip;
            once = !loop;
            segmentFrom = from;
            segmentEnd = length > 0f ? from + length : once ? clip.length : -1d;
        }

        private void Build(bool unscaledTime)
        {
            unscaled = unscaledTime;
            graph = PlayableGraph.Create($"{name}_Pose");
            graph.SetTimeUpdateMode(unscaled ? DirectorUpdateMode.UnscaledGameTime : DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(graph, "Pose", animator);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            output.SetSourcePlayable(mixer);
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
            fadeLeft = 0f;
        }

        private void Update()
        {
            if (!Playing) return;
            if (fadeLeft > 0f)
            {
                fadeLeft -= unscaled ? UnityEngine.Time.unscaledDeltaTime : UnityEngine.Time.deltaTime;
                float w = fadeLeft > 0f ? 1f - fadeLeft / fadeTotal : 1f;
                mixer.SetInputWeight(1, w);
                mixer.SetInputWeight(0, 1f - w);
                if (fadeLeft <= 0f && previous.IsValid())
                {
                    mixer.DisconnectInput(0);
                    previous.Destroy();
                }
            }
            if (segmentEnd < 0d || current.GetTime() < segmentEnd) return;
            if (once) Stop();
            else current.SetTime(segmentFrom); // a looped segment starts again
        }

        private void OnDisable() => Stop();
    }
}
