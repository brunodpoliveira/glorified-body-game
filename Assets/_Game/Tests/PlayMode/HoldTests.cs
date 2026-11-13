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
        public IEnumerator TheTwoScientists_TheFirstTalksAndAttacks_TheSecondRunsToTheDoorAndCowers_ThenTheGuardsComeIn()
        {
            yield return Freed();
            GameObject lucy = Her();
            TankController tank = lucy.GetComponent<TankController>();
            FireDoor door = FireDoor.All.First(d => d.name == "FireDoor_H2_H3");
            Assert.AreEqual(FireDoor.State.Jammed, door.Current, "the guard post holds the door shut");
            tank.Teleport(new Vector3(20.1f, 0f, 35.5f), 270f);
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
            yield return WaitUntil(() => StoryFlags.Has(DemoFlags.RunnerCowered), 15f);
            float cowered = Time.time - t0;
            Vector3 doorAt = new Vector3(15f, 0f, 35.5f);
            float fromDoor = Vector3.Distance(Flat(runner.transform.position), doorAt);
            Debug.Log($"[HoldTests] the runner: cowers {cowered:0.0} s after the death, {fromDoor:0.00} m from the door, phase {runner.Current}, last line '{runner.GetComponent<Bark>().Shown}'");
            Assert.IsTrue(StoryFlags.Has(DemoFlags.RunnerCowered), "he runs to the door and cowers");
            Assert.Less(fromDoor, 1.6f, "he cowers at the door of H3");
            Assert.AreEqual(FireDoor.State.Jammed, door.Current, "it does not open for him");
            Assert.IsTrue(Bark.CountOf(runner.gameObject) >= 1, "he says the writer's lines (h2_runner_*)");
            yield return WaitUntil(() => door.Current == FireDoor.State.Open, 12f);
            float opened = Time.time - t0;
            Assert.AreEqual(FireDoor.State.Open, door.Current, "the guards open the door");
            SoldierBrain[] guards = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).Where(b => b.name.StartsWith("Guard_H3")).ToArray();
            Assert.AreEqual(2, guards.Length, "two guards (round 118, CANON)");
            yield return WaitUntil(() => guards.All(g => g.Alerted), 5f);
            Debug.Log($"[HoldTests] the door opens {opened:0.0} s after the death; guards alerted {guards.Count(g => g.Alerted)} of 2");
            Assert.IsTrue(guards.All(g => g.Alerted), "they come for her");
            yield return WaitUntil(() => StoryFlags.Has(DemoFlags.GuardsDone), 8f);
            Assert.IsTrue(StoryFlags.Has(DemoFlags.GuardsDone), "the beat ran (the guard's line, then the alert)");
        }

        [UnityTest]
        public IEnumerator TheCard_DeniedBeforeTheScan_GivenAfterIt_AndItOpensTheLandingDoor()
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
            Assert.IsFalse(items.Has("airlock_card"));
            // The scan of the reader on the landing.
            tank.Teleport(new Vector3(-3.3f, 0f, 30.2f), 180f);
            yield return WaitUntil(() => RoomTracker.CurrentId == "H6", 3f);
            PsychicScanner scanner = lucy.GetComponent<PsychicScanner>();
            Assert.IsTrue(scanner.TryScan(), "the scan of the card reader");
            Assert.AreEqual("c11_card_reader", scanner.Viewing.scanId);
            scanner.End("test");
            Assert.IsTrue(StoryFlags.Has(DemoFlags.CardScanned), "the scan C11 raises its flag");
            LockedDoor landing = UnityEngine.Object.FindObjectsByType<LockedDoor>(FindObjectsSortMode.None).First(d => d.GetComponentInParent<Room>().id == "H6");
            StringAssert.Contains("airlock card", landing.TextFor(hand), "the door needs the airlock card");
            StringAssert.StartsWith("given:", talk.Confirm(hand), "after the scan he gives it");
            Assert.IsTrue(items.Has("airlock_card"), "the card is hers");
            Assert.AreEqual(1, talk.Denials);
            landing.Confirm(hand);
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(landing.IsOpen, "the card opens the door of the gallery");
            Debug.Log($"[HoldTests] the card: 1 denial before the scan, given after it, the landing door open: {landing.IsOpen}");
        }

        [UnityTest]
        public IEnumerator TheBooth_TwoBlastsBreakTheGlass()
        {
            yield return Freed();
            GameObject lucy = Her();
            lucy.GetComponent<TankController>().Teleport(new Vector3(13.6f, 0f, 39.2f), 0f);
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
            lucy.GetComponent<TankController>().Teleport(new Vector3(13.6f, 0f, 39.2f), 0f);
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
            Assert.AreEqual("THEATRE CONTROL. AUTHORISED STAFF ONLY. DOOR RELEASES ON FIRE ALARM.", door.lockedText, "the sign of DEMO_PLAN §7");
            Assert.AreEqual(2, alarm.paLines.Length, "call F of T5_pa.md");
            yield return WaitUntil(() => alarm.PaSaid >= 1 && GlorifiedBody.Subtitles.SubtitleService.Lines.Any(l => l.Speaker == "PA"), 4f);
            Assert.IsTrue(GlorifiedBody.Subtitles.SubtitleService.Lines.Any(l => l.Speaker == "PA" && l.Text.StartsWith("Fire alarm")), "the PA call under the bell");
            // The nurse is a hider in the booth; she answers when asked for the card.
            AskTalk nurse = UnityEngine.Object.FindObjectsByType<AskTalk>(FindObjectsSortMode.None).FirstOrDefault(a => a.name == "Nurse_Booth");
            Assert.IsNotNull(nurse, "the nurse can be asked");
            StringAssert.StartsWith("denied:", nurse.Confirm(lucy.GetComponent<Interactor>()), "the nurse does not have it");
            // The tape L36 on the console.
            Assert.IsTrue(Feeding.Locked, "the feed is locked before the tape");
            lucy.GetComponent<TankController>().Teleport(new Vector3(12.6f, 0f, 41.8f), 0f);
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
        public IEnumerator TheRide_TheGurneyCarriesHerOverTheGap_ItCosts40Mk_ItDoesNotLandOnTheGap_AFallReturnsHer()
        {
            yield return Freed();
            GameObject lucy = Her();
            TankController tank = lucy.GetComponent<TankController>();
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            Interactor hand = lucy.GetComponent<Interactor>();
            GurneyRide gurney = UnityEngine.Object.FindFirstObjectByType<GurneyRide>();
            Assert.AreEqual("H5", Room.At(gurney.transform.position + Vector3.up * 0.1f).id, "the gurney stands in the airlock");
            Assert.IsNull(gurney.GetComponent<LockOnTargetable>(), "no lock point: nothing breaks it, no throw takes it");
            Assert.IsNull(gurney.GetComponent<GlorifiedBody.Destruction.Breakable>(), "it cannot break");
            // A fall into the gap: she is back in the airlock.
            PitReturn pit = GameObject.Find("Pit_Return").GetComponent<PitReturn>(); // the gap of the tube (G16 has one too)
            tank.Teleport(new Vector3(1f, 0.05f, 35.5f), 270f);
            float lowestFall = 9f;
            yield return WaitUntil(() => { lowestFall = Mathf.Min(lowestFall, lucy.transform.position.y); return pit.Returns > 0; }, 6f);
            Debug.Log($"[HoldTests] the fall: lowest y {lowestFall:0.00}, returns {pit.Returns}, tube floor {(GameObject.Find("Tube_Floor") != null)}, at {lucy.transform.position}");
            Assert.AreEqual(1, pit.Returns, "the gap: she falls and is back in the airlock");
            yield return null;
            Assert.AreEqual("H5", Room.At(lucy.transform.position + Vector3.up * 0.1f).id);
            // An empty bar: the gurney does not rise.
            tank.Teleport(gurney.transform.position + gurney.transform.right * 0.9f, 270f);
            yield return null;
            Assert.AreEqual(InteractKind.Message, gurney.KindFor(hand), "an empty mk_orzabal bar: no ride");
            Assert.IsFalse(gurney.Mount());
            stats.FillMk(PowerKind.Telekinesis);
            Assert.AreEqual(InteractKind.Ask, gurney.KindFor(hand));
            Assert.AreEqual("gurney:on", gurney.Confirm(hand));
            Assert.AreEqual(GurneyRide.Cost, gurney.LastCost, 0.5f, "a ride costs 40 MK (PROPOSED: a full bar crosses twice)");
            Assert.IsTrue(tank.MovementLocked, "her feet stop: the walk keys steer the gurney");
            // The real keys: forward until she is on the landing.
            var pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            var driver = new GameObject("[Pad]").AddComponent<PadDriver>();
            driver.pad = pad;
            pad.SetHeld(InputManager.GameAction.Forward, true);
            float t0 = Time.time;
            bool gapRefused = false;
            float lowest = 9f;
            while (Time.time - t0 < 10f && gurney.transform.position.x > -3.6f)
            {
                yield return null;
                lowest = Mathf.Min(lowest, lucy.transform.position.y);
                if (!gapRefused && Mathf.Abs(gurney.transform.position.x - 1f) < 0.3f)
                {
                    gapRefused = gurney.KindFor(hand) == InteractKind.Message && !gurney.StepOff();
                }
            }
            pad.SetHeld(InputManager.GameAction.Forward, false);
            GameInput.ClearBotSource(pad);
            UnityEngine.Object.Destroy(driver.gameObject);
            float crossing = Time.time - t0;
            Assert.IsTrue(gapRefused, "over the gap it does not land");
            Assert.AreEqual("H6", RoomTracker.CurrentId, "she rides into the landing");
            Assert.Greater(lowest, 0.5f, "she stays on the deck all the way");
            Assert.AreEqual("gurney:off", gurney.Confirm(hand), "over the landing she steps off");
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[HoldTests] the ride: {gurney.Travelled:0.0} m in {crossing:0.0} s, cost {gurney.LastCost:0} MK, bar left {stats.Mk(PowerKind.Telekinesis):0}, lowest foot {lowest:0.00} m, falls before {pit.Returns}");
            Assert.AreEqual(0f, lucy.transform.position.y, 0.15f, "she stands on the floor of the landing");
            Assert.IsFalse(tank.MovementLocked, "she walks");
            Assert.AreEqual(1, pit.Returns, "no fall on the ride");
        }

        [UnityTest]
        public IEnumerator Extend_BringsTheTubeOut_AndSheWalksBackOverTheGap()
        {
            yield return Freed();
            GameObject lucy = Her();
            TankController tank = lucy.GetComponent<TankController>();
            PitReturn pit = GameObject.Find("Pit_Return").GetComponent<PitReturn>(); // the gap of the tube (G16 has one too)
            Assert.IsNull(GameObject.Find("Tube_Floor"), "the tube is pulled back: no floor over the gap");
            tank.Teleport(new Vector3(-2.9f, 0f, 33.6f), 90f);
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
            lucy.GetComponent<ItemInventory>().TryAdd(ItemDefinition.Find("airlock_card"));
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
            lucy.GetComponent<ItemInventory>().TryAdd(ItemDefinition.Find("airlock_card"));
            lucy.GetComponent<TankController>().Teleport(new Vector3(7.5f, 0f, 37f), 270f);
            var heard = new System.Collections.Generic.HashSet<string>();
            yield return WaitUntil(() => { Listen(heard); return StoryFlags.Has(DemoFlags.VoiceFed); }, 25f);
            Debug.Log("[HoldTests] the voice, fed: " + string.Join(" | ", heard));
            Assert.IsTrue(heard.Contains("KNIGHT: You've eaten. Good."), "the other line plays");
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
            StringAssert.Contains("YOU HAVE THE RIGHT TO ASK FOR A VOTE", l37.item.logText);
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
                yield return null;
                yield return null;
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
