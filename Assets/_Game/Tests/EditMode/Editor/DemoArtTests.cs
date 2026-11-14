using System.Linq;
using NUnit.Framework;
using GlorifiedBody.Editor.Demo;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>
    /// Lucy's art texts as audio logs (round 138; game/lines/T13_art_history.md): the parser finds the five logs A1 to A5 and
    /// the beat, and each log splits into lines a subtitle can show.
    /// </summary>
    public class DemoArtTests
    {
        [Test]
        public void TheWritersFile_HasTheArtLogs_AndTheBeat()
        {
            var texts = DemoArt.Texts(out string beat);
            if (texts.Count == 0) Assert.Ignore("no writer file " + DemoArt.File);
            CollectionAssert.IsSubsetOf(new[] { "A1", "A2", "A3", "A4", "A5" }, texts.Keys.ToArray(), "the five logs of the demo (A6 to A9: the statues, round 148)");
            Assert.IsNotEmpty(beat, "the beat after the first log (its words change by round: round 146)");
            StringAssert.DoesNotContain("I'm sure of the year", beat, "the gear's own line is not the beat");
            foreach (var (id, (heading, text)) in texts.Select(k => (k.Key, k.Value)))
            {
                var lines = DemoArt.Sentences(text);
                UnityEngine.Debug.Log($"[DemoArtTests] {id} '{heading}': {text.Split(' ').Length} words, {lines.Count} lines, the longest {lines.Max(l => l.Length)} characters");
                Assert.Greater(text.Split(' ').Length, 60, id + ": the whole text, not one paragraph line");
                Assert.IsTrue(lines.All(l => l.Length <= 200), id + ": every subtitle line fits");
                Assert.AreEqual(text.Replace(" ", ""), string.Join("", lines).Replace(" ", ""), id + ": no word lost in the split");
            }
            StringAssert.Contains("I'm sure of the year", texts["A5"].text, "the gear log keeps its own beat (T13 note 6)");
        }

        [Test]
        public void TheGearsOwnBeat_IsALineOfItsOwn_SkippedWhenTheGearIsHerFirstArtLog()
        {
            // Round 145: the gear log keeps its beat, shown only when the gear is not her first art log.
            if (DemoArt.Texts(out _).Count == 0) Assert.Ignore("no writer file");
            var gear = DemoArt.Log("A5");
            var own = gear.tapeLines.Where(l => !string.IsNullOrEmpty(l.skipIfFlag)).ToList();
            Assert.AreEqual(1, own.Count, "one line with a flag");
            StringAssert.StartsWith("I'm sure of the year", own[0].text);
            Assert.AreEqual(GlorifiedBody.Interaction.ArtLog.GearFirstFlag, own[0].skipIfFlag);
            Assert.IsTrue(gear.tapeLines.All(l => !l.text.Contains("1901, I'm sure")), "the line before it ends at 1901");
            Assert.IsTrue(DemoArt.Log("A1").tapeLines.All(l => string.IsNullOrEmpty(l.skipIfFlag)), "the other logs play whole");
        }
    }
}
