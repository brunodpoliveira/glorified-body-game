#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Automation;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The exp levels (design §11.2, 2026-10-26), in the training room: her kill pays exp, a death she did not cause
    /// and a second hit on a body do not; a level adds maximum HP and power damage, and the HUD shows "LV n" and
    /// "LEVEL n"; a save keeps the exp and the level. The civilian rate is in CivilianTests. Batch:
    /// tools/unity.sh test playmode ExperienceTests.
    /// </summary>
    public class ExperienceTests
    {
        private GameObject lucy;
        private Health health;
        private PsychicStats stats;
        private PsychicPowers powers;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_exp_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadGameplay();
            yield return Room();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SaveSystem.Folder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator HerKill_PaysExp_ButNotADeathSheDidNotCause_NorAHitOnTheBody()
        {
            Assert.AreEqual(0, stats.Exp);
            Assert.AreEqual(1, stats.Level);
            powers.Select(PowerKind.Blast);
            Health b = GameObject.Find("Dummy_B").GetComponent<Health>();
            powers.FireAt(powers.Selected, 0.5f, b.GetComponent<LockOnTargetable>(), 100f);
            Assert.IsTrue(b.IsDead, "her blast killed Dummy_B");
            int soldier = ExperienceRules.ValueOf(EnemyMorale.Rank.Soldier);
            Assert.AreEqual(soldier, stats.Exp, "a training dummy pays a soldier's exp");

            // Dummy_A dies with no hit from her (a fall, a colleague's grenade): no exp.
            Health a = GameObject.Find("Dummy_A").GetComponent<Health>();
            // A training dummy vanishes at its death: this one stays, so a later hit can find the body.
            typeof(Health).GetField("destroyOnDeath", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(a, false);
            a.TakeDamage(a.MaxHealth * 10f);
            Assert.IsTrue(a.IsDead);
            Assert.AreEqual(soldier, stats.Exp, "a death she did not cause pays nothing");
            yield return null;
            // Her blast on the body a frame later reports a fatal hit too: it is not the kill.
            HitEventBus.Raise(new HitInfo { Attacker = lucy, Victim = a.gameObject, HitPoint = a.transform.position, DamageAmount = 50f, DamageType = DamageType.PsychicBlast, WasFatal = true });
            Assert.AreEqual(soldier, stats.Exp, "a hit on a body pays nothing");
            Assert.AreEqual(1, stats.Level);
        }

        [UnityTest]
        public IEnumerator ALevel_AddsHpAndPowerDamage_AndTheHudShowsIt()
        {
            float max0 = health.MaxHealth;
            PlayerHud hud = UnityEngine.Object.FindFirstObjectByType<PlayerHud>();
            Assert.IsNotNull(hud, "the HUD");
            yield return null;
            Assert.AreEqual("LV 1", hud.LevelShown);

            stats.AddExp(ExperienceRules.Threshold(2), "test");
            Assert.AreEqual(2, stats.Level);
            Assert.AreEqual(max0 + ExperienceRules.HpPerLevel, health.MaxHealth, 0.01f, "a level adds maximum HP");
            Assert.AreEqual(max0, health.CurrentHealth, 0.01f, "but no HP: only feeding gives HP (Bruno, 2026-10-30)");
            yield return null;
            Assert.AreEqual("LV 2", hud.LevelShown);
            Assert.IsTrue(hud.LevelUpShown, "\"LEVEL 2\" on the screen");
            HudShot(hud, "level_up.png");

            // The same half blast does 10 % more at level 2 (no MK boost: the bar is under half).
            powers.Select(PowerKind.Blast);
            PowerDefinition blast = powers.Selected;
            Health target = GameObject.Find("Dummy_A").GetComponent<Health>();
            float hp0 = target.CurrentHealth;
            Assert.IsFalse(powers.IsBoosted);
            powers.FireAt(blast, 0.5f, target.GetComponent<LockOnTargetable>());
            Assert.AreEqual(hp0 - blast.baseDamage * 0.5f * 1.1f, target.CurrentHealth, 0.05f, "the level's damage");

            yield return new WaitForSecondsRealtime(PlayerHud.LevelUpSeconds + 0.2f);
            Assert.IsFalse(hud.LevelUpShown, "the banner goes");
        }

        [UnityTest]
        public IEnumerator ASave_KeepsTheExp_AndTheLevel()
        {
            int exp = ExperienceRules.Threshold(3) + 10;
            stats.AddExp(exp, "test");
            Assert.AreEqual(3, stats.Level);
            float max = health.MaxHealth;
            SavePoint point = UnityEngine.Object.FindFirstObjectByType<SavePoint>();
            Assert.IsTrue(SaveSystem.SaveManual(1, point.SpotId), "saved");

            stats.AddExp(1000, "test"); // more after the save
            Assert.Greater(stats.Level, 3);

            int applied = SaveSystem.Applies;
            Assert.IsTrue(SaveSystem.Load(1));
            yield return WaitUntil(() => SaveSystem.Applies > applied, 10f);
            yield return Room();
            Assert.AreEqual(exp, stats.Exp, "the saved exp");
            Assert.AreEqual(3, stats.Level, "the saved level");
            Assert.AreEqual(max, health.MaxHealth, 0.01f, "the saved maximum HP, with the levels' HP");
        }

        private IEnumerator Room()
        {
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "TrainingRoom" && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            foreach (DummyShooter shooter in UnityEngine.Object.FindObjectsByType<DummyShooter>(FindObjectsSortMode.None)) shooter.enabled = false;
            lucy = GameObject.FindGameObjectWithTag("Player");
            health = lucy.GetComponentInChildren<Health>();
            stats = lucy.GetComponent<PsychicStats>();
            powers = lucy.GetComponent<PsychicPowers>();
            health.SetInvulnerable(true);
            stats.RegenPaused = true;
            stats.DebugSet(45f, 0f); // under the MK boost line: the base numbers
        }

        /// <summary>The room with the HUD over it, for Bruno (the overlay canvas renders only through a camera).</summary>
        private static void HudShot(PlayerHud hud, string name)
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "glorified-body-renders", "game", "exp", "2026-10-26");
            Camera cam = Camera.main;
            Canvas canvas = hud.Canvas;
            if (cam == null || canvas == null) return;
            RenderMode mode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 0.05f;
            Canvas.ForceUpdateCanvases();
            try { ShotRenderer.Render(cam, Path.Combine(folder, name), 1920, 1080, useTargetTexture: true); }
            finally { canvas.renderMode = mode; }
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
