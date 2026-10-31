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
    /// The CPR bot (2026-10-31, the animation check): in AI TEST a soldier dies, she hides, a colleague drags the body and
    /// starts the CPR with his gun slung on his back. Shots: shots/cpr_&lt;n&gt;_&lt;moment&gt;.png. Active only when
    /// BotArgs.Bot == "cpr": tools/unity.sh play AiTest Main_Menu cpr 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class CprPlaythrough : MonoBehaviour
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

        public static bool IsRequested => string.Equals(BotArgs.Bot, "cpr", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<CprPlaythrough>() != null) return;
            var go = new GameObject("[CprPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<CprPlaythrough>();
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
            string line = $"[CprBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            Directory.CreateDirectory(Path.Combine(BotArgs.RunDir, "shots"));
            GameManager gm = GameManager.Instance;
            // AI TEST: a soldier tends a soldier. WARD TEST: Marta the nurse tends the guard (the "uncanny" nurse of 10-27).
            bool ward = BotArgs.Level == "WardTest";
            string room = ward ? "WardTest" : "AiTest";
            gm.LoadLevel(room);
            float t0 = BotClock.Now;
            while ((SceneManager.GetActiveScene().name != room || !gm.IsPlaying) && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(1f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            GameObject victim = GameObject.Find(ward ? "Guard_W4" : "Soldier_A");
            GameObject medic = GameObject.Find(ward ? "Marta" : "Gasser_D");
            if (victim == null || medic == null) { Record("figures_in_room", false, "no victim or carer in " + room); Finish(); yield break; }
            yield return BotClock.Wait(2f);

            // A soldier dies; she hides (out of every sight line); a colleague drags him and starts the CPR.
            Health victimHealth = victim.GetComponent<Health>();
            victimHealth.TakeDamage(victimHealth.MaxHealth * 10f);
            HitEventBus.Raise(new HitInfo { Attacker = lucy, Victim = victim.gameObject, HitTransform = victim.transform, HitPoint = victim.transform.position + Vector3.up * 1.2f, HitNormal = -victim.transform.forward, DamageAmount = 999f, DamageType = DamageType.PsychicBlast, WasFatal = true });
            yield return BotClock.Wait(2.2f);
            CharacterController cc = lucy.GetComponent<CharacterController>();
            cc.enabled = false;
            lucy.transform.position = new Vector3(0f, -30f, -5f);
            Physics.SyncTransforms();
            lucy.GetComponent<TankController>().enabled = false;
            yield return BotClock.Wait(0.2f);
            Enemy.CorpseCare care = null;
            t0 = BotClock.Now;
            while (care == null && BotClock.Now - t0 < 6f)
            {
                foreach (Enemy.CorpseCare c in FindObjectsByType<Enemy.CorpseCare>(FindObjectsSortMode.None)) if (c.Patient == victim.transform) care = c;
                if (care == null) { Enemy.CorpseCare mine = medic.GetComponent<Enemy.CorpseCare>(); if (mine != null && mine.Begin(victim.transform)) care = mine; }
                yield return null;
            }
            Transform carer = care != null ? care.transform : medic.transform;
            t0 = BotClock.Now;
            while (care != null && care.Current != Enemy.CorpseCare.Step.Drag && care.Current != Enemy.CorpseCare.Step.Cpr && BotClock.Now - t0 < 10f) yield return null;
            yield return BotClock.Wait(0.8f);
            yield return Shot("drag", carer.position + carer.right * 2.2f + carer.forward * 0.8f + Vector3.up * 1.5f, carer.position + Vector3.up * 0.9f);
            t0 = BotClock.Now;
            while (care != null && care.Current != Enemy.CorpseCare.Step.Cpr && BotClock.Now - t0 < 20f) yield return null;
            yield return BotClock.Wait(1.5f);
            yield return Shot("cpr_side", carer.position + carer.right * 2.0f + Vector3.up * 1.2f, carer.position + Vector3.up * 0.6f);
            yield return Shot("cpr_front", carer.position + carer.forward * 1.9f - carer.right * 0.9f + Vector3.up * 1.0f, carer.position + carer.forward * 0.4f + Vector3.up * 0.5f);
            Enemy.EnemyGun gun = carer.GetComponent<Enemy.EnemyGun>();
            Animator body = carer.GetComponent<Animator>();
            float fromHand = gun != null && body != null ? Vector3.Distance(gun.Model.position, body.GetBoneTransform(HumanBodyBones.RightHand).position) : -1f;
            Record("cpr_kneel_the_gun_off_the_hands", care != null && care.Current == Enemy.CorpseCare.Step.Cpr && (gun == null || fromHand > 0.25f), $"carer={carer.name} step={(care != null ? care.Current.ToString() : "none")} gun_from_hand={fromHand:0.00}");
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
                ShotRenderer.Render(cam, Path.Combine(BotArgs.RunDir, "shots", $"cpr_{(BotArgs.Level == "WardTest" ? "ward_" : "")}{shotIndex:00}_{moment}.png"), useTargetTexture: true);
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
            Debug.Log($"[CprBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("cpr_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
