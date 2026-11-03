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
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Destruction;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Rooms;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The fire doors (2026-11-03, Bruno: "only certain doors that can open and close in selected chokepoints on the map
    /// ... a lever that closes+ locks/jams the door shut (justified by them being firebreaks, that's why guards can't open
    /// them even if they engage the lever on the other side)"; "breachable by the soldier and power armor enemies ... not
    /// by scientists and guards, they can bang on the doors though"). In the DOOR TEST room (DoorTestSetup.BuildDoorTest):
    /// every fire door starts open with a lever on each side; her lever jams it and opens it again; a body in the doorway
    /// refuses the jam; an open fire door lets a soldier through; soldiers kick a jammed door down; the power armour breaks
    /// it in two blows; a guard and a scientist only bang; nothing breaks a card door; a jammed door drops her lock; her
    /// powers break a jammed door; the save keeps a jammed door.
    /// Batch: tools/unity.sh test playmode DoorTests.
    /// </summary>
    public class DoorTests
    {
        private const string Scene = "DoorTest";

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
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_door_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(Scene);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Scene && GameManager.Instance.IsPlaying, 20f);
            yield return null;
            Assert.AreEqual(Scene, SceneManager.GetActiveScene().name, "the room is in the build (DoorTestSetup.BuildDoorTest)");
            lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            pad = new VirtualPad();
            driver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            UnityEngine.Object.DontDestroyOnLoad(driver.gameObject);
            driver.pad = pad;
            GameInput.SetBotSource(pad);
            yield return WaitUntil(() => RoomTracker.Current != null, 3f);
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
        public IEnumerator EveryFireDoor_StartsOpen_WithALeverOnEachSide()
        {
            Assert.AreEqual(3, FireDoor.All.Count, "the three fire doors of the hall");
            foreach (FireDoor door in FireDoor.All)
            {
                Assert.AreEqual(FireDoor.State.Open, door.Current, door.name + ": open at the start");
                Assert.AreEqual(2, door.GetComponentsInChildren<DoorLever>().Length, door.name + ": a lever on each side");
                Assert.IsFalse(Through(door), door.name + ": the way is free");
                Assert.IsFalse(door.BarsShown, door.name + ": no bar on an open door");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator HerLever_JamsTheDoor_AndOpensItAgain()
        {
            FireDoor door = Door("D2");
            DoorLever lever = Lever(door, "Lever_In");
            AtLever(lever);
            yield return new WaitForSeconds(0.2f);
            Interactor hands = lucy.GetComponent<Interactor>();
            Assert.AreSame(lever, hands.FindFocus(), "the lever is in reach");
            StringAssert.Contains("jam the fire door", lever.Hint);
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(FireDoor.State.Jammed, door.Current, "the lever jams it, with no question");
            Assert.AreEqual(Interactor.State.Free, hands.Current);
            Assert.IsTrue(door.BarsShown, "a bar across the jammed door (Bruno: \"a horizontal bar\")");
            yield return new WaitForSeconds(0.8f);
            Assert.IsTrue(Through(door), "the leaf stops a line through the doorway");
            StringAssert.Contains("open the fire door", lever.Hint);
            yield return Press(InputManager.GameAction.Interact);
            Assert.AreEqual(FireDoor.State.Open, door.Current, "her lever opens it again (Bruno: \"yes\")");
            Assert.IsFalse(door.BarsShown, "the bars go");
            yield return new WaitForSeconds(0.8f);
            Assert.IsFalse(Through(door), "the way is free again");
        }

        [UnityTest]
        public IEnumerator TheJam_IsRefused_WithSomeoneInTheDoorway()
        {
            FireDoor door = Door("D2");
            DoorLever lever = Lever(door, "Lever_In");
            lucy.GetComponent<TankController>().Teleport(door.transform.position, 0f);
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(door.Blocked(), "she stands in the doorway");
            Assert.AreEqual(InteractKind.Message, lever.KindFor(lucy.GetComponent<Interactor>()));
            Assert.AreEqual("blocked:" + door.name, lever.Confirm(lucy.GetComponent<Interactor>()));
            Assert.AreEqual(FireDoor.State.Open, door.Current, "the door stays open");
        }

        [UnityTest]
        public IEnumerator AnOpenFireDoor_LetsASoldierThrough()
        {
            SoldierBrain soldier = Figure("Soldier_Pistol");
            soldier.Alert("test", callOthers: false);
            yield return WaitUntil(() => RoomOf(soldier) == "D1", 20f);
            Assert.AreEqual("D1", RoomOf(soldier), "he comes through the open fire door into the hall");
            Assert.AreEqual(0, Door("D2").Kicks);
        }

        [UnityTest]
        public IEnumerator Soldiers_KickAJammedDoorDown_AndComeThrough()
        {
            FireDoor door = Door("D2");
            Assert.IsTrue(door.Jam("test", instant: true));
            SoldierBrain a = Figure("Soldier_Rifle"), b = Figure("Soldier_Pistol");
            a.Alert("test", callOthers: false);
            b.Alert("test", callOthers: false);
            yield return WaitUntil(() => door.Kicks > 0, 20f);
            Assert.Greater(door.Kicks, 0, "a soldier kicks the jammed door");
            Assert.IsTrue(a.AtDoor || b.AtDoor, "he stands at it");
            Assert.AreEqual(0, door.Bangs, "a soldier does not bang");
            yield return WaitUntil(() => door.Current == FireDoor.State.Broken, 25f);
            Assert.AreEqual(FireDoor.State.Broken, door.Current, "the kicks break it");
            Assert.GreaterOrEqual(door.Kicks, 4, "a 100 HP leaf takes four kicks of 25");
            Assert.IsTrue(door.GetComponent<DoorLeaf>().IsBroken);
            Assert.IsFalse(Lever(door, "Lever_In").Available, "a broken door's lever does nothing");
            yield return WaitUntil(() => RoomOf(a) == "D1" || RoomOf(b) == "D1", 15f);
            Assert.IsTrue(RoomOf(a) == "D1" || RoomOf(b) == "D1", "they come through the hole");
        }

        [UnityTest]
        public IEnumerator ThePowerArmour_BreaksAJammedDoor_InTwoBlows()
        {
            FireDoor door = Door("D3");
            door.Jam("test", instant: true);
            SoldierBrain armour = Figure("Armour");
            Assert.IsTrue(armour.IsHeavy && armour.CanBreach);
            armour.Alert("test", callOthers: false);
            yield return WaitUntil(() => door.Current == FireDoor.State.Broken, 30f);
            Assert.AreEqual(FireDoor.State.Broken, door.Current, "the armour breaks it");
            Assert.AreEqual(2, door.Kicks, "two blows of 50");
        }

        [UnityTest]
        public IEnumerator AGuardAndAScientist_OnlyBang_OnAJammedDoor()
        {
            FireDoor door = Door("D4");
            door.Jam("test", instant: true);
            SoldierBrain guard = Figure("Guard"), scientist = Figure("Scientist");
            Assert.IsFalse(guard.CanBreach, "a guard does not breach");
            Assert.IsFalse(scientist.CanBreach, "a scientist does not breach");
            guard.Alert("test", callOthers: false);
            scientist.Alert("test", callOthers: false);
            yield return WaitUntil(() => door.Bangs >= 3, 25f);
            Assert.GreaterOrEqual(door.Bangs, 3, "they bang on it");
            yield return new WaitForSeconds(4f);
            Assert.AreEqual(0, door.Kicks, "no kick");
            Assert.AreEqual(FireDoor.State.Jammed, door.Current, "the door holds: the lever on their side does not open it");
            Assert.AreEqual(door.GetComponent<DoorLeaf>().MaxHealth, door.GetComponent<DoorLeaf>().Health, "no damage");
            Assert.AreNotEqual("D1", RoomOf(guard));
            Assert.AreNotEqual("D1", RoomOf(scientist));
        }

        [UnityTest]
        public IEnumerator NothingBreaksACardDoor()
        {
            LockedDoor card = UnityEngine.Object.FindObjectsByType<LockedDoor>(FindObjectsSortMode.None).Single();
            Assert.IsTrue(Physics.Linecast(new Vector3(5f, 1.3f, 1.5f), new Vector3(5f, 1.3f, -1.5f), out RaycastHit hit, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore)
                && hit.collider.transform.IsChildOf(card.transform), "the card door's leaf stands in the closet doorway");
            SoldierBrain soldier = Figure("Closet_Soldier");
            soldier.Alert("test", callOthers: false);
            yield return new WaitForSeconds(8f);
            Assert.IsFalse(card.IsOpen, "the card door stays shut");
            Assert.AreEqual("D5", RoomOf(soldier), "he stays in the closet");
            Assert.IsFalse(soldier.AtDoor, "a card door is no fire door: no kicks");
        }

        [UnityTest]
        public IEnumerator AJammedDoor_BetweenHerAndHerTarget_DropsTheLock()
        {
            // 2026-11-03, the doors bot: she locked a soldier through the open doorway, the door shut, and the lock held
            // on him through the leaf (a power then reaches him through a shut door).
            FireDoor door = Door("D2");
            Door("D3").Jam("test", instant: true); // only the barracks in her sight
            Door("D4").Jam("test", instant: true);
            lucy.GetComponent<TankController>().Teleport(new Vector3(5f, 0f, 6.2f), 0f);
            LockOnController lockOn = lucy.GetComponent<LockOnController>();
            // A lock on the guard from the start (west door open) drops 1 s after his door jams: then the soldier.
            yield return WaitUntil(() => lockOn.CurrentTarget != null && lockOn.CurrentTarget.name.StartsWith("Soldier"), 5f);
            Assert.IsNotNull(lockOn.CurrentTarget, "she locks a soldier through the open doorway");
            StringAssert.StartsWith("Soldier", lockOn.CurrentTarget.name);
            door.Jam("test", instant: true);
            yield return new WaitForSeconds(1.6f);
            Assert.IsNull(lockOn.CurrentTarget, "the jammed door drops the lock");
        }

        [UnityTest]
        public IEnumerator HerPowers_BreakAJammedDoor_TheEnemyBulletsDoNot()
        {
            // Bruno, 2026-11-03: "her powers can break a door as well ... it should be an option".
            FireDoor door = Door("D2");
            DoorLeaf leaf = door.GetComponent<DoorLeaf>();
            Vector3 at = door.transform.position + Vector3.up;
            Breakables.Splash(at, 1.5f, 500f, DamageType.PsychicBlast);
            Assert.IsFalse(leaf.IsBroken, "an open door stands in its pocket: nothing reaches it");
            door.Jam("test", instant: true);
            leaf.Damage(500f, DamageType.EnemyBullet, at, door.outward);
            leaf.Damage(500f, DamageType.EnemyProjectile, at, door.outward);
            leaf.Damage(500f, DamageType.ShortShock, at, door.outward);
            Assert.AreEqual(leaf.MaxHealth, leaf.Health, "the enemy bullets and the short leave it whole (PROPOSED)");
            Breakables.Splash(at - door.outward * 0.8f, 1.5f, 60f, DamageType.PsychicBlast);
            Assert.Less(leaf.Health, leaf.MaxHealth, "the splash of her blast wears it down");
            Breakables.Splash(at - door.outward * 0.8f, 1.5f, 200f, DamageType.PsychicTelekinesis);
            Assert.IsTrue(leaf.IsBroken, "a thrown thing's splash breaks it");
            Assert.AreEqual(FireDoor.State.Broken, door.Current);
            Assert.IsFalse(door.BarsShown, "no bar on a hole");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheSave_KeepsAJammedDoor()
        {
            FireDoor north = Door("D2"), west = Door("D4");
            north.Jam("test", instant: true);
            RoomState room = SaveSystem.CaptureRoom(SceneManager.GetActiveScene());
            Assert.AreEqual(1, room.jammed.Count, "one jammed door");
            north.Open("test", instant: true);
            west.Jam("test", instant: true);
            SaveSystem.ApplyRoom(SceneManager.GetActiveScene(), room);
            Assert.AreEqual(FireDoor.State.Jammed, north.Current, "the load jams it again");
            Assert.AreEqual(FireDoor.State.Open, west.Current, "and opens the other");
            yield return null;
        }

        // ── Helpers ───────────────────────────────────────────────────

        /// <summary>The hall's fire door to the room.</summary>
        private static FireDoor Door(string to)
        {
            FireDoor door = FireDoor.All.FirstOrDefault(d => d.name == "FireDoor_D1_" + to);
            Assert.IsNotNull(door, "FireDoor_D1_" + to);
            return door;
        }

        private static DoorLever Lever(FireDoor door, string name)
        {
            DoorLever lever = door.GetComponentsInChildren<DoorLever>().FirstOrDefault(l => l.name == name);
            Assert.IsNotNull(lever, door.name + "/" + name);
            return lever;
        }

        /// <summary>A line through the doorway at chest height meets the leaf.</summary>
        private static bool Through(FireDoor door)
        {
            Vector3 a = door.transform.position + Vector3.up * 1.3f - door.outward * 1.5f, b = a + door.outward * 3f;
            return Physics.Linecast(a, b, out RaycastHit hit, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore) && hit.collider.transform.IsChildOf(door.transform);
        }

        private static SoldierBrain Figure(string name)
        {
            SoldierBrain brain = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == name);
            Assert.IsNotNull(brain, name + " (EnemySpawn: run InvectorEnemyFactory.BuildEnemyPrefabs after a vendor import)");
            return brain;
        }

        private static string RoomOf(Component c) => Room.At(c.transform.position)?.id ?? "";

        /// <summary>Her in front of the lever on the hall side, facing it.</summary>
        private void AtLever(DoorLever lever)
        {
            FireDoor door = lever.door;
            Vector3 at = lever.transform.position - door.outward * 0.8f;
            at.y = 0f;
            lucy.GetComponent<TankController>().Teleport(at, Quaternion.LookRotation(door.outward).eulerAngles.y);
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
