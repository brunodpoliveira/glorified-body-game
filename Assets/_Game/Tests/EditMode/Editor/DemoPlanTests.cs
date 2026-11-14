using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using GlorifiedBody.Editor.Demo;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>
    /// The in-world text against DEMO_PLAN.md §7 to §9 (round 138: the rewrite v2; round 142: the EMERGENCY STOP, THEATER):
    /// every sign, log and scan caption that a room builder takes from the plan has its row, the words come out clean, and
    /// no old copy of a changed string stays in the code.
    /// </summary>
    public class DemoPlanTests
    {
        private static readonly Regex Asks = new Regex(@"DemoPlan\.(Sign|Log|Caption|ScanCard)\(""([^""]+)""");

        private static void NeedsPlan()
        {
            if (!File.Exists(DemoPlan.PlanFile)) Assert.Ignore("no plan " + DemoPlan.PlanFile);
            DemoPlan.Unload();
        }

        private static IEnumerable<string> Sources() =>
            Directory.GetFiles("Assets/_Game/Scripts", "*.cs", SearchOption.AllDirectories).Where(f => !f.EndsWith("DemoPlan.cs"));

        [Test]
        public void EveryKeyTheBuildersAsk_HasItsRowInThePlan()
        {
            NeedsPlan();
            var asks = new SortedSet<string>();
            foreach (string file in Sources())
                foreach (Match m in Asks.Matches(File.ReadAllText(file)))
                    asks.Add(m.Groups[1].Value + " | " + m.Groups[2].Value);
            var missing = asks.Where(a =>
            {
                string[] p = a.Split(new[] { " | " }, System.StringSplitOptions.None);
                return p[0] == "Sign" ? !DemoPlan.HasSign(p[1]) : p[0] == "Log" ? !DemoPlan.HasLog(p[1]) : !DemoPlan.HasScan(p[1]);
            }).ToList();
            Debug.Log($"[DemoPlanTests] {asks.Count} keys: {asks.Count(a => a.StartsWith("Sign"))} signs, {asks.Count(a => a.StartsWith("Log"))} logs, {asks.Count(a => a.StartsWith("ScanCard"))} scan cards");
            Assert.GreaterOrEqual(asks.Count(a => a.StartsWith("Sign")), 25, "the signs of the demo come from the plan");
            Assert.GreaterOrEqual(asks.Count(a => a.StartsWith("ScanCard")), 10, "the scan cards come from the plan");
            Assert.Greater(asks.Count(a => a.StartsWith("Log")), 20, "the logs of the demo come from the plan");
            Assert.IsEmpty(missing, "keys with no row in the plan:\n" + string.Join("\n", missing));
        }

        [Test]
        public void TheWords_ComeOutClean_AndRound142IsIn()
        {
            NeedsPlan();
            Assert.AreEqual("THEATER CONTROL. AUTHORIZED STAFF ONLY. DOOR RELEASES ON FIRE ALARM.", DemoPlan.Sign("H4, the booth door", ""));
            Assert.AreEqual("HALON EMERGENCY STOP. PRESS TO STOP DISCHARGE.", DemoPlan.Sign("G17, the panel inside", ""));
            StringAssert.Contains("press EMERGENCY STOP", DemoPlan.Sign("G17 door, outside", ""));
            Assert.AreEqual("IT IS SOWN IN WEAKNESS. IT IS RAISED IN POWER.", DemoPlan.Sign("G4, the chapel, under the gear", ""), "the source note goes");
            StringAssert.DoesNotContain("(by hand)", DemoPlan.Sign("G10, taped to the pizza boxes", ""), "a direction, not words");
            StringAssert.Contains("\n", DemoPlan.Log("L17", ""), "<br> is a new line");
            StringAssert.Contains("EMERGENCY STOP", DemoPlan.Log("L17", ""), "round 142: L17");
            Assert.AreEqual("\"She's a patient. Don't let her touch you.\"", DemoPlan.Caption("C5", ""));
            Assert.AreEqual("", DemoPlan.Caption("C1", "x"), "None: no caption");
        }

        [Test]
        public void NoOldCopy_OfAChangedString_StaysInTheCode()
        {
            var old = new[] { "PRESS ABORT", "HALON ABORT.", "THEATRE CONTROL", "AUTHORISED", "press ABORT" };
            var found = new List<string>();
            foreach (string file in Sources())
            {
                string text = File.ReadAllText(file);
                foreach (string s in old)
                    if (text.Contains("\"" + s) || text.Contains(" " + s)) found.Add(Path.GetFileName(file) + ": " + s);
            }
            Assert.IsEmpty(found, "old strings (rounds 138 and 142):\n" + string.Join("\n", found));
        }
    }
}
