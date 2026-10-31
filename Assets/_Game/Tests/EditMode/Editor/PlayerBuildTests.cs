using System.IO;
using System.Linq;
using NUnit.Framework;
using GlorifiedBody.Editor;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>The player build (2026-10-31, rank 16): a release has the menu, the training room, the tutorial and the demo (START), no test room.</summary>
    public class PlayerBuildTests
    {
        [Test]
        public void TheRelease_StartsAtTheMenu_AndHasNoTestOrLookRoom()
        {
            var scenes = PlayerBuild.ReleaseScenes().Select(Path.GetFileNameWithoutExtension).ToList();
            Assert.AreEqual("Main_Menu", scenes[0], "the menu loads first");
            CollectionAssert.Contains(scenes, "Demo_W1", "START");
            CollectionAssert.Contains(scenes, "Tutorial", "the tutorial mode");
            CollectionAssert.Contains(scenes, "TrainingRoom", "the training room is for the players too");
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
