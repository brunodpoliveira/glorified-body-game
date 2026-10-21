#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GlorifiedBody.Automation;

namespace GlorifiedBody.Editor.Automation
{
    /// <summary>
    /// Opens a start scene, enters Play mode with a bot, waits for playthrough.json, then stops.
    /// Batch entry (tools/unity.sh play): -executeMethod GlorifiedBody.Editor.Automation.PlaythroughRunner.Run
    ///   -gbRunDir DIR -gbScene Main_Menu -gbLevel Gameplay -gbBot structural -gbTimeout 900
    /// Batch mode must run WITHOUT -quit, so the editor keeps ticking in Play mode. The runner calls
    /// EditorApplication.Exit(0 pass / 1 fail / 2 timeout) when the bot is done.
    /// </summary>
    [InitializeOnLoad]
    public static class PlaythroughRunner
    {
        private const string KeyActive = "GlorifiedBody.Runner.Active";
        private const string KeyExit = "GlorifiedBody.Runner.ExitWhenDone";
        private const string KeyStart = "GlorifiedBody.Runner.Start";

        static PlaythroughRunner()
        {
            if (SessionState.GetBool(KeyActive, false)) EditorApplication.update += Poll;
        }

        public static void Run()
        {
            string runDir = AutomationArgs.Get("-gbRunDir");
            if (string.IsNullOrEmpty(runDir))
            {
                Debug.LogError("[PlaythroughRunner] -gbRunDir is required.");
                EditorApplication.Exit(3);
                return;
            }
            Begin(runDir,
                AutomationArgs.Get("-gbScene", "Main_Menu"),
                AutomationArgs.Get("-gbLevel", "Gameplay"),
                AutomationArgs.Get("-gbBot", "structural"),
                AutomationArgs.Get("-gbTimeout", "900"),
                exitWhenDone: true,
                record: AutomationArgs.Get("-gbRecord", "0") == "1");
        }

        public static void Begin(string runDir, string startScene, string level, string bot, string timeout, bool exitWhenDone, bool record = false)
        {
            Directory.CreateDirectory(runDir);
            string result = Path.Combine(runDir, "playthrough.json");
            if (File.Exists(result)) File.Delete(result);

            SessionState.SetString(BotArgs.KeyRunDir, runDir);
            SessionState.SetString(BotArgs.KeyBot, bot);
            SessionState.SetString(BotArgs.KeyLevel, level);
            SessionState.SetString(BotArgs.KeyTimeout, timeout);
            SessionState.SetString(BotArgs.KeyRecord, record ? "1" : "0");
            SessionState.SetBool(KeyActive, true);
            SessionState.SetBool(KeyExit, exitWhenDone);
            SessionState.SetFloat(KeyStart, (float)EditorApplication.timeSinceStartup);

            string scenePath = AutomationArgs.ScenePath(startScene);
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Debug.Log($"[PlaythroughRunner] start scene={scenePath} level={level} bot={bot} runDir={runDir}");
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            EditorApplication.isPlaying = true;
        }

        private static void Poll()
        {
            string runDir = SessionState.GetString(BotArgs.KeyRunDir, string.Empty);
            float.TryParse(SessionState.GetString(BotArgs.KeyTimeout, "900"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float timeout);
            if (timeout <= 0f) timeout = 900f;
            float elapsed = (float)EditorApplication.timeSinceStartup - SessionState.GetFloat(KeyStart, 0f);
            string result = Path.Combine(runDir, "playthrough.json");

            if (File.Exists(result))
            {
                bool pass = File.ReadAllText(result).Contains("\"pass\": true");
                Done(pass ? 0 : 1, pass ? "PASS" : "FAIL");
            }
            else if (elapsed > timeout + 60f)
            {
                Debug.LogError($"[PlaythroughRunner] timeout after {elapsed:0}s with no playthrough.json");
                Done(2, "TIMEOUT");
            }
        }

        private static void Done(int code, string label)
        {
            EditorApplication.update -= Poll;
            bool exit = SessionState.GetBool(KeyExit, false);
            foreach (string k in new[] { KeyActive, KeyExit, KeyStart })
            {
                SessionState.EraseBool(k);
                SessionState.EraseFloat(k);
            }
            foreach (string k in new[] { BotArgs.KeyRunDir, BotArgs.KeyBot, BotArgs.KeyLevel, BotArgs.KeyTimeout, BotArgs.KeyRecord })
            {
                SessionState.EraseString(k);
            }
            Debug.Log($"[PlaythroughRunner] done: {label}");
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            if (exit) EditorApplication.Exit(code);
        }
    }
}
#endif
