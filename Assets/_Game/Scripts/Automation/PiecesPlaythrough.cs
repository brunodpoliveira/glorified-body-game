using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Characters;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Subtitles;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The demo pieces bot (2026-10-31): the hider in WARD TEST (it whispers, then begs where it is), the bulletin card,
    /// the wall sign in OUTFIT TEST. Shots: shots/pieces_&lt;n&gt;_&lt;moment&gt;.png. Active only when BotArgs.Bot == "pieces":
    /// tools/unity.sh play WardTest Main_Menu pieces 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class PiecesPlaythrough : MonoBehaviour
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

        public static bool IsRequested => string.Equals(BotArgs.Bot, "pieces", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<PiecesPlaythrough>() != null) return;
            var go = new GameObject("[PiecesPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<PiecesPlaythrough>();
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
            string line = $"[PiecesBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            Directory.CreateDirectory(Path.Combine(BotArgs.RunDir, "shots"));
            GameManager gm = GameManager.Instance;
            gm.LoadLevel("WardTest");
            float t0 = BotClock.Now;
            while ((SceneManager.GetActiveScene().name != "WardTest" || !gm.IsPlaying) && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(1f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            TankController tank = lucy.GetComponent<TankController>();
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            padDriver = new GameObject("[PiecesPad]").AddComponent<PadDriver>();
            DontDestroyOnLoad(padDriver.gameObject);
            padDriver.Pad = pad;
            GameObject.Find("Marta")?.SetActive(false);

            // 1. The hider: near, it whispers; close, it begs where it is.
            Enemy.CivilianBrain hider = GameObject.Find("Hider_Stall")?.GetComponent<Enemy.CivilianBrain>();
            if (hider == null) { Record("hider_in_room", false, "no Hider_Stall (WardTestSetup.BuildWardTest)"); Finish(); yield break; }
            Vector3 spot = hider.transform.position;
            tank.Teleport(spot + new Vector3(-3.5f, 0f, -1f), 45f);
            yield return BotClock.Wait(1.2f);
            yield return Shot("hider_whispers", spot + new Vector3(-2.6f, 1.7f, -3.2f), spot + Vector3.up * 0.6f);
            Record("the_hider_whispers", hider.Whispers > 0 && !hider.Found, $"whispers={hider.Whispers}");
            tank.Teleport(spot + new Vector3(-1.4f, 0f, -0.6f), 45f);
            yield return BotClock.Wait(1.5f);
            yield return Shot("hider_found", spot + new Vector3(-2.2f, 1.6f, -2.6f), spot + Vector3.up * 0.6f);
            Record("the_hider_begs_where_it_is", hider.Found && hider.Current == Enemy.CivilianBrain.Phase.Beg, $"phase={hider.Current}");

            // 2. The bulletin card, the middle wording.
            bool closed = false;
            Story.Bulletin.Show(15, () => closed = true);
            yield return new WaitForSecondsRealtime(1.2f);
            Canvas card = GameObject.Find("[Bulletin]")?.GetComponent<Canvas>();
            shotIndex++;
            if (card != null) yield return GlorifiedBody.UI.OverlayShot.Capture(card, $"pieces_{shotIndex:00}_bulletin_typing.png", 0f);
            pad.Tap(InputManager.GameAction.Interact);
            yield return new WaitForSecondsRealtime(0.3f);
            shotIndex++;
            if (card != null) yield return GlorifiedBody.UI.OverlayShot.Capture(card, $"pieces_{shotIndex:00}_bulletin.png", 0f);
            Record("the_bulletin_shows", Story.Bulletin.Shown.Contains("15 DEATHS"), Story.Bulletin.Band);
            pad.Tap(InputManager.GameAction.Interact);
            yield return new WaitForSecondsRealtime(0.3f);

            // 3. The wall sign of W6, in the lockers' room.
            gm.LoadLevel("OutfitTest");
            t0 = BotClock.Now;
            while ((SceneManager.GetActiveScene().name != "OutfitTest" || !gm.IsPlaying) && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(1f);
            GameObject sign = GameObject.Find("Sign_HardDays");
            if (sign != null) yield return Shot("sign", sign.transform.position + new Vector3(0.8f, -0.6f, -3.2f), sign.transform.position);
            Record("the_sign_on_the_wall", sign != null && closed, "");
            Finish();
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
            var canvases = new List<Canvas>();
            if (SubtitleUI.Instance != null) canvases.Add(SubtitleUI.Instance.GetComponent<Canvas>());
            GlorifiedBody.UI.PlayerHud hud = FindFirstObjectByType<GlorifiedBody.UI.PlayerHud>();
            Canvas hudCanvas = hud != null ? hud.GetComponentInChildren<Canvas>() : null;
            if (hudCanvas != null) canvases.Add(hudCanvas);
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
                ShotRenderer.Render(cam, Path.Combine(BotArgs.RunDir, "shots", $"pieces_{shotIndex:00}_{moment}.png"), useTargetTexture: true);
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
            Debug.Log($"[PiecesBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("pieces_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
