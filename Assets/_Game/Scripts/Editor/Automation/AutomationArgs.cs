#if UNITY_EDITOR
using System;

namespace GlorifiedBody.Editor.Automation
{
    internal static class AutomationArgs
    {
        public static string Get(string name, string fallback = null)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return fallback;
        }

        /// <summary>Accepts "TrainingRoom", "TrainingRoom.unity" or "Assets/_Game/Scenes/TrainingRoom.unity".</summary>
        public static string ScenePath(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return null;
            if (scene.StartsWith("Assets/")) return scene.EndsWith(".unity") ? scene : scene + ".unity";
            if (scene.EndsWith(".unity")) scene = scene.Substring(0, scene.Length - 6);
            string direct = "Assets/_Game/Scenes/" + scene + ".unity";
            if (System.IO.File.Exists(direct)) return direct;
            // Bare name of a scene in a subfolder (for example "Dev/LookTest" -> Assets/_Game/Scenes/Dev/LookTest.unity).
            string name = System.IO.Path.GetFileName(scene);
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Scene " + name, new[] { "Assets/_Game/Scenes" }))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return path;
            }
            return direct;
        }
    }
}
#endif
