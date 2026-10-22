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
        public void TheScene_Exists_InTheBuildList_WithItsBake()
        {
            Assert.IsTrue(File.Exists(TutorialSetup.ScenePath), TutorialSetup.ScenePath + " (TutorialSetup.BuildTutorial)");
            Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == TutorialSetup.ScenePath), "Tutorial in Build Settings");
            string folder = Path.Combine(Path.GetDirectoryName(TutorialSetup.ScenePath), TutorialSetup.SceneName);
            Assert.IsTrue(Directory.Exists(folder), folder + " (TutorialSetup.BakeTutorial)");
            Assert.IsTrue(Directory.GetFiles(folder, "Lightmap-*").Any(), folder + ": a lightmap");
        }
    }
}
