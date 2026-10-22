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
    /// The look bot (build order step 4): loads a look test room (LookTest, LookTest2), then for each dressing
    /// (greybox, real time, real time with the baked light, plate) holds every fixed camera and saves a 1080p
    /// shot, runs Lucy three seconds from her start (the zones cut the cameras, the plate captures again) and
    /// saves one more, and measures the frame time. Bruno picks from the pictures. Active only when BotArgs.Bot
    /// == "look". Writes playthrough.json and shots/look_&lt;dressing&gt;_&lt;camera&gt;.png to BotArgs.RunDir.
    /// Batch: tools/unity.sh play LookTest Main_Menu look 300 (or LookTest2)
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class LookPlaythrough : MonoBehaviour
    {
        public const int ShotWidth = 1920, ShotHeight = 1080;
        public const float RunSeconds = 3f;

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
        private int cuts;

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
            Record("look_loaded", SceneManager.GetActiveScene().name == level && look != null && director != null && cameras.Length >= 3 && tank != null,
                $"scene={SceneManager.GetActiveScene().name} look={look != null} director={director != null} cameras={cameras.Length} lucy={tank != null} baked={(look != null && look.HasBakedLight)}");
            if (look == null || director == null || tank == null) { Finish(); yield break; }
            Record("baked_light", look.HasBakedLight, look.HasBakedLight ? "the scene carries lightmaps" : "no lightmaps: run LookTestSetup.BakeAll");
            director.OnCameraChanged += _ => cuts++;

            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            look.PlateSizeOverride = new Vector2Int(Mathf.RoundToInt(ShotWidth * LookDirector.LookRenderScale), Mathf.RoundToInt(ShotHeight * LookDirector.LookRenderScale));
            string shotsDir = Path.Combine(BotArgs.RunDir, "shots");
            Vector3 home = look.Home;
            float homeYaw = look.HomeYaw;

            foreach (LookDressing dressing in (LookDressing[])Enum.GetValues(typeof(LookDressing)))
            {
                string tag = dressing.ToString().ToLowerInvariant();
                director.Override(null);
                tank.Teleport(home, homeYaw);
                look.SetDressing(dressing);
                yield return null;
                yield return null;
                int wantedRenderer = dressing == LookDressing.Greybox ? 0 : dressing == LookDressing.Plate ? 3 : 1;
                bool bakedOk = dressing == LookDressing.Greybox || dressing == LookDressing.RealTime ? !look.BakedLightOn : look.BakedLightOn == look.HasBakedLight;
                Record($"dressing_{tag}", look.Dressing == dressing && look.RendererIndex == wantedRenderer && bakedOk, $"renderer={look.RendererIndex} dressed={look.IsDressed} bakedOn={look.BakedLightOn}");

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

                // Three seconds of running from her start under the zone cameras: the characters move, the
                // cameras cut where the zones say, the plate captures again on each cut.
                director.Override(null);
                tank.Teleport(home, homeYaw);
                yield return null;
                yield return null;
                int cutsBefore = cuts, capturesBefore = look.PlateCaptures;
                pad.SetHeld(InputManager.GameAction.Forward, true);
                pad.SetHeld(InputManager.GameAction.Run, true);
                yield return new WaitForSeconds(RunSeconds);
                pad.ReleaseAll();
                yield return null;
                yield return null;
                string walkFile = Path.Combine(shotsDir, $"look_{tag}_walk.png");
                bool walkSaved = ShotRenderer.Render(Camera.main, walkFile, ShotWidth, ShotHeight, useTargetTexture: true);
                float ran = Vector3.Distance(home, player.transform.position);
                int newCuts = cuts - cutsBefore, newCaptures = look.PlateCaptures - capturesBefore;
                bool captured = dressing != LookDressing.Plate || newCuts == 0 || newCaptures >= newCuts;
                Record($"walk_{tag}", walkSaved && ran > 2f && captured, $"ran={ran:0.00} m camera={director.CurrentName} cuts={newCuts} plateCaptures={newCaptures}");

                // The frame time of the dressing over one second of play (the live view, no shots).
                ResetFrames();
                yield return new WaitForSecondsRealtime(1f);
                float avg = frames > 0 ? frameSum / frames : 0f;
                Record($"frames_{tag}", frames > 0, $"frames={frames} avg={avg:0.00} ms max={frameMax:0.00} ms");
            }

            // The LOOK option (Bruno's pick, 2026-10-22): the baked dressing under the default camera, Clear
            // (the stock renderer, MSAA, SMAA) then Authentic (the dither), one shot each for the pixels.
            look.SetDressing(LookDressing.RealTimeBaked);
            director.Override(director.DefaultCamera);
            tank.Teleport(home, homeYaw);
            if (SettingsManager.Instance != null)
            {
                foreach (LookQuality quality in new[] { LookQuality.Clear, LookQuality.Authentic })
                {
                    string qualityTag = quality.ToString().ToLowerInvariant();
                    SettingsManager.Instance.LookQuality = quality;
                    yield return null;
                    yield return null;
                    string qualityFile = Path.Combine(shotsDir, $"look_quality_{qualityTag}.png");
                    bool qualitySaved = ShotRenderer.Render(Camera.main, qualityFile, ShotWidth, ShotHeight, useTargetTexture: true);
                    Record($"quality_{qualityTag}", qualitySaved && look.RendererIndex == LookQualityApplier.RendererIndexFor(quality), $"saved={qualitySaved} renderer={look.RendererIndex}");
                }
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
