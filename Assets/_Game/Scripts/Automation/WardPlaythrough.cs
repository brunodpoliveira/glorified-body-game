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
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The lab bot (2026-10-27, the design sweep's rank 6): in the LAB TEST room it shows the knife lab worker's run and
    /// strike, the gunman's shaking two-hand aim, his retreat at a fear of 3, the card that drops from the carrier,
    /// and the door it opens. Shots: shots/lab_&lt;n&gt;_&lt;moment&gt;.png. In a record run
    /// (tools/unity.sh record LabTest Main_Menu lab 300) each moment is a clip. Active only when BotArgs.Bot == "ward".
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class WardPlaythrough : MonoBehaviour
    {
        public const string SceneName = "WardTest";
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
        private string shots;
        private int shotIndex;
        private GameObject lucy;
        private static Camera closeCamera;

        public static bool IsRequested => string.Equals(BotArgs.Bot, "ward", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<WardPlaythrough>() != null) return;
            var go = new GameObject("[WardPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<WardPlaythrough>();
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
            string line = $"[WardBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            GameManager gm = GameManager.Instance;
            if (gm != null) gm.LoadLevel(SceneName); else SceneManager.LoadScene(SceneName);
            float t0 = BotClock.Now;
            while ((SceneManager.GetActiveScene().name != SceneName || gm == null || !gm.IsPlaying) && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(0.5f);
            shots = Path.Combine(BotArgs.RunDir, "shots");
            Directory.CreateDirectory(shots);

            lucy = GameObject.FindGameObjectWithTag("Player");
            GameObject patientObj = GameObject.Find("Patient_W1"), martaObj = GameObject.Find("Marta"), labObj = GameObject.Find("Lab_W3"), guardObj = GameObject.Find("Guard_W4");
            Record("loaded", lucy != null && patientObj != null && martaObj != null && labObj != null, $"spawned={EnemySpawn.Spawned} missing={EnemySpawn.Missing}");
            if (lucy == null || patientObj == null || martaObj == null || labObj == null) { Finish(); yield break; }
            Health hers = lucy.GetComponentInChildren<Health>();
            hers.SetInvulnerable(true);
            if (guardObj != null) guardObj.SetActive(false); // no shots in this bot: a hit stops a feed
            TankController tank = lucy.GetComponent<TankController>();
            var pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            padDriver = new GameObject("[WardPad]").AddComponent<PadDriver>();
            padDriver.Pad = pad;
            yield return BotClock.Wait(0.5f);

            // 1. The patient sits up in her bed and says her line when Lucy comes near.
            PatientBrain patient = patientObj.GetComponent<PatientBrain>();
            Vector3 bed = patientObj.transform.position;
            Vector3 bedView = new Vector3(bed.x + 2.6f, 1.7f, bed.z - 2.4f);
            ClipFixed("ward_01_patient", bedView, bed + Vector3.up * 0.4f);
            tank.Teleport(new Vector3(bed.x + 0.4f, 0f, bed.z - 1.6f), 330f);
            t0 = BotClock.Now;
            while (!patient.Spoke && BotClock.Now - t0 < 3f) yield return null;
            yield return BotClock.Wait(1.5f);
            ShotFixed("patient_bed", bedView, bed + Vector3.up * 0.4f);
            EndClip();
            Record("patient", patient.Spoke && Vector3.Distance(bed, patientObj.transform.position) < 0.1f, $"spoke={patient.Spoke} moved={Vector3.Distance(bed, patientObj.transform.position):0.00} m");

            // 2. The scan of Marta: her picture, and the flag marta_scanned.
            martaObj.GetComponent<CivilianBrain>().enabled = false; // she stands for the scan
            Vector3 front = martaObj.transform.position + martaObj.transform.forward * 1.1f;
            tank.Teleport(new Vector3(front.x, 0f, front.z), Quaternion.LookRotation(martaObj.transform.position - front).eulerAngles.y);
            yield return BotClock.Wait(0.3f);
            PsychicScanner scanner = lucy.GetComponent<PsychicScanner>();
            pad.Tap(InputManager.GameAction.Scan);
            yield return null;
            yield return null;
            yield return null;
            bool viewing = scanner.IsViewing && scanner.Viewing != null && scanner.Viewing.scanId == "marta";
            yield return new WaitForSecondsRealtime(PsychicScanner.MinViewSeconds + 0.3f);
            GlorifiedBody.UI.InteractionUI ui = FindFirstObjectByType<GlorifiedBody.UI.InteractionUI>();
            bool shown = ui != null && ui.ScanShown;
            if (ui != null && ui.Canvas != null) yield return GlorifiedBody.UI.OverlayShot.Capture(ui.Canvas, "ward_02_scan_marta.png", 0f);
            pad.Tap(InputManager.GameAction.Scan);
            yield return BotClock.Wait(0.3f);
            Record("scan", viewing && shown && StoryFlags.Has("marta_scanned"), $"viewing={viewing} shown={shown} flag={StoryFlags.Has("marta_scanned")}");
            martaObj.GetComponent<CivilianBrain>().enabled = true;

            // 3. Marta dies: the flag, and the mark on the north wall (the game remembers).
            GameObject mark = GameObject.Find("Gate_MartaKilled");
            Vector3 markPoint = mark != null && mark.transform.childCount > 0 ? mark.transform.GetChild(0).position : martaObj.transform.position + Vector3.up;
            Vector3 markView = new Vector3(markPoint.x + 1.5f, 2.2f, markPoint.z - 5.5f);
            ClipFixed("ward_03_marta", markView, (markPoint + martaObj.transform.position) * 0.5f);
            yield return BotClock.Wait(0.5f);
            Kill(martaObj, DamageType.PsychicBlast, 0.6f);
            yield return BotClock.Wait(2.5f);
            ShotFixed("marta_mark", markView, (markPoint + martaObj.transform.position) * 0.5f);
            EndClip();
            bool showing = mark != null && mark.GetComponent<GlorifiedBody.Story.FlagGate>().Showing;
            Record("marta_killed", StoryFlags.Has("marta_killed") && showing, $"flag={StoryFlags.Has("marta_killed")} mark={showing}");

            // 4. The feed: Lucy hurt, a dead lab worker, she crouches at the body and his HP flows into her.
            Kill(labObj, DamageType.PsychicBlast, 0.6f);
            yield return BotClock.Wait(2f);
            hers.SetHealth(30f);
            FeedableBody body = labObj.GetComponent<FeedableBody>();
            Feeding feeding = lucy.GetComponent<Feeding>();
            Vector3 p = body.Point;
            // Beside the body on the room side (the first record put her behind the south wall).
            Vector3 roomSide = new Vector3(-p.x, 0f, -p.z).normalized;
            Vector3 stand = new Vector3(p.x, 0f, p.z) + roomSide * 0.9f;
            tank.Teleport(stand, Quaternion.LookRotation(-roomSide).eulerAngles.y);
            yield return BotClock.Wait(0.3f);
            // From the middle of the room: the first record put this camera in the pillar by the south wall.
            Vector3 inward = new Vector3(-p.x, 0f, -p.z).normalized;
            Vector3 feedView = new Vector3(p.x, 0f, p.z) + Quaternion.Euler(0f, 35f, 0f) * inward * 2.6f + Vector3.up * 1.5f;
            Vector3 feedLook = new Vector3(p.x, 0.5f, p.z) + roomSide * 0.45f;
            ClipFixed("ward_04_feed", feedView, feedLook);
            yield return BotClock.Wait(0.4f);
            float hp0 = hers.CurrentHealth;
            string fed = body.Confirm(lucy.GetComponent<Interactor>());
            yield return BotClock.Wait(Feeding.FeedSeconds * 0.5f);
            ShotFixed("feed", feedView, feedLook);
            t0 = BotClock.Now;
            while (feeding.IsFeeding && BotClock.Now - t0 < 6f) yield return null;
            yield return BotClock.Wait(0.8f);
            EndClip();
            Record("feed", fed.StartsWith("feed:") && hers.CurrentHealth > hp0 + 30f, $"{fed} hp {hp0:0} -> {hers.CurrentHealth:0} ({feeding.LastEnd})");
            Finish();
        }

        private PadDriver padDriver;

        [DefaultExecutionOrder(-1000)]
        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad Pad;
            private void Update() => Pad?.Commit();
        }

        /// <summary>A kill by her: the damage, and the hit on the bus (the death push, the morale, the exp).</summary>
        private void Kill(GameObject figure, DamageType type, float charge)
        {
            Health h = figure.GetComponent<Health>();
            Vector3 chest = figure.transform.position + Vector3.up * 1.2f;
            h.TakeDamage(h.MaxHealth * 10f);
            HitEventBus.Raise(new HitInfo { Attacker = lucy, Victim = figure, HitTransform = figure.transform, HitPoint = chest,
                HitNormal = (lucy.transform.position - chest).normalized, DamageAmount = 999f, DamageType = type, Charge = charge, WasFatal = true });
        }

        /// <summary>The camera side: to the right of the line from her to the figure.</summary>
        private Vector3 Side(Transform figure)
        {
            Vector3 toFigure = Flat(figure.position - lucy.transform.position);
            if (toFigure.sqrMagnitude < 0.01f) return Vector3.right;
            return Vector3.Cross(Vector3.up, toFigure.normalized) - toFigure.normalized * 0.4f;
        }

        private static Camera CloseCamera()
        {
            if (closeCamera == null)
            {
                var go = new GameObject("[WardCamera]");
                DontDestroyOnLoad(go);
                closeCamera = go.AddComponent<Camera>();
                closeCamera.fieldOfView = 40f;
                closeCamera.nearClipPlane = 0.05f;
                closeCamera.enabled = false;
            }
            Camera main = Camera.main;
            if (main != null)
            {
                closeCamera.clearFlags = main.clearFlags;
                closeCamera.backgroundColor = main.backgroundColor;
            }
            return closeCamera;
        }

        private static float placeTurn; // the last clear turn from the wanted side: the camera keeps it while it sees

        /// <summary>The camera on the wanted side, turned around the target until no wall, cover or other figure blocks it (as the reactions bot).</summary>
        private static void Place(Camera cam, Transform target, Vector3 side, float distance, float height)
        {
            side.y = 0f;
            if (side.sqrMagnitude < 0.001f) side = Vector3.back;
            Vector3 point = target.position + Vector3.up * height;
            int mask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("Player", "BodyPart", "Ignore Raycast", "Debris");
            Health owner = target.GetComponentInParent<Health>();
            Transform own = owner != null ? owner.transform : target;
            Vector3 At(float turn) => point + Quaternion.Euler(0f, turn, 0f) * side.normalized * distance + Vector3.up * 0.4f;
            bool Clear(Vector3 at)
            {
                foreach (RaycastHit h in Physics.SphereCastAll(point, 0.15f, at - point, Vector3.Distance(point, at), mask, QueryTriggerInteraction.Ignore))
                    if (!h.collider.transform.IsChildOf(own)) return false;
                return true;
            }
            if (!Clear(At(placeTurn)))
            {
                foreach (float turn in new[] { 0f, 40f, -40f, 80f, -80f, 120f, -120f, 160f, -160f })
                    if (Clear(At(turn))) { placeTurn = turn; break; }
            }
            cam.transform.position = At(placeTurn);
            cam.transform.LookAt(point);
        }

        /// <summary>A clip through a close camera that follows the target (record runs only).</summary>
        private static void Clip(string name, Transform target, Func<Vector3> side, float distance, float height)
        {
            if (!FrameRecorder.Enabled) return;
            placeTurn = 0f;
            Camera cam = CloseCamera();
            FrameRecorder.CameraProvider = () =>
            {
                if (target == null) return null;
                Place(cam, target, side(), distance, height);
                return cam;
            };
            FrameRecorder.Ensure().Begin(name);
        }

        /// <summary>A clip through a camera that stays at one place (record runs only).</summary>
        private static void ClipFixed(string name, Vector3 at, Vector3 lookAt)
        {
            if (!FrameRecorder.Enabled) return;
            Camera cam = CloseCamera();
            FrameRecorder.CameraProvider = () =>
            {
                cam.transform.position = at;
                cam.transform.LookAt(lookAt);
                return cam;
            };
            FrameRecorder.Ensure().Begin(name);
        }

        private void ShotFixed(string moment, Vector3 at, Vector3 lookAt)
        {
            shotIndex++;
            Camera cam = CloseCamera();
            cam.transform.position = at;
            cam.transform.LookAt(lookAt);
            ShotRenderer.Render(cam, Path.Combine(shots, $"ward_{shotIndex:00}_{moment}.png"), ShotWidth, ShotHeight, useTargetTexture: true);
        }

        private static void EndClip()
        {
            if (FrameRecorder.Instance != null) FrameRecorder.Instance.End();
            FrameRecorder.CameraProvider = null;
        }

        private void Shot(string moment, Transform target, Vector3 side, float distance, float height)
        {
            if (target == null) return;
            shotIndex++;
            placeTurn = 0f;
            Camera cam = CloseCamera();
            Place(cam, target, side, distance, height);
            ShotRenderer.Render(cam, Path.Combine(shots, $"ward_{shotIndex:00}_{moment}.png"), ShotWidth, ShotHeight, useTargetTexture: true);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private void Finish()
        {
            if (finished) return;
            finished = true;
            if (padDriver != null) { GameInput.ClearBotSource(padDriver.Pad); Destroy(padDriver.gameObject); }
            EndClip();
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            var sb = new StringBuilder();
            sb.Append("{\n  \"bot\": \"ward\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[WardBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("ward_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
