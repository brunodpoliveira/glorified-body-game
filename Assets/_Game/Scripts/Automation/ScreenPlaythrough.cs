using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Audio;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Subtitles;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The in-world screens bot (2026-10-31): in SCREEN TEST she walks in and the wall of CRTs plays, it ends, she turns
    /// on the CRT on the cart. Shots: shots/screens_&lt;n&gt;_&lt;moment&gt;.png (the room with the subtitles over it). Active only
    /// when BotArgs.Bot == "screens": tools/unity.sh play ScreenTest Main_Menu screens 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class ScreenPlaythrough : MonoBehaviour
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

        public static bool IsRequested => string.Equals(BotArgs.Bot, "screens", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<ScreenPlaythrough>() != null) return;
            var go = new GameObject("[ScreenPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<ScreenPlaythrough>();
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
            string line = $"[ScreenBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            Directory.CreateDirectory(Path.Combine(BotArgs.RunDir, "shots"));
            GameManager gm = GameManager.Instance;
            gm.LoadLevel("ScreenTest");
            float t0 = BotClock.Now;
            while ((SceneManager.GetActiveScene().name != "ScreenTest" || !gm.IsPlaying) && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(0.5f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            padDriver = new GameObject("[ScreenPad]").AddComponent<PadDriver>();
            DontDestroyOnLoad(padDriver.gameObject);
            padDriver.Pad = pad;
            InWorldScreen wall = GameObject.Find("Screen_CrtWall")?.GetComponent<InWorldScreen>();
            InWorldScreen cart = GameObject.Find("Screen_Cart")?.GetComponent<InWorldScreen>();
            if (wall == null || cart == null) { Record("screens_in_room", false, "no Screen_CrtWall or Screen_Cart (ScreenTestSetup.BuildScreenTest)"); Finish(); yield break; }

            // 1. She walks in: the wall of CRTs plays the clip with its line.
            pad.SetHeld(InputManager.GameAction.Forward, true);
            t0 = BotClock.Now;
            while (!wall.IsPlaying && BotClock.Now - t0 < 6f) yield return null;
            yield return BotClock.Wait(0.4f);
            pad.SetHeld(InputManager.GameAction.Forward, false);
            yield return BotClock.Wait(1.6f);
            bool line = SubtitleService.Lines.Any(l => l.Owner == wall.SubtitleOwner);
            yield return Shot("wall_playing");
            yield return Shot("wall_close", new Vector3(-0.6f, 1.5f, 13.6f), new Vector3(-2.2f, 1.1f, 17f));
            Record("the_wall_plays_when_she_walks_in", wall.IsPlaying && wall.ShowsClip && wall.Frame > 5 && line, $"frame={wall.Frame} line={line}");

            // 2. It plays to its end, and the faces go back to the cursor.
            wall.Seek(wall.Length - 0.6f);
            t0 = BotClock.Now;
            while (wall.IsPlaying && BotClock.Now - t0 < 6f) yield return null;
            yield return BotClock.Wait(0.3f);
            Record("the_wall_ends", !wall.IsPlaying && wall.Ends == 1 && !wall.ShowsClip, $"ends={wall.Ends}");

            // 3. The CRT on the cart: Interact turns it on.
            lucy.GetComponent<TankController>().Teleport(new Vector3(cart.Point.x + 0.7f, 0f, cart.Point.z - 0.2f), -80f);
            yield return BotClock.Wait(0.4f);
            pad.Tap(InputManager.GameAction.Interact);
            yield return BotClock.Wait(2.5f);
            yield return Shot("cart_playing");
            yield return Shot("cart_close", new Vector3(-1.6f, 1.6f, 11.4f), cart.Point + new Vector3(0f, 0.6f, 0f));
            Record("the_cart_plays_on_interact", cart.IsPlaying && cart.ShowsClip, $"frame={cart.Frame}");
            pad.Tap(InputManager.GameAction.Interact);
            yield return BotClock.Wait(0.4f);
            Record("the_cart_turns_off", !cart.IsPlaying, "");
            Finish();
        }

        /// <summary>The room through the main camera with the subtitles over it.</summary>
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
            var canvases = new List<Canvas>();
            if (SubtitleUI.Instance != null) canvases.Add(SubtitleUI.Instance.GetComponent<Canvas>());
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
                ShotRenderer.Render(cam, Path.Combine(BotArgs.RunDir, "shots", $"screens_{shotIndex:00}_{moment}.png"), useTargetTexture: true);
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
            sb.Append("{\n  \"bot\": \"screens\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[ScreenBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("screens_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
