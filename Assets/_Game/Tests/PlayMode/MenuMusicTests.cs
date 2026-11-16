#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Audio;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The main menu music (Bruno, 2026-11-16: "carol d is the best", "Yes, send it"): the T45 loop plays on the main
    /// menu, loops, and is not the placeholder. The game leaves it for the room's music. Batch:
    /// tools/unity.sh test playmode MenuMusicTests.
    /// </summary>
    public class MenuMusicTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
        }

        [UnityTest]
        public IEnumerator TheMainMenu_LoopsTheT45Take_AndTheGameLeavesIt()
        {
            MusicDirector music = MusicDirector.Instance;
            AudioClip menu = Resources.Load<AudioClip>(MusicDirector.Folder + MusicDirector.MenuKey + "/menu");
            Assert.IsNotNull(menu, "the T45 loop: tools/menu_music_import.sh");
            yield return WaitUntil(() => music.State == MusicState.Menu && music.CurrentClip == menu && !music.Crossfading, 6f);
            Assert.AreEqual(MusicState.Menu, music.State);
            Assert.AreSame(menu, music.CurrentClip, "the menu plays the T45 loop");
            Assert.IsFalse(music.CurrentIsPlaceholder, "not the placeholder");
            AudioSource source = music.GetComponentsInChildren<AudioSource>().First(s => s.clip == menu);
            Assert.IsTrue(source.loop, "it loops");
            Assert.IsTrue(source.isPlaying, "it plays");
            Assert.Greater(music.CurrentVolume, 0f, "it is heard");
            int sample = music.CurrentSample;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.Greater(music.CurrentSample, sample, "it runs");

            GameManager.Instance.LoadGameplay();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "TrainingRoom" && GameManager.Instance.IsPlaying, 10f);
            yield return WaitUntil(() => music.State != MusicState.Menu && music.CurrentClip != menu, 4f);
            Assert.AreNotEqual(MusicState.Menu, music.State, "the game: the room's music");
            Assert.AreNotSame(menu, music.CurrentClip);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
