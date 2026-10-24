using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Gore.ThatIndividual;

namespace GlorifiedBody.Editor.Gore
{
    /// <summary>
    /// Fills the gruesome-kill fields of the gore profile (rule 6: assets from editor code): the vendor bone ends
    /// and meat for a cut limb, the gush at the cut, and the stain materials of the pool under a dead body. The
    /// vendor prefabs stay in the vendor folder (never in git): the profile only holds their GUIDs, like the
    /// PF_TI_ variants. Batch: tools/unity.sh exec GlorifiedBody.Editor.Gore.GoreLimbSetup.Assign
    /// </summary>
    public static class GoreLimbSetup
    {
        public const string ProfilePath = "Assets/_Game/Prefabs/Gore/ThatIndividual/DA_ThatIndividualGoreProfile.asset";
        private const string Vendor = "Assets/ThatIndividual STORE ASSETS/GoreSystemsByThatIndividual/Prefabs/";
        private const string Ours = "Assets/_Game/Prefabs/Gore/ThatIndividual/";

        [MenuItem("GlorifiedBody/Gore/Assign Limb And Pool Parts")]
        public static void Assign()
        {
            var profile = AssetDatabase.LoadAssetAtPath<ThatIndividualGoreProfile>(ProfilePath);
            if (profile == null)
            {
                Debug.LogError($"[GoreLimbSetup] no profile at {ProfilePath}.");
                return;
            }
            profile.limbStumps = Load<GameObject>(Vendor + "goreLimb-prefab.prefab", Vendor + "goreLimb-prefab-withMuscles.prefab");
            profile.limbSprays = Load<GameObject>(Ours + "Effects/PF_TI_gushingBlood_insane_small.prefab", Ours + "Effects/PF_TI_BloodHit_classic_Huge.prefab");
            profile.bodyPoolMaterials = new[] { BodyPool(1), BodyPool(2), BodyPool(3) };
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GoreLimbSetup] stumps {profile.limbStumps.Length}, sprays {profile.limbSprays.Length}, pools {profile.bodyPoolMaterials.Length}.");
        }

        /// <summary>
        /// A dark copy of a vendor pool stain for the pool under a body: the hit stains use a bright comic rim,
        /// which covers most of a smooth pool mask and reads as red paint (the 2026-10-24 shot).
        /// </summary>
        private static Material BodyPool(int index)
        {
            string source = Ours + $"Materials/M_TI_Stain_pool{index}.mat";
            string path = Ours + $"Materials/M_TI_Stain_bodyPool{index}.mat";
            var from = AssetDatabase.LoadAssetAtPath<Material>(source);
            if (from == null)
            {
                Debug.LogWarning($"[GoreLimbSetup] missing {source}.");
                return null;
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(from);
                AssetDatabase.CreateAsset(material, path);
            }
            else material.CopyPropertiesFromMaterial(from);
            material.SetColor("_Color", BodyPoolColor);
            material.SetColor("_EdgeColor", BodyPoolEdge);
            material.SetFloat("_MaskLow", 0.2f);
            material.SetFloat("_MaskHigh", 0.4f);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static readonly Color BodyPoolColor = new Color(0.16f, 0f, 0.01f, 0.95f);
        public static readonly Color BodyPoolEdge = new Color(0.26f, 0.01f, 0.02f, 0.95f);

        private static T[] Load<T>(params string[] paths) where T : Object
        {
            var found = new List<T>();
            foreach (string path in paths)
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) found.Add(asset);
                else Debug.LogWarning($"[GoreLimbSetup] missing {path} (the vendor package is not imported?).");
            }
            return found.ToArray();
        }
    }
}
