#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The seams between systems that the system sweep of 2026-10-26 found (devlog-notes/2026-10-26_exp_and_sweeps.md),
    /// in the training room: a pause or the inventory during the short's frozen start, a drug in the beat between her
    /// death and the death screen, a save of a room that is not in the build. Batch:
    /// tools/unity.sh test playmode SystemSeamTests.
    /// </summary>
    public class SystemSeamTests
    {
        private GameObject lucy;
        private Health health;
        private PsychicStats stats;
        private PsychicPowers powers;
        private DrugInventory drugs;
        private ShortController shortController;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_seam_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadGameplay();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "TrainingRoom" && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            foreach (DummyShooter shooter in UnityEngine.Object.FindObjectsByType<DummyShooter>(FindObjectsSortMode.None)) shooter.enabled = false;
            lucy = GameObject.FindGameObjectWithTag("Player");
            health = lucy.GetComponentInChildren<Health>();
            stats = lucy.GetComponent<PsychicStats>();
            powers = lucy.GetComponent<PsychicPowers>();
            drugs = lucy.GetComponent<DrugInventory>();
            shortController = lucy.GetComponent<ShortController>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SaveSystem.Folder = null;
            Time.timeScale = 1f;
            yield return null;
        }

        /// <summary>The pause menu during the 2.5 s frozen start of the short: the game must run again after it.</summary>
        [UnityTest]
        public IEnumerator APause_DuringTheShortsFrozenStart_DoesNotStopTheGameForGood()
        {
            yield return FrozenShort();
            GameManager.Instance.Pause();
            yield return new WaitForSecondsRealtime(0.5f);
            GameManager.Instance.Resume();
            yield return WaitUntil(() => Time.timeScale > 0.99f, ShortController.StartFreeze + 3f);
            Assert.AreEqual(1f, Time.timeScale, 0.01f, "the world runs again after the freeze");
            Assert.IsTrue(GameManager.Instance.IsPlaying);
        }

        /// <summary>The same with the inventory overlay.</summary>
        [UnityTest]
        public IEnumerator TheInventory_DuringTheShortsFrozenStart_DoesNotStopTheGameForGood()
        {
            yield return FrozenShort();
            GameManager.Instance.OpenInventory();
            yield return new WaitForSecondsRealtime(0.5f);
            GameManager.Instance.CloseInventory();
            yield return WaitUntil(() => Time.timeScale > 0.99f, ShortController.StartFreeze + 3f);
            Assert.AreEqual(1f, Time.timeScale, 0.01f, "the world runs again after the freeze");
            Assert.IsTrue(GameManager.Instance.IsPlaying);
        }

        /// <summary>A drug in the 0.45 s between her death and the death screen must not bring her back.</summary>
        [UnityTest]
        public IEnumerator ADrug_InTheDeathBeat_DoesNotBringHerBack()
        {
            drugs.DebugSet(DrugType.Nalcon, 1);
            health.SetInvulnerable(false);
            health.SetSecondWindEnabled(false);
            health.TakeDamage(health.MaxHealth * 10f);
            Assert.IsTrue(health.IsDead, "she died");
            yield return null;
            powers.UseDrug();
            Assert.IsTrue(health.IsDead, "she stays dead");
            Assert.AreEqual(1, drugs.Count(DrugType.Nalcon), "the drug is not spent");
            yield return WaitUntil(() => GameManager.Instance.IsGameOver, 3f);
            Assert.IsTrue(GameManager.Instance.IsGameOver, "the death screen comes");
            Assert.IsTrue(health.IsDead, "she is dead under the death screen");
        }

        /// <summary>A save of a room that is not in the build (a renamed room) must not hang the game or replace the checkpoint.</summary>
        [UnityTest]
        public IEnumerator ASaveOfAMissingRoom_RefusesToLoad_AndKeepsTheCheckpoint()
        {
            SavePoint point = UnityEngine.Object.FindFirstObjectByType<SavePoint>();
            Assert.IsTrue(SaveSystem.SaveManual(2, point.SpotId), "a good save in slot 2, and the checkpoint");
            string checkpoint = File.ReadAllText(SaveSystem.PathOf(SaveSystem.CheckpointSlot));
            Assert.IsTrue(SaveSystem.SaveManual(1, point.SpotId));
            string path = SaveSystem.PathOf(1);
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"TrainingRoom\"", "\"NoSuchRoom\""));
            File.WriteAllText(SaveSystem.PathOf(SaveSystem.CheckpointSlot), checkpoint);

            LogAssert.ignoreFailingMessages = true; // the refusal logs a warning; Unity's own load error would log an error
            bool started = SaveSystem.Load(1);
            yield return new WaitForSecondsRealtime(1f);
            LogAssert.ignoreFailingMessages = false;
            Assert.IsFalse(started, "the load refuses");
            Assert.IsTrue(GameManager.Instance.IsPlaying, "the game goes on");
            Assert.AreEqual("TrainingRoom", SceneManager.GetActiveScene().name);
            Assert.AreEqual(checkpoint, File.ReadAllText(SaveSystem.PathOf(SaveSystem.CheckpointSlot)), "the checkpoint stays");
        }

        private IEnumerator FrozenShort()
        {
            stats.RegenPaused = true;
            stats.DebugSet(100f, PsychicStats.MaxShort);
            health.SetInvulnerable(true);
            shortController.Begin();
            Assert.IsTrue(shortController.IsActive, "the short started");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(0f, Time.timeScale, 0.01f, "the world is frozen");
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
