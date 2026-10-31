#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Automation;
using GlorifiedBody.Core;
using GlorifiedBody.Interaction;
using GlorifiedBody.Map;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The map (Bruno, 2026-10-31: "the map will start hidden, and being revealed room-by-room unless the player picks up
    /// a floor plan item; key places (i.e locked door, items, logs) are to marked on the map as well"), in the travel
    /// rooms (TEST FLOOR 1: TRAVEL A, TRAVEL B, KIT TEST), the ward and the lab (TEST FLOOR 2). Needs the atlas:
    /// MapAtlasSetup.BuildAtlas. Batch: tools/unity.sh test playmode MapTests.
    /// </summary>
    public class MapTests
    {
        private const string A = "TravelTest_A";
        private const string B = "TravelTest_B";
        private const string Kit = "KitTest";
        private const string Ward = "WardTest";
        private const string Lab = "LabTest";
        private const string Floor1 = "test_1"; // Floor1 (editor code)
        private const string Floor2 = "test_2";
        private const string LabDoor = "Door_Store"; // LabDoor

        [DefaultExecutionOrder(-1000)]
        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad pad;
            private void Update() => pad?.Commit();
        }

        private VirtualPad pad;
        private PadDriver driver;
        private GameObject lucy;

        private static MapScreen Screen => MapScreen.Instance;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_map_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            pad = new VirtualPad();
            driver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            UnityEngine.Object.DontDestroyOnLoad(driver.gameObject);
            driver.pad = pad;
            GameInput.SetBotSource(pad);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Screen != null && Screen.IsOpen) Screen.Close();
            GameInput.ClearBotSource(pad);
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            SaveSystem.Folder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AtTheStart_OnlyHerRoomShows_WithItsKeyPlaces_AndHerPlace()
        {
            Assert.IsNotNull(MapAtlas.Instance, "the atlas (MapAtlasSetup.BuildAtlas)");
            yield return Load(A);
            yield return Press(InputManager.GameAction.Map);
            Assert.IsTrue(Screen.IsOpen, "the Map key opens the map");
            Assert.IsTrue(GameManager.Instance.IsMap);
            Assert.AreEqual(0f, Time.timeScale, "the room stops");
            Assert.AreEqual(Floor1, Screen.ShownFloor, "her floor");
            CollectionAssert.AreEquivalent(new[] { A }, Screen.ShownRooms.ToArray(), "only the room she is in");
            Assert.IsTrue(HasMark(MapMarkKind.Log, "Log_Travel"), "the log");
            Assert.IsTrue(HasMark(MapMarkKind.Item, "Drug_Nalcon"), "the item");
            Vector3 her = lucy.transform.position;
            Assert.IsTrue(Screen.HerPlace.HasValue, "her place");
            Assert.Less(Vector2.Distance(new Vector2(her.x, her.z), Screen.HerPlace.Value), 0.01f, "her place on the plan (TRAVEL A lies at the origin)");
            yield return Press(InputManager.GameAction.Map);
            Assert.IsFalse(Screen.IsOpen, "the Map key closes it");
            Assert.IsTrue(GameManager.Instance.IsPlaying);
            Assert.AreEqual(1f, Time.timeScale, "the room goes on");
        }

        [UnityTest]
        public IEnumerator ARoomShows_WhenSheEntersIt_AndStaysOnTheMap()
        {
            yield return Load(A);
            yield return Cross("Exit_ToB", B);
            yield return Press(InputManager.GameAction.Map);
            CollectionAssert.AreEquivalent(new[] { A, B }, Screen.ShownRooms.ToArray(), "the room she left stays, the room she is in shows");
            Assert.IsFalse(Screen.ShownRooms.Contains(Kit), "a room she never saw stays hidden");
            Assert.IsTrue(HasMark(MapMarkKind.Save, "Save_TravelB"), "the save machine");
            Assert.IsTrue(HasMark(MapMarkKind.Item, "Plan_TestFloor1"), "the floor plan lies there");
            Vector3 her = lucy.transform.position;
            Assert.Less(Vector2.Distance(new Vector2(her.x + 13.5f, her.z), Screen.HerPlace.Value), 0.01f, "her place in TRAVEL B (13.5 m east)");
            yield return Press(InputManager.GameAction.Back);
            Assert.IsFalse(Screen.IsOpen, "Back closes it too");
        }

        [UnityTest]
        public IEnumerator TheFloorPlan_ShowsTheWholeFloor()
        {
            yield return Load(B);
            yield return Press(InputManager.GameAction.Map);
            CollectionAssert.AreEquivalent(new[] { B }, Screen.ShownRooms.ToArray());
            yield return Press(InputManager.GameAction.Map);
            ItemPickup plan = GameObject.Find("Plan_TestFloor1").GetComponent<ItemPickup>();
            Assert.AreEqual(ItemKind.Map, plan.item.kind);
            Assert.AreEqual("taken:" + plan.item.itemId, plan.Confirm(lucy.GetComponent<Interactor>()));
            Assert.AreEqual(0, lucy.GetComponent<ItemInventory>().UsedSlots, "a floor plan takes no slot");
            yield return null;
            yield return Press(InputManager.GameAction.Map);
            CollectionAssert.AreEquivalent(new[] { A, B, Kit }, Screen.ShownRooms.ToArray(), "the whole floor");
            Assert.IsFalse(HasMark(MapMarkKind.Item, "Plan_TestFloor1"), "the plan she took is off the map");
            Assert.IsTrue(HasMark(MapMarkKind.Log, "Log_Travel"), "the key places of a room she never saw");
            StringAssert.Contains("FLOOR PLAN", Screen.NoteShown);
            Assert.AreEqual(1, Screen.Floors.Count, "no room of the other floor is known");
        }

        [UnityTest]
        public IEnumerator ATakenItem_LeavesTheMap_HereAndInTheRoomSheLeft()
        {
            yield return Load(A);
            ItemPickup log = GameObject.Find("Log_Travel").GetComponent<ItemPickup>();
            Assert.AreEqual("taken:test_log", log.Confirm(lucy.GetComponent<Interactor>()));
            yield return null;
            if (lucy.GetComponent<Interactor>().Current != Interactor.State.Free) yield return Press(InputManager.GameAction.Interact); // the page closes
            yield return WaitUntil(() => GameManager.Instance.IsPlaying, 3f);
            yield return Press(InputManager.GameAction.Map);
            Assert.IsTrue(Screen.IsOpen);
            Assert.IsFalse(HasMark(MapMarkKind.Log, "Log_Travel"), "taken here");
            Assert.IsTrue(HasMark(MapMarkKind.Item, "Drug_Nalcon"), "the other item stays");
            yield return Press(InputManager.GameAction.Map);
            yield return Cross("Exit_ToB", B);
            yield return Press(InputManager.GameAction.Map);
            Assert.IsTrue(Screen.ShownRooms.Contains(A));
            Assert.IsFalse(HasMark(MapMarkKind.Log, "Log_Travel"), "taken in the room she left");
            Assert.IsTrue(HasMark(MapMarkKind.Item, "Drug_Nalcon"), "and the pill she left there");
        }

        [UnityTest]
        public IEnumerator ALockedDoor_IsRed_AndGreenOnceOpen()
        {
            yield return Load(Lab);
            yield return Press(InputManager.GameAction.Map);
            Assert.AreEqual(Floor2, Screen.ShownFloor);
            ShownMarkOf(MapMarkKind.LockedDoor, LabDoor, out MapScreen.ShownMark door);
            Assert.IsFalse(door.Open, "locked");
            yield return Press(InputManager.GameAction.Map);
            GameObject.Find(LabDoor).GetComponent<LockedDoor>().Open(instant: true);
            yield return Press(InputManager.GameAction.Map);
            ShownMarkOf(MapMarkKind.LockedDoor, LabDoor, out door);
            Assert.IsTrue(door.Open, "open");
            Assert.AreEqual(MapScreen.OpenColour, MapScreen.ColourOf(MapMarkKind.LockedDoor, true));
        }

        [UnityTest]
        public IEnumerator TwoFloors_AandDChangeTheFloor()
        {
            yield return Load(Ward);
            yield return Cross("Exit_WardWest", B);
            yield return Press(InputManager.GameAction.Map);
            Assert.AreEqual(Floor1, Screen.ShownFloor, "it opens on her floor");
            Assert.AreEqual(2, Screen.Floors.Count, "the ward's floor is known too");
            Assert.IsTrue(Screen.HerPlace.HasValue);
            yield return Press(InputManager.GameAction.TurnRight);
            Assert.AreEqual(Floor2, Screen.ShownFloor, "D: the next floor");
            CollectionAssert.AreEquivalent(new[] { Ward }, Screen.ShownRooms.ToArray(), "the ward, not the lab she never saw");
            Assert.IsFalse(Screen.HerPlace.HasValue, "she is not on that floor");
            yield return Press(InputManager.GameAction.TurnLeft);
            Assert.AreEqual(Floor1, Screen.ShownFloor, "A: back");
        }

        [UnityTest]
        public IEnumerator ASave_KeepsTheMap_AndTheMenuForgetsIt()
        {
            yield return Load(A);
            yield return Cross("Exit_ToB", B);
            SavePoint point = UnityEngine.Object.FindFirstObjectByType<SavePoint>();
            Assert.IsTrue(SaveSystem.SaveManual(1, point.SpotId));
            GameManager.Instance.LoadMainMenu();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(B);
            yield return Room(B);
            yield return Press(InputManager.GameAction.Map);
            CollectionAssert.AreEquivalent(new[] { B }, Screen.ShownRooms.ToArray(), "a new game forgets the map");
            yield return Press(InputManager.GameAction.Map);
            int applied = SaveSystem.Applies;
            Assert.IsTrue(SaveSystem.Load(1));
            yield return WaitUntil(() => SaveSystem.Applies > applied, 10f);
            yield return Room(B);
            yield return Press(InputManager.GameAction.Map);
            CollectionAssert.AreEquivalent(new[] { A, B }, Screen.ShownRooms.ToArray(), "the save keeps the rooms she saw");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private bool HasMark(MapMarkKind kind, string path) => Screen.ShownMarks.Any(m => m.Kind == kind && m.Path == path);

        private static void ShownMarkOf(MapMarkKind kind, string path, out MapScreen.ShownMark mark)
        {
            mark = Screen.ShownMarks.FirstOrDefault(m => m.Kind == kind && m.Path == path);
            Assert.AreEqual(path, mark.Path, $"a {kind} mark at {path}");
        }

        private IEnumerator Load(string scene)
        {
            GameManager.Instance.LoadLevel(scene);
            yield return Room(scene);
        }

        private IEnumerator Room(string scene)
        {
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == scene && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            Assert.AreEqual(scene, SceneManager.GetActiveScene().name);
            lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
        }

        private IEnumerator Cross(string exitName, string scene)
        {
            int arrivals = SaveSystem.Arrivals;
            Assert.IsTrue(GameObject.Find(exitName).GetComponent<RoomExit>().Use(), exitName + " opens");
            yield return WaitUntil(() => SaveSystem.Arrivals > arrivals, 10f);
            yield return Room(scene);
            yield return WaitUntil(() => !WorldState.Travelling, 3f);
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
