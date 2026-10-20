#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GlorifiedBody.Automation;
using GlorifiedBody.Level.Build;

namespace GlorifiedBody.Editor.Automation
{
    /// <summary>
    /// Edit-mode screenshots of a scene. Uses every ShotPoint in the scene. With no ShotPoint,
    /// it makes one overview shot from above.
    /// Batch (needs graphics, not -nographics): tools/unity.sh shots <Scene>
    /// </summary>
    public static class ShotCapture
    {
        public static void Run()
        {
            string scene = AutomationArgs.Get("-gbScene", "TrainingRoom");
            string runDir = AutomationArgs.Get("-gbRunDir", Path.Combine(Directory.GetCurrentDirectory(), "Temp", "shots"));
            int n = CaptureScene(scene, Path.Combine(runDir, "shots"));
            EditorApplication.Exit(n > 0 ? 0 : 1);
        }

        public static int CaptureScene(string scene, string outDir)
        {
            string path = AutomationArgs.ScenePath(scene);
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string sceneName = Path.GetFileNameWithoutExtension(path);

            var views = new List<(string name, Vector3 pos, Quaternion rot, float fov)>();
            foreach (ShotPoint sp in Object.FindObjectsByType<ShotPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                views.Add((sp.shotName, sp.transform.position, sp.transform.rotation, sp.fieldOfView));
            }

            if (views.Count == 0)
            {
                Bounds world = SceneBounds();
                Vector3 top = world.center + new Vector3(0f, Mathf.Max(world.extents.x, world.extents.z) * 1.1f + 20f, -world.extents.z * 0.6f);
                views.Add(("overview", top, Quaternion.LookRotation(world.center - top), 60f));
            }

            var camGo = new GameObject("[ShotCapture]");
            camGo.hideFlags = HideFlags.HideAndDontSave;
            Camera cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1500f;
            Camera sceneMain = Camera.main;
            if (sceneMain != null)
            {
                cam.clearFlags = sceneMain.clearFlags;
                cam.backgroundColor = sceneMain.backgroundColor;
            }

            int ok = 0;
            foreach (var v in views)
            {
                cam.transform.SetPositionAndRotation(v.pos, v.rot);
                cam.fieldOfView = v.fov;
                string file = Path.Combine(outDir, $"{sceneName}_{Safe(v.name)}.png");
                if (ShotRenderer.Render(cam, file)) { ok++; Debug.Log("[Shots] wrote " + file); }
                else Debug.LogError("[Shots] failed " + file);
            }
            Object.DestroyImmediate(camGo);
            Debug.Log($"[Shots] {sceneName}: {ok}/{views.Count} shots");
            return ok;
        }

        private static Bounds SceneBounds()
        {
            Renderer[] rs = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            if (rs.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 50f);
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) if (r.bounds.size.magnitude < 2000f) b.Encapsulate(r.bounds);
            return b;
        }

        private static string Safe(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }
    }
}
#endif
