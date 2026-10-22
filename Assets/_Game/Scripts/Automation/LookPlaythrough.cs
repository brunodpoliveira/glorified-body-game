using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Cameras;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Player;
using GlorifiedBody.Rendering;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The look bot (build order step 4): loads the look test room, then for each dressing (greybox, real time,
    /// plate) holds every fixed camera and saves a 1080p shot, walks Lucy one step down the gallery and saves
    /// one more, and measures the frame time. Bruno picks from the pictures. Active only when BotArgs.Bot ==
    /// "look". Writes playthrough.json and shots/look_&lt;dressing&gt;_&lt;camera&gt;.png to BotArgs.RunDir.
    /// Batch: tools/unity.sh play LookTest Main_Menu look 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class LookPlaythrough : MonoBehaviour
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
        private int frames;
        private float frameSum, frameMax;

        public static bool IsRequested => string.Equals(BotArgs.Bot, "look", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<LookPlaythrough>() != null) return;
            var go = new GameObject("[LookPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<LookPlaythrough>();
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
            frames++;
            frameSum += Time.unscaledDeltaTime * 1000f;
            frameMax = Mathf.Max(frameMax, Time.unscaledDeltaTime * 1000f);
        }

        private void ResetFrames()
        {
            frames = 0;
            frameSum = 0f;
            frameMax = 0f;
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
            string line = $"[LookBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1f);
            string level = string.IsNullOrEmpty(BotArgs.Level) ? "LookTest" : BotArgs.Level;
            GameManager gm = GameManager.Instance;
            if (gm != null) gm.LoadLevel(level); else SceneManager.LoadScene(level);
            float t0 = Time.realtimeSinceStartup;
            while (SceneManager.GetActiveScene().name != level && Time.realtimeSinceStartup - t0 < 30f) yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSeconds(0.2f);

            LookDirector look = LookDirector.Instance;
            FixedCameraDirector director = FixedCameraDirector.Instance;
            FixedCamera[] cameras = FindObjectsByType<FixedCamera>(FindObjectsSortMode.None).OrderBy(c => c.cameraName).ToArray();
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            TankController tank = player != null ? player.GetComponent<TankController>() : null;
            Record("look_loaded", SceneManager.GetActiveScene().name == level && look != null && director != null && cameras.Length == 3 && tank != null,
                $"scene={SceneManager.GetActiveScene().name} look={look != null} director={director != null} cameras={cameras.Length} lucy={tank != null}");
            if (look == null || director == null || tank == null) { Finish(); yield break; }

            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            look.PlateSizeOverride = new Vector2Int(Mathf.RoundToInt(ShotWidth * LookDirector.LookRenderScale), Mathf.RoundToInt(ShotHeight * LookDirector.LookRenderScale));
            string shotsDir = Path.Combine(BotArgs.RunDir, "shots");
            Vector3 home = new Vector3(-2f, 0f, -5.5f);

            foreach (LookDressing dressing in new[] { LookDressing.Greybox, LookDressing.RealTime, LookDressing.Plate })
            {
                string tag = dressing.ToString().ToLowerInvariant();
                tank.Teleport(home, 0f);
                look.SetDressing(dressing);
                yield return null;
                yield return null;
                bool rendererOk = look.Dressing == dressing && look.RendererIndex == (dressing == LookDressing.Greybox ? 0 : dressing == LookDressing.RealTime ? 1 : 3);
                Record($"dressing_{tag}", rendererOk, $"renderer={look.RendererIndex} dressed={look.IsDressed}");

                foreach (FixedCamera fixedCamera in cameras)
                {
                    // The cut marks the plate dirty; the director's next Update captures it for the new camera.
                    director.Override(fixedCamera);
                    yield return null;
                    yield return null;
                    string file = Path.Combine(shotsDir, $"look_{tag}_{fixedCamera.cameraName.ToLowerInvariant()}.png");
                    bool saved = ShotRenderer.Render(Camera.main, file, ShotWidth, ShotHeight, useTargetTexture: true);
                    bool plateOk = dressing != LookDressing.Plate || (look.Plate != null && look.PlateCameraName == fixedCamera.cameraName);
                    Record($"shot_{tag}_{fixedCamera.cameraName.ToLowerInvariant()}", saved && plateOk, $"saved={saved} camera={director.CurrentName} plate={look.PlateCameraName}");
                }

                // One step down the gallery under the wide camera: the characters move, the plate stays.
                director.Override(cameras.First(c => c.cameraName == "Cam_Wide"));
                yield return null;
                yield return null;
                pad.SetHeld(InputManager.GameAction.Forward, true);
                yield return new WaitForSeconds(1.2f);
                pad.ReleaseAll();
                yield return null;
                string walkFile = Path.Combine(shotsDir, $"look_{tag}_walk.png");
                bool walkSaved = ShotRenderer.Render(Camera.main, walkFile, ShotWidth, ShotHeight, useTargetTexture: true);
                float walked = Vector3.Distance(home, player.transform.position);
                Record($"walk_{tag}", walkSaved && walked > 0.8f, $"walked={walked:0.00} m plateCaptures={look.PlateCaptures}");

                // The frame time of the dressing over one second of play (the live view, no shots).
                director.Override(null);
                yield return null;
                ResetFrames();
                yield return new WaitForSecondsRealtime(1f);
                float avg = frames > 0 ? frameSum / frames : 0f;
                Record($"frames_{tag}", frames > 0, $"frames={frames} avg={avg:0.00} ms max={frameMax:0.00} ms");
            }

            GameInput.ClearBotSource(pad);
            Finish();
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            var sb = new StringBuilder();
            sb.Append("{\n  \"bot\": \"look\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[LookBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("look_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
