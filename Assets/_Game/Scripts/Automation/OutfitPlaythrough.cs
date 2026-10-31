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
    /// The outfit bot (2026-10-31): in OUTFIT TEST she puts on the guard uniform at the locker, then the grab tears it off
    /// and the pills over the bare limit fall. Shots: shots/outfit_&lt;n&gt;_&lt;moment&gt;.png (the HUD and the subtitles over the
    /// room). Active only when BotArgs.Bot == "outfit": tools/unity.sh play OutfitTest Main_Menu outfit 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class OutfitPlaythrough : MonoBehaviour
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

        public static bool IsRequested => string.Equals(BotArgs.Bot, "outfit", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<OutfitPlaythrough>() != null) return;
            var go = new GameObject("[OutfitPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<OutfitPlaythrough>();
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
            string line = $"[OutfitBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            Directory.CreateDirectory(Path.Combine(BotArgs.RunDir, "shots"));
            GameManager gm = GameManager.Instance;
            gm.LoadLevel("OutfitTest");
            float t0 = BotClock.Now;
            while ((SceneManager.GetActiveScene().name != "OutfitTest" || !gm.IsPlaying) && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(0.5f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            Health health = lucy.GetComponentInChildren<Health>();
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            padDriver = new GameObject("[OutfitPad]").AddComponent<PadDriver>();
            DontDestroyOnLoad(padDriver.gameObject);
            padDriver.Pad = pad;
            OutfitLocker locker = GameObject.Find("Locker_Uniform")?.GetComponent<OutfitLocker>();
            if (locker == null) { Record("locker_in_room", false, "no Locker_Uniform (OutfitTestSetup.BuildOutfitTest)"); Finish(); yield break; }
            TankController tank = lucy.GetComponent<TankController>();

            // 1. At the locker: the uniform hangs in it.
            tank.Teleport(new Vector3(-1f, 0f, 5.2f), 0f);
            yield return BotClock.Wait(0.4f);
            yield return Shot("locker", new Vector3(1.6f, 1.7f, 2.6f), new Vector3(-1f, 1.1f, 6.4f));
            Record("the_uniform_hangs_in_the_locker", locker.shown.activeInHierarchy, "");

            // 2. She puts it on: the guard's model, 150 HP, 8 slots, the HUD names it.
            health.SetHealth(60f);
            pad.Tap(InputManager.GameAction.Interact);
            yield return BotClock.Wait(0.3f);
            pad.Tap(InputManager.GameAction.Interact);
            yield return BotClock.Wait(1.0f);
            tank.Teleport(new Vector3(-1f, 0f, 3.8f), 200f);
            yield return BotClock.Wait(0.6f);
            yield return Shot("uniform_on", new Vector3(0.6f, 1.6f, 1.4f), new Vector3(-1f, 0.95f, 3.8f));
            Record("she_wears_the_uniform", stats.Outfit.outfitId == OutfitChange.GuardId && LucyModel.Current(lucy) == "lucy_guard" && Mathf.Approximately(health.MaxHealth, 150f),
                $"outfit={stats.Outfit.outfitId} model={LucyModel.Current(lucy)} hp={health.CurrentHealth:0}/{health.MaxHealth:0}");

            // 3. The grab tears it off: six pills in the pockets, four stay, two fall.
            lucy.GetComponent<DrugInventory>().DebugSet(DrugType.Red, 6);
            tank.Teleport(new Vector3(7f, 0f, -2f), 270f);
            t0 = BotClock.Now;
            while (stats.Outfit.outfitId != OutfitChange.NudeId && BotClock.Now - t0 < 10f) yield return null;
            yield return BotClock.Wait(0.8f);
            int fell = FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).Count(p => p.name.StartsWith("Fell_"));
            yield return Shot("grab_torn_off", new Vector3(4.4f, 1.8f, -4.6f), new Vector3(6.6f, 0.4f, -2f));
            Record("the_grab_tears_it_off", stats.Outfit.outfitId == OutfitChange.NudeId && LucyModel.Current(lucy) == "lucy_bare" && fell == 2,
                $"outfit={stats.Outfit.outfitId} fell={fell} hp={health.CurrentHealth:0}/{health.MaxHealth:0}");
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
                ShotRenderer.Render(cam, Path.Combine(BotArgs.RunDir, "shots", $"outfit_{shotIndex:00}_{moment}.png"), useTargetTexture: true);
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
            Debug.Log($"[OutfitBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("outfit_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
