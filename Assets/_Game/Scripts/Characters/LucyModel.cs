using UnityEngine;

namespace GlorifiedBody.Characters
{
    /// <summary>
    /// Her model by the outfit (2026-10-31, the outfit change: lucy_bare in the nude outfit, lucy_guard in the uniform).
    /// The model is the child "Model" of her root (MenuLoopSetup.EnsureCharacterModel). A change puts the catalog's
    /// prefab of the class in its place with the same layer (the stencil mark of the look). The systems find her
    /// Animator in her children when they need it, so the new one takes over at once (LucyAnimator finds it again).
    /// </summary>
    public static class LucyModel
    {
        public const string ModelName = "Model";

        /// <summary>The class of the model she wears now (the catalog id), or empty.</summary>
        public static string Current(GameObject root)
        {
            Transform model = root != null ? root.transform.Find(ModelName) : null;
            ModelClass tag = model != null ? model.GetComponent<ModelClass>() : null;
            return tag != null ? tag.id : model != null ? "lucy_bare" : "";
        }

        /// <summary>Puts the model of the class on her. False when she wears it already or the catalog has no prefab.</summary>
        public static bool Wear(GameObject root, string classId)
        {
            if (root == null || string.IsNullOrEmpty(classId) || Current(root) == classId) return false;
            CharacterCatalog catalog = CharacterCatalog.Load();
            CharacterEntry entry = catalog != null ? catalog.Find(classId) : null;
            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"[LucyModel] no prefab for the class '{classId}'");
                return false;
            }
            Transform old = root.transform.Find(ModelName);
            int layer = old != null ? old.gameObject.layer : root.layer;
            GameObject model = Object.Instantiate(entry.prefab, root.transform, false);
            model.name = ModelName;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            model.AddComponent<ModelClass>().id = classId;
            if (old != null)
            {
                // Out of the hierarchy now: GetComponentInChildren must not find the old Animator this frame.
                old.gameObject.SetActive(false);
                old.SetParent(null, false);
                Object.Destroy(old.gameObject);
            }
            return true;
        }
    }
}
