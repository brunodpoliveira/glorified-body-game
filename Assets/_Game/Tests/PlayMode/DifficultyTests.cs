#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;
using Section = GlorifiedBody.Systems.DifficultyModuleDefinition.Section;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The difficulty (Bruno, 2026-10-26): what each module does in the training room, and the difficulty screen of
    /// the main menu. The engine itself: DifficultyManagerTests and DifficultyModuleAssetTests (EditMode); the shooters
    /// at a time and the telegraph of the presets: DummyShooterTests and EnemyAiTests. Every test ends at the default
    /// preset. Batch: tools/unity.sh test playmode DifficultyTests.
    /// </summary>
    public class DifficultyTests
    {
        private GameObject lucy;
        private Health health;
        private PsychicStats stats;
        private DifficultyManager difficulty;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            difficulty = DifficultyManager.Instance;
            Assert.IsNotNull(difficulty, "DifficultyManager");
            difficulty.SetPreset(DifficultyManager.DefaultPreset);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (DifficultyManager.Instance != null) DifficultyManager.Instance.SetPreset(DifficultyManager.DefaultPreset);
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheScreen_ShowsThePresets_AndTheTwoSections_AndACustomSet()
        {
            MainMenuUI menu = UnityEngine.Object.FindFirstObjectByType<MainMenuUI>();
            yield return null;
            GameObject button = GameObject.Find("Btn_DIFFICULTY");
            Assert.IsNotNull(button, "the DIFFICULTY button");
            Assert.IsTrue(button.GetComponentInChildren<UnityEngine.UI.Text>().text.Contains("150%"), "it names the preset");
            button.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            DifficultyMenuUI screen = menu.GetComponent<DifficultyMenuUI>();
            Assert.IsTrue(screen.IsVisible, "DIFFICULTY opens the screen");
            Assert.AreEqual(9, screen.RowCount(Section.Harder), "the harder modules");
            Assert.AreEqual(6, screen.RowCount(Section.Easier), "the easier modules");
            Assert.AreEqual(0, screen.RowCount(Section.Base), "the base has no row");
            Assert.AreEqual("150%   PRESET", screen.TotalText);

            screen.PressPreset(100);
            Assert.AreEqual(100, difficulty.ActivePresetPercent);
            Assert.AreEqual("100%   PRESET", screen.TotalText);
            screen.PressModule("thick_skin");
            Assert.IsTrue(difficulty.IsCustom, "a module on top of a preset is custom");
            Assert.AreEqual("75%   CUSTOM", screen.TotalText);
            screen.PressModule("hair_trigger");
            screen.PressModule("slow_trigger");
            Assert.IsFalse(difficulty.IsModuleActive(DifficultyModuleDefinition.ModuleType.HairTrigger), "one telegraph at a time");
            Assert.AreEqual(55, difficulty.TotalDifficultyPercent);
            screen.PressPreset(200);
            Assert.IsFalse(difficulty.IsModuleActive(DifficultyModuleDefinition.ModuleType.ThickSkin), "a preset clears the custom set");
            Assert.AreEqual("200%   PRESET", screen.TotalText);

            screen.Close();
            yield return null;
            yield return null;
            Assert.IsFalse(screen.IsVisible);
            Assert.IsTrue(button.activeInHierarchy, "the main menu comes back");
            Assert.IsTrue(button.GetComponentInChildren<UnityEngine.UI.Text>().text.Contains("200%"), "the button names the new preset");
        }

        [UnityTest]
        public IEnumerator EnemyDamage_FollowsHardHittersAndThickSkin()
        {
            yield return InRoom();
            float normal = 0f, hard = 0f, thick = 0f;
            yield return Bullet(20f, v => normal = v);
            Toggle("hard_hitters");
            yield return Bullet(20f, v => hard = v);
            Toggle("thick_skin");
            yield return Bullet(20f, v => thick = v);
            Debug.Log($"[DifficultyTests] a 20 HP bullet: {normal:0.0} / hard hitters {hard:0.0} / thick skin {thick:0.0}");
            Assert.AreEqual(20f, normal, 0.5f, "150 %: the bullet's own damage");
            Assert.AreEqual(30f, hard, 0.5f, "HARD HITTERS: 1.5x");
            Assert.AreEqual(10f, thick, 0.5f, "THICK SKIN (it turns HARD HITTERS off): half");
        }

        [UnityTest]
        public IEnumerator Fragile_HalvesHerMaximumHp_AndOneHitWonder_LeavesOne()
        {
            yield return InRoom();
            float full = health.MaxHealth;
            Assert.IsTrue(health.IsSecondWindEnabled, "the second wind of the room");
            Toggle("fragile");
            Assert.AreEqual(Mathf.Round(full * 0.5f), health.MaxHealth, 0.01f, "FRAGILE halves it");
            Assert.LessOrEqual(health.CurrentHealth, health.MaxHealth);
            Toggle("fragile");
            Assert.AreEqual(full, health.MaxHealth, 0.01f, "off: back to full size");

            Toggle("one_hit_wonder");
            Assert.AreEqual(1f, health.MaxHealth, 0.01f, "ONE-HIT WONDER: 1 HP");
            Assert.IsFalse(health.IsSecondWindEnabled, "no second wind");
            health.TakeDamage(1f);
            Assert.IsTrue(health.IsDead, "any hit kills");
            yield return null;
        }

        [UnityTest]
        public IEnumerator OneHitWonder_Off_GivesTheSecondWindBack()
        {
            yield return InRoom();
            Toggle("one_hit_wonder");
            Toggle("one_hit_wonder");
            Assert.IsTrue(health.IsSecondWindEnabled, "the second wind comes back");
            Assert.Greater(health.MaxHealth, 1f);
        }

        [UnityTest]
        public IEnumerator MkRecharge_FollowsScarcityAndDeepWell()
        {
            yield return InRoom();
            float normal = 0f, scarce = 0f, deep = 0f;
            yield return MkGain(v => normal = v);
            Toggle("scarcity");
            yield return MkGain(v => scarce = v);
            Toggle("deep_well");
            yield return MkGain(v => deep = v);
            Debug.Log($"[DifficultyTests] MK in 1 s: {normal:0.00} / scarcity {scarce:0.00} / deep well {deep:0.00}");
            Assert.AreEqual(stats.Outfit.nalconRegenPerSecond, normal, 0.25f, "the outfit rate");
            Assert.AreEqual(normal * 0.5f, scarce, 0.25f, "SCARCITY: half");
            Assert.AreEqual(normal * 2f, deep, 0.4f, "DEEP WELL (it turns SCARCITY off): twice");
        }

        [UnityTest]
        public IEnumerator ShortGain_FollowsShortFuseAndSteadyMind_AndMendingDoublesTheFeed()
        {
            yield return InRoom();
            stats.RegenPaused = true;
            float normal = KillGain();
            Toggle("short_fuse");
            float fuse = KillGain();
            Toggle("steady_mind");
            float steady = KillGain();
            Assert.AreEqual(25f, normal, 0.01f, "a kill: 25");
            Assert.AreEqual(37.5f, fuse, 0.01f, "SHORT FUSE: 1.5x");
            Assert.AreEqual(12.5f, steady, 0.01f, "STEADY MIND (it turns SHORT FUSE off): half");

            // No HP comes back by itself (Bruno, 2026-10-30): MENDING doubles the feed instead.
            stats.RegenPaused = false;
            Assert.AreEqual(1f, GlorifiedBody.Player.Feeding.DifficultyScale, 0.001f, "the feed at 150 %");
            Toggle("mending");
            health.SetHealth(health.MaxHealth * 0.5f);
            float hp0 = health.CurrentHealth;
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(hp0, health.CurrentHealth, 0.001f, "no HP recharge, even with MENDING");
            Assert.AreEqual(2f, GlorifiedBody.Player.Feeding.DifficultyScale, 0.001f, "MENDING: twice the HP of a feed");
        }

        [UnityTest]
        public IEnumerator TheTelegraphModules_SetTheEnemyTiming()
        {
            yield return InRoom();
            Toggle("slow_trigger");
            Assert.AreEqual(2f, difficulty.AttackSeconds, 0.001f, "SLOW TRIGGER: 2.0 s");
            Toggle("long_rest");
            Assert.AreEqual(4f, difficulty.RestSeconds, 0.001f, "LONG REST: 4 s");
            Assert.AreEqual(2, difficulty.MaxSimultaneousShooters, "the 150 % guns stay");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private IEnumerator InRoom()
        {
            GameManager.Instance.LoadGameplay();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "TrainingRoom" && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            foreach (DummyShooter shooter in UnityEngine.Object.FindObjectsByType<DummyShooter>(FindObjectsSortMode.None)) shooter.enabled = false;
            lucy = GameObject.FindGameObjectWithTag("Player");
            health = lucy.GetComponentInChildren<Health>();
            stats = lucy.GetComponent<PsychicStats>();
        }

        private void Toggle(string id)
        {
            foreach (var mod in difficulty.AllModules)
            {
                if (mod.moduleId == id) { difficulty.ToggleModule(mod); return; }
            }
            Assert.Fail("no module " + id);
        }

        /// <summary>The HP one enemy bullet takes from her (no recharge, full HP first).</summary>
        private IEnumerator Bullet(float damage, Action<float> result)
        {
            stats.RegenPaused = true;
            health.SetHealth(health.MaxHealth);
            float hp0 = health.CurrentHealth;
            Vector3 target = lucy.transform.position + Vector3.up * 1.2f;
            Vector3 origin = target + lucy.transform.forward * 4f;
            EnemyProjectile.Launch(origin, target - origin, 30f, damage, GameObject.Find("Dummy_A"), "test_bullet");
            yield return WaitUntil(() => health.CurrentHealth < hp0, 2f);
            yield return null;
            result(hp0 - health.CurrentHealth);
        }

        private IEnumerator MkGain(Action<float> result)
        {
            stats.RegenPaused = false;
            stats.DebugSet(10f, 0f);
            float t0 = Time.time;
            yield return new WaitForSeconds(1f);
            result((stats.Nalcon - 10f) / (Time.time - t0));
        }

        private float KillGain()
        {
            stats.DebugSet(stats.Nalcon, 0f);
            stats.NotifyKill("test");
            return stats.Short;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
