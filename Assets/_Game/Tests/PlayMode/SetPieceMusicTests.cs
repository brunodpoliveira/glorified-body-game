#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Audio;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The Dining Hall set piece music (Bruno, 2026-11-15: "Still B", "Yes, cut it"), played in the training room with the
    /// G18 files: the entry starts on the next bar line of the combat track (83 BPM), the fight loop after it; the armour
    /// cue starts the armour entry on the next bar line of the set piece (165 BPM), then the armour loop; the end cue starts
    /// the end section on the next bar line, and the normal states take the music on its last bar line. Each section really
    /// sounds from its start time. She leaves: the set piece stops. Batch: tools/unity.sh test playmode SetPieceMusicTests.
    /// </summary>
    public class SetPieceMusicTests
    {
        private const double OneSample = 1.0 / 48000.0;
        private const double ReadSlack = 0.08;
        private static double Bar165 => 240.0 / 165.0;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadGameplay();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "TrainingRoom" && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            foreach (DummyShooter shooter in UnityEngine.Object.FindObjectsByType<DummyShooter>(FindObjectsSortMode.None)) shooter.enabled = false;
            GameObject.FindGameObjectWithTag("Player").GetComponentInChildren<Health>().SetInvulnerable(true);
            double dsp0 = AudioSettings.dspTime;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.Greater(AudioSettings.dspTime, dsp0, "the audio clock runs");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MusicDirector.Hold(null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheEvents_PlayInOrder_EachOnTheNextBarLine_AndTheStatesComeBackOnTheLastOne()
        {
            MusicDirector music = MusicDirector.Instance;
            MusicDirector.Hold(MusicState.Combat);
            yield return new WaitForSecondsRealtime(MusicDirector.FadeSeconds + 0.5f);
            Assert.AreEqual("combat", music.CurrentClip.name, "the combat theme plays");
            double themeAnchor = music.Anchor;
            int themeSamples = music.CurrentClip.samples, rate = music.CurrentClip.frequency;
            SetPieceMusic kit = SetPieceMusic.Load("G18", 165.0);
            Assert.IsNotNull(kit, "the G18 files");

            double asked = AudioSettings.dspTime;
            MusicDirector.SetPiece(kit);
            yield return null;
            yield return null;
            Assert.AreEqual(MusicState.Score, music.State);
            Assert.IsTrue(music.InFight, "the set piece is a fight (a tape waits)");
            Assert.AreEqual(2, music.SetPieceLog.Count, string.Join(", ", music.SetPieceLog));
            double entry = music.SetPieceLog[0].At, fight = music.SetPieceLog[1].At;
            AssertOnLoopGrid(entry, themeAnchor, themeSamples, rate, MusicDirector.BarSeconds, "the entry on a bar line of the combat theme");
            Assert.Less(entry - asked, MusicDirector.BarSeconds + MusicDirector.ScheduleLead + 0.15, "the next combat bar line");
            Assert.AreEqual(9 * Bar165, fight - entry, OneSample, "the fight loop after the 9 bars of the entry");

            yield return WaitUntilDsp(entry + 0.3);
            Assert.AreEqual(SetPieceMusic.Entry, music.CurrentClip.name);
            AssertPlaysFrom(music, entry, "the entry");
            yield return WaitUntilDsp(fight + 0.3);
            Assert.AreEqual(SetPieceMusic.FightLoop, music.CurrentClip.name);
            AssertPlaysFrom(music, fight, "the fight loop");

            asked = AudioSettings.dspTime;
            MusicDirector.SetPieceCue(SetPieceEvent.Armour);
            yield return null;
            yield return null;
            Assert.AreEqual(4, music.SetPieceLog.Count, string.Join(", ", music.SetPieceLog));
            double armour = music.SetPieceLog[2].At, armourLoop = music.SetPieceLog[3].At;
            AssertOnLoopGrid(armour, fight, kit.FightLoopClip.samples, rate, Bar165, "the armour entry on a bar line of the fight loop");
            Assert.Less(armour - asked, Bar165 + MusicDirector.ScheduleLead + 0.15, "the next set piece bar line");
            Assert.AreEqual(8 * Bar165, armourLoop - armour, OneSample, "the armour loop after the 8 bars of its entry");
            yield return WaitUntilDsp(armour + 0.3);
            Assert.AreEqual(SetPieceMusic.ArmourEntry, music.CurrentClip.name);
            AssertPlaysFrom(music, armour, "the armour entry");
            yield return WaitUntilDsp(armourLoop + 0.3);
            Assert.AreEqual(SetPieceMusic.ArmourLoop, music.CurrentClip.name);
            AssertPlaysFrom(music, armourLoop, "the armour loop");

            asked = AudioSettings.dspTime;
            MusicDirector.SetPieceCue(SetPieceEvent.End);
            yield return null;
            yield return null;
            Assert.AreEqual(5, music.SetPieceLog.Count, string.Join(", ", music.SetPieceLog));
            double end = music.SetPieceLog[4].At;
            AssertOnLoopGrid(end, armourLoop, kit.ArmourLoopClip.samples, rate, Bar165, "the end on a bar line of the armour loop");
            Assert.Less(end - asked, Bar165 + MusicDirector.ScheduleLead + 0.15, "the next set piece bar line");
            Assert.AreEqual(9 * Bar165, music.SetPieceDoneAt - end, OneSample, "the end section: 9 bars");
            yield return WaitUntilDsp(end + 0.3);
            Assert.AreEqual(SetPieceMusic.End, music.CurrentClip.name);
            AssertPlaysFrom(music, end, "the end");

            double done = music.SetPieceDoneAt;
            yield return WaitUntilDsp(done + 0.4);
            Assert.IsFalse(music.SetPieceActive, "the set piece is over");
            Assert.AreNotEqual(MusicState.Score, music.State, "the room decides again");
            Assert.IsNotNull(music.CurrentClip, "a state track plays");
            Assert.AreEqual(done, music.CurrentStartAt, 1e-9, "it starts on the last bar line of the end section");
            AssertPlaysFrom(music, done, "the state after the set piece");
            CollectionAssert.AreEqual(new[] { "entry", "fight_loop", "armour_entry", "armour_loop", "end" }, music.SetPieceLog.Select(s => s.Name).ToArray(), "the order of the events");
            Debug.Log("[SetPieceMusicTests] " + string.Join(", ", music.SetPieceLog.Select(s => $"{s.Name} +{(s.At - entry) / Bar165:0.000000} bars")) +
                      $"; the entry at {(entry - themeAnchor) / MusicDirector.BarSeconds:0.000000} combat bars; the state at +{(done - entry) / Bar165:0.000000} bars");
        }

        [UnityTest]
        public IEnumerator AfterALoad_WithTheArmourIn_TheEntryGoesToTheArmourLoop()
        {
            MusicDirector music = MusicDirector.Instance;
            SetPieceMusic kit = SetPieceMusic.Load("G18", 165.0);
            MusicDirector.SetPiece(kit, armourAlready: true);
            yield return null;
            yield return null;
            CollectionAssert.AreEqual(new[] { "entry", "armour_loop" }, music.SetPieceLog.Select(s => s.Name).ToArray());
            Assert.AreEqual(9 * Bar165, music.SetPieceLog[1].At - music.SetPieceLog[0].At, OneSample);
            MusicDirector.SetPieceCue(SetPieceEvent.Armour); // it came already: no second armour entry
            yield return null;
            Assert.AreEqual(2, music.SetPieceLog.Count, "no second armour entry");
        }

        [UnityTest]
        public IEnumerator SheLeavesTheRoom_TheSetPieceStops_AndTheRoomsMusicComesBack()
        {
            MusicDirector music = MusicDirector.Instance;
            MusicDirector.SetPiece(SetPieceMusic.Load("G18", 165.0));
            yield return null;
            yield return null;
            yield return WaitUntilDsp(music.SetPieceLog[0].At + 0.5);
            Assert.AreEqual(SetPieceMusic.Entry, music.CurrentClip.name);
            MusicDirector.Hold(null); // RoomScore: she left
            yield return null;
            Assert.IsFalse(music.SetPieceActive, "the set piece stops");
            Assert.AreNotEqual(MusicState.Score, music.State);
            yield return new WaitForSecondsRealtime(MusicDirector.FadeSeconds + 0.5f);
            string[] names = { "entry", "fight_loop", "armour_entry", "armour_loop", "end" };
            foreach (AudioSource s in music.GetComponentsInChildren<AudioSource>())
                if (s.clip != null && s.isPlaying) Assert.IsFalse(names.Contains(s.clip.name), $"{s.clip.name} still plays");
            Assert.IsNotNull(music.CurrentClip, "the room's music");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static void AssertOnLoopGrid(double t, double anchor, int loopSamples, int rate, double bar, string what)
        {
            double loopSec = (double)loopSamples / rate;
            double inPass = (t - anchor) % loopSec;
            double bars = inPass / bar;
            double off = Math.Abs(bars - Math.Round(bars)) * bar;
            bool passEnd = loopSec - inPass < OneSample;
            Assert.IsTrue(off < OneSample || passEnd, $"{what}: {off * 48000.0:0.00} samples off");
        }

        private static void AssertPlaysFrom(MusicDirector music, double startedAt, string what)
        {
            double heard = (double)music.CurrentSample / music.CurrentClip.frequency;
            double expected = AudioSettings.dspTime - startedAt;
            Assert.AreEqual(expected, heard, ReadSlack, $"{what}: plays from its bar line");
        }

        private static IEnumerator WaitUntilDsp(double dsp)
        {
            float t0 = Time.realtimeSinceStartup;
            while (AudioSettings.dspTime < dsp && Time.realtimeSinceStartup - t0 < 60f) yield return null;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
