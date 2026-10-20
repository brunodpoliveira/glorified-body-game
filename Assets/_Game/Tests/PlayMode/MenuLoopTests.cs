#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Core;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The menu loop of build order step 1, as play-mode tests. Batch: tools/unity.sh test playmode.
    /// Every test starts in Main_Menu. The managers persist across scenes (DontDestroyOnLoad).
    /// </summary>
    public class MenuLoopTests
    {
        private const string MainMenu = "Main_Menu";
        private const string Room = "TrainingRoom";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainMenu);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == MainMenu
                && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainMenu_HasManagersUiAndEventSystem()
        {
            yield return null;
            Assert.IsNotNull(GameManager.Instance, "GameManager");
            Assert.IsTrue(GameManager.Instance.IsInMenu, "state " + GameManager.Instance.CurrentState);
            Assert.IsNotNull(InputManager.Instance, "InputManager");
            Assert.IsNotNull(DifficultyManager.Instance, "DifficultyManager");
            Assert.IsNotNull(SettingsManager.Instance, "SettingsManager");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<MainMenuUI>(), "MainMenuUI");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<EventSystem>(), "EventSystem");
        }

        [UnityTest]
        public IEnumerator Start_LoadsTrainingRoom_AndPlays()
        {
            yield return EnterRoom();
            Assert.AreEqual(Room, SceneManager.GetActiveScene().name);
            Assert.IsTrue(GameManager.Instance.IsPlaying, "state " + GameManager.Instance.CurrentState);
            Health health = FindPlayerHealth();
            Assert.IsNotNull(health, "an active object tagged Player with a Health");
            Assert.IsFalse(health.IsDead);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Pause_ShowsPauseMenu_AndResumeHidesIt()
        {
            yield return EnterRoom();
            GameManager.Instance.Pause();
            yield return null;
            yield return null;
            GameObject canvas = GameObject.Find("PauseMenuUICanvas");
            Assert.IsTrue(GameManager.Instance.IsPaused, "paused");
            Assert.IsNotNull(canvas, "PauseMenuUICanvas visible");
            Assert.AreEqual(0f, Time.timeScale, "time stops on pause");

            GameManager.Instance.Resume();
            yield return null;
            yield return null;
            Assert.IsTrue(GameManager.Instance.IsPlaying, "playing again");
            Assert.IsFalse(canvas.activeSelf, "pause menu hidden");
            Assert.AreEqual(1f, Time.timeScale, "time runs again");
        }

        [UnityTest]
        public IEnumerator Death_ShowsDeathScreenWithinOneSecond_AndRestartPlays()
        {
            yield return EnterRoom();
            Health health = FindPlayerHealth();
            Assert.IsNotNull(health);

            float t0 = Time.realtimeSinceStartup;
            health.TakeDamage(9999f);
            yield return WaitUntil(() => GameManager.Instance.IsGameOver, 2f);
            float elapsed = Time.realtimeSinceStartup - t0;
            Assert.IsTrue(health.IsDead, "player dead");
            Assert.IsTrue(GameManager.Instance.IsGameOver, "game over");
            Assert.Less(elapsed, 1f, "game over within 1 s of the lethal hit");

            yield return WaitUntil(() => DeathCanvasVisible(), 2f);
            Assert.IsTrue(DeathCanvasVisible(), "DeathMenuUICanvas visible");
            Assert.AreEqual(0f, Time.timeScale, "time stops on game over");

            if (!GameManager.Instance.RestartFromLatestCheckpoint())
            {
                GameManager.Instance.RestartLevel();
            }
            yield return WaitUntil(() => GameManager.Instance.IsPlaying && SceneManager.GetActiveScene().name == Room, 10f);
            yield return null;
            Health again = FindPlayerHealth();
            Assert.IsTrue(GameManager.Instance.IsPlaying, "playing after restart");
            Assert.IsNotNull(again, "player present after restart");
            Assert.IsFalse(again.IsDead, "player alive after restart");
            Assert.AreEqual(1f, Time.timeScale, "time runs after restart");
        }

        [UnityTest]
        public IEnumerator PresetSwitch_ChangesMaxSimultaneousShooters()
        {
            yield return null;
            DifficultyManager dm = DifficultyManager.Instance;
            Assert.IsTrue(dm.SetPreset(DifficultyModuleDefinition.ModuleType.PresetEasy), "easy preset asset");
            Assert.AreEqual(1, dm.MaxSimultaneousShooters, "easy: one shooter");
            Assert.IsTrue(dm.SetPreset(DifficultyModuleDefinition.ModuleType.PresetHard), "hard preset asset");
            Assert.AreEqual(0, dm.MaxSimultaneousShooters, "hard: every enemy fires");
            Assert.IsTrue(dm.SetPreset(DifficultyModuleDefinition.ModuleType.PresetNormal), "normal preset asset");
            Assert.AreEqual(2, dm.MaxSimultaneousShooters, "normal: two shooters");
            Assert.AreEqual(DifficultyModuleDefinition.ModuleType.PresetNormal, dm.ActivePreset.moduleType);
        }

        [UnityTest]
        public IEnumerator Remap_Dodge_SavesToPlayerPrefs_AndResets()
        {
            yield return null;
            InputManager im = InputManager.Instance;
            im.SetBinding(InputManager.GameAction.Dodge, KeyCode.K);
            Assert.AreEqual(KeyCode.K, im.GetKey(InputManager.GameAction.Dodge), "rebound in memory");
            Assert.AreEqual((int)KeyCode.K, PlayerPrefs.GetInt("keybind_Dodge", -1), "saved to PlayerPrefs");

            im.ResetToDefaults();
            Assert.AreEqual(KeyCode.Space, im.GetKey(InputManager.GameAction.Dodge), "default again");
            Assert.IsFalse(PlayerPrefs.HasKey("keybind_Dodge"), "saved key removed");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static IEnumerator EnterRoom()
        {
            GameManager.Instance.LoadGameplay();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Room && GameManager.Instance.IsPlaying, 10f);
            yield return null;
        }

        private static Health FindPlayerHealth()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.GetComponentInChildren<Health>() : null;
        }

        private static bool DeathCanvasVisible()
        {
            GameObject canvas = GameObject.Find("DeathMenuUICanvas");
            return canvas != null && canvas.activeSelf;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds)
            {
                yield return null;
            }
        }
    }
}
#endif
