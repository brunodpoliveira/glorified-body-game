#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Automation;
using GlorifiedBody.Cameras;
using GlorifiedBody.Core;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The demo in greybox (2026-10-31, Bruno: "create the floor 2 and 1 of the demo in greybox"): every room loads with
    /// Lucy and its systems, its cameras frame its floor, its exits lead to rooms that exist and to entries that exist
    /// there; the main way travels from W1 to U1; the story starts in W1; the end beat in U1 shows the bulletin and goes
    /// back to the menu. Rooms: DemoWard.BuildAll, DemoPublic.BuildAll. Batch: tools/unity.sh test playmode DemoTests.
    /// </summary>
    public class DemoTests
    {
        private static readonly string[] Ward = { "W1", "W2", "W3", "W4", "W5", "W6", "W7", "W8", "W9", "W10", "W11", "W12" };
        private static readonly string[] Public = { "G1", "G2", "G3", "G4", "G5", "G6", "G7", "G8", "G9", "U1" };
        private static readonly string[] MainWay = { "W1", "W2", "W5", "W7", "W8", "W9", "W11", "W9", "W12", "G1", "G2", "G5", "G9", "G5", "U1" };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_demo_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SaveSystem.Folder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryRoom_Loads_ItsCamerasFrameItsFloor_AndItsExitsLeadSomewhere()
        {
            var entries = new Dictionary<string, HashSet<string>>();
            var exits = new List<(string from, string scene, string entry)>();
            var failures = new List<string>();
            foreach (string id in Ward.Concat(Public))
            {
                string scene = "Demo_" + id;
                Assert.IsTrue(Application.CanStreamedLevelBeLoaded(scene), scene + " is in the build (DemoWard/DemoPublic.BuildAll)");
                GameManager.Instance.LoadLevel(scene);
                yield return WaitUntil(() => SceneManager.GetActiveScene().name == scene && GameManager.Instance.IsPlaying, 10f);
                yield return null;
                Assert.AreEqual(scene, SceneManager.GetActiveScene().name);
                Assert.IsNotNull(GameObject.FindGameObjectWithTag("Player"), scene + ": Lucy");
                Assert.AreEqual(0, GlorifiedBody.Enemy.EnemySpawn.Missing, scene + ": every figure's prefab is there");
                FixedCameraDirector director = UnityEngine.Object.FindFirstObjectByType<FixedCameraDirector>();
                CameraCoverage.Report report = CameraCoverage.Check(director, CameraCoverage.GameRoomNear, false);
                Debug.Log($"[DemoTests] {scene}: {report.Uncovered.Count} of {report.Total} floor points out of frame");
                if (report.Uncovered.Count > 0) failures.Add($"{scene}: {report}"); // no blind spot (Bruno, 2026-10-31)
                entries[scene] = new HashSet<string>(UnityEngine.Object.FindObjectsByType<RoomEntry>(FindObjectsSortMode.None).Select(e => e.name));
                foreach (RoomExit exit in UnityEngine.Object.FindObjectsByType<RoomExit>(FindObjectsSortMode.None))
                    exits.Add((scene, exit.targetScene, exit.targetEntry));
            }
            foreach ((string from, string scene, string entry) in exits)
            {
                if (!entries.ContainsKey(scene)) failures.Add($"{from}: an exit to {scene}, a room that does not exist");
                else if (!entries[scene].Contains(entry)) failures.Add($"{from}: an exit to {scene}/{entry}, an entry that does not exist");
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator TheMainWay_TravelsFromW1_ToU1()
        {
            GameManager.Instance.LoadLevel("Demo_W1");
            yield return Room("Demo_W1");
            StoryFlags.Raise(DemoFlags.Released); // the security office's release, for the last door
            for (int i = 1; i < MainWay.Length; i++)
            {
                string from = "Demo_" + MainWay[i - 1], to = "Demo_" + MainWay[i];
                RoomExit exit = UnityEngine.Object.FindObjectsByType<RoomExit>(FindObjectsSortMode.None).FirstOrDefault(e => e.targetScene == to);
                Assert.IsNotNull(exit, $"{from} has an exit to {to}");
                int arrivals = SaveSystem.Arrivals;
                Assert.IsTrue(exit.Use(), $"{from} to {to}");
                yield return WaitUntil(() => SaveSystem.Arrivals > arrivals, 10f);
                yield return Room(to);
                RoomEntry entry = UnityEngine.Object.FindObjectsByType<RoomEntry>(FindObjectsSortMode.None).First(e => e.name == "Entry_From_" + MainWay[i - 1]);
                GameObject lucy = GameObject.FindGameObjectWithTag("Player");
                Assert.Less(Vector3.Distance(lucy.transform.position, entry.transform.position), 1f, $"in {to} she stands at the door from {from}");
            }
        }

        [UnityTest]
        public IEnumerator W1_StartsTheStory_TheFeedLocked_TheShortNearlyFull_StevensSpeaks()
        {
            GameManager.Instance.LoadLevel("Demo_W1");
            yield return Room("Demo_W1");
            yield return new WaitForSeconds(2f);
            Assert.IsTrue(Feeding.Locked, "round 18: the feed starts locked");
            PsychicStats stats = GameObject.FindGameObjectWithTag("Player").GetComponent<PsychicStats>();
            Assert.Greater(stats.Short, PsychicStats.MaxShort * 0.8f, "round 8: she starts nearly full on the SHORT");
            Assert.IsTrue(GlorifiedBody.Subtitles.SubtitleService.Lines.Any(l => l.Speaker == "STEVENS"), "Stevens on the speaker (cutscene 01, as subtitles)");
            Assert.IsNotNull(GameObject.Find("L1_Printout"), "the printout on the printer");
        }

        [UnityTest]
        public IEnumerator U1_TheEnd_TheGrabTearsTheUniformOff_TheBulletin_ThenTheTitle()
        {
            GameManager.Instance.LoadLevel("Demo_U1");
            yield return Room("Demo_U1");
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            GlorifiedBody.Interaction.OutfitChange.Change(lucy, GlorifiedBody.Interaction.OutfitChange.Load(GlorifiedBody.Interaction.OutfitChange.GuardId));
            lucy.GetComponent<TankController>().Teleport(new Vector3(20f, 0f, -25.5f), 180f);
            yield return WaitUntil(() => Bulletin.IsShowing, 60f);
            Assert.IsTrue(Bulletin.IsShowing, "the bulletin after the exit lines");
            Assert.AreEqual(GlorifiedBody.Interaction.OutfitChange.NudeId, lucy.GetComponent<PsychicStats>().Outfit.outfitId, "round 20: the grab tears the uniform off");
            var pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            var driver = new GameObject("[Pad]").AddComponent<PadDriver>();
            UnityEngine.Object.DontDestroyOnLoad(driver.gameObject);
            driver.pad = pad;
            float t0 = Time.realtimeSinceStartup;
            while (SceneManager.GetActiveScene().name != "Main_Menu" && Time.realtimeSinceStartup - t0 < 20f)
            {
                pad.Tap(InputManager.GameAction.Interact);
                yield return new WaitForSecondsRealtime(0.4f);
            }
            GameInput.ClearBotSource(pad);
            UnityEngine.Object.Destroy(driver.gameObject);
            Assert.AreEqual("Main_Menu", SceneManager.GetActiveScene().name, "the title: back to the menu");
        }

        [UnityTest]
        public IEnumerator W11_TheWardSister_GivesTheCard_WhenSheAsks()
        {
            // Bruno, 2026-11-02: "the second way to grab cards from civvies is to simply interact/talk with them".
            GameManager.Instance.LoadLevel("Demo_W11");
            yield return Room("Demo_W11");
            GameObject sister = GameObject.Find("Ward_Sister");
            Assert.IsNotNull(sister);
            var talk = sister.GetComponent<GlorifiedBody.Interaction.CarrierTalk>();
            var carrier = sister.GetComponent<GlorifiedBody.Interaction.ItemCarrier>();
            Assert.IsNotNull(talk, "the unarmed carrier talks");
            Assert.IsTrue(talk.Available);
            Assert.IsTrue(carrier.ShowsItem, "the card on her belt");
            StringAssert.Contains("ask for the ward sister's card", talk.Hint);

            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            Vector3 at = sister.transform.position + sister.transform.forward * 1.1f;
            lucy.GetComponent<TankController>().Teleport(new Vector3(at.x, sister.transform.position.y, at.z), sister.transform.eulerAngles.y + 180f);
            yield return null;
            var hands = lucy.GetComponent<GlorifiedBody.Interaction.Interactor>();
            Assert.AreSame(talk, hands.FindFocus(), "the Interact key reaches her");
            Assert.AreEqual("given:stair_card", talk.Confirm(hands));
            Assert.IsTrue(lucy.GetComponent<GlorifiedBody.Interaction.ItemInventory>().Has("stair_card"), "the card is hers");
            Assert.IsFalse(sister.GetComponent<Health>().IsDead, "the sister lives");
            yield return null;
            Assert.IsFalse(carrier.ShowsItem, "the card leaves her belt");
            Assert.IsFalse(talk.Available, "once");
            Assert.IsTrue(GlorifiedBody.Subtitles.SubtitleService.Lines.Any(l => l.Text.Contains(GlorifiedBody.Interaction.CarrierTalk.LineGive)), "she says her line");

            // A death now drops nothing: the card is gone from the room.
            sister.GetComponent<Health>().TakeDamage(9999f);
            yield return new WaitForSeconds(GlorifiedBody.Interaction.ItemCarrier.FallSeconds + 0.3f);
            Assert.IsFalse(carrier.Dropped, "no second card beside the body");
        }

        [DefaultExecutionOrder(-1000)]
        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad pad;
            private void Update() => pad?.Commit();
        }

        private static class DemoFlags
        {
            public const string Released = "sector_c_released";
        }

        private static IEnumerator Room(string scene)
        {
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == scene && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            Assert.AreEqual(scene, SceneManager.GetActiveScene().name);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
