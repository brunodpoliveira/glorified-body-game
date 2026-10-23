using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The AI bot: loads the AI test room from the menu, lets the FSM soldiers fight a Lucy who takes no damage,
    /// and shoots the moments: the soldiers walking, a telegraph (the laser and the HUD circle), a soldier in the
    /// lift, "TOO HEAVY" on the power armour, a corpse. It checks the rule "move or shoot, never both".
    /// Active only when BotArgs.Bot == "ai". Writes playthrough.json and shots/ai_&lt;n&gt;_&lt;moment&gt;.png.
    /// Batch: tools/unity.sh play AiTest Main_Menu ai 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class AiPlaythrough : MonoBehaviour
    {
        public const string SceneName = "AiTest";
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

        public static bool IsRequested => string.Equals(BotArgs.Bot, "ai", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<AiPlaythrough>() != null) return;
            var go = new GameObject("[AiPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<AiPlaythrough>();
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
            string line = $"[AiBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            GameManager gm = GameManager.Instance;
            if (gm != null) gm.LoadLevel(SceneName); else SceneManager.LoadScene(SceneName);
            float t0 = BotClock.Now;
            while (SceneManager.GetActiveScene().name != SceneName && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(0.5f);

            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            SoldierBrain[] soldiers = FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).OrderBy(s => s.name).ToArray();
            Record("ai_loaded", SceneManager.GetActiveScene().name == SceneName && lucy != null && soldiers.Length == 3,
                $"scene={SceneManager.GetActiveScene().name} soldiers={soldiers.Length}");
            if (lucy == null || soldiers.Length == 0) { Finish(); yield break; }
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            LockOnController lockOn = lucy.GetComponent<LockOnController>();
            string shots = Path.Combine(BotArgs.RunDir, "shots");

            yield return BotClock.Wait(3f);
            Shot(Path.Combine(shots, "ai_01_soldiers_move.png"));
            Record("soldiers_engage", soldiers.All(s => s.Body.HasTarget) && soldiers.Any(s => s.Moves > 0),
                string.Join(" ", soldiers.Select(s => $"{s.name}:{s.Current}/target={s.Body.HasTarget}/moves={s.Moves}")));

            t0 = BotClock.Now;
            while (!soldiers.Any(s => s.Gun.Current == DummyShooter.State.Telegraph) && BotClock.Now - t0 < 12f) yield return null;
            yield return null;
            SoldierBrain aiming = soldiers.FirstOrDefault(s => s.Gun.Current == DummyShooter.State.Telegraph);
            Shot(Path.Combine(shots, "ai_02_telegraph.png"));
            Record("telegraph", aiming != null, aiming != null ? $"{aiming.name} counts down" : "no telegraph in 12 s");

            Record("alerted", soldiers.All(s => s.Alerted), string.Join(" ", soldiers.Select(s => $"{s.name}={s.AlertReason}")));

            // The gun: a close shot on the next muzzle flash.
            EnemyGun[] guns = soldiers.Select(s => s.GetComponent<EnemyGun>()).Where(g => g != null).ToArray();
            int before = guns.Sum(g => g.Shots);
            t0 = BotClock.Now;
            while (guns.Sum(g => g.Shots) == before && BotClock.Now - t0 < 10f) yield return null;
            EnemyGun firing = guns.OrderByDescending(g => g.LastFireTime).FirstOrDefault();
            yield return null;
            Shot(Path.Combine(shots, "ai_06_flash_wide.png"));
            // From the soldier's right side: the gun in profile, off the line of fire.
            if (firing != null) CloseShot(firing.Model.position, firing.transform.right, Path.Combine(shots, "ai_07_flash_close.png"));
            Record("muzzle_flash", firing != null && guns.Sum(g => g.Shots) > before, firing != null ? $"{firing.name} shots={firing.Shots}" : "no shot");

            // A miss: she steps aside during a telegraph, the bullet hits the wall behind her.
            t0 = BotClock.Now;
            while (!soldiers.Any(s => s.Gun.Current == DummyShooter.State.Telegraph) && BotClock.Now - t0 < 12f) yield return null;
            Vector3 stand = lucy.transform.position;
            lucy.GetComponent<TankController>().Teleport(stand + Vector3.right * 2.5f, 0f);
            Transform impact = null;
            t0 = BotClock.Now;
            while (impact == null && BotClock.Now - t0 < 4f)
            {
                impact = FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name.StartsWith("impactConcrete"));
                yield return null;
            }
            if (impact != null)
            {
                yield return new WaitForSeconds(0.15f); // the particles need a moment
                // The impact faces out of the wall: the camera stands on that side.
                CloseShot(impact.position, impact.forward, Path.Combine(shots, "ai_08_impact.png"), 1.6f);
            }
            Record("impact", impact != null, impact != null ? $"at {impact.position}" : "no impact in 4 s");
            lucy.GetComponent<TankController>().Teleport(stand, 0f);

            yield return BotClock.Wait(4f);
            int fired = soldiers.Sum(s => s.Gun.ShotsFired);
            int drift = soldiers.Sum(s => s.MovedWhileShootingFrames);
            Record("move_or_shoot", fired >= 2 && drift == 0, $"shots={fired} frames moved while aiming={drift} " +
                string.Join(" ", soldiers.Select(s => $"{s.name}:moves={s.Moves}/shoots={s.ShootPhases}")));

            // The lift, then a slam.
            SoldierBrain lifted = soldiers.FirstOrDefault(s => s.name.StartsWith("Soldier")) ?? soldiers[0]; // not the heavy armour
            stats.DebugSet(100f, 0f);
            powers.Select(PowerKind.Telekinesis);
            lockOn.SetTarget(lifted.GetComponent<LockOnTargetable>());
            powers.Release(1f);
            yield return BotClock.Wait(1f);
            Shot(Path.Combine(shots, "ai_03_lifted.png"));
            Record("lift", lifted.transform.position.y > 1f && lifted.Current == SoldierBrain.Phase.Hold, $"{lifted.name} y={lifted.transform.position.y:0.00} phase={lifted.Current}");
            stats.DebugSet(100f, 0f);
            Health liftedHealth = lifted.GetComponent<Health>();
            float hp = liftedHealth.CurrentHealth;
            bool slammed = powers.TrySlam();
            yield return BotClock.Wait(2.5f);
            Record("slam", slammed && lifted.transform.position.y < 0.2f && liftedHealth.CurrentHealth < hp,
                $"slam={slammed} y={lifted.transform.position.y:0.00} hp {hp:0} -> {liftedHealth.CurrentHealth:0} phase={lifted.Current}");

            // The power armour is heavy.
            SoldierBrain armour = soldiers.FirstOrDefault(s => s.name.StartsWith("Armour"));
            if (armour != null)
            {
                lockOn.SetTarget(armour.GetComponent<LockOnTargetable>());
                powers.Release(1f);
                yield return null;
                yield return null;
                Shot(Path.Combine(shots, "ai_04_too_heavy.png"));
                Record("armour_heavy", powers.LastResult == "too_heavy", $"result={powers.LastResult}");
            }

            // A corpse.
            SoldierBrain victim = soldiers.FirstOrDefault(s => s.name.StartsWith("Guard"));
            if (victim != null)
            {
                Health health = victim.GetComponent<Health>();
                health.TakeDamage(health.MaxHealth * 10f);
                yield return BotClock.Wait(2.5f);
                Shot(Path.Combine(shots, "ai_05_corpse.png"));
                Record("death", victim != null && victim.Body.IsDead && victim.Current == SoldierBrain.Phase.Dead, $"{victim.name} dead={victim.Body.IsDead}");
            }
            Finish();
        }

        /// <summary>A 1080p shot with the HUD (the telegraph circle, the notice) drawn through the camera.</summary>
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
        private static void CloseShot(Vector3 point, Vector3 facing, string path, float distance = 1.4f)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
            var go = new GameObject("[CloseShotCamera]");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;
            Camera main = Camera.main;
            if (main != null)
            {
                cam.clearFlags = main.clearFlags;
                cam.backgroundColor = main.backgroundColor;
            }
            go.transform.position = point + facing.normalized * distance + Vector3.up * 0.25f;
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
            sb.Append("{\n  \"bot\": \"ai\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[AiBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("ai_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
