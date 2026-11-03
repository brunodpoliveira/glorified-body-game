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
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The fire doors bot (2026-11-03): in DOOR TEST she pulls the north door's lever and the door jams; the two soldiers
    /// kick it down; the power armour breaks the east door; the guard and the scientist bang on the west door; the
    /// closet soldier stays behind the card door. Shots: shots/doors_&lt;n&gt;_&lt;moment&gt;.png, through the room camera and
    /// through cameras on both sides of each door. Active only when BotArgs.Bot == "doors":
    /// tools/unity.sh play DoorTest Main_Menu doors 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class DoorPlaythrough : MonoBehaviour
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

        public static bool IsRequested => string.Equals(BotArgs.Bot, "doors", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<DoorPlaythrough>() != null) return;
            var go = new GameObject("[DoorPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<DoorPlaythrough>();
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
            string line = $"[DoorBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            Directory.CreateDirectory(Path.Combine(BotArgs.RunDir, "shots"));
            GameManager gm = GameManager.Instance;
            gm.LoadLevel("DoorTest");
            float t0 = BotClock.Now;
            while ((SceneManager.GetActiveScene().name != "DoorTest" || !gm.IsPlaying) && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(0.8f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            padDriver = new GameObject("[DoorPad]").AddComponent<PadDriver>();
            DontDestroyOnLoad(padDriver.gameObject);
            padDriver.Pad = pad;
            FireDoor north = Door("D2"), east = Door("D3"), west = Door("D4");
            if (north == null || east == null || west == null) { Record("doors_in_room", false, "no FireDoor_D1_D2/D3/D4 (DoorTestSetup.BuildDoorTest)"); Finish(); yield break; }
            yield return Shot("hall");
            Record("every_door_open", FireDoor.All.All(d => d.Current == FireDoor.State.Open), string.Join(",", FireDoor.All.Select(d => d.name + ":" + d.Current)));

            // The east and west doors jammed from the start: a shot heard next door (Room.Hear) does not let those rooms in early.
            east.Jam("bot", instant: true);
            west.Jam("bot", instant: true);

            // 1. She pulls the north door's lever: the door shuts and jams.
            TankController tank = lucy.GetComponent<TankController>();
            DoorLever lever = north.GetComponentsInChildren<DoorLever>().First(l => l.name == "Lever_In");
            Vector3 stand = lever.transform.position - north.outward * 0.8f;
            stand.y = 0f;
            tank.Teleport(stand, Quaternion.LookRotation(north.outward).eulerAngles.y);
            yield return BotClock.Wait(0.5f);
            yield return Shot("north_open", new Vector3(6.6f, 1.8f, 5.0f), new Vector3(4.5f, 1.2f, 8f));
            yield return Shot("lever_up", lever.transform.position - north.outward * 1.0f + Vector3.up * 0.2f + new Vector3(0.4f, 0f, 0f), lever.transform.position);
            pad.Tap(InputManager.GameAction.Interact);
            yield return BotClock.Wait(1f);
            yield return Shot("north_jammed", new Vector3(6.6f, 1.8f, 5.0f), new Vector3(4.5f, 1.2f, 8f));
            yield return Shot("lever_down", lever.transform.position - north.outward * 1.0f + Vector3.up * 0.2f + new Vector3(0.4f, 0f, 0f), lever.transform.position);
            Record("her_lever_jams", north.Current == FireDoor.State.Jammed, north.Current.ToString());
            tank.Teleport(new Vector3(5f, 0f, 3f), 0f);

            // 2. The soldiers kick it down.
            SoldierBrain rifle = Figure("Soldier_Rifle"), pistol = Figure("Soldier_Pistol");
            rifle?.Alert("bot", callOthers: false);
            pistol?.Alert("bot", callOthers: false);
            t0 = BotClock.Now;
            while (north.Kicks < 2 && BotClock.Now - t0 < 25f) yield return null;
            yield return BotClock.Wait(0.25f);
            yield return Shot("north_kick_barracks", new Vector3(6.8f, 1.9f, 12.6f), new Vector3(5f, 1f, 8f));
            yield return Shot("north_kick_hall");
            Record("soldiers_kick", north.Kicks >= 2 && north.Bangs == 0, $"kicks={north.Kicks} bangs={north.Bangs}");
            t0 = BotClock.Now;
            while (north.Current != FireDoor.State.Broken && BotClock.Now - t0 < 25f) yield return null;
            yield return BotClock.Wait(0.5f);
            yield return Shot("north_broken", new Vector3(3.2f, 1.8f, 5.2f), new Vector3(5f, 0.8f, 8f));
            Record("soldiers_break_it", north.Current == FireDoor.State.Broken, $"kicks={north.Kicks}");
            yield return BotClock.Wait(1.2f); // through the hole, before a shot (a shot alerts the rooms next door)
            yield return Shot("north_come_through");
            rifle?.GetComponent<Health>()?.TakeDamage(9999f);
            pistol?.GetComponent<Health>()?.TakeDamage(9999f);

            // 3. The power armour: two blows on the east door.
            SoldierBrain armour = Figure("Armour");
            armour?.Alert("bot", callOthers: false);
            t0 = BotClock.Now;
            while (east.Kicks < 1 && BotClock.Now - t0 < 25f) yield return null;
            yield return BotClock.Wait(0.2f);
            yield return Shot("east_blow_bay", new Vector3(14.6f, 2.2f, 6.2f), new Vector3(10f, 1.2f, 4f));
            t0 = BotClock.Now;
            while (east.Current != FireDoor.State.Broken && BotClock.Now - t0 < 25f) yield return null;
            yield return BotClock.Wait(0.6f);
            yield return Shot("east_broken", new Vector3(6.6f, 1.8f, 2.2f), new Vector3(10f, 1f, 4f));
            Record("armour_two_blows", east.Current == FireDoor.State.Broken && east.Kicks == 2, $"kicks={east.Kicks}");
            armour?.GetComponent<Health>()?.TakeDamage(9999f);

            // 4. The guard and the scientist only bang on the west door.
            SoldierBrain guard = Figure("Guard"), scientist = Figure("Scientist");
            guard?.Alert("bot", callOthers: false);
            scientist?.Alert("bot", callOthers: false);
            t0 = BotClock.Now;
            while (west.Bangs < 2 && BotClock.Now - t0 < 25f) yield return null;
            yield return BotClock.Wait(0.1f);
            yield return Shot("west_bang_office", new Vector3(-4.6f, 1.9f, 6.4f), new Vector3(0f, 1.1f, 4f));
            yield return BotClock.Wait(4f);
            Record("guard_and_scientist_bang", west.Bangs >= 3 && west.Kicks == 0 && west.Current == FireDoor.State.Jammed, $"bangs={west.Bangs} kicks={west.Kicks} {west.Current}");

            // 5. The card door: the closet soldier stays behind it.
            LockedDoor card = FindObjectsByType<LockedDoor>(FindObjectsSortMode.None).FirstOrDefault();
            SoldierBrain closet = Figure("Closet_Soldier");
            closet?.Alert("bot", callOthers: false);
            yield return BotClock.Wait(5f);
            yield return Shot("card_door", new Vector3(7.2f, 1.8f, 3f), new Vector3(5f, 1f, 0f));
            Record("card_door_holds", card != null && !card.IsOpen && closet != null && !closet.AtDoor, card != null ? "open=" + card.IsOpen : "no card door");
            Finish();
        }

        private static FireDoor Door(string to) => FireDoor.All.FirstOrDefault(d => d.name == "FireDoor_D1_" + to);

        private static SoldierBrain Figure(string name) => FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == name);

        /// <summary>The same through a camera of its own at the place, looking at the point.</summary>
        private IEnumerator Shot(string moment, Vector3 from, Vector3 at)
        {
            Camera main = Camera.main;
            var obj = new GameObject("[ShotCamera]");
            Camera cam = obj.AddComponent<Camera>();
            if (main != null) cam.CopyFrom(main);
            cam.fieldOfView = 60f;
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
            ShotRenderer.Render(cam, Path.Combine(BotArgs.RunDir, "shots", $"doors_{shotIndex:00}_{moment}.png"), useTargetTexture: true);
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            if (padDriver != null) { GameInput.ClearBotSource(padDriver.Pad); Destroy(padDriver.gameObject); }
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            var sb = new StringBuilder();
            sb.Append("{\n  \"bot\": \"doors\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[DoorBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("doors_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
