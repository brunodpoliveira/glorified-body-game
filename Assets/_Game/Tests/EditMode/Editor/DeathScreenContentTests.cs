using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using GlorifiedBody.Editor;
using GlorifiedBody.Enemy;
using GlorifiedBody.UI;

namespace GlorifiedBody.Tests.EditMode
{
    /// <summary>
    /// The game-over content (Bruno, 2026-10-25: "killed by assault_rifle_guard", tested in code before the assets):
    /// a killer id names the class and the weapon, the lookup falls back from the whole id to the class to the
    /// fallback, the writer's CSV reads with its quotes and commas, and the built asset has an entry for every killer
    /// id of the test rooms.
    /// </summary>
    public class DeathScreenContentTests
    {
        [Test]
        public void KillerId_NamesTheClassAndTheWeapon()
        {
            Assert.AreEqual("soldier_rifle", DummyShooter.KillerIdFor("soldier", DummyShooter.Weapon.Rifle));
            Assert.AreEqual("guard_pistol", DummyShooter.KillerIdFor("guard", DummyShooter.Weapon.Pistol));
            Assert.AreEqual("soldier_gas", DummyShooter.KillerIdFor("soldier", DummyShooter.Weapon.GasLauncher));
            Assert.AreEqual("power_armour_launchers", DummyShooter.KillerIdFor("power_armour", DummyShooter.Weapon.ArmourLaunchers));
        }

        [Test]
        public void Find_TheWholeId_ThenTheClass_ThenTheFallback()
        {
            var content = ScriptableObject.CreateInstance<DeathScreenContent>();
            var soldier = new DeathScreenContent.Entry { killerId = "soldier", hint = "class" };
            var rifle = new DeathScreenContent.Entry { killerId = "soldier_rifle", hint = "rifle" };
            var armour = new DeathScreenContent.Entry { killerId = "power_armour", hint = "armour" };
            content.entries = new List<DeathScreenContent.Entry> { soldier, rifle, armour };
            Assert.AreSame(rifle, content.Find("soldier_rifle"), "the whole id first");
            Assert.AreSame(soldier, content.Find("soldier_gas"), "then the class");
            Assert.AreSame(armour, content.Find("power_armour_launchers"), "a class with an underscore");
            Assert.AreSame(content.fallback, content.Find("scientist_pistol"), "no class entry: the fallback");
            Assert.AreSame(content.fallback, content.Find(""), "no killer: the fallback");
            Object.DestroyImmediate(content);
        }

        [Test]
        public void ReadCsv_KeepsQuotedCommasAndQuotes()
        {
            string text = "enemy,picture,voice_line,hint\r\nguard,\"the guard, the goggles\",\"COMPUTER: \"\"Subject contained.\"\"\",\"Step out, then fire.\"\nfire,a,b,c";
            List<string[]> rows = DeathScreenSetup.ReadCsv(text);
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual(4, rows[1].Length);
            Assert.AreEqual("the guard, the goggles", rows[1][1]);
            Assert.AreEqual("COMPUTER: \"Subject contained.\"", rows[1][2]);
            Assert.AreEqual("Step out, then fire.", rows[1][3]);
            Assert.AreEqual("c", rows[2][3]);
        }

