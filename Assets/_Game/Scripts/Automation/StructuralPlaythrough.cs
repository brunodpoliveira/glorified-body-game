using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// Structural bot: proves the menu loop is wired. It does not play well, it calls the game's own
    /// methods. Build order step 1: main menu, start, training room ready, pause round trip, forced death,
    /// death screen, restart, back to the main menu. Later steps add the room and the mechanics.
    /// Active only when BotArgs.Bot == "structural". Writes playthrough.json to BotArgs.RunDir.
    /// </summary>
    public class StructuralPlaythrough : MonoBehaviour
    {
        [Serializable]
        private class Step
        {
            public string name;
            public bool ok;
            public string detail;
            public float t;
        }

        private readonly List<Step> steps = new List<Step>();
        private readonly List<string> eventLog = new List<string>();
        private float startTime;
        private float maxFrameMs;
        private float measureFrom = float.MaxValue; // frame times count only after the scene load settles
        private bool finished;

        public static bool IsRequested => string.Equals(BotArgs.Bot, "structural", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<StructuralPlaythrough>() != null) return;
            var go = new GameObject("[StructuralPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<StructuralPlaythrough>();
        }

        private void OnEnable() => RunTelemetry.OnEvent += OnTelemetry;
        private void OnDisable() => RunTelemetry.OnEvent -= OnTelemetry;

        private void OnTelemetry(string name, string detail)
        {
            if (name == "frame_stats") return;
            eventLog.Add(name + (string.IsNullOrEmpty(detail) ? string.Empty : ":" + detail));
        }

        private void Start()
        {
            startTime = Time.unscaledTime;
            StartCoroutine(Run());
            StartCoroutine(Watchdog());
        }

        private void Update()
        {
            if (Time.unscaledTime >= measureFrom) maxFrameMs = Mathf.Max(maxFrameMs, Time.unscaledDeltaTime * 1000f);
        }

        private IEnumerator Watchdog()
        {
            float limit = BotArgs.TimeoutSeconds;
            while (!finished && Time.unscaledTime - startTime < limit) yield return null;
            if (!finished)
            {
                Record("timeout", false, $"bot did not finish in {limit:0}s");
                Finish();
            }
        }

        private void Record(string name, bool ok, string detail = "")
        {
            steps.Add(new Step { name = name, ok = ok, detail = detail, t = Time.unscaledTime - startTime });
            string line = $"[Bot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return WaitReal(1f);
            string level = string.IsNullOrEmpty(BotArgs.Level) ? "TrainingRoom" : BotArgs.Level;
            GameManager gm = GameManager.Instance;

            // 1. Main menu, then start.
            if (SceneManager.GetActiveScene().name == "Main_Menu")
            {
                bool menuUi = FindFirstObjectByType<MainMenuUI>() != null;
                Record("menu_loaded", gm != null && menuUi, $"GameManager={gm != null} MainMenuUI={menuUi}");
                if (gm != null) gm.LoadLevel(level);
                else SceneManager.LoadScene(level);
                yield return WaitForScene(level, 30f);
                Record("start", SceneManager.GetActiveScene().name == level, level);
                if (SceneManager.GetActiveScene().name != level) { Finish(); yield break; }
            }
            else
            {
                Record("start", true, "started in " + SceneManager.GetActiveScene().name);
            }

            // 2. Gameplay ready: the state is Playing and the player exists with a Health.
            gm = GameManager.Instance;
            yield return WaitUntilReal(() => gm != null && gm.IsPlaying, 5f);
            GameObject player = FindPlayer(out Health playerHealth);
            bool ready = gm != null && gm.IsPlaying && player != null && playerHealth != null;
            Record("gameplay_ready", ready, $"state={gm?.CurrentState} player={(player != null ? player.name : "none")} health={playerHealth != null}");
            if (!ready) { Finish(); yield break; }
            measureFrom = Time.unscaledTime + 1f;
            RunTelemetry.Event("bot_assist", "on");
            yield return WaitReal(0.5f);
            CaptureShot("play_training_room");

            // 3. Pause round trip: the pause menu shows on Pause() and hides on Resume().
            gm.Pause();
            yield return null;
            yield return null;
            GameObject pauseCanvas = GameObject.Find("PauseMenuUICanvas");
            bool pauseShown = gm.IsPaused && pauseCanvas != null && pauseCanvas.activeSelf;
            if (pauseShown) yield return OverlayShot.Capture(pauseCanvas.GetComponent<Canvas>(), "pause_ui.png", 0.2f);
            gm.Resume();
            yield return null;
            yield return null;
            bool pauseHidden = gm.IsPlaying && (pauseCanvas == null || !pauseCanvas.activeSelf);
            Record("pause_roundtrip", pauseShown && pauseHidden, $"shown={pauseShown} hidden={pauseHidden} timeScale={Time.timeScale:0.00}");

            // 3b. Options, Controls tab: the remap rows, one per GameAction. Saved as options_controls.png.
            OptionsMenuUI options = FindFirstObjectByType<OptionsMenuUI>();
            bool optionsShown = false;
            int remapRows = 0;
            if (options != null)
            {
                options.Show();
                yield return null;
                var tabField = typeof(OptionsMenuUI).GetField("controlsTabBtn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var controlsTab = tabField?.GetValue(options) as UnityEngine.UI.Button;
                controlsTab?.onClick.Invoke();
                yield return null;
                yield return null;
                GameObject optionsCanvas = GameObject.Find("OptionsMenuCanvas");
                optionsShown = options.IsVisible && optionsCanvas != null;
                if (optionsShown)
                {
                    remapRows = InputManager.Instance != null ? InputManager.Instance.GetAllBindings().Count : 0;
                    yield return OverlayShot.Capture(optionsCanvas.GetComponent<Canvas>(), "options_controls.png", 0.2f);
                }
                options.Hide();
                yield return null;
            }
            Record("options_controls", optionsShown && remapRows == 13, $"shown={optionsShown} remapRows={remapRows}");

            // 4. Forced death: lethal damage through Health, then the game-over state.
            playerHealth.SetSecondWindEnabled(false);
            playerHealth.SetInvulnerable(false);
            playerHealth.SetRuntimeInvulnerable(false);
            playerHealth.TakeDamage(playerHealth.MaxHealth * 10f);
            yield return WaitUntilReal(() => gm.IsGameOver, 8f);
            Record("force_death", gm.IsGameOver && playerHealth.IsDead, $"dead={playerHealth.IsDead} state={gm.CurrentState} timeScale={Time.timeScale:0.00}");

            // 5. Death screen: the overlay is visible. DeathMenuUI saves death_ui.png itself.
            GameObject deathCanvas = null;
            yield return WaitUntilReal(() => (deathCanvas = GameObject.Find("DeathMenuUICanvas")) != null && deathCanvas.activeSelf, 3f);
            bool deathShown = deathCanvas != null && deathCanvas.activeSelf;
            Record("death_screen", deathShown, deathShown ? "DeathMenuUICanvas active" : "DeathMenuUICanvas not visible");
            yield return WaitReal(0.6f);

            // 6. Restart: no checkpoint in step 1, so the scene reloads and the player is alive again.
            if (!gm.RestartFromLatestCheckpoint()) gm.RestartLevel();
            yield return WaitUntilReal(() => gm.IsPlaying && SceneManager.GetActiveScene().name == level, 10f);
            yield return WaitReal(0.5f);
            player = FindPlayer(out playerHealth);
            bool restarted = gm.IsPlaying && player != null && playerHealth != null && !playerHealth.IsDead;
            Record("restart", restarted, $"state={gm.CurrentState} dead={(playerHealth != null ? playerHealth.IsDead.ToString() : "no health")} timeScale={Time.timeScale:0.00}");

            // 7. Back to the main menu with a clean time scale.
            gm.LoadMainMenu();
            yield return WaitUntilReal(() => SceneManager.GetActiveScene().name == "Main_Menu" && gm.IsInMenu, 10f);
            bool backInMenu = SceneManager.GetActiveScene().name == "Main_Menu" && gm.IsInMenu && Mathf.Approximately(Time.timeScale, 1f);
            Record("main_menu_return", backInMenu, $"scene={SceneManager.GetActiveScene().name} state={gm.CurrentState} timeScale={Time.timeScale:0.00}");

            Finish();
        }

        private static GameObject FindPlayer(out Health health)
        {
            health = null;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                health = player.GetComponentInParent<Health>() ?? player.GetComponentInChildren<Health>();
            }
            return player;
        }

        private static IEnumerator WaitForScene(string sceneName, float timeout)
        {
            float t0 = Time.unscaledTime;
            while (SceneManager.GetActiveScene().name != sceneName && Time.unscaledTime - t0 < timeout) yield return null;
        }

        private static IEnumerator WaitUntilReal(Func<bool> condition, float timeout)
        {
            float t0 = Time.unscaledTime;
            while (!condition() && Time.unscaledTime - t0 < timeout) yield return null;
        }

        private void CaptureShot(string shotName)
        {
            string dir = BotArgs.RunDir;
            if (string.IsNullOrEmpty(dir) || Camera.main == null) return;
            try
            {
                ShotRenderer.Render(Camera.main, Path.Combine(dir, "shots", SafeName(shotName) + ".png"));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Bot] shot failed: " + ex.Message);
            }
        }

        private static string SafeName(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            RunTelemetry.Event("bot_assist", "off");
            RunTelemetry.Event("bot_done", pass ? "pass" : "fail");
            string dir = BotArgs.RunDir;
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                var sb = new StringBuilder();
                sb.Append("{\n  \"bot\": \"structural\",\n  \"pass\": ").Append(pass ? "true" : "false")
                  .Append(",\n  \"scene\": \"").Append(SceneManager.GetActiveScene().name)
                  .Append("\",\n  \"duration_s\": ").Append((Time.unscaledTime - startTime).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(",\n  \"max_frame_ms\": ").Append(maxFrameMs.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(",\n  \"steps\": [\n");
                for (int i = 0; i < steps.Count; i++)
                {
                    Step s = steps[i];
                    sb.Append("    ").Append(JsonUtility.ToJson(s)).Append(i < steps.Count - 1 ? ",\n" : "\n");
                }
                sb.Append("  ],\n  \"events\": [");
                sb.Append(string.Join(", ", eventLog.Select(e => "\"" + e.Replace("\\", "/").Replace("\"", "'") + "\"")));
                sb.Append("]\n}\n");
                File.WriteAllText(Path.Combine(dir, "playthrough.json"), sb.ToString());
            }
            Debug.Log($"[Bot] finished: {(pass ? "PASS" : "FAIL")} ({steps.Count(s => s.ok)}/{steps.Count} steps ok)");
#if !UNITY_EDITOR
            Application.Quit(pass ? 0 : 1);
#endif
        }

        private static IEnumerator WaitReal(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }
    }
}
