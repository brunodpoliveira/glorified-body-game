using System.IO;
using System.Linq;
using NUnit.Framework;
using GlorifiedBody.Editor;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>The player build (2026-10-31, rank 16): a release has the menu, the training room, the tutorial and the demo, no test room.</summary>
    public class PlayerBuildTests
    {
        [Test]
        public void TheRelease_StartsAtTheMenu_AndHasNoTestOrLookRoom()
        {
            var scenes = PlayerBuild.ReleaseScenes().Select(Path.GetFileNameWithoutExtension).ToList();
            Assert.AreEqual("Main_Menu", scenes[0], "the menu loads first");
            CollectionAssert.Contains(scenes, "TrainingRoom", "START");
            CollectionAssert.Contains(scenes, "Tutorial", "the tutorial mode");
            Assert.IsFalse(scenes.Any(s => s.EndsWith("Test") || s.StartsWith("LookTest") || s.StartsWith("TravelTest")), "no test or look room: " + string.Join(", ", scenes));
            Assert.IsTrue(scenes.Skip(3).All(s => s.StartsWith("Demo_")), "the rest are the demo rooms");
        }

        [Test]
        public void TheDevBuild_HasEveryRoom()
        {
            var scenes = PlayerBuild.DevScenes().Select(Path.GetFileNameWithoutExtension).ToList();
            Assert.AreEqual("Main_Menu", scenes[0]);
            CollectionAssert.Contains(scenes, "KitTest");
            CollectionAssert.Contains(scenes, "ScreenTest");
            Assert.Greater(scenes.Count, PlayerBuild.ReleaseScenes().Count);
        }
    }
}
