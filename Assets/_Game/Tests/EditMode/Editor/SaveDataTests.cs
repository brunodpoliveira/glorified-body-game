#if UNITY_INCLUDE_TESTS
using System.IO;
using NUnit.Framework;
using UnityEngine;
using GlorifiedBody.Destruction;
using GlorifiedBody.Saving;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>
    /// The save file without a room: the JSON round trip keeps every field, a broken room survives the export and the
    /// import of RoomDamageLedger, a file of another version does not load, and a write leaves no temp file.
    /// </summary>
    public class SaveDataTests
    {
        private string folder;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Application.temporaryCachePath, "gb_savedata_tests");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            SaveSystem.Folder = folder;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.Folder = null;
            RoomDamageLedger.Import(null);
        }

        [Test]
        public void TheFile_KeepsEveryField()
        {
            var data = Sample();
            SaveSystem.Write(1, data);
            Assert.IsFalse(File.Exists(SaveSystem.PathOf(1) + ".tmp"), "no temp file left");
            SaveData back = SaveSystem.Read(1);
            Assert.IsNotNull(back);
            Assert.AreEqual("TrainingRoom", back.scene);
            Assert.AreEqual("SavePoint_Training", back.spot);
            Assert.AreEqual(37f, back.player.health);
            Assert.AreEqual(new[] { "test_key" }, back.player.critical.ToArray());
            Assert.AreEqual(new[] { 1, 2, 0 }, back.player.drugs.ToArray());
            Assert.AreEqual(new[] { "Dummy_B" }, back.room.dead.ToArray());
            Assert.AreEqual(new[] { "met_the_nurse" }, back.flags.ToArray());
            LedgerBreakable b = back.ledger.rooms[0].breakables[0];
            Assert.IsTrue(b.broken);
            Assert.IsTrue(b.pieces[0].has);
            Assert.IsFalse(b.pieces[1].has, "a gone piece stays gone");
            Assert.AreEqual(new Vector3(1f, 2f, 3f), b.pieces[0].position);
        }

        [Test]
        public void ABrokenRoom_SurvivesTheLedgerExportAndImport()
        {
            RoomDamageLedger.Import(Sample().ledger);
            LedgerState again = RoomDamageLedger.Export();
            Assert.AreEqual(1, again.rooms.Count);
            Assert.AreEqual("CoverTest", again.rooms[0].room);
            Assert.AreEqual(1, RoomDamageLedger.BrokenIn("CoverTest"), "the barricade is still broken");
            LedgerBreakable b = again.rooms[0].breakables[0];
            Assert.AreEqual("Barricade_1", b.id);
            Assert.AreEqual(2, b.pieces.Count);
            Assert.AreEqual(Quaternion.Euler(0f, 30f, 0f).eulerAngles.y, b.pieces[0].rotation.eulerAngles.y, 0.01f);
            Assert.AreEqual(new Vector3(4f, 0f, 1f), again.rooms[0].greebles[0].position);
        }

        [Test]
        public void AFileOfAnotherVersion_DoesNotLoad()
        {
            SaveData data = Sample();
            data.version = SaveData.CurrentVersion + 1;
            SaveSystem.Write(2, data);
            Assert.IsNull(SaveSystem.Read(2), "another version");
            File.WriteAllText(SaveSystem.PathOf(3), "{ not json");
            Assert.IsNull(SaveSystem.Read(3), "a broken file");
            Assert.IsNull(SaveSystem.Read(1), "an empty slot");
        }

        private static SaveData Sample()
        {
            var data = new SaveData { scene = "TrainingRoom", spot = "SavePoint_Training", savedAt = "2026-10-25 21:00", playSeconds = 75f };
            data.player.health = 37f;
            data.player.maxHealth = 100f;
            data.player.critical.Add("test_key");
            data.player.drugs.AddRange(new[] { 1, 2, 0 });
            data.room.scene = "TrainingRoom";
            data.room.dead.Add("Dummy_B");
            data.flags.Add("met_the_nurse");
            var room = new LedgerRoom { room = "CoverTest" };
            var barricade = new LedgerBreakable { id = "Barricade_1", broken = true, health = 0f };
            barricade.pieces.Add(new LedgerPose { has = true, position = new Vector3(1f, 2f, 3f), rotation = Quaternion.Euler(0f, 30f, 0f) });
            barricade.pieces.Add(new LedgerPose { has = false });
            room.breakables.Add(barricade);
            room.greebles.Add(new LedgerPose { id = "Mug_1", has = true, position = new Vector3(4f, 0f, 1f), rotation = Quaternion.identity });
            data.ledger.rooms.Add(room);
            return data;
        }
    }
}
#endif
