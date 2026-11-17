using System.Collections.Generic;
using System.Linq;
using System.Text;
using GlorifiedBody.Enemy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace GlorifiedBody.Integration.Invector.Build
{
    /// <summary>
    /// Measures the gun holds of the armed classes on Invector's vBot (option B, 2026-11-17). For each class and vendor
    /// weapon set: the vBot and the class's figure play the same vendor aim clip, the vendor gun sits at zero on the
    /// vBot's Handler (the right hand), and the Humanoid hand frame is the same on both bodies. So the gun's place from
    /// the wrist (in the class's hand bone turn, in forearm lengths), the gun's turn in the hand and the left hand's turn
    /// against the gun's leftHandIK carry across. The rigs have no finger bones; the gloves are modelled half closed.
    /// The tune fields of an entry stay as they are. Batch:
    /// tools/unity.sh exec GlorifiedBody.Integration.Invector.Build.HoldCalibration.Build
    /// </summary>
    public static class HoldCalibration
    {
        public const string AssetPath = "Assets/_Game/Resources/Characters/hold_offsets.asset";
        private const string PosesFbx = "Assets/Invector-3rdPersonController/Shooter/3DModels/Animations/Shooter_UpperBodyPoses.fbx";
        private const string VBotPrefab = "Assets/Invector-FSMAIController/Prefabs/AI - Shooter.prefab";
        private const float ClipTime = 0.1f;

        /// <summary>The armed classes (InvectorEnemyFactory.Kinds) and their vendor weapon sets.</summary>
        public static readonly (string classId, int upperBody)[] Holds =
        {
            ("soldier", 1), ("soldier", 2), ("soldier", 3),
            ("guard", 1), ("toll_guard", 1), ("labworker", 1),
            ("power_armour", 2), ("power_armour", 3),
        };

        private static (string gun, string clip) SetOf(int upperBody)
        {
            switch (upperBody)
            {
                case 2: return (InvectorEnemyFactory.RiflePrefabPath, "Aiming@Rifle");
                case 3: return (InvectorEnemyFactory.LauncherPrefabPath, "Aiming@Shotgun");
                default: return (InvectorEnemyFactory.GunPrefabPath, "Aiming@Pistol");
            }
        }

        [MenuItem("GlorifiedBody/Content/Calibrate the Gun Holds", priority = 24)]
        public static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var set = AssetDatabase.LoadAssetAtPath<HoldOffsets>(AssetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<HoldOffsets>();
                AssetDatabase.CreateAsset(set, AssetPath);
            }
            var log = new StringBuilder("[HoldCalibration]");
            foreach (var (classId, upperBody) in Holds)
            {
                HoldOffsets.Entry entry = set.Find(classId, upperBody);
                if (entry == null)
                {
                    entry = new HoldOffsets.Entry { classId = classId, upperBody = upperBody };
                    set.entries.Add(entry);
                }
                if (Measure(classId, upperBody, entry, out string line)) log.Append("\n  ").Append(line);
                else log.Append($"\n  {classId} {upperBody}: NOT MEASURED ({line})");
            }
            set.entries.Sort((a, b) => a.classId == b.classId ? a.upperBody.CompareTo(b.upperBody) : string.CompareOrdinal(a.classId, b.classId));
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        private static bool Measure(string classId, int upperBody, HoldOffsets.Entry entry, out string line)
        {
            var (gunPath, clipName) = SetOf(upperBody);
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(PosesFbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName);
            if (clip == null) { line = "no clip " + clipName; return false; }
            var graphs = new List<PlayableGraph>();
            GameObject vbot = null, figure = null, vGun = null;
            try
            {
                vbot = Spawn(VBotPrefab, new Vector3(-1.5f, 0f, 0f));
                figure = Spawn($"{CreateCharacterAssetsFolder}/{classId}.prefab", new Vector3(1.5f, 0f, 0f));
                if (vbot == null || figure == null) { line = "a prefab is missing"; return false; }
                Animator v = vbot.GetComponentInChildren<Animator>(), u = figure.GetComponentInChildren<Animator>();
                graphs.Add(Sample(v, clip));
                graphs.Add(Sample(u, clip));
                Transform vHand = v.GetBoneTransform(HumanBodyBones.RightHand), uHand = u.GetBoneTransform(HumanBodyBones.RightHand);
                Transform vFore = v.GetBoneTransform(HumanBodyBones.RightLowerArm), uFore = u.GetBoneTransform(HumanBodyBones.RightLowerArm);
                Transform vLeft = v.GetBoneTransform(HumanBodyBones.LeftHand), uLeft = u.GetBoneTransform(HumanBodyBones.LeftHand);
                Transform handler = FindDeep(vHand, "Handler");
                if (handler == null) { line = "the vBot has no Handler"; return false; }
                for (int i = handler.childCount - 1; i >= 0; i--) Object.DestroyImmediate(handler.GetChild(i).gameObject);
                vGun = Spawn(gunPath, Vector3.zero);
                vGun.transform.SetParent(handler, false);
                vGun.transform.localPosition = Vector3.zero;
                vGun.transform.localRotation = Quaternion.identity;
                float vForearm = Vector3.Distance(vHand.position, vFore.position);
                entry.gripOffset = Quaternion.Inverse(uHand.rotation) * (vGun.transform.position - vHand.position) / vForearm;
                entry.gripEuler = (Quaternion.Inverse(uHand.rotation) * vGun.transform.rotation).eulerAngles;
                entry.leftEuler = (Quaternion.Inverse(vLeft.rotation) * uLeft.rotation).eulerAngles;
                line = $"{classId} {upperBody} ({clipName}): grip {entry.gripOffset} forearms, turn {entry.gripEuler}, left {entry.leftEuler}; " +
                       $"the hand bones differ by {Quaternion.Angle(vHand.rotation, uHand.rotation):0.0} deg (right) and {Quaternion.Angle(vLeft.rotation, uLeft.rotation):0.0} deg (left)";
                return true;
            }
            finally
            {
                foreach (PlayableGraph g in graphs) if (g.IsValid()) g.Destroy();
                if (vbot != null) Object.DestroyImmediate(vbot);
                if (figure != null) Object.DestroyImmediate(figure);
                if (vGun != null) Object.DestroyImmediate(vGun);
            }
        }

        private const string CreateCharacterAssetsFolder = "Assets/_Game/Characters/Prefabs";

        private static GameObject Spawn(string path, Vector3 position)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.SetPositionAndRotation(position, Quaternion.identity);
            for (int pass = 0; pass < 6; pass++)
                foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true)) if (mb != null) Object.DestroyImmediate(mb);
            foreach (Joint j in go.GetComponentsInChildren<Joint>(true)) Object.DestroyImmediate(j);
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (Rigidbody r in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(r);
            Animator animator = go.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = null;
            }
            return go;
        }

        private static PlayableGraph Sample(Animator animator, AnimationClip clip)
        {
            PlayableGraph graph = PlayableGraph.Create("hold calibration");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "pose", animator);
            var play = AnimationClipPlayable.Create(graph, clip);
            play.SetApplyFootIK(false);
            play.SetTime(ClipTime);
            output.SetSourcePlayable(play);
            graph.Evaluate(0f);
            return graph;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
