using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Combat;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The physics bot (2026-11-06): she lifts a box with the held defend key, the wheel raises it, the mouse takes it over the rail,
    /// and she drops it on the plate, by the virtual pad. Active only when BotArgs.Bot == "physics". Writes playthrough.json and
    /// shots/physics_&lt;n&gt;_&lt;moment&gt;.png. Batch: tools/unity.sh play PhysicsTest Main_Menu physics 200
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class PhysicsPlaythrough : MonoBehaviour
    {
        private const string SceneName = "PhysicsTest";
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

        public static bool IsRequested => string.Equals(BotArgs.Bot, "physics", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<PhysicsPlaythrough>() != null) return;
            var go = new GameObject("[PhysicsPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<PhysicsPlaythrough>();
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
            string line = $"[PhysicsBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
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
            yield return BotClock.Wait(1f);
            string shots = Path.Combine(BotArgs.RunDir, "shots");
            Directory.CreateDirectory(shots);
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);

            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            TankController tank = lucy.GetComponent<TankController>();
            GameObject box = GameObject.Find("Box_A");
            PressurePlate plate = FindAnyObjectByType<PressurePlate>();
            GameObject gate = GameObject.Find("Gate_Leaf");
            Record("room", box != null && plate != null && gate != null && gate.activeInHierarchy);
            powers.Select(PowerKind.Telekinesis);
            Vector3 to = box.transform.position - lucy.transform.position;
            tank.Teleport(lucy.transform.position, Quaternion.LookRotation(new Vector3(to.x, 0f, to.z)).eulerAngles.y);
            yield return null;
            Shot(Path.Combine(shots, "physics_01_room.png"));

            // The drop (Bruno, 2026-11-07): the lift by a press, the mouse over the other box, the defend press.
            GameObject under = GameObject.Find("Box_B");
            lucy.GetComponent<LockOnController>().SetTarget(box.GetComponent<LockOnTargetable>());
            pad.Tap(InputManager.GameAction.Charge);
            yield return BotClock.Wait(powers.Selected.liftSeconds + 0.2f);
            Record("press_lift", powers.HeldLift != null, powers.LastResult);
            yield return SteerOver(box, under.transform.position, 8f);
            yield return BotClock.Wait(0.4f);
            Shot(Path.Combine(shots, "physics_01b_held_over_the_box.png"));
            Vector3 atPress = box.transform.position;
            pad.Tap(InputManager.GameAction.Defend);
            yield return BotClock.Wait(1.5f);
            float edge = under.GetComponent<Collider>().bounds.size.y;
            Record("drop_stacks", box != null && powers.LastResult == "drop" && Mathf.Abs(box.transform.position.y - under.transform.position.y - edge) < 0.1f, $"press at {atPress}: A at {box.transform.position}, B at {under.transform.position}");
            Shot(Path.Combine(shots, "physics_01c_stacked.png"));

            pad.SetHeld(InputManager.GameAction.Defend, true);
            yield return Until(() => powers.Carrying, 2f);
            Record("lift", powers.Carrying);
            yield return BotClock.Wait(0.5f);
            pad.Scroll = 0.05f;
            yield return BotClock.Wait(0.5f);
            pad.Scroll = 0f;
            Vector3 point = plate.transform.position;
            Camera cam = Camera.main;
            float t0 = BotClock.Now;
            bool shotInAir = false;
            while (BotClock.Now - t0 < 6f)
            {
                Vector3 left = point - box.transform.position;
                left.y = 0f;
                if (left.magnitude < 0.08f) break;
                Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized, away = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                pad.Look = new Vector2(Mathf.Clamp(Vector3.Dot(left, right) * 3f, -1f, 1f), Mathf.Clamp(Vector3.Dot(left, away) * 3f, -1f, 1f));
                if (!shotInAir && box.transform.position.z > 1.5f)
                {
                    shotInAir = true;
                    Shot(Path.Combine(shots, "physics_02_over_the_rail.png"));
                }
                yield return null;
            }
            pad.Look = Vector2.zero;
            Record("over_the_plate", shotInAir, $"box at {box.transform.position}");
            pad.SetHeld(InputManager.GameAction.Defend, false);
            yield return Until(() => plate.Pressed, 4f);
            yield return BotClock.Wait(1f);
            Record("gate_open", plate.Pressed && !gate.activeInHierarchy, $"box at {box.transform.position}");
            Shot(Path.Combine(shots, "physics_03_gate_open.png"));
            pad.ReleaseAll();
            GameInput.ClearBotSource(pad);
            pad = null;
            Finish();
        }

        /// <summary>
        /// The mouse, until the box rests over the point. A pad value moves the hold point once a frame, so at the frame rate of a batch run a large
        /// value outruns the box (TelekinesisLift.MaxLead) and it goes on after the pad stops. So the value stays small.
        /// </summary>
        private IEnumerator SteerOver(GameObject box, Vector3 point, float seconds)
        {
            Camera cam = Camera.main;
            Rigidbody body = box.GetComponent<Rigidbody>();
            float t0 = BotClock.Now;
            while (BotClock.Now - t0 < seconds)
            {
                Vector3 left = Vector3.ProjectOnPlane(point - box.transform.position, Vector3.up);
                if (left.magnitude < 0.06f && body.linearVelocity.magnitude < 0.05f) break;
                Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized, away = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                pad.Look = new Vector2(Mathf.Clamp(Vector3.Dot(left, right) * 0.5f, -0.1f, 0.1f), Mathf.Clamp(Vector3.Dot(left, away) * 0.5f, -0.1f, 0.1f));
                yield return null;
            }
            pad.Look = Vector2.zero;
        }

        private void Update() => pad?.Commit();

        private VirtualPad pad;

        /// <summary>What the player sees, from the camera of the room.</summary>
        private static void Shot(string path)
        {
            Camera cam = Camera.main;
            if (cam != null) ShotRenderer.Render(cam, path, ShotWidth, ShotHeight, useTargetTexture: true);
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
            Debug.Log($"[PhysicsBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("physics_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
