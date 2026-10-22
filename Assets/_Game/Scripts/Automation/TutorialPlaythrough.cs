using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Systems;
using GlorifiedBody.Tutorial;
using GlorifiedBody.UI;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The tutorial bot: loads the tutorial from the menu, then plays every step through the virtual pad
    /// (TutorialDrive), with a 1080p shot of each step's prompt before the player acts and the time each one
    /// took. Active only when BotArgs.Bot == "tutorial". Writes playthrough.json and shots/tutorial_&lt;n&gt;_&lt;id&gt;.png.
    /// Batch: tools/unity.sh play Tutorial Main_Menu tutorial 400
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class TutorialPlaythrough : MonoBehaviour
    {
        public const int ShotWidth = 1920, ShotHeight = 1080;

        [Serializable]
        private class Step
        {
            public string name;
            public bool ok;
            public string detail;
            public float t;
        }

        private readonly List<Step> steps = new List<Step>();
        private VirtualPad pad;
        private float startTime;
        private bool finished;

        public static bool IsRequested => string.Equals(BotArgs.Bot, "tutorial", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<TutorialPlaythrough>() != null) return;
            var go = new GameObject("[TutorialPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<TutorialPlaythrough>();
        }

        private void Start()
        {
            startTime = Time.unscaledTime;
            StartCoroutine(Run());
            StartCoroutine(Watchdog());
        }

        private void Update()
        {
            pad?.Commit();
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
            string line = $"[TutorialBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1f);
            GameManager gm = GameManager.Instance;
            if (gm != null) gm.LoadLevel(TutorialDirector.SceneName); else SceneManager.LoadScene(TutorialDirector.SceneName);
            float t0 = Time.realtimeSinceStartup;
            while (SceneManager.GetActiveScene().name != TutorialDirector.SceneName && Time.realtimeSinceStartup - t0 < 30f) yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSeconds(0.3f);

            TutorialDirector director = TutorialDirector.Instance;
            Record("tutorial_loaded", SceneManager.GetActiveScene().name == TutorialDirector.SceneName && director != null && director.Current != null,
                $"scene={SceneManager.GetActiveScene().name} director={director != null} steps={(director != null ? director.Steps.Count : 0)} baked={LightmapSettings.lightmaps.Length}");
            if (director == null) { Finish(); yield break; }

            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            TutorialDrive.Context context = TutorialDrive.Context.From(pad, director);
            context.Moment = moment => ShotWithOverlays(Path.Combine(BotArgs.RunDir, "shots", $"tutorial_moment_{moment}.png"));
            string shotsDir = Path.Combine(BotArgs.RunDir, "shots");

            int guard = 0;
            while (!director.IsComplete && guard++ < 40)
            {
                TutorialStep step = director.Current;
                int index = director.StepIndex;
                yield return null;
                string file = Path.Combine(shotsDir, $"tutorial_{index + 1:00}_{step.Id}.png");
                ShotWithOverlays(file);
                float stepStart = Time.realtimeSinceStartup;
                int attempts = 0;
                while (director.StepIndex == index && !director.IsComplete && Time.realtimeSinceStartup - stepStart < TutorialDrive.StepTimeout)
                {
                    attempts++;
                    yield return TutorialDrive.Drive(context, step.Id);
                    float waitUntil = Time.realtimeSinceStartup + 1.5f;
                    while (director.StepIndex == index && !director.IsComplete && Time.realtimeSinceStartup < waitUntil) yield return null;
                }
                bool done = director.StepIndex != index || director.IsComplete;
                Record($"step_{step.Id}", done, $"attempts={attempts} seconds={Time.realtimeSinceStartup - stepStart:0.0} timeScale={Time.timeScale}");
                if (!done) break;
            }
            float tEnd = Time.realtimeSinceStartup;
            while (!GameManager.Instance.IsLevelComplete && Time.realtimeSinceStartup - tEnd < 5f) yield return null;
            Record("level_complete", director.IsComplete && GameManager.Instance.IsLevelComplete, $"complete={director.IsComplete} state={GameManager.Instance.IsLevelComplete}");
            ShotWithOverlays(Path.Combine(shotsDir, "tutorial_complete.png"));

            GameInput.ClearBotSource(pad);
            Finish();
        }

        /// <summary>
        /// A 1080p shot with the prompt and the HUD in it: a camera shot skips screen-space overlay canvases,
        /// so both go to screen-space camera for the one render (as OverlayShot does), then back.
        /// </summary>
        private static void ShotWithOverlays(string path)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            var canvases = new List<Canvas>();
            TutorialPromptUI prompt = FindFirstObjectByType<TutorialPromptUI>();
            if (prompt != null && prompt.Canvas != null && prompt.Canvas.gameObject.activeInHierarchy) canvases.Add(prompt.Canvas);
            PlayerHud hud = FindFirstObjectByType<PlayerHud>();
            if (hud != null && hud.Canvas != null && hud.Canvas.gameObject.activeInHierarchy) canvases.Add(hud.Canvas);
            InteractionUI interaction = FindFirstObjectByType<InteractionUI>();
            if (interaction != null && interaction.Canvas != null && interaction.Canvas.gameObject.activeInHierarchy) canvases.Add(interaction.Canvas);
            var modes = new List<(Canvas canvas, RenderMode mode, Camera camera, float plane)>();
            foreach (Canvas canvas in canvases)
            {
                modes.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance));
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = cam.nearClipPlane + 0.05f;
            }
            Canvas.ForceUpdateCanvases();
            try
            {
                ShotRenderer.Render(cam, path, ShotWidth, ShotHeight, useTargetTexture: true);
            }
            finally
            {
                foreach (var m in modes)
                {
                    m.canvas.renderMode = m.mode;
                    m.canvas.worldCamera = m.camera;
                    m.canvas.planeDistance = m.plane;
                }
            }
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            var sb = new StringBuilder();
            sb.Append("{\n  \"bot\": \"tutorial\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
            for (int i = 0; i < steps.Count; i++)
            {
                Step s = steps[i];
                sb.Append("    {\"name\": \"").Append(Esc(s.name)).Append("\", \"ok\": ").Append(s.ok ? "true" : "false")
                  .Append(", \"t\": ").Append(s.t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(", \"detail\": \"").Append(Esc(s.detail)).Append("\"}").Append(i < steps.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            string dir = BotArgs.RunDir;
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "playthrough.json"), sb.ToString());
            }
            Debug.Log($"[TutorialBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("tutorial_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
