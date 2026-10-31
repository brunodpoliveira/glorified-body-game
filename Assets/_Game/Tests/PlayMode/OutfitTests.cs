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
using GlorifiedBody.Characters;
using GlorifiedBody.Core;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The outfit change (2026-10-31, the design sweep's rank 7; design §4.4), in the OUTFIT TEST room
    /// (OutfitTestSetup.BuildOutfitTest): the locker puts the guard uniform on her (the model, the HP, the slots, no
    /// recharge, the HUD), takes it off again (the damage stays, the pills over 4 fall), the Crown grab tears it off,
    /// a save keeps it. Batch: tools/unity.sh test playmode OutfitTests.
    /// </summary>
    public class OutfitTests
    {
        private const string Room = "OutfitTest";
        private const string Locker = "Locker_Uniform";
        private const string GrabFlag = "outfit_test_grab";

        [DefaultExecutionOrder(-1000)]
        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad pad;
            private void Update() => pad?.Commit();
        }

        private VirtualPad pad;
        private PadDriver driver;
        private GameObject lucy;
        private PsychicStats stats;
        private Health health;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_outfit_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(Room);
            yield return InRoom();
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
            SaveSystem.Folder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheLocker_PutsOnTheUniform_TheModelTheStatsAndTheHud()
        {
            Assert.AreEqual("outfit_test", GlorifiedBody.Audio.MusicDirector.Instance.RoomKey, "the room's music key (RoomMusic loads)");
            Assert.AreEqual(OutfitChange.NudeId, stats.Outfit.outfitId, "she starts bare");
            Assert.AreEqual("lucy_bare", LucyModel.Current(lucy));
            Assert.AreEqual(4, lucy.GetComponent<ItemInventory>().Capacity);
            OutfitLocker locker = GameObject.Find(Locker).GetComponent<OutfitLocker>();
            Assert.IsTrue(locker.shown.activeInHierarchy, "the uniform hangs in the locker");
            health.SetHealth(60f); // she took 40 of 100

            yield return AtLocker();
            Assert.AreSame(locker, lucy.GetComponent<Interactor>().FindFocus(), "the locker is in reach");
            StringAssert.Contains("put on the guard uniform", locker.Hint);
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual("Put on the guard uniform?", lucy.GetComponent<Interactor>().Text);
            yield return Press(InputManager.GameAction.Interact);

            Assert.AreEqual(OutfitChange.GuardId, stats.Outfit.outfitId, "she wears the uniform");
            Assert.AreEqual("lucy_guard", LucyModel.Current(lucy), "her model is the guard's");
            Assert.AreEqual(1, lucy.transform.Cast<Transform>().Count(t => t.name == LucyModel.ModelName), "one model, the old one is gone");
            Assert.AreEqual(LayerMask.NameToLayer("Player"), lucy.transform.Find(LucyModel.ModelName).GetComponentInChildren<SkinnedMeshRenderer>().gameObject.layer, "the new model keeps the stencil layer");
            Assert.AreEqual(150f, health.MaxHealth, "the uniform's HP");
            Assert.AreEqual(110f, health.CurrentHealth, 0.01f, "she keeps the 40 she took: the armour adds, no heal");
            Assert.AreEqual(8, lucy.GetComponent<ItemInventory>().Capacity, "the pockets");
            Assert.AreEqual(0f, stats.Outfit.nalconRegenPerSecond, "the uniform covers the vents: no MK recharge");
            Assert.IsFalse(locker.shown.activeInHierarchy, "the locker is empty");
            yield return null;
            PlayerHud hud = UnityEngine.Object.FindFirstObjectByType<PlayerHud>();
            Assert.AreEqual("SECURITY GUARD", hud.OutfitShown, "the HUD names the outfit");

            // The new body walks and plays its clips.
            yield return new WaitForSeconds(0.3f);
            LucyAnimator animator = lucy.GetComponent<LucyAnimator>();
            Assert.IsNotNull(animator.Animator);
            Assert.IsTrue(animator.Animator.transform.IsChildOf(lucy.transform.Find(LucyModel.ModelName)), "the driver found the new Animator");
            Vector3 from = lucy.transform.position;
            lucy.GetComponent<TankController>().Teleport(new Vector3(-1f, 0f, 2.5f), 180f);
            yield return null;
            from = lucy.transform.position;
            pad.SetHeld(InputManager.GameAction.Forward, true);
            yield return new WaitForSeconds(0.8f);
            pad.SetHeld(InputManager.GameAction.Forward, false);
            Assert.Greater(Vector3.Distance(from, lucy.transform.position), 0.4f, "she walks in the uniform");
            Assert.Greater(animator.Speed, -1f);
        }

        [UnityTest]
        public IEnumerator TakingItOff_WithTooMuch_WarnsFirst_TheDropKeyMakesRoom_AndTheDamageStays()
        {
            OutfitChange.Change(lucy, OutfitChange.Load(OutfitChange.GuardId));
            DrugInventory drugs = lucy.GetComponent<DrugInventory>();
            drugs.DebugSet(DrugType.Red, 6);
            Assert.AreEqual(6, lucy.GetComponent<ItemInventory>().UsedSlots, "six fit in the pockets");
            health.SetHealth(120f);
            Interactor hands = lucy.GetComponent<Interactor>();

            // Bruno, 2026-10-31: "give the player a warning and a chance to drop items".
            yield return AtLocker();
            OutfitLocker locker = GameObject.Find(Locker).GetComponent<OutfitLocker>();
            yield return Press(InputManager.GameAction.Interact);
            StringAssert.Contains("Drop 2 first", hands.Text, "the warning: how many to drop, and how");
            Assert.AreEqual(Interactor.State.Message, hands.Current, "a message, no question");
            Assert.AreEqual(OutfitChange.GuardId, stats.Outfit.outfitId, "she keeps the uniform");
            Assert.AreEqual(6, drugs.Count(DrugType.Red), "nothing fell");
            yield return Press(InputManager.GameAction.Interact); // the message goes

            // The inventory: the drop key asks, Interact drops one at her feet. Twice.
            InventoryUI inventory = UnityEngine.Object.FindFirstObjectByType<InventoryUI>();
            yield return Press(InputManager.GameAction.Inventory);
            Assert.IsTrue(inventory.IsOpen);
            int red = -1;
            for (int i = 0; i < inventory.Entries.Count; i++) if (inventory.Entries[i].Drug == DrugType.Red) red = i;
            inventory.SelectIndex(red);
            yield return Press(InputManager.GameAction.Drop);
            Assert.IsTrue(inventory.AskingDrop, "the drop key asks first");
            Assert.AreEqual(6, drugs.Count(DrugType.Red));
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(5, drugs.Count(DrugType.Red), "one on the floor");
            yield return Press(InputManager.GameAction.Drop); // each drop asks
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(4, drugs.Count(DrugType.Red), "two on the floor");
            yield return Press(InputManager.GameAction.Inventory);
            Assert.AreEqual(2, FloorPills(), "two red pills lie at her feet");

            yield return AtLocker();
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual("Take off the uniform and leave it here?", hands.Text, "now it asks");
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(OutfitChange.NudeId, stats.Outfit.outfitId, "bare again");
            Assert.AreEqual("lucy_bare", LucyModel.Current(lucy));
            Assert.AreEqual(100f, health.MaxHealth);
            Assert.AreEqual(70f, health.CurrentHealth, 0.01f, "the armour goes with the uniform");
            Assert.AreEqual(4, drugs.Count(DrugType.Red));
            Assert.AreEqual(2, FloorPills(), "nothing more fell");
            Assert.IsTrue(locker.shown.activeInHierarchy, "the uniform hangs in the locker again");

            // Low HP: taking the uniform off never kills her.
            OutfitChange.Change(lucy, OutfitChange.Load(OutfitChange.GuardId));
            health.SetHealth(20f);
            OutfitChange.Change(lucy, OutfitChange.Load(OutfitChange.NudeId));
            Assert.AreEqual(1f, health.CurrentHealth, 0.01f, "at least 1 HP");
            Assert.IsFalse(health.IsDead);
        }

        [UnityTest]
        public IEnumerator ASave_KeepsTheItemsOnTheFloor_AndTheShieldScars()
        {
            // Bruno, 2026-10-31: "fix both" (the seam sweep: a dropped item and a scar were gone after a load).
            ItemDefinition pill = ItemDefinition.Find("red");
            FloorItem.Place(pill, new Vector3(2f, 0.06f, -1f), 30f);
            GlorifiedBody.Combat.FloorShield.ScarAt(new Vector3(-2f, 0.004f, -2f), Quaternion.Euler(0f, 45f, 0f));
            yield return null;
            SavePoint point = UnityEngine.Object.FindFirstObjectByType<SavePoint>();
            Assert.IsTrue(SaveSystem.SaveManual(1, point.SpotId));
            GameManager.Instance.LoadMainMenu();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance.IsInMenu, 10f);
            int applied = SaveSystem.Applies;
            Assert.IsTrue(SaveSystem.Load(1));
            yield return WaitUntil(() => SaveSystem.Applies > applied, 10f);
            yield return InRoom();
            FloorItem[] items = UnityEngine.Object.FindObjectsByType<FloorItem>(FindObjectsSortMode.None);
            Assert.AreEqual(1, items.Length, "the pill on the floor is back");
            Assert.AreEqual("red", items[0].itemId);
            Assert.Less(Vector3.Distance(items[0].transform.position, new Vector3(2f, 0.06f, -1f)), 0.01f, "where it lay");
            GlorifiedBody.Combat.FloorScar[] scars = UnityEngine.Object.FindObjectsByType<GlorifiedBody.Combat.FloorScar>(FindObjectsSortMode.None);
            Assert.AreEqual(1, scars.Length, "the scar is back");
            Assert.AreEqual(45f, scars[0].transform.eulerAngles.y, 0.5f);
            Assert.AreEqual("taken:red", items[0].GetComponent<ItemPickup>().Confirm(lucy.GetComponent<Interactor>()), "she can take it");
        }

        private static int FloorPills() => UnityEngine.Object.FindObjectsByType<FloorItem>(FindObjectsSortMode.None).Count(f => f.itemId == "red");

        [UnityTest]
        public IEnumerator TheGrab_TearsTheUniformOff_Once()
        {
            OutfitChange.Change(lucy, OutfitChange.Load(OutfitChange.GuardId));
            lucy.GetComponent<DrugInventory>().DebugSet(DrugType.Red, 5);
            lucy.GetComponent<TankController>().Teleport(new Vector3(7f, 0f, -2f), 90f);
            yield return WaitUntil(() => StoryFlags.Has(GrabFlag), 10f);
            Assert.IsTrue(StoryFlags.Has(GrabFlag), "the grab ran");
            Assert.AreEqual(OutfitChange.NudeId, stats.Outfit.outfitId, "the grab tears the uniform off");
            Assert.AreEqual("lucy_bare", LucyModel.Current(lucy));
            Assert.AreEqual(4, lucy.GetComponent<DrugInventory>().Count(DrugType.Red), "the pills over the bare limit fall");
            Assert.AreEqual(1, FloorPills(), "one pill on the floor");

            OutfitChange.Change(lucy, OutfitChange.Load(OutfitChange.GuardId));
            lucy.GetComponent<TankController>().Teleport(new Vector3(0f, 0f, -2f), 90f);
            yield return new WaitForSeconds(0.3f);
            lucy.GetComponent<TankController>().Teleport(new Vector3(7f, 0f, -2f), 90f);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(OutfitChange.GuardId, stats.Outfit.outfitId, "once a game");
        }

        [UnityTest]
        public IEnumerator ASave_KeepsTheUniform_AndTheLoadPutsItsModelBack()
        {
            OutfitChange.Change(lucy, OutfitChange.Load(OutfitChange.GuardId));
            health.SetHealth(130f);
            SavePoint point = UnityEngine.Object.FindFirstObjectByType<SavePoint>();
            Assert.IsTrue(SaveSystem.SaveManual(1, point.SpotId));
            GameManager.Instance.LoadMainMenu();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance.IsInMenu, 10f);
            int applied = SaveSystem.Applies;
            Assert.IsTrue(SaveSystem.Load(1));
            yield return WaitUntil(() => SaveSystem.Applies > applied, 10f);
            yield return InRoom();
            Assert.AreEqual(OutfitChange.GuardId, stats.Outfit.outfitId, "the save keeps the outfit");
            Assert.AreEqual("lucy_guard", LucyModel.Current(lucy), "and the load puts its model on her");
            Assert.AreEqual(150f, health.MaxHealth);
            Assert.AreEqual(130f, health.CurrentHealth, 0.01f, "the saved HP, not a full bar");
            Assert.IsFalse(GameObject.Find(Locker).GetComponent<OutfitLocker>().shown.activeInHierarchy, "the locker is empty");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private IEnumerator InRoom()
        {
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Room && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            Assert.AreEqual(Room, SceneManager.GetActiveScene().name, "the room is in the build (OutfitTestSetup.BuildOutfitTest)");
            lucy = GameObject.FindGameObjectWithTag("Player");
            stats = lucy.GetComponent<PsychicStats>();
            health = lucy.GetComponent<Health>();
        }

        private IEnumerator AtLocker()
        {
            lucy.GetComponent<TankController>().Teleport(new Vector3(-1f, 0f, 5.7f), 0f);
            yield return new WaitForSeconds(0.2f);
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
