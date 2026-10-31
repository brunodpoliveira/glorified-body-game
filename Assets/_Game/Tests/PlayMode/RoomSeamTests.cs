#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Audio;
using GlorifiedBody.Automation;
using GlorifiedBody.Core;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The seam sweep, part 3 (2026-10-31): faults where the doors, the deaths, the short, the cutscenes and the tapes
    /// meet. Each test failed before its fix. In the travel rooms (TravelTestSetup.BuildTravelTest). Batch:
    /// tools/unity.sh test playmode RoomSeamTests.
    /// </summary>
    public class RoomSeamTests
    {
        private const string A = "TravelTest_A";
        private const string B = "TravelTest_B";

        [DefaultExecutionOrder(-1000)]
        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad pad;
            private void Update() => pad?.Commit();
        }

        private VirtualPad pad;
        private PadDriver driver;
        private GameObject lucy;

        private Health Health => lucy.GetComponentInChildren<Health>();
        private GameManager Gm => GameManager.Instance;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_room_seam_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && Gm != null && Gm.IsInMenu, 10f);
            Gm.LoadLevel(A);
            yield return Room(A);
            pad = new VirtualPad();
            driver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            UnityEngine.Object.DontDestroyOnLoad(driver.gameObject);
            driver.pad = pad;
            GameInput.SetBotSource(pad);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            pad?.ReleaseAll();
            if (pad != null) GameInput.ClearBotSource(pad);
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            TapePlayer.Instance?.Stop();
            SaveSystem.Folder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ARetry_InTheSecondRoom_WithNoCheckpoint_GivesHerBack_AsSheCameIn()
        {
            lucy.GetComponent<ItemInventory>().TryAdd(ItemDefinition.Find("test_key"));
            Health.SetHealth(60f);
            yield return ThroughTheDoor();
            Assert.IsTrue(lucy.GetComponent<ItemInventory>().Has("test_key"), "she came in with the key");
            Vector3 cameIn = lucy.transform.position;
            yield return Die();
            Assert.IsTrue(Gm.IsGameOver, "the death screen");
            if (!Gm.RestartFromLatestCheckpoint()) Gm.RestartLevel(); // RETRY, as DeathMenuUI does it
            yield return Room(B);
            Assert.IsTrue(lucy.GetComponent<ItemInventory>().Has("test_key"), "the key she carried in is hers again");
            Assert.AreEqual(60f, Health.CurrentHealth, 0.01f, "her HP as she came in");
            Assert.Less(Vector3.Distance(cameIn, lucy.transform.position), 1f, "at the door she came in by");
        }

        [UnityTest]
        public IEnumerator ACutscene_DuringTheDoorFade_DoesNotStopTheTrip()
        {
            CutsceneAsset cutscene = Resources.Load<CutsceneAsset>("Cutscenes/cutscene_test");
            Assert.IsNotNull(cutscene);
            int arrivals = SaveSystem.Arrivals;
            Assert.IsTrue(GameObject.Find("Exit_ToB").GetComponent<RoomExit>().Use());
            yield return null;
            CutscenePlayer.Play(cutscene);
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 8f && SaveSystem.Arrivals == arrivals)
            {
                if (CutscenePlayer.IsPlaying && Time.realtimeSinceStartup - t0 > 1f) CutscenePlayer.Skip();
                yield return null;
            }
            Assert.Greater(SaveSystem.Arrivals, arrivals, "she reached the next room");
            Assert.AreEqual(B, SceneManager.GetActiveScene().name);
            Assert.IsFalse(WorldState.Travelling, "the trip ended");
        }

        [UnityTest]
        public IEnumerator ADeath_DuringTheDoorFade_IsADeath()
        {
            Assert.IsTrue(GameObject.Find("Exit_ToB").GetComponent<RoomExit>().Use());
            yield return null;
            Kill();
            Assert.IsTrue(Health.IsDead);
            yield return WaitUntil(() => Gm.IsGameOver, 5f);
            Assert.IsTrue(Gm.IsGameOver, "the death screen, not the next room with her HP from before the hit");
            Assert.AreEqual(A, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator TheShort_EndsAtTheDoor_AndDoesNotStartAgain()
        {
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            ShortController shortController = lucy.GetComponent<ShortController>();
            stats.DebugSet(stats.Nalcon, PsychicStats.MaxShort);
            shortController.Begin();
            Assert.IsTrue(shortController.IsActive);
            yield return new WaitForSeconds(3f); // past the frozen start
            yield return ThroughTheDoor();
            stats = lucy.GetComponent<PsychicStats>();
            Assert.Less(stats.Short, PsychicStats.MaxShort * 0.5f, "the bar does not arrive full: no second short at her next attack");
            Assert.IsFalse(stats.IsShortFull);
        }

        [UnityTest]
        public IEnumerator ALoad_ForgetsATape_SheDidNotHaveThen()
        {
            Assert.IsTrue(SaveSystem.SaveCheckpoint(""));
            ItemDefinition tape = Resources.Load<ItemDefinition>("Items/item_test_tape");
            lucy.GetComponent<ItemInventory>().TryAdd(tape);
            TapePlayer.Instance.Play(tape);
            yield return new WaitForSeconds(0.5f);
            int applied = SaveSystem.Applies;
            Assert.IsTrue(SaveSystem.Load(SaveSystem.CheckpointSlot));
            yield return WaitUntil(() => SaveSystem.Applies > applied, 10f);
            yield return Room(A);
            yield return new WaitForSeconds(0.6f);
            Assert.IsFalse(lucy.GetComponent<ItemInventory>().Has("test_tape"), "the save is from before the tape");
            Assert.IsNull(TapePlayer.Instance.Tape, "the player forgets a tape she does not have");
            Assert.AreEqual("", TapePlayer.Instance.LabelShown);
        }

        [UnityTest]
        public IEnumerator InTheDeathFall_NoInteractNoMap()
        {
            lucy.GetComponent<TankController>().Teleport(new Vector3(3f, 0f, 2.2f), 0f); // the test log 0.8 m ahead
            yield return new WaitForSeconds(0.2f);
            Assert.IsNotNull(lucy.GetComponent<Interactor>().FindFocus(), "the log is in reach");
            Kill();
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(Gm.IsGameOver, "still in the fall");
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(Interactor.State.Free, lucy.GetComponent<Interactor>().Current, "a dead body takes nothing");
            yield return Press(InputManager.GameAction.Map);
            Assert.IsFalse(Gm.IsMap, "no map in the fall");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private IEnumerator ThroughTheDoor()
        {
            int arrivals = SaveSystem.Arrivals;
            Assert.IsTrue(GameObject.Find("Exit_ToB").GetComponent<RoomExit>().Use());
            yield return WaitUntil(() => SaveSystem.Arrivals > arrivals, 10f);
            yield return Room(B);
        }

        /// <summary>One lethal hit, the second wind off (it leaves her at 1 HP, 2.5 s invulnerable).</summary>
        private void Kill()
        {
            Health.SetInvulnerable(false);
            Health.SetSecondWindEnabled(false);
            Health.TakeDamage(Health.MaxHealth * 10f);
        }

        private IEnumerator Die()
        {
            Kill();
            yield return WaitUntil(() => Gm.IsGameOver, 6f);
        }

        private IEnumerator Room(string scene)
        {
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == scene && Gm.IsPlaying, 10f);
            yield return null;
            Assert.AreEqual(scene, SceneManager.GetActiveScene().name);
            lucy = GameObject.FindGameObjectWithTag("Player");
        }

        private IEnumerator Press(InputManager.GameAction action)
        {
            pad.Tap(action);
            yield return null;
            yield return null;
            yield return null;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
