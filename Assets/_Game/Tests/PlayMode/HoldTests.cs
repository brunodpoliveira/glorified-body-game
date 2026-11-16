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
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Rooms;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The Hold, map v3 (rounds 118, 119 and 123; DEMO_PLAN §5.0, §6 beats 1 to 5e, §10.6): the restraint, the two
    /// scientists and the cower, the guards who open the door, the booth with two solutions, the tape L36 that unlocks
    /// the feed, the ride on the gurney, EXTEND, the scan C11 and the card, the voice in the airlock (the urge or the
    /// other line), the first feed as cutscene 02b, the gallery, the save room, the hidden door into H9 and W2.
    /// Each test prints its numbers ("[HoldTests]"). The world: DemoWorld.BuildAll. Batch: tools/unity.sh test playmode HoldTests.
    /// </summary>
    public class HoldTests
    {
        private const string World = "Demo";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_hold_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (CutscenePlayer.IsPlaying) CutscenePlayer.Skip();
            yield return WaitUntil(() => !CutscenePlayer.IsPlaying, 5f);
            SaveSystem.Folder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheRestraint_HoldsHer_EveryMkBarAtZero_TheShortAt90_TwelvePressesFreeHer_TheHoldSeals()
        {
            yield return LoadWorld(invulnerable: true);
            yield return WaitUntil(() => CutscenePlayer.IsPlaying, 10f);
            Assert.AreEqual("01_the_wake", CutscenePlayer.Current, "cutscene 01 (version 3) opens the game");
            CutscenePlayer.Skip();
            UprightRestraint frame = UnityEngine.Object.FindFirstObjectByType<UprightRestraint>();
            yield return WaitUntil(() => frame.Holding, 10f);
            GameObject lucy = Her();
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            TankController tank = lucy.GetComponent<TankController>();
            Interactor hand = lucy.GetComponent<Interactor>();
            Assert.IsTrue(frame.Holding, "she is in the frame");
            Assert.AreEqual("H1", RoomTracker.CurrentId, "the cell H1");
            foreach (PowerKind k in new[] { PowerKind.Blast, PowerKind.Fire, PowerKind.Telekinesis })
                Assert.AreEqual(0f, stats.Mk(k), 0.01f, $"the drain line: the {k} bar at zero");
            Assert.AreEqual(90f, stats.Short, 3f, "round 119: the SHORT bar almost full (about 90 percent, PROPOSED)");
            Assert.IsTrue(Feeding.Locked, "the feed is locked");
            Assert.AreSame(frame, hand.FindFocus(), "the Interact key reaches the frame");
            Vector3 start = lucy.transform.position;
            for (int i = 1; i < UprightRestraint.PressesNeeded; i++)
            {
                Assert.AreEqual("restraint:" + i, frame.Confirm(hand));
                yield return null;
                Assert.IsTrue(tank.MovementLocked, $"after {i} presses she is still held");
            }
            int strapsLeft = frame.straps.Count(s => s.activeSelf);
            Assert.IsFalse(StoryFlags.Has(DemoFlags.Free), "not free before the last press");
            frame.Confirm(hand);
            yield return WaitUntil(() => StoryFlags.Has("demo_h1_wake"), 3f);
            Debug.Log($"[HoldTests] the restraint: {frame.Presses} presses, straps left before the last {strapsLeft}, MK {stats.Mk(PowerKind.Blast):0}/{stats.Mk(PowerKind.Fire):0}/{stats.Mk(PowerKind.Telekinesis):0}, SHORT {stats.Short:0.0}");
            Assert.AreEqual(UprightRestraint.PressesNeeded, frame.Presses, "12 presses (PROPOSED)");
            Assert.IsTrue(frame.Broken && !frame.Holding, "the frame lets go");
            Assert.IsTrue(StoryFlags.Has(DemoFlags.Free) && StoryFlags.Has(DemoFlags.Lockdown), "free, and the Hold seals");
            Assert.IsFalse(tank.MovementLocked, "she walks");
            Assert.IsFalse(frame.drainLine.activeSelf, "the line is torn out");
            Assert.Less(frame.cage.position.y, 0.5f, "the cage lies on the floor");
            Assert.AreEqual(0f, stats.Mk(PowerKind.Blast), 0.01f, "she starts with empty bars");
            Assert.Less(Vector3.Distance(start, lucy.transform.position), 0.3f, "she stands where the frame held her");
            Assert.IsNotNull(GameObject.Find("Capsule_H1"), "the morning dose on the tray");
            Assert.AreEqual(GameObject.Find("Gate_Lockdown").GetComponentInChildren<Light>(true).gameObject.activeInHierarchy, true, "the red lamps of the airlock");
        }

        [UnityTest]
        public IEnumerator TheTwoScientists_TheFirstTalksAndAttacks_TheSecondRunsOutOfReach_ThenTheGuardsComeIn()
        {
            yield return Freed();
            GameObject lucy = Her();
            TankController tank = lucy.GetComponent<TankController>();
            FireDoor door = FireDoor.All.First(d => d.name == "FireDoor_H2b_H3");
            FireDoor runnerDoor = FireDoor.All.First(d => d.name == "FireDoor_H2_H2b");
            Assert.AreEqual(FireDoor.State.Jammed, door.Current, "the guard post holds the door shut");
            Assert.AreEqual(FireDoor.State.Jammed, runnerDoor.Current, "the stores' door is shut");
            tank.Teleport(new Vector3(24.1f, 0f, 35.5f), 270f);
            yield return WaitUntil(() => CutscenePlayer.Current == "02_the_scientist", 10f);
            Assert.AreEqual("02_the_scientist", CutscenePlayer.Current, "cutscene 02 in H2");
            CutscenePlayer.Skip();
            yield return WaitUntil(() => StoryFlags.Has(DemoFlags.MeetDone), 10f);
            Assert.AreEqual(2, new[] { "Capsule_H2_Erebus", "Capsule_H2_Orzabal" }.Count(n => GameObject.Find(n) != null), "the EREBUS and ORZABAL capsules fell");
            SoldierBrain knife = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).First(b => b.name == "Scientist_Knife");
            CivilianBrain runner = UnityEngine.Object.FindObjectsByType<CivilianBrain>(FindObjectsSortMode.None).First(b => b.name == "Scientist_Runner");
            yield return WaitUntil(() => knife.Alerted, 5f);
            Assert.IsTrue(knife.Alerted, "he attacks after the talk");
            Assert.AreEqual("H2", Room.At(runner.transform.position + Vector3.up * 0.1f).id, "the second scientist is in H2");
            float t0 = Time.time;
            knife.GetComponent<Health>().TakeDamage(9999f);
            yield return WaitUntil(() => runnerDoor.Current == FireDoor.State.Open, 3f);
            Assert.AreEqual(FireDoor.State.Open, runnerDoor.Current, "the stores' door opens at the death (Bruno, 2026-11-15: \"run out of reach\")");
            yield return WaitUntil(() => StoryFlags.Has(DemoFlags.RunnerCowered), 15f);
            float cowered = Time.time - t0;
            Vector3 hideAt = new Vector3(15.75f, 0f, 37.2f);
            float fromHide = Vector3.Distance(Flat(runner.transform.position), hideAt);
            Debug.Log($"[HoldTests] the runner: cowers {cowered:0.0} s after the death, {fromHide:0.00} m from his corner, in {Room.At(runner.transform.position + Vector3.up * 0.1f)?.id}, phase {runner.Current}, last line '{runner.GetComponent<Bark>().Shown}'");
            Assert.IsTrue(StoryFlags.Has(DemoFlags.RunnerCowered), "he runs into the stores and cowers");
            Assert.AreEqual("H2b", Room.At(runner.transform.position + Vector3.up * 0.1f).id, "he cowers in the stores");
            Assert.Less(fromHide, 1.2f, "behind the linen shelf");
            yield return WaitUntil(() => runnerDoor.Current == FireDoor.State.Jammed, 3f);
            Assert.AreEqual(FireDoor.State.Jammed, runnerDoor.Current, "the door shuts behind him");
            Assert.AreEqual(FireDoor.State.Jammed, door.Current, "the guard post's door is still shut");
            // No line from H2 to him: the short in H2 does not reach him.
            Vector3 hisChest = runner.transform.position + Vector3.up * 1.2f;
            int open = 0;
            for (float x = 19.5f; x < 25f; x += 0.5f)
                for (float z = 33.5f; z < 38f; z += 0.5f)
                    if (!Physics.Linecast(new Vector3(x, 1f, z), hisChest, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore)) open++;
            Assert.AreEqual(0, open, "no line from H2 to him");
            Assert.IsTrue(Bark.CountOf(runner.gameObject) >= 1, "he says the writer's lines (h2_runner_*)");
            yield return WaitUntil(() => door.Current == FireDoor.State.Open, 12f);
            float opened = Time.time - t0;
            Assert.AreEqual(FireDoor.State.Open, door.Current, "the guards open the door");
            yield return WaitUntil(() => runnerDoor.Current == FireDoor.State.Open, 3f);
            Assert.AreEqual(FireDoor.State.Open, runnerDoor.Current, "and the stores' door on their way");
            SoldierBrain[] guards = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).Where(b => b.name.StartsWith("Guard_H3")).ToArray();
            Assert.AreEqual(2, guards.Length, "two guards (round 118, CANON)");
            yield return WaitUntil(() => guards.All(g => g.Alerted), 5f);
            Debug.Log($"[HoldTests] the door opens {opened:0.0} s after the death; guards alerted {guards.Count(g => g.Alerted)} of 2");
            Assert.IsTrue(guards.All(g => g.Alerted), "they come for her");
            yield return WaitUntil(() => StoryFlags.Has(DemoFlags.GuardsDone), 8f);
            Assert.IsTrue(StoryFlags.Has(DemoFlags.GuardsDone), "the beat ran (the guard's line, then the alert)");
        }

        [UnityTest]
        public IEnumerator TheCard_DeniedBeforeTheScanOfHim_GivenAfterIt_AndItOpensTheAirlockGlassDoor()
        {
            yield return Freed();
            GameObject lucy = Her();
            TankController tank = lucy.GetComponent<TankController>();
            Interactor hand = lucy.GetComponent<Interactor>();
            ItemInventory items = lucy.GetComponent<ItemInventory>();
            yield return WaitUntil(() => UnityEngine.Object.FindObjectsByType<CarrierTalk>(FindObjectsSortMode.None).Any(t => t.name == "Scientist_Runner"), 5f);
            CarrierTalk talk = UnityEngine.Object.FindObjectsByType<CarrierTalk>(FindObjectsSortMode.None).First(t => t.name == "Scientist_Runner");
            Assert.AreEqual("H2", Room.At(talk.transform.position + Vector3.up * 0.1f).id);
            StringAssert.StartsWith("denied:", talk.Confirm(hand), "before the scan C11 he denies it");
            Assert.IsFalse(items.Has("security_card"));
            // The scan C11 on him (Bruno, 2026-11-16: "scan him"; it was on the reader of the landing, across the gap).
            Vector3 front = talk.transform.position + talk.transform.forward * 0.9f;
            tank.Teleport(new Vector3(front.x, 0f, front.z), Quaternion.LookRotation(-talk.transform.forward).eulerAngles.y);
            yield return null;
            PsychicScanner scanner = lucy.GetComponent<PsychicScanner>();
            Assert.IsTrue(scanner.TryScan(), "the scan of the card carrier");
            Assert.AreSame(talk.GetComponent<Scannable>(), scanner.Viewing, "his own scan");
            scanner.End("test");
            Assert.IsTrue(StoryFlags.Has(DemoFlags.CardScanned), "the scan C11 raises its flag");
            LockedDoor landing = AirlockDoor(); // the glass door of the airlock, on her side of the gap since 2026-11-16
            StringAssert.Contains("security card", landing.TextFor(hand), "the door needs the security card");
            StringAssert.StartsWith("given:", talk.Confirm(hand), "after the scan he gives it");
            Assert.IsTrue(items.Has("security_card"), "the card is hers");
            Assert.AreEqual(1, talk.Denials);
            landing.Confirm(hand);
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(landing.IsOpen, "the card opens the glass door to the tube");
            Debug.Log($"[HoldTests] the card: 1 denial before the scan, given after it, the landing door open: {landing.IsOpen}");
        }

        [UnityTest]
        public IEnumerator TheBooth_TwoBlastsBreakTheGlass()
        {
            yield return Freed();
            GameObject lucy = Her();
            lucy.GetComponent<TankController>().Teleport(new Vector3(11f, 0f, 44f), 0f);
            yield return WaitUntil(() => RoomTracker.CurrentId == "H4", 3f);
            GlassDoor door = UnityEngine.Object.FindFirstObjectByType<GlassDoor>();
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            powers.Select(PowerKind.Blast);
            LockOnTargetable target = door.GetComponent<LockOnTargetable>();
            powers.FireAt(powers.Selected, 1f, target);
            yield return null;
            Assert.AreEqual(1, door.Blasts);
            Assert.IsFalse(door.IsBroken, "the first blast cracks it");
            Assert.IsTrue(door.crack.activeSelf, "the crack shows");
            powers.FireAt(powers.Selected, 0.4f, target);
            yield return null;
            Debug.Log($"[HoldTests] the glass: {door.Blasts} blasts, broken {door.IsBroken}, alarm {StoryFlags.Has(DemoFlags.Alarm)}");
            Assert.IsTrue(door.IsBroken, "the second breaks it (a part charge too)");
            Assert.IsTrue(StoryFlags.Has(DemoFlags.GlassBroken));
            Assert.IsFalse(door.GetComponent<Collider>().enabled, "the way is open");
            Assert.IsFalse(StoryFlags.Has(DemoFlags.Alarm), "no fire alarm");
        }

        [UnityTest]
        public IEnumerator TheBooth_FireOnTheDrapes_StartsTheAlarm_TheDoorOpens_ThenTheTapeUnlocksTheFeed()
        {
            yield return Freed();
            GameObject lucy = Her();
            lucy.GetComponent<TankController>().Teleport(new Vector3(11f, 0f, 44f), 0f);
            yield return WaitUntil(() => RoomTracker.CurrentId == "H4", 3f);
            GlassDoor door = UnityEngine.Object.FindFirstObjectByType<GlassDoor>();
            FireAlarm alarm = UnityEngine.Object.FindFirstObjectByType<FireAlarm>();
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            powers.Select(PowerKind.Fire);
            LockOnTargetable drapes = GameObject.Find("Drapes").GetComponent<LockOnTargetable>();
            powers.FireAt(powers.Selected, 0.6f, drapes);
            yield return null;
            Assert.IsFalse(alarm.Ringing, "a part charge only stuns: no fire, no alarm");
            powers.FireAt(powers.Selected, 1f, drapes);
            yield return null;
            Assert.IsTrue(alarm.Ringing, "a full circle on the drapes starts the alarm");
            Assert.IsTrue(door.IsOpen && !door.IsBroken, "the lock releases: the door opens whole");
            Assert.IsTrue(alarm.lamps.activeSelf && alarm.flames.activeSelf, "the red lamps, the drapes burn");
            Assert.AreEqual("THEATER CONTROL. AUTHORIZED STAFF ONLY. DOOR RELEASES ON FIRE ALARM.", door.lockedText, "the sign of DEMO_PLAN §7 (rounds 138 and 142)");
            Assert.AreEqual(2, alarm.paLines.Length, "call F of T5_pa.md");
            yield return WaitUntil(() => alarm.PaSaid >= 1 && GlorifiedBody.Subtitles.SubtitleService.Lines.Any(l => l.Speaker == "PA"), 4f);
            Assert.IsTrue(GlorifiedBody.Subtitles.SubtitleService.Lines.Any(l => l.Speaker == "PA" && l.Text.Contains("Fire alarm")), "the PA call under the bell (round 138: \"Attention all personnel. Fire alarm in this area.\")");
            // The nurse is a hider in the booth; she answers when asked for the card.
            AskTalk nurse = UnityEngine.Object.FindObjectsByType<AskTalk>(FindObjectsSortMode.None).FirstOrDefault(a => a.name == "Nurse_Booth");
            Assert.IsNotNull(nurse, "the nurse can be asked");
            StringAssert.StartsWith("denied:", nurse.Confirm(lucy.GetComponent<Interactor>()), "the nurse does not have it");
            // The tape L36 on the console.
            Assert.IsTrue(Feeding.Locked, "the feed is locked before the tape");
            lucy.GetComponent<TankController>().Teleport(new Vector3(12.6f, 0f, 47.8f), 0f);
            yield return null;
            FlagSwitch console = GameObject.Find("Console_L36").GetComponent<FlagSwitch>();
            console.Confirm(lucy.GetComponent<Interactor>());
            yield return WaitUntil(() => CutscenePlayer.Current == "l36_incident_tape", 5f);
            Assert.AreEqual("l36_incident_tape", CutscenePlayer.Current, "the tape plays");
            CutscenePlayer.Skip();
            yield return WaitUntil(() => StoryFlags.Has(DemoFlags.TapeDone), 5f);
            Debug.Log($"[HoldTests] the tape: the cutscene played, feed locked after {Feeding.Locked}, L36 in her logs {lucy.GetComponent<ItemInventory>().Has("l36_incident_tape")}");
            Assert.IsFalse(Feeding.Locked, "round 118, CANON: the video log unlocks the feed");
            Assert.IsTrue(StoryFlags.Has(FirstFeed.TapeFlag));
            Assert.IsTrue(lucy.GetComponent<ItemInventory>().Has("l36_incident_tape"), "the tape lies in her logs");
        }

        [UnityTest]
        public IEnumerator StevensNote_IsInHerInventory_WhenTheFrameLetsGo_AndOpensNoMap()
        {
            // Bruno, 2026-11-15: "the note starts in the inventory ... to signal that this game has inventory. like he instructs,
            // the full instruction thing/map is on a printer in a room".
            yield return Freed();
            GameObject lucy = Her();
            ItemInventory items = lucy.GetComponent<ItemInventory>();
            yield return WaitUntil(() => items.Has("stevens_note"), 3f);
            Interactor hand = lucy.GetComponent<Interactor>();
            Debug.Log($"[HoldTests] Stevens' note: in the inventory {items.Has("stevens_note")}, the toast '{SaveSystem.LastToast}', the printout {items.Has("demo_plan")}");
            Assert.IsTrue(items.Has("stevens_note"), "the note is in her inventory");
            StringAssert.Contains("inventory", SaveSystem.LastToast, "a toast says so");
            Assert.IsFalse(lucy.GetComponent<TankController>().MovementLocked, "the toast does not stop her");
            Assert.AreEqual(ItemKind.Log, ItemDefinition.Find("stevens_note").kind, "a log, no map");
            Assert.IsFalse(items.Has("demo_plan"), "the printout is not hers yet");
            Assert.IsFalse(GlorifiedBody.Map.MapKnowledge.HasPlan(items, "demo_2") || GlorifiedBody.Map.MapKnowledge.HasPlan(items, "demo_1"), "the map shows no floor from the note");
            ItemPickup printout = GameObject.Find("L1_Printout").GetComponent<ItemPickup>();
            Assert.AreEqual("H9", printout.GetComponentInParent<Room>().id, "the printout lies on the printer of H9");
        }

        // ── The theater (Bruno, 2026-11-15: "every power has its chance to shine") ──

        private static FireDoor TheaterDoor() => UnityEngine.Object.FindObjectsByType<FireDoor>(FindObjectsSortMode.None)
            .First(d => d.name == "FireDoor_H3b_H4" || d.name == "FireDoor_H4_H3b");

        /// <summary>Into the theater past the lock line: the door shuts and jams behind her.</summary>
        private static IEnumerator LockedInTheTheater()
        {
            yield return Freed();
            Her().GetComponent<TankController>().Teleport(new Vector3(11f, 0f, 43.9f), 0f);
            yield return WaitUntil(() => TheaterDoor().Current == FireDoor.State.Jammed, 4f);
        }

        [UnityTest]
        public IEnumerator TheTheater_LocksBehindHer_NothingBreaksTheDoor_TheBoothButtonOpensIt()
        {
            yield return LockedInTheTheater();
            FireDoor door = TheaterDoor();
            Assert.AreEqual(FireDoor.State.Jammed, door.Current, "the door shuts and jams behind her");
            Assert.IsTrue(StoryFlags.Has(DemoFlags.TheaterLocked));
            Assert.IsFalse(door.canBreak, "an unbreakable door (\"an invincible one, shows that there are those\")");
            GlorifiedBody.Destruction.DoorLeaf leaf = door.GetComponentInChildren<GlorifiedBody.Destruction.DoorLeaf>();
            if (leaf != null) leaf.Damage(1000f, GlorifiedBody.Combat.DamageType.PsychicBlast, leaf.transform.position, Vector3.forward);
            yield return null;
            Assert.AreEqual(FireDoor.State.Jammed, door.Current, "her powers do not break it either");
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<DoorLever>(FindObjectsSortMode.None).Count(l => l.name.Contains("H3b_H4") || l.name.Contains("H4_H3b")), "no lever");
            // Two blasts break the booth's glass; the button in the booth opens the theater's door.
            GameObject lucy = Her();
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            GlassDoor glass = UnityEngine.Object.FindFirstObjectByType<GlassDoor>();
            powers.Select(PowerKind.Blast);
            powers.FireAt(powers.Selected, 1f, glass.GetComponent<LockOnTargetable>());
            string first = powers.LastResult;
            yield return new WaitForSeconds(0.3f); // HitEventBus drops the same hit within one frame: a player never fires two blasts that close
            powers.FireAt(powers.Selected, 1f, glass.GetComponent<LockOnTargetable>());
            yield return null;
            Debug.Log($"[HoldTests] the blasts on the glass: {first}, {powers.LastResult}, blasts {glass.Blasts}, broken {glass.IsBroken}");
            Assert.IsTrue(glass.IsBroken, "two blasts break the glass");
            lucy.GetComponent<TankController>().Teleport(new Vector3(4.9f, 0f, 48.1f), -30f);
            yield return null;
            FlagSwitch button = GameObject.Find("Theater_Release").GetComponent<FlagSwitch>();
            button.Confirm(lucy.GetComponent<Interactor>());
            yield return WaitUntil(() => door.Current == FireDoor.State.Open, 3f);
            Debug.Log($"[HoldTests] the theater: locked, the glass broken, the button raised {StoryFlags.Has(DemoFlags.TheaterRelease)}, the door {door.Current}");
            Assert.AreEqual(FireDoor.State.Open, door.Current, "the button in the booth opens the theater's door");
        }

        [UnityTest]
        public IEnumerator TheTheater_FireOnTheNurse_RingsTheAlarm_AndOpensBothDoors()
        {
            yield return LockedInTheTheater();
            GameObject lucy = Her();
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            powers.Select(PowerKind.Fire);
            LockOnTargetable nurse = GameObject.Find("Nurse_Booth").GetComponentInChildren<LockOnTargetable>();
            Assert.IsNotNull(nurse, "the nurse is a target through the glass");
            powers.FireAt(powers.Selected, 1f, nurse);
            yield return WaitUntil(() => TheaterDoor().Current == FireDoor.State.Open, 3f);
            GlassDoor glass = UnityEngine.Object.FindFirstObjectByType<GlassDoor>();
            Debug.Log($"[HoldTests] the nurse on fire: alarm {StoryFlags.Has(DemoFlags.Alarm)}, the booth open {glass.IsOpen}, the theater's door {TheaterDoor().Current}");
            Assert.IsTrue(StoryFlags.Has(DemoFlags.Alarm), "she burns: the fire alarm");
            Assert.IsTrue(glass.IsOpen, "the booth's door releases");
            Assert.AreEqual(FireDoor.State.Open, TheaterDoor().Current, "the theater's door releases");
        }

        [UnityTest]
        public IEnumerator TheGurney_ThrownAtTheGlass_BreaksIt_AndStaysWhole()
        {
            yield return LockedInTheTheater();
            GameObject lucy = Her();
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            GameObject gurney = GameObject.Find("Steel_Gurney");
            GlassDoor glass = UnityEngine.Object.FindFirstObjectByType<GlassDoor>();
            lucy.GetComponent<TankController>().Teleport(new Vector3(14.4f, 0f, 43.6f), 270f);
            stats.FillMk(PowerKind.Telekinesis);
            powers.Select(PowerKind.Telekinesis);
            LockOnController lockOn = lucy.GetComponent<LockOnController>();
            lockOn.SetTarget(gurney.GetComponent<LockOnTargetable>());
            yield return null;
            var pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            var driver = new GameObject("[Pad]").AddComponent<PadDriver>();
            driver.pad = pad;
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return WaitUntil(() => powers.HeldLift != null, 2f);
            pad.SetHeld(InputManager.GameAction.Charge, false);
            GameInput.ClearBotSource(pad);
            UnityEngine.Object.Destroy(driver.gameObject);
            Assert.IsNotNull(powers.HeldLift, "mk_orzabal lifts the gurney");
            yield return new WaitForSeconds(0.5f);
            Vector3 leaf = glass.leaf.position;
            Assert.IsTrue(powers.HeldLift.Launch(leaf, null), "she throws it at the glass");
            yield return WaitUntil(() => glass.IsBroken, 4f);
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[HoldTests] the thrown gurney: glass broken {glass.IsBroken}, the gurney at {gurney.transform.position}, active {gurney.activeInHierarchy}");
            Assert.IsTrue(glass.IsBroken, "the gurney breaks the glass door");
            Assert.IsTrue(StoryFlags.Has(DemoFlags.GlassBroken));
            Assert.IsTrue(gurney != null && gurney.activeInHierarchy && gurney.GetComponentsInChildren<Renderer>().Length >= 8, "the gurney stays whole");
            Assert.IsNotNull(gurney.GetComponent<Rideable>(), "and she can still ride it");
        }

        [UnityTest, Ignore("2026-11-16: the surf is plain physics now (Bruno: no special treatment; the mouse moves the held thing under her, TankController.PlatformDelta carries her). A test of the new surf waits for a run.")]
        public IEnumerator TheGurney_RidesOutOfTheTheater_ThroughTheScrubRoomAndTheGuardPost_IntoTheAirlock()
        {
            yield return Freed();
            StoryFlags.Raise("demo_h4_locked"); // the lock has a test of its own: here the door stays open
            foreach (string guard in new[] { "Guard_H3_A", "Guard_H3_B" }) GameObject.Find(guard)?.SetActive(false); // the guards lie dead by now
            GameObject lucy = Her();
            TankController tank = lucy.GetComponent<TankController>();
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            Rideable gurney = GameObject.Find("Steel_Gurney").GetComponent<Rideable>();
            tank.Teleport(gurney.transform.position + gurney.transform.right * 0.9f, gurney.transform.eulerAngles.y);
            yield return null;
            Assert.IsTrue(gurney.ClimbOn());
            stats.FillMk(PowerKind.Telekinesis);
            Assert.IsTrue(gurney.StartRide(stats));
            yield return new WaitForSeconds(0.5f);
            // South through the theater's door and the scrub room, west along the guard post, through the airlock to the tube's door.
            foreach (Vector3 point in new[] { new Vector3(12f, 0f, 40f), new Vector3(12f, 0f, 35.5f), new Vector3(8.0f, 0f, 35.5f), new Vector3(5.2f, 0f, 35.5f) })
                yield return Drive(gurney, point, 14f);
            Debug.Log($"[HoldTests] the way out: the gurney at {gurney.transform.position}, in {Room.At(gurney.transform.position + Vector3.up * 0.1f)?.id}, {gurney.Travelled:0.0} m");
            Assert.AreEqual("H5", Room.At(gurney.transform.position + Vector3.up * 0.1f).id, "she rides it from the theater to the airlock");
            Assert.IsTrue(gurney.OnTop(lucy.transform), "on it all the way");
        }

        /// <summary>The glass card door of the airlock H5, to the tube.</summary>
        private static LockedDoor AirlockDoor() =>
            UnityEngine.Object.FindObjectsByType<LockedDoor>(FindObjectsSortMode.None).First(d => d.GetComponentInParent<Room>().id == "H5");

        /// <summary>Steers the ride to the point: the turn keys first, then forward (a test's driver, not the player).</summary>
        private static IEnumerator Drive(Rideable ride, Vector3 point, float seconds)
        {
            GameObject lucy = Her();
            float t0 = Time.time;
            while (Time.time - t0 < seconds)
            {
                Vector3 to = point - ride.transform.position;
                to.y = 0f;
                if (to.magnitude < 0.3f) yield break;
                float want = Quaternion.LookRotation(to).eulerAngles.y;
                float delta = Mathf.DeltaAngle(lucy.transform.eulerAngles.y, want);
                float turn = Mathf.Abs(delta) > 2f ? Mathf.Clamp(delta / 20f, -1f, 1f) : 0f;
                float move = Mathf.Abs(delta) < 12f ? 1f : 0f;
                ride.Steer(move, turn, Time.deltaTime);
                yield return null;
            }
        }

        [UnityTest, Ignore("2026-11-16: the surf is plain physics now (Bruno: no special treatment; the mouse moves the held thing under her, TankController.PlatformDelta carries her). A test of the new surf waits for a run.")]
        public IEnumerator TheRide_SheLowersTheGurneyOverTheGap_StepsOn_ItCosts40Mk_ItDoesNotLandOnTheGap_AFallReturnsHer()
        {
            yield return Freed();
            GameObject lucy = Her();
            TankController tank = lucy.GetComponent<TankController>();
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            Interactor hand = lucy.GetComponent<Interactor>();
            Rideable gurney = GameObject.Find("Steel_Gurney").GetComponent<Rideable>(); // the crates of G16 ride too
            Assert.AreEqual("H4", Room.At(gurney.transform.position + Vector3.up * 0.1f).id, "the gurney stands in the theater (Bruno, 2026-11-15)");
            LockOnTargetable lockPoint = gurney.GetComponent<LockOnTargetable>();
            Assert.IsTrue(lockPoint != null && lockPoint.IsProp && lockPoint.IsUnbreakable && !lockPoint.IsHeavy, "mk_orzabal lifts and throws it, and nothing breaks it");
            Assert.IsNull(gurney.GetComponent<GlorifiedBody.Destruction.Breakable>(), "it cannot break");
            // 2026-11-14 (Bruno: "the gurney is invisible"): every piece draws at the gurney, not 36 m away.
            Renderer[] pieces = gurney.GetComponentsInChildren<Renderer>(true);
            Assert.GreaterOrEqual(pieces.Length, 8, "a deck, four legs, wheels, rails, a push bar");
            foreach (Renderer piece in pieces)
                Assert.Less(Vector3.Distance(piece.bounds.center, gurney.transform.position), 1.5f, piece.name + " draws at the gurney");
            // For the gap, the gurney waits in the airlock (the way from the theater has a test of its own).
            gurney.transform.SetPositionAndRotation(new Vector3(6.4f, 0f, 35.5f), Quaternion.Euler(0f, 270f, 0f));
            Physics.SyncTransforms();
            Assert.GreaterOrEqual(GameObject.Find("Pit_Return").GetComponent<PitReturn>().size.x, 12f, "the gap is 12 m (Bruno: \"you can't jump over it\")");
            // A fall into the gap: she is back in the airlock.
            PitReturn pit = GameObject.Find("Pit_Return").GetComponent<PitReturn>(); // the gap of the tube (G16 has one too)
            tank.Teleport(new Vector3(1f, 0.05f, 35.5f), 270f);
            float lowestFall = 9f;
            yield return WaitUntil(() => { lowestFall = Mathf.Min(lowestFall, lucy.transform.position.y); return pit.Returns > 0; }, 6f);
            Debug.Log($"[HoldTests] the fall: lowest y {lowestFall:0.00}, returns {pit.Returns}, tube floor {(GameObject.Find("Tube_Floor") != null)}, at {lucy.transform.position}");
            Assert.AreEqual(1, pit.Returns, "the gap: she falls and is back in the airlock");
            yield return null;
            Assert.AreEqual("H5", Room.At(lucy.transform.position + Vector3.up * 0.1f).id);
            // No key climbs on (Bruno, 2026-11-16: "the interact to go on top of it must go"). She carries it over the gap, lowers it
            // until the deck is level with the floor, and walks onto it: the carry becomes the ride.
            tank.Teleport(gurney.transform.position + gurney.transform.right * 0.9f, gurney.transform.eulerAngles.y - 90f);
            yield return null;
            Assert.AreNotSame(gurney, hand.FindFocus(), "the Interact key does not reach the gurney");
            AirlockDoor().Open(true); // the card's glass door (its own test: TheCard_...)
            gurney.transform.SetPositionAndRotation(new Vector3(2.9f, 0f, 35.5f), Quaternion.Euler(0f, 270f, 0f)); // over the gap, as the mouse brings it
            Physics.SyncTransforms();
            tank.Teleport(new Vector3(4.5f, 0f, 35.5f), 270f); // at the edge, facing the gap
            var pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            var driver = new GameObject("[Pad]").AddComponent<PadDriver>();
            driver.pad = pad;
            powers.Select(PowerKind.Telekinesis);
            stats.FillMk(PowerKind.Telekinesis);
            lucy.GetComponent<LockOnController>().SetTarget(gurney.GetComponent<LockOnTargetable>());
            pad.SetHeld(InputManager.GameAction.Defend, true);
            yield return WaitUntil(() => powers.Carrying, 2f);
            Assert.IsTrue(powers.Carrying, "the defend key carries it");
            for (int i = 0; i < 90 && gurney.GetComponent<Collider>().bounds.max.y > 0.02f; i++)
            {
                powers.HeldLift.SteerBy(Vector3.zero, -0.05f, 6f); // the wheel down
                yield return null;
            }
            yield return new WaitForSeconds(0.3f);
            float deck = gurney.GetComponent<Collider>().bounds.max.y;
            Debug.Log($"[HoldTests] lowered over the gap: the deck at {deck:0.00} m");
            Assert.AreEqual(0f, deck, Rideable.BoardLevel, "over the gap the wheel lowers the deck level with the floor");
            Assert.IsTrue(gurney.CanBoardFrom(lucy.transform), "a step onto it");
            pad.SetHeld(InputManager.GameAction.Forward, true);
            yield return WaitUntil(() => powers.Riding != null, 2f);
            pad.SetHeld(InputManager.GameAction.Forward, false);
            Assert.AreSame(gurney, powers.Riding, "the walk key steps her onto it, and the carry becomes the ride");
            Assert.IsFalse(powers.Carrying);
            Assert.IsTrue(gurney.OnTop(lucy.transform), "she stands on the deck");
            Assert.AreEqual(40f, gurney.LastCost, 0.5f, "a ride costs 40 MK (PROPOSED: a full bar crosses twice)");
            Assert.IsTrue(tank.MovementLocked, "her feet stop: the walk keys steer the gurney");
            // The real keys: forward until she is on the landing. A release over the gap does not land it.
            pad.SetHeld(InputManager.GameAction.Forward, true);
            float t0 = Time.time;
            bool gapRefused = false;
            float lowest = 9f;
            int hidden = 0;
            while (Time.time - t0 < 16f && gurney.transform.position.x > -9.6f)
            {
                yield return null;
                lowest = Mathf.Min(lowest, lucy.transform.position.y);
                if (gurney.transform.position.x < -2f && gurney.GetComponentsInChildren<Renderer>().Any(r => !r.enabled))
                    hidden++; // 2026-11-15: the room cull hid the gurney under her over the gap
                if (!gapRefused && Mathf.Abs(gurney.transform.position.x - 1f) < 0.3f)
                {
                    pad.SetHeld(InputManager.GameAction.Defend, false);
                    yield return null;
                    yield return null;
                    gapRefused = powers.Riding == gurney && gurney.Riding;
                    pad.SetHeld(InputManager.GameAction.Defend, true);
                }
            }
            pad.SetHeld(InputManager.GameAction.Forward, false);
            float crossing = Time.time - t0;
            Assert.IsTrue(gapRefused, "over the gap the release does not land it: the ride goes on");
            Assert.AreEqual(0, hidden, "the gurney is drawn all the way over the gap");
            Assert.AreEqual("H6", RoomTracker.CurrentId, "she rides into the landing");
            Assert.Greater(lowest, 0.5f, "she stays on the deck all the way");
            pad.SetHeld(InputManager.GameAction.Defend, false);
            yield return WaitUntil(() => powers.Riding == null, 1f);
            Assert.IsFalse(gurney.Riding, "over the landing the release sets it down");
            Assert.IsFalse(tank.MovementLocked, "she can walk");
            Assert.IsTrue(gurney.OnTop(lucy.transform), "she still stands on the deck");
            pad.SetHeld(InputManager.GameAction.Forward, true); // she walks off its end
            yield return new WaitForSeconds(1.2f);
            pad.SetHeld(InputManager.GameAction.Forward, false);
            GameInput.ClearBotSource(pad);
            UnityEngine.Object.Destroy(driver.gameObject);
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[HoldTests] the ride: {gurney.Travelled:0.0} m in {crossing:0.0} s, cost {gurney.LastCost:0} MK, bar left {stats.Mk(PowerKind.Telekinesis):0}, lowest foot {lowest:0.00} m, falls before {pit.Returns}");
            Assert.AreEqual(0f, lucy.transform.position.y, 0.15f, "she stands on the floor of the landing");
            Assert.AreEqual("H6", RoomTracker.CurrentId);
            Assert.AreEqual(1, pit.Returns, "no fall on the ride");
        }

        [UnityTest]
        public IEnumerator TheBooth_HerCameraInFrontOfTheGlass_ShowsTheNurseThroughIt()
        {
            yield return Freed();
            GameObject lucy = Her();
            lucy.GetComponent<TankController>().Teleport(new Vector3(9f, 0f, 46.6f), 0f);
            yield return WaitUntil(() => RoomTracker.CurrentId == "H4", 3f);
            yield return new WaitForSeconds(1f);
            var director = GlorifiedBody.Cameras.FixedCameraDirector.Instance;
            Assert.AreEqual("Cam_H4_booth", director.Current.name, "in front of the glass, the camera looks into the booth");
            GameObject nurse = GameObject.Find("Nurse_Booth");
            Assert.IsNotNull(nurse, "the nurse is in the booth");
            Camera cam = Camera.main;
            Vector3 chest = nurse.transform.position + Vector3.up * 1.2f;
            Vector3 v = cam.WorldToViewportPoint(chest);
            Debug.Log($"[HoldTests] the booth: the nurse at {nurse.transform.position}, in the frame at {v}");
            Assert.IsTrue(v.z > 0f && v.x > 0.05f && v.x < 0.95f && v.y > 0.05f && v.y < 0.95f, "the nurse is in the frame");
            foreach (RaycastHit hit in Physics.RaycastAll(cam.transform.position, (chest - cam.transform.position).normalized, Vector3.Distance(cam.transform.position, chest), ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(nurse.transform)) continue;
                Renderer r = hit.collider.GetComponent<Renderer>();
                Assert.IsTrue(r == null || !r.enabled || r.sharedMaterial.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent,
                    $"{hit.collider.name} hides her: it must be glass");
            }
            foreach (string pane in new[] { "Booth_Glass_W", "Booth_Glass_E", "Booth_Door" })
                Assert.GreaterOrEqual(GameObject.Find(pane).GetComponent<Renderer>().sharedMaterial.renderQueue, (int)UnityEngine.Rendering.RenderQueue.Transparent, pane + " is see-through");
        }

        [UnityTest]
        public IEnumerator Extend_BringsTheTubeOut_AndSheWalksBackOverTheGap()
        {
            yield return Freed();
            GameObject lucy = Her();
            TankController tank = lucy.GetComponent<TankController>();
            PitReturn pit = GameObject.Find("Pit_Return").GetComponent<PitReturn>(); // the gap of the tube (G16 has one too)
            Assert.IsNull(GameObject.Find("Tube_Floor"), "the tube is pulled back: no floor over the gap");
            tank.Teleport(new Vector3(-8.9f, 0f, 33.6f), 90f);
            yield return null;
            FlagSwitch extend = GameObject.Find("Extend_Button").GetComponent<FlagSwitch>();
            Assert.AreSame(extend, lucy.GetComponent<Interactor>().FindFocus(), "EXTEND in reach on the landing");
            extend.Confirm(lucy.GetComponent<Interactor>());
            yield return null;
            Assert.IsTrue(StoryFlags.Has(DemoFlags.TubeOut));
            Assert.IsNotNull(GameObject.Find("Tube_Floor"), "the tube is out");
            tank.Teleport(new Vector3(1f, 0f, 35.5f), 90f);
            yield return new WaitForSeconds(1f);
            Debug.Log($"[HoldTests] on the tube: y {lucy.transform.position.y:0.00}, room {RoomTracker.CurrentId}, falls {pit.Returns}");
            Assert.AreEqual(0f, lucy.transform.position.y, 0.15f, "she stands on the tube");
            Assert.AreEqual("HT", RoomTracker.CurrentId);
            Assert.AreEqual(0, pit.Returns, "no fall");
        }

        [UnityTest]
        public IEnumerator TheVoice_WithTheFeedLocked_UrgesHer_AndCutscene02bPlaysOnTheNearestBody()
        {
            yield return Freed();
            GameObject lucy = Her();
            Health hp = lucy.GetComponentInChildren<Health>();
            hp.SetInvulnerable(false);
            hp.TakeDamage(hp.MaxHealth * 0.5f);
            hp.SetInvulnerable(true);
            float before = hp.CurrentHealth;
            SoldierBrain knife = null;
            yield return WaitUntil(() => (knife = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == "Scientist_Knife")) != null, 5f);
            knife.GetComponent<Health>().TakeDamage(9999f);
            Assert.IsTrue(Feeding.Locked);
            lucy.GetComponent<ItemInventory>().TryAdd(ItemDefinition.Find("security_card"));
            lucy.GetComponent<TankController>().Teleport(new Vector3(7.5f, 0f, 37f), 270f);
            var heard = new System.Collections.Generic.HashSet<string>();
            yield return WaitUntil(() => { Listen(heard); return CutscenePlayer.Current == "02b_the_chart"; }, 25f);
            Assert.IsTrue(heard.Contains("KNIGHT: Eat."), "round 123: KNIGHT speaks in the airlock, the urge ends on its last line (" + string.Join(" | ", heard) + ")");
            Assert.IsFalse(heard.Any(h => h.Contains("You've eaten")), "the other line does not play");
            Assert.AreEqual("02b_the_chart", CutscenePlayer.Current, "the urge: cutscene 02b");
            CutscenePlayer.Skip();
            yield return WaitUntil(() => StoryFlags.Has(DemoFlags.VoiceUrge), 5f);
            Debug.Log($"[HoldTests] the urge: HP {before:0} to {hp.CurrentHealth:0} of {hp.MaxHealth:0}, feed locked {Feeding.Locked}, at the body {Vector3.Distance(lucy.transform.position, knife.transform.position):0.0} m");
            Assert.IsFalse(Feeding.Locked, "the feed unlocks");
            Assert.AreEqual(hp.MaxHealth, hp.CurrentHealth, 0.5f, "02b, note 5: she heals to full");
            Assert.IsTrue(StoryFlags.Has(FirstFeed.DoneFlag));
            Assert.IsFalse(lucy.GetComponent<TankController>().MovementLocked, "she walks on");
        }

        [UnityTest]
        public IEnumerator TheVoice_WithTheFeedUnlocked_SaysTheOtherLine_NoCutscene()
        {
            yield return Freed();
            GameObject lucy = Her();
            StoryFlags.Raise(Feeding.UnlockFlag);
            lucy.GetComponent<ItemInventory>().TryAdd(ItemDefinition.Find("security_card"));
            lucy.GetComponent<TankController>().Teleport(new Vector3(7.5f, 0f, 37f), 270f);
            var heard = new System.Collections.Generic.HashSet<string>();
            yield return WaitUntil(() => { Listen(heard); return StoryFlags.Has(DemoFlags.VoiceFed); }, 25f);
            Debug.Log("[HoldTests] the voice, fed: " + string.Join(" | ", heard));
            Assert.IsTrue(heard.Any(l => l.StartsWith("KNIGHT: You did fine back there")), "the other line plays (round 138)");
            Assert.IsFalse(heard.Any(h => h.Contains("Eat.") && !h.Contains("eaten")), "no urge");
            Assert.IsFalse(StoryFlags.Has(FirstFeed.DoneFlag), "no cutscene 02b");
            Assert.AreNotEqual("02b_the_chart", CutscenePlayer.Current);
        }

        [UnityTest]
        public IEnumerator TheFirstFeed_AfterTheTape_Is02b_AndHealsHerToFull()
        {
            yield return Freed();
            GameObject lucy = Her();
            StoryFlags.Raise(Feeding.UnlockFlag);
            StoryFlags.Raise(FirstFeed.TapeFlag);
            Health hp = lucy.GetComponentInChildren<Health>();
            hp.SetInvulnerable(false);
            hp.TakeDamage(hp.MaxHealth * 0.6f);
            hp.SetInvulnerable(true);
            SoldierBrain knife = null;
            yield return WaitUntil(() => (knife = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == "Scientist_Knife")) != null, 5f);
            knife.GetComponent<Health>().TakeDamage(9999f);
            yield return new WaitForSeconds(1.5f);
            FeedableBody body = knife.GetComponent<FeedableBody>();
            lucy.GetComponent<TankController>().Teleport(body.Point + Vector3.right * 0.8f, 270f);
            yield return null;
            Assert.IsTrue(lucy.GetComponent<Feeding>().Begin(body), "she feeds");
            yield return WaitUntil(() => CutscenePlayer.Current == "02b_the_chart", 3f);
            Assert.AreEqual("02b_the_chart", CutscenePlayer.Current, "the first feed is cutscene 02b");
            CutscenePlayer.Skip();
            yield return WaitUntil(() => !CutscenePlayer.IsPlaying, 5f);
            yield return null;
            Debug.Log($"[HoldTests] the first feed: HP {hp.CurrentHealth:0} of {hp.MaxHealth:0}, played {FirstFeed.Played}");
            Assert.AreEqual(hp.MaxHealth, hp.CurrentHealth, 0.5f, "a whole man: full HP");
            Assert.IsTrue(StoryFlags.Has(FirstFeed.DoneFlag), "once");
        }

        [UnityTest]
        public IEnumerator TheGallery_TheSaveRoom_TheHiddenDoorShutsBehindHer_AndTheOfficeOpensIntoW2()
        {
            yield return Freed();
            GameObject lucy = Her();
            TankController tank = lucy.GetComponent<TankController>();
            SoldierBrain[] arena = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).Where(b => Room.At(b.transform.position + Vector3.up * 0.1f)?.id == "H7").ToArray();
            Assert.AreEqual(4, arena.Length, "the gallery: three armed scientists and a guard (" + string.Join(",", arena.Select(a => a.name)) + ")");
            Assert.IsNotNull(GameObject.Find("Save_H8"), "the save machine of H8");
            Assert.AreEqual(3, new[] { "Capsule_H8_Warspite", "Capsule_H8_Erebus", "Capsule_H8_Orzabal" }.Count(n => GameObject.Find(n) != null), "one capsule of each power");
            FireDoor hidden = FireDoor.All.First(d => d.name == "FireDoor_H8_H9");
            Assert.AreEqual(FireDoor.State.Jammed, hidden.Current, "the hidden door is shut");
            DoorLever[] levers = hidden.GetComponentsInChildren<DoorLever>();
            Assert.AreEqual(1, levers.Length, "a push bar on the Hold side only");
            Assert.AreEqual("H8", Room.At(levers[0].transform.position + levers[0].transform.forward * 0.3f + Vector3.down * (levers[0].transform.position.y - 0.1f)).id);
            tank.Teleport(new Vector3(4f, 0f, 19.9f), 180f);
            yield return null;
            levers[0].Confirm(lucy.GetComponent<Interactor>());
            yield return WaitUntil(() => hidden.Current == FireDoor.State.Open, 2f);
            Assert.AreEqual(FireDoor.State.Open, hidden.Current, "the push bar opens it");
            tank.Teleport(new Vector3(4f, 0f, 17.5f), 180f);
            yield return WaitUntil(() => hidden.Current == FireDoor.State.Jammed, 4f);
            Debug.Log($"[HoldTests] the hidden door: {hidden.Current} after she walks into {RoomTracker.CurrentId}");
            Assert.AreEqual(FireDoor.State.Jammed, hidden.Current, "it shuts behind her and is a wall again");
            Assert.AreEqual("H9", GameObject.Find("L1_Printout").GetComponentInParent<Room>().id, "Stevens' sheet in the printer of the office");
            tank.Teleport(new Vector3(4f, 0f, 13.2f), 180f);
            yield return WaitUntil(() => RoomTracker.CurrentId == "W2", 3f);
            Assert.AreEqual("W2", RoomTracker.CurrentId, "the office opens into W2");
            Assert.IsTrue(Room.Of("H9").IsNeighbour("W2"));
            Assert.IsNull(Room.Of("W1"), "W1 left the map");
            Assert.IsNull(Room.Of("W1B"), "W1B left the map");
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).Count(b => Room.At(b.transform.position + Vector3.up * 0.1f)?.id == "W2"), "W2 is a quiet corridor");
        }

        [UnityTest]
        public IEnumerator TheUnionSheet_L37_AndTheLetter_L14_InTheBreakRoomG11()
        {
            // Round 119 (CANON: the union angle; the words of L37 PROPOSED, DEMO_PLAN §8). Not the Hold: the change of the same round.
            yield return Freed();
            GameObject lucy = Her();
            ItemPickup l37 = GameObject.Find("L37_Union_Sheet").GetComponent<ItemPickup>();
            ItemPickup l14 = GameObject.Find("L14_Union_Letter").GetComponent<ItemPickup>();
            Assert.AreEqual("G11", l37.GetComponentInParent<Room>().id);
            Assert.AreEqual("G11", l14.GetComponentInParent<Room>().id);
            StringAssert.Contains("You have the right to ask for a union vote.", l37.item.logText); // the plan's words (DEMO_PLAN §8)
            StringAssert.Contains("HR works for them.", l37.item.logText);
            Assert.AreEqual(ItemKind.Log, l37.item.kind);
            lucy.GetComponent<TankController>().Teleport(l37.transform.position + new Vector3(-0.5f, -1.4f, 0f), 90f);
            yield return WaitUntil(() => RoomTracker.CurrentId == "G11", 3f);
            Interactor hand = lucy.GetComponent<Interactor>();
            Debug.Log($"[HoldTests] L37: in focus {(hand.FindFocus() != null ? hand.FindFocus().name : "none")}, {l37.item.logText.Length} characters");
            Assert.AreSame(l37, hand.FindFocus(), "she can take it from the floor in front of the corkboard");
        }

        [UnityTest]
        public IEnumerator TheMusic_ExplorationSearchCombat_AreOnePiece_AndAChangeKeepsTheBeat()
        {
            // Rounds 119 and 123 (CANON): one composition, three states cut from the same audio. The test loops of T41
            // (tools/music_states_placeholder.sh) stand in. Ambient = exploration, Tension = search, Combat = combat.
            yield return Freed();
            var music = GlorifiedBody.Audio.MusicDirector.Instance;
            GlorifiedBody.Audio.MusicDirector.Hold(GlorifiedBody.Audio.MusicState.Ambient);
            yield return new WaitForSeconds(3f);
            Assert.AreEqual("ambient", music.CurrentClip.name);
            int locksBefore = music.Locks;
            var report = new System.Collections.Generic.List<string>();
            foreach (var state in new[] { GlorifiedBody.Audio.MusicState.Tension, GlorifiedBody.Audio.MusicState.Combat, GlorifiedBody.Audio.MusicState.Ambient })
            {
                AudioClip before = music.CurrentClip;
                int sample = music.CurrentSample;
                float t0 = Time.unscaledTime;
                GlorifiedBody.Audio.MusicDirector.Hold(state);
                // The new state starts on the DSP clock, ScheduleLead ahead (2026-11-14): read it once it sounds.
                yield return new WaitForSecondsRealtime((float)GlorifiedBody.Audio.MusicDirector.ScheduleLead + 0.1f);
                float expected = sample + (Time.unscaledTime - t0) * before.frequency;
                float off = (music.CurrentSample - expected) / before.frequency;
                report.Add($"{before.name}->{music.CurrentClip.name} off {off * 1000f:0} ms");
                Assert.IsTrue(GlorifiedBody.Audio.MusicDirector.SamePiece(before, music.CurrentClip), $"{before.name} and {music.CurrentClip.name}: one piece (the same length)");
                Assert.Less(Mathf.Abs(off), 0.12f, $"{state}: the new state starts where the old one was");
                yield return new WaitForSeconds(2.5f);
            }
            Debug.Log("[HoldTests] the music states: " + string.Join("; ", report) + $"; locks {music.Locks - locksBefore}");
            Assert.AreEqual(3, music.Locks - locksBefore, "every change kept the beat");
            GlorifiedBody.Audio.MusicDirector.Hold(null);
        }

        // ── Helpers ───────────────────────────────────────────────────

        [DefaultExecutionOrder(-1000)]
        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad pad;
            private void Update() => pad?.Commit();
        }

        /// <summary>The flag names of the Hold (DemoHold is editor code: the tests keep their own copy).</summary>
        private static class DemoFlags
        {
            public const string Free = "hold_free", Lockdown = "hold_lockdown", MeetDone = "demo_h2_scientist", RunnerCowered = "hold_h2_cowered";
            public const string GuardsDone = "demo_h3_guards", Alarm = "hold_h4_alarm", GlassBroken = "hold_h4_glass_broken", TapeDone = "demo_h4_tape";
            public const string TubeOut = "hold_tube_out", CardScanned = "hold_c11", VoiceUrge = "demo_h5_voice_urge", VoiceFed = "demo_h5_voice_fed";
            public const string TheaterLocked = "hold_h4_locked", TheaterRelease = "hold_h4_release";
        }

        /// <summary>The subtitle lines on screen now, as "SPEAKER: words", added to the set.</summary>
        private static void Listen(System.Collections.Generic.HashSet<string> heard)
        {
            foreach (var l in GlorifiedBody.Subtitles.SubtitleService.Lines) heard.Add(l.Speaker + ": " + l.Text);
        }

        private static GameObject Her() => GameObject.FindGameObjectWithTag("Player");

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static IEnumerator LoadWorld(bool invulnerable)
        {
            GameManager.Instance.LoadLevel(World);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == World && GameManager.Instance.IsPlaying, 30f);
            yield return null;
            Assert.AreEqual(World, SceneManager.GetActiveScene().name);
            if (invulnerable) Her().GetComponentInChildren<Health>().SetInvulnerable(true);
            yield return WaitUntil(() => RoomTracker.Current != null, 3f);
        }

        /// <summary>The world, the wake skipped, the restraint broken (as the last press), the world running.</summary>
        private static IEnumerator Freed()
        {
            yield return LoadWorld(invulnerable: true);
            yield return WaitUntil(() =>
            {
                if (CutscenePlayer.IsPlaying) CutscenePlayer.Skip();
                UprightRestraint.FreeAll();
                return StoryFlags.Has("demo_h1_wake") && !CutscenePlayer.IsPlaying && Time.timeScale > 0f;
            }, 20f);
            Assert.IsTrue(StoryFlags.Has("demo_h1_wake"), "free");
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
