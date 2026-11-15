using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GlorifiedBody.Audio;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>
    /// The demo music (2026-11-14, the T43 v4 kit): the files are on the 84 BPM grid, the states are one piece, and the
    /// bar math of MusicDirector puts every scheduled start on a bar line. Batch: tools/unity.sh test editmode MusicKitTests.
    /// </summary>
    public class MusicKitTests
    {
        private const int Rate = 48000;
        private static double BarSamples => MusicDirector.BarSeconds * Rate; // 137,142.857

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
                Assert.AreEqual(n * BarSamples, clip.samples, 1.0, $"{name}: {n} bars at 84 BPM");
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
            const int loop = 1645715; // the T43 theme: 12 bars and 0.71 samples
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
            // The end of a pass is a bar line: just before it, the next bar line is the loop point (the v4 join).
            double nearEnd = anchor + 11.5 * MusicDirector.BarSeconds;
            Assert.AreEqual(anchor + loopSec, MusicDirector.NextBarLine(anchor, nearEnd + 0.5 * MusicDirector.BarSeconds + 1e-6, loop, Rate), 1e-9);
        }

        [Test]
        public void SampleAt_FollowsTheLoop()
        {
            Assert.AreEqual(0, MusicDirector.SampleAt(10.0, 10.0, 1000, Rate));
            Assert.AreEqual(Rate % 1000, MusicDirector.SampleAt(10.0, 11.0, 1000, Rate));
            Assert.AreEqual(1000 - 480, MusicDirector.SampleAt(10.0, 9.99, 1000, Rate), "before the anchor: the pass before");
        }
    }
}
