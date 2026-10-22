using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Cameras;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Automation
{
    /// <summary>
    /// Structural bot: proves the menu loop is wired. It does not play well, it calls the game's own
    /// methods. Build order step 1: main menu, start, training room ready, pause round trip, forced death,
    /// death screen, restart, back to the main menu. Later steps add the room and the mechanics.
    /// Active only when BotArgs.Bot == "structural". Writes playthrough.json to BotArgs.RunDir.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class StructuralPlaythrough : MonoBehaviour
    {
        private VirtualPad pad;
        [Serializable]
        private class Step
        {
            public string name;
            public bool ok;
            public string detail;
            public float t;
        }

        private readonly List<Step> steps = new List<Step>();
        private readonly List<string> eventLog = new List<string>();
        private float startTime;
        private float maxFrameMs;
        private float measureFrom = float.MaxValue; // frame times count only after the scene load settles
        private bool finished;

        public static bool IsRequested => string.Equals(BotArgs.Bot, "structural", StringComparison.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (!IsRequested) return;
            if (FindFirstObjectByType<StructuralPlaythrough>() != null) return;
            var go = new GameObject("[StructuralPlaythrough]");
            DontDestroyOnLoad(go);
            go.AddComponent<StructuralPlaythrough>();
        }

        private void OnEnable() => RunTelemetry.OnEvent += OnTelemetry;
        private void OnDisable() => RunTelemetry.OnEvent -= OnTelemetry;

        private void OnTelemetry(string name, string detail)
        {
            if (name == "frame_stats") return;
            eventLog.Add(name + (string.IsNullOrEmpty(detail) ? string.Empty : ":" + detail));
        }

        private void Start()
        {
            startTime = Time.unscaledTime;
            StartCoroutine(Run());
            StartCoroutine(Watchdog());
        }

        private void Update()
        {
            pad?.Commit();
            if (Time.unscaledTime >= measureFrom) maxFrameMs = Mathf.Max(maxFrameMs, Time.unscaledDeltaTime * 1000f);
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
            string line = $"[Bot] {(ok ? "OK  " : "FAIL")} {name} {detail}";
            if (ok) Debug.Log(line); else Debug.LogWarning(line);
        }

        private IEnumerator Run()
        {
            yield return WaitReal(1f);
            string level = string.IsNullOrEmpty(BotArgs.Level) ? "TrainingRoom" : BotArgs.Level;
            GameManager gm = GameManager.Instance;

            // 1. Main menu, then start.
            if (SceneManager.GetActiveScene().name == "Main_Menu")
            {
                bool menuUi = FindFirstObjectByType<MainMenuUI>() != null;
                Record("menu_loaded", gm != null && menuUi, $"GameManager={gm != null} MainMenuUI={menuUi}");

                // 1b. The how-to-play panel: shown, shot, hidden. Saved as how_to_play_ui.png (the rows must fit).
                HowToPlayUI howTo = FindFirstObjectByType<HowToPlayUI>();
                bool howToShown = false;
                if (howTo != null)
                {
                    howTo.Show();
                    yield return null;
                    yield return null;
                    howToShown = howTo.IsVisible;
                    Transform howToCanvas = howTo.transform.Find("HowToPlayCanvas");
                    if (howToShown && howToCanvas != null) yield return OverlayShot.Capture(howToCanvas.GetComponent<Canvas>(), "how_to_play_ui.png", 0.2f);
                    howTo.Hide();
                    yield return null;
                }
                Record("how_to_play_ui", howTo != null && howToShown && !howTo.IsVisible, $"found={howTo != null} shown={howToShown}");

                if (gm != null) gm.LoadLevel(level);
                else SceneManager.LoadScene(level);
                yield return WaitForScene(level, 30f);
                Record("start", SceneManager.GetActiveScene().name == level, level);
                if (SceneManager.GetActiveScene().name != level) { Finish(); yield break; }
            }
            else
            {
                Record("start", true, "started in " + SceneManager.GetActiveScene().name);
            }

            // 2. Gameplay ready: the state is Playing and the player exists with a Health.
            gm = GameManager.Instance;
            yield return WaitUntilReal(() => gm != null && gm.IsPlaying, 5f);
            GameObject player = FindPlayer(out Health playerHealth);
            bool ready = gm != null && gm.IsPlaying && player != null && playerHealth != null;
            Record("gameplay_ready", ready, $"state={gm?.CurrentState} player={(player != null ? player.name : "none")} health={playerHealth != null}");
            if (!ready) { Finish(); yield break; }
            measureFrom = Time.unscaledTime + 1f;
            RunTelemetry.Event("bot_assist", "on");
            yield return WaitReal(0.5f);
            CaptureShot("play_training_room");

            // 2a. The cast (2026-10-21): Lucy's model with its Humanoid animator, the two dummies with theirs, the
            // six figures along the north wall. cast_lineup.png from a camera in front of the line-up.
            Animator lucyAnimator = player.GetComponentInChildren<Animator>();
            bool lucyHuman = lucyAnimator != null && lucyAnimator.avatar != null && lucyAnimator.avatar.isHuman;
            int dummiesWithModels = 0;
            foreach (string dummyName in new[] { "Dummy_A", "Dummy_B" })
            {
                GameObject dummy = GameObject.Find(dummyName);
                if (dummy != null && dummy.GetComponentInChildren<SkinnedMeshRenderer>() != null) dummiesWithModels++;
            }
            int castFigures = 0;
            foreach (SkinnedMeshRenderer skin in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
            {
                if (skin.transform.root.name.StartsWith("Cast_")) castFigures++;
            }
            // The shooter rests for the two shots: its laser crossed the east shot (the camera stands next to it).
            GameObject dummyAObject = GameObject.Find("Dummy_A");
            DummyShooter castShooter = dummyAObject != null ? dummyAObject.GetComponent<DummyShooter>() : null;
            bool shooterWasOn = castShooter != null && castShooter.enabled;
            if (castShooter != null) castShooter.enabled = false;
            yield return null;
            CaptureLineupShot("cast_lineup_west", -2.4f); // the power armour, Lucy guard, the soldier, the patient
            CaptureLineupShot("cast_lineup_east", 2.0f);  // the nurse, the lab worker, the fireman, the guard in front
            if (castShooter != null) castShooter.enabled = shooterWasOn;
            Record("cast", lucyHuman && dummiesWithModels == 2 && castFigures == 6, $"lucyHuman={lucyHuman} dummies={dummiesWithModels} cast={castFigures}");

            // 2b. Step 2: tank controls, dodge, camera cuts and the lock-on, driven through the virtual pad.
            yield return MovementSteps(player, playerHealth);

            // 3. Pause round trip: the pause menu shows on Pause() and hides on Resume().
            gm.Pause();
            yield return null;
            yield return null;
            GameObject pauseCanvas = GameObject.Find("PauseMenuUICanvas");
            bool pauseShown = gm.IsPaused && pauseCanvas != null && pauseCanvas.activeSelf;
            if (pauseShown) yield return OverlayShot.Capture(pauseCanvas.GetComponent<Canvas>(), "pause_ui.png", 0.2f);
            gm.Resume();
            yield return null;
            yield return null;
            bool pauseHidden = gm.IsPlaying && (pauseCanvas == null || !pauseCanvas.activeSelf);
            Record("pause_roundtrip", pauseShown && pauseHidden, $"shown={pauseShown} hidden={pauseHidden} timeScale={Time.timeScale:0.00}");

            // 3b. Options, Controls tab: the remap rows, one per GameAction. Saved as options_controls.png.
            OptionsMenuUI options = FindFirstObjectByType<OptionsMenuUI>();
            bool optionsShown = false;
            int remapRows = 0;
            if (options != null)
            {
                options.Show();
                yield return null;
                var tabField = typeof(OptionsMenuUI).GetField("controlsTabBtn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var controlsTab = tabField?.GetValue(options) as UnityEngine.UI.Button;
                controlsTab?.onClick.Invoke();
                yield return null;
                yield return null;
                GameObject optionsCanvas = GameObject.Find("OptionsMenuCanvas");
                optionsShown = options.IsVisible && optionsCanvas != null;
                if (optionsShown)
                {
                    remapRows = InputManager.Instance != null ? InputManager.Instance.GetAllBindings().Count : 0;
                    yield return OverlayShot.Capture(optionsCanvas.GetComponent<Canvas>(), "options_controls.png", 0.2f);
                }
                options.Hide();
                yield return null;
            }
            int actions = System.Enum.GetValues(typeof(InputManager.GameAction)).Length; // one remap row per action (15 with Scan, 2026-10-22)
            Record("options_controls", optionsShown && remapRows == actions, $"shown={optionsShown} remapRows={remapRows} actions={actions}");

            // 4. Forced death: lethal damage through Health, then the game-over state.
            playerHealth.SetSecondWindEnabled(false);
            playerHealth.SetInvulnerable(false);
            playerHealth.SetRuntimeInvulnerable(false);
            playerHealth.TakeDamage(playerHealth.MaxHealth * 10f);
            yield return WaitUntilReal(() => gm.IsGameOver, 8f);
            Record("force_death", gm.IsGameOver && playerHealth.IsDead, $"dead={playerHealth.IsDead} state={gm.CurrentState} timeScale={Time.timeScale:0.00}");

            // 5. Death screen: the overlay is visible. DeathMenuUI saves death_ui.png itself.
            GameObject deathCanvas = null;
            yield return WaitUntilReal(() => (deathCanvas = GameObject.Find("DeathMenuUICanvas")) != null && deathCanvas.activeSelf, 3f);
            bool deathShown = deathCanvas != null && deathCanvas.activeSelf;
            Record("death_screen", deathShown, deathShown ? "DeathMenuUICanvas active" : "DeathMenuUICanvas not visible");
            yield return WaitReal(0.6f);

            // 6. Restart: no checkpoint in step 1, so the scene reloads and the player is alive again.
            if (!gm.RestartFromLatestCheckpoint()) gm.RestartLevel();
            yield return WaitUntilReal(() => gm.IsPlaying && SceneManager.GetActiveScene().name == level, 10f);
            yield return WaitReal(0.5f);
            player = FindPlayer(out playerHealth);
            bool restarted = gm.IsPlaying && player != null && playerHealth != null && !playerHealth.IsDead;
            Record("restart", restarted, $"state={gm.CurrentState} dead={(playerHealth != null ? playerHealth.IsDead.ToString() : "no health")} timeScale={Time.timeScale:0.00}");

            // 7. Back to the main menu with a clean time scale.
            gm.LoadMainMenu();
            yield return WaitUntilReal(() => SceneManager.GetActiveScene().name == "Main_Menu" && gm.IsInMenu, 10f);
            bool backInMenu = SceneManager.GetActiveScene().name == "Main_Menu" && gm.IsInMenu && Mathf.Approximately(Time.timeScale, 1f);
            Record("main_menu_return", backInMenu, $"scene={SceneManager.GetActiveScene().name} state={gm.CurrentState} timeScale={Time.timeScale:0.00}");

            Finish();
        }

        private IEnumerator MovementSteps(GameObject player, Health playerHealth)
        {
            TankController tank = player.GetComponent<TankController>();
            LockOnController lockOn = player.GetComponent<LockOnController>();
            FixedCameraDirector director = FixedCameraDirector.Instance;
            if (tank == null || lockOn == null || director == null)
            {
                Record("step2_components", false, $"tank={tank != null} lockOn={lockOn != null} director={director != null}");
                yield break;
            }

            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            tank.Teleport(new Vector3(0f, 1f, 0f), 0f);
            // Let the game clock settle after the load hitches, then measure in game time (WaitGame).
            yield return WaitReal(0.5f);
            yield return WaitGame(0.2f);

            // Walk forward for one second.
            Clip("tank_controls");
            Vector3 start = player.transform.position;
            Vector3 forward = player.transform.forward;
            pad.SetHeld(InputManager.GameAction.Forward, true);
            yield return WaitGame(1f);
            pad.ReleaseAll();
            yield return null;
            float along = Vector3.Dot(player.transform.position - start, forward);
            Record("walk_forward", along >= 0.8f, $"{along:0.00} m along the facing in 1 s (walk {tank.WalkSpeed} m/s)");
            yield return Linger(0.8f);

            // Turn right for half a second.
            float yaw0 = tank.Yaw;
            pad.SetHeld(InputManager.GameAction.TurnRight, true);
            yield return WaitGame(0.5f);
            pad.ReleaseAll();
            yield return null;
            float turned = Mathf.DeltaAngle(yaw0, tank.Yaw);
            Record("turn_right", turned >= 45f, $"{turned:0} deg in 0.5 s (turn {tank.TurnSpeed} deg/s)");
            yield return Linger(0.8f);

            // Dodge: a dash with invulnerability frames.
            Vector3 beforeDodge = player.transform.position;
            pad.Tap(InputManager.GameAction.Dodge);
            yield return null;
            yield return null;
            bool dodging = tank.IsDodging;
            bool invulnerable = playerHealth.IsInvulnerable;
            yield return WaitGame(0.6f);
            float dodged = Vector3.Distance(beforeDodge, player.transform.position);
            Record("dodge", dodging && invulnerable && dodged >= 1f, $"dodging={dodging} invulnerable={invulnerable} moved={dodged:0.00} m");
            yield return Linger(1f);
            EndClip();

            // Camera cuts: into the north-east zone, the south-west zone, back to the wide camera.
            Clip("camera_cuts");
            tank.Teleport(new Vector3(3f, 1f, 3f), 180f);
            yield return WaitUntilReal(() => director.CurrentName == "Cam_NorthEast", 1.5f);
            string camA = director.CurrentName;
            yield return WaitReal(0.3f);
            CaptureShot("cam_northeast");
            yield return Linger(1.5f);
            tank.Teleport(new Vector3(-3f, 1f, -3f), 0f);
            yield return WaitUntilReal(() => director.CurrentName == "Cam_SouthWest", 1.5f);
            string camB = director.CurrentName;
            yield return WaitReal(0.3f);
            CaptureShot("cam_southwest");
            yield return Linger(1.5f);

            // The edge arrow from the room's own camera (Bruno, 2026-10-20: no camera angle hid the dummy).
            // Cam_SouthWest looks west along the south wall, so Dummy_A is outside the frame here. While it
            // aims, the HUD points at it from the screen edge. hud_edge_arrow.png.
            GameObject edgeDummy = GameObject.Find("Dummy_A");
            DummyShooter edgeShooter = edgeDummy != null ? edgeDummy.GetComponent<DummyShooter>() : null;
            PlayerHud edgeHud = FindFirstObjectByType<PlayerHud>();
            bool shooterHidden = false;
            if (Camera.main != null && edgeShooter != null)
            {
                Vector3 screen = Camera.main.WorldToScreenPoint(edgeShooter.transform.position + Vector3.up);
                shooterHidden = !(screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height);
            }
            yield return WaitUntilReal(() => edgeHud != null && edgeHud.ActiveThreatArrows >= 1, 6f);
            bool arrow = edgeHud != null && edgeHud.ActiveThreatArrows >= 1;
            if (arrow && edgeHud.Canvas != null) yield return OverlayShot.Capture(edgeHud.Canvas, "hud_edge_arrow.png", 0.05f);
            Record("edge_arrow", shooterHidden && arrow, $"hidden={shooterHidden} arrows={edgeHud?.ActiveThreatArrows} shooter={edgeShooter?.Current}");

            tank.Teleport(new Vector3(0f, 1f, 0f), 0f);
            yield return WaitUntilReal(() => director.CurrentName == "Cam_Wide", 1.5f);
            string camC = director.CurrentName;
            Record("camera_cut", camA == "Cam_NorthEast" && camB == "Cam_SouthWest" && camC == "Cam_Wide", $"{camA} > {camB} > {camC}");
            yield return Linger(1.5f);
            EndClip();

            // Lock-on: the nearest dummy, then the switch.
            Clip("lock_on_charge");
            yield return WaitUntilReal(() => lockOn.HasTarget, 1.5f);
            string first = lockOn.TargetName;
            pad.Tap(InputManager.GameAction.SwitchTarget);
            yield return null;
            yield return null;
            string second = lockOn.TargetName;
            Record("lock_on", lockOn.HasTarget && first != "none" && second != first, $"nearest={first} switched={second}");
            yield return WaitReal(0.3f);
            CaptureShot("lock_on");
            yield return Linger(1f);

            // Charge: she stands still and faces the target.
            Vector3 beforeCharge = player.transform.position;
            pad.SetHeld(InputManager.GameAction.Charge, true);
            pad.SetHeld(InputManager.GameAction.Forward, true);
            yield return WaitGame(0.6f);
            float moved = Vector3.Distance(beforeCharge, player.transform.position);
            float angle = 999f;
            if (lockOn.CurrentTarget != null)
            {
                Vector3 to = lockOn.CurrentTarget.GetLockPoint() - player.transform.position;
                to.y = 0f;
                angle = Vector3.Angle(player.transform.forward, to);
            }
            Record("charge_faces_target", lockOn.IsCharging && moved < 0.05f && angle < 8f, $"charging={lockOn.IsCharging} moved={moved:0.00} m angle={angle:0}");
            yield return Linger(1f);
            pad.ReleaseAll();
            yield return null;
            yield return null;
            yield return Linger(1f);
            EndClip();

            // Step 3: the shooter's telegraph, a charged blast, the kill and the SHORT bar, a drug, the short.
            yield return PowerSteps(player, playerHealth, tank, lockOn);

            GameInput.ClearBotSource(pad);
            pad = null;
        }

        private IEnumerator PowerSteps(GameObject player, Health playerHealth, TankController tank, LockOnController lockOn)
        {
            PsychicStats stats = player.GetComponent<PsychicStats>();
            PsychicPowers powers = player.GetComponent<PsychicPowers>();
            DrugInventory drugs = player.GetComponent<DrugInventory>();
            ShortController shortController = player.GetComponent<ShortController>();
            GameObject dummyObject = GameObject.Find("Dummy_A");
            DummyShooter shooter = dummyObject != null ? dummyObject.GetComponent<DummyShooter>() : null;
            if (stats == null || powers == null || drugs == null || shortController == null || shooter == null)
            {
                Record("step3_components", false, $"stats={stats != null} powers={powers != null} drugs={drugs != null} short={shortController != null} shooter={shooter != null}");
                yield break;
            }

            // The telegraph: the dummy aims at her, the circle spins and shrinks, then it fires.
            Clip("telegraph");
            tank.Teleport(new Vector3(0f, 1f, 0f), 0f);
            // On video: start with the aim (the laser), so the clip shows aim, telegraph, shot and impact.
            if (FrameRecorder.Enabled) yield return WaitUntilReal(() => shooter.Current == DummyShooter.State.Aiming, 5f);
            yield return WaitUntilReal(() => shooter.Current == DummyShooter.State.Telegraph, 6f);
            bool telegraph = shooter.Current == DummyShooter.State.Telegraph;
            if (telegraph)
            {
                yield return WaitReal(0.3f);
                CaptureShot("telegraph");
                // The circle lives on the HUD now (over her), so the overlay shot shows the heads-up. hud_telegraph.png.
                PlayerHud telegraphHud = FindFirstObjectByType<PlayerHud>();
                if (telegraphHud != null && telegraphHud.Canvas != null) yield return OverlayShot.Capture(telegraphHud.Canvas, "hud_telegraph.png", 0.05f);
            }
            yield return WaitUntilReal(() => shooter.ShotsFired >= 1, 3f);
            Record("telegraph", telegraph && shooter.ShotsFired >= 1, $"telegraph={telegraph} shots={shooter.ShotsFired} hp={playerHealth.CurrentHealth:0}");
            yield return Linger(1.5f);
            EndClip();

            // A full-circle blast on Dummy_A (the lock may still sit on Dummy_B after the switch).
            Clip("blast_kill");
            lockOn.SetTarget(dummyObject.GetComponent<LockOnTargetable>());
            yield return null;
            powers.Select(PowerKind.Blast);
            stats.DebugSet(100f, 0f);
            Health dummyHealth = dummyObject.GetComponent<Health>();
            float dummyHp0 = dummyHealth.CurrentHealth;
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return WaitGame(powers.Selected.fullChargeTime * 0.7f);
            CaptureShot("charge");
            yield return WaitGame(powers.Selected.fullChargeTime * 0.5f);
            float charge = powers.Charge;
            pad.ReleaseAll();
            yield return null;
            yield return null;
            float dealt = dummyHp0 - dummyHealth.CurrentHealth;
            Record("blast_full_charge", charge >= 0.99f && powers.LastResult == "fired" && dealt >= powers.Selected.baseDamage - 0.5f,
                $"charge={charge:0.00} result={powers.LastResult} dealt={dealt:0} nalcon={stats.Nalcon:0}");

            // Finish it: the kill fills the SHORT bar by 25.
            float shortBefore = stats.Short;
            int guard = 0;
            while (!dummyHealth.IsDead && guard++ < 10)
            {
                powers.FireAt(powers.Selected, 1f, lockOn.CurrentTarget != null && lockOn.TargetName == "Dummy_A" ? lockOn.CurrentTarget : dummyObject.GetComponent<LockOnTargetable>());
                yield return null;
            }
            yield return null;
            Record("kill_fills_short", dummyHealth.IsDead && stats.Short >= shortBefore + 24f, $"dead={dummyHealth.IsDead} short {shortBefore:0} -> {stats.Short:0} kills={stats.Kills}");
            yield return Linger(1.5f);
            EndClip();

            // A drug from the floor.
            Clip("drug_pickup");
            GameObject pickup = GameObject.Find("Drug_Nalcon");
            if (pickup != null && FrameRecorder.Enabled)
            {
                // On video: stand next to it first, then step onto it.
                tank.Teleport(pickup.transform.position + Vector3.up * 0.6f - Vector3.forward * 1.2f, 0f);
                yield return Linger(0.8f);
            }
            if (pickup != null) tank.Teleport(pickup.transform.position + Vector3.up * 0.6f, 0f);
            yield return WaitUntilReal(() => drugs.Count(DrugType.Nalcon) >= 1, 2f);
            Record("drug_pickup", drugs.Count(DrugType.Nalcon) >= 1, $"nalcon drugs={drugs.Count(DrugType.Nalcon)}");
            yield return Linger(1f);
            tank.Teleport(new Vector3(0f, 1f, 0f), 0f);
            yield return null;
            EndClip();

            // The bar warnings (Bruno): low HP, low nalcon, high SHORT, each with an icon and a word. hud_warnings.png.
            playerHealth.TakeDamage(Mathf.Max(0f, playerHealth.CurrentHealth - playerHealth.MaxHealth * 0.2f));
            stats.DebugSet(20f, 80f);
            yield return WaitReal(0.3f);
            PlayerHud warnHud = FindFirstObjectByType<PlayerHud>();
            bool warnings = warnHud != null && warnHud.LowHpWarning && warnHud.LowNalconWarning && warnHud.HighShortWarning;
            if (warnHud != null && warnHud.Canvas != null) yield return OverlayShot.Capture(warnHud.Canvas, "hud_warnings.png", 0.05f);
            Record("hud_warnings", warnings && warnHud.WarningBeeps >= 1,
                $"hp={warnHud?.LowHpWarning} nalcon={warnHud?.LowNalconWarning} short={warnHud?.HighShortWarning} beeps={warnHud?.WarningBeeps}");
            stats.RefillAll();
            yield return null;

            // The short: a full bar, the attack press starts it at once (Bruno, 2026-10-20), the shocks, the slow
            // walk, then the drug ends it.
            Clip("short");
            stats.DebugSet(100f, 100f);
            pad.Tap(InputManager.GameAction.Charge);
            yield return null;
            yield return null;
            bool shortBegan = shortController.IsActive && tank.SlowWalk;
            float hpBefore = playerHealth.CurrentHealth;
            yield return WaitGame(1.2f);
            yield return Linger(2f);
            CaptureShot("short");
            PlayerHud hud = FindFirstObjectByType<PlayerHud>();
            if (hud != null && hud.Canvas != null) yield return OverlayShot.Capture(hud.Canvas, "hud_short.png", 0.1f);
            float hpDuring = playerHealth.CurrentHealth;
            pad.Tap(InputManager.GameAction.UseDrug);
            yield return null;
            yield return null;
            Record("short_and_drug", shortBegan && hpDuring < hpBefore - 2f && !shortController.IsActive && !tank.SlowWalk && playerHealth.CurrentHealth >= playerHealth.MaxHealth - 0.5f,
                $"began={shortBegan} hp {hpBefore:0} -> {hpDuring:0} -> {playerHealth.CurrentHealth:0} ended={!shortController.IsActive} slow={tank.SlowWalk} drugs={drugs.Count(DrugType.Nalcon)}");
            if (hud != null && hud.Canvas != null) yield return OverlayShot.Capture(hud.Canvas, "hud.png", 0.2f);
            yield return Linger(1f);
            EndClip();
        }

        /// <summary>A game-clock pause, only on video (-gbRecord 1), so a clip holds on what it shows.</summary>
        private static IEnumerator Linger(float seconds)
        {
            if (FrameRecorder.Enabled) yield return WaitGame(seconds);
        }

        /// <summary>Starts a video clip of the next steps (only with -gbRecord 1, see FrameRecorder).</summary>
        private static void Clip(string name)
        {
            if (FrameRecorder.Enabled) FrameRecorder.Ensure().Begin(name);
        }

        private static void EndClip()
        {
            if (FrameRecorder.Instance != null) FrameRecorder.Instance.End();
        }

        private static GameObject FindPlayer(out Health health)
        {
            health = null;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                health = player.GetComponentInParent<Health>() ?? player.GetComponentInChildren<Health>();
            }
            return player;
        }

        private static IEnumerator WaitForScene(string sceneName, float timeout)
        {
            float t0 = Time.unscaledTime;
            while (SceneManager.GetActiveScene().name != sceneName && Time.unscaledTime - t0 < timeout) yield return null;
        }

        private static IEnumerator WaitUntilReal(Func<bool> condition, float timeout)
        {
            float t0 = Time.unscaledTime;
            while (!condition() && Time.unscaledTime - t0 < timeout) yield return null;
        }

        private void CaptureShot(string shotName)
        {
            string dir = BotArgs.RunDir;
            if (string.IsNullOrEmpty(dir) || Camera.main == null) return;
            try
            {
                ShotRenderer.Render(Camera.main, Path.Combine(dir, "shots", SafeName(shotName) + ".png"));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Bot] shot failed: " + ex.Message);
            }
        }

        /// <summary>
        /// A part of the cast along the north wall (z 4.4) from a camera 4 m in front of it at eye height, centred
        /// on centreX: three figures fill the frame, the 2.6 m one included (a look check).
        /// </summary>
        private void CaptureLineupShot(string shotName, float centreX)
        {
            string dir = BotArgs.RunDir;
            if (string.IsNullOrEmpty(dir) || Camera.main == null) return;
            var rig = new GameObject("[LineupCamera]");
            try
            {
                Camera cam = rig.AddComponent<Camera>();
                cam.CopyFrom(Camera.main);
                cam.enabled = false;
                cam.transform.position = new Vector3(centreX, 1.35f, 0.4f);
                cam.transform.LookAt(new Vector3(centreX, 1.2f, 4.4f));
                cam.fieldOfView = 45f;
                ShotRenderer.Render(cam, Path.Combine(dir, "shots", SafeName(shotName) + ".png"));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Bot] line-up shot failed: " + ex.Message);
            }
            finally
            {
                Destroy(rig);
            }
        }

        private static string SafeName(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            EndClip();
            bool pass = steps.Count > 0 && steps.All(s => s.ok);
            RunTelemetry.Event("bot_assist", "off");
            RunTelemetry.Event("bot_done", pass ? "pass" : "fail");
            string dir = BotArgs.RunDir;
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                var sb = new StringBuilder();
                sb.Append("{\n  \"bot\": \"structural\",\n  \"pass\": ").Append(pass ? "true" : "false")
                  .Append(",\n  \"scene\": \"").Append(SceneManager.GetActiveScene().name)
                  .Append("\",\n  \"duration_s\": ").Append((Time.unscaledTime - startTime).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(",\n  \"max_frame_ms\": ").Append(maxFrameMs.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(",\n  \"steps\": [\n");
                for (int i = 0; i < steps.Count; i++)
                {
                    Step s = steps[i];
                    sb.Append("    ").Append(JsonUtility.ToJson(s)).Append(i < steps.Count - 1 ? ",\n" : "\n");
                }
                sb.Append("  ],\n  \"events\": [");
                sb.Append(string.Join(", ", eventLog.Select(e => "\"" + e.Replace("\\", "/").Replace("\"", "'") + "\"")));
                sb.Append("]\n}\n");
                File.WriteAllText(Path.Combine(dir, "playthrough.json"), sb.ToString());
            }
            Debug.Log($"[Bot] finished: {(pass ? "PASS" : "FAIL")} ({steps.Count(s => s.ok)}/{steps.Count} steps ok)");
#if !UNITY_EDITOR
            Application.Quit(pass ? 0 : 1);
#endif
        }

        private static IEnumerator WaitReal(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
        }

        /// <summary>Game-clock wait, for distances and angles: the controllers integrate Time.deltaTime.</summary>
        private static IEnumerator WaitGame(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }
    }
}
