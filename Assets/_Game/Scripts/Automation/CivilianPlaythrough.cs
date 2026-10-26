using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The civilian bot (Bruno, 2026-10-25: "another test room to test the unarmed civvies, then put in code for the
    /// game over screen"): loads the civilian test room from the menu and shoots the moments: the office at work,
    /// the civilians backing away from her, the guard called by the scream, a civilian begging at her feet, the nurse
    /// cornered in the alcove. Then she kills a lab worker: the others run and cower, and the nurse drags the body and
    /// does CPR in her sight (Bruno, 2026-10-25). Then the guard kills her, and the death screen shows his entry
    /// (death_ui.png). Active only when BotArgs.Bot == "civilians". Writes playthrough.json and
    /// shots/civ_&lt;n&gt;_&lt;moment&gt;.png. Batch: tools/unity.sh play CivilianTest Main_Menu civilians 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class CivilianPlaythrough : MonoBehaviour
    {
        public const string SceneName = "CivilianTest";
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

        public static bool IsRequested => string.Equals(BotArgs.Bot, "civilians", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<CivilianPlaythrough>() != null) return;
            var go = new GameObject("[CivilianPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<CivilianPlaythrough>();
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
            string line = $"[CivilianBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            GameManager gm = GameManager.Instance;
            if (gm != null) gm.LoadLevel(SceneName); else SceneManager.LoadScene(SceneName);
            float t0 = BotClock.Now;
            while (SceneManager.GetActiveScene().name != SceneName && BotClock.Now - t0 < 30f) yield return null;
            string shots = Path.Combine(BotArgs.RunDir, "shots");
            Directory.CreateDirectory(shots);

            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            CivilianBrain[] civilians = FindObjectsByType<CivilianBrain>(FindObjectsSortMode.None).OrderBy(c => c.name).ToArray();
            SoldierBrain guard = FindFirstObjectByType<SoldierBrain>();
            Record("civ_loaded", SceneManager.GetActiveScene().name == SceneName && lucy != null && civilians.Length == 3 && guard != null,
                $"civilians={civilians.Length} guard={(guard != null ? guard.name : "none")}");
            if (lucy == null || civilians.Length == 0 || guard == null) { Finish(); yield break; }
            Health health = lucy.GetComponentInChildren<Health>();
            health.SetInvulnerable(true);
            TankController tank = lucy.GetComponent<TankController>();
            Shot(Path.Combine(shots, "civ_01_office.png"));

            // She steps in: they back away slowly, facing her, and tremble (Bruno, 2026-10-25). Nobody runs yet.
            CivilianBrain lab = civilians.First(c => c.name == "Lab_A");
            CivilianBrain victim = civilians.First(c => c.name == "Lab_B");
            CivilianBrain nurse = civilians.First(c => c.name == "Nurse_C");
            Vector3 middle = new Vector3(-2.5f, 0f, 0.5f);
            tank.Teleport(middle, 0f);
            t0 = BotClock.Now;
            while (lab.Current != CivilianBrain.Phase.Wary && BotClock.Now - t0 < 8f) yield return null;
            yield return BotClock.Wait(1.2f);
            Vector3 chest = lab.transform.position + Vector3.up * 0.9f;
            CloseShot(chest, Quaternion.Euler(0f, -50f, 0f) * ToMiddle(lab.transform.position), Path.Combine(shots, "civ_02_wary.png"), 3.2f, 0.6f);
            Record("civ_wary", lab.Current == CivilianBrain.Phase.Wary && !civilians.Any(c => c.Panicked) && lab.Tremble.LastDegrees > 0f,
                string.Join(" ", civilians.Select(c => $"{c.name}:{c.Current}/{c.AlertReason}")) + $" tremble={lab.Tremble.LastDegrees:0.0}");

            // The guard heard the scream.
            t0 = BotClock.Now;
            while (!guard.Alerted && BotClock.Now - t0 < 6f) yield return null;
            Record("guard_called", guard.Alerted, $"{guard.name}: {guard.AlertReason}");

            // She walks up: it begs.
            tank.Teleport(lab.transform.position + ToMiddle(lab.transform.position) * 1.6f, 0f);
            t0 = BotClock.Now;
            while (lab.Current != CivilianBrain.Phase.Beg && BotClock.Now - t0 < 3f) yield return null;
            yield return BotClock.Wait(0.4f);
            chest = lab.transform.position + Vector3.up * 0.9f;
            CloseShot(chest, Quaternion.Euler(0f, 60f, 0f) * ToMiddle(lab.transform.position), Path.Combine(shots, "civ_03_beg.png"), 2.6f, 0.6f);
            Record("civ_beg", lab.Current == CivilianBrain.Phase.Beg && lab.Begs > 0, $"{lab.name} {lab.Current} begs={lab.Begs}");

            // The nurse in the alcove: she stands at its mouth, and it has nowhere to go.
            tank.Teleport(new Vector3(5.4f, 0f, 2.1f), 0f);
            yield return BotClock.Wait(1.5f);
            CloseShot(nurse.transform.position + Vector3.up * 0.9f, Vector3.back + Vector3.left * 0.6f, Path.Combine(shots, "civ_04_cornered.png"), 2.4f, 0.6f);
            Record("civ_cornered", nurse.Current == CivilianBrain.Phase.Beg, $"{nurse.name} {nurse.Current} begs={nurse.Begs}");

            // She is 10 m from Lab_A now: it stands and watches her in a fear stance (an RFab preset).
            t0 = BotClock.Now;
            while (!(lab.CurrentPose == CivilianBrain.Pose.Shake || lab.CurrentPose == CivilianBrain.Pose.Hug) && BotClock.Now - t0 < 6f) yield return null;
            yield return BotClock.Wait(0.8f);
            CloseShot(lab.transform.position + Vector3.up * 0.9f, Quaternion.Euler(0f, -40f, 0f) * ToMiddle(lab.transform.position), Path.Combine(shots, "civ_04b_wary_stance.png"), 2.6f, 0.4f);
            Record("civ_stance", lab.CurrentPose == CivilianBrain.Pose.Shake || lab.CurrentPose == CivilianBrain.Pose.Hug,
                $"{lab.name} {lab.Current}/{lab.CurrentPose} clip={(lab.PoseOverride.Current != null ? lab.PoseOverride.Current.name : "none")}");
            lab.SetVariant(1); // its cower: the back flat on a wall

            // She kills Lab_B with a full mk_warspite: the witnesses panic, run and cower.
            tank.Teleport(middle, 0f);
            yield return BotClock.Wait(0.8f);
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            LockOnController lockOn = lucy.GetComponent<LockOnController>();
            stats.DebugSet(PsychicStats.MaxNalcon, 0f);
            powers.Select(PowerKind.Blast);
            lockOn.SetTarget(victim.GetComponent<GlorifiedBody.Combat.LockOnTargetable>());
            powers.Release(1f);
            yield return BotClock.Wait(0.2f);
            Health victimHealth = victim.GetComponent<Health>();
            string how = victimHealth.IsDead ? "blast" : "forced";
            if (!victimHealth.IsDead) victimHealth.TakeDamage(victimHealth.MaxHealth * 10f);
            t0 = BotClock.Now;
            while (lab.Current != CivilianBrain.Phase.Flee && BotClock.Now - t0 < 2f) yield return null;
            yield return BotClock.Wait(0.4f);
            Shot(Path.Combine(shots, "civ_05_they_run.png"));
            Record("civ_panic", lab.Panicked && victimHealth.IsDead, $"kill={how} {lab.name} {lab.Current}/{lab.PanicReason}");

            t0 = BotClock.Now;
            while (lab.Current != CivilianBrain.Phase.Cower && BotClock.Now - t0 < 12f) yield return null;
            yield return BotClock.Wait(0.8f);
            chest = lab.transform.position + Vector3.up * 0.9f;
            Vector3 view = lab.CurrentPose == CivilianBrain.Pose.Lean ? Quaternion.Euler(0f, 35f, 0f) * lab.Lean.WallNormal : ToMiddle(lab.transform.position);
            CloseShot(chest, view, Path.Combine(shots, "civ_06_cower.png"), 2.6f, 0.4f);
            Record("civ_cower", lab.Current == CivilianBrain.Phase.Cower && lab.CurrentPose == CivilianBrain.Pose.Lean,
                $"{lab.name} {lab.Current}/{lab.CurrentPose} lean={lab.Lean.Weight:0.00} hips gap={lab.Lean.LastHipsGap:0.00} m at {lab.transform.position.x:0.0},{lab.transform.position.z:0.0}");

            // The nurse tends the body with her in plain sight: the drag, then the CPR.
            CorpseCare care = nurse.GetComponent<CorpseCare>();
            t0 = BotClock.Now;
            while (care.Current != CorpseCare.Step.Drag && BotClock.Now - t0 < 15f) yield return null;
            yield return BotClock.Wait(0.8f);
            if (care.Patient != null) { Where(care.Patient, "drag patient"); Where(nurse.transform, "drag nurse"); CloseShot(BodyPoint(care.Patient), ToMiddle(BodyPoint(care.Patient)), Path.Combine(shots, "civ_07_nurse_drag.png"), 3.2f, 1.2f); }
            t0 = BotClock.Now;
            while (care.Current != CorpseCare.Step.Cpr && BotClock.Now - t0 < 15f) yield return null;
            yield return BotClock.Wait(1f);
            if (care.Patient != null) CloseShot(BodyPoint(care.Patient), ToMiddle(BodyPoint(care.Patient)), Path.Combine(shots, "civ_08_nurse_cpr.png"), 3.2f, 1.2f);
            Record("civ_nurse_care", care.Current == CorpseCare.Step.Cpr && care.Patient == victim.transform && care.Dragged > 1f,
                $"{nurse.name} {care.Current} dragged={care.Dragged:0.0} m patient={(care.Patient != null ? care.Patient.name : "none")}");

            // She walks up to Lab_A on its wall: it slides down to the floor, then begs from there (Bruno, 2026-10-25).
            bool sat = false, begFloor = false;
            if (lab.CurrentPose == CivilianBrain.Pose.Lean)
            {
                Vector3 wallOut = lab.Lean.WallNormal;
                tank.Teleport(OnMesh(lab.transform.position + wallOut * 4.2f), Quaternion.LookRotation(-wallOut).eulerAngles.y);
                t0 = BotClock.Now;
                while (lab.CurrentPose != CivilianBrain.Pose.Sit && BotClock.Now - t0 < 3f) yield return null;
                sat = lab.CurrentPose == CivilianBrain.Pose.Sit;
                yield return BotClock.Wait(1.2f);
                CloseShot(lab.transform.position + Vector3.up * 0.6f, Quaternion.Euler(0f, 35f, 0f) * wallOut, Path.Combine(shots, "civ_08b_slide_down.png"), 2.4f, 0.5f);
                tank.Teleport(OnMesh(lab.transform.position + wallOut * 2.2f), Quaternion.LookRotation(-wallOut).eulerAngles.y);
                t0 = BotClock.Now;
                while (lab.Current != CivilianBrain.Phase.Beg && BotClock.Now - t0 < 3f) yield return null;
                yield return BotClock.Wait(0.8f);
                begFloor = lab.Current == CivilianBrain.Phase.Beg && lab.CurrentPose == CivilianBrain.Pose.Sit;
                Where(lab.transform, "beg on the floor");
                CloseShot(lab.transform.position + Vector3.up * 0.6f, Quaternion.Euler(0f, -35f, 0f) * wallOut, Path.Combine(shots, "civ_08c_beg_on_floor.png"), 2.4f, 0.5f);
            }
            Record("civ_slide_beg", sat && begFloor, $"{lab.name} sat={sat} beg on the floor={begFloor} now {lab.Current}/{lab.CurrentPose}");

            // Its plea as a subtitle at the bottom, with the speaker's name (Bruno, 2026-10-25).
            Bark.Say(lab.gameObject, CivilianBrain.LineBeg);
            yield return null;
            string subtitle = GlorifiedBody.Subtitles.SubtitleUI.Instance != null ? string.Join(" | ", GlorifiedBody.Subtitles.SubtitleUI.Instance.Shown) : "";
            if (GlorifiedBody.Subtitles.SubtitleUI.Instance != null)
                yield return OverlayShot.Capture(GlorifiedBody.Subtitles.SubtitleUI.Instance.GetComponent<Canvas>(), "civ_08d_subtitle.png", 0.05f);
            Record("civ_subtitle", subtitle.Contains("LAB WORKER: " + CivilianBrain.LineBeg), $"'{subtitle}'");

            // The short near the nurse: the start freezes the game while Lucy shows pain, then each shock arcs to its
            // victim, zaps and stuns it (Bruno, 2026-10-25).
            ShortController shortController = lucy.GetComponent<ShortController>();
            // On the room side of the nurse, so the camera from the middle of the room sees her.
            Vector3 stand = OnMesh(nurse.transform.position + ToMiddle(nurse.transform.position) * 2.5f);
            Vector3 toNurse = nurse.transform.position - stand;
            toNurse.y = 0f;
            tank.Teleport(stand, Quaternion.LookRotation(toNurse).eulerAngles.y);
            yield return BotClock.Wait(0.3f);
            stats.DebugSet(stats.Nalcon, PsychicStats.MaxShort);
            shortController.Begin();
            yield return new WaitForSecondsRealtime(0.25f);
            bool frozen = Time.timeScale < 0.01f;
            CloseShot(lucy.transform.position + Vector3.up * 1.1f, Quaternion.Euler(0f, 25f, 0f) * ToMiddle(lucy.transform.position), Path.Combine(shots, "civ_09_short_pain.png"), 2.4f, 0.3f);
            bool screamed = shortController.Screams > 0;
            yield return new WaitForSecondsRealtime(1.6f);
            CloseShot(lucy.transform.position + Vector3.up * 1.1f, Quaternion.Euler(0f, 25f, 0f) * ToMiddle(lucy.transform.position), Path.Combine(shots, "civ_09b_short_pain_late.png"), 2.4f, 0.3f);
            bool noShockInPain = shortController.ShocksFired == 0;
            yield return BotClock.WaitWorld();
            float r0 = Time.realtimeSinceStartup;
            while (ShockArc.Alive == 0 && Time.realtimeSinceStartup - r0 < 2f) yield return null;
            Shot(Path.Combine(shots, "civ_10_short_arc.png"));
            Health shocked = shortController.Victim;
            // The nearest one is the victim: the nurse, or the guard when he comes closer.
            bool stunned = shocked != null && (shocked.IsDead
                || (shocked.GetComponent<CivilianBrain>() is CivilianBrain sc && sc.Stunned)
                || (shocked.GetComponent<DummyShooter>() is DummyShooter gun && gun.Current == DummyShooter.State.Staggered));
            Record("short_start_and_arc", frozen && screamed && noShockInPain && shortController.StartPains > 0 && shortController.Arcs > 0 && shortController.Zaps > 0 && stunned,
                $"frozen={frozen} scream={screamed} no shock in the pain={noShockInPain} pains={shortController.StartPains} arcs={shortController.Arcs} zaps={shortController.Zaps} victim={(shocked != null ? shocked.name : "none")} stunned={stunned}");
            shortController.End(byDrug: false);
            yield return BotClock.Wait(0.5f);

            // The guard kills her: the death screen shows his entry.
            tank.Teleport(new Vector3(2f, 0f, -4f), 90f);
            health.SetInvulnerable(false);
            health.SetSecondWindEnabled(false);
            health.SetHealth(5f);
            t0 = BotClock.Now;
            while (GameManager.Instance != null && !GameManager.Instance.IsGameOver && BotClock.Now - t0 < 40f) yield return null;
            yield return new WaitForSecondsRealtime(1.2f); // the screen's own shot (death_ui.png) takes 0.3 s
            DeathMenuUI screen = FindFirstObjectByType<DeathMenuUI>();
            bool shown = screen != null && screen.Shown != null;
            if (GlorifiedBody.Subtitles.SubtitleUI.Instance != null)
                yield return OverlayShot.Capture(GlorifiedBody.Subtitles.SubtitleUI.Instance.GetComponent<Canvas>(), "death_subtitle.png", 0.05f);
            Record("death_screen", GameManager.Instance != null && GameManager.Instance.IsGameOver && shown && DeathMenuUI.LastKillerId == "guard_pistol",
                $"killer={DeathMenuUI.LastKillerId} entry={(shown ? screen.Shown.killerId : "none")} caption='{(screen != null ? screen.CaptionShown : "")}'");
            Finish();
        }

        /// <summary>The nearest NavMesh point: a teleport target never inside a wall.</summary>
        private static Vector3 OnMesh(Vector3 point)
        {
            return UnityEngine.AI.NavMesh.SamplePosition(point, out UnityEngine.AI.NavMeshHit hit, 2f, UnityEngine.AI.NavMesh.AllAreas) ? hit.position : point;
        }

        /// <summary>Where a figure is (the log, for the shots): its root, its hips, the middle of its mesh, a civilian's plea.</summary>
        private static void Where(Transform figure, string what)
        {
            Animator a = figure.GetComponent<Animator>();
            Transform hips = a != null && a.isHuman ? a.GetBoneTransform(HumanBodyBones.Hips) : null;
            var skin = figure.GetComponentInChildren<SkinnedMeshRenderer>();
            CivilianBrain brain = figure.GetComponent<CivilianBrain>();
            if (brain != null && a != null) Debug.Log($"[CivilianBot] {figure.name}: {brain.Current}/{brain.CurrentPose} variant {brain.Variant} pleads {brain.PleadsWithHands} plea on {brain.Plea.On} weight {brain.Plea.Weight:0.00} hand y {a.GetBoneTransform(HumanBodyBones.RightHand).position.y:0.00} elbow y {a.GetBoneTransform(HumanBodyBones.RightLowerArm).position.y:0.00} shoulder y {a.GetBoneTransform(HumanBodyBones.RightUpperArm).position.y:0.00} frame {Time.frameCount}");
            Debug.Log($"[CivilianBot] where {what}: {figure.name} root {figure.position} hips {(hips != null ? hips.position.ToString() : "-")} mesh {(skin != null ? skin.bounds.center.ToString() : "-")} shown {(skin != null && skin.isVisible)} active {figure.gameObject.activeInHierarchy}");
        }

        /// <summary>The body's hips (a ragdoll lies where its bones are), half a metre up.</summary>
        private static Vector3 BodyPoint(Transform body)
        {
            EnemyRagdoll ragdoll = body.GetComponent<EnemyRagdoll>();
            Vector3 at = ragdoll != null && ragdoll.Hips != null ? ragdoll.Hips.position : body.position;
            return new Vector3(at.x, body.position.y + 0.5f, at.z);
        }

        private static Vector3 ToMiddle(Vector3 from)
        {
            Vector3 d = -from;
            d.y = 0f;
            return d.sqrMagnitude > 0.01f ? d.normalized : Vector3.back;
        }

        /// <summary>A 1080p shot of the room camera with the HUD drawn through it.</summary>
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

        /// <summary>
        /// The camera spot: the wanted side first, then turned more and more, until no wall stands between it and
        /// the point (a corner put the camera behind the side wall, 2026-10-25).
        /// </summary>
        private static Vector3 ClearSpot(Vector3 point, Vector3 facing, float distance, float height)
        {
            int walls = LayerMask.GetMask("Default");
            int solid = LayerMask.GetMask("Default", "Enemy", "Player"); // not inside a wall or a figure
            foreach (float turn in new[] { 0f, 35f, -35f, 70f, -70f, 110f, -110f, 180f })
            {
                Vector3 spot = point + Quaternion.Euler(0f, turn, 0f) * facing * distance + Vector3.up * height;
                if (!Physics.Linecast(spot, point, walls, QueryTriggerInteraction.Ignore) && !Physics.CheckSphere(spot, 0.35f, solid, QueryTriggerInteraction.Ignore)) return spot;
            }
            return point + facing * distance + Vector3.up * height;
        }

        /// <summary>A close shot from a temporary camera: it stands back from the point and looks at it.</summary>
        private static void CloseShot(Vector3 point, Vector3 facing, string path, float distance = 1.4f, float height = 0.25f)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
            var go = new GameObject("[CloseShotCamera]");
            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.05f;
            Camera main = Camera.main;
            if (main != null)
            {
                cam.clearFlags = main.clearFlags;
                cam.backgroundColor = main.backgroundColor;
            }
            go.transform.position = ClearSpot(point, facing.normalized, distance, height);
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
            sb.Append("{\n  \"bot\": \"civilians\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[CivilianBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("civilians_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
