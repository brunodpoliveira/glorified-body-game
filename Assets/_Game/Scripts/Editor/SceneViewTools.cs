#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// Scene view help for Bruno's checks (2026-11-15, "slice off the roof to better indicate changes directly"):
    ///   GlorifiedBody/Scene View/Hide Roofs: the ceilings of every room and the black hall of the Hold go out of the Scene
    ///     view only (SceneVisibilityManager). The game, the scene file and the bake do not change.
    ///   GlorifiedBody/Scene View/Show Roofs: they come back.
    /// It also takes the MIS refactoring banner off the Scene view ("remove the mis refactor warning from the screen"):
    /// the vendor's MISRefactoringChecker draws it on every Scene view frame. We do not run the MIS refactor, so the banner
    /// only covers the view. Reflection: no compile link to the vendor (a clone without MIS compiles).
    /// </summary>
    [InitializeOnLoad]
    public static class SceneViewTools
    {
        private const string MisChecker = "com.mobilin.games.MISRefactoringChecker";

        static SceneViewTools()
        {
            // The vendor's static constructor can run after ours: remove its hook on the next editor tick.
            EditorApplication.delayCall += RemoveMisBanner;
        }

        private static void RemoveMisBanner()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type checker = assembly.GetType(MisChecker, false);
                if (checker == null) continue;
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(checker.TypeHandle); // its hook goes on first, then off
                MethodInfo draw = checker.GetMethod("OnSceneGUI", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (draw == null) return;
                SceneView.duringSceneGui -= (Action<SceneView>)Delegate.CreateDelegate(typeof(Action<SceneView>), draw);
                SceneView.RepaintAll();
                return;
            }
        }

        [MenuItem("GlorifiedBody/Scene View/Hide Roofs")]
        public static void HideRoofs()
        {
            GameObject[] roofs = Roofs();
            SceneVisibilityManager.instance.Hide(roofs, true);
            Debug.Log($"[SceneViewTools] {roofs.Length} roofs hidden in the Scene view");
        }

        [MenuItem("GlorifiedBody/Scene View/Show Roofs")]
        public static void ShowRoofs()
        {
            GameObject[] roofs = Roofs();
            SceneVisibilityManager.instance.Show(roofs, true);
            Debug.Log($"[SceneViewTools] {roofs.Length} roofs shown in the Scene view");
        }

        /// <summary>Every room's ceiling (DemoRoom.Ceiling) and the black hall of the Hold (DemoHold.Hall), in the open scenes.</summary>
        private static GameObject[] Roofs()
        {
            var roofs = new List<GameObject>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                        if (t.name == "Ceiling" || t.name == "[Hall]") roofs.Add(t.gameObject);
            }
            return roofs.ToArray();
        }
    }
}
#endif
