#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Audio;
using GlorifiedBody.Automation;
using GlorifiedBody.Cameras;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The room kit and everything together (2026-10-26), in the KIT TEST room (in git: KitTestSetup.BuildKitTest; the
    /// enemy prefabs are local: InvectorEnemyFactory.BuildEnemyPrefabs): the markers become their figures in place,
    /// the cameras cover the floor, a fight turns the music and sends the civilians running, the dead stay dead
    /// through a save and a load and through a trip to another room. Batch: tools/unity.sh test playmode KitTests.
    /// </summary>
    public class KitTests
    {
        private const string Room = "KitTest";
        private static readonly string[] Armed = { "Soldier_K1", "Soldier_K2", "Guard_K3" };
        private static readonly string[] Civilians = { "Lab_K4", "Nurse_K5" };
        private GameObject lucy;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_kit_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(Room);
            yield return InRoom();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SaveSystem.Folder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheMarkers_BecomeTheirFigures_InPlace()
        {
            yield return null;
            Assert.AreEqual(0, EnemySpawn.Missing, "every prefab is there (InvectorEnemyFactory.BuildEnemyPrefabs)");
            Assert.IsEmpty(UnityEngine.Object.FindObjectsByType<EnemySpawn>(FindObjectsInactive.Include, FindObjectsSortMode.None), "no marker is left");
            foreach (string name in Armed)
            {
                GameObject figure = GameObject.Find(name);
                Assert.IsNotNull(figure, name);
                Assert.IsNotNull(figure.GetComponent<SoldierBrain>(), name + " fights");
                Assert.AreEqual("[Enemies]/" + name, ScenePaths.Of(figure.transform), "the save's path");
            }
            foreach (string name in Civilians) Assert.IsNotNull(GameObject.Find(name).GetComponent<CivilianBrain>(), name + " is a civilian");
            Assert.AreEqual(3, UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).Length);
            Assert.IsNotNull(GameObject.Find("Guard_K3").GetComponent<EnemyShield>(), "the guard has his shield");
            Assert.IsTrue(NavMesh.SamplePosition(lucy.transform.position, out _, 1f, NavMesh.AllAreas), "the room's NavMesh");
            Assert.AreEqual(KitTestSetupMusicKey, MusicDirector.Instance.RoomKey, "the room's music key");
        }

        [UnityTest]
        public IEnumerator EveryFloorPoint_IsInTheFrameOfItsCamera()
        {
            yield return null;
            CameraCoverage.Report report = CameraCoverage.Check(FixedCameraDirector.Instance, CameraCoverage.GameRoomNear, false);
            Debug.Log("[KitTests] " + report);
            Assert.Greater(report.Total, 50);
            Assert.LessOrEqual(report.Uncovered.Count, report.Total * 0.05f, report.ToString());
        }

        [UnityTest]
        public IEnumerator AFight_TurnsTheMusic_SendsTheCiviliansRunning_AndPaysExp()
        {
            foreach (SoldierBrain s in UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None)) s.Alert("test", callOthers: false);
            MusicDirector music = MusicDirector.Instance;
            yield return WaitUntil(() => music.State == MusicState.Combat || music.State == MusicState.Tension, 10f);
            Assert.That(music.State, Is.EqualTo(MusicState.Combat).Or.EqualTo(MusicState.Tension), "they hunt her: the music turns");
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            int exp0 = stats.Exp;
            foreach (string name in Armed) Kill(name);
            Assert.AreEqual(exp0 + 20 + 20 + 15, stats.Exp, "two soldiers and a guard");
            CivilianBrain lab = GameObject.Find("Lab_K4").GetComponent<CivilianBrain>();
            yield return WaitUntil(() => lab.Current != CivilianBrain.Phase.Idle && lab.Current != CivilianBrain.Phase.Wary, 8f);
            Debug.Log($"[KitTests] the lab worker after the kills: {lab.Current}");
            Assert.That(lab.Current, Is.Not.EqualTo(CivilianBrain.Phase.Idle), "the civilians react to the kills");
            yield return WaitUntil(() => music.State == MusicState.Ambient, MusicDirector.CombatHoldSeconds + 6f);
            Assert.AreEqual(MusicState.Ambient, music.State, "no one hunts her: the ambient comes back");
        }

        [UnityTest]
        public IEnumerator ASaveAndALoad_KeepTheDead_AndSpawnNoSecondFigure()
        {
            Kill("Soldier_K1");
            Hurt("Guard_K3");
            UnityEngine.Object.Destroy(GameObject.Find("Drug_Nalcon"));
            yield return new WaitForSeconds(1.5f);
            SavePoint point = UnityEngine.Object.FindFirstObjectByType<SavePoint>();
            Assert.IsTrue(SaveSystem.SaveManual(1, point.SpotId));
            int applied = SaveSystem.Applies;
            Assert.IsTrue(SaveSystem.Load(1));
            yield return WaitUntil(() => SaveSystem.Applies > applied, 10f);
            yield return InRoom();
            Assert.IsTrue(GameObject.Find("Soldier_K1").GetComponent<Health>().IsDead, "the dead soldier stays dead");
            Assert.IsFalse(GameObject.Find("Soldier_K2").GetComponent<Health>().IsDead, "the others live");
            Assert.AreEqual(3, UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).Length, "no second figure");
            AssertFullAndCalm("Guard_K3");
            GameObject pill = GameObject.Find("Drug_Nalcon");
            Assert.IsTrue(pill == null || !pill.activeInHierarchy, "the pill stays taken");
        }

        [UnityTest]
        public IEnumerator ATripToAnotherRoom_AndBack_KeepsTheDead()
        {
            Kill("Soldier_K2");
            Hurt("Guard_K3");
            yield return new WaitForSeconds(1.5f);
            int arrivals = SaveSystem.Arrivals;
            Assert.IsTrue(GameObject.Find("Exit_KitWest").GetComponent<RoomExit>().Use());
            yield return WaitUntil(() => SaveSystem.Arrivals > arrivals, 10f);
            Assert.AreEqual("TravelTest_B", SceneManager.GetActiveScene().name);
            arrivals = SaveSystem.Arrivals;
            Assert.IsTrue(WorldState.Travel(Room, "Entry_KitWest"), "back to the hall");
            yield return WaitUntil(() => SaveSystem.Arrivals > arrivals, 10f);
            yield return InRoom();
            Assert.IsTrue(GameObject.Find("Soldier_K2").GetComponent<Health>().IsDead, "dead, as she left him");
            Assert.AreEqual(3, UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).Length, "no second figure");
            AssertFullAndCalm("Guard_K3");
        }

        private const string KitTestSetupMusicKey = "kit_hall";

        private void Kill(string name)
        {
            Health h = GameObject.Find(name).GetComponent<Health>();
            h.TakeDamage(h.MaxHealth * 10f);
            HitEventBus.Raise(new HitInfo { Attacker = lucy, Victim = h.gameObject, HitTransform = h.transform, HitPoint = h.transform.position + Vector3.up, DamageAmount = 999f, DamageType = DamageType.PsychicBlast, WasFatal = true });
        }

        /// <summary>A live enemy she hurt, alerted. Bruno, 2026-10-26: after a load or a return he is at full HP and calm.</summary>
        private void Hurt(string name)
        {
            Health h = GameObject.Find(name).GetComponent<Health>();
            h.TakeDamage(h.MaxHealth * 0.5f);
            GameObject.Find(name).GetComponent<SoldierBrain>().Alert("test", callOthers: false);
            Assert.Less(h.CurrentHealth, h.MaxHealth, "hurt");
            Assert.IsTrue(GameObject.Find(name).GetComponent<SoldierBrain>().Alerted, "alerted");
        }

        private static void AssertFullAndCalm(string name)
        {
            Health h = GameObject.Find(name).GetComponent<Health>();
            Assert.IsFalse(h.IsDead, name + " lives");
            Assert.AreEqual(h.MaxHealth, h.CurrentHealth, 0.01f, name + " is at full HP again");
            Assert.IsFalse(GameObject.Find(name).GetComponent<SoldierBrain>().Alerted, name + " is calm again");
        }

        private IEnumerator InRoom()
        {
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Room && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            Assert.AreEqual(Room, SceneManager.GetActiveScene().name, "the kit test room (KitTestSetup.BuildKitTest)");
            lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
