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
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
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
        private static readonly string[] Rooms = { "W1", "W1B", "W2", "W3", "W4", "W5", "W6", "W7", "W8", "W9", "W10", "W11", "W12", "S1", "G1", "G2", "G3", "G4", "G5", "G6", "G7", "G8", "G9", "U1" };
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
                    foreach (GlorifiedBody.Rooms.Room other in GlorifiedBody.Rooms.Room.All)
                    {
                        Renderer floor = other.transform.Find("[Room]")?.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name.EndsWith("Floor"));
                        if (floor == null) continue;
                        bool drawn = other == room || room.IsNeighbour(other.id);
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
            CollectionAssert.AreEqual(new[] { "FireDoor_G2_G5", "FireDoor_W1B_W2", "FireDoor_W2_W5", "FireDoor_W5_W7" }, names, "the fire doors");
            var failures = new List<string>();
            int solid = LayerMask.GetMask("Default");
            foreach (GlorifiedBody.Interaction.FireDoor door in GlorifiedBody.Interaction.FireDoor.All)
            {
                // The door of W1B to W2 starts shut (Bruno, 2026-11-05). The others stand open.
                if (door.name == "FireDoor_W1B_W2") door.Open("test", instant: true);
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
                if (rooms.Count != 2) failures.Add($"{door.name}: its levers in {rooms.Count} rooms ({string.Join(",", rooms)})");
                // The open leaf lies in its wall pocket: a line to it from each side meets the wall.
                Vector3 leafCentre = door.leaf.position + Vector3.up * 1.1f;
                foreach (float side in new[] { -1f, 1f })
                    if (!Physics.Linecast(leafCentre + door.outward * side * 1f, leafCentre, solid, QueryTriggerInteraction.Ignore))
                        failures.Add($"{door.name}: the open leaf shows on the {(side > 0 ? "outer" : "inner")} side");
            }
            Assert.IsEmpty(failures, string.Join("; ", failures));
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
            // switches room". W3's door to W2: the south wall (z 14), at x 13.
            yield return LoadWorld();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake"), 120f);
            int loads = 0;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> count = (scene, mode) => loads++;
            SceneManager.sceneLoaded += count;
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponent<TankController>().Teleport(new Vector3(13f, 0f, 15.2f), 180f);
            yield return WaitUntil(() => GlorifiedBody.Rooms.RoomTracker.CurrentId == "W3", 3f);
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
            Assert.IsTrue(GlorifiedBody.Subtitles.SubtitleService.Lines.Any(l => l.Speaker == "STEVENS"), "Stevens on the speaker (cutscene 01: the FMV slot shows his lines)");
            Assert.IsNotNull(GameObject.Find("L1_Printout"), "the printout on the printer");
        }

        [UnityTest]
        public IEnumerator U1_TheEnd_TheGrabTearsTheUniformOff_TheBulletin_ThenTheTitle()
        {
            yield return LoadWorld();
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            GlorifiedBody.Interaction.OutfitChange.Change(lucy, GlorifiedBody.Interaction.OutfitChange.Load(GlorifiedBody.Interaction.OutfitChange.GuardId));
            lucy.GetComponent<TankController>().Teleport(GlorifiedBody.Rooms.Room.Of("U1").transform.position + new Vector3(20f, 0f, -25.5f), 180f); // U1's plan point, on floor 1
            yield return WaitUntil(() => Bulletin.IsShowing, 60f);
            Assert.IsTrue(Bulletin.IsShowing, "the bulletin after the exit lines");
            Assert.AreEqual(GlorifiedBody.Interaction.OutfitChange.NudeId, lucy.GetComponent<PsychicStats>().Outfit.outfitId, "round 20: the grab tears the uniform off");
            var pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            var driver = new GameObject("[Pad]").AddComponent<PadDriver>();
            UnityEngine.Object.DontDestroyOnLoad(driver.gameObject);
            driver.pad = pad;
            float t0 = Time.realtimeSinceStartup;
            while (SceneManager.GetActiveScene().name != "Main_Menu" && Time.realtimeSinceStartup - t0 < 20f)
            {
                pad.Tap(InputManager.GameAction.Interact);
                yield return new WaitForSecondsRealtime(0.4f);
            }
            GameInput.ClearBotSource(pad);
            UnityEngine.Object.Destroy(driver.gameObject);
            Assert.AreEqual("Main_Menu", SceneManager.GetActiveScene().name, "the title: back to the menu");
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
        public IEnumerator TheBulletin_ClosesByItself_ToTheTitle_WithNoKey()
        {
            // Bruno, 2026-11-05: "it needs to exit after the demo is complete". The card waited for E, and no hint said so.
            yield return LoadWorld();
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            GlorifiedBody.Interaction.OutfitChange.Change(lucy, GlorifiedBody.Interaction.OutfitChange.Load(GlorifiedBody.Interaction.OutfitChange.GuardId));
            lucy.GetComponent<TankController>().Teleport(GlorifiedBody.Rooms.Room.Of("U1").transform.position + new Vector3(20f, 0f, -25.5f), 180f);
            yield return WaitUntil(() => Bulletin.IsShowing, 60f);
            Assert.IsTrue(Bulletin.IsShowing);
            float t0 = Time.realtimeSinceStartup;
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu", 45f);
            Debug.Log($"[DemoTests] the title {Time.realtimeSinceStartup - t0:0.0} s after the card");
            Assert.AreEqual("Main_Menu", SceneManager.GetActiveScene().name, "the demo ends by itself");
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
