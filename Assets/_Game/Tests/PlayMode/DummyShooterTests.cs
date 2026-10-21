#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Cameras;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The dummy enemy of the training room: aim, telegraph, shot, the difficulty slots, the death-screen hint.
    /// </summary>
    public class DummyShooterTests
    {
        private const string MainMenu = "Main_Menu";
        private const string Room = "TrainingRoom";

        private GameObject player;
        private TankController tank;
        private Health health;
        private PsychicPowers powers;
        private DummyShooter shooter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainMenu);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == MainMenu && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadGameplay();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Room && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            player = GameObject.FindGameObjectWithTag("Player");
            tank = player.GetComponent<TankController>();
            health = player.GetComponentInChildren<Health>();
            powers = player.GetComponent<PsychicPowers>();
            shooter = GameObject.Find("Dummy_A").GetComponent<DummyShooter>();
            Assert.IsNotNull(shooter, "Dummy_A shoots");
            shooter.enabled = false; // each test switches it on and watches a full cycle from Idle
            DifficultyManager.Instance.SetPreset(DifficultyModuleDefinition.ModuleType.PresetNormal);
            tank.Teleport(new Vector3(0f, 1f, 0f), 0f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSeconds(0.2f);
        }

        [UnityTest]
        public IEnumerator Shooter_Aims_Telegraphs_Fires_AndHitsLucy()
        {
            player.GetComponent<PsychicStats>().SetOutfit(Resources.Load<OutfitProfile>("Outfits/outfit_guard")); // no HP regen in the way
            float hp0 = health.CurrentHealth;
            shooter.enabled = true;
            yield return WaitUntil(() => shooter.Current == DummyShooter.State.Aiming, 2f);
            Assert.AreEqual(DummyShooter.State.Aiming, shooter.Current, "it aims when she is in range");
            Assert.IsTrue(shooter.transform.Find("Laser").GetComponent<LineRenderer>().enabled, "the laser line shows");

            yield return WaitUntil(() => shooter.Current == DummyShooter.State.Telegraph, 2f);
            Assert.AreEqual(DummyShooter.State.Telegraph, shooter.Current, "then the telegraph");
            PlayerHud hud = UnityEngine.Object.FindFirstObjectByType<PlayerHud>();
            Assert.IsNotNull(hud, "the HUD exists");
            yield return null;
            Assert.AreEqual(1, hud.ActiveThreatRings, "the circle shows on the HUD, over the locked point (her)");
            Assert.IsTrue(shooter.transform.Find("TelegraphRing") == null, "no circle in the world any more");
            Vector3 lockedAim = shooter.AimPoint;
            float p0 = shooter.TelegraphProgress;
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(shooter.TelegraphProgress, p0, "the circle shrinks over time");
            Assert.AreEqual(lockedAim, shooter.AimPoint, "once it aims, it cannot aim again");

            yield return WaitUntil(() => shooter.ShotsFired >= 1, 2f);
            Assert.AreEqual(1, shooter.ShotsFired, "it fires when the circle closes");
            yield return WaitUntil(() => health.CurrentHealth < hp0, 2f);
            Assert.Less(health.CurrentHealth, hp0, "the bullet hits her");
            Assert.AreEqual(DummyShooter.State.Cooldown, shooter.Current, "then it cools down");
        }

        [UnityTest]
        public IEnumerator Telegraph_ShowsAnEdgeArrow_WhenTheShooterIsOutsideTheFrame()
        {
            // Bruno, 2026-10-20: the circle is her heads-up to dodge, so it must show when the camera hides the shooter.
            // The camera goes 3 m above her and looks down: she is in the frame, the dummy 4 m away is not.
            FixedCameraDirector director = UnityEngine.Object.FindFirstObjectByType<FixedCameraDirector>();
            if (director != null) director.enabled = false;
            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "a camera");
            cam.transform.position = player.transform.position + Vector3.up * 3f;
            cam.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            cam.fieldOfView = 60f;
            yield return null;
            Vector3 shooterScreen = cam.WorldToScreenPoint(shooter.transform.position + Vector3.up);
            bool shooterInFrame = shooterScreen.z > 0f && shooterScreen.x >= 0f && shooterScreen.x <= Screen.width && shooterScreen.y >= 0f && shooterScreen.y <= Screen.height;
            Assert.IsFalse(shooterInFrame, "the dummy is outside the frame");

            PlayerHud hud = UnityEngine.Object.FindFirstObjectByType<PlayerHud>();
            Assert.IsNotNull(hud, "the HUD exists");
            shooter.enabled = true;
            yield return WaitUntil(() => shooter.Current == DummyShooter.State.Aiming, 2f);
            yield return null;
            Assert.AreEqual(1, hud.ActiveThreatArrows, "an edge arrow points at the shooter while it aims");
            yield return WaitUntil(() => shooter.Current == DummyShooter.State.Telegraph, 2f);
            yield return null;
            Assert.AreEqual(1, hud.ActiveThreatRings, "the circle shows over her during the telegraph");
            Assert.AreEqual(1, hud.ActiveThreatArrows, "the arrow stays during the telegraph");
            yield return WaitUntil(() => shooter.ShotsFired >= 1, 2f);
            yield return null;
            Assert.AreEqual(0, hud.ActiveThreatRings, "the circle is gone after the shot");
            Assert.AreEqual(0, hud.ActiveThreatArrows, "the arrow is gone after the shot");
        }

        [UnityTest]
        public IEnumerator Telegraph_EdgeArrow_ShowsFromTheRoomsOwnSouthWestCamera()
        {
            // Bruno, 2026-10-20: "no camera angle cuts the dummy target". Cam_SouthWest now looks west along the
            // south wall: in its zone she is in the frame and the shooter is not, so the arrow shows in the room
            // itself, with no test camera.
            FixedCameraDirector director = FixedCameraDirector.Instance;
            Assert.IsNotNull(director, "FixedCameraDirector");
            tank.Teleport(new Vector3(-3f, 1f, -3f), 0f);
            yield return WaitUntil(() => director.CurrentName == "Cam_SouthWest", 1.5f);
            Assert.AreEqual("Cam_SouthWest", director.CurrentName, "the south-west zone cuts to its camera");
            yield return null;
            Camera cam = Camera.main;
            Assert.IsTrue(InFrame(cam, player.transform.position + Vector3.up), "she is in the frame");
            Assert.IsFalse(InFrame(cam, shooter.transform.position + Vector3.up), "the shooter is outside the frame");
            Assert.Less(Vector3.Distance(player.transform.position, shooter.transform.position), 10f, "and inside its range, so it aims");

            PlayerHud hud = UnityEngine.Object.FindFirstObjectByType<PlayerHud>();
            Assert.IsNotNull(hud, "the HUD exists");
            shooter.enabled = true;
            yield return WaitUntil(() => shooter.Current == DummyShooter.State.Aiming, 2f);
            Assert.AreEqual(DummyShooter.State.Aiming, shooter.Current, "it aims at her from outside the frame");
            yield return null;
            Assert.AreEqual(1, hud.ActiveThreatArrows, "the edge arrow shows from the room's own camera");
            yield return WaitUntil(() => shooter.Current == DummyShooter.State.Telegraph, 2f);
            yield return null;
            Assert.AreEqual(1, hud.ActiveThreatRings, "the circle shows over her");
            Assert.AreEqual(1, hud.ActiveThreatArrows, "the arrow stays during the telegraph");
        }

        private static bool InFrame(Camera cam, Vector3 world)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            return screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height;
        }

        [UnityTest]
        public IEnumerator Blast_StaggersTheAim()
        {
            shooter.enabled = true;
            yield return WaitUntil(() => shooter.Current == DummyShooter.State.Aiming || shooter.Current == DummyShooter.State.Telegraph, 2f);
            powers.Select(PowerKind.Blast);
            powers.FireAt(powers.Selected, 1f, shooter.GetComponent<LockOnTargetable>());
            yield return null;
            Assert.AreEqual(DummyShooter.State.Staggered, shooter.Current, "a blast interrupts the aim");
            Assert.IsFalse(shooter.transform.Find("Laser").GetComponent<LineRenderer>().enabled, "the laser is gone");
        }

        [UnityTest]
        public IEnumerator DeathByTheShooter_NamesIt_ForTheDeathScreen()
        {
            DeathMenuUI.LastKillerId = "";
            player.GetComponent<PsychicStats>().SetOutfit(Resources.Load<OutfitProfile>("Outfits/outfit_guard"));
            health.SetSecondWindEnabled(false); // the second wind has its own test; this one is the killer's name
            health.SetHealth(5f);
            shooter.enabled = true;
            yield return WaitUntil(() => health.IsDead, 10f);
            Assert.IsTrue(health.IsDead, "a bullet kills her at 5 HP");
            Assert.AreEqual("dummy_shooter", DeathMenuUI.LastKillerId, "the death screen knows the killer");
            DeathScreenContent content = Resources.Load<DeathScreenContent>(DeathMenuUI.ContentResourceName);
            Assert.AreNotEqual(content.fallback, content.Find("dummy_shooter"), "the killer has its own hint entry");
        }

        [UnityTest]
        public IEnumerator Difficulty_LimitsHowManyAimAtOnce()
        {
            // Easy: one slot. Two shooters start together, so only one of them may aim at a time.
            DifficultyManager.Instance.SetPreset(DifficultyModuleDefinition.ModuleType.PresetEasy);
            Assert.AreEqual(1, ShooterScheduler.MaxSlots);
            DummyShooter second = GameObject.Find("Dummy_B").AddComponent<DummyShooter>();
            shooter.enabled = true;
            int maxSeen = 0;
            bool secondAimedUnderEasy = false;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 1.2f)
            {
                maxSeen = Mathf.Max(maxSeen, ShooterScheduler.ActiveCount);
                if (second.HoldsSlot && shooter.HoldsSlot) secondAimedUnderEasy = true;
                yield return null;
            }
            Assert.AreEqual(1, maxSeen, "easy: one shooter aims at a time");
            Assert.IsFalse(secondAimedUnderEasy, "easy: never both at once");

            // Hard: no limit. Both start a fresh cycle together and both aim at once.
            shooter.enabled = false;
            second.enabled = false;
            DifficultyManager.Instance.SetPreset(DifficultyModuleDefinition.ModuleType.PresetHard);
            Assert.AreEqual(int.MaxValue, ShooterScheduler.MaxSlots, "hard: no limit");
            yield return null;
            shooter.enabled = true;
            second.enabled = true;
            yield return WaitUntil(() => ShooterScheduler.ActiveCount >= 2, 4f);
            Assert.GreaterOrEqual(ShooterScheduler.ActiveCount, 2, "hard: both aim at once");
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
