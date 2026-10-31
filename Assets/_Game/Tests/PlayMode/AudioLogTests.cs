#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Audio;
using GlorifiedBody.Automation;
using GlorifiedBody.Core;
using GlorifiedBody.Interaction;
using GlorifiedBody.Map;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Subtitles;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The audio logs (Bruno, 2026-10-31: "it can be collected and listened to while walking around; it automatically
    /// pauses when combat begins; music goes down to almost nothing"; it plays again "once the player un-pauses it";
    /// replay from the inventory "yes"), in TRAVEL A (the tape: TravelTestSetup.BuildTravelTest). Batch:
    /// tools/unity.sh test playmode AudioLogTests.
    /// </summary>
    public class AudioLogTests
    {
        private const string A = "TravelTest_A";
        private const string B = "TravelTest_B";
        private const string TapeId = "test_tape";

        [DefaultExecutionOrder(-1000)]
        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad pad;
            private void Update() => pad?.Commit();
        }

        private VirtualPad pad;
        private PadDriver driver;
        private GameObject lucy;

        private static TapePlayer Tapes => TapePlayer.Instance;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_tape_tests");
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
            pad.ReleaseAll();
            GameInput.ClearBotSource(pad);
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            Tapes?.Stop();
            SaveSystem.Folder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TakingTheTape_PlaysIt_WhileSheWalks_AndTheMusicDrops()
        {
            Assert.IsNotNull(Tapes, "the tape player lives on the sound system");
            ItemPickup pickup = GameObject.Find("Tape_Travel").GetComponent<ItemPickup>();
            Assert.AreEqual(ItemKind.AudioLog, pickup.item.kind);
            Assert.IsNotNull(pickup.item.audioClip, "the recording (tools/tape_placeholder.py)");
            lucy.GetComponent<TankController>().Teleport(pickup.transform.position + new Vector3(0f, 0f, 1f), 180f);
            yield return null;
            yield return Press(InputManager.GameAction.Interact); // "Take it?"
            yield return Press(InputManager.GameAction.Interact); // yes: it plays
            Assert.IsTrue(Tapes.IsPlaying, "the tape plays at once");
            Assert.AreEqual(TapeId, Tapes.Tape.itemId);
            Assert.IsTrue(GameManager.Instance.IsPlaying, "the world goes on (no page, no stop)");
            Assert.AreEqual(Interactor.State.Free, lucy.GetComponent<Interactor>().Current, "her hands are free");
            Assert.IsTrue(lucy.GetComponent<ItemInventory>().Has(TapeId), "it is in her inventory");
            Assert.AreEqual(0, lucy.GetComponent<ItemInventory>().UsedSlots, "an audio log takes no slot");

            yield return new WaitForSeconds(1.2f);
            SubtitleService.Line line = SubtitleService.Lines.FirstOrDefault(l => l.Owner == TapePlayer.SubtitleOwner);
            Assert.IsNotNull(line, "its line shows as a subtitle");
            Assert.AreEqual("This is a placeholder tape.", line.Text);
            Assert.AreEqual("TAPE", line.Speaker, "the subtitle names the speaker");
            Assert.LessOrEqual(MusicDirector.Instance.DuckFactor, TapePlayer.MusicLevel + 0.001f, "the music drops to almost nothing");
            StringAssert.Contains("TEST TAPE", Tapes.LabelShown);

            Vector3 from = lucy.transform.position;
            float at = Tapes.Position;
            pad.SetHeld(InputManager.GameAction.Forward, true);
            yield return new WaitForSeconds(0.8f);
            pad.SetHeld(InputManager.GameAction.Forward, false);
            Assert.Greater(Vector3.Distance(from, lucy.transform.position), 0.3f, "she walks while it plays");
            Assert.IsTrue(Tapes.IsPlaying);
            Assert.Greater(Tapes.Position, at + 0.5f, "the tape runs on");

            MapRoom room = MapAtlas.Instance.Room(A);
            Assert.IsTrue(room.marks.Any(m => m.path == "Tape_Travel" && m.kind == MapMarkKind.Log), "the map marks an audio log as a log");
        }

        [UnityTest]
        public IEnumerator AFight_PausesTheTape_UntilSheResumesIt_FromTheInventory()
        {
            ItemDefinition tape = GiveTape();
            Tapes.Play(tape);
            yield return new WaitForSeconds(1.5f);
            MusicDirector.Threat(); // a hit: the music goes to Combat
            yield return WaitUntil(() => !Tapes.IsPlaying, 2f);
            Assert.IsFalse(Tapes.IsPlaying, "a fight pauses the tape");
            Assert.AreEqual(TapePlayer.PausedByCombat, Tapes.StoppedBy);
            Assert.AreEqual(1, Tapes.CombatPauses);
            float pausedAt = Tapes.Position;
            Assert.Greater(pausedAt, 1f);
            Assert.IsFalse(SubtitleService.Lines.Any(l => l.Owner == TapePlayer.SubtitleOwner), "its subtitle goes");
            StringAssert.Contains("a fight", Tapes.LabelShown);
            yield return null;
            Assert.Greater(MusicDirector.Instance.DuckFactor, TapePlayer.MusicLevel + 0.1f, "the fight's music comes back up");

            yield return WaitUntil(() => MusicDirector.Instance.State != MusicState.Combat, MusicDirector.CombatHoldSeconds + 3f);
            Assert.AreNotEqual(MusicState.Combat, MusicDirector.Instance.State, "the fight is over");
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(Tapes.IsPlaying, "it stays paused: she plays it again herself");
            Assert.AreEqual(pausedAt, Tapes.Position, 0.01f);

            InventoryUI inventory = UnityEngine.Object.FindFirstObjectByType<InventoryUI>();
            yield return Press(InputManager.GameAction.Inventory);
            Assert.IsTrue(inventory.IsOpen);
            inventory.SelectIndex(IndexOf(inventory, tape));
            StringAssert.StartsWith("tape_play:", inventory.Act(), "Interact on the tape plays it");
            yield return Press(InputManager.GameAction.Inventory);
            Assert.IsFalse(inventory.IsOpen);
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(Tapes.IsPlaying, "it plays again");
            Assert.GreaterOrEqual(Tapes.Position, pausedAt, "from where the fight stopped it");
        }

        [UnityTest]
        public IEnumerator TheInventory_PausesIt_AndAnEndedTapePlaysFromTheStart()
        {
            ItemDefinition tape = GiveTape();
            InventoryUI inventory = UnityEngine.Object.FindFirstObjectByType<InventoryUI>();
            Tapes.Play(tape);
            yield return new WaitForSeconds(1.2f);
            yield return Press(InputManager.GameAction.Inventory);
            inventory.SelectIndex(IndexOf(inventory, tape));
            StringAssert.StartsWith("tape_pause:", inventory.Act(), "Interact on a playing tape pauses it");
            Assert.AreEqual(TapePlayer.PausedByPlayer, Tapes.StoppedBy);
            float pausedAt = Tapes.Position;
            yield return Press(InputManager.GameAction.Inventory);
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(Tapes.IsPlaying);
            Assert.AreEqual(pausedAt, Tapes.Position, 0.01f, "a paused tape holds its place");

            Assert.IsTrue(Tapes.Toggle(tape), "and plays again");
            Tapes.Seek(Tapes.Length - 0.3f);
            yield return WaitUntil(() => !Tapes.IsPlaying, 2f);
            Assert.AreEqual(TapePlayer.Ended, Tapes.StoppedBy, "the tape ends");
            Assert.AreEqual("", Tapes.LabelShown, "the label goes at the end");
            Assert.IsTrue(Tapes.Toggle(tape), "she plays it again");
            Assert.Less(Tapes.Position, 0.5f, "from the start");
        }

        [UnityTest]
        public IEnumerator TheTape_PlaysOnThroughADoor_AndStopsWithTheWorld()
        {
            ItemDefinition tape = GiveTape();
            Tapes.Play(tape);
            yield return new WaitForSeconds(1f);
            yield return Press(InputManager.GameAction.Map); // the map stops the world
            Assert.IsTrue(GameManager.Instance.IsMap);
            float at = Tapes.Position;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(at, Tapes.Position, 0.01f, "the tape stops with the world");
            yield return Press(InputManager.GameAction.Map);
            int arrivals = SaveSystem.Arrivals;
            Assert.IsTrue(GameObject.Find("Exit_ToB").GetComponent<RoomExit>().Use());
            yield return WaitUntil(() => SaveSystem.Arrivals > arrivals, 10f);
            yield return Room(B);
            Assert.IsTrue(Tapes.IsPlaying, "it plays on in the next room");
            Assert.Greater(Tapes.Position, at);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(SubtitleService.Lines.Any(l => l.Owner == TapePlayer.SubtitleOwner), "its line shows again after the load");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private ItemDefinition GiveTape()
        {
            ItemDefinition tape = Resources.Load<ItemDefinition>("Items/item_" + TapeId);
            Assert.IsNotNull(tape, "the test tape (ItemAssets.TestTape)");
            lucy.GetComponent<ItemInventory>().TryAdd(tape);
            return tape;
        }

        private static int IndexOf(InventoryUI inventory, ItemDefinition item)
        {
            for (int i = 0; i < inventory.Entries.Count; i++)
                if (inventory.Entries[i].Item == item) return i;
            Assert.Fail("the tape is in the inventory list");
            return -1;
        }

        private IEnumerator Room(string scene)
        {
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == scene && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            Assert.AreEqual(scene, SceneManager.GetActiveScene().name);
            lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
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
