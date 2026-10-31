using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Cameras;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The demo bot (the greybox of floors 2 and 1, 2026-10-31): loads every demo room in turn, waits for its figures,
    /// holds each fixed camera and saves a 1080p shot, so Bruno sees each room with its people in place. Lucy cannot be
    /// hurt during the tour. Active only when BotArgs.Bot == "demo". Writes playthrough.json and
    /// shots/demo_&lt;room&gt;_&lt;camera&gt;.png to BotArgs.RunDir.
    /// Batch: tools/unity.sh play Demo_W1 Main_Menu demo 900
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class DemoPlaythrough : MonoBehaviour
    {
        public const int ShotWidth = 1920, ShotHeight = 1080;
        public static readonly string[] Rooms =
        {
            "W1", "W2", "W3", "W4", "W5", "W6", "W7", "W8", "W9", "W10", "W11", "W12",
            "G1", "G2", "G3", "G4", "G5", "G6", "G7", "G8", "G9", "U1",
        };

        [Serializable]
        private class Step
        {
            public string name;
            public bool ok;
            public string detail;
            public float t;
        }

        private readonly List<Step> steps = new List<Step>();
        private float startTime;
        private bool finished;

        public static bool IsRequested => string.Equals(BotArgs.Bot, "demo", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<DemoPlaythrough>() != null) return;
            var go = new GameObject("[DemoPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<DemoPlaythrough>();
        }

        private void Start()
        {
            startTime = Time.unscaledTime;
            StartCoroutine(Run());
            StartCoroutine(Watchdog());
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
            string line = $"[DemoBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1f);
            string shotsDir = Path.Combine(BotArgs.RunDirOrData, "shots");
            foreach (string id in Rooms)
            {
                string scene = "Demo_" + id;
                GameManager.Instance.LoadLevel(scene);
                float t0 = Time.realtimeSinceStartup;
                while ((SceneManager.GetActiveScene().name != scene || !GameManager.Instance.IsPlaying) && Time.realtimeSinceStartup - t0 < 30f) yield return null;
                yield return new WaitForSecondsRealtime(0.5f);
                GameObject lucy = GameObject.FindGameObjectWithTag("Player");
                Health health = lucy != null ? lucy.GetComponentInChildren<Health>() : null;
                health?.SetInvulnerable(true);
                FixedCameraDirector director = FixedCameraDirector.Instance;
                FixedCamera[] cameras = FindObjectsByType<FixedCamera>(FindObjectsSortMode.None).OrderBy(c => c.cameraName).ToArray();
                int figures = FindObjectsByType<FeedableBody>(FindObjectsSortMode.None).Length;
                Record($"room_{id}", SceneManager.GetActiveScene().name == scene && lucy != null && director != null && cameras.Length > 0 && EnemySpawn.Missing == 0,
                    $"scene={SceneManager.GetActiveScene().name} cameras={cameras.Length} figures={figures} missing={EnemySpawn.Missing}");
                if (director == null) continue;
                foreach (FixedCamera fixedCamera in cameras)
                {
                    director.Override(fixedCamera);
                    yield return null;
                    yield return null;
                    string file = Path.Combine(shotsDir, $"demo_{fixedCamera.cameraName.ToLowerInvariant()}.png");
                    bool saved = ShotRenderer.Render(Camera.main, file, ShotWidth, ShotHeight, useTargetTexture: true);
                    Record($"shot_{fixedCamera.cameraName.ToLowerInvariant()}", saved, $"saved={saved}");
                    if (BotArgs.ShotWait > 0f) yield return new WaitForSecondsRealtime(BotArgs.ShotWait); // a browser driver takes the screen now
                }
                director.Override(null);
            }
            GameManager.Instance.LoadMainMenu();
            Finish();
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            var sb = new StringBuilder();
            sb.Append("{\n  \"bot\": \"demo\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
            for (int i = 0; i < steps.Count; i++)
            {
                Step s = steps[i];
                sb.Append("    {\"name\": \"").Append(Esc(s.name)).Append("\", \"ok\": ").Append(s.ok ? "true" : "false")
                  .Append(", \"t\": ").Append(s.t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(", \"detail\": \"").Append(Esc(s.detail)).Append("\"}").Append(i < steps.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            string dir = BotArgs.RunDirOrData;
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "playthrough.json"), sb.ToString());
            }
            Debug.Log($"[DemoBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            // A player run (the Linux player, the Windows player under Wine) ends here; the editor runner and the web page stop it themselves.
            if (!Application.isEditor && Application.platform != RuntimePlatform.WebGLPlayer) Application.Quit(pass ? 0 : 1);
            RunTelemetry.Event("demo_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
