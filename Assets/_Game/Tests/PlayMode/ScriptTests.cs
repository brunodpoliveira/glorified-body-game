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
using GlorifiedBody.Automation;
using GlorifiedBody.Core;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;
using GlorifiedBody.Subtitles;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// Scripted events, story flags and cutscenes (2026-10-26, the design sweep's ranks 2 and 4), in the SCRIPT TEST
    /// room (in git: ScriptTestSetup.BuildScriptTest): the ultimatum plays its lines in order with their speakers,
    /// holds her still, sets her SHORT, calls code silver and opens the gate, once; walking in starts it; the flag and
    /// the open gate survive a save and a load; a cutscene stops the game, shows its stills and its video with their
    /// lines, and gives the game back; the skip key ends it at once. Batch: tools/unity.sh test playmode ScriptTests.
    /// </summary>
    public class ScriptTests
    {
        private const string Room = "ScriptTest";

        [DefaultExecutionOrder(-1000)]
        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad pad;
            private void Update() => pad?.Commit();
        }

        private VirtualPad pad;
        private PadDriver driver;
        private GameObject lucy;
        private ScriptedSequence ultimatum;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_script_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(Room);
            yield return InRoom();
            pad = new VirtualPad();
            driver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            UnityEngine.Object.DontDestroyOnLoad(driver.gameObject);
            driver.pad = pad;
            GameInput.SetBotSource(pad);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (pad != null)
            {
                pad.ReleaseAll();
                GameInput.ClearBotSource(pad);
            }
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            if (CutscenePlayer.IsPlaying) CutscenePlayer.Skip();
            yield return WaitUntil(() => !CutscenePlayer.IsPlaying, 5f);
            SaveSystem.Folder = null;
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator TheUltimatum_SaysItsLinesInOrder_HoldsHer_ThenOpensTheGate_Once()
        {
            GameObject barrier = GameObject.Find("Gate_Barrier");
            Assert.IsNotNull(barrier, "the gate is shut at the start");
            var ids = new List<string>();
            var speakers = new List<string>();
            string lastId = Voice.LastId; // a line of an earlier test is not one of these
            Assert.IsTrue(ultimatum.Run(), "it starts");
            TankController tank = lucy.GetComponent<TankController>();
            bool held = false;
            float t0 = Time.realtimeSinceStartup;
            while (ultimatum.Running && Time.realtimeSinceStartup - t0 < 60f)
            {
                if (Voice.LastId != lastId)
                {
                    lastId = Voice.LastId;
                    ids.Add(lastId);
                    SubtitleService.Line line = SubtitleService.Lines.LastOrDefault();
                    if (line != null) speakers.Add(line.Speaker);
                }
                if (tank.MovementLocked) held = true;
                yield return null;
            }
            Debug.Log("[ScriptTests] lines: " + string.Join(" | ", ids) + " / speakers: " + string.Join(", ", speakers));
            Assert.IsFalse(ultimatum.Running, "it ends");
            CollectionAssert.AreEqual(new[]
            {
                "scientist.computer_subject_has_escaped", "computer.with_a_firm_tone", "scientist.lie_down_on_the_ground",
                "scientist.subject_did_not_comply", "computer.approach_and_repeat", "pa.code_silver",
            }, ids, "the lines in order");
            CollectionAssert.IsSubsetOf(new[] { "SCIENTIST", "COMPUTER", "PA" }, speakers, "every line says its speaker");
            Assert.IsTrue(held, "she stands still while they talk");
            Assert.IsFalse(tank.MovementLocked, "then she moves again");
            Assert.GreaterOrEqual(lucy.GetComponent<PsychicStats>().Short, 80f, "her SHORT near full (90, it drains a little)");
            Assert.IsTrue(StoryFlags.Has(ScriptTestSetupFlag), "the flag is up");
            Assert.IsFalse(barrier.activeInHierarchy, "the gate opened");
            Assert.IsFalse(ultimatum.Run(), "it runs once");
        }

        [UnityTest]
        public IEnumerator WalkingIntoTheTrigger_StartsTheSequence()
        {
            pad.SetHeld(InputManager.GameAction.Forward, true);
            yield return WaitUntil(() => ultimatum.Running, 5f);
            pad.ReleaseAll();
            Assert.IsTrue(ultimatum.Running, "she walked into the trigger");
        }

        [UnityTest]
        public IEnumerator TheFlag_AndTheOpenGate_SurviveASaveAndALoad()
        {
            StoryFlags.Raise(ScriptTestSetupFlag);
            yield return null;
            Assert.IsFalse(GameObject.Find("[Gate]").transform.GetChild(0).gameObject.activeSelf, "the flag opens the gate");
            SavePoint point = UnityEngine.Object.FindFirstObjectByType<SavePoint>();
            Assert.IsTrue(SaveSystem.SaveManual(1, point.SpotId));
            StoryFlags.Drop(ScriptTestSetupFlag);
            yield return null;
            Assert.IsTrue(GameObject.Find("[Gate]").transform.GetChild(0).gameObject.activeSelf, "the gate shuts with the flag down");

            int applied = SaveSystem.Applies;
            Assert.IsTrue(SaveSystem.Load(1));
            yield return WaitUntil(() => SaveSystem.Applies > applied, 10f);
            yield return InRoom();
            Assert.IsTrue(StoryFlags.Has(ScriptTestSetupFlag), "the save kept the flag");
            Assert.IsFalse(GameObject.Find("[Gate]").transform.GetChild(0).gameObject.activeSelf, "the gate is open after the load");
            Assert.IsFalse(ultimatum.Run(), "the sequence does not run again");
        }

        [UnityTest]
        public IEnumerator ACutscene_StopsTheGame_ShowsItsShotsAndLines_AndGivesTheGameBack()
        {
            CutsceneAsset asset = Resources.Load<CutsceneAsset>("Cutscenes/cutscene_test");
            Assert.IsNotNull(asset, "the test cutscene (ScriptTestSetup)");
            Assert.IsTrue(CutscenePlayer.Play(asset), "it starts");
            yield return WaitUntil(() => CutscenePlayer.ShotIndex >= 0, 3f);
            Assert.IsTrue(GameManager.Instance.IsCutscene, "the Cutscene state");
            Assert.AreEqual(0f, Time.timeScale, "the world stops");
            Assert.IsFalse(GameManager.Instance.AllowsAction(InputManager.GameAction.Forward), "no game key passes");
            Assert.IsTrue(GameManager.Instance.AllowsAction(InputManager.GameAction.Interact), "the skip key passes");
            Assert.IsTrue(AudioListener.pause, "the world sounds pause");

            var seen = new List<string>();
            bool voiced = false;
            int lastShot = -1;
            float t0 = Time.realtimeSinceStartup;
            while (CutscenePlayer.IsPlaying && Time.realtimeSinceStartup - t0 < 30f)
            {
                if (CutscenePlayer.ShotIndex != lastShot && CutscenePlayer.ShotIndex >= 0)
                {
                    lastShot = CutscenePlayer.ShotIndex;
                    yield return null;
                    SubtitleService.Line line = SubtitleService.Lines.FirstOrDefault(l => l.Owner == CutscenePlayer.SubtitleKey);
                    seen.Add($"{lastShot}:{(CutscenePlayer.Showing != null ? CutscenePlayer.Showing.name : "none")}:{line?.Speaker}");
                    if (lastShot == 1) voiced = Voice.LastHadClip && Voice.LastId == "test.voice_check";
                    if (lastShot == 0) { yield return new WaitForSecondsRealtime(0.6f); OverlayShot("cutscene_shot0.png"); }
                }
                yield return null;
            }
            Debug.Log("[ScriptTests] shots: " + string.Join(" | ", seen));
            Assert.AreEqual(3, seen.Count, "three shots");
            StringAssert.StartsWith("0:scan_placeholder_image:STEVENS", seen[0]);
            StringAssert.StartsWith("1:gear_emblem:LUCY", seen[1]);
            StringAssert.StartsWith("2:cutscene_video:STEVENS", seen[2], "the video shot");
            Assert.IsTrue(voiced, "the recorded line played");
            yield return WaitUntil(() => GameManager.Instance.IsPlaying, 3f);
            Assert.IsTrue(GameManager.Instance.IsPlaying, "the game plays again");
            Assert.AreEqual(1f, Time.timeScale, 0.01f);
            Assert.IsFalse(AudioListener.pause);
            Assert.IsTrue(StoryFlags.Has(asset.DoneFlag), "its flag is up");
        }

        [UnityTest]
        public IEnumerator TheSkipKey_EndsTheCutsceneAtOnce()
        {
            CutsceneAsset asset = Resources.Load<CutsceneAsset>("Cutscenes/cutscene_test");
            int skipped = CutscenePlayer.Skipped;
            Assert.IsTrue(CutscenePlayer.Play(asset));
            yield return WaitUntil(() => CutscenePlayer.ShotIndex >= 0, 3f);
            yield return new WaitForSecondsRealtime(CutscenePlayer.SkipGuardSeconds + 0.1f);
            pad.Tap(InputManager.GameAction.Interact);
            yield return WaitUntil(() => !CutscenePlayer.IsPlaying, 3f);
            Assert.IsFalse(CutscenePlayer.IsPlaying, "the key ended it");
            Assert.AreEqual(skipped + 1, CutscenePlayer.Skipped);
            Assert.IsTrue(GameManager.Instance.IsPlaying, "the game plays");
            Assert.IsTrue(StoryFlags.Has(asset.DoneFlag), "a skipped cutscene counts as seen");
        }

        /// <summary>The frame with every overlay canvas on it (the cutscene, the subtitles), for Bruno.</summary>
        private static void OverlayShot(string name)
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "glorified-body-renders", "game", "story", "2026-10-26");
            Camera cam = Camera.main;
            if (cam == null) return;
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.enabled && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            foreach (Canvas c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + 0.05f + (1000 - c.sortingOrder) * 0.0001f;
            }
            Canvas.ForceUpdateCanvases();
            try { ShotRenderer.Render(cam, Path.Combine(folder, name), 1920, 1080, useTargetTexture: true); }
            finally { foreach (Canvas c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay; }
        }

        private const string ScriptTestSetupFlag = "ultimatum_done";

        private IEnumerator InRoom()
        {
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Room && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            Assert.AreEqual(Room, SceneManager.GetActiveScene().name, "the script test room (ScriptTestSetup.BuildScriptTest)");
            lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            ultimatum = GameObject.Find("Seq_Ultimatum").GetComponent<ScriptedSequence>();
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
