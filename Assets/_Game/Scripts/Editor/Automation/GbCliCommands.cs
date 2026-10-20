#if UNITY_EDITOR
using System.IO;
using Unity.Pipeline.Commands;

namespace GlorifiedBody.Editor.Automation
{
    /// <summary>
    /// Warm-editor entry points for tools/unity.sh wcmd. Batch runs use -executeMethod on the same classes.
    /// </summary>
    public static class GbCliCommands
    {
        [CliCommand("gb_sanity", "Run WorldSanity on a scene and write sanity-<scene>.json to runDir")]
        public static string Sanity(
            [CliArg("scene", "Scene name or path", Required = true)] string scene,
            [CliArg("runDir", "Output folder", Required = true)] string runDir)
        {
            return WorldSanity.RunOn(scene, runDir) ? "PASS" : "FAIL";
        }

        [CliCommand("gb_shots", "Capture edit-mode screenshots of a scene into runDir/shots")]
        public static int Shots(
            [CliArg("scene", "Scene name or path", Required = true)] string scene,
            [CliArg("runDir", "Output folder", Required = true)] string runDir)
        {
            return ShotCapture.CaptureScene(scene, Path.Combine(runDir, "shots"));
        }

        [CliCommand("gb_play", "Enter Play mode with a bot. Returns at once. Poll runDir/playthrough.json")]
        public static string Play(
            [CliArg("runDir", "Output folder", Required = true)] string runDir,
            [CliArg("level", "Level scene to load from the menu")] string level = "TrainingRoom",
            [CliArg("startScene", "Scene to open first")] string startScene = "Main_Menu",
            [CliArg("bot", "structural or input")] string bot = "structural",
            [CliArg("timeout", "Seconds")] string timeout = "900")
        {
            PlaythroughRunner.Begin(runDir, startScene, level, bot, timeout, exitWhenDone: false);
            return "started";
        }

        [CliCommand("gb_build_menu_loop", "Rebuild Main_Menu.unity and TrainingRoom.unity from code")]
        public static string BuildMenuLoop()
        {
            MenuLoopSetup.BuildMenuLoopScenes();
            return "ok";
        }
    }
}
#endif
