#if UNITY_EDITOR
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Characters;

namespace GlorifiedBody.Editor.Characters
{
    /// <summary>
    /// Samples a clip on a figure of the catalog and logs, every 0.05 s, the heights of the hips, the knees and the hands
    /// over the feet, and the hands' reach ahead of the hips. A tool to pick a segment of a vendor clip (2026-10-31: a
    /// CPR from Pick_Down). Run: tools/unity.sh exec GlorifiedBody.Editor.Characters.ClipProbe.PickDown
    /// </summary>
    public static class ClipProbe
    {
        public const string BasicActions = "Assets/Invector-3rdPersonController/Basic Locomotion/3D Models/Animations/Basic_Actions.fbx";

        public static void PickDown() => Probe(BasicActions, new[] { "Pick_Down", "Pick_Mid", "Put" }, "soldier");

        public static void Probe(string path, string[] clipNames, string classId)
        {
            CharacterCatalog catalog = CharacterCatalog.Load();
            CharacterEntry entry = catalog != null ? catalog.Find(classId) : null;
            if (entry == null || entry.prefab == null) { Debug.LogError("[ClipProbe] no class " + classId); return; }
            GameObject figure = Object.Instantiate(entry.prefab);
            Animator animator = figure.GetComponentInChildren<Animator>();
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
            Debug.Log("[ClipProbe] clips in " + path + ": " + string.Join(", ", clips.Select(c => $"{c.name} {c.length:0.00}s")));
            foreach (string name in clipNames)
            {
                AnimationClip clip = clips.FirstOrDefault(c => c.name == name);
                if (clip == null) { Debug.LogWarning("[ClipProbe] no clip " + name); continue; }
                var sb = new StringBuilder($"[ClipProbe] {name} ({clip.length:0.00} s) t: hips kneeL kneeR handL handR reach\n");
                AnimationMode.StartAnimationMode();
                for (float t = 0f; t <= clip.length + 0.001f; t += 0.05f)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(animator.gameObject, clip, t);
                    AnimationMode.EndSampling();
                    float foot = Mathf.Min(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y, animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
                    Vector3 hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                    float H(HumanBodyBones b) => animator.GetBoneTransform(b).position.y - foot;
                    Vector3 hand = (animator.GetBoneTransform(HumanBodyBones.LeftHand).position + animator.GetBoneTransform(HumanBodyBones.RightHand).position) * 0.5f;
                    float reach = Vector3.Dot(hand - hips, figure.transform.forward);
                    sb.Append($"{t:0.00}: {hips.y - foot:0.00} {H(HumanBodyBones.LeftLowerLeg):0.00} {H(HumanBodyBones.RightLowerLeg):0.00} {H(HumanBodyBones.LeftHand):0.00} {H(HumanBodyBones.RightHand):0.00} {reach:0.00}\n");
                }
                AnimationMode.StopAnimationMode();
                Debug.Log(sb.ToString());
            }
            Object.DestroyImmediate(figure);
        }
    }
}
#endif
