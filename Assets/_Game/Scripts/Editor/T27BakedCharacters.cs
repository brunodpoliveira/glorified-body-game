using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// T27 (2026-11-01, design §28a items 7 to 9): the baked game characters. The new guard (8,999 triangles), Lucy's new
    /// body (18,000) and the new helmet (2,800), each with 2048 maps baked in Blender
    /// (the-glorified-body/tests/T27_baked_characters/), as prefabs beside the old ones. Nothing old changes.
    /// In: Assets/_Game/Characters/T27/ t27_guard.fbx, t27_lucy.fbx, t27_crown.fbx and the maps t27_&lt;id&gt;_base / _n / _ms
    /// (_ao for the guard and the helmet). Out: Resources/T27/t27_guard.prefab and t27_lucy.prefab (the helmet on her
    /// mixamorig:Head, placed once at the rest pose), the URP Lit materials.
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.T27BakedCharacters.Import
    /// </summary>
    public static class T27BakedCharacters
    {
        public const string Folder = "Assets/_Game/Characters/T27";
        public const string PrefabFolder = Folder + "/Resources/T27";

        public static void Import()
        {
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();
            foreach (string id in new[] { "guard", "lucy", "crown" }) Textures(id);
            Material guard = LitMaterial("guard", ao: true);
            Material lucy = LitMaterial("lucy", ao: false);
            Material crown = LitMaterial("crown", ao: true);
            GameObject crownModel = StaticModel("t27_crown");
            Prefab("t27_guard", "guard", 1.80f, guard, null, null);
            Prefab("t27_lucy", "lucy_bare", 1.70f, lucy, crownModel, crown);
            AssetDatabase.SaveAssets();
            Debug.Log("[T27] import done");
        }

        private static void Textures(string id)
        {
            foreach (string kind in new[] { "base", "n", "ms", "ao" })
            {
                string path = $"{Folder}/t27_{id}_{kind}.png";
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                ti.textureType = kind == "n" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = kind == "base";
                ti.maxTextureSize = 2048;
                ti.mipmapEnabled = true;
                ti.filterMode = FilterMode.Trilinear;
                ti.anisoLevel = 8;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.alphaSource = kind == "ms" ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.SaveAndReimport();
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Debug.Log($"[T27] map {path}: {ti.textureType}, sRGB {ti.sRGBTexture}, {tex?.width}x{tex?.height}, {tex?.format}");
            }
        }

        private static Texture2D Tex(string id, string kind) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/t27_{id}_{kind}.png");

        private static Material LitMaterial(string id, bool ao)
        {
            string path = $"{Folder}/T27_{id}.mat";
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
            m.shader = lit;
            m.shaderKeywords = new string[0];
            m.SetFloat("_WorkflowMode", 1f);
            m.SetTexture("_BaseMap", Tex(id, "base"));
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_MetallicGlossMap", Tex(id, "ms"));
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Metallic", 1f);
            m.SetFloat("_Smoothness", 1f);
            m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.SetTexture("_BumpMap", Tex(id, "n"));
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            if (ao && Tex(id, "ao") != null)
            {
                m.SetTexture("_OcclusionMap", Tex(id, "ao"));
                m.SetFloat("_OcclusionStrength", 0.6f);
                m.EnableKeyword("_OCCLUSIONMAP");
            }
            m.SetFloat("_EnvironmentReflections", 1f);
            m.SetFloat("_SpecularHighlights", 1f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static GameObject StaticModel(string file)
        {
            string fbx = $"{Folder}/{file}.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.bakeAxisConversion = true;   // identity roots: the helmet and Lucy share one space
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        }

        private static void Prefab(string file, string controllerClass, float height, Material material, GameObject crownModel, Material crownMaterial)
        {
            string fbx = $"{Folder}/{file}.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = true;   // as the game's characters (SeveredLimb reads the bone weights and the triangles)
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.importAnimation = true;
            importer.bakeAxisConversion = true;
            importer.SaveAndReimport();
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                int bar = clip.takeName.LastIndexOf('|');
                clip.name = bar >= 0 ? clip.takeName.Substring(bar + 1) : clip.takeName;
                clip.loopTime = true;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"Assets/_Game/Characters/Anim/{controllerClass}.controller");
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var root = new GameObject(file);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "Model";
            instance.transform.SetParent(root.transform, false);
            instance.transform.localScale = Vector3.one * height;   // 3D Studio's 1.0 m source, as the T19 and T26 figures
            Animator animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            int tris = 0;
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
            {
                if (r.name.StartsWith("Helmet_T27")) continue;
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer sk) { sk.updateWhenOffscreen = true; tris += sk.sharedMesh != null ? sk.sharedMesh.triangles.Length / 3 : 0; }
            }
            // The helmet comes inside her FBX, bolted to mixamorig:Head in Blender at the rest pose (t27_fbx.py): it gets
            // its own material here. (t27_crown.fbx is the same helmet alone, for other outfits.)
            string helmet = "none";
            Renderer hr = instance.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name.StartsWith("Helmet_T27"));
            if (hr != null && crownMaterial != null)
            {
                hr.sharedMaterial = crownMaterial;
                hr.shadowCastingMode = ShadowCastingMode.On;
                if (hr is SkinnedMeshRenderer hsk) hsk.updateWhenOffscreen = true;
                Mesh hm = hr is SkinnedMeshRenderer hs ? hs.sharedMesh : (hr.TryGetComponent(out MeshFilter mf) ? mf.sharedMesh : null);
                int ctris = hm != null ? hm.triangles.Length / 3 : 0;
                helmet = $"{ctris} triangles, skinned to {(hr is SkinnedMeshRenderer s2 && s2.bones.Length > 0 ? string.Join(",", s2.bones.Select(b => b.name)) : "-")}";
            }
            string avatar = animator.avatar != null && animator.avatar.isHuman ? "human" : "MISSING";
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{file}.prefab");
            Object.DestroyImmediate(root);
            Debug.Log($"[T27] {file}: {tris} triangles, scale {height:0.00}, avatar {avatar}, controller {(controller != null ? controller.name : "MISSING")}, clips {string.Join(",", clips.Select(c => c.name))}, helmet {helmet}");
        }
    }
}
