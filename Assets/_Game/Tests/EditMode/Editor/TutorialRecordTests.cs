using NUnit.Framework;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>
    /// The tutorial flag (Bruno, 2026-11-16: "add a flag to the user so we can know they done the tutorial"). In a batch run
    /// the record keeps its own key: these tests never touch the editor player's flag.
    /// </summary>
    public class TutorialRecordTests
    {
        [TearDown]
        public void TearDown() => TutorialRecord.Forget();

        [Test]
        public void TheRecord_StartsEmpty_AndTheEndOfTheTutorialSetsIt()
        {
            TutorialRecord.Forget();
            Assert.IsFalse(TutorialRecord.Done, "a new player: START asks");
            TutorialRecord.MarkDone();
            Assert.IsTrue(TutorialRecord.Done, "after the tutorial: START does not ask");
        }

        [Test]
        public void Forget_ClearsTheRecord_AndTheStartAfterTheTutorial()
        {
            TutorialRecord.MarkDone();
            TutorialRecord.StartGameAfter = true;
            TutorialRecord.Forget();
            Assert.IsFalse(TutorialRecord.Done);
            Assert.IsFalse(TutorialRecord.StartGameAfter);
        }
    }
}
