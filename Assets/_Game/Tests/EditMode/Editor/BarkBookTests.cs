using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using GlorifiedBody.Editor;
using GlorifiedBody.Enemy;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>
    /// The writer's barks in the game (2026-11-13, rounds 133 to 135, dialogue/takes/barks_v2/GAME_NOTES.md): the file reads,
    /// the copy in Resources is the writer's file, every situation the code asks for has rows for every personality of the
    /// enemy (or the fallback rows), every scripted line of the room builders has its row, and a voice plays the "plays
    /// first" row first, then the others with no repeat, and falls back to the rows with no personality.
    /// </summary>
    public class BarkBookTests
    {
        /// <summary>
        /// Gaps that the writer must close. The test fails when a gap is closed too, so that it leaves this list.
        /// </summary>
        private static readonly string[] KnownScriptedGaps =
        {
            // Round 135 cut it with the old generic rows (old_generic_rows.md, row 124); the README of barks_v2 keeps it.
            "soldier | armour_call | 0",
        };

        private static string WriterText()
        {
            string path = BarkBookSetup.WriterFile;
            if (!File.Exists(path)) Assert.Ignore("no writer file " + path);
            return File.ReadAllText(path);
        }

        [TearDown]
        public void Unload() => BarkBook.Unload();

        [Test]
        public void TheWritersFile_Reads_AndTheResourcesCopyIsTheSame()
        {
            string text = WriterText();
            StringAssert.StartsWith(BarkBook.Header, text);
            List<BarkRow> rows = BarkBook.Parse(text);
            int lines = BarkBook.ReadCsv(text).Count(r => r.Length > 0 && !string.IsNullOrWhiteSpace(r[0])) - 1;
            Debug.Log($"[BarkBookTests] {rows.Count} rows of {lines} in the writer's file, {rows.Count(r => r.first)} play first, {rows.Count(r => r.places.Length > 0)} with a PLACE");
            Assert.AreEqual(lines, rows.Count, "every row has an enemy and a line");
            Assert.IsTrue(rows.All(r => r.situation.Length > 0), "every row has a situation");
            Assert.IsTrue(File.Exists(BarkBookSetup.ResourceFile), "the copy in Resources: run BarkBookSetup.Build");
            Assert.AreEqual(text, File.ReadAllText(BarkBookSetup.ResourceFile), "the copy is the writer's file: run BarkBookSetup.Build");
        }

        [Test]
        public void EverySituationTheCodeAsks_HasRows_ForEveryPersonality()
        {
            BarkBook.LoadText(WriterText(), "test");
            var missing = new List<string>();
            var report = new List<string>();
            foreach ((string enemy, string situation) in BarkBook.Asked)
            {
                var counts = new List<string>();
                foreach (string personality in BarkBook.PersonalitiesAsked(enemy, situation))
                {
                    // A row with a PLACE plays only in its room: count the rows that play anywhere.
                    List<BarkRow> rows = BarkBook.RowsFor(enemy, personality, situation);
                    int anywhere = rows.Count(r => r.places.Length == 0);
                    bool fallback = rows.Count > 0 && rows[0].personality == "" && personality != "";
                    counts.Add($"{(personality == "" ? "-" : personality)} {anywhere}{(fallback ? " (fallback)" : "")}");
                    if (anywhere == 0) missing.Add($"{enemy} | {(personality == "" ? "-" : personality)} | {situation}");
                }
                report.Add($"{enemy} | {situation} | {string.Join(", ", counts)}");
            }
            Debug.Log("[BarkBookTests] the asks:\n" + string.Join("\n", report));
            Assert.IsEmpty(missing, "situations with no row for a personality and no fallback:\n" + string.Join("\n", missing));
        }

        [Test]
        public void EveryScriptedLine_OfTheCode_HasItsRow()
        {
            BarkBook.LoadText(WriterText(), "test");
            var rx = new Regex(@"Scripted\(""([^""]+)"",\s*(?:""([^""]+)""|BarkSituation\.(\w+))(?:,\s*(\d+))?");
            var asks = new SortedSet<string>();
            foreach (string file in Directory.GetFiles("Assets/_Game/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                if (file.EndsWith("BarkBook.cs") || file.EndsWith("BarkBookSetup.cs")) continue;
                foreach (Match m in rx.Matches(File.ReadAllText(file)))
                {
                    string situation = m.Groups[2].Success ? m.Groups[2].Value
                        : (string)typeof(BarkSituation).GetField(m.Groups[3].Value).GetValue(null);
                    asks.Add($"{m.Groups[1].Value} | {situation} | {(m.Groups[4].Success ? m.Groups[4].Value : "0")}");
                }
            }
            var missing = new List<string>();
            foreach (string ask in asks)
            {
                string[] p = ask.Split(new[] { " | " }, System.StringSplitOptions.None);
                if (BarkBook.Scripted(p[0], p[1], int.Parse(p[2])) == "") missing.Add(ask);
            }
            Debug.Log($"[BarkBookTests] {asks.Count} scripted lines in the code, {missing.Count} with no row: {string.Join("; ", missing)}");
            Assert.Greater(asks.Count, 15, "the scan finds the scripted lines");
            CollectionAssert.AreEquivalent(KnownScriptedGaps, missing, "the scripted lines with no row (a closed gap leaves KnownScriptedGaps)");
        }

        private const string Sample =
            "enemy,personality,situation,line,direction\n" +
            "guard,broken,cower,Gun's down.,plays first; take x\n" +
            "guard,broken,cower,One.,take x\n" +
            "guard,broken,cower,Two.,take x\n" +
            "guard,broken,cower,Three.,take x\n" +
            "guard,broken,cower,\"Four, in quotes.\",PLACE: counter; take x\n" +
            "guard,,hit,Ow.,the fallback\n" +
            "guard,sarcastic,hit,Great.,his own\n" +
            "nurse,afraid,backs away slowly,I'm going.,take x\n" +
            "scientist,bargaining,talks to Ghost,Ghost. Stand down.,round 123\n" +
            "ward sister,,\"W11, gives the card when asked\",Here.,scripted\n";

        private static BarkVoice Voice(string enemy, string personality, bool armed)
        {
            var voice = new GameObject("BarkTestFigure").AddComponent<BarkVoice>();
            voice.Init(enemy, personality, armed);
            return voice;
        }

        [Test]
        public void AVoice_PlaysFirstFirst_ThenTheOthers_WithNoRepeat()
        {
            BarkBook.LoadText(Sample, "sample");
            BarkVoice voice = Voice("guard", "broken", armed: true);
            try
            {
                Assert.AreEqual("Gun's down.", voice.Next(BarkSituation.Cower), "the 'plays first' row first");
                var next = new[] { voice.Next(BarkSituation.Cower), voice.Next(BarkSituation.Cower), voice.Next(BarkSituation.Cower) };
                CollectionAssert.AreEquivalent(new[] { "One.", "Two.", "Three." }, next, "then the others, each once (the PLACE row needs its room)");
                Assert.AreNotEqual("Gun's down.", voice.Next(BarkSituation.Cower), "the first row plays once");
                Assert.IsNull(voice.Next(BarkSituation.Shock), "no row: no line");
            }
            finally { Object.DestroyImmediate(voice.gameObject); }
        }

        [Test]
        public void AVoice_FallsBack_ToTheRowsWithNoPersonality_AndTheUnarmedSkipTheDrop()
        {
            BarkBook.LoadText(Sample, "sample");
            BarkVoice sarcastic = Voice("guard", "sarcastic", armed: true);
            BarkVoice professional = Voice("guard", "professional", armed: true);
            BarkVoice unarmed = Voice("guard", "broken", armed: false);
            try
            {
                Assert.AreEqual("Great.", sarcastic.Next(BarkSituation.Hit), "his own row");
                Assert.AreEqual("Ow.", professional.Next(BarkSituation.Hit), "no row of his: the fallback");
                Assert.AreNotEqual("Gun's down.", unarmed.Next(BarkSituation.Cower), "no weapon: no drop line");
                Assert.AreEqual("Here.", BarkBook.Scripted("ward sister", "W11, gives the card when asked"), "a quoted situation with a comma");
            }
            finally
            {
                Object.DestroyImmediate(sarcastic.gameObject);
                Object.DestroyImmediate(professional.gameObject);
                Object.DestroyImmediate(unarmed.gameObject);
            }
        }

        [Test]
        public void TheNurse_TakesHerNearestSituation_AndTheLabWorker_TheScientistsRows()
        {
            BarkBook.LoadText(Sample, "sample");
            BarkVoice nurse = Voice("nurse", "afraid", armed: false);
            BarkVoice labworker = Voice("labworker", "bargaining", armed: false);
            try
            {
                Assert.AreEqual("I'm going.", nurse.Next(BarkSituation.Begs), "a nurse begs with 'backs away slowly'");
                Assert.AreEqual("Ghost. Stand down.", labworker.Next(BarkSituation.TalksToGhost), "the game has no scientist figure");
            }
            finally
            {
                Object.DestroyImmediate(nurse.gameObject);
                Object.DestroyImmediate(labworker.gameObject);
            }
        }

        [Test]
        public void TheStaff_TurnAfraid_AndAGuard_TurnsBroken_TheOthersStay()
        {
            BarkBook.LoadText(Sample, "sample");
            BarkVoice nurse = Voice("nurse", BarkVoice.Confused, armed: false);
            BarkVoice guard = Voice("guard", "overconfident", armed: true);
            BarkVoice soldier = Voice("soldier", "callous", armed: true);
            try
            {
                nurse.Frighten();
                guard.Frighten();
                guard.Panic();
                soldier.Panic();
                Assert.AreEqual(BarkVoice.Afraid, nurse.Personality, "the staff turn afraid (round 134)");
                Assert.AreEqual(BarkVoice.Broken, guard.Personality, "a guard who panics is broken (round 134)");
                Assert.AreEqual("callous", soldier.Personality, "a soldier keeps his");
                (float confused, float bargaining) = BarkVoice.StaffShares(Vector3.zero);
                Assert.AreEqual(0.6f, confused, 1e-4f, "no rooms: the ward's share of confused staff (PROPOSED)");
                Assert.AreEqual(0.2f, bargaining, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(nurse.gameObject);
                Object.DestroyImmediate(guard.gameObject);
                Object.DestroyImmediate(soldier.gameObject);
            }
        }
    }
}
