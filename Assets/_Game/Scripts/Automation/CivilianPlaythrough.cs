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
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The civilian bot (Bruno, 2026-10-25: "another test room to test the unarmed civvies, then put in code for the
    /// game over screen"): loads the civilian test room from the menu and shoots the moments: the office at work,
    /// the civilians running from her, a civilian cowering, a civilian begging at her feet, the nurse cornered in the
    /// alcove, the guard called by the scream. Then the guard kills her, and the death screen shows his entry
    /// (death_ui.png). Active only when BotArgs.Bot == "civilians". Writes playthrough.json and
    /// shots/civ_&lt;n&gt;_&lt;moment&gt;.png. Batch: tools/unity.sh play CivilianTest Main_Menu civilians 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class CivilianPlaythrough : MonoBehaviour
    {
        public const string SceneName = "CivilianTest";
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
        private float startTime;
        private bool finished;

        public static bool IsRequested => string.Equals(BotArgs.Bot, "civilians", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<CivilianPlaythrough>() != null) return;
            var go = new GameObject("[CivilianPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<CivilianPlaythrough>();
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
            string line = $"[CivilianBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            GameManager gm = GameManager.Instance;
            if (gm != null) gm.LoadLevel(SceneName); else SceneManager.LoadScene(SceneName);
            float t0 = BotClock.Now;
            while (SceneManager.GetActiveScene().name != SceneName && BotClock.Now - t0 < 30f) yield return null;
            string shots = Path.Combine(BotArgs.RunDir, "shots");
            Directory.CreateDirectory(shots);

            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            CivilianBrain[] civilians = FindObjectsByType<CivilianBrain>(FindObjectsSortMode.None).OrderBy(c => c.name).ToArray();
            SoldierBrain guard = FindFirstObjectByType<SoldierBrain>();
            Record("civ_loaded", SceneManager.GetActiveScene().name == SceneName && lucy != null && civilians.Length == 3 && guard != null,
                $"civilians={civilians.Length} guard={(guard != null ? guard.name : "none")}");
            if (lucy == null || civilians.Length == 0 || guard == null) { Finish(); yield break; }
            Health health = lucy.GetComponentInChildren<Health>();
            health.SetInvulnerable(true);
            TankController tank = lucy.GetComponent<TankController>();
            Shot(Path.Combine(shots, "civ_01_office.png"));

            // They see her and run.
            CivilianBrain lab = civilians.First(c => c.name == "Lab_A");
            t0 = BotClock.Now;
            while (!civilians.Any(c => c.Current == CivilianBrain.Phase.Flee) && BotClock.Now - t0 < 8f) yield return null;
            yield return BotClock.Wait(0.6f);
            Shot(Path.Combine(shots, "civ_02_they_run.png"));
            Record("civ_run", civilians.Count(c => c.Alerted) >= 2, string.Join(" ", civilians.Select(c => $"{c.name}:{c.Current}/{c.AlertReason}")));

            t0 = BotClock.Now;
            while (lab.Current != CivilianBrain.Phase.Cower && BotClock.Now - t0 < 12f) yield return null;
            yield return BotClock.Wait(0.5f);
            Vector3 chest = lab.transform.position + Vector3.up * 0.9f;
            CloseShot(chest, ToMiddle(lab.transform.position), Path.Combine(shots, "civ_03_cower.png"), 2.4f, 0.4f);
            Record("civ_cower", lab.Current == CivilianBrain.Phase.Cower, $"{lab.name} {lab.Current} at {lab.transform.position.x:0.0},{lab.transform.position.z:0.0}");

            // The guard heard the scream.
            t0 = BotClock.Now;
            while (!guard.Alerted && BotClock.Now - t0 < 6f) yield return null;
            Record("guard_called", guard.Alerted, $"{guard.name}: {guard.AlertReason}");

            // She walks up: it begs.
            tank.Teleport(lab.transform.position + ToMiddle(lab.transform.position) * 1.6f, 0f);
            t0 = BotClock.Now;
            while (lab.Current != CivilianBrain.Phase.Beg && BotClock.Now - t0 < 3f) yield return null;
            yield return BotClock.Wait(0.4f);
            chest = lab.transform.position + Vector3.up * 0.9f;
            CloseShot(chest, Quaternion.Euler(0f, 60f, 0f) * ToMiddle(lab.transform.position), Path.Combine(shots, "civ_04_beg.png"), 2.6f, 0.6f);
            Record("civ_beg", lab.Current == CivilianBrain.Phase.Beg && lab.Begs > 0, $"{lab.name} {lab.Current} begs={lab.Begs}");

            // The nurse in the alcove: she stands at its mouth, and it has nowhere to run.
            CivilianBrain nurse = civilians.First(c => c.name == "Nurse_C");
            tank.Teleport(new Vector3(5.4f, 0f, 2.1f), 0f);
            yield return BotClock.Wait(1.5f);
            CloseShot(nurse.transform.position + Vector3.up * 0.9f, Vector3.back + Vector3.left * 0.6f, Path.Combine(shots, "civ_05_cornered.png"), 2.4f, 0.6f);
            Record("civ_cornered", nurse.Current == CivilianBrain.Phase.Beg || nurse.Current == CivilianBrain.Phase.Cower, $"{nurse.name} {nurse.Current} begs={nurse.Begs}");

            // The guard kills her: the death screen shows his entry.
            tank.Teleport(new Vector3(2f, 0f, -4f), 90f);
            health.SetInvulnerable(false);
            health.SetSecondWindEnabled(false);
            health.SetHealth(5f);
            t0 = BotClock.Now;
            while (GameManager.Instance != null && !GameManager.Instance.IsGameOver && BotClock.Now - t0 < 40f) yield return null;
            yield return new WaitForSecondsRealtime(1.2f); // the screen's own shot (death_ui.png) takes 0.3 s
            DeathMenuUI screen = FindFirstObjectByType<DeathMenuUI>();
            bool shown = screen != null && screen.Shown != null;
            Record("death_screen", GameManager.Instance != null && GameManager.Instance.IsGameOver && shown && DeathMenuUI.LastKillerId == "guard_pistol",
                $"killer={DeathMenuUI.LastKillerId} entry={(shown ? screen.Shown.killerId : "none")} caption='{(screen != null ? screen.CaptionShown : "")}'");
            Finish();
        }

        private static Vector3 ToMiddle(Vector3 from)
        {
            Vector3 d = -from;
            d.y = 0f;
            return d.sqrMagnitude > 0.01f ? d.normalized : Vector3.back;
        }

        /// <summary>A 1080p shot of the room camera with the HUD drawn through it.</summary>
        private static void Shot(string path)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            PlayerHud hud = FindFirstObjectByType<PlayerHud>();
            Canvas canvas = hud != null ? hud.Canvas : null;
            RenderMode mode = RenderMode.ScreenSpaceOverlay;
            Camera worldCamera = null;
            float plane = 1f;
            if (canvas != null)
            {
                mode = canvas.renderMode;
                worldCamera = canvas.worldCamera;
                plane = canvas.planeDistance;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = cam.nearClipPlane + 0.05f;
                Canvas.ForceUpdateCanvases();
            }
            try
            {
                ShotRenderer.Render(cam, path, ShotWidth, ShotHeight, useTargetTexture: true);
            }
            finally
            {
                if (canvas != null)
                {
                    canvas.renderMode = mode;
                    canvas.worldCamera = worldCamera;
                    canvas.planeDistance = plane;
                }
            }
        }

        /// <summary>A close shot from a temporary camera: it stands back from the point and looks at it.</summary>
        private static void CloseShot(Vector3 point, Vector3 facing, string path, float distance = 1.4f, float height = 0.25f)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
            var go = new GameObject("[CloseShotCamera]");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.05f;
            Camera main = Camera.main;
            if (main != null)
            {
                cam.clearFlags = main.clearFlags;
                cam.backgroundColor = main.backgroundColor;
            }
            go.transform.position = point + facing.normalized * distance + Vector3.up * height;
            go.transform.LookAt(point);
            try
            {
                ShotRenderer.Render(cam, path, ShotWidth, ShotHeight, useTargetTexture: true);
            }
            finally
            {
                Destroy(go);
            }
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            var sb = new StringBuilder();
            sb.Append("{\n  \"bot\": \"civilians\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[CivilianBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("civilians_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
