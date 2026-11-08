using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Subtitles;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The floor shield bot (2026-10-31): in AI TEST she holds the defend key with mk_orzabal, a slab tears out of the
    /// floor and bends up, it stays where it rose when she moves (2026-11-08), bullets stop on it, and the attack key leaves
    /// it in place. Shots:
    /// shots/shield_&lt;n&gt;_&lt;moment&gt;.png. Active only when BotArgs.Bot == "shield": tools/unity.sh play AiTest Main_Menu shield 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class ShieldPlaythrough : MonoBehaviour
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

        public static bool IsRequested => string.Equals(BotArgs.Bot, "shield", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<ShieldPlaythrough>() != null) return;
            var go = new GameObject("[ShieldPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<ShieldPlaythrough>();
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
            string line = $"[ShieldBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            Directory.CreateDirectory(Path.Combine(BotArgs.RunDir, "shots"));
            GameManager gm = GameManager.Instance;
            gm.LoadLevel("AiTest");
            float t0 = BotClock.Now;
            while ((SceneManager.GetActiveScene().name != "AiTest" || !gm.IsPlaying) && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(1f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            LockOnController lockOn = lucy.GetComponent<LockOnController>();
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            padDriver = new GameObject("[ShieldPad]").AddComponent<PadDriver>();
            DontDestroyOnLoad(padDriver.gameObject);
            padDriver.Pad = pad;
            LockOnTargetable soldier = GameObject.Find("Soldier_A")?.GetComponent<LockOnTargetable>();
            LockOnTargetable armour = GameObject.Find("Armour_C")?.GetComponent<LockOnTargetable>();
            if (soldier == null || armour == null) { Record("figures_in_room", false, "no Soldier_A or Armour_C (AiTestSetup.BuildAiTest)"); Finish(); yield break; }
            foreach (string other in new[] { "Guard_B", "Gasser_D" }) GameObject.Find(other)?.SetActive(false); // two figures: the shot stays readable
            foreach (Enemy.DummyShooter gun in armour.GetComponentsInChildren<Enemy.DummyShooter>()) gun.enabled = false; // the throw's target only
            int rifleHits = 0;
            HitEventBus.OnHit += hit =>
            {
                if (hit != null && hit.Victim != null && hit.Victim.CompareTag("Player") && hit.Attacker != null && hit.Attacker.transform.IsChildOf(soldier.transform.root)) rifleHits++;
            };

            // 1. mk_orzabal, the soldier locked, the defend key held to the full circle: a slab tears out of the floor.
            pad.Tap(InputManager.GameAction.PowerTelekinesis);
            yield return BotClock.Wait(0.2f);
            stats.DebugSet(PsychicStats.MaxNalcon, 0f);
            lockOn.SetTarget(soldier);
            pad.SetHeld(InputManager.GameAction.Defend, true);
            t0 = BotClock.Now;
            while (FloorShield.Current == null && BotClock.Now - t0 < 6f) yield return null;
            pad.SetHeld(InputManager.GameAction.Defend, false);
            yield return BotClock.Wait(0.5f);
            FloorShield shield = FloorShield.Current;
            Transform her = lucy.transform;
            yield return Shot("shield_up", her.position - her.forward * 2.4f + her.right * 0.9f + Vector3.up * 1.9f, her.position + her.forward * 2f + Vector3.up * 1f);
            Record("a_slab_tears_out", shield != null && powers.LastResult == "shield", $"mk={stats.Nalcon:0}");

            // 2. It stays where it rose (Bruno, 2026-11-08: "it should be fixed in place"): she turns and steps aside, it does not move.
            TankController tank = lucy.GetComponent<TankController>();
            Vector3 front = her.forward, stand = her.position;
            Vector3 slabAt = shield != null ? shield.Slab.position : Vector3.zero;
            Quaternion slabTurn = shield != null ? shield.Slab.rotation : Quaternion.identity;
            tank.Teleport(stand + her.right * 2f, her.eulerAngles.y + 90f);
            yield return BotClock.Wait(0.5f);
            yield return Shot("shield_stays", stand - front * 2.4f + Vector3.Cross(Vector3.up, front) * 2.5f + Vector3.up * 2.2f, slabAt);
            Record("it_stays_when_she_moves", shield != null && Vector3.Distance(slabAt, shield.Slab.position) < 0.01f && Quaternion.Angle(slabTurn, shield.Slab.rotation) < 0.5f,
                shield != null ? $"moved {Vector3.Distance(slabAt, shield.Slab.position):0.000} m" : "broken");
            tank.Teleport(stand, Quaternion.LookRotation(front).eulerAngles.y);
            yield return BotClock.Wait(0.3f);

            // 3. Three rounds at her chest from in front of the slab: they stop on it.
            int hitsBefore = rifleHits;
            if (shield != null)
            {
                Vector3 chest = her.position + Vector3.up * Body.ChestHeight;
                Vector3 muzzle = stand + front * 8f + Vector3.up * Body.MuzzleHeight;
                for (int i = 0; i < 3; i++)
                {
                    Enemy.EnemyProjectile.Launch(muzzle, (chest - muzzle).normalized, 40f, 15f, soldier.gameObject, "soldier_rifle");
                    yield return BotClock.Wait(0.12f);
                }
                yield return Shot("shield_takes_fire", her.position - her.forward * 2.4f + her.right * 0.9f + Vector3.up * 1.9f, her.position + her.forward * 2f + Vector3.up * 1f);
                yield return BotClock.Wait(0.5f);
            }
            Record("the_slab_takes_the_bullets", shield != null && shield.Absorbed == 3 && rifleHits == hitsBefore, shield != null ? $"absorbed={shield.Absorbed} hp={shield.Hp:0}" : "broken");

            // 4. The attack key does not throw it: it is no held thing.
            lockOn.SetTarget(armour);
            yield return BotClock.Wait(0.2f);
            pad.Tap(InputManager.GameAction.Charge);
            yield return BotClock.Wait(1f);
            Record("the_attack_leaves_it", shield != null && FloorShield.Current == shield && Vector3.Distance(slabAt, shield.Slab.position) < 0.01f, $"result={powers.LastResult}");
            yield return Shot("after_the_attack");
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
                ShotRenderer.Render(cam, Path.Combine(BotArgs.RunDir, "shots", $"shield_{shotIndex:00}_{moment}.png"), useTargetTexture: true);
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
            Debug.Log($"[ShieldBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("shield_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
