#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Core;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// Saves and checkpoints (Bruno, 2026-10-25: saves "only in save rooms", checkpoints "only at specific save
    /// points", "3 manual save slots, one for the auto checkpoints"). In the training room, which has one save machine
    /// and one checkpoint: a save then a load gives back her bars, her items, her place and the room (the dead, the
    /// taken items); the checkpoint saves when she walks in and a death restarts there; a new game ignores an old
    /// checkpoint; the slot menu stops the game and asks before it overwrites; the main menu loads a slot. The files
    /// go to a test folder. Batch: tools/unity.sh test playmode SaveTests.
    /// </summary>
    public class SaveTests
    {
        private GameObject lucy;
        private Health health;
        private PsychicStats stats;
        private ItemInventory items;
        private DrugInventory drugs;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_save_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadGameplay();
            yield return Room();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (SaveMenuUI.Instance != null && SaveMenuUI.Instance.IsOpen) SaveMenuUI.Instance.Close();
            SaveSystem.Folder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ASave_ThenALoad_GivesBackHerBarsHerItemsHerPlace_AndTheRoom()
        {
            health.SetHealth(37f);
            stats.RegenPaused = true;
            stats.DebugSet(20f, 50f);
            items.TryAdd(ItemDefinition.Find("test_key"));
            drugs.DebugSet(DrugType.Red, 2);
            Kill("Dummy_B");
            UnityEngine.Object.Destroy(GameObject.Find("Drug_Nalcon")); // taken, as ItemPickup.Confirm does
            yield return null;
            SavePoint point = UnityEngine.Object.FindFirstObjectByType<SavePoint>();
            Assert.IsNotNull(point, "the training room has a save machine");
            Assert.IsTrue(SaveSystem.SaveManual(2, point.SpotId), "saved in slot 2");
            Assert.IsTrue(SaveSystem.Exists(2), "the slot file");
            Assert.IsTrue(SaveSystem.Exists(SaveSystem.CheckpointSlot), "a manual save is the checkpoint too");

            // Everything changes after the save.
            health.SetHealth(health.MaxHealth);
            stats.DebugSet(100f, 0f);
            items.Clear();
            drugs.DebugSet(DrugType.Red, 0);
            lucy.GetComponent<TankController>().Teleport(new Vector3(-2f, 0f, 2f), 0f);

            int applied = SaveSystem.Applies;
            Assert.IsTrue(SaveSystem.Load(2), "the load starts");
            yield return WaitUntil(() => SaveSystem.Applies > applied, 10f);
            yield return Room();
            stats.RegenPaused = true;
            point = UnityEngine.Object.FindFirstObjectByType<SavePoint>();
            Debug.Log($"[SaveTests] after the load: hp {health.CurrentHealth}, mk {stats.Nalcon:0.0}, short {stats.Short:0.0}, key {items.Has("test_key")}, red {drugs.Count(DrugType.Red)}, at {lucy.transform.position}");
            Assert.AreEqual(37f, health.CurrentHealth, 0.5f, "her HP (the outfit heals a little each frame)");
            Assert.AreEqual(20f, stats.Nalcon, 0.5f, "her MK");
            Assert.AreEqual(50f, stats.Short, 0.5f, "her SHORT");
            Assert.IsTrue(items.Has("test_key"), "her key item");
            Assert.AreEqual(2, drugs.Count(DrugType.Red), "her pills");
            Assert.Less(Flat(lucy.transform.position, point.SpawnPose.position), 0.3f, "she starts at the machine");
            // The training dummy vanishes at its death (Health.destroyOnDeath), so it has no body to keep: it stays
            // gone. The bodies that stay on the floor are in SaveGoreTests.
            GameObject dummy = ScenePaths.Find(SceneManager.GetActiveScene(), "Dummy_B");
            Assert.IsTrue(dummy == null || !dummy.activeInHierarchy, "the dead dummy stays dead");
            GameObject pill = GameObject.Find("Drug_Nalcon");
            Assert.IsTrue(pill == null || !pill.activeInHierarchy, "the taken pill stays taken");
            Assert.IsNotNull(GameObject.Find("Drug_Red"), "the other pills are still there");
        }

        [UnityTest]
        public IEnumerator ACheckpoint_SavesWhenSheWalksIn_AndADeathRestartsThere()
        {
            CheckpointPoint checkpoint = UnityEngine.Object.FindFirstObjectByType<CheckpointPoint>();
            Assert.IsNotNull(checkpoint, "the training room has a checkpoint");
            Assert.IsFalse(SaveSystem.HasSessionCheckpoint, "no checkpoint at the start");
            health.SetHealth(55f);
            lucy.GetComponent<TankController>().Teleport(checkpoint.transform.position, 0f);
            yield return WaitUntil(() => checkpoint.Reached, 2f);
            Assert.IsTrue(checkpoint.Reached, "she walked in");
            Assert.IsTrue(SaveSystem.Exists(SaveSystem.CheckpointSlot), "the checkpoint file");
            Assert.IsTrue(SaveSystem.HasSessionCheckpoint);
            yield return null;
            Assert.AreEqual("CHECKPOINT", SaveMenuUI.Instance.ToastShown, "the screen says so");

            lucy.GetComponent<TankController>().Teleport(new Vector3(2f, 0f, -2f), 0f);
            health.SetInvulnerable(false);
            health.SetSecondWindEnabled(false);
            health.TakeDamage(health.MaxHealth * 10f);
            yield return WaitUntil(() => GameManager.Instance.IsGameOver, 3f);
            Assert.IsTrue(GameManager.Instance.IsGameOver, "she died");
            int applied = SaveSystem.Applies;
            Assert.IsTrue(GameManager.Instance.RestartFromLatestCheckpoint(), "the retry goes to the checkpoint");
            yield return WaitUntil(() => SaveSystem.Applies > applied, 10f);
            yield return Room();
            checkpoint = UnityEngine.Object.FindFirstObjectByType<CheckpointPoint>();
            Debug.Log($"[SaveTests] after the retry: hp {health.CurrentHealth}, at {lucy.transform.position}, checkpoint {checkpoint.transform.position}");
            Assert.AreEqual(55f, health.CurrentHealth, 0.5f, "her HP at the checkpoint");
            Assert.Less(Flat(lucy.transform.position, checkpoint.transform.position), 0.3f, "she starts at the checkpoint");
            Assert.IsTrue(checkpoint.Reached, "it does not save again at once");
        }

        [UnityTest]
        public IEnumerator ANewGame_IgnoresTheCheckpointOfAnOldOne()
        {
            Assert.IsTrue(SaveSystem.SaveCheckpoint("Checkpoint_Training"));
            Assert.IsTrue(SaveSystem.HasSessionCheckpoint);
            GameManager.Instance.LoadMainMenu();
            yield return WaitUntil(() => GameManager.Instance.IsInMenu, 10f);
            Assert.IsFalse(SaveSystem.HasSessionCheckpoint, "the menu ends that game");
            GameManager.Instance.LoadGameplay();
            yield return Room();
            Assert.IsFalse(SaveSystem.RestartFromCheckpoint(), "a death in the new game restarts the room, not the old checkpoint");
            Assert.IsTrue(SaveSystem.Exists(SaveSystem.CheckpointSlot), "the file stays for CONTINUE");
        }

        [UnityTest]
        public IEnumerator TheSlotMenu_StopsTheGame_ShowsThreeSlots_AndAsksBeforeItOverwrites()
        {
            SavePoint point = UnityEngine.Object.FindFirstObjectByType<SavePoint>();
            Assert.AreEqual("save_menu:" + point.SpotId, point.Confirm(lucy.GetComponent<Interactor>()), "the yes opens the menu");
            yield return null;
            SaveMenuUI menu = SaveMenuUI.Instance;
            Assert.IsTrue(menu.IsOpen && menu.Current == SaveMenuUI.Mode.Save, "the save menu");
            Assert.IsTrue(GameManager.Instance.IsInventory, "the game stops");
            Assert.AreEqual(0f, Time.timeScale, "time stands still");
            Assert.AreEqual(3, menu.Slots.Count, "three slots");
            StringAssert.Contains("EMPTY", menu.RowText(0));
            menu.Choose(0);
            Assert.IsFalse(menu.IsOpen, "a save closes it");
            Assert.IsTrue(GameManager.Instance.IsPlaying, "the game goes on");
            Assert.IsTrue(SaveSystem.Exists(1), "slot 1");
            yield return null;
            Assert.AreEqual("SAVED IN SLOT 1", menu.ToastShown);

            SaveMenuUI.OpenSave(point);
            yield return null;
            StringAssert.Contains("TrainingRoom", menu.RowText(0), "the used slot names its room");
            menu.Choose(0);
            Assert.IsTrue(menu.AskingOverwrite, "a used slot asks first");
            Assert.IsTrue(menu.IsOpen);
            menu.Choose(0);
            Assert.IsFalse(menu.IsOpen, "the second yes overwrites");
        }

        [UnityTest]
        public IEnumerator TheMainMenu_LoadsASlot_AndListsTheCheckpoint()
        {
            health.SetHealth(44f);
            SavePoint point = UnityEngine.Object.FindFirstObjectByType<SavePoint>();
            Assert.IsTrue(SaveSystem.SaveManual(3, point.SpotId));
            GameManager.Instance.LoadMainMenu();
            yield return WaitUntil(() => GameManager.Instance.IsInMenu, 10f);
            Assert.AreEqual(3, SaveSystem.Newest() == SaveSystem.CheckpointSlot ? 3 : SaveSystem.Newest(), "CONTINUE takes the newest");
            SaveMenuUI.OpenLoad();
            yield return null;
            SaveMenuUI menu = SaveMenuUI.Instance;
            Assert.AreEqual(4, menu.Slots.Count, "three slots and the checkpoint");
            Assert.Contains(SaveSystem.CheckpointSlot, (System.Collections.ICollection)menu.Slots);
            int applied = SaveSystem.Applies;
            menu.Choose(2); // slot 3
            yield return WaitUntil(() => SaveSystem.Applies > applied, 10f);
            yield return Room();
            Assert.AreEqual("TrainingRoom", SceneManager.GetActiveScene().name);
            Assert.AreEqual(44f, health.CurrentHealth, 0.5f, "the slot's HP");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private IEnumerator Room()
        {
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "TrainingRoom" && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            lucy = GameObject.FindGameObjectWithTag("Player");
            health = lucy.GetComponentInChildren<Health>();
            stats = lucy.GetComponentInChildren<PsychicStats>();
            items = lucy.GetComponentInChildren<ItemInventory>();
            drugs = lucy.GetComponentInChildren<DrugInventory>();
            health.SetInvulnerable(true); // the shooting dummy must not change her HP between the steps
        }

        private static void Kill(string name)
        {
            GameObject target = GameObject.Find(name);
            Assert.IsNotNull(target, name);
            Health h = target.GetComponent<Health>();
            h.TakeDamage(h.MaxHealth * 10f);
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
