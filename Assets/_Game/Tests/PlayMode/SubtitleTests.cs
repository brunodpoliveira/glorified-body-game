#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Timeline;
using UnityEngine.UI;
using GlorifiedBody.Enemy;
using GlorifiedBody.Subtitles;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The subtitles (Bruno, 2026-10-25: "on by default; move to the bottom of the screen. subtitles should be yellow
    /// with black outlines, and subtitle must always identify speaker"; in-game and in cutscenes). In the training
    /// room: a bark shows at the bottom with its speaker, yellow with a black outline; Lucy is LUCY; the option hides
    /// them and sets the size; three lines at most, one per speaker; a line goes when its time is over; a Timeline
    /// clip shows its line for its time. Batch: tools/unity.sh test playmode SubtitleTests.
    /// </summary>
    public class SubtitleTests
    {
        private bool savedOn;
        private int savedSize;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel("TrainingRoom");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "TrainingRoom" && GameManager.Instance.IsPlaying, 10f);
            savedOn = SettingsManager.Instance.SubtitlesOn;
            savedSize = SettingsManager.Instance.SubtitleSize;
            SettingsManager.Instance.SubtitlesOn = true;
            SettingsManager.Instance.SubtitleSize = 1;
            SubtitleService.Clear();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SettingsManager.Instance.SubtitlesOn = savedOn;
            SettingsManager.Instance.SubtitleSize = savedSize;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ABark_ShowsAtTheBottom_YellowWithABlackOutline_WithItsSpeaker()
        {
            GameObject nurse = new GameObject("Nurse_Test");
            nurse.AddComponent<Speaker>().DisplayName = "NURSE";
            Bark.Say(nurse, "Don't move. Don't try to move, I'm coming.");
            yield return null;
            yield return null;
            SubtitleUI ui = SubtitleUI.Instance;
            Assert.IsNotNull(ui, "the subtitle overlay exists");
            Debug.Log($"[SubtitleTests] shown: {string.Join(" | ", ui.Shown)}");
            Assert.Contains("NURSE: Don't move. Don't try to move, I'm coming.", ui.Shown.ToList(), "the speaker, then the words");
            Text text = ui.Texts.First(t => t.gameObject.activeSelf);
            Assert.AreEqual(SubtitleUI.Yellow, text.color, "yellow");
            Outline outline = text.GetComponent<Outline>();
            Assert.IsNotNull(outline, "an outline");
            Assert.AreEqual(Color.black, outline.effectColor, "a black outline");
            var corners = new Vector3[4];
            text.rectTransform.GetWorldCorners(corners);
            Debug.Log($"[SubtitleTests] line bottom {corners[0].y:0} px of {Screen.height}");
            Assert.Less(corners[0].y, Screen.height * 0.2f, "at the bottom of the screen");
            Assert.Greater(corners[0].y, 0f, "on the screen");
            UnityEngine.Object.Destroy(nurse);
        }

        [UnityTest]
        public IEnumerator EveryLine_NamesItsSpeaker_LucyIsLucy()
        {
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            Bark.Say(lucy, "[screams]");
            GameObject unnamed = new GameObject("Lab_Worker_9");
            Bark.Say(unnamed, "Is she gone?");
            yield return null;
            yield return null;
            var shown = SubtitleUI.Instance.Shown.ToList();
            Debug.Log($"[SubtitleTests] shown: {string.Join(" | ", shown)}");
            Assert.Contains("LUCY: [screams]", shown);
            Assert.Contains("LAB WORKER 9: Is she gone?", shown, "with no Speaker, its own name");
            Assert.AreEqual(("COMPUTER", "Subject contained."), SubtitleService.Split("COMPUTER: Subject contained.", "VOICE"));
            Assert.AreEqual(("VOICE", "No name here."), SubtitleService.Split("No name here.", "VOICE"));
            UnityEngine.Object.Destroy(unnamed);
        }

        [UnityTest]
        public IEnumerator TheOption_HidesThem_AndSetsTheSize()
        {
            SubtitleService.Say("test", "LUCY", "Where am I?", 5f);
            yield return null;
            Assert.IsTrue(SubtitleUI.Instance.Visible, "on by default");
            Assert.AreEqual(SubtitleUI.Sizes[1], SubtitleUI.Instance.FontSize, "medium");
            SettingsManager.Instance.SubtitleSize = 2;
            yield return null;
            Assert.AreEqual(SubtitleUI.Sizes[2], SubtitleUI.Instance.FontSize, "large");
            SettingsManager.Instance.SubtitlesOn = false;
            yield return null;
            Assert.IsFalse(SubtitleUI.Instance.Visible, "off");
            Assert.AreEqual(0, SubtitleUI.Instance.Shown.Count(), "nothing shows");
        }

        [UnityTest]
        public IEnumerator ThreeLinesAtMost_OnePerSpeaker_TheyGoWhenTheirTimeIsOver()
        {
            SubtitleService.Say("a", "NURSE", "One.", 0.1f);
            SubtitleService.Say("b", "GUARD", "Two.", 9f);
            SubtitleService.Say("c", "SOLDIER", "Three.", 9f);
            SubtitleService.Say("d", "LAB WORKER", "Four.", 9f);
            SubtitleService.Say("b", "GUARD", "Two again.", 9f);
            yield return null;
            var shown = SubtitleUI.Instance.Shown.ToList();
            Debug.Log($"[SubtitleTests] shown: {string.Join(" | ", shown)}");
            Assert.AreEqual(3, shown.Count, "three at most");
            Assert.IsFalse(shown.Contains("NURSE: One."), "the oldest went first");
            Assert.AreEqual("GUARD: Two again.", shown.Last(), "a new line of the same speaker replaces the old one, at the bottom");
            Assert.AreEqual(1, shown.Count(s => s.StartsWith("GUARD")), "one line per speaker");
            SubtitleService.Clear();
            SubtitleService.Say("e", "NURSE", "Short.", 0.1f);
            yield return null;
            Assert.AreEqual(1, SubtitleUI.Instance.Shown.Count());
            yield return new WaitForSeconds(SubtitleService.ReadSeconds("Short.") + 0.3f);
            Assert.AreEqual(0, SubtitleUI.Instance.Shown.Count(), "it goes when its reading time is over");
        }

        [UnityTest]
        public IEnumerator ACutsceneClip_ShowsItsLine_ForItsTime()
        {
            TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            SubtitleTrack track = timeline.CreateTrack<SubtitleTrack>(null, "Subtitles");
            TimelineClip clip = track.CreateClip<SubtitleClip>();
            clip.start = 0.3;
            clip.duration = 0.8;
            var line = (SubtitleClip)clip.asset;
            line.speaker = "PRESENTER";
            line.line = "Welcome back, Lucy.";
            var holder = new GameObject("Cutscene");
            var director = holder.AddComponent<PlayableDirector>();
            director.playableAsset = timeline;
            director.Play();
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(0, SubtitleUI.Instance == null ? 0 : SubtitleUI.Instance.Shown.Count(), "nothing before the clip");
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[SubtitleTests] cutscene: {string.Join(" | ", SubtitleUI.Instance.Shown)}");
            Assert.Contains("PRESENTER: Welcome back, Lucy.", SubtitleUI.Instance.Shown.ToList(), "the clip's line");
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(0, SubtitleUI.Instance.Shown.Count(), "gone after the clip");
            UnityEngine.Object.Destroy(holder);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
