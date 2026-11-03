using System.IO;
using System.Linq;
using NUnit.Framework;
using GlorifiedBody.Editor;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>The player build (2026-10-31, rank 16): a release has the menu, the tutorial and the demo (START), no test room and no training room (2026-11-02).</summary>
    public class PlayerBuildTests
    {
        [Test]
        public void TheRelease_StartsAtTheMenu_AndHasNoTestOrLookRoom()
        {
            var scenes = PlayerBuild.ReleaseScenes().Select(Path.GetFileNameWithoutExtension).ToList();
            Assert.AreEqual("Main_Menu", scenes[0], "the menu loads first");
            CollectionAssert.Contains(scenes, "Demo", "START: the world scene (2026-11-02)");
            CollectionAssert.Contains(scenes, "Tutorial", "the tutorial mode");
            CollectionAssert.DoesNotContain(scenes, "TrainingRoom", "the training room is dev only (Bruno, 2026-11-02)");
            Assert.IsFalse(scenes.Any(s => s.EndsWith("Test") || s.StartsWith("LookTest") || s.StartsWith("TravelTest")), "no test or look room: " + string.Join(", ", scenes));
            Assert.AreEqual(new[] { "Main_Menu", "Tutorial", "Demo" }, scenes.ToArray(), "the menu, the tutorial, the demo: one scene, no room scenes");
        }

        [Test]
        public void TheDevBuild_HasEveryRoom()
        {
            var scenes = PlayerBuild.DevScenes().Select(Path.GetFileNameWithoutExtension).ToList();
            Assert.AreEqual("Main_Menu", scenes[0]);
            CollectionAssert.Contains(scenes, "KitTest");
            CollectionAssert.Contains(scenes, "ScreenTest");
            CollectionAssert.Contains(scenes, "TrainingRoom");
            Assert.Greater(scenes.Count, PlayerBuild.ReleaseScenes().Count);
        }
    }
}
