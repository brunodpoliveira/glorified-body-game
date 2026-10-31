using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Video;
using GlorifiedBody.Core;
using GlorifiedBody.Editor;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>The web build (2026-10-31, itch.io first): the page keeps the saves, and every clip has a file name the web player can ask for.</summary>
    public class WebBuildTests
    {
        [Test]
        public void TheWebTemplate_KeepsTheSavesInTheBrowser()
        {
            PlayerBuild.EnsureWebTemplate();
            string html = File.ReadAllText(Path.Combine(PlayerBuild.WebTemplateFolder, "index.html"));
            StringAssert.Contains("config.autoSyncPersistentDataPath = true;", html);
            StringAssert.DoesNotContain("// config.autoSyncPersistentDataPath", html);
        }

        [Test]
        public void TheWebPlayer_GetsStandalonesDefines()
        {
            PlayerBuild.SyncWebDefines();
            string standalone = PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Standalone);
            StringAssert.Contains("INVECTOR_SHOOTER", standalone);
            Assert.AreEqual(standalone, PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.WebGL));
        }

        [Test]
        public void EveryClip_HasAWebFileName_WithItsSourceExtension()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:VideoClip", new[] { "Assets/_Game" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(path);
                Assert.AreEqual(Path.GetFileNameWithoutExtension(path) + Path.GetExtension(path), VideoClips.FileName(clip), path);
            }
        }
    }
}
