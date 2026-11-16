using UnityEngine;

namespace GlorifiedBody.Systems
{
    /// <summary>
    /// The player finished the tutorial (Bruno, 2026-11-16: "add a flag to the user so we can know they done the tutorial, so as
    /// to not pester them if they did so already before clicking on start game"). Kept in PlayerPrefs. Batch runs keep their
    /// own key, so a test that completes the tutorial never marks the editor's player as done. TutorialDirector marks it at the
    /// end of the tutorial. MainMenuUI asks "Play the tutorial first?" at START until it is set.
    /// </summary>
    public static class TutorialRecord
    {
        private static string Key => Application.isBatchMode ? "batch_tutorial_done" : "tutorial_done";

        public static bool Done => PlayerPrefs.GetInt(Key, 0) == 1;

        /// <summary>The player chose the tutorial at START: at its end the game starts (MainMenuUI, TutorialDirector).</summary>
        public static bool StartGameAfter { get; set; }

        public static void MarkDone()
        {
            PlayerPrefs.SetInt(Key, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Tests: the record forgotten.</summary>
        public static void Forget()
        {
            PlayerPrefs.DeleteKey(Key);
            StartGameAfter = false;
        }
    }
}
