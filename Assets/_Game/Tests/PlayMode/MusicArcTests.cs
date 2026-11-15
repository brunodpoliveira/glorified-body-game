#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Audio;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Systems;
using UnityEngine.TestTools;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The combat arc (Bruno: the T43 v4 kit on 2026-11-14, the T44 "hunted a" kit since 2026-11-15), in the training room: the theme loops in
    /// combat; after ArcAfterBars of combat the drum break starts on the next bar line of the theme, the payoff right
    /// after it, then the theme again from its bar 1. Every start is on the bar grid of the theme (83 BPM) (DSP time), and
    /// each section really starts at its time. A fight that ends in the arc leaves it. The states still keep the beat.
    /// The arc tests ride the music in real time (about 50 and 90 s). Batch: tools/unity.sh test playmode MusicArcTests.
    /// </summary>
    public class MusicArcTests
    {
        private const double OneSample = 1.0 / 48000.0;
        /// <summary>The main thread reads the audio position once an audio buffer: a read is good to about this much.</summary>
        private const double ReadSlack = 0.08;

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
            Assert.Greater(AudioSettings.dspTime, dsp0, "the audio clock runs (no audio device: these tests cannot hear the music)");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MusicDirector.Hold(null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheStates_StillKeepTheBeat_IntoTheKitsTheme()
        {
            MusicDirector music = MusicDirector.Instance;
            MusicDirector.Hold(MusicState.Tension);
            yield return new WaitForSecondsRealtime(MusicDirector.FadeSeconds + 0.5f);
            Assert.AreEqual("tension", music.CurrentClip.name);
            int locks = music.Locks;
            MusicDirector.Hold(MusicState.Combat);
            yield return null;
            yield return null;
            Assert.AreEqual("combat", music.CurrentClip.name, "the theme loop of the kit is the combat state");
            Assert.IsFalse(music.CurrentIsPlaceholder, "the kit, not the placeholder");
            Assert.AreEqual(locks + 1, music.Locks, "search to combat kept the beat (one piece)");
        }

        [UnityTest]
        public IEnumerator TheArc_StartsAfterTwelveBarsOfCombat_OnABarLine_AndEverySectionStartsOnTheGrid()
        {
            MusicDirector music = MusicDirector.Instance;
            int arcs = music.Arcs;
            MusicDirector.Hold(MusicState.Combat);
            yield return null;
            yield return null;
            Assert.AreEqual("combat", music.CurrentClip.name);
            double since = music.CombatSince;
            double bars12 = MusicDirector.ArcAfterBars * MusicDirector.BarSeconds;
            yield return WaitUntil(() => music.Arcs > arcs, (float)bars12 + 6f);
            Assert.AreEqual(arcs + 1, music.Arcs, "the arc after twelve bars of combat");
            double armed = AudioSettings.dspTime;
            Assert.GreaterOrEqual(music.ArcBreakAt - since, bars12 - 1e-6, "not before twelve bars");
            Assert.Less(music.ArcBreakAt - armed, MusicDirector.BarSeconds + MusicDirector.ScheduleLead + 0.1, "on the next bar line");
            AssertOnTheGrid(music);

            yield return WaitUntilDsp(music.ArcBreakAt + 0.6);
            Assert.AreEqual(MusicDirector.BreakName, music.CurrentClip.name, "the break sounds");
            AssertPlaysFrom(music, music.ArcBreakAt, "the break");
            yield return WaitUntilDsp(music.ArcPayoffAt + 0.6);
            Assert.AreEqual(MusicDirector.PayoffName, music.CurrentClip.name, "the payoff sounds");
            AssertPlaysFrom(music, music.ArcPayoffAt, "the payoff");
            Debug.Log($"[MusicArcTests] break at bar {(music.ArcBreakAt - music.ArcThemeAnchor) / MusicDirector.BarSeconds:0.000000} of the theme, " +
                      $"payoff +{(music.ArcPayoffAt - music.ArcBreakAt) / MusicDirector.BarSeconds:0.000000} bars, theme +{(music.ArcReturnAt - music.ArcPayoffAt) / MusicDirector.BarSeconds:0.000000} bars");
        }

        [UnityTest]
        public IEnumerator AfterThePayoff_TheThemeLoopsAgain_FromItsBarOne()
        {
            MusicDirector music = MusicDirector.Instance;
            MusicDirector.Hold(MusicState.Combat);
            yield return new WaitForSecondsRealtime(MusicDirector.FadeSeconds + 0.5f);
            int arcs = music.Arcs;
            MusicDirector.StartArc(); // a combat event asks for it now
            yield return WaitUntil(() => music.Arcs > arcs, 2f);
            Assert.AreEqual(arcs + 1, music.Arcs, "StartArc schedules the arc");
            AssertOnTheGrid(music);
            double back = music.ArcReturnAt;
            yield return WaitUntilDsp(back + 0.6);
            Assert.IsFalse(music.ArcActive, "the arc is over");
            Assert.AreEqual("combat", music.CurrentClip.name, "the theme again");
            Assert.AreEqual(back, music.Anchor, 1e-9, "the theme's bar 1 is the bar line after the payoff");
            AssertPlaysFrom(music, back, "the theme");
            Assert.AreEqual(back, music.CombatSince, 1e-9, "the next arc waits twelve more bars");
            Assert.AreEqual(arcs + 1, music.Arcs, "no second arc at once");
        }

        [UnityTest]
        public IEnumerator AFightThatEndsInTheBreak_LeavesTheArc_ForTheState()
        {
            MusicDirector music = MusicDirector.Instance;
            MusicDirector.Hold(MusicState.Combat);
            yield return new WaitForSecondsRealtime(MusicDirector.FadeSeconds + 0.5f);
            MusicDirector.StartArc();
            yield return WaitUntil(() => music.ArcActive, 2f);
            yield return WaitUntilDsp(music.ArcBreakAt + 0.3);
            MusicDirector.Hold(MusicState.Tension);
            yield return null;
            Assert.IsFalse(music.ArcActive, "the arc stops when the fight ends");
            Assert.AreEqual("tension", music.CurrentClip.name, "the search state");
            yield return new WaitForSecondsRealtime(MusicDirector.FadeSeconds + 0.5f);
            foreach (AudioSource s in music.GetComponentsInChildren<AudioSource>())
            {
                if (s.clip == null || !s.isPlaying) continue;
                Assert.AreNotEqual(MusicDirector.PayoffName, s.clip.name, "the payoff never starts");
                Assert.AreNotEqual(MusicDirector.BreakName, s.clip.name, "the break faded out");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────

        /// <summary>The break on a bar line of the theme (to one sample), the payoff 4 bars on, the theme 14 bars after it.</summary>
        private static void AssertOnTheGrid(MusicDirector music)
        {
            double loopSec = (double)music.ArcThemeSamples / music.ArcThemeFrequency;
            double inPass = (music.ArcBreakAt - music.ArcThemeAnchor) % loopSec;
            double bars = inPass / MusicDirector.BarSeconds;
            double off = Math.Abs(bars - Math.Round(bars)) * MusicDirector.BarSeconds;
            bool passEnd = loopSec - inPass < OneSample;
            Assert.IsTrue(off < OneSample || passEnd, $"the break is {off * 48000.0:0.00} samples off a bar line of the theme");
            Assert.AreEqual(MusicDirector.BreakBars * MusicDirector.BarSeconds, music.ArcPayoffAt - music.ArcBreakAt, OneSample, "the payoff: 4 bars after the break");
            Assert.AreEqual(MusicDirector.PayoffBars * MusicDirector.BarSeconds, music.ArcReturnAt - music.ArcPayoffAt, OneSample, "the theme: 14 bars after the payoff");
        }

        /// <summary>The section that sounds is where its DSP start says (it really started at its bar line).</summary>
        private static void AssertPlaysFrom(MusicDirector music, double startedAt, string what)
        {
            double heard = (double)music.CurrentSample / music.CurrentClip.frequency;
            double expected = AudioSettings.dspTime - startedAt;
            Assert.AreEqual(expected, heard, ReadSlack, $"{what}: plays from its bar line");
        }

        private static IEnumerator WaitUntilDsp(double dsp)
        {
            float t0 = Time.realtimeSinceStartup;
            while (AudioSettings.dspTime < dsp && Time.realtimeSinceStartup - t0 < 150f) yield return null;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
