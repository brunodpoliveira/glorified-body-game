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
    /// The gun holds bot (2026-10-31, the animation check): close shots of each armed figure's hands while it aims (the
    /// pistol in front of the fingers, the long-gun arms). AI TEST by default, LAB TEST with that level. Shots:
    /// shots/holds_&lt;n&gt;_&lt;figure&gt;_hands.png. Active only when BotArgs.Bot == "holds": tools/unity.sh play AiTest Main_Menu holds 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class HoldsPlaythrough : MonoBehaviour
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

        public static bool IsRequested => string.Equals(BotArgs.Bot, "holds", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<HoldsPlaythrough>() != null) return;
            var go = new GameObject("[HoldsPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<HoldsPlaythrough>();
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
            string line = $"[HoldsBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            Directory.CreateDirectory(Path.Combine(BotArgs.RunDir, "shots"));
            GameManager gm = GameManager.Instance;
            string room = BotArgs.Level == "LabTest" ? "LabTest" : "AiTest";
            gm.LoadLevel(room);
            float t0 = BotClock.Now;
            while ((SceneManager.GetActiveScene().name != room || !gm.IsPlaying) && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(1f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            if (room == "LabTest")
            {
                // The armed lab worker at rest, then the room reduced to him, with her in his sight.
                GameObject gunman = GameObject.Find("Lab_Gun2");
                Vector3 restChest = gunman.transform.position + Vector3.up * 1.2f;
                yield return Shot("Lab_Gun2_rest_side", restChest + gunman.transform.right * 2.1f, restChest);
                yield return Shot("Lab_Gun2_rest_front", restChest + gunman.transform.forward * 2.2f + gunman.transform.right * 0.9f, restChest);
                foreach (string other in new[] { "Lab_Knife", "Lab_Gun1" }) GameObject.Find(other)?.SetActive(false);
                lucy.GetComponent<TankController>().Teleport(gunman.transform.position + gunman.transform.forward * 6f, 0f);
                gunman.GetComponent<Enemy.SoldierBrain>().Alert("bot", callOthers: false);
            }
            int seen = 0;
            var done = new HashSet<Enemy.EnemyGun>();
            t0 = BotClock.Now;
            // Each armed figure, close at its hands while it aims at her (the telegraph): the grip of the gun.
            while (BotClock.Now - t0 < 40f)
            {
                foreach (Enemy.EnemyGun gun in FindObjectsByType<Enemy.EnemyGun>(FindObjectsSortMode.None))
                {
                    if (done.Contains(gun) || gun.Model == null) continue;
                    Enemy.DummyShooter shooter = gun.GetComponent<Enemy.DummyShooter>();
                    if (shooter == null || !shooter.HoldsSlot || shooter.CurrentAttack == Enemy.DummyShooter.Attack.Melee) continue;
                    Animator body = gun.GetComponent<Animator>();
                    Transform hand = body != null ? body.GetBoneTransform(HumanBodyBones.RightHand) : null;
                    if (hand == null) continue;
                    done.Add(gun);
                    yield return BotClock.Wait(0.4f); // the raise
                    Vector3 side = gun.transform.right;
                    yield return Shot($"{gun.name}_hands", hand.position + side * 0.7f + gun.transform.forward * 0.15f + Vector3.up * 0.1f, hand.position);
                    Vector3 chest = gun.transform.position + Vector3.up * 1.3f;
                    if (gun.name != "Guard_B") Record($"{gun.name}_long_gun_hold", gun.RightGap < 0.03f && gun.LeftGap < 0.05f && gun.RightElbowLift < 0f, $"right gap {gun.RightGap:0.000} left gap {gun.LeftGap:0.000} elbow {gun.RightElbowLift:0.00} m");
                    // The others hide for the two body shots (two soldiers overlapped).
                    var hidden = new List<Renderer>();
                    foreach (Enemy.EnemyGun other in FindObjectsByType<Enemy.EnemyGun>(FindObjectsSortMode.None))
                        if (other != gun) foreach (Renderer r in other.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hidden.Add(r); }
                    yield return Shot($"{gun.name}_side", chest + side * 2.1f + Vector3.up * 0.1f, chest);
                    yield return Shot($"{gun.name}_front", chest + gun.transform.forward * 2.2f + side * 0.9f + Vector3.up * 0.2f, chest);
                    foreach (Renderer r in hidden) if (r != null) r.enabled = true;
                    seen++;
                    if (gun.Model != null && gun.name.Contains("Guard"))
                        Record($"{gun.name}_pistol_grip_in_the_palm", gun.RightGap < 0.03f, $"gap {gun.RightGap:0.000}");
                }
                yield return null;
            }
            Record("hands_seen", seen > 0, $"guns={seen}");
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
                ShotRenderer.Render(cam, Path.Combine(BotArgs.RunDir, "shots", $"holds_{shotIndex:00}_{moment}.png"), useTargetTexture: true);
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
            Debug.Log($"[HoldsBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("holds_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
