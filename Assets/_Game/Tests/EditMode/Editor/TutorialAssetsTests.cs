using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using GlorifiedBody.Editor;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>The tutorial scene, as TutorialSetup.BuildTutorial and BakeTutorial write it. Batch: tools/unity.sh test editmode.</summary>
    public class TutorialAssetsTests
    {
        [Test]
        public void TheScene_Exists_InTheBuildList_WithNoBake()
        {
            Assert.IsTrue(File.Exists(TutorialSetup.ScenePath), TutorialSetup.ScenePath + " (TutorialSetup.BuildTutorial)");
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == TutorialSetup.ScenePath), "Tutorial in Build Settings");
            string folder = Path.Combine(Path.GetDirectoryName(TutorialSetup.ScenePath), TutorialSetup.SceneName);
            Assert.IsFalse(Directory.Exists(folder), folder + ": a greybox has no bake (Bruno, 2026-11-06)");
        }
    }
}
