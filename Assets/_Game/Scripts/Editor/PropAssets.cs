#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Combat;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The telekinesis props (Bruno, 2026-10-22: "rename them now, and the props"). A crate is a cube with a
    /// solid collider and a LockOnTargetable marked as a prop: the auto-lock never picks it, the target switch
    /// reaches it while mk_orzabal is selected, the lift raises it, the throw hurts the enemy it hits. It has no
    /// HP. Its pivot is its centre, so the drop puts it back on the floor. The material is unlit, as Bruno
    /// ruled for "objects you can throw" (03_GAME_DESIGN §12.3, after Resident Evil 4); the dark frame of the
    /// texture (tools/prop_crate_texture.py) shows the cube's edges. Placeholder art: the free asset props come
    /// later.
    /// </summary>
    public static class PropAssets
    {
        public const string CrateMaterialPath = ItemAssets.MaterialsFolder + "/GB_Prop_Crate.mat";
        public const string CrateTexturePath = "Assets/_Game/Props/crate_placeholder.png";

        /// <summary>A crate of the given edge in metres on the floor at the point, turned by yaw degrees.</summary>
        public static GameObject Crate(string name, Vector3 floorPoint, float yaw, float size = 0.5f)
        {
            GameObject obj = MenuLoopSetup.EnsurePrimitive(name, PrimitiveType.Cube, floorPoint + Vector3.up * (size * 0.5f), Vector3.one * size);
            obj.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            obj.isStatic = false;
            obj.layer = LayerMask.NameToLayer("Default");
            Renderer renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = CrateMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            LockOnTargetable target = MenuLoopSetup.EnsureComponent<LockOnTargetable>(obj);
            var so = new SerializedObject(target);
            so.FindProperty("fallbackHeight").floatValue = 0f; // the lock point is the centre
            so.FindProperty("isProp").boolValue = true;
            so.FindProperty("heavy").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return obj;
        }

        public static Material CrateMaterial()
        {
            Shader shader = Shader.Find(ItemAssets.UnlitShaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(CrateMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, CrateMaterialPath);
            }
            material.shader = shader;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CrateTexturePath);
            if (texture == null) Debug.LogError($"[PropAssets] no texture at {CrateTexturePath}: run tools/prop_crate_texture.py.");
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
#endif
