#if UNITY_EDITOR
using System.IO;
using GlorifiedBody.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GlorifiedBody.Editor.Characters
{
    /// <summary>
    /// Renders the plea (PleaHands) on a figure over a pose clip, from the front and the side, one column per muscle
    /// set, into one sheet. A tool to pick the numbers from pictures. Sets come from a JSON file
    /// (-gbPleaJson, {"sets":[{"name":"a","muscles":["Arm Down-Up"],"values":[0.2]}]}), else the PleaHands values.
    /// Run: tools/unity.sh exec GlorifiedBody.Editor.Characters.PoseProbe.Plea [-gbPleaJson f] [-gbOut f.png]
    /// </summary>
    public static class PoseProbe
    {
        [System.Serializable] private class Set { public string name; public string[] muscles; public float[] values; }
        [System.Serializable] private class Sets { public Set[] sets; }

        private const int Size = 384;

        /// <summary>
        /// The skinned mesh in world space, skinned here on the CPU from the bones and the bind poses. In edit mode
        /// neither the renderer nor BakeMesh takes the bone change of the same frame (BakeMesh gave no vertices).
        /// </summary>
        private static Mesh Skin(SkinnedMeshRenderer skin)
        {
            Mesh source = skin.sharedMesh;
            Transform[] bones = skin.bones;
            Matrix4x4[] bind = source.bindposes;
            var matrices = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) matrices[i] = bones[i] != null ? bones[i].localToWorldMatrix * bind[i] : Matrix4x4.identity;
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            BoneWeight[] weights = source.boneWeights;
            var outV = new Vector3[vertices.Length];
            var outN = new Vector3[vertices.Length];
            for (int v = 0; v < vertices.Length; v++)
            {
                BoneWeight w = weights[v];
                Matrix4x4 m = Add(Add(Scale(matrices[w.boneIndex0], w.weight0), Scale(matrices[w.boneIndex1], w.weight1)),
                                  Add(Scale(matrices[w.boneIndex2], w.weight2), Scale(matrices[w.boneIndex3], w.weight3)));
                outV[v] = m.MultiplyPoint3x4(vertices[v]);
                if (normals.Length == vertices.Length) outN[v] = m.MultiplyVector(normals[v]).normalized;
            }
            var mesh = new Mesh { indexFormat = source.indexFormat, vertices = outV, normals = outN, uv = source.uv, subMeshCount = source.subMeshCount };
            for (int sub = 0; sub < source.subMeshCount; sub++) mesh.SetTriangles(source.GetTriangles(sub), sub);
            return mesh;
        }

        private static Matrix4x4 Scale(Matrix4x4 m, float k)
        {
            for (int i = 0; i < 16; i++) m[i] *= k;
            return m;
        }

        private static Matrix4x4 Add(Matrix4x4 a, Matrix4x4 b)
        {
            for (int i = 0; i < 16; i++) a[i] += b[i];
            return a;
        }

        public static void Plea()
        {
            string json = Automation.AutomationArgs.Get("-gbPleaJson");
            string output = Automation.AutomationArgs.Get("-gbOut", Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "MEGA/glorified-body-renders/game/civilians/plea_probe/plea_sheet.png"));
            string figure = Automation.AutomationArgs.Get("-gbFigure", "labworker");
            int renderMax = int.Parse(Automation.AutomationArgs.Get("-gbRenderMax", "8"));
            float at = float.Parse(Automation.AutomationArgs.Get("-gbAt", "1.0"), System.Globalization.CultureInfo.InvariantCulture);

            Set[] sets;
            if (!string.IsNullOrEmpty(json) && File.Exists(json)) sets = JsonUtility.FromJson<Sets>(File.ReadAllText(json)).sets;
            else
            {
                var current = new Set { name = "code", muscles = new string[PleaHands.Plea.Length], values = new float[PleaHands.Plea.Length] };
                for (int i = 0; i < PleaHands.Plea.Length; i++) { current.muscles[i] = PleaHands.Plea[i].muscle; current.values[i] = PleaHands.Plea[i].value; }
                sets = new[] { new Set { name = "none", muscles = new string[0], values = new float[0] }, current };
            }

            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Camera camera = Camera.main;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.35f, 0.35f, 0.38f);
            camera.fieldOfView = 40f;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Characters/Prefabs/{figure}.prefab");
            GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            body.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Animator animator = body.GetComponentInChildren<Animator>();
            PoseClips clips = AssetDatabase.LoadAssetAtPath<PoseClips>("Assets/_Game/Resources/Characters/pose_clips.asset");
            AnimationClip clip = clips != null ? clips.sit : null;
            var handler = new HumanPoseHandler(animator.avatar, animator.transform);
            var pose = new HumanPose();

            var target = new RenderTexture(Size, Size, 24);
            var sheet = new Texture2D(Size * Mathf.Max(1, Mathf.Min(sets.Length, renderMax)), Size * 2, TextureFormat.RGB24, false);
            var read = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            (Vector3 eye, Vector3 look)[] views =
            {
                (new Vector3(-0.2f, 0.9f, 1.7f), new Vector3(-0.2f, 0.55f, 0.2f)), // the front: the figure faces +z
                (new Vector3(1.5f, 0.9f, 0.5f), new Vector3(-0.2f, 0.55f, 0.2f)), // the side
            };
            for (int s = 0; s < sets.Length; s++)
            {
                if (clip != null) clip.SampleAnimation(animator.gameObject, at);
                var set = new (string, float)[sets[s].muscles.Length];
                for (int m = 0; m < set.Length; m++) set[m] = (sets[s].muscles[m], sets[s].values[m]);
                PleaHands.Resolve(set, out int[] indices, out float[] values);
                if (indices.Length > 0) PleaHands.Apply(handler, ref pose, indices, values, 1f, animator.GetBoneTransform(HumanBodyBones.Hips));
                // The hand and the elbow from the shoulder, in the chest's frame (x to her right, y up, z where the
                // chest faces): the sit clip turns the body, so the root's frame is not the body's front.
                Transform shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                Vector3 across = shoulder.position - animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
                across.y = 0f;
                Quaternion chestFrame = Quaternion.LookRotation(Vector3.Cross(across.normalized, Vector3.up), Vector3.up);
                Quaternion toChest = Quaternion.Inverse(chestFrame);
                Vector3 hand = toChest * (animator.GetBoneTransform(HumanBodyBones.RightHand).position - shoulder.position);
                Vector3 elbow = toChest * (animator.GetBoneTransform(HumanBodyBones.RightLowerArm).position - shoulder.position);
                Vector3 palm = toChest * (animator.GetBoneTransform(HumanBodyBones.RightHand).rotation * Vector3.forward);
                Debug.Log($"[PoseProbe] SET {sets[s].name} hand {hand.x:0.00} {hand.y:0.00} {hand.z:0.00} elbow {elbow.x:0.00} {elbow.y:0.00} {elbow.z:0.00} handfwd {palm.x:0.00} {palm.y:0.00} {palm.z:0.00}");
                if (s >= renderMax) continue;
                // Edit mode does not skin the mesh again after a bone change: skin it here and draw it for each shot.
                var drawn = new System.Collections.Generic.List<(Mesh mesh, Material[] materials)>();
                foreach (SkinnedMeshRenderer skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    drawn.Add((Skin(skin), skin.sharedMaterials));
                    skin.enabled = false;
                }
                for (int v = 0; v < views.Length; v++)
                {
                    camera.transform.position = views[v].eye;
                    camera.transform.LookAt(views[v].look);
                    camera.targetTexture = target;
                    foreach ((Mesh mesh, Material[] materials) in drawn)
                        for (int sub = 0; sub < mesh.subMeshCount; sub++)
                            Graphics.DrawMesh(mesh, Matrix4x4.identity, materials[Mathf.Min(sub, materials.Length - 1)], 0, camera, sub);
                    camera.Render();
                    RenderTexture.active = target;
                    read.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                    read.Apply();
                    sheet.SetPixels(s * Size, (1 - v) * Size, Size, Size, read.GetPixels());
                }
                foreach ((Mesh mesh, Material[] _) in drawn) Object.DestroyImmediate(mesh);
                foreach (SkinnedMeshRenderer skin in body.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.enabled = true;
            }
            camera.targetTexture = null;
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllBytes(output, sheet.EncodeToPNG());
            handler.Dispose();
            Debug.Log($"[PoseProbe] wrote {output} ({sets.Length} sets, clip {(clip != null ? clip.name : "none")} at {at:0.00} s)");
        }
    }
}
#endif
