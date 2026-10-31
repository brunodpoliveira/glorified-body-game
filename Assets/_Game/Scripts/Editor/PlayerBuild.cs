#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The player build (2026-10-31, the design sweep's rank 16: "a build script, a dev flag for the debug keys and the
    /// test-room buttons"). A release build has the menu, the training room, the tutorial and the demo rooms (Demo_*),
    /// no test or look room, and no debug keys (GameBuild.Dev). A dev build has every scene of the build settings and is
    /// a development build. Linux or Windows. The output goes to builds/&lt;kind&gt;_&lt;target&gt;/ (git- and MEGA-ignored).
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.PlayerBuild.Release -gbTarget linux|windows
    ///        tools/unity.sh exec GlorifiedBody.Editor.PlayerBuild.Dev -gbTarget linux|windows
    /// </summary>
    public static class PlayerBuild
    {
        public const string ProductFile = "TheGlorifiedBody";

        /// <summary>The scenes of a release, in order: the menu first. The demo rooms are the scenes named Demo_*.</summary>
        public static List<string> ReleaseScenes()
        {
            var scenes = new List<string> { MenuLoopSetup.MainMenuScenePath, MenuLoopSetup.TrainingRoomScenePath, TutorialSetup.ScenePath };
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (Path.GetFileNameWithoutExtension(s.path).StartsWith("Demo_") && !scenes.Contains(s.path)) scenes.Add(s.path);
            return scenes.Where(File.Exists).ToList();
        }

        /// <summary>Every scene of the build settings, the menu first.</summary>
        public static List<string> DevScenes()
        {
            var scenes = new List<string> { MenuLoopSetup.MainMenuScenePath };
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (s.enabled && !scenes.Contains(s.path) && File.Exists(s.path)) scenes.Add(s.path);
            return scenes;
        }

        [MenuItem("GlorifiedBody/Build/Release (Linux)")]
        public static void Release() => Build(dev: false);

        [MenuItem("GlorifiedBody/Build/Dev (Linux)")]
        public static void Dev() => Build(dev: true);

        private static void Build(bool dev)
        {
            string target = Automation.AutomationArgs.Get("-gbTarget", "linux").ToLowerInvariant();
            BuildTarget buildTarget = target == "windows" ? BuildTarget.StandaloneWindows64 : BuildTarget.StandaloneLinux64;
            string extension = buildTarget == BuildTarget.StandaloneWindows64 ? ".exe" : ".x86_64";
            string folder = Path.Combine("builds", $"{(dev ? "dev" : "release")}_{target}");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            var options = new BuildPlayerOptions
            {
                scenes = (dev ? DevScenes() : ReleaseScenes()).ToArray(),
                locationPathName = Path.Combine(folder, ProductFile + extension),
                target = buildTarget,
                options = dev ? BuildOptions.Development : BuildOptions.None,
            };
            Debug.Log($"[PlayerBuild] {(dev ? "dev" : "release")} {target}: {options.scenes.Length} scenes: {string.Join(", ", options.scenes.Select(Path.GetFileNameWithoutExtension))}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            string line = $"[PlayerBuild] {summary.result}: {summary.outputPath}, {summary.totalSize / (1024f * 1024f):0} MB, {summary.totalTime.TotalSeconds:0} s, {summary.totalErrors} errors, {summary.totalWarnings} warnings";
            if (summary.result == BuildResult.Succeeded) Debug.Log(line);
            else Debug.LogError(line);
            string runDir = Automation.AutomationArgs.Get("-gbRunDir");
            if (!string.IsNullOrEmpty(runDir))
                File.WriteAllText(Path.Combine(runDir, "build.json"),
                    $"{{\"result\": \"{summary.result}\", \"path\": \"{summary.outputPath}\", \"mb\": {summary.totalSize / (1024f * 1024f):0}, \"seconds\": {summary.totalTime.TotalSeconds:0}, \"scenes\": {options.scenes.Length}}}\n");
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
#endif
