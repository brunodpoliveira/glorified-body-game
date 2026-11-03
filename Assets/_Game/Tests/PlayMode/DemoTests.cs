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
        private static readonly string[] Rooms = { "W1", "W2", "W3", "W4", "W5", "W6", "W7", "W8", "W9", "W10", "W11", "W12", "S1", "G1", "G2", "G3", "G4", "G5", "G6", "G7", "G8", "G9", "U1" };
        private static readonly string[] MainWay = { "W1", "W2", "W5", "W7", "W8", "W9", "W11", "W9", "W12", "S1", "G1", "G2", "G5", "G9", "G5", "U1" };

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
        public IEnumerator TheFireDoors_StandAtTheThreeChokepoints_TheirLeversInTheRooms_TheirLeavesInTheWall()
        {
            // Bruno, 2026-11-03: fire doors "in selected chokepoints"; his pick: W2 to W5, W5 to W7, G2 to G5.
            yield return LoadWorld();
            var names = GlorifiedBody.Interaction.FireDoor.All.Select(d => d.name).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "FireDoor_G2_G5", "FireDoor_W2_W5", "FireDoor_W5_W7" }, names, "three fire doors");
            var failures = new List<string>();
            int solid = LayerMask.GetMask("Default");
            foreach (GlorifiedBody.Interaction.FireDoor door in GlorifiedBody.Interaction.FireDoor.All)
            {
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
            // The wake is an FMV slot (Bruno, 2026-11-02): it starts after a breath and a fade.
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
        public IEnumerator W1_TheScientist_ComesInFromOutside_TalksFirst_ThenAttacks()
        {
            // Bruno, 2026-11-02: "the dude just pops into the room and begins attacking her immediately (even as the
            // dialogue continues) ... it should do the dialogue then he attacks her".
            yield return LoadWorld();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake"), 120f);
            Assert.IsTrue(StoryFlags.Has("demo_w1_wake"), "the wake ends");
            ScriptedSequence meet = GameObject.Find("W1_Scientist").GetComponent<ScriptedSequence>();
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponent<TankController>().Teleport(new Vector3(4f, 0f, 15.8f), 180f);
            yield return WaitUntil(() => meet.Running, 3f);
            Assert.IsTrue(meet.Running, "she nears the door: the beat starts");
            GameObject scientist = null;
            yield return WaitUntil(() => (scientist = GameObject.Find("Scientist_Knife")) != null && scientist.GetComponent<GlorifiedBody.Enemy.SoldierBrain>() != null, 3f);
            Assert.IsNotNull(scientist, "the scientist spawns");
            float startZ = scientist.transform.position.z;
            var brain = scientist.GetComponent<GlorifiedBody.Enemy.SoldierBrain>();
            int alertStep = meet.steps.FindIndex(s => s.kind == StepKind.Alert);
            Assert.Greater(alertStep, 0);
            string early = null;
            while (meet.Running && meet.StepIndex < alertStep)
            {
                if (brain.Alerted && early == null) early = $"alerted at step {meet.StepIndex} of {alertStep} ({meet.steps[meet.StepIndex].kind} {meet.steps[meet.StepIndex].text})";
                yield return null;
            }
            Assert.IsNull(early, "he talks first, he attacks after the last line");
            Assert.Less(startZ, 14f - DemoRoomWall, "he starts outside the room, in the corridor W2 behind the door (the cutscene: he stops in the doorway)");
            Assert.GreaterOrEqual(scientist.transform.position.z, 14f - 0.2f, "by the last line he stands in the doorway or in the room");
            yield return null;
            Assert.IsTrue(brain.Alerted, "then he attacks");
        }

        private const float DemoRoomWall = 0.3f; // DemoRoom.Wall (editor code)

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
