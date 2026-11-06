using UnityEngine;
using GlorifiedBody.Enemy;

namespace GlorifiedBody.Characters
{
    /// <summary>
    /// On both knees, the arms low (mk_edgar, Bruno, 2026-11-06: "the enemy is on his knees on the floor"): the kneel of the CPR, the
    /// vendor's Pick_Down held at its low point with the legs over it as muscles (CorpseCare.CprLegs). A figure with no kneel clip (the
    /// power armour) only crouches.
    /// </summary>
    public sealed class KneelPose : MonoBehaviour
    {
        private Animator animator;
        private PoseOverride pose;
        private HumanPoseHandler handler;
        private HumanPose human;
        private int[] indices;
        private float[] values;
        private Transform hips;
        private bool on;

        public bool On => on;

        public static void Set(GameObject figure, bool kneel)
        {
            CorpseCare care = figure.GetComponent<CorpseCare>();
            Animator animator = figure.GetComponentInChildren<Animator>();
            if (care == null || care.KneelClip == null || animator == null || !animator.isHuman) return;
            KneelPose kneelPose = animator.GetComponent<KneelPose>() ?? animator.gameObject.AddComponent<KneelPose>();
            if (kneel) kneelPose.Begin(animator, care.KneelClip);
            else kneelPose.End();
        }

        private void Begin(Animator with, AnimationClip clip)
        {
            animator = with;
            if (handler == null && animator.avatar != null) handler = new HumanPoseHandler(animator.avatar, animator.transform);
            if (indices == null) PleaHands.Resolve(CorpseCare.CprLegs, out indices, out values);
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            pose = animator.GetComponent<PoseOverride>() ?? animator.gameObject.AddComponent<PoseOverride>();
            pose.Play(clip, loop: true, fade: 0.2f, from: CorpseCare.KneelFrom, length: CorpseCare.KneelLength);
            on = true;
        }

        private void End()
        {
            on = false;
            if (pose != null) pose.Stop();
        }

        private void LateUpdate()
        {
            if (on && handler != null && pose != null && pose.Playing) PleaHands.Apply(handler, ref human, indices, values, 1f, hips);
        }

        private void OnDestroy() => handler?.Dispose();
    }
}
