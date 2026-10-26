#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Automation;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// Room travel and the world state (2026-10-26, the design sweep's rank 1), in the two travel rooms (in git:
    /// TravelTestSetup.BuildTravelTest): an exit takes her to the next room with her state; the room she left stays as
    /// she left it (the dead with their bodies, the taken pill); she walks through a door; a save keeps every room;
    /// the main menu starts a new world. Batch: tools/unity.sh test playmode TravelTests.
    /// </summary>
    public class TravelTests
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

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_travel_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(A);
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
            if (pad != null)
            {
                pad.ReleaseAll();
                GameInput.ClearBotSource(pad);
            }
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            SaveSystem.Folder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnExit_TakesHerToTheNextRoom_WithHerState()
        {
            Health health = lucy.GetComponentInChildren<Health>();
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            stats.AddExp(130, "test"); // level 2 first: its +10 HP would change the 60 below
            health.SetHealth(60f);
            stats.DebugSet(30f, 10f);
            lucy.GetComponent<ItemInventory>().TryAdd(ItemDefinition.Find("test_key"));
            lucy.GetComponent<DrugInventory>().DebugSet(DrugType.Red, 2);

            yield return Cross("Exit_ToB", B);
            Assert.AreEqual(B, SceneManager.GetActiveScene().name);
            health = lucy.GetComponentInChildren<Health>();
            stats = lucy.GetComponent<PsychicStats>();
            Assert.AreEqual(60f, health.CurrentHealth, 3f, "her HP (the nude outfit heals 1 a second)");
            Assert.AreEqual(30f, stats.Nalcon, 3f, "her MK");
            Assert.AreEqual(130, stats.Exp, "her exp");
            Assert.AreEqual(2, stats.Level, "her level");
            Assert.IsTrue(lucy.GetComponent<ItemInventory>().Has("test_key"), "her key");
            Assert.AreEqual(2, lucy.GetComponent<DrugInventory>().Count(DrugType.Red), "her pills");
            Transform entry = GameObject.Find("Entry_FromA").transform;
            Assert.Less(Flat(lucy.transform.position, entry.position), 0.3f, "at the entry of the door she came through");
            yield return new WaitForSecondsRealtime(WorldState.FadeInSeconds + 0.2f);
            Assert.AreEqual(0f, ScreenFade.Alpha, 0.01f, "the screen comes back");
            Assert.IsFalse(WorldState.Travelling);
        }

        [UnityTest]
        public IEnumerator TheRoomSheLeft_StaysAsSheLeftIt()
        {
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            Health dummy = GameObject.Find("Dummy_Travel").GetComponent<Health>();
            dummy.TakeDamage(dummy.MaxHealth * 10f);
            HitEventBus.Raise(new HitInfo { Attacker = lucy, Victim = dummy.gameObject, HitPoint = dummy.transform.position + Vector3.up, DamageAmount = 999f, DamageType = DamageType.PsychicBlast, WasFatal = true });
            UnityEngine.Object.Destroy(GameObject.Find("Drug_Nalcon")); // taken, as ItemPickup.Confirm does
            yield return new WaitForSeconds(1.5f); // the body falls
            int kills = stats.Kills, exp = stats.Exp;

            yield return Cross("Exit_ToB", B);
            yield return Cross("Exit_ToA", A);
            Assert.AreEqual(A, SceneManager.GetActiveScene().name);
            stats = lucy.GetComponent<PsychicStats>();
            GameObject body = GameObject.Find("Dummy_Travel");
            Assert.IsNotNull(body, "the body is still in the room");
            Assert.IsTrue(body.GetComponent<Health>().IsDead, "dead, as she left him");
            GameObject pill = GameObject.Find("Drug_Nalcon");
            Assert.IsTrue(pill == null || !pill.activeInHierarchy, "the pill she took stays taken");
            Assert.AreEqual(kills, stats.Kills, "no second kill for the old body");
            Assert.AreEqual(exp, stats.Exp, "no second exp");
            Assert.Less(Flat(lucy.transform.position, GameObject.Find("Entry_FromB").transform.position), 0.3f, "at the door from B");
            Assert.IsTrue(WorldState.Visited(A) && WorldState.Visited(B), "both rooms are on record");
        }

        [UnityTest]
        public IEnumerator SheWalksThroughTheDoor_IntoTheNextRoom()
        {
            lucy.GetComponent<TankController>().Teleport(new Vector3(4.2f, 0f, 0f), 90f);
            yield return new WaitForSeconds(0.3f);
            int arrivals = SaveSystem.Arrivals;
            pad.SetHeld(InputManager.GameAction.Forward, true);
            yield return WaitUntil(() => SaveSystem.Arrivals > arrivals, 8f);
            pad.ReleaseAll();
            Assert.Greater(SaveSystem.Arrivals, arrivals, "she walked into the exit");
            yield return Room(B);
            Assert.AreEqual(B, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator ASave_KeepsEveryRoom_AndTheMenuStartsANewWorld()
        {
            Health dummy = GameObject.Find("Dummy_Travel").GetComponent<Health>();
            dummy.TakeDamage(dummy.MaxHealth * 10f);
            yield return new WaitForSeconds(1.2f);
            yield return Cross("Exit_ToB", B);
            SavePoint point = UnityEngine.Object.FindFirstObjectByType<SavePoint>();
            Assert.IsTrue(SaveSystem.SaveManual(1, point.SpotId), "saved in B");
            Assert.AreEqual(1, SaveSystem.Read(1).rooms.Count, "the save keeps room A");

            GameManager.Instance.LoadMainMenu();
            yield return WaitUntil(() => GameManager.Instance.IsInMenu, 10f);
            Assert.AreEqual(0, WorldState.Rooms.Count, "the menu forgets the rooms");

            int applied = SaveSystem.Applies;
            Assert.IsTrue(SaveSystem.Load(1));
            yield return WaitUntil(() => SaveSystem.Applies > applied, 10f);
            yield return Room(B);
            yield return Cross("Exit_ToA", A);
            Assert.IsTrue(GameObject.Find("Dummy_Travel").GetComponent<Health>().IsDead, "room A as the save had it");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private IEnumerator Cross(string exitName, string scene)
        {
            int arrivals = SaveSystem.Arrivals;
            RoomExit exit = GameObject.Find(exitName).GetComponent<RoomExit>();
            Assert.IsTrue(exit.Use(), exitName + " opens");
            yield return WaitUntil(() => SaveSystem.Arrivals > arrivals, 10f);
            Assert.Greater(SaveSystem.Arrivals, arrivals, "arrived in " + scene);
            yield return Room(scene);
        }

        private IEnumerator Room(string scene)
        {
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == scene && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            Assert.AreEqual(scene, SceneManager.GetActiveScene().name, "the room " + scene + " (TravelTestSetup.BuildTravelTest)");
            lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
