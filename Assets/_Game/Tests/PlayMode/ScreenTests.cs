#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Video;
using GlorifiedBody.Audio;
using GlorifiedBody.Automation;
using GlorifiedBody.Core;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Subtitles;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The in-world screens (2026-10-31, Bruno: "in-world screens (a CRT clip with sound)"), in the SCREEN TEST room
    /// (ScreenTestSetup.BuildScreenTest): the CRT wall plays once when she walks in, the CRT on the cart turns on and
    /// off with Interact. Batch: tools/unity.sh test playmode ScreenTests.
    /// </summary>
    public class ScreenTests
    {
        private const string Room = "ScreenTest";
        private const string Wall = "Screen_CrtWall", Cart = "Screen_Cart";
        private const string TapeFlag = "screen_test_tape_seen";

        [DefaultExecutionOrder(-1000)]
        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad pad;
            private void Update() => pad?.Commit();
        }

        private VirtualPad pad;
        private PadDriver driver;
        private GameObject lucy;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_screen_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(Room);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Room && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            Assert.AreEqual(Room, SceneManager.GetActiveScene().name, "the room is in the build (ScreenTestSetup.BuildScreenTest)");
            lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            pad = new VirtualPad();
            driver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            UnityEngine.Object.DontDestroyOnLoad(driver.gameObject);
            driver.pad = pad;
            GameInput.SetBotSource(pad);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            pad.ReleaseAll();
            GameInput.ClearBotSource(pad);
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            TapePlayer.Instance?.Stop();
            SaveSystem.Folder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheCrtWall_PlaysWhenSheWalksIn_WithItsSoundAndLines_Once()
        {
            Assert.AreEqual("screen_test", MusicDirector.Instance.RoomKey, "the room's music key (RoomMusic loads)");
            InWorldScreen wall = Screen(Wall);
            Assert.AreEqual(24, wall.faces.Length, "the wall of CRTs is one screen with 24 faces");
            Assert.IsNotNull(wall.clip, "the clip (tools/screen_placeholder.sh)");
            Assert.IsFalse(wall.IsPlaying, "it waits for her");
            Assert.IsFalse(wall.Available, "Interact does not reach the wall: only the trigger starts it");

            pad.SetHeld(InputManager.GameAction.Forward, true);
            yield return WaitUntil(() => wall.IsPlaying, 6f);
            pad.SetHeld(InputManager.GameAction.Forward, false);
            Assert.IsTrue(wall.IsPlaying, "a step into the room starts it");
            Assert.IsTrue(wall.ShowsClip, "the faces show the clip");
            Assert.IsTrue(wall.faces.All(f => f.sharedMaterial == wall.faces[0].sharedMaterial), "every face shows the same clip");

            VideoPlayer video = wall.GetComponent<VideoPlayer>();
            Assert.AreEqual(VideoAudioOutputMode.AudioSource, video.audioOutputMode);
            Assert.AreSame(wall.Sound, video.GetTargetAudioSource(0), "the sound comes out of the screen's source");
            Assert.AreEqual(1f, wall.Sound.spatialBlend, "in 3D, from the wall");
            Assert.IsFalse(wall.Sound.ignoreListenerPause, "a pause stops it with the world sounds");

            yield return new WaitForSeconds(1.5f);
            Assert.Greater(wall.Frame, 5, "the clip runs");
            SubtitleService.Line line = SubtitleService.Lines.FirstOrDefault(l => l.Owner == wall.SubtitleOwner);
            Assert.IsNotNull(line, "its line shows as a subtitle");
            Assert.AreEqual("This is a placeholder clip on a screen.", line.Text);
            Assert.LessOrEqual(MusicDirector.Instance.DuckFactor, TapePlayer.MusicLevel + 0.001f, "the music drops while it speaks");

            wall.Seek(wall.Length - 0.6f);
            yield return WaitUntil(() => !wall.IsPlaying, 6f);
            Assert.AreEqual(1, wall.Ends, "it plays to its end");
            Assert.IsFalse(wall.ShowsClip, "the faces go back to the green cursor");
            Assert.IsTrue(StoryFlags.Has(TapeFlag), "the flag of the beat is up (the saves keep it)");
            Assert.IsFalse(SubtitleService.Lines.Any(l => l.Owner == wall.SubtitleOwner), "its subtitle goes");

            // Out of the trigger and in again: it does not play a second time.
            lucy.GetComponent<TankController>().Teleport(new Vector3(0.3f, 0f, 11f), 180f);
            yield return new WaitForSeconds(0.3f);
            lucy.GetComponent<TankController>().Teleport(new Vector3(0f, 0f, 14f), 0f);
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(wall.IsPlaying, "once");
            Assert.AreEqual(1, wall.Plays);
        }

        [UnityTest]
        public IEnumerator TheClip_StopsWithTheWorld_AndGoesOnAfter()
        {
            InWorldScreen wall = Screen(Wall);
            Assert.IsTrue(wall.Play());
            yield return new WaitForSeconds(1.2f);
            yield return Press(InputManager.GameAction.Map);
            Assert.IsTrue(GameManager.Instance.IsMap, "the map stops the world");
            yield return new WaitForSecondsRealtime(0.3f);
            long frame = wall.Frame;
            float at = wall.Time;
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(frame, wall.Frame, "the clip holds while the world stops");
            Assert.AreEqual(at, wall.Time, 0.05f);
            Assert.IsTrue(wall.IsPlaying, "it is still on");
            yield return Press(InputManager.GameAction.Map);
            yield return new WaitForSeconds(0.8f);
            Assert.Greater(wall.Frame, frame, "and goes on after");
        }

        [UnityTest]
        public IEnumerator TheCart_TurnsOnAndOff_WithInteract_AndLoops()
        {
            InWorldScreen cart = Screen(Cart);
            Assert.IsTrue(cart.loop);
            lucy.GetComponent<TankController>().Teleport(cart.Point + new Vector3(0.6f, 0f, 0f) - Vector3.up * cart.Point.y, -90f);
            yield return new WaitForSeconds(0.2f);
            Interactor hands = lucy.GetComponent<Interactor>();
            Assert.AreSame(cart, hands.FindFocus(), "the cart's screen is in reach");
            StringAssert.Contains("turn on the screen", cart.Hint);
            yield return Press(InputManager.GameAction.Interact);
            Assert.IsTrue(cart.IsPlaying, "Interact turns it on, with no question");
            Assert.AreEqual(Interactor.State.Free, hands.Current, "her hands stay free");
            StringAssert.Contains("turn off", cart.Hint);

            cart.Seek(cart.Length - 0.5f);
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(cart.IsPlaying, "it loops");
            Assert.AreEqual(0, cart.Ends);
            yield return Press(InputManager.GameAction.Interact);
            Assert.IsFalse(cart.IsPlaying, "Interact turns it off");
            Assert.IsFalse(cart.ShowsClip);
        }

        [UnityTest]
        public IEnumerator AScreen_PausesAPlayingTape_AndTheTapeKeyPlaysItAgain()
        {
            ItemDefinition tape = Resources.Load<ItemDefinition>("Items/item_test_tape");
            Assert.IsNotNull(tape);
            lucy.GetComponent<ItemInventory>().TryAdd(tape);
            TapePlayer.Instance.Play(tape);
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(Screen(Wall).Play());
            yield return null;
            Assert.IsFalse(TapePlayer.Instance.IsPlaying, "two voices at once: the screen pauses the tape");
            Assert.AreEqual(InWorldScreen.PausedTape, TapePlayer.Instance.StoppedBy);
            // Bruno, 2026-11-02: "the player should be able to press a button to unpause; it shouldn't autoplay".
            InWorldScreen wall = Screen(Wall);
            wall.Seek(wall.Length - 0.5f);
            yield return WaitUntil(() => !wall.IsPlaying, 6f);
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(TapePlayer.Instance.IsPlaying, "the screen ends and the tape stays paused");
            yield return Press(InputManager.GameAction.Tape);
            Assert.IsTrue(TapePlayer.Instance.IsPlaying, "she plays it again with the tape key");
        }

        [UnityTest]
        public IEnumerator AScreenWithNoClip_RunsItsLinesAlone()
        {
            var obj = new GameObject("Screen_Card");
            obj.transform.position = lucy.transform.position + Vector3.forward;
            InWorldScreen card = obj.AddComponent<InWorldScreen>();
            card.lines.Add(new TapeLine { at = 0.2f, speaker = "PRESENTER", text = "A card with the words." });
            Assert.IsTrue(card.Play());
            yield return new WaitForSeconds(0.6f);
            SubtitleService.Line line = SubtitleService.Lines.FirstOrDefault(l => l.Owner == card.SubtitleOwner);
            Assert.IsNotNull(line, "the greybox card: its lines run on its own clock");
            Assert.AreEqual("PRESENTER", line.Speaker);
            yield return WaitUntil(() => !card.IsPlaying, card.Length + 2f);
            Assert.AreEqual(1, card.Ends, "it ends after its last line");
            UnityEngine.Object.Destroy(obj);
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static InWorldScreen Screen(string name)
        {
            GameObject obj = GameObject.Find(name);
            Assert.IsNotNull(obj, name + " (ScreenTestSetup.BuildScreenTest)");
            return obj.GetComponent<InWorldScreen>();
        }

        private IEnumerator Press(InputManager.GameAction action)
        {
            pad.Tap(action);
            yield return null;
            yield return null;
            yield return null;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
