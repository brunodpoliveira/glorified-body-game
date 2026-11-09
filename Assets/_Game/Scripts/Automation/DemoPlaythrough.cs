using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Cameras;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The demo bot (the greybox of floors 2 and 1, 2026-10-31): since 2026-11-02 the demo is one world scene, loaded
    /// once. The bot puts Lucy in every room in turn (her room changes, no scene loads), holds each fixed camera of the
    /// room and saves a 1080p shot, so Bruno sees each room with its people in place. Lucy cannot be hurt during the
    /// tour. Active only when BotArgs.Bot == "demo". Writes playthrough.json and shots/demo_&lt;camera&gt;.png to
    /// BotArgs.RunDir.
    /// Batch: tools/unity.sh play Demo Main_Menu demo 900
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class DemoPlaythrough : MonoBehaviour
    {
        public const int ShotWidth = 1920, ShotHeight = 1080;
        public static readonly string[] Rooms =
        {
            "W1", "W1B", "W2", "W3", "W4", "W5", "W6", "W7", "W8", "W9", "W10", "W11", "W12", "W13", "W14", "W15", "W16", "W17", "S1",
            "G1", "G2", "G3", "G4", "G5", "G6", "G7", "G8", "G9", "G9b", "G10", "G11", "G12", "G13", "G14", "G15", "G16", "X1", "R1", "R2", "R3", "U1", "B1", "B2",
        };

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

        public static bool IsRequested => string.Equals(BotArgs.Bot, "demo", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<DemoPlaythrough>() != null) return;
            var go = new GameObject("[DemoPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<DemoPlaythrough>();
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
            string line = $"[DemoBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1f);
            string shotsDir = Path.Combine(BotArgs.RunDirOrData, "shots");
            int loads = 0;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> count = (scene, mode) => loads++;
            if (SceneManager.GetActiveScene().name != World)
            {
                GameManager.Instance.LoadLevel(World);
                float t0 = Time.realtimeSinceStartup;
                while ((SceneManager.GetActiveScene().name != World || !GameManager.Instance.IsPlaying) && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            }
            yield return new WaitForSecondsRealtime(0.5f);
            SceneManager.sceneLoaded += count;
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            Health health = lucy != null ? lucy.GetComponentInChildren<Health>() : null;
            health?.SetInvulnerable(true);
            FixedCameraDirector director = FixedCameraDirector.Instance;
            Record("world", SceneManager.GetActiveScene().name == World && lucy != null && director != null && GlorifiedBody.Rooms.Room.All.Count == Rooms.Length && EnemySpawn.Missing == 0,
                $"scene={SceneManager.GetActiveScene().name} rooms={GlorifiedBody.Rooms.Room.All.Count} figures={FindObjectsByType<FeedableBody>(FindObjectsSortMode.None).Length} missing={EnemySpawn.Missing}");
            // The opening: the wake plays first (2026-11-04), and the scientist waits in the next room, W1B (2026-11-05).
            if (lucy != null)
            {
                float t2 = Time.realtimeSinceStartup;
                while (!GlorifiedBody.Story.CutscenePlayer.IsPlaying && Time.realtimeSinceStartup - t2 < 5f) yield return null;
                Record("opens_on_the_wake", GlorifiedBody.Story.CutscenePlayer.Current == "01_the_wake", GlorifiedBody.Story.CutscenePlayer.Current);
                GlorifiedBody.Story.CutscenePlayer.Skip();
                while (GlorifiedBody.Story.CutscenePlayer.IsPlaying && Time.realtimeSinceStartup - t2 < 15f) yield return null;
                yield return new WaitForSecondsRealtime(0.8f);
                ShotRenderer.Render(Camera.main, Path.Combine(shotsDir, "demo_meet_0_safe_room.png"), ShotWidth, ShotHeight, useTargetTexture: true);
                lucy.GetComponent<GlorifiedBody.Player.TankController>().Teleport(new Vector3(4f, 0f, 18.4f), 180f);
                yield return new WaitForSecondsRealtime(0.4f);
                ShotRenderer.Render(Camera.main, Path.Combine(shotsDir, "demo_meet_0b_the_doorway.png"), ShotWidth, ShotHeight, useTargetTexture: true);
                lucy.GetComponent<GlorifiedBody.Player.TankController>().Teleport(new Vector3(4f, 0f, 17.9f), 180f);
                yield return new WaitForSecondsRealtime(1.5f);
                ShotRenderer.Render(Camera.main, Path.Combine(shotsDir, "demo_meet_1_he_comes.png"), ShotWidth, ShotHeight, useTargetTexture: true);
                t2 = Time.realtimeSinceStartup;
                while (!GlorifiedBody.Story.CutscenePlayer.IsPlaying && Time.realtimeSinceStartup - t2 < 5f) yield return null;
                Record("the_talk_in_w1b", GlorifiedBody.Story.CutscenePlayer.Current == "02_the_scientist", GlorifiedBody.Story.CutscenePlayer.Current);
                GlorifiedBody.Story.CutscenePlayer.Skip();
                while (GlorifiedBody.Story.CutscenePlayer.IsPlaying && Time.realtimeSinceStartup - t2 < 15f) yield return null;
                yield return new WaitForSecondsRealtime(1.2f); // he steps in
                ShotRenderer.Render(Camera.main, Path.Combine(shotsDir, "demo_meet_2_after_the_talk.png"), ShotWidth, ShotHeight, useTargetTexture: true);
            }
            foreach (string id in Rooms)
            {
                GlorifiedBody.Rooms.Room room = GlorifiedBody.Rooms.Room.Of(id);
                if (room == null || lucy == null || director == null) { Record($"room_{id}", false, "no such room"); continue; }
                Vector3 at = FreePoint(room);
                lucy.GetComponent<GlorifiedBody.Player.TankController>().Teleport(at, lucy.transform.eulerAngles.y);
                float t1 = Time.realtimeSinceStartup;
                while (GlorifiedBody.Rooms.RoomTracker.CurrentId != id && Time.realtimeSinceStartup - t1 < 3f) yield return null;
                yield return new WaitForSecondsRealtime(0.3f);
                FixedCamera[] cameras = room.GetComponentsInChildren<FixedCamera>().OrderBy(c => c.cameraName).ToArray();
                Record($"room_{id}", GlorifiedBody.Rooms.RoomTracker.CurrentId == id && cameras.Length > 0 && loads == 0,
                    $"here={GlorifiedBody.Rooms.RoomTracker.CurrentId} cameras={cameras.Length} loads={loads} culled={GlorifiedBody.Rooms.RoomTracker.Instance?.CulledCount}");
                foreach (FixedCamera fixedCamera in cameras)
                {
                    director.Override(fixedCamera);
                    yield return null;
                    yield return null;
                    string file = Path.Combine(shotsDir, $"demo_{fixedCamera.cameraName.ToLowerInvariant()}.png");
                    bool saved = ShotRenderer.Render(Camera.main, file, ShotWidth, ShotHeight, useTargetTexture: true);
                    Record($"shot_{fixedCamera.cameraName.ToLowerInvariant()}", saved, $"saved={saved}");
                    if (BotArgs.ShotWait > 0f) yield return new WaitForSecondsRealtime(BotArgs.ShotWait); // a browser driver takes the screen now
                }
                director.Override(null);
            }
            SceneManager.sceneLoaded -= count;
            Record("one_load", loads == 0, $"scene loads during the tour={loads}");
            // The last card: its hint and its own end (Bruno, 2026-11-05: "it needs to exit after the demo is complete").
            GlorifiedBody.Story.Bulletin.Show(15);
            float t3 = Time.realtimeSinceStartup;
            while (!GlorifiedBody.Story.Bulletin.HintShown && Time.realtimeSinceStartup - t3 < 20f) yield return null;
            yield return GlorifiedBody.UI.OverlayShot.Capture(GlorifiedBody.Story.Bulletin.Card, "demo_bulletin.png", 0.2f);
            Record("bulletin_hint", GlorifiedBody.Story.Bulletin.HintShown, GlorifiedBody.Story.Bulletin.Shown.Length.ToString());
            t3 = Time.realtimeSinceStartup;
            while (GlorifiedBody.Story.Bulletin.IsShowing && Time.realtimeSinceStartup - t3 < 15f) yield return null;
            Record("bulletin_closes_itself", !GlorifiedBody.Story.Bulletin.IsShowing, $"{Time.realtimeSinceStartup - t3:0.0} s");
            GameManager.Instance.LoadMainMenu();
            Finish();
        }

        private const string World = "Demo";

        /// <summary>A floor point of the room clear of its props, nearest its middle (a ramp too: the ray finds the floor).</summary>
        private static Vector3 FreePoint(GlorifiedBody.Rooms.Room room)
        {
            int solid = LayerMask.GetMask("Default");
            Vector3 c = room.Centre;
            float w = room.rect.z - room.rect.x, d = room.rect.w - room.rect.y;
            for (float r = 0f; r <= Mathf.Max(w, d) * 0.5f; r += 0.5f)
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    Vector3 top = new Vector3(c.x + Mathf.Cos(a) * r, room.BaseY + 2.5f, c.z + Mathf.Sin(a) * r);
                    if (!room.Contains(new Vector3(top.x, room.BaseY, top.z), -0.5f)) continue;
                    if (!Physics.Raycast(top, Vector3.down, out RaycastHit hit, 10f, solid, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.point.y > room.BaseY + 0.3f) continue; // the top of a counter or a desk (she stood on the W9 counter)
                    if (Physics.CheckCapsule(hit.point + Vector3.up * 0.4f, hit.point + Vector3.up * 1.5f, 0.35f, solid, QueryTriggerInteraction.Ignore)) continue;
                    return hit.point;
                    }
            return c;
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            var sb = new StringBuilder();
            sb.Append("{\n  \"bot\": \"demo\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
            for (int i = 0; i < steps.Count; i++)
            {
                Step s = steps[i];
                sb.Append("    {\"name\": \"").Append(Esc(s.name)).Append("\", \"ok\": ").Append(s.ok ? "true" : "false")
                  .Append(", \"t\": ").Append(s.t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(", \"detail\": \"").Append(Esc(s.detail)).Append("\"}").Append(i < steps.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            string dir = BotArgs.RunDirOrData;
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "playthrough.json"), sb.ToString());
            }
            Debug.Log($"[DemoBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            // A player run (the Linux player, the Windows player under Wine) ends here; the editor runner and the web page stop it themselves.
            if (!Application.isEditor && Application.platform != RuntimePlatform.WebGLPlayer) Application.Quit(pass ? 0 : 1);
            RunTelemetry.Event("demo_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
