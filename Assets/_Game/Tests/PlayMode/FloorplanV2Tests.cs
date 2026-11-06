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
using GlorifiedBody.Audio;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Rooms;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;
using GlorifiedBody.Subtitles;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The floorplan v2 (2026-11-05, DEMO_PLAN.md §3.4, the writer's map): the twelve new rooms stand in the world with
    /// the doors of the map, a camera each, their name on a sign, and a NavMesh way in; the main way's rooms did not
    /// move; the fixtures of round 30 work (the microwave, the coffee machine, the toilet, the faucet); the head card
    /// of the conference room plays the flashback of cutscene 07b once and raises the flag, the SHORT bar rises a
    /// tenth, the other cards read their name; the voices of U1 say "Encore." when the flag is up.
    /// Batch: tools/unity.sh test playmode FloorplanV2Tests.
    /// </summary>
    public class FloorplanV2Tests
    {
        private const string World = "Demo";
        private const string CardFlag = "demo_g12_card";
        private const string FlashbackId = "07b_the_conference_room";

        /// <summary>The rooms behind the doors of each room, as the map draws them. W13's second door is W10 (the built ward has no wall of W8 by W12).</summary>
        private static readonly (string id, string[] doors)[] Map =
        {
            ("W13", new[] { "W12", "W10" }), ("W14", new[] { "W2" }), ("W15", new[] { "W2" }), ("W16", new[] { "W11" }), ("W17", new[] { "W11" }),
            ("G9b", new[] { "G9" }), ("G10", new[] { "G2", "G5" }), ("G11", new[] { "G8", "G15" }), ("G12", new[] { "G5" }), ("G13", new[] { "G2", "G7" }),
            ("G14", new[] { "U1" }), ("G15", new[] { "G11" }), ("G6", new[] { "G5" }), ("G8", new[] { "G5", "G11" }),
        };
        private static readonly string[] NewRooms = { "W13", "W14", "W15", "W16", "W17", "G9b", "G10", "G11", "G12", "G13", "G14", "G15" };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_v2_tests");
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
        public IEnumerator TheTwelveRooms_StandOnTheMap_WithTheirDoors_ACameraEach_TheirSign_AndAWayIn_TheMainWayUnmoved()
        {
            yield return LoadWorld();
            var failures = new List<string>();
            foreach ((string id, string[] doors) in Map)
            {
                Room room = Room.Of(id);
                if (room == null) { failures.Add(id + ": missing"); continue; }
                var expected = new HashSet<string>(doors);
                var actual = new HashSet<string>(room.neighbours);
                if (!expected.SetEquals(actual)) failures.Add($"{id}: doors to {string.Join(",", actual)}, the map says {string.Join(",", expected)}");
                foreach (string other in doors)
                {
                    Room next = Room.Of(other);
                    if (next == null) failures.Add($"{id}: {other} does not exist");
                    else if (!next.IsNeighbour(id)) failures.Add($"{id}: {other} does not list it back");
                }
                if (room.GetComponentsInChildren<GlorifiedBody.Cameras.FixedCamera>().Length == 0) failures.Add(id + ": no camera");
                if (room.transform.Find("[Props]/Sign_Name") == null) failures.Add(id + ": no sign with its name");
                // A way in for the chasers: the NavMesh runs through the doorway from the room behind it (the mesh under a free floor point of each).
                if (!UnityEngine.AI.NavMesh.SamplePosition(FloorPoint(room), out UnityEngine.AI.NavMeshHit here, 1f, UnityEngine.AI.NavMesh.AllAreas)) { failures.Add(id + ": no NavMesh under its floor"); continue; }
                if (!UnityEngine.AI.NavMesh.SamplePosition(FloorPoint(Room.Of(doors[0])), out UnityEngine.AI.NavMeshHit there, 1f, UnityEngine.AI.NavMesh.AllAreas)) { failures.Add(doors[0] + ": no NavMesh under its floor"); continue; }
                var path = new UnityEngine.AI.NavMeshPath();
                if (!UnityEngine.AI.NavMesh.CalculatePath(there.position, here.position, UnityEngine.AI.NavMesh.AllAreas, path) || path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete)
                    failures.Add($"{id}: no NavMesh way in from {doors[0]} ({path.status})");
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
            // The main way did not move (Bruno: "the main way does not change").
            foreach ((string id, Vector4 rect) in new[]
            {
                ("W1", new Vector4(0, 19, 8, 26)), ("W2", new Vector4(0, 11, 24, 14)), ("W5", new Vector4(0, -9, 3, 11)), ("W9", new Vector4(3, -10, 17, -2)),
                ("W11", new Vector4(3, -18, 13, -10)), ("W12", new Vector4(13, -14, 17, -10)), ("G1", new Vector4(0, 0, 5, 5)), ("G2", new Vector4(5, 0, 35, 6)),
                ("G5", new Vector4(12, -16, 28, 0)), ("G9", new Vector4(28, -16, 34, -9)), ("U1", new Vector4(14, -28, 26, -16)),
            })
                Assert.AreEqual(rect, Room.Of(id).rect, id + " stands where it stood");
            Assert.AreEqual(37, Room.All.Count, "36 rooms and the stair");
        }

        [UnityTest]
        public IEnumerator G12_TheHeadCard_PlaysTheFlashbackOnce_TheShortRises_ThenItReadsItsName_TheOthersReadTheirs()
        {
            yield return LoadWorld();
            yield return SkipTheWake();
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            Room g12 = Room.Of("G12");
            NameCard head = g12.transform.Find("[Props]/Card_Head").GetComponent<NameCard>();
            Assert.IsNotNull(head.flashback, "the head card carries the flashback");
            Assert.AreEqual(FlashbackId, head.flashback.cutsceneId);
            Assert.AreEqual(9, head.flashback.shots.Length, "F0 to F5 as black and text cards");
            float seconds = head.flashback.shots.Sum(s => s.seconds);
            Assert.That(seconds, Is.InRange(12f, 15f), "twelve to fifteen seconds");
            Assert.IsNotNull(head.flashback.shots[0].sound, "F0: gravel under tyres, a car door");
            Assert.IsTrue(head.flashback.shots.Where(s => !string.IsNullOrEmpty(s.text)).All(s => s.speaker == ""), "the French lines carry no speaker name");
            CollectionAssert.AreEqual(new[] { "Encore.", "Un. Deux. Trois.", "Elle ne pleure pas.", "Pas elle. L'autre." }, head.flashback.shots.Where(s => !string.IsNullOrEmpty(s.text)).Select(s => s.text).ToArray());

            StandBefore(lucy, g12, head.Point);
            yield return null;
            var hands = lucy.GetComponent<Interactor>();
            Assert.AreSame(head, hands.FindFocus(), "the Interact key reaches the head card");
            Assert.AreEqual(InteractKind.Act, head.KindFor(hands), "the first press plays");
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            stats.DebugSet(stats.Nalcon, 50f);
            float before = stats.Short;
            int played = CutscenePlayer.Played;
            Assert.AreEqual("card:flashback", head.Confirm(hands));
            Assert.IsTrue(StoryFlags.Has(CardFlag), "the flag goes up at the first press");
            yield return WaitUntil(() => CutscenePlayer.IsPlaying && CutscenePlayer.ShotIndex >= 0, 5f);
            Assert.AreEqual(FlashbackId, CutscenePlayer.Current, "the flashback plays");
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsNotNull(GameAudio.LastClip, "a sound");
            Assert.AreEqual("cs07b_f0", GameAudio.LastClip.name, "the gravel runs under the black");
            CutscenePlayer.Skip();
            yield return WaitUntil(() => !CutscenePlayer.IsPlaying, 8f);
            yield return null;
            yield return null;
            Assert.That(stats.Short - before, Is.InRange(9f, 10.01f), "the SHORT bar rises a tenth (PROPOSED)");
            Assert.AreEqual(played + 1, CutscenePlayer.Played);
            Assert.AreEqual(InteractKind.Message, head.KindFor(hands), "the second press shows the card only");
            Assert.AreEqual("DE HOMEN-CRISTO", head.TextFor(hands));
            Assert.AreEqual("card:DE HOMEN-CRISTO", head.Confirm(hands));
            Assert.AreEqual(played + 1, CutscenePlayer.Played, "no second flashback");
            NameCard colton = g12.transform.Find("[Props]/Card_Colton").GetComponent<NameCard>();
            Assert.AreEqual(InteractKind.Message, colton.KindFor(hands));
            Assert.AreEqual("COLTON", colton.TextFor(hands), "the other cards read as one line each");
            Assert.AreEqual(NameCard.FaceDown, g12.transform.Find("[Props]/Card_Down_A").GetComponent<NameCard>().TextFor(hands));
            Assert.IsTrue(Room.Of("U1").transform.Find("[Story]/Gate_Encore").GetComponent<FlagGate>().Showing, "the voices of U1 hear the flag");
        }

        [UnityTest]
        public IEnumerator TheFixtures_TheMicrowaveDings_TheCoffeeFills_TheToiletFlushesAndTheHiderAnswersOnce_TheFaucetRunsUntilSheLeaves()
        {
            yield return LoadWorld();
            yield return SkipTheWake();
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            var hands = lucy.GetComponent<Interactor>();
            Assert.AreEqual(16, UnityEngine.Object.FindObjectsByType<Fixture>(FindObjectsSortMode.None).Length, "W4 2, W6 2, W15 3, G6 2, G10 2, G11 3, G15 2: 16 fixtures");

            // The microwave of W15: the light while it runs, then the ding.
            Fixture microwave = Of("W15", Fixture.Kind.Microwave);
            StandBefore(lucy, Room.Of("W15"), microwave);
            yield return null;
            Assert.AreSame(microwave, hands.FindFocus(), "the Interact key reaches the microwave");
            microwave.runSeconds = 1.5f;
            Assert.AreEqual("fixture:microwave", microwave.Confirm(hands));
            Assert.IsTrue(microwave.Running && microwave.shown.activeSelf, "the light is on");
            Assert.IsTrue(GameAudio.PlayedCount(Fixture.HumKey) > 0, "the hum");
            Assert.AreEqual("fixture:busy:" + microwave.name, microwave.Confirm(hands), "a second press waits");
            yield return new WaitForSeconds(2.2f);
            Assert.IsFalse(microwave.Running, "it stops after its seconds");
            Assert.IsFalse(microwave.shown.activeSelf, "the light is off");
            Assert.AreEqual(1, Fixture.Dings, "it dings");
            Assert.AreEqual(1, GameAudio.PlayedCount(Fixture.DingKey));

            // The coffee machine of G11: the cup fills, she drinks, nothing changes.
            Fixture coffee = Of("G11", Fixture.Kind.Coffee);
            StandBefore(lucy, Room.Of("G11"), coffee);
            yield return null;
            Assert.AreSame(coffee, hands.FindFocus(), "the Interact key reaches the coffee machine");
            float empty = coffee.moving.localScale.y;
            Health health = lucy.GetComponentInChildren<Health>();
            float hp = health.CurrentHealth;
            Assert.AreEqual("fixture:coffee", coffee.Confirm(hands));
            yield return new WaitForSeconds(2f);
            Assert.Greater(coffee.moving.localScale.y, empty + 0.02f, "the coffee rises in the cup");
            yield return WaitUntil(() => !coffee.Running, Fixture.FillSeconds + Fixture.DrinkSeconds + 3f);
            Assert.IsFalse(coffee.Running);
            Assert.AreEqual(empty, coffee.moving.localScale.y, 0.001f, "the cup is empty again");
            Assert.AreEqual(hp, health.CurrentHealth, "nothing changes (L9: ORAL INTAKE: NONE)");

            // The toilet of G6: the flush, and the hider answers it once. The hider whispers on its own every 7 s: the answer comes within a second of the flush.
            Fixture toilet = Of("G6", Fixture.Kind.Toilet);
            CivilianBrain hider = Room.Of("G6").GetComponentInChildren<CivilianBrain>();
            Assert.IsTrue(hider != null && hider.Hides, "the clerk hides in G6");
            StandBefore(lucy, Room.Of("G6"), toilet);
            yield return new WaitForSeconds(1.2f);
            Assert.AreSame(toilet, hands.FindFocus(), "the Interact key reaches the toilet");
            int said = Bark.CountOf(hider.gameObject);
            Assert.AreEqual("fixture:toilet", toilet.Confirm(hands));
            Assert.IsTrue(GameAudio.PlayedCount(Fixture.FlushKey) > 0, "the flush");
            yield return WaitUntil(() => Bark.CountOf(hider.gameObject) > said, 1.5f);
            Assert.AreEqual(said + 1, Bark.CountOf(hider.gameObject), "the hider answers");
            Assert.AreEqual("Occupied.", hider.GetComponent<Bark>().Shown);
            Assert.IsTrue(toilet.Answered);
            yield return WaitUntil(() => !toilet.Running, Fixture.FlushSeconds + 1f);
            said = Bark.CountOf(hider.gameObject);
            toilet.Confirm(hands);
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(said, Bark.CountOf(hider.gameObject), "the hider answers once");

            // The faucet of G15: it runs until she leaves the room.
            Fixture faucet = Of("G15", Fixture.Kind.Faucet);
            StandBefore(lucy, Room.Of("G15"), faucet);
            yield return null;
            Assert.AreSame(faucet, hands.FindFocus(), "the Interact key reaches the faucet");
            Assert.AreEqual("fixture:faucet_on", faucet.Confirm(hands));
            Assert.IsTrue(faucet.Running && faucet.shown.activeSelf, "the water runs");
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(faucet.Running, "it keeps running while she is in the room");
            Assert.AreEqual("fixture:faucet_off", faucet.Confirm(hands), "a second press turns it off");
            Assert.AreEqual("fixture:faucet_on", faucet.Confirm(hands));
            lucy.GetComponent<TankController>().Teleport(FloorPoint(Room.Of("G11")), 0f);
            yield return WaitUntil(() => !faucet.Running, 2f);
            Assert.IsFalse(faucet.Running, "she left the room: it stops");
            Assert.IsFalse(faucet.shown.activeSelf);
        }

        [UnityTest]
        public IEnumerator U1_TheVoices_SayEncore_OnlyWhenSheReadTheCard()
        {
            yield return LoadWorld();
            yield return SkipTheWake();
            FlagGate gate = Room.Of("U1").transform.Find("[Story]/Gate_Encore").GetComponent<FlagGate>();
            Assert.IsFalse(gate.Showing, "the flag is down: the line waits off");
            Assert.IsFalse(gate.transform.Find("Trigger_U1_Encore").gameObject.activeSelf, "its trigger is off");
            StoryFlags.Raise(CardFlag);
            yield return null;
            Assert.IsTrue(gate.Showing && gate.transform.Find("Trigger_U1_Encore").gameObject.activeSelf, "the flag is up: the line is in the voices");
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponent<TankController>().Teleport(Room.Of("U1").transform.position + new Vector3(20f, 0f, -25.5f), 180f);
            yield return WaitUntil(() => SubtitleService.Lines.Any(l => l.Text == "Encore."), 15f);
            Assert.IsTrue(SubtitleService.Lines.Any(l => l.Text == "Encore."), "\"Encore.\" among the voices of the night");
            yield return WaitUntil(() => Bulletin.IsShowing, 60f);
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static Fixture Of(string roomId, Fixture.Kind kind) => Room.Of(roomId).GetComponentsInChildren<Fixture>().First(f => f.kind == kind);

        /// <summary>Lucy 0.9 m in front of the fixture (its forward is the way it faces), looking at it.</summary>
        private static void StandBefore(GameObject lucy, Room room, Fixture fixture)
        {
            Vector3 at = fixture.transform.position + fixture.transform.forward * 0.9f;
            at.y = room.BaseY;
            float yaw = Quaternion.LookRotation(Vector3.ProjectOnPlane(fixture.Point - at, Vector3.up)).eulerAngles.y;
            lucy.GetComponent<TankController>().Teleport(at, yaw);
        }

        /// <summary>Lucy north-east of the table's end, 1.3 m from the head card, facing it.</summary>
        private static void StandBefore(GameObject lucy, Room room, Vector3 point)
        {
            Vector3 at = room.transform.position + new Vector3(36.6f, 0f, -18.2f);
            float yaw = Quaternion.LookRotation(Vector3.ProjectOnPlane(point - at, Vector3.up)).eulerAngles.y;
            lucy.GetComponent<TankController>().Teleport(at, yaw);
        }

        private static IEnumerator SkipTheWake()
        {
            yield return WaitUntil(() => CutscenePlayer.IsPlaying && CutscenePlayer.ShotIndex >= 0, 10f);
            CutscenePlayer.Skip();
            yield return WaitUntil(() => StoryFlags.Has("demo_w1_wake") && !CutscenePlayer.IsPlaying, 10f);
            yield return null;
        }

        private static IEnumerator LoadWorld()
        {
            GameManager.Instance.LoadLevel(World);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == World && GameManager.Instance.IsPlaying, 30f);
            yield return null;
            Assert.AreEqual(World, SceneManager.GetActiveScene().name, "the world scene is in the build (DemoWorld.BuildAll)");
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            yield return WaitUntil(() => RoomTracker.Current != null, 3f);
        }

        /// <summary>A floor point of the room clear of its props, nearest its middle.</summary>
        private static Vector3 FloorPoint(Room room)
        {
            int solid = LayerMask.GetMask("Default");
            Vector3 c = room.Centre;
            for (float r = 0f; r < 20f; r += 0.5f)
                for (int k = 0; k < 8; k++)
                {
                    Vector3 top = new Vector3(c.x + Mathf.Cos(k * Mathf.PI / 4f) * r, room.BaseY + 2.5f, c.z + Mathf.Sin(k * Mathf.PI / 4f) * r);
                    if (!room.Contains(new Vector3(top.x, room.BaseY, top.z), -0.5f)) continue;
                    if (!Physics.Raycast(top, Vector3.down, out RaycastHit hit, 10f, solid, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.point.y > room.BaseY + 0.3f) continue; // the top of a desk or a table is no floor
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
