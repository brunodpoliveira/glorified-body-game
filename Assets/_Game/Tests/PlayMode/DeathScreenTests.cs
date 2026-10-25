#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The game-over screen by killer (Bruno, 2026-10-25: "put in code for the game over screen (i.e killed by
    /// assault_rifle_guard) so that we can code test the feature before building assets for it"). In the AI test
    /// room one soldier at a time kills her for real; the screen must show that killer's entry: its hint, its voice
    /// line as a caption, and the card with the picture note and the killer id. Batch: tools/unity.sh test
    /// playmode DeathScreenTests.
    /// </summary>
    public class DeathScreenTests
    {
        private GameObject lucy;
        private Health health;
        private SoldierBrain[] soldiers;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel("AiTest");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "AiTest" && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            lucy = GameObject.FindGameObjectWithTag("Player");
            health = lucy.GetComponentInChildren<Health>();
            soldiers = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None);
        }

        [UnityTest]
        public IEnumerator KilledByTheRifleSoldier_TheScreenShowsTheRifleEntry()
        {
            yield return DieTo("Soldier_A");
            DeathMenuUI screen = UnityEngine.Object.FindFirstObjectByType<DeathMenuUI>();
            Debug.Log($"[DeathScreenTests] killer {DeathMenuUI.LastKillerId}, entry {screen.Shown?.killerId}, caption '{screen.CaptionShown}', card '{screen.PictureNoteShown}'");
            Assert.AreEqual("soldier_rifle", DeathMenuUI.LastKillerId, "the rifle soldier killed her");
            Assert.AreEqual("soldier_rifle", screen.Shown.killerId, "the rifle's own entry");
            Assert.AreEqual(screen.Shown.hint, screen.HintShown, "its hint on the screen");
            StringAssert.Contains("killer: soldier_rifle", screen.PictureNoteShown, "the card names the killer until the picture exists");
            if (ClassEntry("soldier") != null) Assert.AreEqual(ClassEntry("soldier").voiceText, screen.CaptionShown, "the soldier's voice line as the caption");
        }

        [UnityTest]
        public IEnumerator KilledByTheGuard_TheScreenFallsBackToTheGuardClass()
        {
            yield return DieTo("Guard_B");
            DeathMenuUI screen = UnityEngine.Object.FindFirstObjectByType<DeathMenuUI>();
            Debug.Log($"[DeathScreenTests] killer {DeathMenuUI.LastKillerId}, entry {screen.Shown?.killerId}, hint '{screen.HintShown}'");
            Assert.AreEqual("guard_pistol", DeathMenuUI.LastKillerId, "the guard killed her");
            DeathScreenContent content = Resources.Load<DeathScreenContent>(DeathMenuUI.ContentResourceName);
            Assert.AreSame(content.Find("guard_pistol"), screen.Shown, "the entry the lookup gives");
            Assert.AreNotSame(content.fallback, screen.Shown, "not the fallback: the guard class entry");
        }

        [UnityTest]
        public IEnumerator ANewRoom_ForgetsTheLastKiller()
        {
            DeathMenuUI.LastKillerId = "soldier_rifle";
            GameManager.Instance.RestartLevel();
            yield return WaitUntil(() => SceneManager.GetActiveScene().isLoaded && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            Assert.AreEqual("", DeathMenuUI.LastKillerId, "a reload clears the killer");
        }

        /// <summary>Only the named soldier stays; it kills her for real (no second wind, low HP).</summary>
        private IEnumerator DieTo(string killer)
        {
            foreach (SoldierBrain s in soldiers.Where(s => s.name != killer)) s.gameObject.SetActive(false);
            health.SetInvulnerable(false);
            health.SetSecondWindEnabled(false);
            health.SetHealth(5f);
            yield return WaitUntil(() => GameManager.Instance.IsGameOver, 40f);
            Assert.IsTrue(GameManager.Instance.IsGameOver, killer + " killed her");
            yield return new WaitForSecondsRealtime(0.5f);
        }

        private static DeathScreenContent.Entry ClassEntry(string id)
        {
            DeathScreenContent content = Resources.Load<DeathScreenContent>(DeathMenuUI.ContentResourceName);
            return content != null ? content.Exact(id) : null;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
