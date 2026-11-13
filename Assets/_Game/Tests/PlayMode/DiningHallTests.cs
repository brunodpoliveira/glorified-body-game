#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Rooms;
using GlorifiedBody.Saving;
using GlorifiedBody.Story;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The Dining Hall fight after rounds 121 and 122 (DEMO_PLAN §3.4 item 4, §3.1 item 4, beat 23b): six soldiers in two
    /// sets (a shield, a grenadier, a shotgunner), the power armour out of the lodge door at three left with the squad's
    /// call and its operator's line, the shotgun's cone, the armour's sweep and long burst. Numbers PROPOSED.
    /// Batch: tools/unity.sh test playmode DiningHallTests.
    /// </summary>
    public class DiningHallTests
    {
        private const string World = "Demo";
        private const string ArmourFlag = "demo_g18_armour";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_dining_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (CutscenePlayer.IsPlaying) CutscenePlayer.Skip();
            yield return WaitUntil(() => !CutscenePlayer.IsPlaying, 5f);
            SaveSystem.Folder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AtThreeLeft_TheSquadCalls_AndThePowerArmourStepsOutOfTheLodgeDoor_WithItsGuns()
        {
            yield return InTheHall();
            SoldierBrain[] six = Six();
            Assert.AreEqual(6, six.Length, "six soldiers");
            HeavyArrival arrival = UnityEngine.Object.FindFirstObjectByType<HeavyArrival>();
            Assert.IsNull(Armour(), "no armour before");
            six[0].Alert("test", callOthers: true);
            for (int i = 0; i < 2; i++) six[i].GetComponent<Health>().TakeDamage(9999f);
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(arrival.Arrived, "four stand: no armour yet");
            six[2].GetComponent<Health>().TakeDamage(9999f);
            yield return WaitUntil(() => Armour() != null, 5f);
            SoldierBrain armour = Armour();
            Assert.IsNotNull(armour, "the power armour comes in");
            Assert.AreEqual(3, arrival.StandingAtCall, "at three left (round 122)");
            Assert.IsTrue(StoryFlags.Has(ArmourFlag));
            DummyShooter guns = armour.GetComponent<DummyShooter>();
            Assert.AreEqual(DummyShooter.Weapon.ArmourGuns, guns.WeaponKind, "the autofire archetype, no grenades and no rockets");
            Assert.AreEqual("power_armour_guns", guns.EnemyId, "its killer id: the death screen power_armour_guns");
            Vector3 door = new Vector3(44f, 0f, 9.9f);
            float fromDoor = Vector3.Distance(Flat(armour.transform.position), Flat(Room.Of("G18").transform.position + door));
            Assert.Less(fromDoor, 2.5f, "it steps out of the lodge door");
            bool called = six.Skip(3).Any(s => Bark.CountOf(s.gameObject) > 0 && s.GetComponent<Bark>().Shown.StartsWith("We're at half"));
            yield return WaitUntil(() => Bark.CountOf(armour.gameObject) > 0, 3f);
            Debug.Log($"[DiningHallTests] the armour: called at {arrival.StandingAtCall} standing, {fromDoor:0.0} m from the lodge door, the call heard {called}, its line '{armour.GetComponent<Bark>().Shown}'");
            Assert.IsTrue(called, "a soldier calls for it (barks.csv, armour_call)");
            // A row of his own personality (barks.csv, BarkBook, 2026-11-13).
            BarkVoice voice = BarkVoice.Of(armour.gameObject);
            CollectionAssert.Contains(BarkBook.RowsFor(voice.Enemy, voice.Personality, BarkSituation.ArmourEnter).Select(r => r.line).ToList(),
                armour.GetComponent<Bark>().Shown, "its operator speaks (armour_enter, " + voice.Personality + ")");
            yield return WaitUntil(() => armour.Alerted, 3f);
            Assert.IsTrue(armour.Alerted, "it hunts her");
        }

        [UnityTest]
        public IEnumerator TheShotgun_ShowsAShortWideCone_ThenFiresItsPellets()
        {
            yield return InTheHall();
            SoldierBrain[] six = Six();
            SoldierBrain gunner = six.First(s => s.name == "Hall_Shotgun_A");
            foreach (SoldierBrain s in six.Where(s => s != gunner)) s.GetComponent<Health>().TakeDamage(9999f);
            yield return WaitUntil(() => Armour() != null, 5f);
            Armour()?.GetComponent<Health>().TakeDamage(9999f); // the armour came at three: out of this test
            DummyShooter gun = gunner.GetComponent<DummyShooter>();
            Assert.AreEqual(DummyShooter.Weapon.Shotgun, gun.WeaponKind);
            Assert.LessOrEqual(gun.Range, 6.01f, "close range only");
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponent<TankController>().Teleport(gunner.transform.position + new Vector3(-3.5f, 0f, 0f), 90f);
            gunner.Alert("test", callOthers: false);
            bool cone = false, line = false;
            yield return WaitUntil(() =>
            {
                if (gun.Current == DummyShooter.State.Telegraph) { cone |= gun.ConeShown; line |= gun.GetComponentsInChildren<LineRenderer>().Any(l => l.name == "Laser" && l.enabled); }
                return gun.RoundsFired >= DummyShooter.Pellets;
            }, 25f);
            Debug.Log($"[DiningHallTests] the shotgun: cone shown {cone}, laser line shown {line}, pellets {gun.RoundsFired}, attacks {gun.ShotsFired}");
            Assert.IsTrue(cone, "the telegraph is a cone");
            Assert.IsFalse(line, "no laser line for the shotgun");
            Assert.GreaterOrEqual(gun.RoundsFired, DummyShooter.Pellets, "seven pellets in one shot");
            Assert.AreEqual(1, gun.ShotsFired, "one shot");
        }

        [UnityTest]
        public IEnumerator TheArmourGuns_TheLaserSweepsAcrossHer_ThenALongBurstFollowsTheSweep()
        {
            yield return InTheHall();
            SoldierBrain[] six = Six();
            foreach (SoldierBrain s in six) s.GetComponent<Health>().TakeDamage(9999f);
            yield return WaitUntil(() => Armour() != null, 5f);
            SoldierBrain armour = Armour();
            Assert.IsNotNull(armour);
            DummyShooter gun = armour.GetComponent<DummyShooter>();
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponent<TankController>().Teleport(Room.Of("G18").transform.position + new Vector3(38.5f, 0f, 8.5f), 90f);
            float span = 0f;
            yield return WaitUntil(() =>
            {
                if (gun.Current == DummyShooter.State.Telegraph || gun.Current == DummyShooter.State.Aiming) span = Mathf.Max(span, gun.LastSweepSpan);
                return gun.RoundsFired >= DummyShooter.SweepRounds;
            }, 30f);
            Debug.Log($"[DiningHallTests] the armour guns: sweep span {span:0.00} m, rounds {gun.RoundsFired}, attacks {gun.ShotsFired}");
            Assert.Greater(span, 1.5f, "the laser sweeps across her before the burst");
            Assert.GreaterOrEqual(gun.RoundsFired, DummyShooter.SweepRounds, "a long burst (12 rounds)");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static SoldierBrain[] Six() => GameObject.Find("Hall_Team").GetComponentsInChildren<SoldierBrain>().Where(s => s.name != "Hall_Power_Armour").ToArray();

        private static SoldierBrain Armour() => UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).FirstOrDefault(s => s.name == "Hall_Power_Armour");

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>The world, free of the frame, invulnerable, in the Dining Hall with the six spawned.</summary>
        private static IEnumerator InTheHall()
        {
            GameManager.Instance.LoadLevel(World);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == World && GameManager.Instance.IsPlaying, 30f);
            yield return null;
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            yield return WaitUntil(() =>
            {
                if (CutscenePlayer.IsPlaying) CutscenePlayer.Skip();
                UprightRestraint.FreeAll();
                return StoryFlags.Has("demo_h1_wake") && !CutscenePlayer.IsPlaying && Time.timeScale > 0f;
            }, 20f);
            Room g18 = Room.Of("G18");
            lucy.GetComponent<TankController>().Teleport(g18.transform.position + new Vector3(36.3f, 0f, 3f), 90f);
            yield return WaitUntil(() => RoomTracker.CurrentId == "G18" && Six().Length == 6, 5f);
            Assert.AreEqual(6, Six().Length, "the six spawned");
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
