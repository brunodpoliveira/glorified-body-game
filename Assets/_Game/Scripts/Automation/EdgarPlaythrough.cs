using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The mk_edgar bot (2026-11-06): a refusal, a guard on his knees, the taken figure with its mark, and its fight with a soldier, by
    /// the virtual pad. Active only when BotArgs.Bot == "edgar". Writes playthrough.json and shots/edgar_&lt;n&gt;_&lt;moment&gt;.png.
    /// Batch: tools/unity.sh play EdgarTest Main_Menu edgar 200
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class EdgarPlaythrough : MonoBehaviour
    {
        private const string SceneName = "EdgarTest";
        private const int ShotWidth = 1920, ShotHeight = 1080;

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

        public static bool IsRequested => string.Equals(BotArgs.Bot, "edgar", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<EdgarPlaythrough>() != null) return;
            var go = new GameObject("[EdgarPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<EdgarPlaythrough>();
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
            string line = $"[EdgarBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private static IEnumerator Until(Func<bool> condition, float seconds)
        {
            float t0 = BotClock.Now;
            while (!condition() && BotClock.Now - t0 < seconds) yield return null;
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            GameManager gm = GameManager.Instance;
            if (gm != null) gm.LoadLevel(SceneName); else SceneManager.LoadScene(SceneName);
            yield return Until(() => SceneManager.GetActiveScene().name == SceneName && gm != null && gm.IsPlaying, 30f);
            yield return Until(() => PsychicPowers.EdgarUnlocked, 3f);
            yield return BotClock.Wait(0.5f);
            string shots = Path.Combine(BotArgs.RunDir, "shots");
            Directory.CreateDirectory(shots);
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);

            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            LockOnController lockOn = lucy.GetComponent<LockOnController>();
            TankController tank = lucy.GetComponent<TankController>();
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            Canvas hud = GameObject.Find("PlayerHudCanvas")?.GetComponent<Canvas>();
            GameObject guard = GameObject.Find("Guard_B"), soldier = GameObject.Find("Soldier_A");
            Record("room", guard != null && soldier != null);
            foreach (string name in new[] { "Worker_C", "Boss_D", "Guard_Far" })
            {
                GameObject other = GameObject.Find(name);
                if (other != null) other.SetActive(false);
            }
            soldier.SetActive(false);
            powers.Select(PowerKind.Domination);
            stats.DebugSet(100f, 0f);
            tank.Teleport(guard.transform.position + Vector3.back * 3f, 0f);
            yield return null;
            lockOn.SetTarget(guard.GetComponent<LockOnTargetable>());
            yield return BotClock.Wait(0.2f);

            // The room starts with its AI off.
            yield return BotClock.Wait(1.5f);
            Record("passive", !guard.GetComponent<SoldierBrain>().Hunting, "");
            Shot(Path.Combine(shots, "edgar_00_figures_off.png"));
            GlorifiedBody.Saving.StoryFlags.Raise("edgar_test_awake");
            yield return null;

            // A guard at full HP: the HUD says why, at the press. This hold is too short to stun him, so it costs nothing.
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return BotClock.Wait(0.3f);
            Record("refusal", powers.LastResult == "dom_not_hurt", powers.LastResult);
            if (hud != null) yield return OverlayShot.Capture(hud, "edgar_01_refused_hud.png", 0.05f);
            pad.SetHeld(InputManager.GameAction.Charge, false);
            yield return BotClock.Wait(0.1f);

            // With damage: he goes down on his knees while the circle fills.
            guard.GetComponent<Health>().TakeDamage(5f);
            yield return BotClock.Wait(0.3f);
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return BotClock.Wait(powers.Selected.fullChargeTime * 0.7f);
            Vector3 chest = guard.transform.position + Vector3.up * 0.8f;
            CloseShot(chest, guard.transform.right + guard.transform.forward, Path.Combine(shots, "edgar_02_on_his_knees.png"), 3f, 0.4f);
            Shot(Path.Combine(shots, "edgar_03_kneel_room.png"));
            Record("kneel", powers.Charge > 0f, $"charge {powers.Charge:0.00}");
            yield return Until(() => guard.GetComponent<SoldierBrain>().IsDominated, powers.Selected.fullChargeTime);
            pad.SetHeld(InputManager.GameAction.Charge, false);
            Record("taken", guard.GetComponent<SoldierBrain>().IsDominated, $"mk {stats.Mk(PowerKind.Domination):0}");
            yield return BotClock.Wait(0.8f);
            CloseShot(guard.transform.position + Vector3.up, guard.transform.forward + guard.transform.right * 0.6f, Path.Combine(shots, "edgar_04_taken_mark.png"), 3f, 0.3f);

            // A soldier comes: they fight.
            soldier.SetActive(true);
            soldier.GetComponent<SoldierBrain>().Alert("bot", callOthers: false);
            Health soldierHealth = soldier.GetComponent<Health>();
            float s0 = soldierHealth.CurrentHealth;
            yield return Until(() => guard.GetComponent<DummyShooter>().Current == DummyShooter.State.Telegraph, 20f);
            Shot(Path.Combine(shots, "edgar_05_the_fight.png"));
            yield return Until(() => soldierHealth.CurrentHealth < s0, 25f);
            Record("fight", soldierHealth.CurrentHealth < s0, $"soldier {soldierHealth.CurrentHealth:0}/{s0:0}");

            // The time runs out: he kneels, then he hunts her (Bruno, 2026-11-07).
            soldier.SetActive(false);
            SoldierBrain guardBrain = guard.GetComponent<SoldierBrain>();
            Dominated mind = Domination.Current;
            mind.SetTimeLeft(Dominated.WarnSeconds);
            yield return BotClock.Wait(2f);
            CloseShot(guard.transform.position + Vector3.up, guard.transform.forward + guard.transform.right * 0.6f, Path.Combine(shots, "edgar_06_warning.png"), 3f, 0.3f);
            yield return Until(() => mind == null || mind.Snapping, 5f);
            yield return BotClock.Wait(0.8f);
            CloseShot(guard.transform.position + Vector3.up * 0.8f, guard.transform.forward + guard.transform.right * 0.6f, Path.Combine(shots, "edgar_07_snap_knees.png"), 3f, 0.4f);
            Shot(Path.Combine(shots, "edgar_08_snap_room.png"));
            yield return Until(() => !guardBrain.IsDominated, Dominated.SnapKneelSeconds + 2f);
            Record("snap", !guardBrain.IsDominated && guardBrain.Hunting && Domination.Current == null, $"hunting {guardBrain.Hunting}");
            pad.ReleaseAll();
            GameInput.ClearBotSource(pad);
            pad = null;
            Finish();
        }

        private void Update() => pad?.Commit();

        private VirtualPad pad;

        /// <summary>What the player sees, from the camera of the room.</summary>
        private static void Shot(string path)
        {
            Camera cam = Camera.main;
            if (cam != null) ShotRenderer.Render(cam, path, ShotWidth, ShotHeight, useTargetTexture: true);
        }

        /// <summary>A close shot from a temporary camera.</summary>
        private static void CloseShot(Vector3 point, Vector3 facing, string path, float distance, float height)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
            var go = new GameObject("[CloseShotCamera]");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.05f;
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
            sb.Append("{\n  \"bot\": \"team\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[EdgarBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("edgar_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
