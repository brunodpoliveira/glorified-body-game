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

        [Test]
        public void TheBuiltContent_HasAnEntry_ForEveryKillerOfTheTestRooms()
        {
            var content = ScriptableObject.CreateInstance<DeathScreenContent>();
            int rows = DeathScreenSetup.Fill(content);
            var ids = new List<string> { "dummy_shooter", "soldier_rifle", "soldier_gas", "power_armour_launchers" };
            // The class rows (guard, soldier) come from the writer's file, in the RFab repo next to this one.
            if (System.IO.File.Exists(System.IO.Path.GetFullPath(DeathScreenSetup.WriterCsv)))
            {
                Assert.GreaterOrEqual(rows, 7, "the writer's rows");
                StringAssert.StartsWith("COMPUTER:", content.Find("guard_pistol").voiceText, "the guard's voice line from the writer");
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
