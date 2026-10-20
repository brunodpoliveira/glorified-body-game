#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace GlorifiedBody.Editor.Automation
{
    /// <summary>
    /// Static checks on a gameplay scene. Writes sanity.json. Each failed check is a Debug.LogError,
    /// so log triage marks the run DIRTY.
    /// Batch: tools/unity.sh sanity <Scene>   (-executeMethod GlorifiedBody.Editor.Automation.WorldSanity.Run -gbScene X)
    /// </summary>
    public static class WorldSanity
    {
        private static readonly string[] RequiredSystems =
        {
            // The persistent managers (GameManager, InputManager, SettingsManager, DifficultyManager) live in
            // Main_Menu and survive the scene load. GameplayInitializer creates them when the room starts alone.
            "ImpactFrameController", "PlayerDeathFlowController", "GameplayInitializer", "PoolService",
            "TankController", "LockOnController", "FixedCameraDirector",
            "PsychicStats", "PsychicPowers", "DummyShooter", "PlayerHud"
        };

        private struct Check
        {
            public string name;
            public bool ok;
            public bool warnOnly;
            public string detail;
        }

        [MenuItem("GlorifiedBody/Automation/World Sanity (Active Scene)")]
        public static void RunOnActiveSceneMenu()
        {
            List<Check> checks = CheckActiveScene();
            Report(EditorSceneManager.GetActiveScene().name, checks, Path.Combine(Directory.GetCurrentDirectory(), "Temp", "sanity"));
        }

        public static void Run()
        {
            string scene = AutomationArgs.Get("-gbScene", "TrainingRoom");
            string runDir = AutomationArgs.Get("-gbRunDir", Path.Combine(Directory.GetCurrentDirectory(), "Temp", "sanity"));
            bool pass = RunOn(scene, runDir);
            EditorApplication.Exit(pass ? 0 : 1);
        }

        public static bool RunOn(string scene, string runDir)
        {
            string path = AutomationArgs.ScenePath(scene);
            if (!File.Exists(path))
            {
                Debug.LogError($"[Sanity] scene not found: {path}");
                return false;
            }
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            List<Check> checks = CheckActiveScene();
            return Report(Path.GetFileNameWithoutExtension(path), checks, runDir);
        }

        private static List<Check> CheckActiveScene()
        {
            var checks = new List<Check>();
            var scene = EditorSceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            List<GameObject> all = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToList();

            // Missing scripts.
            int missing = 0;
            var missingNames = new List<string>();
            foreach (GameObject go in all)
            {
                int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (n > 0) { missing += n; if (missingNames.Count < 10) missingNames.Add(go.name); }
            }
            checks.Add(new Check { name = "no_missing_scripts", ok = missing == 0, detail = missing == 0 ? "0" : $"{missing} on: {string.Join(", ", missingNames)}" });

            // Build settings.
            bool inBuild = EditorBuildSettings.scenes.Any(s => s.path == scene.path && s.enabled);
            checks.Add(new Check { name = "in_build_settings", ok = inBuild, detail = scene.path });

            // System singletons.
            var present = new HashSet<string>(all.SelectMany(g => g.GetComponents<MonoBehaviour>()).Where(m => m != null).Select(m => m.GetType().Name));
            string[] lacking = RequiredSystems.Where(s => !present.Contains(s)).ToArray();
            checks.Add(new Check { name = "gameplay_systems", ok = lacking.Length == 0, detail = lacking.Length == 0 ? $"all {RequiredSystems.Length}" : "missing: " + string.Join(", ", lacking) });

            // Player.
            GameObject player = all.FirstOrDefault(g => g.CompareTag("Player") && g.activeInHierarchy);
            checks.Add(new Check { name = "player_present", ok = player != null, detail = player != null ? player.name : "no active object tagged Player" });

            // NavMesh at the player.
            Vector3 start = player != null ? player.transform.position : Vector3.zero;
            bool onMesh = NavMesh.SamplePosition(start, out NavMeshHit startHit, 4f, NavMesh.AllAreas);
            checks.Add(new Check { name = "navmesh_at_player", ok = onMesh, warnOnly = true, detail = onMesh ? $"{startHit.position}" : $"no NavMesh within 4 m of {start}" });

            return checks;
        }

        private static bool Report(string scene, List<Check> checks, string runDir)
        {
            bool pass = checks.All(c => c.ok || c.warnOnly);
            var sb = new StringBuilder();
            sb.Append("{\n  \"scene\": \"").Append(scene).Append("\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"checks\": [\n");
            for (int i = 0; i < checks.Count; i++)
            {
                Check c = checks[i];
                string level = c.ok ? "ok" : c.warnOnly ? "warn" : "fail";
                sb.Append("    {\"name\": \"").Append(Esc(c.name)).Append("\", \"result\": \"").Append(level)
                  .Append("\", \"detail\": \"").Append(Esc(c.detail)).Append("\"}").Append(i < checks.Count - 1 ? ",\n" : "\n");
                string line = $"[Sanity] {level.ToUpper()} {c.name}: {c.detail}";
                if (c.ok) Debug.Log(line);
                else if (c.warnOnly) Debug.LogWarning(line);
                else Debug.LogError(line);
            }
            sb.Append("  ]\n}\n");
            Directory.CreateDirectory(runDir);
            File.WriteAllText(Path.Combine(runDir, $"sanity-{scene}.json"), sb.ToString());
            Debug.Log($"[Sanity] {scene}: {(pass ? "PASS" : "FAIL")} ({checks.Count(c => c.ok)}/{checks.Count} ok)");
            return pass;
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "/").Replace("\"", "'");
    }
}
#endif
