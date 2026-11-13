#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
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
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The scope of the search music (Bruno, 2026-11-13: "fix the search scope to the room"; then "how about it being on
    /// the room and the attached room? like if lucy is a room right next to where they're searching the song changes and
    /// the player has time to strategize"). In the demo world one alerted soldier of G2 hunts. Lucy stands in a room next
    /// door (G3, one door): Tension. Lucy stands two doors away (G7, through G13 or G5) or on the other floor (W6): Ambient.
    /// Their guns are off, so no aim makes it Combat. Batch: tools/unity.sh test playmode MusicScopeTests.
    /// </summary>
    public class MusicScopeTests
    {
        private const string World = "Demo";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_music_scope_tests");
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
        public IEnumerator AHunterInAFarRoom_IsNotTheSearch_OnlyHerRoomAndTheRoomsNextDoor()
        {
            GameManager.Instance.LoadLevel(World);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == World && GameManager.Instance.IsPlaying, 30f);
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            yield return WaitUntil(() =>
            {
                if (CutscenePlayer.IsPlaying) CutscenePlayer.Skip();
                UprightRestraint.FreeAll();
                return StoryFlags.Has("demo_h1_wake") && !CutscenePlayer.IsPlaying && Time.timeScale > 0f;
            }, 20f);
            MusicDirector music = MusicDirector.Instance;
            TankController tank = lucy.GetComponent<TankController>();
            Room g2 = Room.Of("G2");
            Assert.IsTrue(g2.IsNeighbour("G3") && !g2.IsNeighbour("G7") && !g2.IsNeighbour("W6"), "G3 shares a door with G2; G7 (through G13 or G5) and W6 do not");

            // The far room first, before any hunter: the room's own music.
            yield return Stand(tank, "G7");
            yield return WaitUntil(() => music.State == MusicState.Ambient, 10f);
            Assert.AreEqual(MusicState.Ambient, music.State, "nobody hunts: exploration");

            yield return WaitUntil(() => g2.GetComponentsInChildren<SoldierBrain>().Length > 0, 5f);
            SoldierBrain hunter = g2.GetComponentsInChildren<SoldierBrain>().First(b => b.name.StartsWith("Soldier_Pair"));
            foreach (DummyShooter gun in UnityEngine.Object.FindObjectsByType<DummyShooter>(FindObjectsSortMode.None)) gun.enabled = false; // no aim: no Combat
            hunter.Alert("test", callOthers: false);
            Assert.IsTrue(hunter.Alerted);

            // The rooms are picked from where the hunter stands at each check (he walks): next door, two doors away, the other floor.
            var states = new System.Collections.Generic.List<string>();
            foreach (string kind in new[] { "next door", "two doors", "far" })
            {
                Room at = Room.At(hunter.transform.position + Vector3.up * 0.1f);
                Assert.IsNotNull(at, "the hunter stands in a room");
                string pick = kind == "far" ? (at.floor == "demo_2" ? "G7" : "W6")
                    : kind == "next door" ? at.neighbours.First(n => n != "S1" && Room.Of(n) != null && Room.Of(n).floor == at.floor)
                    : Room.All.Where(r => r.floor == at.floor && r.id != at.id && !at.IsNeighbour(r.id) && r.neighbours.Any(n => at.IsNeighbour(n))).Select(r => r.id).First();
                MusicState expected = kind == "next door" ? MusicState.Tension : MusicState.Ambient;
                yield return Stand(tank, pick);
                yield return new WaitForSeconds(1.5f); // three hunt checks (0.5 s each)
                Room now = Room.At(hunter.transform.position + Vector3.up * 0.1f);
                bool stillNear = now != null && (now == Room.Of(pick) || now.IsNeighbour(pick));
                states.Add($"{kind}: Lucy in {pick}, the hunter in {(now != null ? now.id : "none")} (was {at.id}): {music.State}");
                Debug.Log("[MusicScopeTests] " + states[states.Count - 1]);
                if (kind != "next door" && stillNear) Assert.Inconclusive("the hunter walked next to her: " + string.Join("; ", states));
                if (kind == "next door" && !stillNear) Assert.Inconclusive("the hunter walked away: " + string.Join("; ", states));
                Assert.AreEqual(expected, music.State, string.Join("; ", states));
            }
        }

        private static IEnumerator Stand(TankController tank, string roomId)
        {
            Room room = Room.Of(roomId);
            tank.Teleport(room.Centre, 0f);
            yield return WaitUntil(() => RoomTracker.CurrentId == roomId, 3f);
            Assert.AreEqual(roomId, RoomTracker.CurrentId);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
