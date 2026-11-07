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
            if (GameManager.Instance != null && GameManager.Instance.IsInventory) GameManager.Instance.CloseInventory();
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
            AssertWorldStopped("the page");

            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(Interactor.State.Free, interactor.Current, "Interact closes the page");
            Assert.IsFalse(ui.ReaderShown);
            Assert.IsFalse(tank.MovementLocked, "she walks again");
            AssertWorldRuns("the page closed");
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
        public IEnumerator AFullInventory_RefusesAGrabbable_ButNeverAKeyItem_AndTheGuardOutfitCarriesMore()
        {
            PsychicStats stats = tank.GetComponent<PsychicStats>();
            int nudeSlots = inventory.Capacity;
            Assert.AreEqual("nude", stats.Outfit.outfitId);
            var filler = ScriptableObject.CreateInstance<ItemDefinition>();
            filler.itemId = "filler";
            filler.kind = ItemKind.Object;
            while (!inventory.IsFull) inventory.TryAdd(filler);

            ItemPickup capsule = TutorialDrive.FindPickup("nalcon");
            yield return StandFacing(capsule.Point + Vector3.forward * 0.8f, capsule.Point);
            yield return Press(InputManager.GameAction.Interact);
            yield return Press(InputManager.GameAction.Interact);
            Assert.IsTrue(interactor.LastResult.StartsWith("full:"), "a full inventory refuses a grabbable: " + interactor.LastResult);
            Assert.AreEqual(Interactor.State.Message, interactor.Current, "it says so");
            Assert.IsTrue(capsule != null && capsule.gameObject.activeInHierarchy, "the capsule stays in the room");
            yield return Press(InputManager.GameAction.Interact);

            ItemPickup log = TutorialDrive.FindPickup(director.LogItemId);
            yield return StandFacing(director.LogPoint, log.Point);
            yield return Press(InputManager.GameAction.Interact);
            yield return Press(InputManager.GameAction.Interact);
            Assert.IsTrue(inventory.Has(director.LogItemId), "a key item fits a full inventory: it takes no slot");
            yield return Press(InputManager.GameAction.Interact);

            stats.SetOutfit(Resources.Load<OutfitProfile>("Outfits/outfit_guard"));
            Assert.Greater(inventory.Capacity, nudeSlots, "the guard outfit has more slots (Bruno, 2026-10-22)");
            Assert.IsFalse(inventory.IsFull, "room for the capsule now");
            inventory.Clear();
            UnityEngine.Object.Destroy(filler);
        }

        [UnityTest]
        public IEnumerator Scan_OnTheDoor_ShowsThePicture_AndInteractDoesNotScan()
        {
            Scannable door = TutorialDrive.FindScannable(director.DoorScanId);
            Assert.IsNotNull(door, "the door is a scan target");
            Assert.IsNotNull(door.image, "the placeholder picture");
            yield return StandFacing(director.DoorPoint, door.FocusPoint);
            Assert.AreSame(door, scanner.Focus, "the door is in reach and in front of her");
            Assert.IsTrue(ui.HintShown.Contains("scan"), "the hint offers the scan: " + ui.HintShown);

            yield return Press(InputManager.GameAction.Interact);
            Assert.IsFalse(scanner.IsViewing, "Interact never scans");
            Assert.AreEqual(Interactor.State.Message, interactor.Current, "Interact at the door says it is locked");
            yield return Press(InputManager.GameAction.Back);
            Assert.AreEqual(Interactor.State.Free, interactor.Current);

            yield return Press(InputManager.GameAction.Scan);
            Assert.IsTrue(scanner.IsViewing, "Scan reads the door");
            Assert.IsTrue(ui.ScanShown, "the scan view on screen");
            Assert.AreSame(door.image, ui.ScanTextureShown, "the door's picture");
            Assert.IsTrue(tank.MovementLocked, "she stands still while she sees it");
            AssertWorldStopped("the picture");
            yield return new WaitForSecondsRealtime(PsychicScanner.MinViewSeconds + 0.1f);
            yield return Press(InputManager.GameAction.Scan);
            Assert.IsFalse(scanner.IsViewing, "Scan again closes it");
            Assert.IsFalse(ui.ScanShown);
            Assert.AreEqual(door.scanId, scanner.LastClosedId);
            Assert.IsFalse(tank.MovementLocked);
            AssertWorldRuns("the picture closed");
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
            AssertWorldStopped("the clip");
            float t0 = Time.realtimeSinceStartup;
            while (ui.VideoFramesSeen < 30 && Time.realtimeSinceStartup - t0 < 5f) yield return null;
            Assert.GreaterOrEqual(ui.VideoFramesSeen, 30, "the clip plays in the stopped world (frames seen)");
            while (scanner.IsViewing && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            Assert.IsFalse(scanner.IsViewing, "the view closes at the end of the clip");
            Assert.IsTrue(scanner.LastClosedWasVideo);
            Assert.IsTrue(scanner.LastResult.StartsWith("ended:"), "it ended by itself: " + scanner.LastResult);
            AssertWorldRuns("the clip ended");
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

        [UnityTest]
        public IEnumerator Scan_ReachesOnlyCloseToTheObject()
        {
            Scannable door = TutorialDrive.FindScannable(director.DoorScanId);
            Assert.AreEqual(Scannable.DefaultRange, door.range, 0.01f, "the scan reach (Bruno, 2026-10-22: closer)");
            Assert.LessOrEqual(Scannable.DefaultRange, 1.6f, "no farther than the Interact reach");
            Vector3 away = door.FocusPoint + (director.DoorPoint - door.FocusPoint).normalized * (Scannable.DefaultRange + 0.6f);
            away.y = 0f;
            yield return StandFacing(away, door.FocusPoint);
            Assert.IsNull(scanner.Focus, "too far to scan");
            yield return Press(InputManager.GameAction.Scan);
            Assert.IsFalse(scanner.IsViewing);
        }

        [UnityTest]
        public IEnumerator TheLockedDoor_SaysItIsLocked_AndOpensWithTheKey()
        {
            LockedDoor door = UnityEngine.Object.FindFirstObjectByType<LockedDoor>();
            Assert.IsNotNull(door, "the locked door of the gym");
            Vector3 leafBefore = door.leaf.position;
            yield return StandFacing(director.DoorPoint, door.Point);
            Assert.AreSame(door, interactor.Focus, "the door is in reach");
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(Interactor.State.Message, interactor.Current, "no key: a message, no question");
            Assert.IsTrue(ui.ConfirmTextShown.Contains("locked"), ui.ConfirmTextShown);
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(Interactor.State.Free, interactor.Current);
            Assert.IsFalse(door.IsOpen);

            ItemPickup key = TutorialDrive.FindPickup(director.KeyItemId);
            Assert.IsNotNull(key, "the test key on its stand");
            yield return StandFacing(director.KeyPoint, key.Point);
            int usedBefore = inventory.UsedSlots;
            yield return Press(InputManager.GameAction.Interact);
            yield return Press(InputManager.GameAction.Interact);
            Assert.IsTrue(inventory.Has(director.KeyItemId), "the key taken");
            Assert.AreEqual(usedBefore, inventory.UsedSlots, "a key item takes no slot");

            yield return StandFacing(director.DoorPoint, door.Point);
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(Interactor.State.Confirming, interactor.Current, "with the key it asks");
            Assert.IsTrue(ui.ConfirmTextShown.Contains("test key"), ui.ConfirmTextShown);
            yield return Press(InputManager.GameAction.Interact);
            Assert.IsTrue(door.IsOpen, "the yes opens it");
            yield return new WaitForSecondsRealtime(door.openSeconds + 0.2f);
            Assert.Greater(Vector3.Distance(leafBefore, door.leaf.position), 1f, "the leaf slid aside");
            Assert.IsFalse(door.leaf.GetComponent<Collider>().enabled, "the way is clear");
            Assert.IsTrue(inventory.Has(director.KeyItemId), "the key stays");
        }

        [UnityTest]
        public IEnumerator TheCapsule_AsksFirst_AndTakesASlot()
        {
            ItemPickup capsule = TutorialDrive.FindPickup("nalcon");
            Assert.IsNotNull(capsule, "the capsule is a grabbable");
            Renderer renderer = capsule.GetComponent<Renderer>();
            Assert.AreEqual("Universal Render Pipeline/Unlit", renderer.sharedMaterial.shader.name, "unlit: it stands out of the room light");
            DrugInventory drugs = tank.GetComponent<DrugInventory>();
            int before = drugs.Count(GlorifiedBody.Player.DrugType.Nalcon);
            yield return StandFacing(capsule.Point + Vector3.forward * 0.8f, capsule.Point);
            Assert.AreEqual(before, drugs.Count(GlorifiedBody.Player.DrugType.Nalcon), "walking onto it takes nothing");
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(Interactor.State.Confirming, interactor.Current, "it asks");
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(before + 1, drugs.Count(GlorifiedBody.Player.DrugType.Nalcon), "taken");
            Assert.AreEqual(1, inventory.UsedSlots, "one slot used");
        }

        [UnityTest]
        public IEnumerator TheInventory_StopsTheRoom_AndListsTheItems()
        {
            InventoryUI screen = UnityEngine.Object.FindFirstObjectByType<InventoryUI>();
            Assert.IsNotNull(screen, "the inventory screen in the room");
            ItemPickup log = TutorialDrive.FindPickup(director.LogItemId);
            yield return StandFacing(director.LogPoint, log.Point);
            yield return Press(InputManager.GameAction.Interact);
            yield return Press(InputManager.GameAction.Interact);
            yield return Press(InputManager.GameAction.Interact); // close the page
            tank.GetComponent<DrugInventory>().DebugSet(GlorifiedBody.Player.DrugType.Red, 2);
            PsychicStats barStats = tank.GetComponent<PsychicStats>();

            yield return Press(InputManager.GameAction.Inventory);
            Assert.IsTrue(screen.IsOpen, "the inventory opens");
            Assert.IsTrue(GameManager.Instance.IsInventory, "the game is in the inventory state");
            Assert.AreEqual(0f, Time.timeScale, "the room stops");
            // The SHORT capsule needs a bar to lower (Bruno, 2026-10-30: each drug does one thing). The tutorial empties the bar
            // while the game plays, in an order that changes from run to run, so the test fills it after the room stops.
            barStats.DebugSet(barStats.Nalcon, 60f);
            Assert.AreEqual(2, screen.Entries.Count, "the log and the red drugs");
            Assert.IsTrue(screen.SlotsShown.Contains("2 / 4"), "the log takes no slot, two drugs take two: " + screen.SlotsShown);
            Vector3 lucyBefore = tank.transform.position;
            pad.SetHeld(InputManager.GameAction.Forward, true);
            yield return null;
            yield return null;
            pad.SetHeld(InputManager.GameAction.Forward, false);
            Assert.AreEqual(lucyBefore, tank.transform.position, "she does not walk while it is open");

            Assert.AreEqual(InventoryUI.Consumables, screen.Entries[screen.Selected].Category, "it opens on the first category");
            yield return Press(InputManager.GameAction.TurnRight); // the keys are empty: to the logs
            Assert.AreEqual(InventoryUI.Logs, screen.Entries[screen.Selected].Category, "the next category that holds anything");
            yield return Press(InputManager.GameAction.Interact);
            Assert.IsTrue(screen.ReaderOpen, "Interact reads the log from the inventory");
            yield return Press(InputManager.GameAction.Back);
            Assert.IsFalse(screen.ReaderOpen);
            Assert.IsTrue(screen.IsOpen, "the page closes, the inventory stays");
            yield return Press(InputManager.GameAction.TurnLeft);
            Assert.AreEqual(InventoryUI.Consumables, screen.Entries[screen.Selected].Category, "back to the drugs");
            yield return Press(InputManager.GameAction.Interact); // "Use the red?"
            Assert.IsTrue(screen.AskingDrug, "it asks first (Bruno, 2026-10-22)");
            Assert.AreEqual(2, tank.GetComponent<DrugInventory>().Count(GlorifiedBody.Player.DrugType.Red), "nothing used yet");
            yield return Press(InputManager.GameAction.Interact); // yes
            Assert.AreEqual(1, tank.GetComponent<DrugInventory>().Count(GlorifiedBody.Player.DrugType.Red), "a drug used from the inventory");
            Assert.AreEqual(60f - PsychicStats.MaxShort * PsychicPowers.ShortCapsuleShare, barStats.Short, 0.01f, "the SHORT capsule lowers the bar");

            yield return Press(InputManager.GameAction.Inventory);
            Assert.IsFalse(screen.IsOpen, "the Inventory key closes it");
            Assert.IsTrue(GameManager.Instance.IsPlaying);
            Assert.AreEqual(1f, Time.timeScale, "the room runs again");
        }

        [UnityTest]
        public IEnumerator TheInventory_HasThreeCategories_InOrder_AndUpAndDownScrollEach()
        {
            // Bruno, 2026-11-06: "three categories: consumables, keys, logs, in that order; can be scrolled each cat by going up or down".
            var items = tank.GetComponent<ItemInventory>();
            DrugInventory carried = tank.GetComponent<DrugInventory>();
            carried.DebugSet(GlorifiedBody.Player.DrugType.Red, 1);
            carried.DebugSet(GlorifiedBody.Player.DrugType.NalconFire, 1);
            ItemDefinition Make(string id, ItemKind kind)
            {
                ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.itemId = id;
                item.displayName = id.ToUpperInvariant();
                item.kind = kind;
                return item;
            }
            items.TryAdd(Make("key_a", ItemKind.Key));
            items.TryAdd(Make("plan_a", ItemKind.Map));
            for (int i = 0; i < 8; i++) items.TryAdd(Make("log_" + i, i % 2 == 0 ? ItemKind.Log : ItemKind.AudioLog));
            InventoryUI screen = UnityEngine.Object.FindFirstObjectByType<InventoryUI>();
            yield return Press(InputManager.GameAction.Inventory);
            Assert.IsTrue(screen.IsOpen);

            var categories = new System.Collections.Generic.List<int>();
            foreach (InventoryUI.Entry e in screen.Entries) categories.Add(e.Category);
            var sorted = new System.Collections.Generic.List<int>(categories);
            sorted.Sort();
            CollectionAssert.AreEqual(sorted, categories, "consumables, keys, logs, in that order");
            Assert.AreEqual(GlorifiedBody.Player.DrugType.NalconFire, screen.Entries[0].Drug, "the capsule before the SHORT capsule");
            Assert.AreEqual(2, categories.FindAll(c => c == InventoryUI.Consumables).Count);
            Assert.AreEqual(2, categories.FindAll(c => c == InventoryUI.Keys).Count, "a key and a floor plan");
            Assert.AreEqual(8, categories.FindAll(c => c == InventoryUI.Logs).Count, "the logs and the tapes");

            // Up and down stay inside the category and wrap.
            int first = screen.Selected;
            yield return Press(InputManager.GameAction.Back);
            Assert.AreEqual(first + 1, screen.Selected, "down: the next one");
            Assert.AreEqual(InventoryUI.Consumables, screen.Entries[screen.Selected].Category);
            Assert.IsTrue(screen.IsOpen, "the down key does not close it");
            yield return Press(InputManager.GameAction.Back);
            Assert.AreEqual(first, screen.Selected, "it wraps inside the category");
            yield return Press(InputManager.GameAction.Forward);
            Assert.AreEqual(first + 1, screen.Selected, "up wraps too");

            // A list longer than the screen scrolls.
            yield return Press(InputManager.GameAction.TurnRight);
            Assert.AreEqual(InventoryUI.Keys, screen.Entries[screen.Selected].Category, "right: the next category");
            yield return Press(InputManager.GameAction.TurnRight);
            Assert.AreEqual(InventoryUI.Logs, screen.Entries[screen.Selected].Category);
            Assert.AreEqual(0, screen.ScrollOf(InventoryUI.Logs));
            int row = 1; // the same row as before
            for (; row < InventoryUI.VisibleRows - 1; row++) yield return Press(InputManager.GameAction.Back);
            Assert.AreEqual(0, screen.ScrollOf(InventoryUI.Logs), "the sixth row still shows");
            yield return Press(InputManager.GameAction.Back);
            Assert.AreEqual(1, screen.ScrollOf(InventoryUI.Logs), "the seventh scrolls the list");
            yield return Press(InputManager.GameAction.Back);
            Assert.AreEqual(2, screen.ScrollOf(InventoryUI.Logs), "the last one");
            yield return Press(InputManager.GameAction.Back);
            Assert.AreEqual(0, screen.ScrollOf(InventoryUI.Logs), "it wraps to the top");

            yield return Press(InputManager.GameAction.Inventory);
            Assert.IsFalse(screen.IsOpen, "the inventory key closes it");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private int Scans0 => 0;

        /// <summary>Bruno, 2026-10-26: reading a log or watching a scan stops the world; only the close keys pass.</summary>
        private static void AssertWorldStopped(string what)
        {
            Assert.AreEqual(0f, Time.timeScale, 0.001f, what + " stops the world");
            Assert.IsFalse(GameManager.Instance.IsPlaying, what + ": the game state is not Playing");
            Assert.IsFalse(GameManager.Instance.AllowsAction(InputManager.GameAction.Charge), what + ": no power key passes");
            Assert.IsTrue(GameManager.Instance.AllowsAction(InputManager.GameAction.Back), what + ": the close key passes");
        }

        private static void AssertWorldRuns(string what)
        {
            Assert.AreEqual(1f, Time.timeScale, 0.001f, what + ": the world runs again");
            Assert.IsTrue(GameManager.Instance.IsPlaying, what + ": Playing again");
        }

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
