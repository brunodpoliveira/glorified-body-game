using UnityEditor;
using UnityEngine;

namespace GlorifiedBody.Editor.Gore
{
    /// <summary>
    /// The textured sprays (Bruno, 2026-10-24: "change it to the textured sprays"). The Warspite port drew every
    /// spray with its flat comic shader (GoreSprayUnlit: a loud red mask, or a round drop with no texture), which
    /// read as red cards in a close shot. Each M_TI_Spray_ material now takes the shader and the properties of its
    /// vendor material (the vendor's URP shader graphs with the blood flipbooks). The procedural drop
    /// (bloodFlying 2) takes the textured fly shader. The mist takes URP Particles/Unlit with its picture. The
    /// materials stay ours (in git); they point at the vendor shaders and textures by GUID, like the PF_TI_ variants.
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.Gore.GoreSprayLook.UseTexturedSprays
    /// </summary>
    public static class GoreSprayLook
    {
        private const string Ours = "Assets/_Game/Prefabs/Gore/ThatIndividual/Materials/";
        private const string Vendor = "Assets/ThatIndividual STORE ASSETS/GoreSystemsByThatIndividual/Materials/";
        private const string ParticlesUnlit = "Universal Render Pipeline/Particles/Unlit";
        private const float MaxSmoothness = 0.55f; // wet, not a mirror, PROPOSED

        // Ours -> the vendor material it copies.
        private static readonly (string ours, string vendor)[] Map =
        {
            ("M_TI_Spray_animSheetMaterial_1", "animSheetMaterial 1"),
            ("M_TI_Spray_animSheetMaterial_2", "animSheetMaterial 2"),
            ("M_TI_Spray_animSheetMaterial_3", "animSheetMaterial 3"),
            ("M_TI_Spray_animSheetMaterial_4", "animSheetMaterial 4"),
            ("M_TI_Spray_animSheetMaterial_5", "animSheetMaterial 5"),
            ("M_TI_Spray_bloodFlyingTextured", "bloodFlyingTextured"),
            ("M_TI_Spray_bloodFlyingTextured_Trail", "bloodFlyingTextured"),
            ("M_TI_Spray_bloodFlying_2", "bloodFlyingTextured"),
            ("M_TI_Spray_bloodFlying_2_Trail", "bloodFlyingTextured"),
        };

        [MenuItem("GlorifiedBody/Gore/Use Textured Sprays")]
        public static void UseTexturedSprays()
        {
            int done = 0;
            foreach ((string ours, string vendor) in Map)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(Ours + ours + ".mat");
                var source = AssetDatabase.LoadAssetAtPath<Material>(Vendor + vendor + ".mat");
                if (material == null || source == null)
                {
                    Debug.LogError($"[GoreSprayLook] {ours} or its vendor material '{vendor}' is missing.");
                    continue;
                }
                material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
                material.renderQueue = source.renderQueue;
                // The vendor sheets are wet mirrors (smoothness 1): indoors they showed the blue default sky.
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", Mathf.Min(material.GetFloat("_Smoothness"), MaxSmoothness));
                EditorUtility.SetDirty(material);
                done++;
            }

            // The mist: the vendor uses a built-in particle shader, which URP does not draw. URP Particles/Unlit with
            // its picture, alpha blended, the vendor's dark red.
            var mist = AssetDatabase.LoadAssetAtPath<Material>(Ours + "M_TI_Spray_red_mist_puff.mat");
            var mistSource = AssetDatabase.LoadAssetAtPath<Material>(Vendor + "red mist puff.mat");
            if (mist != null && mistSource != null)
            {
                Shader shader = Shader.Find(ParticlesUnlit);
                if (mist.shader != shader) mist.shader = shader;
                mist.SetTexture("_BaseMap", mistSource.GetTexture("_MainTex"));
                mist.SetColor("_BaseColor", new Color(0.45f, 0f, 0f, 0.72f));
                mist.SetFloat("_Surface", 1f);
                mist.SetFloat("_Blend", 0f);
                mist.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mist.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mist.SetFloat("_ZWrite", 0f);
                mist.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mist.SetOverrideTag("RenderType", "Transparent");
                mist.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                EditorUtility.SetDirty(mist);
                done++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[GoreSprayLook] {done} spray materials on the textured vendor look.");
        }
    }
}