        /// <summary>
        /// The writer's file as the game reads it (2026-11-13, the brain row check): every row has the header's column
        /// count, and the brain row gives the game its whole hint. The game uses columns 0 to 3 (enemy, picture,
        /// voice_line, hint); speaker and outcome are for the writer. An unquoted comma in a cell splits it, and the
        /// game keeps only the part before the first comma. The log prints what the game stores for the brain.
        /// </summary>
        [Test]
        public void TheWritersFile_EveryRowHasTheHeadersColumns_AndTheBrainHintIsWhole()
        {
            string path = System.IO.Path.GetFullPath(DeathScreenSetup.WriterCsv);
            if (!System.IO.File.Exists(path)) Assert.Ignore("no writer file at " + path);
            List<string[]> rows = DeathScreenSetup.ReadCsv(System.IO.File.ReadAllText(path));
            int columns = rows[0].Length;
            var bad = new List<string>();
            for (int i = 1; i < rows.Count; i++)
                if (rows[i].Length != columns) bad.Add($"{rows[i][0]} (line {i + 1}: {rows[i].Length} columns)");

            var content = ScriptableObject.CreateInstance<DeathScreenContent>();
            DeathScreenSetup.Fill(content);
            DeathScreenContent.Entry brain = content.Exact("brain");
            string[] raw = rows.Find(r => r[0].Trim() == "brain");
            Debug.Log($"[DeathScreenCsv] header columns {columns}; brain row columns {(raw != null ? raw.Length : 0)}");
            if (raw != null)
                for (int c = 0; c < raw.Length; c++)
                    Debug.Log($"[DeathScreenCsv] brain column {c} ({(c < columns ? rows[0][c] : "EXTRA")}): '{raw[c]}'");
            if (brain != null)
            {
                Debug.Log($"[DeathScreenCsv] game entry brain.pictureNote = '{brain.pictureNote}'");
                Debug.Log($"[DeathScreenCsv] game entry brain.voiceText   = '{brain.voiceText}'");
                Debug.Log($"[DeathScreenCsv] game entry brain.hint        = '{brain.hint}'");
            }
            Object.DestroyImmediate(content);
            Assert.IsEmpty(bad, $"rows with a column count other than the header's {columns}: " + string.Join(", ", bad)
                + ". Put a cell with a comma in double quotes.");
        }

        [Test]
        public void TheArmourGuns_HaveTheirOwnDeathScreen_FromTheWritersRow()
        {
            // Round 122 (PROPOSED by the writer: one new death screen for the guns of the armour, and three death lines).
            Assert.AreEqual("power_armour_guns", DummyShooter.KillerIdFor("power_armour", DummyShooter.Weapon.ArmourGuns));
            Assert.AreEqual("soldier_shotgun", DummyShooter.KillerIdFor("soldier", DummyShooter.Weapon.Shotgun));
            if (!System.IO.File.Exists(System.IO.Path.GetFullPath(DeathScreenSetup.WriterCsv))) Assert.Ignore("no writer file");
            var content = ScriptableObject.CreateInstance<DeathScreenContent>();
            DeathScreenSetup.Fill(content);
            DeathScreenContent.Entry guns = content.Exact("power_armour_guns");
            Assert.IsNotNull(guns, "the writer's row power_armour_guns");
            StringAssert.StartsWith("POWER ARMOUR:", guns.voiceText);
            StringAssert.Contains("sweep", guns.hint);
            Assert.AreEqual("soldier", content.Find("soldier_shotgun").killerId, "the shotgunner takes the soldier's screen until the writer gives it its own");
            Debug.Log($"[DeathScreenCsv] power_armour_guns: '{guns.voiceText}' / '{guns.hint}'");
            Object.DestroyImmediate(content);
        }

        [Test]
        public void TheBuiltContent_HasAnEntry_ForEveryKillerOfTheTestRooms()
        {
            var content = ScriptableObject.CreateInstance<DeathScreenContent>();
            int rows = DeathScreenSetup.Fill(content);
            var ids = new List<string> { "dummy_shooter", "soldier_rifle", "soldier_gas", "power_armour_launchers" };
            // The class rows (guard, soldier) come from the writer's file, in the RFab repo next to this one.
            if (System.IO.File.Exists(System.IO.Path.GetFullPath(DeathScreenSetup.WriterCsv)))
            {
                Assert.GreaterOrEqual(rows, 6, "the writer's rows (round 28: six archetypes)");
                Assert.IsNotEmpty(content.Find("guard_pistol").voiceText, "the guard's voice line from the writer");
                ids.Add("guard_pistol");
                ids.Add("soldier_pistol");
            }
            foreach (string id in ids)
            {
                DeathScreenContent.Entry e = content.Find(id);
                Assert.AreNotSame(content.fallback, e, id + " has its own entry or its class entry");
                Assert.IsFalse(string.IsNullOrEmpty(e.hint), id + " has a hint");
                Assert.IsFalse(string.IsNullOrEmpty(e.pictureNote), id + " has a picture note");
            }
            Assert.AreEqual("soldier_rifle", content.Find("soldier_rifle").killerId, "the rifle has its own hint");
            Object.DestroyImmediate(content);
        }
    }
}
