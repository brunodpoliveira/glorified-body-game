#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The Humanoid NavMesh agent of every room bake. The Unity default step height (0.75 m) is the height of a desk:
    /// the bake made every desk top a step, the paths went over the desks, and the Invector bodies stopped at the
    /// desk edge (found 2026-10-25: the nurse never reached a body behind a desk). 0.4 m is a stair step.
    /// The room builders call Ensure before they bake.
    /// </summary>
    public static class NavAgentSettings
    {
        public const float StepHeight = 0.4f;

        public static void Ensure()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
            if (assets == null || assets.Length == 0) { Debug.LogError("[NavAgentSettings] no NavMeshAreas.asset"); return; }
            var so = new SerializedObject(assets[0]);
            SerializedProperty agents = so.FindProperty("m_Settings");
            for (int i = 0; i < agents.arraySize; i++)
            {
                SerializedProperty agent = agents.GetArrayElementAtIndex(i);
                if (agent.FindPropertyRelative("agentTypeID").intValue != 0) continue; // Humanoid
                SerializedProperty climb = agent.FindPropertyRelative("agentClimb");
                if (Mathf.Approximately(climb.floatValue, StepHeight)) return;
                Debug.Log($"[NavAgentSettings] Humanoid step height {climb.floatValue} -> {StepHeight} m");
                climb.floatValue = StepHeight;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return;
            }
        }
    }
}
#endif
