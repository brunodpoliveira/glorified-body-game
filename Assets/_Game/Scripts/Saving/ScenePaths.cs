using UnityEngine;
using UnityEngine.SceneManagement;

namespace GlorifiedBody.Saving
{
    /// <summary>
    /// An object's name in a save: its path from the scene root ("[Enemies]/Lab_A"). The room builders name every
    /// object, so a rebuild of the room keeps the paths. Find also reaches inactive objects.
    /// </summary>
    public static class ScenePaths
    {
        public static string Of(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        public static GameObject Find(Scene scene, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            int slash = path.IndexOf('/');
            string rootName = slash < 0 ? path : path.Substring(0, slash);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != rootName) continue;
                if (slash < 0) return root;
                Transform found = root.transform.Find(path.Substring(slash + 1));
                if (found != null) return found.gameObject;
            }
            return null;
        }
    }
}
