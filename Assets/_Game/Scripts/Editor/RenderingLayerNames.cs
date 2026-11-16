using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using GlorifiedBody.Rendering;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// Names the rendering layers the game uses, at each load of the editor. URP drops every rendering layer with no name in
    /// the project settings (RenderingLayerUtils.ToValidRenderingLayers): a light or a renderer on such a layer takes no
    /// light. Darkness.DarkLayer (bit 1) had no name until 2026-11-16, so the monitors lit nothing in the dark.
    /// </summary>
    [InitializeOnLoad]
    public static class RenderingLayerNames
    {
        public const string Dark = "Dark";

        static RenderingLayerNames()
        {
            EditorApplication.delayCall += Ensure;
        }

        [MenuItem("GlorifiedBody/Checks/Name The Rendering Layers", priority = 61)]
        public static void Ensure()
        {
            string[] names = RenderingLayerMask.GetDefinedRenderingLayerNames();
            int bit = 0;
            for (uint v = Darkness.DarkLayer; v > 1; v >>= 1) bit++;
            if (names.Length > bit)
            {
                if (names[bit] != Dark) Debug.LogWarning($"[RenderingLayerNames] layer {bit} is \"{names[bit]}\", not \"{Dark}\": Darkness.DarkLayer needs it.");
                return;
            }
            while (RenderingLayerMask.GetDefinedRenderingLayerNames().Length < bit)
                if (!RenderPipelineEditorUtility.TryAddRenderingLayerName("Layer" + RenderingLayerMask.GetDefinedRenderingLayerNames().Length)) return;
            if (RenderPipelineEditorUtility.TryAddRenderingLayerName(Dark))
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[RenderingLayerNames] rendering layer {bit} is \"{Dark}\" now.");
            }
            else Debug.LogWarning($"[RenderingLayerNames] could not name rendering layer {bit} \"{Dark}\".");
        }
    }
}
