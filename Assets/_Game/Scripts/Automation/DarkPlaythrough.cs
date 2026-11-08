using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Cameras;
using GlorifiedBody.Interaction;
using GlorifiedBody.Rooms;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;
using GlorifiedBody.Subtitles;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The dark bot (2026-11-08): in DARK TEST she walks into the office and the lights go out, the release is dead, she
    /// starts a unit out of order and the alarm trips, then the units in order, the monitors come back, the release works.
    /// Shots: shots/dark_&lt;n&gt;_&lt;moment&gt;.png, most from the office's own camera. Active only when
    /// BotArgs.Bot == "dark": tools/unity.sh play DarkTest Main_Menu dark 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class DarkPlaythrough : MonoBehaviour
    {
        [Serializable]
        private class Step
        {
            public string name;
            public bool ok;
            public string detail;
            public float t;
        }

        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad Pad;
            private void Update() => Pad?.Commit();
        }

        private readonly List<Step> steps = new List<Step>();
        private float startTime;
        private bool finished;
        private int shotIndex;
        private VirtualPad pad;
        private PadDriver padDriver;

        public static bool IsRequested => string.Equals(BotArgs.Bot, "dark", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<DarkPlaythrough>() != null) return;
            var go = new GameObject("[DarkPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<DarkPlaythrough>();
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
            string line = $"[DarkBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            Directory.CreateDirectory(Path.Combine(BotArgs.RunDir, "shots"));
            GameManager gm = GameManager.Instance;
            gm.LoadLevel("DarkTest");
            float t0 = BotClock.Now;
            while ((SceneManager.GetActiveScene().name != "DarkTest" || !gm.IsPlaying) && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(0.5f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            padDriver = new GameObject("[DarkPad]").AddComponent<PadDriver>();
            DontDestroyOnLoad(padDriver.gameObject);
            padDriver.Pad = pad;
            tank = lucy.GetComponent<TankController>();
            interactor = lucy.GetComponent<Interactor>();
            DarkRoom dark = FindFirstObjectByType<DarkRoom>();
            UpsBank bank = FindFirstObjectByType<UpsBank>();
            FlagSwitch release = GameObject.Find("Release_Button")?.GetComponent<FlagSwitch>();
            Room office = Room.Of("Q2"), closet = Room.Of("Q3");
            if (dark == null || bank == null || release == null || office == null) { Record("pieces_in_room", false, "no dark room, bank or release (DarkTestSetup.BuildDarkTest)"); Finish(); yield break; }
            FixedCamera officeCam = office.GetComponentsInChildren<FixedCamera>().First();
            FixedCamera closetCam = closet.GetComponentsInChildren<FixedCamera>().First();

            // 1. Before she enters: the office is lit and the monitor wall runs.
            yield return Shot("office_lit", officeCam);
            Record("lit_before", !dark.IsDark && bank.MonitorsOn == 6, $"monitors={bank.MonitorsOn}");

            // 2. She walks in: the lights go out, the monitors go black, the units beep.
            tank.Teleport(office.Centre + new Vector3(-1.5f, 0f, -1.5f), 30f);
            t0 = BotClock.Now;
            while ((!dark.IsDark || !dark.HasInside(lucy)) && BotClock.Now - t0 < 3f) yield return null;
            yield return BotClock.Wait(0.3f);
            yield return Shot("office_dark", officeCam);
            Record("the_lights_go_out", dark.IsDark && bank.Beeping && bank.MonitorsOn == 0, $"state={bank.Current}");

            // 3. The release is dead: WAITING FOR NET.
            yield return Face(release.Point + Vector3.left * 1.0f, release.Point);
            yield return Press(InputManager.GameAction.Interact);
            yield return Shot("release_waits", officeCam);
            Record("the_release_waits", interactor.Text == "WAITING FOR NET.", interactor.Text);
            yield return BotClock.Wait(2.2f);

            // 4. ACCESS first: the alarm trips (the bot shortens the 5 s).
            bank.resetSeconds = 2f;
            yield return Unit(bank, "ACCESS");
            yield return Shot("alarm", closetCam);
            Record("access_first_trips_the_alarm", bank.Current == UpsBank.State.Alarm && bank.Alarms == 1, $"state={bank.Current}");
            t0 = BotClock.Now;
            while (!bank.Beeping && BotClock.Now - t0 < 5f) yield return null;

            // 5. NET, REC, ACCESS: the monitors come back, one by one.
            yield return Unit(bank, "NET");
            yield return Unit(bank, "REC");
            yield return Shot("two_units_up", closetCam);
            yield return Unit(bank, "ACCESS");
            yield return BotClock.Wait(bank.monitorGap * 2.5f);
            yield return Shot("monitors_coming_back", officeCam);
            t0 = BotClock.Now;
            while (bank.MonitorsOn < 6 && BotClock.Now - t0 < 6f) yield return null;
            yield return BotClock.Wait(0.3f);
            tank.Teleport(office.Centre, 0f);
            yield return BotClock.Wait(0.3f);
            yield return Shot("monitors_light_the_dark", officeCam);
            Record("the_units_in_order", bank.Done && bank.MonitorsOn == 6 && dark.IsDark, $"monitors={bank.MonitorsOn}");

            // 5b. The room lights come back after the monitors (Bruno, 2026-11-08).
            t0 = BotClock.Now;
            while (dark.IsDark && BotClock.Now - t0 < bank.lightsBackSeconds + 3f) yield return null;
            yield return BotClock.Wait(0.3f);
            yield return Shot("the_lights_come_back", officeCam);
            Record("the_lights_come_back", !dark.IsDark, $"dark={dark.IsDark}");

            // 6. The release works now.
            yield return Face(release.Point + Vector3.left * 1.0f, release.Point);
            yield return Press(InputManager.GameAction.Interact);
            yield return Press(InputManager.GameAction.Interact);
            Record("the_release_works", Saving.StoryFlags.Has(release.flag), interactor.LastResult);
            Finish();
        }

        private IEnumerator Unit(UpsBank bank, string label)
        {
            UpsUnit unit = bank.units.First(u => u.label == label);
            yield return Face(unit.Point, unit.transform.position);
            yield return Press(InputManager.GameAction.Interact);
        }

        private IEnumerator Shot(string moment, FixedCamera pose)
        {
            Camera main = Camera.main;
            var obj = new GameObject("[ShotCamera]");
            Camera cam = obj.AddComponent<Camera>();
            if (main != null) cam.CopyFrom(main);
            cam.fieldOfView = pose.fieldOfView;
            obj.transform.SetPositionAndRotation(pose.transform.position, pose.transform.rotation);
            cam.enabled = false;
            yield return Shot(moment, cam);
            Destroy(obj);
        }

        private TankController tank;
        private Interactor interactor;

        private IEnumerator Face(Vector3 point, Vector3 target)
        {
            point.y = 0f;
            Vector3 to = target - point;
            tank.Teleport(point, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            yield return null;
            yield return null;
        }

        private IEnumerator Press(InputManager.GameAction action)
        {
            pad.Tap(action);
            yield return null;
            yield return null;
            yield return null;
        }

        /// <summary>The same through a camera of its own at the place, looking at the point (a close look at a screen).</summary>
        private IEnumerator Shot(string moment, Vector3 from, Vector3 at)
        {
            Camera main = Camera.main;
            var obj = new GameObject("[ShotCamera]");
            Camera cam = obj.AddComponent<Camera>();
            if (main != null) cam.CopyFrom(main);
            cam.fieldOfView = 50f;
            obj.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
            cam.enabled = false;
            yield return Shot(moment, cam);
            Destroy(obj);
        }

        private IEnumerator Shot(string moment, Camera own = null)
        {
            shotIndex++;
            yield return null; // not WaitForEndOfFrame: it never comes in a batch run
            Camera cam = own != null ? own : Camera.main;
            if (cam == null) yield break;
            // Every overlay on screen: the HUD, the subtitles, the Interact message and hint, the keypad.
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas && c.isActiveAndEnabled && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
            var old = canvases.Select(c => (c.renderMode, c.worldCamera, c.planeDistance)).ToList();
            foreach (Canvas c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + 0.05f;
            }
            Canvas.ForceUpdateCanvases();
            try
            {
                ShotRenderer.Render(cam, Path.Combine(BotArgs.RunDir, "shots", $"dark_{shotIndex:00}_{moment}.png"), useTargetTexture: true);
            }
            finally
            {
                for (int i = 0; i < canvases.Count; i++)
                {
                    canvases[i].renderMode = old[i].renderMode;
                    canvases[i].worldCamera = old[i].worldCamera;
                    canvases[i].planeDistance = old[i].planeDistance;
                }
            }
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            if (padDriver != null) { GameInput.ClearBotSource(padDriver.Pad); Destroy(padDriver.gameObject); }
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            var sb = new StringBuilder();
            sb.Append("{\n  \"bot\": \"dark\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[DarkBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("dark_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
