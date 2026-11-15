#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Subtitles;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The two-voice rule of the barks (Bruno, 2026-11-15: "two people can bark at a time, no more"; "two people of two
    /// different archetypes ... never two nurses, two soldiers etc at the same time"). Batch: tools/unity.sh test playmode BarkVoicesTests.
    /// </summary>
    public class BarkVoicesTests
    {
        private GameObject soldierA, soldierB, guard, nurse;

        private static GameObject Person(string name, string enemy)
        {
            var obj = new GameObject(name);
            obj.AddComponent<BarkVoice>().Init(enemy, "", enemy != "nurse");
            return obj;
        }

        [SetUp]
        public void SetUp()
        {
            Bark.ResetVoices();
            SubtitleService.Clear();
            soldierA = Person("Soldier_A", "soldier");
            soldierB = Person("Soldier_B", "soldier");
            guard = Person("Guard_A", "guard");
            nurse = Person("Nurse_A", "nurse");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject o in new[] { soldierA, soldierB, guard, nurse }) Object.Destroy(o);
            Bark.ResetVoices();
            SubtitleService.Clear();
        }

        [UnityTest]
        public IEnumerator TwoVoicesAtMost_OfTwoArchetypes_TheThirdAndTheSameArchetypeDrop()
        {
            int dropped = Bark.Dropped;
            Assert.IsTrue(Bark.Say(soldierA, "Contact front.", 1f), "the first voice");
            Assert.IsFalse(Bark.Say(soldierB, "Moving up.", 1f), "never two soldiers at the same time");
            Assert.IsTrue(Bark.Say(guard, "Hold it right there.", 1f), "a guard and a soldier: two archetypes");
            Assert.AreEqual(2, Bark.Speaking);
            Assert.IsFalse(Bark.Say(nurse, "Oh God, oh God.", 1f), "a third voice drops");
            Assert.AreEqual(dropped + 2, Bark.Dropped);
            Assert.AreEqual(0, Bark.CountOf(soldierB), "the dropped line is not said");
            Assert.AreEqual(0, Bark.CountOf(nurse));
            Assert.IsTrue(Bark.Say(soldierA, "Covering.", 1f), "a voice that speaks already may go on");
            Assert.AreEqual(2, Bark.Speaking);
            Assert.LessOrEqual(SubtitleService.Lines.Count, 2, "two lines on the screen at most");
            yield return WaitUntil(() => Bark.Speaking == 0, 8f);
            Assert.AreEqual(0, Bark.Speaking, "the voices end");
            Assert.IsTrue(Bark.Say(nurse, "Oh God, oh God.", 1f), "a free place takes the next voice");
            Assert.IsTrue(Bark.Say(soldierB, "Moving up.", 1f));
        }

        [UnityTest]
        public IEnumerator AForcedLine_AlwaysPlays_AndCutsTheOldestVoice()
        {
            Assert.IsTrue(Bark.Say(soldierA, "Contact front.", 3f));
            yield return null;
            Assert.IsTrue(Bark.Say(guard, "Hold it right there.", 3f));
            Assert.IsTrue(Bark.SayForced(nurse, "Here. Take it.", 2f), "a line the player asked for");
            Assert.AreEqual(2, Bark.Speaking, "still two voices");
            Assert.IsFalse(SubtitleService.Lines.Any(l => l.Text == "Contact front."), "the oldest voice is cut");
            Assert.IsTrue(SubtitleService.Lines.Any(l => l.Text == "Hold it right there."));
            Assert.IsTrue(SubtitleService.Lines.Any(l => l.Text == "Here. Take it."));
            // A forced line of an archetype that speaks cuts that voice first.
            Assert.IsTrue(Bark.SayForced(soldierB, "Get down!", 2f));
            Assert.AreEqual(2, Bark.Speaking);
            Assert.IsFalse(Bark.CanSpeak(soldierA), "the soldiers' place is taken");
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, float seconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < seconds) yield return null;
        }
    }
}
#endif
