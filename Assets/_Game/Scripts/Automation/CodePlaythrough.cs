using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;
using GlorifiedBody.Subtitles;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The code bot (2026-11-08): in CODE TEST she takes the card, opens store A and store B with it, the PA rotates the
    /// codes, the reader of the way on refuses the card, she reads the code on the day room wall, enrols the card at the
    /// terminal with the keypad (seven digits), and the way on opens.
    /// Shots: shots/code_&lt;n&gt;_&lt;moment&gt;.png (the HUD, the messages and the keypad over the room). Active only when
    /// BotArgs.Bot == "code": tools/unity.sh play CodeTest Main_Menu code 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class CodePlaythrough : MonoBehaviour
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

        public static bool IsRequested => string.Equals(BotArgs.Bot, "code", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<CodePlaythrough>() != null) return;
            var go = new GameObject("[CodePlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<CodePlaythrough>();
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
            string line = $"[CodeBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            Directory.CreateDirectory(Path.Combine(BotArgs.RunDir, "shots"));
            GameManager gm = GameManager.Instance;
            gm.LoadLevel("CodeTest");
            float t0 = BotClock.Now;
            while ((SceneManager.GetActiveScene().name != "CodeTest" || !gm.IsPlaying) && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(0.5f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            padDriver = new GameObject("[CodePad]").AddComponent<PadDriver>();
            DontDestroyOnLoad(padDriver.gameObject);
            padDriver.Pad = pad;
            tank = lucy.GetComponent<TankController>();
            interactor = lucy.GetComponent<Interactor>();
            LockedDoor[] doors = FindObjectsByType<LockedDoor>(FindObjectsSortMode.None);
            LockedDoor door = doors.FirstOrDefault(d => d.name == "Door_K6");
            LockedDoor storeA = doors.FirstOrDefault(d => d.name == "Door_K2"), storeB = doors.FirstOrDefault(d => d.name == "Door_K5");
            CodeTerminal terminal = FindFirstObjectByType<CodeTerminal>();
            CodeRotation rotation = FindFirstObjectByType<CodeRotation>();
            ItemPickup card = FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).FirstOrDefault(p => p.item != null && p.item.itemId == "code_test_card");
            if (door == null || storeA == null || storeB == null || terminal == null || rotation == null || card == null)
            {
                Record("pieces_in_room", false, "no doors, terminal, rotation or card (CodeTestSetup.BuildCodeTest)");
                Finish();
                yield break;
            }

            // 1. The card from the table.
            yield return Face(card.Point + Vector3.back * 0.9f, card.Point);
            yield return Press(InputManager.GameAction.Interact);
            yield return Press(InputManager.GameAction.Interact);
            Record("she_takes_the_card", interactor.Inventory.Has("code_test_card"), interactor.LastResult);

            // 1b. The common interaction: the table under the card shows her comment.
            yield return Press(InputManager.GameAction.Interact);
            yield return Shot("table_comment");
            Record("the_table_comments", interactor.LastResult.StartsWith("examined:") && interactor.Text.Length > 0, interactor.Text);
            yield return BotClock.Wait(Interactor.MessageSeconds + 0.3f);

            // 2. The card opens store A and store B, then the PA call 1B rotates the codes.
            yield return Face(storeA.Point + Vector3.back * 1.1f, storeA.Point);
            yield return Press(InputManager.GameAction.Interact);
            yield return Press(InputManager.GameAction.Interact);
            yield return BotClock.Wait(1f);
            Record("the_card_opens_store_a", storeA.IsOpen && !StoryFlags.Has(rotation.rotatedFlag), interactor.LastResult);
            yield return Face(storeB.Point + Vector3.back * 1.1f, storeB.Point);
            yield return Press(InputManager.GameAction.Interact);
            yield return Press(InputManager.GameAction.Interact);
            yield return BotClock.Wait(0.5f);
            Record("the_card_opens_store_b", storeB.IsOpen, interactor.LastResult);
            t0 = BotClock.Now;
            while (!StoryFlags.Has(rotation.rotatedFlag) && BotClock.Now - t0 < rotation.delaySeconds + 3f) yield return null;
            yield return BotClock.Wait(1.2f);
            // Through the room's own camera: the cull draws only the rooms that camera sees, so a free camera shows a black store.
            yield return Shot("pa_rotates_the_codes");
            Record("the_codes_rotate", StoryFlags.Has(rotation.rotatedFlag), $"opened={rotation.Opened}");
            yield return BotClock.Wait(2.2f);

            // 3. The reader of the way on refuses the card: red, the words, two bleats.
            yield return Face(door.Point + Vector3.forward * 1.1f, door.Point);
            yield return Press(InputManager.GameAction.Interact);
            yield return Shot("reader_refuses", door.Point + new Vector3(-1.6f, 0.6f, 2.6f), door.Point + Vector3.right * 0.6f);
            Record("the_reader_refuses_the_card", interactor.LastResult.StartsWith("rotated:") && !door.IsOpen && interactor.Text == LockedDoor.RotatedText, interactor.LastResult);
            yield return BotClock.Wait(2.2f);

            // 4. The code on the wall of the day room.
            Transform code = GameObject.Find("Sign_Code")?.transform;
            if (code != null)
            {
                yield return Face(code.position + Vector3.back * 2.2f, code.position);
                yield return Shot("the_code_on_the_wall", code.position + new Vector3(0.8f, 0.2f, -2.6f), code.position);
            }

            // 5. The terminal: the keypad, the code by the digit and cursor keys, the Interact key.
            yield return Face(terminal.Point, terminal.transform.position);
            yield return Press(InputManager.GameAction.Interact);
            bool opened = GlorifiedBody.UI.KeypadUI.IsOpen && gm.IsReading;
            if (opened)
            {
                foreach (char c in CodeTestDigits)
                {
                    int want = c - '0';
                    for (int k = 0; k < want; k++) yield return Press(InputManager.GameAction.Forward);
                    if (GlorifiedBody.UI.KeypadUI.Instance.Cursor < GlorifiedBody.UI.KeypadUI.Instance.Length - 1) yield return Press(InputManager.GameAction.TurnRight);
                }
                yield return Shot("keypad", terminal.Point + new Vector3(1.2f, 0.7f, -1.8f), terminal.transform.position);
                yield return Press(InputManager.GameAction.Interact);
                yield return Shot("keypad_enrolled", terminal.Point + new Vector3(1.2f, 0.7f, -1.8f), terminal.transform.position);
            }
            t0 = BotClock.Now;
            while (GlorifiedBody.UI.KeypadUI.IsOpen && BotClock.Now - t0 < 4f) yield return null;
            Record("the_keypad_enrols_the_card", opened && StoryFlags.Has(terminal.enrolledFlag) && !GlorifiedBody.UI.KeypadUI.IsOpen && gm.IsPlaying,
                $"digits={(GlorifiedBody.UI.KeypadUI.Instance != null ? GlorifiedBody.UI.KeypadUI.Instance.Digits : "-")} tries={terminal.Tries}");

            // 6. The reader is green, and the card opens the way on.
            yield return Face(door.Point + Vector3.forward * 1.1f, door.Point);
            yield return BotClock.Wait(0.3f);
            yield return Shot("reader_green", door.Point + new Vector3(-1.6f, 0.6f, 2.6f), door.Point + Vector3.right * 0.6f);
            Vector3 doorway = door.Point; // the point is the leaf, and the leaf slides away
            yield return Press(InputManager.GameAction.Interact);
            yield return Press(InputManager.GameAction.Interact);
            yield return BotClock.Wait(1.2f);
            yield return Shot("door_open", doorway + new Vector3(-1.6f, 0.6f, 2.6f), doorway + Vector3.back * 1.5f);
            Record("the_door_opens", door.IsOpen, interactor.LastResult);
            Finish();
        }

        private const string CodeTestDigits = "8675309";
        private TankController tank;
        private Interactor interactor;

        private IEnumerator Face(Vector3 point, Vector3 target)
        {
            point.y = 0f;
            Vector3 to = target - point;
            tank.Teleport(point, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            yield return null;
            yield return null;
        }

        private IEnumerator Press(InputManager.GameAction action)
        {
            pad.Tap(action);
            yield return null;
            yield return null;
            yield return null;
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
            // Every overlay on screen: the HUD, the subtitles, the Interact message and hint, the keypad.
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas && c.isActiveAndEnabled && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
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
                ShotRenderer.Render(cam, Path.Combine(BotArgs.RunDir, "shots", $"code_{shotIndex:00}_{moment}.png"), useTargetTexture: true);
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
            sb.Append("{\n  \"bot\": \"code\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[CodeBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("code_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
