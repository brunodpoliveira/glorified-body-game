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
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The demo in greybox (2026-10-31, Bruno: "create the floor 2 and 1 of the demo in greybox"), one world scene since
    /// 2026-11-02 (Bruno: "i want all the map to be built and loaded up in one go"): the world loads once with every room,
    /// the cameras frame every floor point, the rooms behind the doorways exist, her room changes at a doorway with no
    /// scene load, the far rooms are not drawn, she walks down the stair to floor 1, a RETRY puts her at the doorway of
    /// her room; the story starts in W1; the end beat in U1 shows the bulletin and goes back to the menu.
    /// The world: DemoWorld.BuildAll. Batch: tools/unity.sh test playmode DemoTests.
    /// </summary>
    public class DemoTests
    {
        private const string World = "Demo";
        private static readonly string[] Rooms = { "W1", "W1B", "W2", "W3", "W4", "W5", "W6", "W7", "W8", "W9", "W10", "W11", "W12", "W13", "W14", "W15", "W16", "W17", "S1",
            "G1", "G2", "G3", "G4", "G5", "G6", "G7", "G8", "G9", "G9b", "G10", "G11", "G12", "G13", "G14", "G15", "G16", "X1", "R1", "R2", "R3", "U1", "B1", "B2", "G18", "G19", "G17b", "G17" }; // the floorplan v2 (2026-11-05), the brain sector (2026-11-09), the Dining Hall and the halon room (2026-11-10)
        private static readonly string[] MainWay = { "W1", "W1B", "W2", "W5", "W7", "W8", "W9", "W11", "W9", "W12", "S1", "G1", "G2", "G5", "G9", "G5", "U1" };

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
            // The W1 wake is a cutscene now (2026-11-02): a test that ends in it must not leave it to the next test.
            if (CutscenePlayer.IsPlaying) CutscenePlayer.Skip();
            yield return WaitUntil(() => !CutscenePlayer.IsPlaying, 5f);
            SaveSystem.Folder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheWorld_LoadsOnce_EveryRoom_ItsCamerasFrameItsFloor_AndItsNeighboursExist()
        {
            yield return LoadWorld();
            Assert.AreEqual(Rooms.Length, GlorifiedBody.Rooms.Room.All.Count, "every room of both floors and the stair in one scene");
            Assert.AreEqual(1, GameObject.FindGameObjectsWithTag("Player").Length, "one Lucy");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<FixedCameraDirector>(FindObjectsSortMode.None).Length, "one camera director");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.type == LightType.Directional), "one sun");
            Assert.AreEqual(0, GlorifiedBody.Enemy.EnemySpawn.Missing, "every figure's prefab is there");
            var failures = new List<string>();
            foreach (string id in Rooms)
            {
                GlorifiedBody.Rooms.Room room = GlorifiedBody.Rooms.Room.Of(id);
                if (room == null) { failures.Add(id + ": missing"); continue; }
                if (room.GetComponentsInChildren<FixedCamera>().Length == 0) failures.Add(id + ": no camera");
                foreach (string n in room.neighbours)
                {
                    GlorifiedBody.Rooms.Room other = GlorifiedBody.Rooms.Room.Of(n);
                    if (other == null) failures.Add($"{id}: a doorway to {n}, a room that does not exist");
                    else if (!other.IsNeighbour(id)) failures.Add($"{id}: {n} does not list it back");
                }
            }
            FixedCameraDirector director = UnityEngine.Object.FindFirstObjectByType<FixedCameraDirector>();
            CameraCoverage.Report report = CameraCoverage.Check(director, CameraCoverage.GameRoomNear, false);
            Debug.Log($"[DemoTests] the world: {report.Uncovered.Count} of {report.Total} floor points out of frame");
            if (report.Uncovered.Count > 0) failures.Add(report.ToString()); // no blind spot (Bruno, 2026-10-31)
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator TheMainWay_FromW1_ToU1_ChangesHerRoom_WithNoSceneLoad_AndCullsTheFarRooms()
        {
            yield return LoadWorld();
            int loads = 0;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> count = (scene, mode) => loads++;
            SceneManager.sceneLoaded += count;
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            var tank = lucy.GetComponent<TankController>();
            try
            {
                foreach (string id in MainWay)
                {
                    GlorifiedBody.Rooms.Room room = GlorifiedBody.Rooms.Room.Of(id);
                    tank.Teleport(FloorPoint(room), 0f);
                    yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == id, 3f);
                    Assert.AreEqual(id, GlorifiedBody.Rooms.RoomTracker.CurrentId, "her room");
                    Assert.IsTrue(GlorifiedBody.Rooms.RoomTracker.WasIn(id));
                    yield return null; // the camera cut of the new room
                    yield return null;
                    // Drawn: her room, and the rooms the camera sees (2026-11-07, RoomSight).
                    FixedCamera cam = FixedCameraDirector.Instance.Current;
                    foreach (GlorifiedBody.Rooms.Room other in GlorifiedBody.Rooms.Room.All)
                    {
                        Renderer floor = other.transform.Find("[Room]")?.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name.EndsWith("Floor"));
                        if (floor == null) continue;
                        bool drawn = other == room || (cam != null && cam.sees.Length > 0 ? cam.sees.Contains(other.id) : room.IsNeighbour(other.id));
                        Assert.AreEqual(drawn, floor.enabled, $"in {id}: the floor of {other.id} is {(drawn ? "drawn" : "culled")}");
                    }
                }
            }
            finally { SceneManager.sceneLoaded -= count; }
            Assert.AreEqual(0, loads, "no scene loads between the rooms");
            Assert.AreEqual(World, SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator TheFireDoors_StandAtTheChokepoints_TheirLeversInTheRooms_TheirLeavesInTheWall()
        {
            // Bruno, 2026-11-03: fire doors "in selected chokepoints"; his pick: W2 to W5, W5 to W7, G2 to G5.
            yield return LoadWorld();
            var names = GlorifiedBody.Interaction.FireDoor.All.Select(d => d.name).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "FireDoor_G17b_G17", "FireDoor_G2_G5", "FireDoor_G9b_G19", "FireDoor_W1B_W2", "FireDoor_W2_W5", "FireDoor_W5_W7", "FireDoor_W5_W9" }, names, "the fire doors (W5 to W9: the route switch, §10.5; G19 to G9b: the service exit; G17: the halon door, §10.4)");
            var failures = new List<string>();
            int solid = LayerMask.GetMask("Default");
            foreach (GlorifiedBody.Interaction.FireDoor door in GlorifiedBody.Interaction.FireDoor.All)
            {
                // The door of W1B to W2 starts shut (Bruno, 2026-11-05). The others stand open.
                if (door.name == "FireDoor_W1B_W2" || door.name == "FireDoor_G9b_G19") door.Open("test", instant: true); // shut at the start: the W1B door, the service exit
                if (door.Current != GlorifiedBody.Interaction.FireDoor.State.Open) failures.Add(door.name + ": not open at the start");
                var rooms = new HashSet<string>();
                foreach (GlorifiedBody.Interaction.DoorLever lever in door.GetComponentsInChildren<GlorifiedBody.Interaction.DoorLever>())
                {
                    Vector3 front = lever.transform.position + lever.transform.forward * 0.3f;
                    GlorifiedBody.Rooms.Room room = GlorifiedBody.Rooms.Room.At(new Vector3(front.x, door.transform.position.y + 0.1f, front.z));
                    if (room == null) failures.Add($"{door.name}/{lever.name}: in no room");
                    else rooms.Add(room.id);
                    if (Physics.Linecast(front, lever.transform.position + lever.transform.forward * 0.05f, solid, QueryTriggerInteraction.Ignore)) failures.Add($"{door.name}/{lever.name}: buried");
                }
                int sides = door.name == "FireDoor_G17b_G17" ? 0 : door.name == "FireDoor_W5_W9" || door.name == "FireDoor_G9b_G19" ? 1 : 2; // one lever: the route switch (W9 side), the service exit (G19 side); none: the halon door
                if (rooms.Count != sides) failures.Add($"{door.name}: its levers in {rooms.Count} rooms ({string.Join(",", rooms)})");
                // The open leaf lies in its wall pocket: a line to it from each side meets the wall.
                Vector3 leafCentre = door.leaf.position + Vector3.up * 1.1f;
                foreach (float side in new[] { -1f, 1f })
                    if (!Physics.Linecast(leafCentre + door.outward * side * 1f, leafCentre, solid, QueryTriggerInteraction.Ignore))
                        failures.Add($"{door.name}: the open leaf shows on the {(side > 0 ? "outer" : "inner")} side");
            }
            Assert.IsEmpty(failures, string.Join("; ", failures));
        }

        // ── The halon room (2026-11-10: DEMO_PLAN §10.4, CANON round 39; Bruno: "create the halon room now") ──

        [UnityTest]
        public IEnumerator TheHalonRoom_G17_OffFacilities_TheWallHidesTheCage_TheLogsInTheirRooms()
        {
            yield return LoadWorld();
            GlorifiedBody.Rooms.Room g17 = GlorifiedBody.Rooms.Room.Of("G17"), g17b = GlorifiedBody.Rooms.Room.Of("G17b");
            Assert.IsNotNull(g17, "G17, the server room");
            Assert.IsNotNull(g17b, "G17b, the room beside it, with the lockout cabinet");
            Assert.IsTrue(GlorifiedBody.Rooms.Room.Of("G19").exits.Any(e => e.to == "G17b"), "off the kitchen");
            Assert.IsTrue(g17b.exits.Any(e => e.to == "G17"));

            HalonSystem halon = UnityEngine.Object.FindAnyObjectByType<HalonSystem>();
            Assert.AreEqual(HalonSystem.State.Idle, halon.Current);
            Assert.IsTrue(halon.wall.IsHeavy && !halon.wall.IsProp, "a heavy wall: no lift, and the lock takes it");
            Assert.AreEqual("ARMED", halon.panel.text);
            Assert.IsFalse(halon.cage.activeInHierarchy, "the cage hides behind the boxes");
            Assert.AreEqual(0, halon.door.GetComponentsInChildren<DoorLever>().Length, "no lever: the horn seals it");

            var pickups = UnityEngine.Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            string RoomOfItem(string id) => pickups.FirstOrDefault(p => p.item != null && p.item.itemId == id)?.GetComponentInParent<GlorifiedBody.Rooms.Room>()?.id ?? "none";
            Assert.AreEqual("G17b", RoomOfItem("l15_facilities_memo"));
            Assert.AreEqual("G12", RoomOfItem("l16_counsel_note"));
            Assert.AreEqual("G17", RoomOfItem("l17_health_note"));
            Assert.AreEqual("G17", RoomOfItem("l18_stock_ledger"));
            Assert.AreEqual(7, halon.cage.GetComponentsInChildren<ItemPickup>(true).Count(p => p.item != null && p.item.kind != ItemKind.Log), "two of each power, one second wind (the count of L18)");
        }

        [UnityTest]
        public IEnumerator TheHalon_LockoutFirst_ThePartCircleOnlyFlashes_TheFullOneBurnsTheBoxes_NoHorn()
        {
            (HalonSystem halon, PsychicPowers powers) = default((HalonSystem, PsychicPowers));
            yield return InG17(r => (halon, powers) = r);
            StoryFlags.Raise("halon_lockout");
            powers.FireAt(powers.Selected, 0.5f, halon.wall);
            yield return null;
            Assert.AreEqual(HalonSystem.State.Idle, halon.Current, "a part circle only flashes (the rule of mk_erebus)");
            powers.FireAt(powers.Selected, 1f, halon.wall);
            yield return null;
            Assert.AreEqual(HalonSystem.State.Burning, halon.Current, "a full circle lights the boxes");
            Assert.IsFalse(halon.HornOn, "the lockout: no horn");
            Assert.AreEqual(FireDoor.State.Open, halon.door.Current, "the door stays open");
            Assert.IsTrue(halon.flames.activeInHierarchy, "the fire");
            yield return WaitUntil(() => halon.Current == HalonSystem.State.Burned, HalonSystem.BurnSeconds + 3f);
            Assert.IsFalse(halon.boxes.activeInHierarchy, "the boxes are gone");
            Assert.IsTrue(halon.cage.activeInHierarchy, "the cage shows");
            Assert.IsTrue(StoryFlags.Has("demo_g17_burned"));
        }

        [UnityTest]
        public IEnumerator TheHalon_TheHornSealsTheDoor_AndAbortOpensIt()
        {
            (HalonSystem halon, PsychicPowers powers) = default((HalonSystem, PsychicPowers));
            yield return InG17(r => (halon, powers) = r);
            powers.FireAt(powers.Selected, 1f, halon.wall);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(HalonSystem.State.Horn, halon.Current);
            Assert.IsTrue(halon.HornOn, "the horn");
            Assert.AreEqual(FireDoor.State.Jammed, halon.door.Current, "the door seals");
            Assert.Greater(halon.SecondsLeft, 27f, "30 s");
            StringAssert.StartsWith("DISCHARGE IN", halon.panel.text);
            HalonAbort abort = UnityEngine.Object.FindAnyObjectByType<HalonAbort>();
            Assert.AreEqual("halon_abort", abort.Confirm(GameObject.FindGameObjectWithTag("Player").GetComponent<Interactor>()));
            Assert.IsFalse(halon.HornOn, "ABORT stops it");
            Assert.AreEqual(FireDoor.State.Open, halon.door.Current, "the door opens");
            yield return WaitUntil(() => halon.Current == HalonSystem.State.Burned, HalonSystem.BurnSeconds + 3f);
            Assert.IsTrue(halon.cage.activeInHierarchy, "the boxes burn, the cage shows");
        }

        [UnityTest]
        public IEnumerator TheHalon_NoAbort_TheHalonComes_AndSheGoesUnder()
        {
            (HalonSystem halon, PsychicPowers powers) = default((HalonSystem, PsychicPowers));
            yield return InG17(r => (halon, powers) = r);
            Health health = powers.GetComponent<Health>();
            health.SetInvulnerable(false);
            health.SetRuntimeInvulnerable(false);
            powers.FireAt(powers.Selected, 1f, halon.wall);
            yield return WaitUntil(() => halon.Current == HalonSystem.State.Discharged, HalonSystem.DischargeSeconds + 3f);
            Assert.AreEqual(HalonSystem.State.Discharged, halon.Current, "30 s, then the halon");
            Assert.IsFalse(halon.flames.activeInHierarchy, "the fire is out");
            yield return WaitUntil(() => health.IsDead, HalonSystem.PassOutSeconds + 3f);
            Assert.IsTrue(health.IsDead, "she passes out: game over (an exception to rule 2)");
            Assert.AreEqual(HalonSystem.KillerId, GlorifiedBody.UI.DeathMenuUI.LastKillerId, "the death screen of the halon");
        }

        /// <summary>The demo after the wake, she in G17 facing the box wall, mk_erebus selected and full.</summary>
        private static IEnumerator InG17(Action<(HalonSystem, PsychicPowers)> found)
        {
            yield return Coda("G17");
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            HalonSystem halon = UnityEngine.Object.FindAnyObjectByType<HalonSystem>();
            Vector3 wall = halon.wall.transform.position;
            lucy.GetComponent<TankController>().Teleport(new Vector3(wall.x, GlorifiedBody.Rooms.Room.Of("G17").BaseY, wall.z + 3f), 180f);
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "G17", 3f);
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            powers.Select(PowerKind.Fire);
            lucy.GetComponent<PsychicStats>().FillMk(PowerKind.Fire);
            found((halon, powers));
        }

        // ── The Dining Hall (2026-11-10: beat 23b, CANON round 60; the second fireteam, round 62) ──

        [UnityTest]
        public IEnumerator TheDiningHall_G18AndItsKitchen_TheDutyKey_TheLogs_TheServiceExit()
        {
            yield return LoadWorld();
            GlorifiedBody.Rooms.Room g2 = GlorifiedBody.Rooms.Room.Of("G2"), g18 = GlorifiedBody.Rooms.Room.Of("G18"), g19 = GlorifiedBody.Rooms.Room.Of("G19");
            Assert.IsNotNull(g18, "G18, the Dining Hall");
            Assert.IsNotNull(g19, "G19, its kitchen");
            Assert.IsTrue(g2.exits.Any(e => e.to == "G18"), "the glass doors from the concourse");
            Assert.IsTrue(g18.exits.Any(e => e.to == "G19"), "the hall to the kitchen");
            Assert.IsTrue(g19.exits.Any(e => e.to == "G9b"), "the service exit (into the closet of G9: G9 has no free wall)");

            var pickups = UnityEngine.Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            string RoomOfItem(string id) => pickups.FirstOrDefault(p => p.item != null && p.item.itemId == id)?.GetComponentInParent<GlorifiedBody.Rooms.Room>()?.id ?? "none";
            Assert.AreEqual("G19", RoomOfItem("duty_key"), "the duty key on its hook in the kitchen office");
            Assert.AreEqual("G9", RoomOfItem("l30_release_card"));
            Assert.AreEqual("G18", RoomOfItem("l31_reservation_book"));
            Assert.AreEqual("G18", RoomOfItem("l32_comment_card"));
            Assert.AreEqual("G19", RoomOfItem("l33_chef_note"));
            Assert.AreEqual("G19", RoomOfItem("l34_key_log"));

            // The service exit: a push bar on the kitchen side only, and no kick breaks it.
            var exit = GlorifiedBody.Interaction.FireDoor.All.First(d => d.name == "FireDoor_G9b_G19");
            Assert.AreEqual(GlorifiedBody.Interaction.FireDoor.State.Jammed, exit.Current, "shut at the start");
            Assert.IsFalse(exit.canBreak, "no kick breaks it");
            var levers = exit.GetComponentsInChildren<GlorifiedBody.Interaction.DoorLever>();
            Assert.AreEqual(1, levers.Length);
            Vector3 front = levers[0].transform.position + levers[0].transform.forward * 0.3f;
            Assert.AreEqual("G19", GlorifiedBody.Rooms.Room.At(new Vector3(front.x, g19.BaseY + 0.1f, front.z))?.id, "the bar is on the kitchen side");
            Interactor hands = GameObject.FindGameObjectWithTag("Player").GetComponent<Interactor>();
            Assert.AreEqual("opened:FireDoor_G9b_G19", levers[0].Confirm(hands));
        }

        [UnityTest]
        public IEnumerator TheForwardPost_HoldsTheHall_UntilSheTakesTheKey_AndItsTrackPlaysWhileTheySeeHer()
        {
            yield return Coda("G18");
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            GlorifiedBody.Rooms.Room g18 = GlorifiedBody.Rooms.Room.Of("G18");
            SoldierBrain[] team = GameObject.Find("Hall_Team").GetComponentsInChildren<SoldierBrain>();
            Assert.AreEqual(4, team.Length, "the second fireteam of four (round 62)");
            Assert.IsTrue(team.Any(b => b.name == "Ortiz") && team.Any(b => b.name == "Brandt"), "Ortiz and Brandt");
            Assert.IsTrue(team.All(b => b.Leashed && b.LeashRoom == g18), "they hold the hall");

            GlorifiedBody.Audio.RoomScore score = GameObject.Find("Score_G18").GetComponent<GlorifiedBody.Audio.RoomScore>();
            lucy.GetComponent<TankController>().Teleport(g18.transform.position + new Vector3(36.3f, 0f, 3f), 90f);
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "G18", 3f);
            team[0].Alert("test", callOthers: true);
            yield return WaitUntil(() => score.Playing, 3f);
            Assert.IsTrue(score.Playing, "its own track when they see her (§12, round 103)");
            Assert.IsTrue(GlorifiedBody.Audio.MusicDirector.Instance.InFight);

            // She runs back to the concourse: they stay in the hall, and the track stops.
            lucy.GetComponent<TankController>().Teleport(g18.transform.position + new Vector3(30f, 0f, 3f), 270f);
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "G2", 3f);
            yield return new WaitForSeconds(8f);
            foreach (SoldierBrain b in team)
                Assert.IsTrue(g18.Contains(b.transform.position, 0.3f), $"{b.name} holds the hall: {b.transform.position}");
            Assert.IsFalse(score.Playing, "she left: the room's music again");

            StoryFlags.Raise("demo_duty_key_taken");
            Assert.IsTrue(team.All(b => !b.Leashed), "after the key, they follow her (round 62)");
        }

        // ── The night of 30 December (2026-11-10: beat 19b, L24 to L29, N1, N2, the title card) ──

        [UnityTest]
        public IEnumerator TheOpening_ShowsTheCardRaven_First()
        {
            // DEMO_PLAN §14 item 14 (rounds 104 and 105): "RAVEN, before cutscene 01, the first thing on screen".
            var wake = Resources.Load<CutsceneAsset>("Cutscenes/cutscene_01_the_wake");
            Assert.IsNotNull(wake);
            Assert.AreEqual("cs00_raven", wake.shots[0].still != null ? wake.shots[0].still.name : "", "the first shot is the card");
            Assert.Greater(wake.shots.Length, 1, "then the wake");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheCafeteria_G10_TheTreeBlinks_TheRadioPlays_AndTheScanOfTheTreePlaysTheParty()
        {
            yield return Coda("G10");
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            Transform tree = GameObject.Find("Desktop_Tree").transform;
            lucy.GetComponent<TankController>().Teleport(new Vector3(tree.position.x, tree.position.y - 0.91f, tree.position.z + 1.0f), 180f);
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "G10", 3f);

            // Two props are named "Radio" (G10, W13): the one in G10 plays.
            Broadcast radio = UnityEngine.Object.FindObjectsByType<Broadcast>(FindObjectsSortMode.None).First(b => b.roomId == "G10");
            yield return WaitUntil(() => radio.Spoken > 0, 3f);
            Assert.IsTrue(radio.Started, "the radio plays when she comes in (N1a)");
            StringAssert.StartsWith("...my door is open", radio.LastText, "mid-sentence");

            GlorifiedBody.Rendering.Blink blink = GameObject.Find("Tree_Lights").GetComponent<GlorifiedBody.Rendering.Blink>();
            int first = blink.Phase;
            yield return new WaitForSeconds(blink.seconds + 0.1f);
            Assert.AreNotEqual(first, blink.Phase, "the lights blink");

            var logs = UnityEngine.Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (string id in new[] { "l25_winter_schedule", "l26_gene_birthday" })
                Assert.IsTrue(logs.Any(p => p.item != null && p.item.itemId == id && p.GetComponentInParent<GlorifiedBody.Rooms.Room>()?.id == "G10"), id + " in G10");

            PsychicScanner scanner = lucy.GetComponent<PsychicScanner>();
            Assert.AreEqual("Scan_Desktop_Tree", scanner.FindFocus()?.name, "the tree is in her scan");
            Assert.IsTrue(scanner.TryScan(), scanner.LastResult);
            yield return null;
            Assert.AreEqual("07c_the_party", CutscenePlayer.Current, "the one scan that plays a cutscene (C10)");
            Assert.IsTrue(StoryFlags.Has("demo_c10_scanned"));
            CutscenePlayer.Skip();
            yield return WaitUntil(() => !CutscenePlayer.IsPlaying, 5f);
        }

        [UnityTest]
        public IEnumerator TheLogsOfTheSeason_L24ToL29_StandInTheirRooms()
        {
            yield return LoadWorld();
            var logs = UnityEngine.Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var expected = new (string id, string room)[]
            {
                ("l24_colton_thread", "W9"), ("l25_winter_schedule", "G10"), ("l26_gene_birthday", "G10"),
                ("l27_the_25th", "G13"), ("l28_law_flyer", "G11"), ("l29_holiday_roster", "G9"),
            };
            var failures = new List<string>();
            foreach ((string id, string room) in expected)
            {
                ItemPickup log = logs.FirstOrDefault(p => p.item != null && p.item.itemId == id);
                if (log == null) { failures.Add(id + ": none"); continue; }
                string at = log.GetComponentInParent<GlorifiedBody.Rooms.Room>()?.id;
                if (at != room) failures.Add($"{id}: in {at}, not {room}");
                if (log.item.kind != ItemKind.Log) failures.Add(id + ": not a log");
                // She can reach it: a floor point of its room within its range, seen from above.
                if (!UnityEngine.AI.NavMesh.SamplePosition(new Vector3(log.transform.position.x, GlorifiedBody.Rooms.Room.Of(room).BaseY, log.transform.position.z), out _, log.range + 0.3f, UnityEngine.AI.NavMesh.AllAreas))
                    failures.Add(id + ": out of her reach");
            }
            Assert.IsEmpty(failures, string.Join("; ", failures));
        }

        [UnityTest]
        public IEnumerator TheW14Tv_PlaysTheEndOfTheEpisode_WhenSheWalksIn_AndHerTextIsOnIt()
        {
            yield return Coda("W14");
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            InWorldScreen tv = UnityEngine.Object.FindObjectsByType<InWorldScreen>(FindObjectsSortMode.None).First(s => s.GetComponentInParent<GlorifiedBody.Rooms.Room>()?.id == "W14");
            Assert.IsFalse(tv.IsPlaying, "off before she comes");
            Assert.AreEqual("CABAL", tv.lines[0].speaker);
            StringAssert.Contains("THE TECHNOPURITAN CHURCH", tv.lines[tv.lines.Count - 1].text, "the copyright, last (round 109)");
            Vector3 trigger = GameObject.Find("Trigger_W14_TV")?.transform.position ?? GameObject.Find("W14_TV").transform.position;
            lucy.GetComponent<TankController>().Teleport(new Vector3(trigger.x, 0f, trigger.z), 270f);
            yield return WaitUntil(() => tv.IsPlaying, 3f);
            Assert.IsTrue(tv.IsPlaying, "it plays when she walks in (round 108)");
            Examinable look = GameObject.Find("TV_Table").GetComponent<Examinable>();
            StringAssert.StartsWith("Aw, man. I tuned in right at the end!", look.comment, "her text on the TV");
        }

        [UnityTest]
        public IEnumerator TheRouteSwitch_CodeSilverShutsTheW5ToW9Door_ItsLeverInW9OpensIt_ALoadKeepsItOpen()
        {
            // DEMO_PLAN §10.5, row 3 (CANON, round 43): "The W5 to W9 fire door opens from the W9 side: the ward becomes a ring".
            yield return LoadWorld();
            var door = GlorifiedBody.Interaction.FireDoor.All.First(d => d.name == "FireDoor_W5_W9");
            Assert.AreEqual(GlorifiedBody.Interaction.FireDoor.State.Open, door.Current, "open before code silver");
            var levers = door.GetComponentsInChildren<GlorifiedBody.Interaction.DoorLever>();
            Assert.AreEqual(1, levers.Length, "one lever");
            Vector3 front = levers[0].transform.position + levers[0].transform.forward * 0.3f;
            Assert.AreEqual("W9", GlorifiedBody.Rooms.Room.At(new Vector3(front.x, 0.1f, front.z))?.id, "on the W9 side");

            StoryFlags.Raise(DemoFlags.CodeSilver);
            yield return null;
            Assert.AreEqual(GlorifiedBody.Interaction.FireDoor.State.Jammed, door.Current, "code silver shuts it");
            Interactor hands = GameObject.FindGameObjectWithTag("Player").GetComponent<Interactor>();
            Assert.AreEqual("opened:FireDoor_W5_W9", levers[0].Confirm(hands), "her lever in W9 opens it");
            Assert.AreEqual(GlorifiedBody.Interaction.FireDoor.State.Open, door.Current);
            StoryFlags.Set(StoryFlags.All.ToList()); // a load sets every flag again
            yield return null;
            Assert.AreEqual(GlorifiedBody.Interaction.FireDoor.State.Open, door.Current, "a load does not shut it again");
        }

        [UnityTest]
        public IEnumerator EveryCrt_StandsBehindItsFace()
        {
            // 2026-11-03: the CRT boxes stood at twice their place (a local position under a parent at the floor point).
            yield return LoadWorld();
            var failures = new List<string>();
            int count = 0;
            foreach (GlorifiedBody.Interaction.InWorldScreen screen in UnityEngine.Object.FindObjectsByType<GlorifiedBody.Interaction.InWorldScreen>(FindObjectsSortMode.None))
                foreach (Transform face in screen.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("CrtFace_")))
                {
                    Transform box = screen.transform.Find(face.name.Replace("CrtFace_", "Crt_"));
                    if (box == null) continue;
                    count++;
                    float gap = Vector3.Distance(box.position, face.position);
                    if (gap > 0.5f) failures.Add($"{screen.name}/{box.name}: {gap:0.0} m from its face");
                }
            Assert.Greater(count, 0, "the demo has CRTs");
            Assert.IsEmpty(failures, string.Join("; ", failures));
        }

        [UnityTest]
        public IEnumerator TheStair_SheWalksDown_FromW12_ToG1_OnFloorOne()
        {
            // Bruno, 2026-11-02: "a real stair room" between the floors.
            yield return LoadWorld();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake"), 120f); // the wake holds her feet while Stevens talks
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            GlorifiedBody.Interaction.LockedDoor door = GlorifiedBody.Rooms.Room.Of("W12").GetComponentInChildren<GlorifiedBody.Interaction.LockedDoor>();
            Assert.IsNotNull(door, "the stair door is locked (the ward sister's card)");
            // 2026-11-03: the leaf stood at twice its doorway's place (a local position under a parent at the doorway).
            Assert.IsTrue(Physics.Linecast(new Vector3(15f, 1.2f, -12.5f), new Vector3(15f, 1.2f, -15.5f), out RaycastHit leafHit, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore)
                && leafHit.collider.transform.IsChildOf(door.transform), "the card door's leaf stands in W12's south doorway");
            door.Open();
            yield return new WaitForSeconds(1.5f); // the leaf slides away
            lucy.GetComponent<TankController>().Teleport(new Vector3(15f, 0f, -12f), 180f);
            yield return new WaitForSeconds(0.3f);
            var pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            var driver = new GameObject("[Pad]").AddComponent<PadDriver>();
            UnityEngine.Object.DontDestroyOnLoad(driver.gameObject);
            driver.pad = pad;
            pad.SetHeld(InputManager.GameAction.Forward, true);
            var seen = new List<string>();
            float t0 = Time.realtimeSinceStartup;
            while (GlorifiedBody.Rooms.RoomTracker.CurrentId != "G1" && Time.realtimeSinceStartup - t0 < 20f)
            {
                string here = GlorifiedBody.Rooms.RoomTracker.CurrentId;
                if (seen.Count == 0 || seen[seen.Count - 1] != here) seen.Add(here);
                yield return null;
            }
            pad.SetHeld(InputManager.GameAction.Forward, false);
            GameInput.ClearBotSource(pad);
            UnityEngine.Object.Destroy(driver.gameObject);
            Debug.Log($"[DemoTests] the stair: {string.Join(" > ", seen)} > {GlorifiedBody.Rooms.RoomTracker.CurrentId}, y {lucy.transform.position.y:0.00}");
            Assert.AreEqual("G1", GlorifiedBody.Rooms.RoomTracker.CurrentId, "she walks down into G1: " + string.Join(" > ", seen));
            CollectionAssert.Contains(seen, "S1", "through the stair room");
            Assert.AreEqual(-4f, lucy.transform.position.y, 0.2f, "on floor 1");
        }

        [UnityTest]
        public IEnumerator ARetry_PutsHerAtTheDoorway_OfHerRoom_AsSheCameIn()
        {
            // The world scene has no room scene to reload: the retry point moves when she walks into a room.
            yield return LoadWorld();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake"), 120f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            var tank = lucy.GetComponent<TankController>();
            tank.Teleport(new Vector3(13f, 0f, 12.5f), 0f); // W2, under W3's door
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "W2", 3f);
            tank.Teleport(new Vector3(13f, 0f, 14.6f), 0f); // into W3
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "W3", 3f);
            Vector3 cameIn = lucy.transform.position;
            float shortAtDoor = lucy.GetComponent<PsychicStats>().Short;
            lucy.GetComponent<PsychicStats>().AddShort(-30f); // after the doorway: a RETRY undoes it
            tank.Teleport(new Vector3(16f, 0f, 16f), 90f); // she walks on in the room
            yield return null;
            int arrivals = SaveSystem.Arrivals;
            GameManager.Instance.RestartLevel();
            yield return WaitUntil(() => GameManager.Instance.IsPlaying && GlorifiedBody.Rooms.RoomTracker.CurrentId == "W3", 15f);
            yield return new WaitForSeconds(0.3f);
            lucy = GameObject.FindGameObjectWithTag("Player");
            Assert.AreEqual(World, SceneManager.GetActiveScene().name);
            Assert.AreEqual("W3", GlorifiedBody.Rooms.RoomTracker.CurrentId, "back in her room");
            Assert.Less(Vector3.Distance(lucy.transform.position, cameIn), 0.5f, "at the doorway where she came in");
            Assert.AreEqual(shortAtDoor, lucy.GetComponent<PsychicStats>().Short, 1f, "her state as she came in");
            Assert.IsTrue(StoryFlags.Has("demo_w1_wake"), "the story as it was: the wake does not play again");
            Assert.IsTrue(GlorifiedBody.Rooms.RoomTracker.WasIn("W2"), "the map keeps the rooms she was in");
        }

        [UnityTest]
        public IEnumerator ADoorway_ChangesTheRoom_AsSheStepsThrough_InFrame_WithNoLoad()
        {
            // Bruno, 2026-11-02: "a huge gap between all rooms ... at least 3-4 steps blindly down the doorway before it
            // switches room". W4's door to W2: the south wall (z 14), at x 22 (W3's door is a card door since 2026-11-08).
            yield return LoadWorld();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake"), 120f);
            int loads = 0;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> count = (scene, mode) => loads++;
            SceneManager.sceneLoaded += count;
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponent<TankController>().Teleport(new Vector3(22f, 0f, 15.2f), 180f);
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "W4", 3f);
            var pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            var driver = new GameObject("[Pad]").AddComponent<PadDriver>();
            UnityEngine.Object.DontDestroyOnLoad(driver.gameObject);
            driver.pad = pad;
            pad.SetHeld(InputManager.GameAction.Forward, true);
            float t0 = Time.realtimeSinceStartup;
            Vector3 last = lucy.transform.position;
            bool seen = false;
            while (GlorifiedBody.Rooms.RoomTracker.CurrentId != "W2" && Time.realtimeSinceStartup - t0 < 8f)
            {
                yield return null;
                last = lucy.transform.position;
                Vector3 v = Camera.main.WorldToViewportPoint(last + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight);
                seen = v.z > 0f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;
            }
            pad.SetHeld(InputManager.GameAction.Forward, false);
            GameInput.ClearBotSource(pad);
            UnityEngine.Object.Destroy(driver.gameObject);
            SceneManager.sceneLoaded -= count;
            Assert.AreEqual("W2", GlorifiedBody.Rooms.RoomTracker.CurrentId, "she walks into W2");
            float past = 14f - last.z;
            Debug.Log($"[DemoTests] the room changes {past:0.00} m past the wall line, in frame: {seen}");
            Assert.LessOrEqual(past, 0.3f, "her room changes at the wall line");
            Assert.IsTrue(seen, "the camera sees her as her room changes");
            Assert.AreEqual(0, loads, "no load, no fade");
        }

        [UnityTest]
        public IEnumerator W1_StartsTheStory_TheFeedLocked_TheShortNearlyFull_StevensSpeaks()
        {
            yield return LoadWorld();
            // The wake is an FMV slot (Bruno, 2026-11-02): the game opens on it (2026-11-04).
            yield return WaitUntil(() => CutscenePlayer.IsPlaying && CutscenePlayer.ShotIndex >= 0, 10f);
            Assert.AreEqual("01_the_wake", CutscenePlayer.Current, "the wake plays as a cutscene");
            yield return null;
            Assert.IsTrue(Feeding.Locked, "round 18: the feed starts locked");
            PsychicStats stats = GameObject.FindGameObjectWithTag("Player").GetComponent<PsychicStats>();
            Assert.Greater(stats.Short, PsychicStats.MaxShort * 0.8f, "round 8: she starts nearly full on the SHORT");
            // The card RAVEN is the first shot (round 104), then the wake's lines.
            yield return WaitUntil(() => CutscenePlayer.ShotIndex >= 1 && GlorifiedBody.Subtitles.SubtitleService.Lines.Any(l => l.Speaker == "STEVENS"), 10f);
            Assert.IsTrue(GlorifiedBody.Subtitles.SubtitleService.Lines.Any(l => l.Speaker == "STEVENS"), "Stevens on the speaker (cutscene 01: the FMV slot shows his lines)");
            Assert.IsNotNull(GameObject.Find("L1_Printout"), "the printout on the printer");
        }

        [UnityTest]
        public IEnumerator U1_TheGrabTearsTheUniformOff_AndSheStandsInTheBrainSector()
        {
            yield return LoadWorld();
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            GlorifiedBody.Interaction.OutfitChange.Change(lucy, GlorifiedBody.Interaction.OutfitChange.Load(GlorifiedBody.Interaction.OutfitChange.GuardId));
            lucy.GetComponent<TankController>().Teleport(GlorifiedBody.Rooms.Room.Of("U1").transform.position + new Vector3(20f, 0f, -25.5f), 180f);
            // Beat 25 (the demo waits for it, round 52; built 2026-11-09): after the grab, the brain sector B1. The exit lines
            // and the car park come after its death (TheBrainSector_B1_...).
            yield return WaitUntil(() =>
            {
                if (CutscenePlayer.IsPlaying) CutscenePlayer.Skip();
                return GlorifiedBody.Rooms.RoomTracker.CurrentId == "B1";
            }, 60f);
            Assert.AreEqual("B1", GlorifiedBody.Rooms.RoomTracker.CurrentId, "after the grab: the brain sector");
            Assert.AreEqual(GlorifiedBody.Interaction.OutfitChange.NudeId, lucy.GetComponent<PsychicStats>().Outfit.outfitId, "round 20: the grab tears the uniform off");
            Assert.IsTrue(StoryFlags.Has("demo_crown_gone"), "the Crown is gone");
            yield return WaitUntil(() => GlorifiedBody.Boss.BrainFight.Instance != null && GlorifiedBody.Boss.BrainFight.Instance.Current == GlorifiedBody.Boss.BrainFight.Phase.Fighting, 3f);
            Assert.AreEqual(GlorifiedBody.Boss.BrainFight.Phase.Fighting, GlorifiedBody.Boss.BrainFight.Instance.Current, "the fight begins");
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(UnityEngine.Object.FindFirstObjectByType<GlorifiedBody.UI.PlayerHud>().HpShown, "the Crown's HP row is gone (the brain reads her: SUBJECT)");
            Assert.IsFalse(lucy.GetComponent<TankController>().MovementLocked, "she walks");
        }

        private static IEnumerator Coda(string roomId, params string[] flags)
        {
            yield return LoadWorld();
            yield return WaitUntil(() => CutscenePlayer.IsPlaying && CutscenePlayer.ShotIndex >= 0, 10f);
            CutscenePlayer.Skip();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake"), 10f);
            foreach (string flag in flags) StoryFlags.Raise(flag);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            GlorifiedBody.Rooms.Room room = GlorifiedBody.Rooms.Room.Of(roomId);
            Assert.IsNotNull(room, roomId);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheDarkRoom_G9_GoesDark_TheUnitsInOrder_BringTheMonitors_ThenTheLights_AndTheRelease()
        {
            // DEMO_PLAN §10.1 in the demo (2026-11-09; DARK TEST since 2026-11-08).
            yield return Coda("G9");
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            GlorifiedBody.Rooms.Room g9 = GlorifiedBody.Rooms.Room.Of("G9");
            GlorifiedBody.Rooms.DarkRoom dark = g9.GetComponent<GlorifiedBody.Rooms.DarkRoom>();
            UpsBank bank = UnityEngine.Object.FindFirstObjectByType<UpsBank>();
            FlagSwitch release = GameObject.Find("Release_Switch").GetComponent<FlagSwitch>();
            Assert.IsNotNull(dark, "G9 is a dark room (DemoDark)");
            Assert.IsFalse(dark.IsDark, "lit until she comes in");
            lucy.GetComponent<TankController>().Teleport(g9.Centre, 0f);
            yield return WaitUntil(() => dark.IsDark, 3f);
            Assert.IsTrue(dark.IsDark, "she enters G9: the lights go out");
            Assert.IsTrue(bank.Beeping, "the units beep");
            Assert.AreEqual(0, bank.MonitorsOn, "the monitors go black");
            Interactor her = lucy.GetComponent<Interactor>();
            StringAssert.StartsWith("waiting:", release.Confirm(her), "the release waits for the units");
            Assert.AreEqual(3, bank.units.Length);
            foreach (string label in new[] { "NET", "REC", "ACCESS" }) bank.Press(bank.units.First(u => u.label == label));
            Assert.IsTrue(bank.Done, "NET, REC, ACCESS");
            yield return WaitUntil(() => !dark.IsDark, bank.monitorGap * bank.monitors.Length + bank.lightsBackSeconds + 3f);
            Assert.AreEqual(bank.monitorLights.Length, bank.MonitorsOn, "every monitor light is on");
            Assert.IsFalse(dark.IsDark, "the monitors, then the lights");
            // The second key (beat 23, round 60): the duty key from the kitchen of the Dining Hall.
            Assert.AreEqual("DUTY KEY REQUIRED.", release.TextFor(her), "the console without the key");
            StringAssert.StartsWith("missing:", release.Confirm(her));
            her.Inventory.TryAdd(UnityEngine.Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(p => p.name == "Duty_Key").item);
            StringAssert.StartsWith("switch:", release.Confirm(her), "the release works");
            Assert.IsTrue(StoryFlags.Has(release.flag));
        }

        [UnityTest]
        public IEnumerator TheAudioLogs_L19InW9_L20InW11_AndTheEndOfL19UnlocksTheFeed()
        {
            // DEMO_PLAN §8 (rounds 44 and 49; 2026-11-09): two spoken logs, the writer's words, placeholder clips.
            yield return Coda("W9");
            ItemPickup l19 = GameObject.Find("L19_Dictaphone")?.GetComponent<ItemPickup>();
            ItemPickup l20 = GameObject.Find("L20_Recorder")?.GetComponent<ItemPickup>();
            Assert.IsNotNull(l19, "the dictaphone in W9");
            Assert.IsNotNull(l20, "the recorder in W11");
            Assert.AreEqual("W9", l19.GetComponentInParent<GlorifiedBody.Rooms.Room>().id);
            Assert.AreEqual("W11", l20.GetComponentInParent<GlorifiedBody.Rooms.Room>().id);
            foreach (ItemPickup p in new[] { l19, l20 })
            {
                Assert.AreEqual(ItemKind.AudioLog, p.item.kind, p.name);
                Assert.IsNotNull(p.item.audioClip, p.name + ": its clip (tools/audio_log_placeholders.py)");
                Assert.Greater(p.item.audioClip.length, p.item.tapeLines.Last().at, p.name + ": the last line inside the clip");
            }
            StringAssert.Contains("Never enter alone", string.Join(" ", l19.item.tapeLines.Select(l => l.text)));

            yield return WaitUntil(() =>
            {
                if (CutscenePlayer.IsPlaying) CutscenePlayer.Skip();
                return !CutscenePlayer.IsPlaying && Time.timeScale > 0f;
            }, 60f); // the tape runs on game time
            StoryFlags.Raise(Feeding.LockFlag);
            StoryFlags.Drop(Feeding.UnlockFlag);
            Assert.IsTrue(Feeding.Locked, "the feed is locked");
            var tapes = GlorifiedBody.Audio.TapePlayer.Instance;
            GameObject.FindGameObjectWithTag("Player").GetComponent<ItemInventory>().TryAdd(l19.item); // the player stops a tape she does not carry
            tapes.Play(l19.item);
            yield return null;
            tapes.Seek(l19.item.audioClip.length - 0.5f);
            yield return WaitUntil(() => tapes.StoppedBy == GlorifiedBody.Audio.TapePlayer.Ended, 3f);
            Assert.AreEqual(GlorifiedBody.Audio.TapePlayer.Ended, tapes.StoppedBy, "it plays to its end");
            Assert.IsFalse(Feeding.Locked, "at its end the feed unlocks (round 44: the backup)");
        }

        [UnityTest]
        public IEnumerator TheBrainSector_B1_ItsDeathOpensB2_AndB2CutsToTheCarPark()
        {
            yield return Coda("B1", "demo_crown_gone");
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            TankController tank = lucy.GetComponent<TankController>();
            tank.Teleport(GameObject.Find("Entry_B1").transform.position, 0f);
            var fight = GlorifiedBody.Boss.BrainFight.Instance;
            Assert.IsNotNull(fight, "the brain of B1 (DemoBrain)");
            yield return WaitUntil(() => fight.Current == GlorifiedBody.Boss.BrainFight.Phase.Fighting, 3f);
            Assert.AreEqual(GlorifiedBody.Boss.BrainFight.Phase.Fighting, fight.Current, "her first step into the sector");
            Assert.AreSame(fight.eye, FixedCameraDirector.Instance.Current, "the camera is the brain's eye");
            Assert.IsTrue(fight.exitLeaf.activeSelf, "the way out is shut while it lives");
            fight.trackSpeed = 0f;
            Health body = fight.nodes.Single().Health;
            body.TakeDamage(body.MaxHealth);
            yield return WaitUntil(() => fight.Current == GlorifiedBody.Boss.BrainFight.Phase.Dead, 8f);
            Assert.IsFalse(fight.exitLeaf.activeSelf, "its death opens the builders' door");
            Assert.IsTrue(StoryFlags.Has("demo_brain_dead"));
            GlorifiedBody.Rooms.Room b2 = GlorifiedBody.Rooms.Room.Of("B2");
            tank.Teleport(b2.Centre, 0f);
            yield return WaitUntil(() =>
            {
                if (CutscenePlayer.IsPlaying) CutscenePlayer.Skip();
                return GlorifiedBody.Rooms.RoomTracker.CurrentId == "X1";
            }, 90f);
            Assert.AreEqual("X1", GlorifiedBody.Rooms.RoomTracker.CurrentId, "the exit lines, then the car park");
        }

        [UnityTest]
        public IEnumerator TheCoda_EveryWayIsOpenOnTheFloor()
        {
            // The first layout started her inside a car: no test walked it.
            yield return Coda("X1");
            bool Way(Vector3 from, Vector3 to, string what)
            {
                Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(from, out UnityEngine.AI.NavMeshHit a, 0.6f, UnityEngine.AI.NavMesh.AllAreas), what + ": the start stands on the floor");
                Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(to, out UnityEngine.AI.NavMeshHit b, 1.2f, UnityEngine.AI.NavMesh.AllAreas), what + ": the end stands by the floor");
                var path = new UnityEngine.AI.NavMeshPath();
                return UnityEngine.AI.NavMesh.CalculatePath(a.position, b.position, UnityEngine.AI.NavMesh.AllAreas, path) && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
            }
            Vector3 x1 = GameObject.Find("Entry_X1").transform.position, r1 = GameObject.Find("Entry_R1").transform.position;
            Assert.IsTrue(Way(x1, GameObject.Find("Glove_Box").transform.position, "X1"), "the way to the glove box");
            Vector3 door = GameObject.Find("Car_Door").transform.position;
            Assert.IsTrue(Way(x1, new Vector3(door.x - 0.8f, x1.y, door.z), "X1"), "the way to where she stands at the door of the car");
            Vector3 toR2 = GlorifiedBody.Rooms.Room.Of("R2").Centre + Vector3.back * 2f;
            Assert.IsTrue(Way(r1, toR2, "R1"), "the way from the shop to the bay");
            Assert.IsFalse(Way(r1, GameObject.Find("Mechanic").transform.position, "R1"), "she cannot walk behind the rail");
            Assert.IsFalse(Way(r1, GameObject.Find("Guard_Booth").transform.position, "R1"), "nor behind the rail of the yard");
        }

        [UnityTest]
        public IEnumerator X1_TheDoorWaitsForTheSunglasses_ThenTheBulletin_ThenTheShop()
        {
            yield return Coda("X1", "demo_crown_gone");
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponent<TankController>().Teleport(GameObject.Find("Entry_X1").transform.position, 45f);
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "X1", 3f);
            FlagSwitch door = UnityEngine.Object.FindObjectsByType<FlagSwitch>(FindObjectsSortMode.None).First(f => f.name == "Car_Door");
            Assert.IsFalse(door.Available, "no sunglasses: the door waits");
            ItemPickup tin = UnityEngine.Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).First(p => p.name == "Tin_Unknown");
            Assert.AreEqual("UNKNOWN CAPSULE", tin.item.ShownName);
            ItemInventory items = lucy.GetComponent<ItemInventory>();
            items.TryAdd(UnityEngine.Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).First(p => p.name == "Sunglasses").item);
            yield return WaitUntil(() => StoryFlags.Has("demo_readout_on"), 3f);
            yield return null;
            Assert.IsTrue(GameObject.Find("PlayerHudCanvas").GetComponent<Canvas>().enabled, "the HUD comes back with the sunglasses");
            Assert.IsTrue(door.Available, "the door opens now");
            StoryFlags.Raise("demo_car_door");
            yield return WaitUntil(() => Bulletin.IsShowing, 15f);
            Assert.IsTrue(Bulletin.IsShowing, "the bulletin");
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "R1", 25f);
            Assert.AreEqual("R1", GlorifiedBody.Rooms.RoomTracker.CurrentId, "the service station");
        }

        [UnityTest]
        public IEnumerator TheCodeRotation_InTheWard_W3AndW10_ThenThePa_ThenW13Enrols_AndW12Opens()
        {
            yield return LoadWorld();
            // The W1 wake stops time, and the PA waits on game time: skip each of its cutscenes until the world runs.
            yield return WaitUntil(() =>
            {
                if (CutscenePlayer.IsPlaying) CutscenePlayer.Skip();
                return StoryFlags.Has("demo_w1_wake") && !CutscenePlayer.IsPlaying && Time.timeScale > 0f;
            }, 120f);
            Interactor her = GameObject.FindGameObjectWithTag("Player").GetComponent<Interactor>();
            ItemDefinition card = Resources.Load<ItemDefinition>("Items/item_stair_card");
            Assert.IsNotNull(card, "the ward sister's card");
            LockedDoor w3 = CardDoor("W2", "Door_W3"), w10 = CardDoor("W9", "Door_W10"), w10Back = CardDoor("W13", "Door_W10"), stairs = CardDoor("W12", "Door_S1");
            CodeTerminal terminal = UnityEngine.Object.FindObjectsByType<CodeTerminal>(FindObjectsSortMode.None).Single();
            Assert.AreEqual("8675309", terminal.code, "Bruno, 2026-11-08");
            Assert.IsTrue(stairs.rotatesOnTry, "the way on");
            try
            {
                StringAssert.StartsWith("locked:", w3.Confirm(her), "W3 is locked before she has the card");
                her.Inventory.TryAdd(card);
                StringAssert.StartsWith("opened:", w3.Confirm(her), "the card opens W3");
                StringAssert.StartsWith("opened:", w10.Confirm(her), "and W10");
                yield return WaitUntil(() => StoryFlags.Has(DemoFlags.CodesRotated), 6f);
                CodeRotation rotation = UnityEngine.Object.FindObjectsByType<CodeRotation>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
                Assert.IsTrue(StoryFlags.Has(DemoFlags.CodesRotated),
                    $"two doors: the PA rotates the codes (rotation active={rotation.isActiveAndEnabled} opened={rotation.Opened} doors={rotation.Doors.Count} timeScale={Time.timeScale})");
                Assert.IsTrue(stairs.CodeBlocked, "the stairwell reader refuses the card");
                Assert.IsTrue(w10Back.CodeBlocked, "and so does the W13 side of W10");
                Assert.IsFalse(terminal.TryCode("5281"), "the old code is wrong");
                Assert.IsTrue(terminal.TryCode("8675309"), "the code on the wall of W14");
                Assert.IsFalse(stairs.CodeBlocked);
                StringAssert.StartsWith("opened:", stairs.Confirm(her), "the enrolled card opens the stairwell");
            }
            finally
            {
                StoryFlags.Drop(DemoFlags.CodesRotated);
                StoryFlags.Drop(DemoFlags.CardEnrolled);
            }
        }

        [UnityTest]
        public IEnumerator TheWardSistersCard_AtTheStairsFirst_RotatesTheCodesAtOnce()
        {
            yield return LoadWorld();
            Interactor her = GameObject.FindGameObjectWithTag("Player").GetComponent<Interactor>();
            her.Inventory.TryAdd(Resources.Load<ItemDefinition>("Items/item_stair_card"));
            LockedDoor stairs = CardDoor("W12", "Door_S1");
            try
            {
                stairs.BeforeKey(her);
                Assert.IsTrue(StoryFlags.Has(DemoFlags.CodesRotated), "no way past the puzzle: the stairs rotate the codes");
                StringAssert.StartsWith("rotated:", stairs.Confirm(her));
                Assert.IsFalse(stairs.IsOpen);
            }
            finally
            {
                StoryFlags.Drop(DemoFlags.CodesRotated);
            }
        }

        [UnityTest]
        public IEnumerator EveryProp_HasHerComment_AndTheStructureNone()
        {
            yield return LoadWorld();
            Examinable[] all = UnityEngine.Object.FindObjectsByType<Examinable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"[DemoTests] {all.Length} props with a comment");
            Assert.Greater(all.Length, 100, "the props of the demo (DemoRoom.Prop)");
            Assert.IsEmpty(all.Where(e => string.IsNullOrWhiteSpace(e.comment)).Select(e => e.name), "no empty comment");
            Assert.IsEmpty(all.Where(e => e.name.StartsWith("Wall") || e.name == "Handrail" || e.name.StartsWith("Stairs")).Select(e => e.name), "no comment on the structure");
        }

        private static LockedDoor CardDoor(string room, string name)
        {
            LockedDoor door = UnityEngine.Object.FindObjectsByType<LockedDoor>(FindObjectsSortMode.None)
                .FirstOrDefault(d => d.name == name && d.GetComponentInParent<GlorifiedBody.Rooms.Room>()?.id == room);
            Assert.IsNotNull(door, $"{name} in {room}");
            return door;
        }

        [UnityTest]
        public IEnumerator R1_ThePosterBeat_TheFlashback_UnlocksMkEdgar_AndTheAttendantFollows()
        {
            yield return Coda("R1", "demo_crown_gone", "demo_readout_on");
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            Assert.IsFalse(PsychicPowers.EdgarUnlocked, "before the beat");
            lucy.GetComponent<TankController>().Teleport(GameObject.Find("Entry_R1").transform.position + Vector3.forward * 1.6f, 0f);
            // The news on the TV behind the till first (N1b, round 108): the beat waits for its end.
            Broadcast news = GameObject.Find("TV_Till").GetComponent<Broadcast>();
            yield return WaitUntil(() => news.Spoken > 0, 3f);
            Assert.IsTrue(news.Started, "the TV plays when she steps in");
            yield return new WaitForSeconds(3f);
            Assert.IsFalse(CutscenePlayer.IsPlaying, "the poster beat waits for the news");
            Assert.IsFalse(lucy.GetComponent<TankController>().MovementLocked, "she walks while it plays");
            yield return WaitUntil(() => StoryFlags.Has("demo_r1_news_done"), 120f);
            Assert.IsTrue(news.Done, "the report ends");
            Assert.AreEqual("low", news.Band, "no kill in this test: the low band");
            StringAssert.StartsWith("Police are asking citizens", news.LastText, "cut off");
            yield return WaitUntil(() => CutscenePlayer.IsPlaying, 40f);
            Assert.IsTrue(CutscenePlayer.IsPlaying, "the flashback");
            yield return WaitUntil(() => !CutscenePlayer.IsPlaying, 25f);
            yield return WaitUntil(() => PsychicPowers.EdgarUnlocked, 8f);
            Assert.IsTrue(PsychicPowers.EdgarUnlocked, "the flag goes up");
            yield return null;
            GameObject attendant = GameObject.Find("Attendant");
            Assert.IsTrue(attendant.GetComponent<IDominable>().IsDominated, "he is hers, and she did not want it");
            Assert.AreEqual(100f, lucy.GetComponent<PsychicStats>().Mk(PowerKind.Domination), 0.01f, "the first take costs no MK");
            yield return WaitUntil(() => StoryFlags.Has("demo_r1_poster"), 20f);
            Assert.IsFalse(lucy.GetComponent<TankController>().MovementLocked, "she walks");
            Assert.AreEqual("EDGAR CAPSULE", Resources.Load<ItemDefinition>("Items/item_nalconmind").ShownName);
            StoryFlags.Drop(PsychicPowers.EdgarFlag);
        }

        [UnityTest]
        public IEnumerator R2_TheMechanicAtFullHp_SentToTheSwitch_OpensTheShutter_AndAKillOnlyBringsASpare()
        {
            yield return Coda("R2", "demo_crown_gone", "demo_readout_on", PsychicPowers.EdgarFlag);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            TankController tank = lucy.GetComponent<TankController>();
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            GameObject mechanic = GameObject.Find("Mechanic"), shutter = GameObject.Find("Shutter");
            Assert.IsNotNull(mechanic);
            Assert.IsTrue(shutter.activeInHierarchy, "the shutter is shut");
            tank.Teleport(mechanic.transform.position + Vector3.back * 3.4f, 0f);
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "R2", 3f);

            // A kill does not open it: a spare comes out.
            mechanic.GetComponent<Health>().TakeDamage(9999f);
            yield return WaitUntil(() => GameObject.Find("Spare_Mechanic") != null, GlorifiedBody.Story.SpareFigure.WaitSeconds + 3f);
            GameObject spare = GameObject.Find("Spare_Mechanic");
            Assert.IsNotNull(spare, "the spare comes");
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(shutter.activeInHierarchy, "the kill opened nothing");

            powers.Select(PowerKind.Domination);
            tank.Teleport(spare.transform.position + (Vector3.back * 3f), 0f);
            yield return null;
            Assert.IsNull(Domination.Refusal(spare.GetComponent<GlorifiedBody.Combat.LockOnTargetable>(), lucy.transform.position, powers.Selected.range, true), "she can take him");
            Domination.Take(spare.GetComponent<IDominable>(), lucy);
            Assert.IsTrue(Domination.Current.SendToSwitch(lucy.transform.position, powers.Selected.range), "the switch is in reach");
            yield return WaitUntil(() => StoryFlags.Has("demo_r2_shutter"), 15f);
            Assert.IsTrue(StoryFlags.Has("demo_r2_shutter"), $"he reached the switch; he stands at {spare.transform.position}");
            yield return null;
            Assert.IsFalse(shutter.activeInHierarchy, "the shutter opens");
            StoryFlags.Drop(PsychicPowers.EdgarFlag);
        }

        [UnityTest]
        public IEnumerator R3_TheGuardMustBeHurt_SentToTheSwitch_TheBarrierRises_AndTheDemoEnds()
        {
            yield return Coda("R3", "demo_crown_gone", "demo_readout_on", PsychicPowers.EdgarFlag);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            TankController tank = lucy.GetComponent<TankController>();
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            GameObject guard = GameObject.Find("Guard_Booth");
            Assert.IsNotNull(guard);
            tank.Teleport(guard.transform.position + Vector3.back * 3.4f, 0f);
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "R3", 3f);
            var lockable = guard.GetComponent<GlorifiedBody.Combat.LockOnTargetable>();
            powers.Select(PowerKind.Domination);
            Assert.AreEqual("dom_not_hurt", Domination.Refusal(lockable, lucy.transform.position, powers.Selected.range, true), "unhurt: not weak enough");
            guard.GetComponent<Health>().TakeDamage(5f);
            yield return null;
            Assert.IsNull(Domination.Refusal(lockable, lucy.transform.position, powers.Selected.range, true));
            Domination.Take(guard.GetComponent<IDominable>(), lucy);
            Assert.IsTrue(Domination.Current.SendToSwitch(lucy.transform.position, powers.Selected.range));
            yield return WaitUntil(() => StoryFlags.Has("demo_r3_barrier"), 15f);
            Assert.IsTrue(StoryFlags.Has("demo_r3_barrier"), $"he reached the switch; he stands at {guard.transform.position}");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu", 20f);
            Assert.AreEqual("Main_Menu", SceneManager.GetActiveScene().name, "the title: the demo ends");
            StoryFlags.Drop(PsychicPowers.EdgarFlag);
        }

        [UnityTest]
        public IEnumerator W11_TheWardSister_GivesTheCard_WhenSheAsks()
        {
            // Bruno, 2026-11-02: "the second way to grab cards from civvies is to simply interact/talk with them".
            yield return LoadWorld();
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

        [UnityTest]
        public IEnumerator Start_OpensOnTheWake_NoFrameOfTheGameBeforeIt()
        {
            // Bruno, 2026-11-04: "we see the game screen, then it fades to the cutscene. it needs to start on the curscene
            // right away".
            GameManager.Instance.LoadLevel(World);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == World, 30f);
            Assert.AreEqual(World, SceneManager.GetActiveScene().name);
            int frames = 0;
            float shown = -1f;
            float t0 = Time.realtimeSinceStartup;
            while (!CutscenePlayer.ShowsPicture && Time.realtimeSinceStartup - t0 < 5f)
            {
                Assert.Greater(GlorifiedBody.UI.ScreenFade.Alpha, 0.99f, $"frame {frames} of the world: the screen stays black until the cutscene shows");
                frames++;
                yield return null;
            }
            shown = Time.realtimeSinceStartup - t0;
            Debug.Log($"[DemoTests] the wake shows after {frames} frames, {shown:0.00} s");
            Assert.IsTrue(CutscenePlayer.ShowsPicture, "the wake shows");
            Assert.AreEqual("01_the_wake", CutscenePlayer.Current);
            Assert.Less(shown, 0.5f, "right away: no breath and no fade before it");
        }

        [UnityTest]
        public IEnumerator W1_IsTheQuietRoom_NoBeatAnywhereInIt_NoLineToTheScientist()
        {
            // Bruno, 2026-11-05: "i can barely walk and the guy barges into the room even before i leave it ... add a second room
            // that the first scientist will be in, not at the corridor. that gives the first room a mini-tutorial/safe space".
            yield return LoadWorld();
            yield return WaitUntil(() => CutscenePlayer.IsPlaying && CutscenePlayer.ShotIndex >= 0, 10f);
            CutscenePlayer.Skip();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake"), 10f);
            ScriptedSequence meet = GameObject.Find("W1B_Scientist").GetComponent<ScriptedSequence>();
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            TankController tank = lucy.GetComponent<TankController>();
            GlorifiedBody.Rooms.Room w1 = GlorifiedBody.Rooms.Room.Of("W1");
            Assert.AreEqual("W1", GlorifiedBody.Rooms.RoomTracker.CurrentId, "she wakes in W1");
            float toDoor = lucy.transform.position.z - 19f;
            Debug.Log($"[DemoTests] from her start to the doorway: {toDoor:0.0} m");
            Assert.GreaterOrEqual(toDoor, 5f, "room to walk before the door");

            yield return WaitUntil(() => UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).Any(b => b.name == "Scientist_Knife"), 5f);
            SoldierBrain knife = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).First(b => b.name == "Scientist_Knife");
            Assert.AreEqual("W1B", GlorifiedBody.Rooms.Room.At(knife.transform.position + Vector3.up * 0.1f).id, "he stands in the next room");
            int solid = LayerMask.GetMask("Default");
            Vector3 hisChest = knife.transform.position + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight;
            int checkedPoints = 0;
            var openLines = new List<string>();
            for (float x = 0.5f; x < 8f; x += 0.5f)
                for (float z = 19.5f; z < 26f; z += 0.5f)
                {
                    Vector3 from = new Vector3(x, GlorifiedBody.Characters.Body.ChestHeight, z);
                    checkedPoints++;
                    if (!Physics.Linecast(from, hisChest, solid, QueryTriggerInteraction.Ignore)) openLines.Add($"({x},{z})");
                }
            Assert.IsEmpty(openLines, $"no line to him from {checkedPoints} points of W1: she cannot lock on or wake him from the quiet room");

            foreach (Vector3 point in new[] { new Vector3(1f, 0f, 20f), new Vector3(7f, 0f, 20f), new Vector3(4f, 0f, 19.6f), new Vector3(1f, 0f, 24f), new Vector3(7f, 0f, 23.5f) })
            {
                tank.Teleport(point, 180f);
                yield return new WaitForSeconds(0.3f);
                Assert.AreEqual("W1", GlorifiedBody.Rooms.RoomTracker.CurrentId, $"at {point}");
                Assert.IsFalse(meet.Running || meet.Done, $"at {point}: no scientist yet");
                Assert.IsFalse(knife.Alerted, $"at {point}: he does not know");
            }
        }

        [UnityTest]
        public IEnumerator EveryExit_ShowsInTheCameraOfTheFloorInFrontOfIt()
        {
            // Bruno, 2026-11-05: "although i get good visuals on the room i also need to see the exits; in some rooms the
            // doors/exits are in a blind spot".
            yield return LoadWorld();
            yield return new WaitForSeconds(0.2f);
            FixedCameraDirector director = UnityEngine.Object.FindAnyObjectByType<FixedCameraDirector>();
            int exits = GlorifiedBody.Rooms.Room.All.Sum(r => r.exits.Length);
            List<string> missing = GlorifiedBody.Automation.CameraCoverage.ExitsOutOfView(director);
            Debug.Log($"[DemoTests] exits: {missing.Count} of {exits} out of view: {string.Join("; ", missing)}");
            Assert.Greater(exits, 40, "the demo's doorways");
            Assert.IsEmpty(missing, "every exit shows in its camera");
        }

        [UnityTest]
        public IEnumerator TheGuardWithTheScan_FightsLikeTheOther_AndCowersOnlyByFear()
        {
            // Bruno, 2026-11-05: "what is the deal with this scannable guard? he won't attack and keeps turning its back on me?"
            yield return LoadWorld();
            yield return WaitUntil(() => CutscenePlayer.IsPlaying && CutscenePlayer.ShotIndex >= 0, 10f);
            CutscenePlayer.Skip();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake"), 10f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            TankController tank = lucy.GetComponent<TankController>();
            tank.Teleport(new Vector3(1.5f, 0f, 3f), 180f); // W5, in front of the two guards
            SoldierBrain[] all = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None);
            SoldierBrain plain = all.First(b => b.name == "Guard_A");
            SoldierBrain scanned = all.First(b => b.name == "Guard_B");
            Assert.IsNull(plain.GetComponent<GlorifiedBody.Interaction.Scannable>(), "the other guard has no scan");
            Assert.IsNotNull(scanned.GetComponent<GlorifiedBody.Interaction.Scannable>(), "this guard has a scan");
            bool aims = false;
            float t0 = Time.time;
            while (!aims && Time.time - t0 < 25f) { aims = scanned.Gun.HoldsSlot; yield return null; }
            Assert.IsTrue(aims, "with no fear he aims at her, as the other one does");
            Assert.AreEqual(0, scanned.GetComponent<EnemyMorale>().Fear);
            scanned.GetComponent<EnemyMorale>().SeeFeed("test"); // +2: one gruesome sight
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(scanned.Retreating, "one gruesome sight is not enough (the limit of a guard is 3)");
            scanned.GetComponent<EnemyMorale>().SeeFeed("test"); // +2: at the limit
            yield return WaitUntil(() => scanned.Current == SoldierBrain.Phase.Cower, 12f);
            Assert.AreEqual(SoldierBrain.Phase.Cower, scanned.Current, "at the limit he runs and cowers");
            foreach (Vector3 her in new[] { scanned.transform.position + Vector3.forward * 2f, scanned.transform.position + Vector3.right * 1.5f })
            {
                tank.Teleport(her, 180f);
                yield return new WaitForSeconds(1.5f);
                Vector3 toHer = lucy.transform.position - scanned.transform.position;
                toHer.y = 0f;
                float dot = Vector3.Dot(scanned.transform.forward, toHer.normalized);
                Debug.Log($"[DemoTests] the cowering guard: facing dot {dot:0.00} with her at {her}");
                Assert.Less(dot, -0.5f, "he turns his back to her, wherever she stands");
                Assert.IsFalse(scanned.Gun.HoldsSlot, "and he does not aim");
            }
        }

        [UnityTest]
        public IEnumerator AFigureThatCowers_SaysItGaveUp_WhenSheComesNear_OnlyThen()
        {
            // Bruno, 2026-11-05: yes to a clearer sign for a guard who cowers. The lines are the writer's (barks.csv, cower_*).
            yield return LoadWorld();
            yield return WaitUntil(() => CutscenePlayer.IsPlaying && CutscenePlayer.ShotIndex >= 0, 10f);
            CutscenePlayer.Skip();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake"), 10f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            TankController tank = lucy.GetComponent<TankController>();
            SoldierBrain[] all = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None);
            foreach ((string who, string[] lines, Vector3 start) in new[]
            {
                ("Guard_A", SoldierBrain.GuardCowerLines, new Vector3(1.5f, 0f, 3f)),
                ("Scientist_B", SoldierBrain.LabWorkerCowerLines, new Vector3(20f, 0f, 12.5f)),
            })
            {
                SoldierBrain figure = all.First(b => b.name == who);
                tank.Teleport(start, 180f);
                yield return new WaitForSeconds(0.2f);
                EnemyMorale morale = figure.GetComponent<EnemyMorale>();
                morale.SeeFeed("test");
                morale.SeeFeed("test");
                yield return WaitUntil(() => figure.Current == SoldierBrain.Phase.Cower, 12f);
                Assert.AreEqual(SoldierBrain.Phase.Cower, figure.Current, who + " cowers");
                // Bruno, 2026-11-05: "cowering figure drop its weapon".
                EnemyGun weapon = figure.GetComponent<EnemyGun>();
                Assert.IsTrue(weapon.Dropped, who + " drops his weapon");
                Assert.IsNull(weapon.Model.parent, who + ": the weapon left his hands");
                yield return new WaitForSeconds(2f);
                Debug.Log($"[DemoTests] {who}: the weapon lies at y {weapon.Model.position.y:0.00}, his root at y {figure.transform.position.y:0.00}, {Vector3.Distance(weapon.Model.position, figure.transform.position):0.00} m from him");
                Assert.Less(Mathf.Abs(weapon.Model.position.y - figure.transform.position.y), 0.4f, who + ": the weapon lies on the floor");
                Vector3 at = figure.transform.position;
                Vector3 far = at + (start - at).normalized * Mathf.Min(6f, Vector3.Distance(start, at));
                bool startFar = Vector3.Distance(far, at) > 4.5f;
                if (startFar) tank.Teleport(far, 180f);
                yield return new WaitForSeconds(0.3f);
                int before = Bark.CountOf(figure.gameObject);
                if (startFar)
                {
                    yield return new WaitForSeconds(1f);
                    Assert.AreEqual(before, Bark.CountOf(figure.gameObject), who + ": she is far, he says nothing");
                }
                tank.Teleport(at + Vector3.forward * 1.5f, 180f);
                yield return new WaitForSeconds(0.5f);
                Assert.AreEqual(before + 1, Bark.CountOf(figure.gameObject), who + ": she comes near, he speaks");
                Assert.AreEqual(lines[0], figure.GetComponent<Bark>().Shown, who + ": the plainest line first");
                yield return new WaitForSeconds(6.5f);
                Assert.AreEqual(before + 2, Bark.CountOf(figure.gameObject), who + ": another line after the gap");
                Assert.Contains(figure.GetComponent<Bark>().Shown, lines, who + ": from his own list");
                Assert.AreNotEqual(lines[0], figure.GetComponent<Bark>().Shown, who + ": no repeat");
            }
        }

        [UnityTest]
        public IEnumerator TheCoverMap_FindsCover_InTheRooms_WithNoMarks()
        {
            // Bruno, 2026-11-06: "the ai does not take cover because there are no places assigned ... adding them manuially one by
            // one is a non-starter".
            yield return LoadWorld();
            yield return WaitUntil(() => CutscenePlayer.IsPlaying && CutscenePlayer.ShotIndex >= 0, 10f);
            CutscenePlayer.Skip();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake"), 10f);
            yield return new WaitForSeconds(0.5f); // the carving of the props
            IReadOnlyList<CoverSpot> spots = CoverMap.Spots;
            var perRoom = spots.GroupBy(c => GlorifiedBody.Rooms.Room.At(c.Position + Vector3.up * 0.1f)?.id ?? "?").OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Count()}");
            Debug.Log($"[DemoTests] cover: {spots.Count} spots in {CoverMap.BuildMilliseconds:0} ms: low {spots.Count(c => c.Low)}, corners {spots.Count(c => !c.Low)}, on a breakable {spots.Count(c => c.Piece != null)}; {string.Join(" ", perRoom)}");
            Assert.Less(CoverMap.BuildMilliseconds, 1500f, "the scan costs little at the first fight");
            Assert.IsTrue(spots.Any(c => c.Low && c.Piece != null), "low cover that breaks (a desk, a cabinet)");
            Assert.IsTrue(spots.Any(c => c.Low && c.Piece == null), "low cover that does not break: a counter");
            GlorifiedBody.Rooms.Room g2 = GlorifiedBody.Rooms.Room.Of("G2");
            Assert.IsTrue(spots.Any(c => !c.Low && g2.Contains(c.Position + Vector3.up * 0.1f)), "a corner in G2: its columns");
            foreach (CoverSpot c in spots.Take(50))
            {
                Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(c.FirePoint, out _, 0.3f, UnityEngine.AI.NavMesh.AllAreas), "a soldier can stand there");
                Assert.AreEqual(1f, c.Facing.magnitude, 0.01f);
            }
            Assert.IsFalse(spots.Any(c => c.Piece != null && !c.Piece.IsCover), "a door leaf or a low box is no cover");
        }

        [UnityTest]
        public IEnumerator TheProps_BreakByType_AnItemFalls_AndTheWayOpens()
        {
            // Bruno, 2026-11-06: "add some prop types".
            yield return LoadWorld();
            yield return new WaitForSeconds(0.5f);
            Destruction.Breakable[] all = UnityEngine.Object.FindObjectsByType<Destruction.Breakable>(FindObjectsSortMode.None);
            Destruction.Breakable Named(string n) => all.FirstOrDefault(b => b.name == n);
            Assert.IsNull(Named("Planter_0"), "a planter does not break: she moves it (§10.5)");
            Assert.IsNotNull(GameObject.Find("Planter_0").GetComponent<GlorifiedBody.Interaction.MovableProp>(), "a planter she moves");
            Assert.IsNotNull(Named("Desk_A"), "a desk breaks");
            Assert.IsNull(all.FirstOrDefault(b => b.name == "Counter"), "a counter does not");
            Destruction.Breakable bin = all.FirstOrDefault(b => b.name.StartsWith("Box_"));
            Assert.IsNotNull(bin, "a box breaks");
            Assert.IsFalse(bin.IsCover, "a low box is no cover");
            // L7 lies on Desk_A in W10.
            GameObject note = GameObject.Find("L7_Session_Note");
            Assert.IsNotNull(note);
            Destruction.Breakable desk = Named("Desk_A");
            Vector3 middle = desk.Bounds.center;
            middle.y = desk.Bounds.min.y;
            Assert.IsFalse(UnityEngine.AI.NavMesh.SamplePosition(middle, out _, 0.2f, UnityEngine.AI.NavMesh.AllAreas), "she and they walk around the desk");
            desk.Break(desk.Bounds.center, Vector3.forward);
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(desk.IsBroken);
            Assert.Less(note.transform.position.y - desk.Bounds.min.y, 0.2f, "the note lies on the floor");
            Assert.IsTrue(note.GetComponent<GlorifiedBody.Interaction.ItemPickup>().Available, "she can still take it");
            Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(middle, out _, 0.2f, UnityEngine.AI.NavMesh.AllAreas), "the way opens");
        }

        [UnityTest]
        public IEnumerator ASoldier_TakesCover_HidesBetweenShots_StandsToFire_AndLeavesWhenSheGetsPast()
        {
            yield return LoadWorld();
            yield return WaitUntil(() => CutscenePlayer.IsPlaying && CutscenePlayer.ShotIndex >= 0, 10f);
            CutscenePlayer.Skip();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake"), 10f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            stats.AddShort(-PsychicStats.MaxShort);
            TankController tank = lucy.GetComponent<TankController>();
            Vector3 g2 = GlorifiedBody.Rooms.Room.Of("G2").transform.position;
            tank.Teleport(g2 + new Vector3(19f, 0f, 1f), 90f);
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "G2", 3f);
            SoldierBrain[] pairs = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).Where(b => b.name.StartsWith("Soldier_Pair")).ToArray();
            Assert.AreEqual(4, pairs.Length, "the fireteam of G2");
            SoldierBrain ducking = null;
            float t0 = Time.time;
            while (ducking == null && Time.time - t0 < 40f)
            {
                stats.AddShort(-PsychicStats.MaxShort);
                ducking = pairs.FirstOrDefault(b => b.Current == SoldierBrain.Phase.Duck && b.GetComponent<GlorifiedBody.Combat.IFrontGuard>() == null);
                if (ducking == null) yield return null;
            }
            Debug.Log($"[DemoTests] after {Time.time - t0:0.0} s: " + string.Join("; ", pairs.Select(b => $"{b.name} {b.Current} cover={(b.HeldCover == null ? "none" : (b.HeldCover.Low ? "low " : "corner ") + (b.Cover != null ? b.Cover.name : "solid"))} ducks={b.Ducks}")));
            Assert.IsNotNull(ducking, "a soldier takes cover in the fight, and hides between shots");
            Assert.Greater(Vector3.Distance(ducking.transform.position, lucy.transform.position), 2.5f, "he stays off her: no charge to cover");
            CoverSpot spot = ducking.HeldCover;
            Assert.IsNotNull(spot);
            LockOnTargetable lockable = ducking.GetComponent<LockOnTargetable>();
            yield return WaitUntil(() => lockable.InCover || ducking.Current != SoldierBrain.Phase.Duck, 4f);
            if (lockable.InCover) Assert.LessOrEqual(Vector3.Distance(Vector3.ProjectOnPlane(ducking.transform.position, Vector3.up), Vector3.ProjectOnPlane(spot.Position, Vector3.up)), 1.25f, "he hides at his spot, not in the open");
            yield return new WaitForSeconds(0.2f);
            Debug.Log($"[DemoTests] {ducking.name} at {ducking.transform.position}, her at {lucy.transform.position}, phase {ducking.Current}, {(spot.Low ? "low" : "corner")} {(spot.Piece != null ? spot.Piece.name : "solid")}");
            Health health = ducking.GetComponent<Health>();
            float before = health.CurrentHealth;
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            powers.Select(PowerKind.Blast);
            stats.DebugSet(45f, 0f);
            if (ducking.Current == SoldierBrain.Phase.Duck && lockable.InCover)
            {
                powers.FireAt(powers.Selected, 0.5f, lockable, 1f);
                Assert.AreEqual(before, health.CurrentHealth, 0.01f, "her power stops on the cover while he hides");
            }
            int shots = ducking.Gun.ShotsFired;
            t0 = Time.time;
            bool stood = false;
            while (ducking.Gun.ShotsFired == shots && Time.time - t0 < 15f)
            {
                stats.AddShort(-PsychicStats.MaxShort);
                stood |= !lockable.InCover;
                yield return null;
            }
            Assert.IsTrue(stood, "he stands");
            Assert.Greater(ducking.Gun.ShotsFired, shots, "and fires");
            Assert.IsFalse(lockable.InCover, "he is open while he fires");
            // She gets past the thing: the spot no longer works, and he leaves it.
            Vector3 past = ducking.transform.position - spot.Facing * 3f;
            Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(past, out UnityEngine.AI.NavMeshHit on, 2f, UnityEngine.AI.NavMesh.AllAreas));
            tank.Teleport(on.position, 0f);
            t0 = Time.time;
            while (ducking.HeldCover == spot && Time.time - t0 < 10f)
            {
                stats.AddShort(-PsychicStats.MaxShort);
                yield return null;
            }
            Assert.AreNotEqual(spot, ducking.HeldCover, "she stands on his side: he leaves that spot");
        }

        private IEnumerator InG2(System.Action<SoldierBrain[], PsychicStats> ready)
        {
            yield return WaitUntil(() => CutscenePlayer.IsPlaying && CutscenePlayer.ShotIndex >= 0, 10f);
            CutscenePlayer.Skip();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake"), 10f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            stats.AddShort(-PsychicStats.MaxShort);
            lucy.GetComponent<TankController>().Teleport(GlorifiedBody.Rooms.Room.Of("G2").transform.position + new Vector3(16f, 0f, 1f), 90f);
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "G2", 3f);
            ready(UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).Where(b => b.name.StartsWith("Soldier_Pair")).ToArray(), stats);
        }

        [UnityTest]
        public IEnumerator TheFireteam_Bounds_OneElementMoves_WhileTheOtherFires()
        {
            // Bruno, 2026-11-05: "they need to be (and look and feel like) an organized team".
            yield return LoadWorld();
            SoldierBrain[] team = null;
            PsychicStats stats = null;
            yield return InG2((t, s) => { team = t; stats = s; });
            Assert.AreEqual(4, team.Length);
            yield return WaitUntil(() => team.All(b => b.Team != null), 8f);
            FireTeam fire = team[0].Team;
            Assert.IsNotNull(fire, "they join a team");
            Assert.IsTrue(team.All(b => b.Team == fire), "one team");
            yield return new WaitForSeconds(3f); // others near hear the fight and join (the fireteam of G5)
            team = fire.Members.ToArray();
            Assert.GreaterOrEqual(team.Length, 4);
            Assert.LessOrEqual(Mathf.Abs(team.Count(b => fire.ElementOf(b) == 0) - team.Count(b => fire.ElementOf(b) == 1)), 1, "two even elements");
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            int frames = 0, bothMoving = 0, shots0 = 0, shots1 = 0, moves0 = 0, moves1 = 0;
            float nearestSum = 0f;
            int[] startShots = team.Select(b => b.Gun.ShotsFired).ToArray();
            int[] startMoves = team.Select(b => b.Moves).ToArray();
            float t0 = Time.time;
            int swapsAtStart = fire.Swaps;
            while (Time.time - t0 < 30f)
            {
                stats.AddShort(-PsychicStats.MaxShort);
                frames++;
                bool e0 = team.Any(b => fire.ElementOf(b) == 0 && b.Current == SoldierBrain.Phase.Move);
                bool e1 = team.Any(b => fire.ElementOf(b) == 1 && b.Current == SoldierBrain.Phase.Move);
                if (e0 && e1) bothMoving++;
                nearestSum += team.Min(b => Vector3.Distance(b.transform.position, lucy.transform.position));
                yield return null;
            }
            for (int i = 0; i < team.Length; i++)
            {
                int el = fire.ElementOf(team[i]);
                if (el == 0) { shots0 += team[i].Gun.ShotsFired - startShots[i]; moves0 += team[i].Moves - startMoves[i]; }
                else { shots1 += team[i].Gun.ShotsFired - startShots[i]; moves1 += team[i].Moves - startMoves[i]; }
            }
            float both = (float)bothMoving / frames, nearest = nearestSum / frames;
            Debug.Log($"[DemoTests] the team in 30 s: {fire.Swaps - swapsAtStart} bounds, both elements moving {both:P0} of the frames, shots {shots0} and {shots1}, moves {moves0} and {moves1}, nearest soldier {nearest:0.0} m on average; {string.Join("; ", team.Select(b => $"{b.name} {(b.HeldCover != null ? (b.HeldCover.Low ? "low" : "corner") : "open")}"))}");
            Assert.GreaterOrEqual(fire.Swaps - swapsAtStart, 3, "the elements take turns");
            Assert.Less(both, 0.35f, "they rarely move at the same time");
            Assert.Greater(moves0, 0, "the first element moves");
            Assert.Greater(moves1, 0, "the second element moves");
            Assert.Greater(shots0 + shots1, 2, "they fire");
            // The door to G5 lies 4 m from where she stands: the two who come from G5 pass near her. The runs gave 4.5 to 4.6 m.
            Assert.Greater(nearest, 4.2f, "no charge: they stay off her");
        }

        /// <summary>A room on the other floor: no soldier finds her there in the time of the test (in G3 and G9 they walked up and saw her, as they should).</summary>
        private static Vector3 HidingPlace() => FloorPoint(GlorifiedBody.Rooms.Room.Of("W6"));

        /// <summary>A floor point 3 m from him with a clear line.</summary>
        private static Vector3 InSight(SoldierBrain soldier)
        {
            Vector3 eye = soldier.transform.position + Vector3.up * 1.3f;
            for (int i = 0; i < 12; i++)
            {
                Vector3 point = soldier.transform.position + Quaternion.Euler(0f, i * 30f, 0f) * soldier.transform.forward * 3f;
                if (!UnityEngine.AI.NavMesh.SamplePosition(point, out UnityEngine.AI.NavMeshHit hit, 0.5f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                if (!Physics.Linecast(eye, hit.position + Vector3.up * 1.2f, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore)) return hit.position;
            }
            Assert.Fail("no clear point near " + soldier.name);
            return soldier.transform.position;
        }

        [UnityTest]
        public IEnumerator TheFireteam_SheHides_TheyFireAtThePlace_ThenSearchInPairs_ThenContact()
        {
            // DEMO_PLAN §13.6: suppression and the search in pairs.
            yield return LoadWorld();
            SoldierBrain[] team = null;
            PsychicStats stats = null;
            yield return InG2((t, s) => { team = t; stats = s; });
            yield return WaitUntil(() => team.All(b => b.Team != null), 8f);
            FireTeam fire = team[0].Team;
            yield return new WaitForSeconds(3f);
            team = fire.Members.ToArray();
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            TankController tank = lucy.GetComponent<TankController>();
            Vector3 fight = lucy.transform.position;
            Assert.IsFalse(fire.HerHidden, "they see her");

            // She hides.
            tank.Teleport(HidingPlace(), 0f);
            int[] shots = team.Select(b => b.Gun.SuppressShots).ToArray();
            bool sawPlaceAim = false, sawSlotFree = false;
            float t0 = Time.time;
            yield return WaitUntil(() => fire.HerHidden, 4f);
            Assert.IsTrue(fire.HerHidden, "no line for 1.5 s");
            Assert.Greater(Vector3.Distance(team[0].TargetPosition, lucy.transform.position), 3f, "they know only what they saw: not where she is now");
            while (!fire.Lost && Time.time - t0 < FireTeam.LostAfter + 2f)
            {
                stats.AddShort(-PsychicStats.MaxShort);
                foreach (SoldierBrain b in team)
                {
                    if (b.Current == SoldierBrain.Phase.Dead || !b.Gun.Suppressing) continue;
                    sawPlaceAim |= Vector3.Distance(b.Gun.AimPoint, fire.LastSeen) < 4f;
                }
                // An aim at her that began before she hid runs out in the first seconds.
                sawSlotFree |= Time.time - t0 > 4f && ShooterScheduler.ActiveCount == 0 && team.Any(b => b.Gun.Suppressing);
                yield return null;
            }
            int suppressed = 0;
            for (int i = 0; i < team.Length; i++) suppressed += team[i].Gun.SuppressShots - shots[i];
            Debug.Log($"[DemoTests] she hid: {suppressed} shots at the place in {Time.time - t0:0.0} s by {team.Length} soldiers, last seen {fire.LastSeen}, she stands at {lucy.transform.position}");
            Assert.Greater(suppressed, 0, "the holders fire at the place");
            Assert.IsTrue(sawPlaceAim, "the aim lies at the place where they saw her");
            Assert.IsTrue(sawSlotFree, "suppression takes no slot: the slots count the guns on her");
            Assert.IsTrue(fire.Lost, "nobody has seen her for 8 s");

            // The search.
            yield return WaitUntil(() => team.Count(b => b.Current == SoldierBrain.Phase.Search) >= 2, 8f);
            Assert.GreaterOrEqual(team.Count(b => b.Current == SoldierBrain.Phase.Search), 2, "they go into the search");
            Assert.GreaterOrEqual(fire.Route.Count, 2, "the place, then the rooms next to it");
            Assert.Less(Vector3.Distance(fire.Route[0], fire.LastSeen), 3.5f, "the first point: where they lost her");
            int frames = 0, bothWalk = 0, walkOne = 0;
            t0 = Time.time;
            while (Time.time - t0 < 25f && fire.Lost)
            {
                stats.AddShort(-PsychicStats.MaxShort);
                foreach (SoldierBrain b in team)
                {
                    SoldierBrain partner = fire.PartnerOf(b);
                    if (partner == null || b.GetInstanceID() > partner.GetInstanceID() || b.Current != SoldierBrain.Phase.Search || partner.Current != SoldierBrain.Phase.Search) continue;
                    frames++;
                    if (b.Body.IsMoving && partner.Body.IsMoving) bothWalk++;
                    else if (b.Body.IsMoving || partner.Body.IsMoving) walkOne++;
                }
                yield return null;
            }
            Debug.Log($"[DemoTests] the search in 25 s: {fire.LegsDone} legs, {fire.Route.Count} points, pair frames {frames}: one walks {walkOne}, both walk {bothWalk}; {string.Join("; ", team.Select(b => $"{b.name} {b.Current} {b.transform.position}"))}");
            Assert.Greater(fire.LegsDone, 1, "the pairs go from point to point");
            Assert.Greater(walkOne, bothWalk * 3, "one walks, and the other covers him");
        }

        [UnityTest]
        public IEnumerator TheFireteam_SeesHerInTheSearch_AndTheFightStartsAgain()
        {
            yield return LoadWorld();
            SoldierBrain[] team = null;
            PsychicStats stats = null;
            yield return InG2((t, s) => { team = t; stats = s; });
            yield return WaitUntil(() => team.All(b => b.Team != null), 8f);
            FireTeam fire = team[0].Team;
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            TankController tank = lucy.GetComponent<TankController>();
            tank.Teleport(HidingPlace(), 0f);
            yield return WaitUntil(() => fire.Lost && team.Count(b => b.Current == SoldierBrain.Phase.Search) >= 2, FireTeam.LostAfter + 6f);
            Assert.IsTrue(fire.Lost);
            SoldierBrain seeker = team.First(b => b.Current == SoldierBrain.Phase.Search);
            tank.Teleport(InSight(seeker), 0f);
            yield return WaitUntil(() => !fire.Lost, 2f);
            Assert.IsFalse(fire.Lost, "a soldier sees her");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0, team.Count(b => b.Current == SoldierBrain.Phase.Search), "contact: the search ends");
            int before = team.Sum(b => b.Gun.ShotsFired);
            yield return WaitUntil(() => team.Sum(b => b.Gun.ShotsFired) > before, 8f);
            Assert.Greater(team.Sum(b => b.Gun.ShotsFired), before, "they fire at her again");
            Assert.Less(Vector3.Distance(team[0].TargetPosition, lucy.transform.position), 2f, "they know where she is");
        }

        [UnityTest]
        public IEnumerator AHitFromHer_EndsTheSearch()
        {
            yield return LoadWorld();
            SoldierBrain[] team = null;
            yield return InG2((t, s) => team = t);
            yield return WaitUntil(() => team.All(b => b.Team != null), 8f);
            FireTeam fire = team[0].Team;
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponent<TankController>().Teleport(HidingPlace(), 0f);
            yield return WaitUntil(() => fire.Lost, FireTeam.LostAfter + 3f);
            Assert.IsTrue(fire.Lost);
            team[0].GetComponent<Health>().TakeDamage(1f);
            yield return null;
            Assert.IsFalse(fire.Lost, "a hurt soldier knows where she is");
            Assert.Less(Vector3.Distance(fire.LastSeen, lucy.transform.position), 1f);
        }

        [UnityTest]
        public IEnumerator TheFourDie_SectorCIsReleased_AndASecondFireteamComes_AfterASilence()
        {
            // Bruno, 2026-11-06: "if the player kills all four soldiers, then spawn in more soldiers".
            yield return LoadWorld();
            SoldierBrain[] team = null;
            yield return InG2((t, s) => team = t);
            Assert.AreEqual(4, team.Length);
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).Count(b => b.name.StartsWith("Reinforcement_")), "no second team before its time");
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponent<TankController>().Teleport(GlorifiedBody.Rooms.Room.Of("G9").Centre + Vector3.back * 2f, 90f);
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "G9", 3f);
            for (int i = 0; i < 3; i++) team[i].GetComponent<Health>().TakeDamage(9999f);
            StoryFlags.Raise(SectorCReleased);
            float wait = TeamReinforcement.SilenceSeconds * 0.5f;
            yield return new WaitForSeconds(wait);
            Assert.IsFalse(StoryFlags.Has(Reinforced), "one lives: no new soldiers");
            Assert.IsTrue(team[3].Alerted, "Control tells the one who lives");
            float t0 = Time.time;
            team[3].GetComponent<Health>().TakeDamage(9999f);
            yield return new WaitForSeconds(TeamReinforcement.SilenceSeconds - 3f);
            Assert.IsFalse(StoryFlags.Has(Reinforced), "the kill has its silence");
            yield return WaitUntil(() => StoryFlags.Has(Reinforced), 5f);
            Assert.IsTrue(StoryFlags.Has(Reinforced), "all four dead: they come");
            yield return null;
            yield return new WaitForSeconds(0.7f);
            SoldierBrain[] second = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).Where(b => b.name.StartsWith("Reinforcement_")).ToArray();
            Assert.AreEqual(4, second.Length, "a fireteam of four (round 32: no pairs)");
            Assert.IsTrue(second.All(b => b.Alerted && b.Team != null), "they know");
            Vector3 her = lucy.transform.position;
            float far = second.Min(b => Vector3.Distance(b.transform.position, her));
            yield return WaitUntil(() => second.Min(b => Vector3.Distance(b.transform.position, her)) < far - 5f, 15f);
            float near = second.Min(b => Vector3.Distance(b.transform.position, her));
            Debug.Log($"[DemoTests] the second team: from {far:0.0} m to {near:0.0} m in {Time.time - t0:0} s after the kill");
            Assert.Less(near, far - 5f, "they come for her");
        }

        [UnityTest]
        public IEnumerator ACrateOnTheRim_SlidesIntoTheGap()
        {
            // 2026-11-08: the gap leaves the crate 5 cm on each side, and a drop near it rested on the rim.
            yield return LoadWorld();
            yield return WaitUntil(() => CutscenePlayer.IsPlaying && CutscenePlayer.ShotIndex >= 0, 10f);
            CutscenePlayer.Skip(); // the wake stops the world: the physics of the test runs after it
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake") && Time.timeScale > 0f, 10f);
            GlorifiedBody.Rooms.Room room = GlorifiedBody.Rooms.Room.Of("G16");
            GlorifiedBody.Interaction.PitReturn pit = room.GetComponentInChildren<GlorifiedBody.Interaction.PitReturn>();
            Rigidbody crate = GameObject.Find("Riser_Crate_A").GetComponent<Rigidbody>();
            float floorY = room.transform.position.y;
            // On the east rim, 0.35 m off the centre of the gap, at rest.
            crate.position = new Vector3(pit.transform.position.x + 0.35f, floorY + 0.56f, pit.transform.position.z);
            crate.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(2.5f);
            float top = crate.GetComponent<Collider>().bounds.max.y;
            Assert.Greater(pit.Guided, 0, "the pit guides it");
            Assert.AreEqual(floorY, top, 0.1f, "it lies in the gap, level with the floor");
        }

        [UnityTest]
        public IEnumerator TheGap_SheCannotCross_ACrateFillsIt_ThenSheWalksOver()
        {
            // Bruno, 2026-11-06: "a new room ... that will have a gap that needs to be filled in by a box".
            yield return LoadWorld();
            yield return WaitUntil(() => CutscenePlayer.IsPlaying && CutscenePlayer.ShotIndex >= 0, 10f);
            CutscenePlayer.Skip();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake"), 10f);
            var pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            GameObject drive = new GameObject("[Pad]");
            drive.AddComponent<PadCommit>().Pad = pad;
            try
            {
                GameObject lucy = GameObject.FindGameObjectWithTag("Player");
                lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
                TankController tank = lucy.GetComponent<TankController>();
                PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
                GlorifiedBody.Rooms.Room room = GlorifiedBody.Rooms.Room.Of("G16");
                Assert.IsNotNull(room, "the riser room");
                Vector3 Plan(float x, float z) => room.transform.position + new Vector3(x, 0f, z);
                GlorifiedBody.Interaction.PitReturn pit = room.GetComponentInChildren<GlorifiedBody.Interaction.PitReturn>();
                GameObject crate = GameObject.Find("Riser_Crate_A");
                Assert.IsNotNull(pit);
                Assert.IsNotNull(crate);

                // No crate: she walks in and comes back out on her side.
                tank.Teleport(Plan(18.4f, 7.5f), 90f);
                yield return new WaitForSeconds(0.3f);
                pad.SetHeld(InputManager.GameAction.Forward, true);
                yield return WaitUntil(() => pit.Returns > 0 || lucy.transform.position.x > Plan(20.8f, 0f).x, 6f);
                pad.SetHeld(InputManager.GameAction.Forward, false);
                Assert.Greater(pit.Returns, 0, "the gap stops her, and she comes back");
                Assert.Less(lucy.transform.position.x, Plan(19.4f, 0f).x, "on her side");
                yield return new WaitForSeconds(0.5f);

                // The crate goes in: she stands facing it, lifts it, the mouse takes it over the gap, the release drops it.
                powers.Select(PowerKind.Telekinesis);
                tank.Teleport(Plan(16.6f, 7.5f), 90f);
                yield return null;
                pad.SetHeld(InputManager.GameAction.Defend, true);
                yield return WaitUntil(() => powers.Carrying, 2f);
                Assert.IsTrue(powers.Carrying, "she lifts the crate");
                Assert.AreSame(crate, powers.HeldLift.gameObject);
                yield return new WaitForSeconds(0.4f);
                Camera cam = Camera.main;
                Vector3 over = Plan(20f, 7.5f);
                float t0 = Time.time;
                while (Time.time - t0 < 6f)
                {
                    Vector3 left = over - crate.transform.position;
                    left.y = 0f;
                    if (left.magnitude < 0.05f) break;
                    Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized, away = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                    pad.Look = new Vector2(Mathf.Clamp(Vector3.Dot(left, right) * 3f, -1f, 1f), Mathf.Clamp(Vector3.Dot(left, away) * 3f, -1f, 1f));
                    yield return null;
                }
                pad.Look = Vector2.zero;
                pad.SetHeld(InputManager.GameAction.Defend, false);
                yield return new WaitForSeconds(2f);
                float floorY = room.transform.position.y;
                float top = crate.GetComponent<Collider>().bounds.max.y;
                Debug.Log($"[DemoTests] the crate lies at {crate.transform.position}, its top {top - floorY:0.00} m over the floor");
                Assert.AreEqual(floorY, top, 0.1f, "it lies level with the floor");

                // She walks over it.
                int returns = pit.Returns;
                tank.Teleport(Plan(18.4f, 7.5f), 90f);
                yield return new WaitForSeconds(0.3f);
                pad.SetHeld(InputManager.GameAction.Forward, true);
                yield return WaitUntil(() => lucy.transform.position.x > Plan(21f, 0f).x || pit.Returns > returns, 6f);
                pad.SetHeld(InputManager.GameAction.Forward, false);
                Assert.AreEqual(returns, pit.Returns, "no fall");
                Assert.Greater(lucy.transform.position.x, Plan(21f, 0f).x, "she stands on the far side");
                Assert.IsNotNull(GameObject.Find("Capsule_G16_A"), "the capsules wait there");
            }
            finally
            {
                pad.ReleaseAll();
                GameInput.ClearBotSource(pad);
                UnityEngine.Object.Destroy(drive);
            }
        }

        private sealed class PadCommit : MonoBehaviour
        {
            public VirtualPad Pad;
            private void Update() => Pad?.Commit();
        }

        [UnityTest]
        public IEnumerator TheCapsules_LieOnTheWay_OnTheNavMesh_AtLeastThreeForEachPower()
        {
            yield return LoadWorld();
            var counts = new Dictionary<DrugType, int>();
            foreach (ItemPickup pickup in UnityEngine.Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pickup.item == null || pickup.item.kind != ItemKind.Drug) continue;
                counts[pickup.item.drugType] = counts.TryGetValue(pickup.item.drugType, out int n) ? n + 1 : 1;
                Assert.IsNotNull(GlorifiedBody.Rooms.Room.At(pickup.transform.position), pickup.name + " lies in a room");
                Vector3 floor = pickup.transform.position;
                floor.y = GlorifiedBody.Rooms.Room.At(floor).BaseY; // one lies on a cabinet
                Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(floor, out UnityEngine.AI.NavMeshHit hit, 1.2f, UnityEngine.AI.NavMesh.AllAreas), pickup.name + " lies by the floor she walks");
            }
            Debug.Log($"[DemoTests] capsules: {string.Join(", ", counts.Select(c => $"{c.Key} {c.Value}"))}");
            Assert.GreaterOrEqual(counts[DrugType.Nalcon], 3);
            Assert.GreaterOrEqual(counts[DrugType.NalconFire], 3);
            Assert.GreaterOrEqual(counts[DrugType.NalconLift], 3);
        }

        [UnityTest]
        public IEnumerator TheFireteam_Calls_ItsLosses_AndTheLastOneFightsAlone()
        {
            yield return LoadWorld();
            SoldierBrain[] team = null;
            yield return InG2((t, s) => team = t);
            yield return WaitUntil(() => team.All(b => b.Team != null), 8f);
            FireTeam fire = team[0].Team;
            Assert.IsNotNull(fire);
            yield return new WaitForSeconds(3f);
            team = fire.Members.ToArray();
            int Lines() => team.Sum(b => Bark.CountOf(b.gameObject));
            for (int alive = team.Length - 1; alive >= 1; alive--)
            {
                SoldierBrain victim = team.First(b => b.Current != SoldierBrain.Phase.Dead && !b.Retreating);
                int before = Lines();
                victim.GetComponent<Health>().TakeDamage(9999f);
                yield return new WaitForSeconds(0.5f);
                fire.Tick();
                Assert.AreEqual(alive, fire.Alive, "the team counts its dead");
                Assert.Greater(Lines(), before, $"{alive} left: a call");
            }
            SoldierBrain last = team.First(b => b.Current != SoldierBrain.Phase.Dead);
            Assert.IsTrue(fire.MayMove(last), "the last one moves as he must: no turns");
        }

        [UnityTest]
        public IEnumerator TheBulletin_ClosesByItself_WithNoKey()
        {
            // Bruno, 2026-11-05: "it needs to exit after the demo is complete". The card waited for E, and no hint said so.
            yield return Coda("X1", "demo_crown_gone", "demo_readout_on");
            GameObject.FindGameObjectWithTag("Player").GetComponent<TankController>().Teleport(GameObject.Find("Entry_X1").transform.position, 45f);
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "X1", 3f);
            StoryFlags.Raise("demo_car_door");
            yield return WaitUntil(() => Bulletin.IsShowing, 15f);
            Assert.IsTrue(Bulletin.IsShowing);
            float t0 = Time.realtimeSinceStartup;
            yield return WaitUntil(() => !Bulletin.IsShowing, 30f);
            Debug.Log($"[DemoTests] the card closes {Time.realtimeSinceStartup - t0:0.0} s after it shows");
            Assert.IsFalse(Bulletin.IsShowing, "it closes by itself");
        }

        private static void KillThree()
        {
            foreach (SoldierBrain brain in UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).Where(b => b.name.StartsWith("Scientist_") && b.name != "Scientist_Knife").ToArray())
                brain.GetComponent<Health>().TakeDamage(9999f);
        }

        [UnityTest]
        public IEnumerator TheFeed_OpensByACutscene_WhenTheThreeDie_AndASkipStillOpensIt()
        {
            // Bruno, 2026-11-05: "the unlocking oif the eating/feeding should be a cutscene; i missed the log note".
            yield return LoadWorld();
            yield return WaitUntil(() => CutscenePlayer.IsPlaying && CutscenePlayer.ShotIndex >= 0, 10f);
            CutscenePlayer.Skip();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake"), 10f);
            Assert.IsNull(GameObject.Find("L9_Care_Chart"), "no chart on the floor to miss");
            Assert.IsTrue(Feeding.Locked, "the feed starts locked");
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            lucy.GetComponent<TankController>().Teleport(new Vector3(20f, 0f, 12.5f), 90f); // W2, far from the three
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "W2", 3f);
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(CutscenePlayer.IsPlaying, "they live: no cutscene");
            Assert.IsTrue(Feeding.Locked);
            KillThree();
            yield return WaitUntil(() => CutscenePlayer.IsPlaying, 6f);
            Assert.AreEqual("02b_the_chart", CutscenePlayer.Current, "the three are dead: the chart");
            Assert.IsTrue(Feeding.Locked, "it opens at the end of the cutscene");
            CutscenePlayer.Skip();
            yield return WaitUntil(() => StoryFlags.Has("demo_w2_chart"), 5f);
            Assert.IsFalse(Feeding.Locked, "a skip still opens the feed");
            Assert.IsTrue(lucy.GetComponent<GlorifiedBody.Interaction.ItemInventory>().Has("l9_care_chart"), "the chart is in her items, to read again");
        }

        [UnityTest]
        public IEnumerator TheFeed_OpensInW5_WhenSheRunsPastTheThree_AndPlaysOnce()
        {
            yield return LoadWorld();
            yield return WaitUntil(() => CutscenePlayer.IsPlaying && CutscenePlayer.ShotIndex >= 0, 10f);
            CutscenePlayer.Skip();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake"), 10f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            TankController tank = lucy.GetComponent<TankController>();
            tank.Teleport(new Vector3(1.5f, 0f, 12.5f), 180f); // W2 at the W5 door
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "W2", 3f);
            yield return null;
            tank.Teleport(new Vector3(1.5f, 0f, 10.1f), 180f);
            yield return WaitUntil(() => CutscenePlayer.IsPlaying, 4f);
            Assert.AreEqual("02b_the_chart", CutscenePlayer.Current, "she ran past them: the chart in W5");
            CutscenePlayer.Skip();
            yield return WaitUntil(() => !Feeding.Locked, 5f);
            Assert.IsFalse(Feeding.Locked, "the feed opens with the three alive");
            yield return WaitUntil(() => StoryFlags.Has("demo_w2_chart"), 3f);
            KillThree();
            yield return new WaitForSeconds(3f);
            Assert.AreNotEqual("02b_the_chart", CutscenePlayer.IsPlaying ? CutscenePlayer.Current : "", "their death later does not play it again");
        }

        [UnityTest]
        public IEnumerator TheSoldierWithAShield_IsASoldier_HisShieldStopsAFrontPower()
        {
            // Bruno, 2026-11-05: "shield dude also needs to have a soldier variant". The pair of G2 had a guard with a shield.
            yield return LoadWorld();
            SoldierBrain soldier = null;
            yield return WaitUntil(() => (soldier = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == "Soldier_Pair2_B")) != null, 5f);
            Assert.IsNotNull(soldier);
            Assert.AreEqual(EnemyMorale.Rank.Soldier, soldier.GetComponent<EnemyMorale>().RankOf, "the rank of a soldier: he never runs");
            Assert.AreEqual(SoldierBrain.Approach.Flank, soldier.Style, "he flanks, as soldiers do");
            GlorifiedBody.Combat.IFrontGuard shield = soldier.GetComponent<GlorifiedBody.Combat.IFrontGuard>();
            Assert.IsNotNull(shield, "he has the shield");
            Assert.AreEqual(DummyShooter.Weapon.Pistol, soldier.Gun.WeaponKind, "one hand for the pistol");
            yield return null;
            Vector3 front = soldier.transform.position + soldier.transform.forward * 3f;
            Vector3 back = soldier.transform.position - soldier.transform.forward * 3f;
            Assert.IsTrue(shield.Blocks(front), "a power from his front stops");
            Assert.IsFalse(shield.Blocks(back), "a power from his back goes in");
        }

        [UnityTest]
        public IEnumerator TheCapsules_FallInTheTalk_SheCanTakeThem_ARetryKeepsTheTakenOneGone()
        {
            // Cutscene 02, note 1: the capsules roll off his tray, and the player can take them after the fight.
            yield return LoadWorld();
            yield return WaitUntil(() => CutscenePlayer.IsPlaying && CutscenePlayer.ShotIndex >= 0, 10f);
            CutscenePlayer.Skip();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake"), 10f);
            GlorifiedBody.Interaction.ItemPickup[] All() => UnityEngine.Object.FindObjectsByType<GlorifiedBody.Interaction.ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GlorifiedBody.Interaction.ItemPickup a = All().First(p => p.name == "Capsule_W1B_A");
            GlorifiedBody.Interaction.ItemPickup b = All().First(p => p.name == "Capsule_W1B_B");
            Assert.IsFalse(a.gameObject.activeInHierarchy || b.gameObject.activeInHierarchy, "on his tray before the talk");
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            ScriptedSequence meet = GameObject.Find("W1B_Scientist").GetComponent<ScriptedSequence>();
            lucy.GetComponent<TankController>().Teleport(new Vector3(4f, 0f, 17.9f), 180f);
            yield return WaitUntil(() => CutscenePlayer.IsPlaying, 8f);
            Assert.IsTrue(a.gameObject.activeInHierarchy && b.gameObject.activeInHierarchy, "they fall under the cut");
            CutscenePlayer.Skip();
            yield return WaitUntil(() => meet.Done, 10f);
            foreach (GlorifiedBody.Interaction.ItemPickup capsule in new[] { a, b })
            {
                Assert.AreEqual("W1B", GlorifiedBody.Rooms.Room.At(capsule.transform.position).id, capsule.name + " lies in W1B");
                Assert.AreEqual(0.15f, capsule.transform.position.y, 0.01f, capsule.name + " lies on the floor");
                Assert.IsTrue(capsule.Available, capsule.name + " can be taken");
            }
            a.Confirm(lucy.GetComponent<GlorifiedBody.Interaction.Interactor>());
            Assert.IsTrue(a.Taken, "she takes one");
            lucy.GetComponent<TankController>().Teleport(new Vector3(13f, 0f, 12.5f), 0f); // W2: the retry point moves there
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "W2", 3f);
            yield return null;
            GameManager.Instance.RestartLevel();
            yield return WaitUntil(() => GameManager.Instance.IsPlaying && GlorifiedBody.Rooms.RoomTracker.CurrentId == "W2", 15f);
            yield return new WaitForSeconds(0.3f);
            b = All().First(p => p.name == "Capsule_W1B_B");
            Assert.IsNull(All().FirstOrDefault(p => p.name == "Capsule_W1B_A" && p.gameObject.activeInHierarchy), "the taken capsule stays gone after a retry");
            Assert.IsTrue(b.gameObject.activeInHierarchy && b.Available, "the other one still lies there");
        }

        [UnityTest]
        public IEnumerator SheWalks_FromHerStart_ThroughTheDoorway_AndTheTalkStartsOnlyInTheNextRoom()
        {
            // The real walk on the pad, because Bruno met the talk twice before he left the room (2026-11-04, 2026-11-05).
            yield return LoadWorld();
            yield return WaitUntil(() => CutscenePlayer.IsPlaying && CutscenePlayer.ShotIndex >= 0, 10f);
            CutscenePlayer.Skip();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake"), 10f);
            yield return new WaitForSeconds(0.5f);
            ScriptedSequence meet = GameObject.Find("W1B_Scientist").GetComponent<ScriptedSequence>();
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            var pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            var driver = new GameObject("[Pad]").AddComponent<PadDriver>();
            UnityEngine.Object.DontDestroyOnLoad(driver.gameObject);
            driver.pad = pad;
            pad.SetHeld(InputManager.GameAction.Forward, true);
            float t0 = Time.time;
            float wallAt = -1f;
            while (!meet.Running && Time.time - t0 < 15f)
            {
                if (wallAt < 0f && lucy.transform.position.z < 19f) wallAt = Time.time - t0;
                Assert.IsTrue(lucy.transform.position.z < 19f || !meet.Done, "no talk in W1");
                yield return null;
            }
            float walked = Time.time - t0;
            pad.SetHeld(InputManager.GameAction.Forward, false);
            GameInput.ClearBotSource(pad);
            UnityEngine.Object.Destroy(driver.gameObject);
            Debug.Log($"[DemoTests] the walk: the doorway after {wallAt:0.0} s, the talk after {walked:0.0} s, at z {lucy.transform.position.z:0.00}");
            Assert.IsTrue(meet.Running, "she walks into the beat");
            Assert.AreEqual("W1B", GlorifiedBody.Rooms.RoomTracker.CurrentId, "she stands in the next room");
            Assert.Less(lucy.transform.position.z, 19f, "past the wall line");
            Assert.GreaterOrEqual(wallAt, 2f, "seconds of walk in the quiet room, in a straight line");
        }

        [UnityTest]
        public IEnumerator TheScientist_InTheNextRoom_TalksFirst_ThenAttacks_TheThreeStayBehindTheShutDoor()
        {
            // Bruno, 2026-11-05: "the one knife scientis is in one room, and the three in the other one. the door should be close
            // so that the other 3 won't bum rush the player at once; also, one has a pistol and two have a knife". And 2026-11-02:
            // "it should do the dialogue then he attacks her".
            yield return LoadWorld();
            yield return WaitUntil(() => CutscenePlayer.IsPlaying && CutscenePlayer.ShotIndex >= 0, 10f);
            CutscenePlayer.Skip();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake"), 10f);
            ScriptedSequence meet = GameObject.Find("W1B_Scientist").GetComponent<ScriptedSequence>();
            GlorifiedBody.Interaction.FireDoor door = GlorifiedBody.Interaction.FireDoor.All.First(d => d.name == "FireDoor_W1B_W2");
            GlorifiedBody.Interaction.DoorLever[] levers = door.GetComponentsInChildren<GlorifiedBody.Interaction.DoorLever>();
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            TankController tank = lucy.GetComponent<TankController>();
            Assert.AreEqual(GlorifiedBody.Interaction.FireDoor.State.Jammed, door.Current, "the door to W2 is shut at the start");
            Assert.IsTrue(levers.All(l => !l.Available), "her lever does nothing before the talk");

            SoldierBrain[] all = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None);
            SoldierBrain[] three = new[] { "Scientist_A", "Scientist_B", "Scientist_C" }.Select(n => all.First(b => b.name == n)).ToArray();
            Assert.AreEqual(1, three.Count(b => b.Gun.WeaponKind == DummyShooter.Weapon.Pistol), "one pistol");
            Assert.AreEqual(2, three.Count(b => b.Gun.WeaponKind == DummyShooter.Weapon.Knife), "two knives");
            SoldierBrain knife = all.First(b => b.name == "Scientist_Knife");
            Assert.IsTrue(three.All(b => GlorifiedBody.Rooms.Room.At(b.transform.position + Vector3.up * 0.1f).id == "W2"), "the three stand in W2");

            tank.Teleport(new Vector3(4f, 0f, 18.6f), 180f); // through the doorway, before the line
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual("W1B", GlorifiedBody.Rooms.RoomTracker.CurrentId, "in the next room");
            Assert.IsFalse(meet.Running || meet.Done, "a step past the doorway: no talk yet");
            tank.Teleport(new Vector3(4f, 0f, 17.9f), 180f);
            yield return WaitUntil(() => meet.Running, 3f);
            Assert.IsTrue(meet.Running, "at the line: the beat starts");
            int alertStep = meet.steps.FindIndex(s => s.kind == StepKind.Alert);
            Assert.Greater(alertStep, 0);
            string early = null;
            while (meet.Running && meet.StepIndex < alertStep)
            {
                if (knife.Alerted && early == null) early = $"alerted at step {meet.StepIndex} of {alertStep} ({meet.steps[meet.StepIndex].kind} {meet.steps[meet.StepIndex].text})";
                if (CutscenePlayer.IsPlaying)
                {
                    Assert.AreEqual("02_the_scientist", CutscenePlayer.Current);
                    Assert.IsFalse(three.Any(b => b.Alerted), "the three stay unaware during the talk");
                    Assert.AreEqual(GlorifiedBody.Interaction.FireDoor.State.Jammed, door.Current, "the door stays shut during the talk");
                    Vector3 chest = Vector3.up * GlorifiedBody.Characters.Body.ChestHeight;
                    Assert.IsFalse(Physics.Linecast(lucy.transform.position + chest, knife.transform.position + chest, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore), "he stands out from behind the screen when he talks");
                    Camera view = Camera.main;
                    foreach (Vector3 body in new[] { lucy.transform.position + chest, knife.transform.position + chest })
                        Assert.IsFalse(Physics.Linecast(view.transform.position, body, LayerMask.GetMask("Default"), QueryTriggerInteraction.Ignore), "nothing hides them from the camera of the talk");
                    CutscenePlayer.Skip();
                }
                yield return null;
            }
            Assert.IsNull(early, "he talks first, he attacks after the last line");
            yield return WaitUntil(() => meet.Done, 10f);
            Assert.IsTrue(meet.Done, "the beat ends");
            Debug.Log($"[DemoTests] after the talk: the knife at {knife.transform.position}, her at {lucy.transform.position}, the door {door.Current}");
            Assert.AreEqual(GlorifiedBody.Interaction.FireDoor.State.Jammed, door.Current, "the door stays shut for the first fight");
            Assert.AreEqual("W1B", GlorifiedBody.Rooms.Room.At(knife.transform.position + Vector3.up * 0.1f).id, "he fights her in W1B");
            Assert.IsTrue(knife.Alerted, "he attacks");
            yield return new WaitForSeconds(2f);
            Assert.IsFalse(three.Any(b => b.Alerted), "the three do not come: they saw nothing, and he called nobody");
            Assert.IsTrue(three.All(b => GlorifiedBody.Rooms.Room.At(b.transform.position + Vector3.up * 0.1f).id == "W2"), "they stay in W2");
            Assert.IsTrue(levers.All(l => l.Available), "her lever works after the talk");
            levers.First(l => l.name == "Lever_In").Confirm(lucy.GetComponent<GlorifiedBody.Interaction.Interactor>());
            Assert.AreEqual(GlorifiedBody.Interaction.FireDoor.State.Open, door.Current, "her lever opens the way on");
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
            public const string CodeSilver = "code_silver"; // DemoWard.CodeSilver
            public const string CodesRotated = "demo_codes_rotated", CardEnrolled = "demo_card_enrolled"; // DemoWard
        }

        private const string SectorCReleased = "sector_c_released"; // DemoPublic.Released (editor code)
        private const string Reinforced = "demo_g5_reinforced";     // DemoPublic.Reinforced

        [UnityTest]
        public IEnumerator EveryCardDoor_SlidesItsDrawnLeaf_WhenItOpens()
        {
            // Bruno, 2026-11-07: "ward's sister card; door won't appear open but you can go pass throught it".
            yield return LoadWorld();
            var doors = UnityEngine.Object.FindObjectsByType<LockedDoor>(FindObjectsSortMode.None);
            Assert.IsNotEmpty(doors, "the card doors of the demo");
            foreach (LockedDoor door in doors)
            {
                Renderer leaf = door.leaf.GetComponent<Renderer>();
                Assert.IsFalse(leaf.isPartOfStaticBatch, $"{door.name}: a static batch draws the leaf at its first place");
                Vector3 before = leaf.bounds.center;
                door.Open(instant: true);
                Assert.AreEqual(0f, Vector3.Distance(before + door.openOffset, leaf.bounds.center), 0.01f, $"{door.name}: the drawn leaf slides with the door");
            }
        }

        [UnityTest]
        public IEnumerator TheCull_DrawsOnlyTheRoomsTheCameraSees_AndHers()
        {
            // Bruno, 2026-11-07: "cull rendering to what the player can see; this is easy due to the fixed camera angles".
            yield return LoadWorld();
            var rooms = GlorifiedBody.Rooms.Room.All.ToList();
            var stale = new List<string>();
            foreach (FixedCamera cam in UnityEngine.Object.FindObjectsByType<FixedCamera>(FindObjectsSortMode.None))
            {
                GlorifiedBody.Rooms.Room own = cam.GetComponentInParent<GlorifiedBody.Rooms.Room>();
                if (own == null) continue;
                var now = GlorifiedBody.Rooms.RoomSight.Scan(cam, own, rooms).Seen.Select(r => r.id).OrderBy(id => id, StringComparer.Ordinal);
                if (cam.sees == null || !now.SequenceEqual(cam.sees)) stale.Add($"{cam.name}: stored [{string.Join(",", cam.sees ?? new string[0])}], now [{string.Join(",", now)}]");
            }
            Assert.IsEmpty(stale, "each camera holds its sight list (DemoWorld.Sight): " + string.Join("\n", stale.Take(5)));

            // Each camera of three rooms on both floors: only its rooms and hers draw.
            TankController tank = GameObject.FindGameObjectWithTag("Player").GetComponent<TankController>();
            foreach (string id in new[] { "W2", "G2", "G5" })
            {
                GlorifiedBody.Rooms.Room room = GlorifiedBody.Rooms.Room.Of(id);
                tank.Teleport(FloorPoint(room), 0f);
                yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.Current == room, 2f);
                foreach (FixedCamera cam in room.GetComponentsInChildren<FixedCamera>())
                {
                    FixedCameraDirector.Instance.Override(cam);
                    yield return null;
                    yield return null;
                    var want = new HashSet<string>(cam.sees) { id };
                    CollectionAssert.AreEquivalent(want, GlorifiedBody.Rooms.RoomTracker.Shown, $"{cam.name}: the rooms drawn");
                    foreach (GlorifiedBody.Rooms.Room other in rooms)
                    {
                        Transform floor = other.transform.Find("[Room]/Floor");
                        if (floor == null) continue;
                        Assert.AreEqual(want.Contains(other.id), floor.GetComponent<Renderer>().enabled, $"{cam.name}: the floor of {other.id}");
                    }
                }
            }
            FixedCameraDirector.Instance.Override(null);
            Debug.Log($"[RoomSight] {GlorifiedBody.Rooms.RoomTracker.Instance.CulledCount} renderers off with the last camera");
        }

        [UnityTest]
        public IEnumerator NoCamera_SeesAnotherRoom_ExceptThroughADoorway()
        {
            // Bruno, 2026-11-07: "in some areas the walls are not fully built, so i can see through scenarion; ... auto build
            // logic needs to be verified to avoid such gaps".
            yield return LoadWorld();
            var rooms = GlorifiedBody.Rooms.Room.All.ToList();
            var failures = new List<string>();
            // The scan stops a ray at the ceiling: each room must have one over its whole plan, at its height.
            foreach (GlorifiedBody.Rooms.Room room in rooms)
            {
                Transform ceiling = room.transform.Find("[Room]/Ceiling");
                Renderer r = ceiling != null ? ceiling.GetComponent<Renderer>() : null;
                if (r == null) { failures.Add($"{room.id}: no ceiling"); continue; }
                Bounds b = r.bounds;
                Vector3 p = room.transform.position;
                bool covers = b.min.x <= p.x + room.rect.x + 0.01f && b.max.x >= p.x + room.rect.z - 0.01f && b.min.z <= p.z + room.rect.y + 0.01f && b.max.z >= p.z + room.rect.w - 0.01f;
                if (!covers || Mathf.Abs(b.min.y - GlorifiedBody.Rooms.RoomSight.CeilingY(room)) > 0.05f)
                    failures.Add($"{room.id}: the ceiling {b.min} to {b.max} does not cover the plan at {GlorifiedBody.Rooms.RoomSight.CeilingY(room):0.00}");
            }
            int cameras = 0, rays = 0;
            foreach (FixedCamera cam in UnityEngine.Object.FindObjectsByType<FixedCamera>(FindObjectsSortMode.None).OrderBy(c => c.name))
            {
                GlorifiedBody.Rooms.Room own = cam.GetComponentInParent<GlorifiedBody.Rooms.Room>();
                if (own == null) continue;
                cameras++;
                if (cam.transform.position.y > GlorifiedBody.Rooms.RoomSight.CeilingY(own) - 0.05f)
                    failures.Add($"{cam.name} ({own.id}): the camera at {cam.transform.position.y:0.00} is over the ceiling at {GlorifiedBody.Rooms.RoomSight.CeilingY(own):0.00}");
                GlorifiedBody.Rooms.RoomSight.Result sight = GlorifiedBody.Rooms.RoomSight.Scan(cam, own, rooms);
                rays += sight.Rays;
                if (sight.Gaps.Count == 0) continue;
                var where = sight.Gaps.GroupBy(g => g.what).OrderByDescending(g => g.Count()).Take(3)
                    .Select(g => $"{g.Count()} rays to {g.Key}, out at {g.First().exit.x:0.0},{g.First().exit.y:0.0},{g.First().exit.z:0.0}");
                failures.Add($"{cam.name} ({own.id}): {sight.Gaps.Count} of {sight.Rays} rays: {string.Join("; ", where)}");
            }
            foreach (string f in failures) Debug.Log("[RoomSight] gap " + f);
            Debug.Log($"[RoomSight] {cameras} cameras, {rays} rays, {failures.Count} cameras with a gap");
            Assert.Greater(cameras, 40, "the demo cameras");
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator TheCarers_LookForABody_AFewTimesASecond_NotEachFrame()
        {
            // 2026-11-07: 33 carers that searched the scene each frame took 7 ms of a demo frame (TheFrame_Profile).
            yield return LoadWorld();
            if (CutscenePlayer.IsPlaying) CutscenePlayer.Skip();
            yield return WaitUntil(() => !CutscenePlayer.IsPlaying && GameManager.Instance.IsPlaying, 10f);
            int carers = UnityEngine.Object.FindObjectsByType<CorpseCare>(FindObjectsSortMode.None).Length;
            Assert.Greater(carers, 10, "the demo has its carers");
            int before = CorpseCare.Searches, frames = 0;
            float t0 = Time.time;
            while (Time.time - t0 < 2f) { frames++; yield return null; }
            int searches = CorpseCare.Searches - before;
            float most = carers * (2f * 4f + 2f); // about four searches a second for each carer
            Debug.Log($"[CareSearch] {searches} searches by {carers} carers in {frames} frames");
            Assert.LessOrEqual(searches, most, $"{carers} carers in {frames} frames: at most {most:0} searches");
        }

        /// <summary>
        /// A measurement, not a check (Bruno, 2026-11-07: "it seems to be locked at 30 fps or lower"): the costliest profiler
        /// markers of a demo frame, then the frame with the figures off. It runs only when the filter names it.
        /// </summary>
        [UnityTest, Explicit]
        public IEnumerator TheFrame_Profile()
        {
            yield return LoadWorld();
            if (CutscenePlayer.IsPlaying) CutscenePlayer.Skip();
            yield return WaitUntil(() => !CutscenePlayer.IsPlaying && GameManager.Instance.IsPlaying, 10f);
            yield return new WaitForSecondsRealtime(3f);

            var handles = new List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
            Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
            var recorders = new List<(string name, Unity.Profiling.ProfilerRecorder rec)>();
            foreach (var h in handles)
            {
                var d = Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(h);
                if (d.UnitType != Unity.Profiling.ProfilerMarkerDataUnit.TimeNanoseconds) continue;
                recorders.Add((d.Category.Name + "/" + d.Name, Unity.Profiling.ProfilerRecorder.StartNew(d.Category, d.Name, 160)));
            }
            float t0 = Time.realtimeSinceStartup;
            for (int i = 0; i < 150; i++) yield return null;
            float withAll = (Time.realtimeSinceStartup - t0) * 1000f / 150f;
            var rows = new List<(string name, double ms)>();
            foreach (var (name, rec) in recorders)
            {
                long sum = 0;
                int n = rec.Count;
                for (int i = 0; i < n; i++) sum += rec.GetSample(i).Value;
                if (n > 0) rows.Add((name, sum / 1e6 / 150.0));
                rec.Dispose();
            }
            foreach (var (name, ms) in rows.OrderByDescending(r => r.ms).Take(60))
                Debug.Log($"[FrameProfile] {ms,8:0.000} ms  {name}");

            // The cost of each script type on the figures: all of one type off for 90 frames, against the frame with all on.
            var bodies = UnityEngine.Object.FindObjectsByType<Health>(FindObjectsSortMode.None)
                .Where(h => !h.CompareTag("Player") && h.GetComponentInParent<LockOnTargetable>() != null).Select(h => h.gameObject).ToList();
            var byType = bodies.SelectMany(b => b.GetComponentsInChildren<MonoBehaviour>()).Where(m => m != null && m.enabled)
                .GroupBy(m => m.GetType().Name).ToList();
            IEnumerator Measure(Action<float> result)
            {
                for (int i = 0; i < 20; i++) yield return null;
                float s = Time.realtimeSinceStartup;
                for (int i = 0; i < 90; i++) yield return null;
                result((Time.realtimeSinceStartup - s) * 1000f / 90f);
            }
            float baseMs = 0f;
            yield return Measure(v => baseMs = v);
            var costs = new List<(string type, int count, float saved)>();
            foreach (var group in byType)
            {
                foreach (MonoBehaviour m in group) if (m != null) m.enabled = false;
                float off = 0f;
                yield return Measure(v => off = v);
                foreach (MonoBehaviour m in group) if (m != null) m.enabled = true;
                costs.Add((group.Key, group.Count(), baseMs - off));
            }
            foreach (var (type, count, saved) in costs.OrderByDescending(c => c.saved))
                Debug.Log($"[FrameProfile] type {saved,6:0.00} ms saved  x{count}  {type}");

            int figures = 0;
            foreach (Health h in UnityEngine.Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (h.CompareTag("Player")) continue;
                if (h.GetComponentInParent<LockOnTargetable>() == null) continue;
                h.gameObject.SetActive(false);
                figures++;
            }
            yield return new WaitForSecondsRealtime(1f);
            t0 = Time.realtimeSinceStartup;
            for (int i = 0; i < 150; i++) yield return null;
            float without = (Time.realtimeSinceStartup - t0) * 1000f / 150f;
            Debug.Log($"[FrameProfile] frame {withAll:0.0} ms with everything, {without:0.0} ms with {figures} figures off, {GlorifiedBody.Rooms.Room.All.Count} rooms");
        }

        private static IEnumerator LoadWorld()
        {
            GameManager.Instance.LoadLevel(World);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == World && GameManager.Instance.IsPlaying, 30f);
            yield return null;
            Assert.AreEqual(World, SceneManager.GetActiveScene().name, "the world scene is in the build (DemoWorld.BuildAll)");
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.Current != null, 3f);
        }

        /// <summary>A floor point of the room clear of its props, nearest its middle.</summary>
        private static Vector3 FloorPoint(GlorifiedBody.Rooms.Room room)
        {
            int solid = LayerMask.GetMask("Default");
            Vector3 c = room.Centre;
            for (float r = 0f; r < 20f; r += 0.5f)
                for (int k = 0; k < 8; k++)
                {
                    Vector3 top = new Vector3(c.x + Mathf.Cos(k * Mathf.PI / 4f) * r, room.BaseY + 2.5f, c.z + Mathf.Sin(k * Mathf.PI / 4f) * r);
                    if (!room.Contains(new Vector3(top.x, room.BaseY, top.z), -0.5f)) continue;
                    if (!Physics.Raycast(top, Vector3.down, out RaycastHit hit, 10f, solid, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.CheckCapsule(hit.point + Vector3.up * 0.4f, hit.point + Vector3.up * 1.5f, 0.35f, solid, QueryTriggerInteraction.Ignore)) continue;
                    return hit.point;
                }
            return c;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
