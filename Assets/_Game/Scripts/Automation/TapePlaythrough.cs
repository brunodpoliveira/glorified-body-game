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
using GlorifiedBody.UI;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The audio log bot (Bruno, 2026-10-31): in TRAVEL A she takes the tape, it plays while she walks (the label, the
    /// subtitle, the music down), a hit pauses it, she plays it again from the inventory. Shots:
    /// shots/tape_&lt;n&gt;_&lt;moment&gt;.png (the room with the tape label and the subtitles over it). Active only when
    /// BotArgs.Bot == "tape": tools/unity.sh play TravelTest_A Main_Menu tape 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class TapePlaythrough : MonoBehaviour
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

        public static bool IsRequested => string.Equals(BotArgs.Bot, "tape", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<TapePlaythrough>() != null) return;
            var go = new GameObject("[TapePlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<TapePlaythrough>();
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
            string line = $"[TapeBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            Directory.CreateDirectory(Path.Combine(BotArgs.RunDir, "shots"));
            GameManager gm = GameManager.Instance;
            gm.LoadLevel("TravelTest_A");
            float t0 = BotClock.Now;
            while ((SceneManager.GetActiveScene().name != "TravelTest_A" || !gm.IsPlaying) && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(0.5f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            padDriver = new GameObject("[TapePad]").AddComponent<PadDriver>();
            DontDestroyOnLoad(padDriver.gameObject);
            padDriver.Pad = pad;
            TapePlayer tapes = TapePlayer.Instance;

            // 1. She takes the tape: it plays, she walks, the music drops.
            ItemPickup pickup = GameObject.Find("Tape_Travel")?.GetComponent<ItemPickup>();
            if (pickup == null) { Record("tape_in_room", false, "no Tape_Travel (TravelTestSetup.BuildTravelTest)"); Finish(); yield break; }
            ItemDefinition tape = pickup.item;
            lucy.GetComponent<TankController>().Teleport(pickup.transform.position + new Vector3(0f, 0f, 1f), 180f);
            yield return BotClock.Wait(0.3f);
            pad.Tap(InputManager.GameAction.Interact);
            yield return BotClock.Wait(0.3f);
            pad.Tap(InputManager.GameAction.Interact);
            yield return BotClock.Wait(0.3f);
            // She walks north in the middle of the room: by the south wall the wide camera loses her.
            lucy.GetComponent<TankController>().Teleport(new Vector3(0f, 0f, -1.5f), 0f);
            yield return BotClock.Wait(0.2f);
            pad.SetHeld(InputManager.GameAction.Forward, true);
            yield return BotClock.Wait(1.2f);
            pad.SetHeld(InputManager.GameAction.Forward, false);
            yield return BotClock.Wait(0.4f);
            float duck = MusicDirector.Instance != null ? MusicDirector.Instance.DuckFactor : -1f;
            bool line = SubtitleService.Lines.Any(l => l.Owner == TapePlayer.SubtitleOwner);
            yield return Shot("playing");
            Record("plays_while_she_walks", tapes.IsPlaying && gm.IsPlaying && line && duck <= TapePlayer.MusicLevel + 0.001f,
                $"playing={tapes.IsPlaying} at={tapes.Position:0.0} line={line} music={duck:0.00}");

            // 2. A hit: the music goes to Combat, the tape pauses and stays paused.
            MusicDirector.Threat();
            t0 = BotClock.Now;
            while (tapes.IsPlaying && BotClock.Now - t0 < 2f) yield return null;
            yield return BotClock.Wait(0.3f);
            yield return Shot("fight_paused");
            Record("a_fight_pauses_it", !tapes.IsPlaying && tapes.StoppedBy == TapePlayer.PausedByCombat, $"stopped={tapes.StoppedBy} at={tapes.Position:0.0}");

            // 3. The fight ends (the music holds Combat 8 s after the last hit), and she plays it again from the inventory.
            t0 = BotClock.Now;
            while (MusicDirector.Instance.State == MusicState.Combat && BotClock.Now - t0 < MusicDirector.CombatHoldSeconds + 3f) yield return null;
            yield return BotClock.Wait(0.5f);
            bool stillPaused = !tapes.IsPlaying;
            InventoryUI inventory = FindFirstObjectByType<InventoryUI>();
            pad.Tap(InputManager.GameAction.Inventory);
            yield return BotClock.Wait(0.4f);
            int index = -1;
            for (int i = 0; i < inventory.Entries.Count; i++) if (inventory.Entries[i].Item == tape) index = i;
            inventory.SelectIndex(index);
            yield return BotClock.Wait(0.2f);
            shotIndex++;
            yield return GlorifiedBody.UI.OverlayShot.Capture(inventory.Canvas, $"tape_{shotIndex:00}_inventory.png", 0f);
            string act = inventory.Act();
            pad.Tap(InputManager.GameAction.Inventory);
            yield return BotClock.Wait(1.2f);
            yield return Shot("playing_again");
            Record("played_again_from_the_inventory", stillPaused && act.StartsWith("tape_play:") && tapes.IsPlaying, $"paused_after_fight={stillPaused} act={act} at={tapes.Position:0.0}");
            Finish();
        }

        /// <summary>The room through the main camera with the tape label and the subtitles over it.</summary>
        private IEnumerator Shot(string moment)
        {
            shotIndex++;
            yield return null; // not WaitForEndOfFrame: it never comes in a batch run
            Camera cam = Camera.main;
            if (cam == null) yield break;
            var canvases = new List<Canvas>();
            if (TapePlayer.Instance != null && TapePlayer.Instance.Canvas != null) canvases.Add(TapePlayer.Instance.Canvas);
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
                ShotRenderer.Render(cam, Path.Combine(BotArgs.RunDir, "shots", $"tape_{shotIndex:00}_{moment}.png"), useTargetTexture: true);
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
            sb.Append("{\n  \"bot\": \"tape\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[TapeBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("tape_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
