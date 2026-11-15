using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Audio;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>
    /// The demo music (the T44 kit since 2026-11-15): the files are on the 83 BPM grid, the states are one piece, and the
    /// bar math of MusicDirector puts every scheduled start on a bar line. Batch: tools/unity.sh test editmode MusicKitTests.
    /// </summary>
    public class MusicKitTests
    {
        private const int Rate = 48000;
        private static double BarSamples => MusicDirector.BarSeconds * Rate; // 138,795.18 at 83 BPM

        private static AudioClip Load(string name) =>
            Resources.Load<AudioClip>(MusicDirector.Folder + MusicDirector.DemoKey + "/" + name);

        [Test]
        public void TheKit_IsOnTheBarGrid_AndTheStatesAreOnePiece()
        {
            var bars = new (string name, int bars)[]
            {
                ("ambient", 12), ("tension", 12), ("combat", 12),
                (MusicDirector.BreakName, MusicDirector.BreakBars), (MusicDirector.PayoffName, MusicDirector.PayoffBars),
            };
            foreach (var (name, n) in bars)
            {
                AudioClip clip = Load(name);
                Assert.IsNotNull(clip, name + ": tools/music_kit_import.sh");
                Assert.AreEqual(Rate, clip.frequency, name + ": 48 kHz kept");
                Assert.AreEqual(2, clip.channels, name + ": stereo");
                Assert.AreEqual(n * BarSamples, clip.samples, 1.0, $"{name}: {n} bars at {MusicDirector.Bpm} BPM");
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
                Assert.IsTrue(AssetDatabase.GetAssetPath(clip).EndsWith(".wav"), name + ": a WAV source, no MP3");
                Assert.AreEqual(AudioClipLoadType.CompressedInMemory, importer.defaultSampleSettings.loadType, name + ": MusicImport");
                Assert.IsTrue(importer.defaultSampleSettings.preloadAudioData, name + ": loaded with the asset");
            }
            AudioClip combat = Load("combat");
            Assert.IsTrue(MusicDirector.SamePiece(Load("ambient"), combat), "exploration and combat: one piece");
            Assert.IsTrue(MusicDirector.SamePiece(Load("tension"), combat), "search and combat: one piece");
            Assert.IsTrue(MusicDirector.SamePiece(Load("ambient"), Load("tension")), "exploration and search: one piece");
            Assert.IsFalse(MusicDirector.SamePiece(combat, Load(MusicDirector.PayoffName)), "the payoff is not a state");
        }

        [Test]
        public void NextBarLine_IsTheFirstBarLineAfterTheLead_OnTheGridOfTheLoop()
        {
            const int loop = 1665542; // the T44 theme: 12 bars less 0.17 samples
            double loopSec = (double)loop / Rate;
            double anchor = 1000.123456;
            for (int i = 0; i <= 400; i++)
            {
                // Any time over three passes of the loop.
                double earliest = anchor + i * (3.0 * loopSec / 400.0);
                double at = MusicDirector.NextBarLine(anchor, earliest, loop, Rate);
                Assert.GreaterOrEqual(at, earliest - 1e-9, "never before the lead");
                Assert.Less(at - earliest, MusicDirector.BarSeconds + 1e-9, "the next bar line, not a later one");
                double inPass = (at - anchor) % loopSec;
                double barsIn = inPass / MusicDirector.BarSeconds;
                double offSamples = (barsIn - System.Math.Round(barsIn)) * BarSamples;
                bool atPassEnd = (loopSec - inPass) * Rate < 1.0;
                Assert.IsTrue(System.Math.Abs(offSamples) < 1.0 || atPassEnd, $"t {earliest - anchor:0.000}: {offSamples:0.00} samples off a bar line");
            }
            // The end of a pass is a bar line: just before it, the next bar line is the loop point (where the break joins).
            // (The loop can be a fraction of a sample longer or shorter than 12 bars: ask 1 microsecond before its end.)
            Assert.AreEqual(anchor + loopSec, MusicDirector.NextBarLine(anchor, anchor + loopSec - 1e-6, loop, Rate), 1e-9);
            Assert.AreEqual(anchor + loopSec, MusicDirector.NextBarLine(anchor, anchor + 11.5 * MusicDirector.BarSeconds, loop, Rate), 1e-9);
        }

        [Test]
        public void SampleAt_FollowsTheLoop()
        {
            Assert.AreEqual(0, MusicDirector.SampleAt(10.0, 10.0, 1000, Rate));
            Assert.AreEqual(Rate % 1000, MusicDirector.SampleAt(10.0, 11.0, 1000, Rate));
            Assert.AreEqual(1000 - 480, MusicDirector.SampleAt(10.0, 9.99, 1000, Rate), "before the anchor: the pass before");
        }

        /// <summary>The Dining Hall set piece (2026-11-15, "Still B", "Yes, cut it"): five files on the 165 BPM grid.</summary>
        [Test]
        public void TheDiningHallSetPiece_IsOnThe165BpmGrid()
        {
            SetPieceMusic kit = SetPieceMusic.Load("G18", 165.0);
            Assert.IsNotNull(kit, "Resources/Audio/Music/_setpiece/G18: tools/setpiece_import.sh");
            double bar = 240.0 / 165.0 * Rate; // 69,818.18 samples
            var bars = new (AudioClip clip, string name, int bars)[]
            {
                (kit.EntryClip, "entry", 9), (kit.FightLoopClip, "fight_loop", 56), (kit.ArmourEntryClip, "armour_entry", 8),
                (kit.ArmourLoopClip, "armour_loop", 16), (kit.EndClip, "end", 9),
            };
            foreach (var (clip, name, n) in bars)
            {
                Assert.IsNotNull(clip, name);
                Assert.AreEqual(Rate, clip.frequency, name + ": 48 kHz kept");
                Assert.AreEqual(n * bar, clip.samples, 1.0, $"{name}: {n} bars at 165 BPM");
                Assert.AreEqual(n, kit.Bars(clip), name + ": its bars");
                Assert.IsTrue(AssetDatabase.GetAssetPath(clip).EndsWith(".wav"), name + ": a WAV source");
            }
            Assert.IsNull(SetPieceMusic.Load("G2", 165.0), "a room with no files has no set piece");
        }

        [Test]
        public void NextBarLine_At165Bpm_OnALoopAndOnASectionThatPlaysOnce()
        {
            double bar = 240.0 / 165.0;
            const int loop = 3909818; // the fight loop: 56 bars less 0.18 samples
            double anchor = 500.25;
            for (int i = 0; i <= 300; i++)
            {
                double earliest = anchor + i * (2.0 * loop / (double)Rate / 300.0);
                double at = MusicDirector.NextBarLine(anchor, earliest, loop, Rate, bar);
                Assert.GreaterOrEqual(at, earliest - 1e-9);
                Assert.Less(at - earliest, bar + 1e-9, "the next bar line");
                double loopSec = (double)loop / Rate;
                double inPass = (at - anchor) % loopSec;
                double off = (inPass / bar - System.Math.Round(inPass / bar)) * bar * Rate;
                Assert.IsTrue(System.Math.Abs(off) < 1.0 || (loopSec - inPass) * Rate < 1.0, $"{off:0.00} samples off");
            }
            Assert.AreEqual(anchor, MusicDirector.NextBarLineOnce(anchor, anchor - 1.0, bar), 1e-9, "before it starts: its start");
            Assert.AreEqual(anchor + 3 * bar, MusicDirector.NextBarLineOnce(anchor, anchor + 2.5 * bar, bar), 1e-9);
            Assert.AreEqual(anchor + 2 * bar, MusicDirector.NextBarLineOnce(anchor, anchor + 2 * bar, bar), 1e-9, "on a bar line: that one");
        }
    }
}
