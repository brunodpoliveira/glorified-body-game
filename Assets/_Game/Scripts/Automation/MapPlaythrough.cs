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
using GlorifiedBody.Interaction;
using GlorifiedBody.Map;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The map bot (Bruno, 2026-10-31): TRAVEL A alone on the map, TRAVEL B after the door, the floor plan that shows
    /// KIT TEST, the lab's locked door red then green, and the second floor after the ward's stairs. Shots:
    /// shots/map_&lt;n&gt;_&lt;moment&gt;.png (the map screen over the stopped room). Active only when BotArgs.Bot == "map":
    /// tools/unity.sh play TravelTest_A Main_Menu map 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class MapPlaythrough : MonoBehaviour
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
        private GameObject lucy;

        public static bool IsRequested => string.Equals(BotArgs.Bot, "map", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<MapPlaythrough>() != null) return;
            var go = new GameObject("[MapPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<MapPlaythrough>();
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
            string line = $"[MapBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            Directory.CreateDirectory(Path.Combine(BotArgs.RunDir, "shots"));
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            padDriver = new GameObject("[MapPad]").AddComponent<PadDriver>();
            DontDestroyOnLoad(padDriver.gameObject);
            padDriver.Pad = pad;

            // 1. TRAVEL A: only her room, the log and the pill, her place.
            yield return Load("TravelTest_A");
            yield return MapShot("travel_a");
            MapScreen map = MapScreen.Instance;
            Record("start_hidden", map.ShownRooms.SequenceEqual(new[] { "TravelTest_A" }) && map.HerPlace.HasValue && map.ShownMarks.Count >= 2,
                $"rooms={string.Join(",", map.ShownRooms)} marks={map.ShownMarks.Count}");
            yield return CloseMap();

            // 2. She takes the log, goes through the door: TRAVEL B shows, A stays (without the log).
            GameObject.Find("Log_Travel")?.GetComponent<ItemPickup>()?.Confirm(lucy.GetComponent<Interactor>());
            yield return BotClock.Wait(0.3f);
            yield return Cross("Exit_ToB", "TravelTest_B");
            yield return MapShot("travel_b");
            Record("room_by_room", map.ShownRooms.Count == 2 && !map.ShownRooms.Contains("KitTest") && !map.ShownMarks.Any(m => m.Path == "Log_Travel"),
                $"rooms={string.Join(",", map.ShownRooms)}");
            yield return CloseMap();

            // 3. The floor plan: KIT TEST shows before she goes there.
            GameObject.Find("Plan_TestFloor1")?.GetComponent<ItemPickup>()?.Confirm(lucy.GetComponent<Interactor>());
            yield return BotClock.Wait(0.3f);
            yield return MapShot("floor_plan");
            Record("floor_plan", map.ShownRooms.Contains("KitTest"), $"rooms={string.Join(",", map.ShownRooms)}");
            yield return CloseMap();

            // 4. The lab: the locked door red, then green.
            yield return Load("LabTest");
            yield return MapShot("lab_locked");
            bool red = map.ShownMarks.Any(m => m.Kind == MapMarkKind.LockedDoor && !m.Open);
            yield return CloseMap();
            GameObject.Find("Door_Store")?.GetComponent<LockedDoor>()?.Open(instant: true);
            yield return MapShot("lab_open");
            bool green = map.ShownMarks.Any(m => m.Kind == MapMarkKind.LockedDoor && m.Open);
            Record("locked_door", red && green, $"red={red} green={green}");
            yield return CloseMap();

            // 5. The ward, then its stairs to TRAVEL B: two floors, D shows the ward's.
            yield return Load("WardTest");
            yield return Cross("Exit_WardWest", "TravelTest_B");
            yield return OpenMap();
            int floors = map.Floors.Count;
            pad.Tap(InputManager.GameAction.TurnRight);
            yield return BotClock.Wait(0.3f);
            yield return OverlayShot(map, "floor_2");
            Record("two_floors", floors == 2 && map.ShownFloor == "test_2" && map.ShownRooms.Contains("WardTest"), $"floors={floors} shown={map.ShownFloor}");
            yield return CloseMap();
            Finish();
        }

        private IEnumerator Load(string scene)
        {
            GameManager gm = GameManager.Instance;
            gm.LoadLevel(scene);
            float t0 = BotClock.Now;
            while ((SceneManager.GetActiveScene().name != scene || !gm.IsPlaying) && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(0.5f);
            Hers();
        }

        private IEnumerator Cross(string exitName, string scene)
        {
            int arrivals = SaveSystem.Arrivals;
            RoomExit exit = GameObject.Find(exitName)?.GetComponent<RoomExit>();
            bool used = exit != null && exit.Use();
            float t0 = BotClock.Now;
            while ((SaveSystem.Arrivals <= arrivals || WorldState.Travelling) && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(0.5f);
            Hers();
            Record("cross_" + scene, used && SceneManager.GetActiveScene().name == scene, exitName);
        }

        private void Hers()
        {
            lucy = GameObject.FindGameObjectWithTag("Player");
            if (lucy != null) lucy.GetComponentInChildren<Health>()?.SetInvulnerable(true);
        }

        private IEnumerator OpenMap()
        {
            pad.Tap(InputManager.GameAction.Map);
            float t0 = BotClock.Now;
            while (!MapScreen.Instance.IsOpen && BotClock.Now - t0 < 3f) yield return null;
            yield return BotClock.Wait(0.2f);
        }

        private IEnumerator MapShot(string moment)
        {
            yield return OpenMap();
            yield return OverlayShot(MapScreen.Instance, moment);
        }

        private IEnumerator OverlayShot(MapScreen map, string moment)
        {
            shotIndex++;
            yield return GlorifiedBody.UI.OverlayShot.Capture(map.Canvas, $"map_{shotIndex:00}_{moment}.png", 0f);
        }

        private IEnumerator CloseMap()
        {
            pad.Tap(InputManager.GameAction.Map);
            float t0 = BotClock.Now;
            while (MapScreen.Instance.IsOpen && BotClock.Now - t0 < 3f) yield return null;
            yield return BotClock.Wait(0.2f);
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            if (padDriver != null) { GameInput.ClearBotSource(padDriver.Pad); Destroy(padDriver.gameObject); }
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            var sb = new StringBuilder();
            sb.Append("{\n  \"bot\": \"map\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[MapBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("map_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
