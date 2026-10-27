#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using System.Linq;
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
            yield return WaitUntil(() => GameManager.Instance.IsGameOver, 5f);
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

        /// <summary>The second wind's 2.5 s of invulnerability must not run out during a pause (the gaps sweep, row 25).</summary>
        [UnityTest]
        public IEnumerator TheSecondWind_OutlastsAPause()
        {
            health.SetInvulnerable(false);
            health.SetSecondWindEnabled(true);
            health.RearmSecondWind();
            health.TakeDamage(health.MaxHealth * 10f);
            Assert.IsTrue(health.IsSecondWindActive, "the second wind");
            GameManager.Instance.Pause();
            yield return new WaitForSecondsRealtime(3f);
            GameManager.Instance.Resume();
            yield return null;
            Assert.IsTrue(health.IsSecondWindInvulnerable, "still protected after a 3 s pause");
            Assert.Greater(health.SecondWindInvulnerabilityRemaining, 2f, "almost all of its 2.5 s left");
        }

        /// <summary>A line must not show over the pause menu (the gaps sweep, row 29).</summary>
        [UnityTest]
        public IEnumerator Subtitles_HideUnderThePauseMenu()
        {
            GlorifiedBody.Enemy.Bark.Say(GameObject.Find("Dummy_B"), "A line during the pause.");
            yield return null;
            GlorifiedBody.Subtitles.SubtitleUI ui = UnityEngine.Object.FindFirstObjectByType<GlorifiedBody.Subtitles.SubtitleUI>();
            Assert.IsNotNull(ui, "the subtitle screen");
            Assert.IsTrue(ui.Visible, "the line shows in play");
            GameManager.Instance.Pause();
            yield return null;
            Assert.IsFalse(ui.Visible, "hidden under the pause menu");
            GameManager.Instance.Resume();
            yield return null;
            Assert.IsTrue(ui.Visible, "back after the pause");
        }

        /// <summary>The "[screams]" caption ends with the scream, not 2.5 s after the freeze (the gaps sweep, row 29).</summary>
        [UnityTest]
        public IEnumerator TheScreamCaption_EndsWithTheFrozenStart()
        {
            yield return FrozenShort();
            Assert.IsTrue(GlorifiedBody.Subtitles.SubtitleService.Lines.Any(l => l.Text == "[screams]"), "the caption during the scream");
            yield return new WaitForSecondsRealtime(ShortController.StartFreeze);
            yield return null;
            Assert.IsFalse(GlorifiedBody.Subtitles.SubtitleService.Lines.Any(l => l.Text == "[screams]"), "gone when the world moves again");
        }

        /// <summary>Per-room statics: the civilians' pose turn and the F12 care flag start again in each room (the gaps sweep, row 39).</summary>
        [UnityTest]
        public IEnumerator TheRoomStatics_StartAgainInTheNextRoom()
        {
            System.Reflection.FieldInfo next = typeof(GlorifiedBody.Enemy.CivilianBrain).GetField("nextVariant", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(next);
            next.SetValue(null, 5);
            GlorifiedBody.Enemy.CorpseCare.DebugHidden = true;
            GameManager.Instance.LoadGameplay();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "TrainingRoom" && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            Assert.AreEqual(0, (int)next.GetValue(null), "the pose turn starts at 0");
            Assert.IsFalse(GlorifiedBody.Enemy.CorpseCare.DebugHidden, "the debug flag is off");
        }

        /// <summary>The Clear look must not leave the shared URP asset changed (the gaps sweep, row 32).</summary>
        [UnityTest]
        public IEnumerator TheClearLook_GivesTheUrpAssetItsOwnSettingBack()
        {
            var asset = UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset;
            int own = asset.msaaSampleCount;
            GlorifiedBody.Rendering.LookQualityApplier.Apply(GlorifiedBody.Rendering.LookQuality.Clear);
            yield return null;
            Assert.AreEqual(GlorifiedBody.Rendering.LookQualityApplier.ClearMsaa, asset.msaaSampleCount, "Clear: MSAA 4x");
            GlorifiedBody.Rendering.LookQualityApplier.RestoreAsset();
            Assert.AreEqual(own, asset.msaaSampleCount, "the asset's own value back (the end of play does this)");
            GlorifiedBody.Rendering.LookQualityApplier.Apply(GlorifiedBody.Rendering.LookQuality.Authentic);
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
