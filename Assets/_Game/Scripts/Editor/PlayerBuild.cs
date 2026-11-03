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
    /// test-room buttons"). A release build has the menu, the tutorial and the demo (the world scene Demo; START loads it),
    /// no test or look room, and no debug keys (GameBuild.Dev). A dev build has every scene of the build settings and is
    /// a development build. Linux, Windows (Mono: IL2CPP for Windows does not build on Linux) or web (itch.io). The output
    /// goes to builds/&lt;kind&gt;_&lt;target&gt;/ (git- and MEGA-ignored).
    /// Batch: tools/unity.sh build release|dev linux|windows|web
    /// The web build: the page template keeps the saves in the browser (autoSyncPersistentDataPath), gzip without the
    /// fallback (itch.io sends the gzip header), the clips go to StreamingAssets/Video for the build only (the web
    /// player plays video only by URL), and WebTexturePostprocessor compresses the textures that import uncompressed.
    /// </summary>
    public static class PlayerBuild
    {
        public const string ProductFile = "TheGlorifiedBody";

        /// <summary>The scenes of a release, in order: the menu first. The demo is the scene named Demo (one world, 2026-11-02).</summary>
        public static List<string> ReleaseScenes()
        {
            // No training room: it is dev only (Bruno, 2026-11-02).
            var scenes = new List<string> { MenuLoopSetup.MainMenuScenePath, TutorialSetup.ScenePath };
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (Path.GetFileNameWithoutExtension(s.path).StartsWith("Demo") && !scenes.Contains(s.path)) scenes.Add(s.path);
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
            bool web = target == "web";
            BuildTarget buildTarget = web ? BuildTarget.WebGL : target == "windows" ? BuildTarget.StandaloneWindows64 : BuildTarget.StandaloneLinux64;
            string extension = buildTarget == BuildTarget.StandaloneWindows64 ? ".exe" : ".x86_64";
            string folder = Path.Combine("builds", $"{(dev ? "dev" : "release")}_{target}");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            if (web) PrepareWeb();
            var options = new BuildPlayerOptions
            {
                scenes = (dev ? DevScenes() : ReleaseScenes()).ToArray(),
                locationPathName = web ? folder : Path.Combine(folder, ProductFile + extension), // the web build is a folder (index.html)
                target = buildTarget,
                options = dev ? BuildOptions.Development : BuildOptions.None,
            };
            Debug.Log($"[PlayerBuild] {(dev ? "dev" : "release")} {target}: {options.scenes.Length} scenes: {string.Join(", ", options.scenes.Select(Path.GetFileNameWithoutExtension))}");
            BuildReport report;
            try { report = BuildPipeline.BuildPlayer(options); }
            finally
            {
                if (web) RemoveWebVideos();
                // A build leaves the editor on its target. On WebGL the vendor editor code does not compile (its defines are
                // Standalone's), and with no asmdefs that stops every editor script: back to Linux, always.
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneLinux64)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
            }
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

        // ── The web build ─────────────────────────────────────────────

        public const string WebTemplateName = "GlorifiedBody";
        public const string WebTemplateFolder = "Assets/WebGLTemplates/" + WebTemplateName;
        private const string WebVideoFolder = "Assets/StreamingAssets/" + Core.VideoClips.Folder;

        private static void PrepareWeb()
        {
            EnsureWebTemplate();
            SyncWebDefines();
            PlayerSettings.WebGL.template = "PROJECT:" + WebTemplateName;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;   // itch.io finds gzip by content and sends the header
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;                                // the browser keeps the data between visits
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            // Less generated C++ and less memory: the IL2CPP step of the second web build grew to 4.7 GB and the kernel
            // killed it (2026-10-31, a global out-of-memory on this 15 GB machine).
            PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.WebGL.maximumMemorySize = 4096;                          // the wasm32 limit; the data alone is some hundred MB
            PlayerSettings.defaultWebScreenWidth = 1280;                            // the itch.io frame: 1280 x 720
            PlayerSettings.defaultWebScreenHeight = 720;
            CopyWebVideos();
        }

        /// <summary>
        /// The web player gets Standalone's scripting defines (INVECTOR_SHOOTER and the rest): without them the first web
        /// build compiled the Invector code with its define-gated parts left out (2026-10-31).
        /// </summary>
        public static void SyncWebDefines()
        {
            string defines = PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Standalone);
            if (PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.WebGL) != defines)
                PlayerSettings.SetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.WebGL, defines);
        }

        /// <summary>The page template: Unity's Default with the saves kept (autoSyncPersistentDataPath, off by default).</summary>
        public static void EnsureWebTemplate()
        {
            string source = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/WebGLSupport/BuildTools/WebGLTemplates/Base/Default");
            if (!Directory.Exists(source)) throw new DirectoryNotFoundException("the web build module: " + source);
            Directory.CreateDirectory(WebTemplateFolder);
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string to = Path.Combine(WebTemplateFolder, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(file, to, true);
            }
            string index = Path.Combine(WebTemplateFolder, "index.html");
            string html = File.ReadAllText(index);
            const string off = "// config.autoSyncPersistentDataPath = true;";
            if (!html.Contains(off)) throw new InvalidDataException("the Default template changed: no '" + off + "' line in " + index);
            html = html.Replace(off, "config.autoSyncPersistentDataPath = true; // The Glorified Body: the saves stay in the browser");
            html = html.Replace("<title>Unity Web Player | {{{ PRODUCT_NAME }}}</title>", "<title>{{{ PRODUCT_NAME }}}</title>");
            // The canvas fills the page (the itch.io frame, or its fullscreen): no footer, no scrollbar (the Default
            // template puts a footer under a fixed 1280 x 720 canvas). The CSS wins over the template's inline sizes.
            const string css = "<link rel=\"stylesheet\" href=\"TemplateData/style.css\">";
            if (!html.Contains(css)) throw new InvalidDataException("the Default template changed: no style.css link in " + index);
            html = html.Replace(css, css + "\n    <style>/* The Glorified Body: the canvas fills the page */ html, body { width: 100%; height: 100%; overflow: hidden; background: #000 } "
                + "#unity-container, #unity-container.unity-desktop { position: fixed; left: 0; top: 0; width: 100%; height: 100%; transform: none } "
                + "#unity-canvas { width: 100% !important; height: 100% !important; display: block; background: #000 } #unity-footer { display: none }</style>");
            File.WriteAllText(index, html);
            AssetDatabase.Refresh();
        }

        /// <summary>Every clip of the game into StreamingAssets/Video under the name the player asks for (VideoClips.FileName).</summary>
        private static void CopyWebVideos()
        {
            Directory.CreateDirectory(WebVideoFolder);
            foreach (string guid in AssetDatabase.FindAssets("t:VideoClip", new[] { "Assets/_Game" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(path);
                if (clip == null) continue;
                File.Copy(path, Path.Combine(WebVideoFolder, Core.VideoClips.FileName(clip)), true);
                Debug.Log($"[PlayerBuild] web video: {path} -> {WebVideoFolder}/{Core.VideoClips.FileName(clip)}");
            }
            AssetDatabase.Refresh();
        }

        private static void RemoveWebVideos()
        {
            if (Directory.Exists(WebVideoFolder)) Directory.Delete(WebVideoFolder, true);
            if (File.Exists(WebVideoFolder + ".meta")) File.Delete(WebVideoFolder + ".meta");
            const string streaming = "Assets/StreamingAssets";
            if (Directory.Exists(streaming) && Directory.GetFileSystemEntries(streaming).Length == 0)
            {
                Directory.Delete(streaming);
                if (File.Exists(streaming + ".meta")) File.Delete(streaming + ".meta");
            }
            AssetDatabase.Refresh();
        }
    }
}
#endif
