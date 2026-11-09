using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Boss;
using GlorifiedBody.Cameras;
using GlorifiedBody.Combat;
using GlorifiedBody.Interaction;
using GlorifiedBody.Rooms;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;
using GlorifiedBody.Subtitles;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// The brain bot (2026-11-08): in BRAIN TEST she walks into the sector and the fight begins under the brain's eye. The
    /// aim follows her and she outruns its strike, a blast staggers it, a grabber pins her and a blast frees her, a second one she rips out with
    /// mk_orzabal and throws at a node, the work lights go out as the nodes die, and its death opens the exit. Shots:
    /// shots/brain_&lt;n&gt;_&lt;moment&gt;.png through the game camera (the eye). Active only when BotArgs.Bot == "brain":
    /// tools/unity.sh play BrainTest Main_Menu brain 300
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class BrainPlaythrough : MonoBehaviour
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

        public static bool IsRequested => string.Equals(BotArgs.Bot, "brain", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<BrainPlaythrough>() != null) return;
            var go = new GameObject("[BrainPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<BrainPlaythrough>();
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
            string line = $"[BrainBot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return BotClock.Wait(1f);
            Directory.CreateDirectory(Path.Combine(BotArgs.RunDir, "shots"));
            GameManager gm = GameManager.Instance;
            gm.LoadLevel("BrainTest");
            float t0 = BotClock.Now;
            while ((SceneManager.GetActiveScene().name != "BrainTest" || !gm.IsPlaying) && BotClock.Now - t0 < 30f) yield return null;
            yield return BotClock.Wait(0.5f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            padDriver = new GameObject("[BrainPad]").AddComponent<PadDriver>();
            DontDestroyOnLoad(padDriver.gameObject);
            padDriver.Pad = pad;
            tank = lucy.GetComponent<TankController>();
            interactor = lucy.GetComponent<Interactor>();
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            LockOnController lockOn = lucy.GetComponent<LockOnController>();
            lucy.GetComponentInChildren<Core.Health>().SetInvulnerable(true); // the bot shows the fight; the tests count the damage
            BrainFight fight = BrainFight.Instance;
            Room sector = Room.Of("B1");
            if (fight == null || sector == null) { Record("pieces_in_room", false, "no brain or sector (BrainTestSetup.BuildBrainTest)"); Finish(); yield break; }
            fight.firstGrab = 999f;
            foreach (PowerKind k in new[] { PowerKind.Blast, PowerKind.Telekinesis }) stats.FillMk(k);

            // 1. She steps in: the eye; the aim starts far and follows her.
            tank.Teleport(sector.Centre + new Vector3(0f, 0f, -2f), 0f);
            t0 = BotClock.Now;
            while (fight.Current != BrainFight.Phase.Fighting && BotClock.Now - t0 < 3f) yield return null;
            yield return BotClock.Wait(1.2f);
            yield return Shot("the_aim_follows");
            Record("the_fight_begins", fight.Current == BrainFight.Phase.Fighting && FixedCameraDirector.Instance.Current == fight.eye && fight.Aiming == BrainFight.AimState.Tracking,
                $"camera={FixedCameraDirector.Instance.CurrentName} aim={fight.Aiming}");

            // 2. She stands in the aim: the lock, the charge. She runs out before it fires.
            tank.Teleport(fight.Aim, 0f);
            t0 = BotClock.Now;
            while (fight.Aiming != BrainFight.AimState.Charging && BotClock.Now - t0 < 4f) yield return null;
            yield return BotClock.Wait(fight.chargeSeconds * 0.4f);
            yield return Shot("the_charge");
            int hits = fight.StrikeHits, strikes = fight.Strikes;
            tank.Teleport(fight.Aim + Vector3.right * 3.5f, 0f);
            t0 = BotClock.Now;
            while (fight.Strikes == strikes && BotClock.Now - t0 < 4f) yield return null;
            Record("she_outruns_the_strike", fight.Strikes > strikes && fight.StrikeHits == hits, $"strikes={fight.Strikes} hits={fight.StrikeHits}");

            // 2b. The next charge: a full blast on a node staggers it.
            t0 = BotClock.Now;
            while (fight.Aiming != BrainFight.AimState.Tracking && BotClock.Now - t0 < 4f) yield return null;
            tank.Teleport(fight.Aim, 0f);
            t0 = BotClock.Now;
            while (fight.Aiming != BrainFight.AimState.Charging && BotClock.Now - t0 < 4f) yield return null;
            powers.Select(PowerKind.Blast);
            pad.SetHeld(InputManager.GameAction.Charge, true); // her charge on the brain: the circle over her, the MK bar
            yield return BotClock.Wait(0.7f);
            yield return Shot("her_charge");
            // A close look at the one pose for every power (PowerArm): the arm up, the palm out.
            Vector3 chest = tank.transform.position + Vector3.up * 1.3f;
            yield return Shot("her_arm", chest + tank.transform.forward * 1.8f + tank.transform.right * 0.7f + Vector3.up * 0.1f, chest);
            pad.SetHeld(InputManager.GameAction.Charge, false);
            yield return BotClock.Wait(0.1f);
            if (fight.Aiming == BrainFight.AimState.Charging) powers.FireAt(powers.Selected, 1f, fight.nodes[0].GetComponent<LockOnTargetable>());
            yield return BotClock.Wait(0.2f);
            yield return Shot("staggered");
            Record("a_blast_staggers_it", fight.Aiming == BrainFight.AimState.Staggered && fight.Staggers == 1, $"aim={fight.Aiming} damage={fight.ChargeDamage:0}");

            // 3. A grabber pins her; a blast frees her.
            fight.trackSpeed = 0f; // the grabbers alone from here
            fight.ResetAim();
            Grabber g = fight.Grab();
            t0 = BotClock.Now;
            while (g != null && !g.Holding && BotClock.Now - t0 < 3f) yield return null;
            yield return BotClock.Wait(0.2f);
            yield return Shot("the_grabber_holds");
            bool held = g != null && g.Holding && tank.MovementLocked;
            powers.Select(PowerKind.Blast);
            powers.FireAt(powers.Selected, 0.6f, g.GetComponent<LockOnTargetable>());
            yield return BotClock.Wait(0.3f);
            Record("a_blast_frees_her", held && !tank.MovementLocked, $"held={held}");

            // 4. A second grabber: mk_orzabal rips it out and throws it at a node.
            g = fight.Grab();
            t0 = BotClock.Now;
            while (g != null && !g.Holding && BotClock.Now - t0 < 3f) yield return null;
            powers.Select(PowerKind.Telekinesis);
            lockOn.SetTarget(g.GetComponent<LockOnTargetable>());
            yield return null;
            pad.Tap(InputManager.GameAction.Charge);
            yield return BotClock.Wait(0.7f);
            yield return Shot("ripped_out");
            BrainNode node = fight.nodes.OrderBy(n => Vector3.Distance(n.transform.position, lucy.transform.position)).First();
            float hp0 = node.Health.CurrentHealth;
            lockOn.SetTarget(node.GetComponent<LockOnTargetable>());
            yield return null;
            powers.TryLaunch(1f);
            t0 = BotClock.Now;
            while (node.Health.CurrentHealth >= hp0 && BotClock.Now - t0 < 3f) yield return null;
            Record("the_thrown_grabber_hurts_a_node", node.Health.CurrentHealth < hp0, $"{hp0:0}>{node.Health.CurrentHealth:0}");

            // 5. Half its life: half the lights.
            foreach (BrainNode n in fight.nodes) n.Health.TakeDamage(Mathf.Max(0f, n.Health.CurrentHealth - n.Health.MaxHealth * 0.5f));
            yield return BotClock.Wait(0.3f);
            tank.Teleport(sector.Centre, 0f);
            yield return BotClock.Wait(0.3f);
            yield return Shot("half_the_lights");
            Record("the_lights_follow_its_life", fight.LightsOn == 3, $"lights={fight.LightsOn}");

            // 6. Its death: the flare, the lights out one by one, the exit open.
            foreach (BrainNode n in fight.nodes) n.Health.TakeDamage(n.Health.MaxHealth);
            yield return BotClock.Wait(0.2f);
            yield return Shot("the_flare");
            t0 = BotClock.Now;
            while (fight.Current != BrainFight.Phase.Dead && BotClock.Now - t0 < 6f) yield return null;
            yield return BotClock.Wait(0.3f);
            yield return Shot("dark_and_open");
            Record("its_death_opens_the_exit", fight.Current == BrainFight.Phase.Dead && !fight.exitLeaf.activeSelf && fight.LightsOn == 0, $"phase={fight.Current}");
            Finish();
        }

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
                ShotRenderer.Render(cam, Path.Combine(BotArgs.RunDir, "shots", $"brain_{shotIndex:00}_{moment}.png"), useTargetTexture: true);
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
            sb.Append("{\n  \"bot\": \"brain\",\n  \"pass\": ").Append(pass ? "true" : "false").Append(",\n  \"steps\": [\n");
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
            Debug.Log($"[BrainBot] finished pass={pass} steps={steps.Count} ok={steps.Count(s => s.ok)}");
            RunTelemetry.Event("brain_bot_done", pass ? "pass" : "fail");
        }

        private static string Esc(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
