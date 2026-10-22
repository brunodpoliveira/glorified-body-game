#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Automation;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;
using GlorifiedBody.Tutorial;
using GlorifiedBody.UI;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The Interact and Scan keys (Bruno, 2026-10-22), in the tutorial gym where the test log and the two scan
    /// targets stand: Interact asks first and takes on a second press, Back leaves the item, a log opens for
    /// reading, a full inventory refuses, the guard outfit carries more, Scan shows the picture of the door and
    /// plays the clip of the monitors, the two keys never do each other's job.
    /// Batch: tools/unity.sh test playmode.
    /// </summary>
    public class InteractionTests
    {
        private const string MainMenu = "Main_Menu";

        private VirtualPad pad;
        private PadDriver driver;
        private TutorialDirector director;
        private TankController tank;
        private Interactor interactor;
        private PsychicScanner scanner;
        private ItemInventory inventory;
        private InteractionUI ui;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainMenu);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == MainMenu && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(TutorialDirector.SceneName);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == TutorialDirector.SceneName && GameManager.Instance.IsPlaying, 10f);
            yield return new WaitForSecondsRealtime(0.5f);
            director = TutorialDirector.Instance;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            tank = player.GetComponent<TankController>();
            interactor = player.GetComponent<Interactor>();
            scanner = player.GetComponent<PsychicScanner>();
            inventory = player.GetComponent<ItemInventory>();
            ui = UnityEngine.Object.FindFirstObjectByType<InteractionUI>();
            Assert.IsNotNull(interactor, "the interactor on Lucy");
            Assert.IsNotNull(scanner, "the scanner on Lucy");
            Assert.IsNotNull(inventory, "the item inventory on Lucy");
            Assert.IsNotNull(ui, "the interaction UI in the room");
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            driver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            driver.Pad = pad;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameInput.ClearBotSource(pad);
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Interact_AsksFirst_ThenTakesTheLog_AndOpensItForReading()
        {
            ItemPickup log = TutorialDrive.FindPickup(director.LogItemId);
            Assert.IsNotNull(log, "the test log in the gym");
            yield return StandFacing(director.LogPoint, log.transform.position);
            Assert.AreSame(log, interactor.Focus, "the log is in reach and in front of her");
            Assert.IsTrue(ui.HintShown.Contains("take the test log"), "the hint names it: " + ui.HintShown);

            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(Interactor.State.Confirming, interactor.Current, "the first press asks");
            Assert.IsTrue(ui.ConfirmShown, "the question on screen");
            Assert.IsFalse(inventory.Has(director.LogItemId), "nothing taken yet");
            Assert.IsTrue(tank.MovementLocked, "she stands still while she answers");

            yield return Press(InputManager.GameAction.Interact);
            Assert.IsTrue(inventory.Has(director.LogItemId), "the second press takes it");
            Assert.AreEqual(Interactor.State.Reading, interactor.Current, "a log opens for reading");
            Assert.IsTrue(ui.ReaderShown, "the page on screen");
            Assert.IsTrue(ui.ReaderTextShown.Contains("PLACEHOLDER"), "the log's text");
            Assert.IsTrue(log == null, "the log left the room");

            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(Interactor.State.Free, interactor.Current, "Interact closes the page");
            Assert.IsFalse(ui.ReaderShown);
            Assert.IsFalse(tank.MovementLocked, "she walks again");
            Assert.AreEqual(Scans0, scanner.Scans, "Interact never scans");
        }

        [UnityTest]
        public IEnumerator Back_LeavesTheItemWhereItIs()
        {
            ItemPickup log = TutorialDrive.FindPickup(director.LogItemId);
            yield return StandFacing(director.LogPoint, log.transform.position);
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(Interactor.State.Confirming, interactor.Current);
            yield return Press(InputManager.GameAction.Back);
            Assert.AreEqual(Interactor.State.Free, interactor.Current, "Back answers no");
            Assert.IsFalse(inventory.Has(director.LogItemId), "nothing taken");
            Assert.IsTrue(log != null && log.gameObject.activeInHierarchy, "the log stays in the room");
            Assert.IsFalse(tank.MovementLocked);
        }

        [UnityTest]
        public IEnumerator AFullInventory_RefusesThePickup_AndTheGuardOutfitCarriesMore()
        {
            PsychicStats stats = tank.GetComponent<PsychicStats>();
            int nudeSlots = inventory.Capacity;
            Assert.AreEqual("nude", stats.Outfit.outfitId);
            var filler = ScriptableObject.CreateInstance<ItemDefinition>();
            filler.itemId = "filler";
            while (!inventory.IsFull) inventory.TryAdd(filler);

            ItemPickup log = TutorialDrive.FindPickup(director.LogItemId);
            yield return StandFacing(director.LogPoint, log.transform.position);
            yield return Press(InputManager.GameAction.Interact);
            yield return Press(InputManager.GameAction.Interact);
            Assert.IsTrue(interactor.LastResult.StartsWith("full:"), "a full inventory refuses: " + interactor.LastResult);
            Assert.IsFalse(inventory.Has(director.LogItemId));
            Assert.IsTrue(log != null && log.gameObject.activeInHierarchy, "the log stays in the room");
            Assert.AreEqual(Interactor.State.Free, interactor.Current);

            stats.SetOutfit(Resources.Load<OutfitProfile>("Outfits/outfit_guard"));
            Assert.Greater(inventory.Capacity, nudeSlots, "the guard outfit has more slots (Bruno, 2026-10-22)");
            Assert.IsFalse(inventory.IsFull, "room for the log now");
            inventory.Clear();
            UnityEngine.Object.Destroy(filler);
        }

        [UnityTest]
        public IEnumerator Scan_OnTheDoor_ShowsThePicture_AndInteractDoesNotScan()
        {
            Scannable door = TutorialDrive.FindScannable(director.DoorScanId);
            Assert.IsNotNull(door, "the door is a scan target");
            Assert.IsNotNull(door.image, "the placeholder picture");
            yield return StandFacing(director.BarsPoint, door.FocusPoint);
            Assert.AreSame(door, scanner.Focus, "the door is in reach and in front of her");
            Assert.IsTrue(ui.HintShown.Contains("scan"), "the hint offers the scan: " + ui.HintShown);

            yield return Press(InputManager.GameAction.Interact);
            Assert.IsFalse(scanner.IsViewing, "Interact never scans");
            Assert.AreEqual(Interactor.State.Free, interactor.Current, "nothing to take at the door");

            yield return Press(InputManager.GameAction.Scan);
            Assert.IsTrue(scanner.IsViewing, "Scan reads the door");
            Assert.IsTrue(ui.ScanShown, "the scan view on screen");
            Assert.AreSame(door.image, ui.ScanTextureShown, "the door's picture");
            Assert.IsTrue(tank.MovementLocked, "she stands still while she sees it");
            yield return new WaitForSecondsRealtime(PsychicScanner.MinViewSeconds + 0.1f);
            yield return Press(InputManager.GameAction.Scan);
            Assert.IsFalse(scanner.IsViewing, "Scan again closes it");
            Assert.IsFalse(ui.ScanShown);
            Assert.AreEqual(door.scanId, scanner.LastClosedId);
            Assert.IsFalse(tank.MovementLocked);
        }

        [UnityTest]
        public IEnumerator Scan_OnTheMonitors_PlaysTheClipToItsEnd()
        {
            Scannable monitors = TutorialDrive.FindScannable(director.MonitorsScanId);
            Assert.IsNotNull(monitors, "the monitors are a scan target");
            Assert.IsNotNull(monitors.clip, "the placeholder clip");
            yield return StandFacing(director.MonitorsPoint, monitors.FocusPoint);
            yield return Press(InputManager.GameAction.Scan);
            Assert.IsTrue(scanner.IsViewing, "Scan reads the monitors");
            float t0 = Time.realtimeSinceStartup;
            while (ui.VideoFramesSeen < 30 && Time.realtimeSinceStartup - t0 < 5f) yield return null;
            Assert.GreaterOrEqual(ui.VideoFramesSeen, 30, "the clip plays (frames seen)");
            while (scanner.IsViewing && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            Assert.IsFalse(scanner.IsViewing, "the view closes at the end of the clip");
            Assert.IsTrue(scanner.LastClosedWasVideo);
            Assert.IsTrue(scanner.LastResult.StartsWith("ended:"), "it ended by itself: " + scanner.LastResult);
        }

        [UnityTest]
        public IEnumerator Scan_FacingAway_FindsNothing()
        {
            Scannable door = TutorialDrive.FindScannable(director.DoorScanId);
            Vector3 away = director.BarsPoint + (director.BarsPoint - door.FocusPoint);
            yield return StandFacing(director.BarsPoint, away);
            Assert.IsNull(scanner.Focus, "the door is behind her");
            yield return Press(InputManager.GameAction.Scan);
            Assert.IsFalse(scanner.IsViewing);
            Assert.AreEqual("nothing", scanner.LastResult);
        }

        // ── Helpers ───────────────────────────────────────────────────

        private int Scans0 => 0;

        private IEnumerator StandFacing(Vector3 point, Vector3 target)
        {
            Vector3 to = target - point;
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            tank.Teleport(point, yaw);
            yield return null;
            yield return null;
        }

        private IEnumerator Press(InputManager.GameAction action)
        {
            pad.Tap(action);
            yield return null;
            yield return null;
            yield return null;
        }

        [DefaultExecutionOrder(-1000)]
        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad Pad;
            private void Update() => Pad?.Commit();
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
