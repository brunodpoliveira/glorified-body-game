#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Destruction;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// mk_warspite as a pressure wave (Bruno, 2026-11-15): the things her powers can move on the way to the target fly. His
    /// test the same day: "no blast on the physics objects, including the grebles on the table and the crate". In KIT TEST.
    /// Batch: tools/unity.sh test playmode WarspiteWaveTests.
    /// </summary>
    public class WarspiteWaveTests
    {
        private GameObject lucy;
        private PsychicPowers powers;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_wave_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel("KitTest");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "KitTest" && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            powers = lucy.GetComponent<PsychicPowers>();
            powers.Select(PowerKind.Blast);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SaveSystem.Folder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheWave_PushesACrateOnTheFloor_AndTheThingsOnATable()
        {
            // A crate on the floor between her and the target.
            GameObject crate = GameObject.Find("Crate_K1");
            LockOnTargetable target = GameObject.Find("Crate_K2").GetComponent<LockOnTargetable>();
            Vector3 crateAt = crate.transform.position;
            lucy.GetComponent<TankController>().Teleport(new Vector3(-5f, 0f, -0.4f), 90f);
            yield return null;
            powers.FireAt(powers.Selected, 1f, target);
            yield return new WaitForSeconds(1f);
            float moved = Vector3.Distance(crate.transform.position, crateAt);
            Debug.Log($"[WarspiteWaveTests] the crate moved {moved:0.00} m, pushed {powers.WavePushed}");
            Assert.Greater(moved, 0.5f, "the crate on the floor flies");

            // The things on a table between her and the target (the nurse behind the table, north).
            Breakable table = Breakable.All.First(b => b.name == "Table_K2" || b.Id == "Table_K2");
            Greeble[] things = Greeble.All.Where(g => Vector3.Distance(g.transform.position, table.Bounds.center) < 1.2f).ToArray();
            Assert.IsNotEmpty(things, "the table has things");
            int before = Greeble.TotalDisturbed;
            LockOnTargetable nurse = GameObject.Find("Nurse_K5").GetComponentInChildren<LockOnTargetable>();
            lucy.GetComponent<TankController>().Teleport(new Vector3(table.Bounds.center.x, 0f, -2.5f), 0f); // the nurse stands north of the table at the start
            yield return null;
            powers.FireAt(powers.Selected, 1f, nurse);
            yield return new WaitForSeconds(0.3f);
            Debug.Log($"[WarspiteWaveTests] the table: {things.Length} things, {Greeble.TotalDisturbed - before} flew");
            Assert.Greater(Greeble.TotalDisturbed - before, 0, "the things on the table fly");
        }

        [UnityTest]
        public IEnumerator AShot_AndAGrenade_PushTheThingsTheyHit()
        {
            // Bruno, 2026-11-15: "check if grenades and bullets ... can also interact with these physic objects as well".
            GameObject crate = GameObject.Find("Crate_K1");
            Vector3 at = crate.transform.position;
            Vector3 aim = crate.GetComponent<Collider>().bounds.center;
            GlorifiedBody.Enemy.EnemyProjectile.Launch(aim + new Vector3(-4f, 0f, 0f), Vector3.right, 30f, 5f, null, "test_pistol");
            yield return new WaitForSeconds(1f);
            float shot = Vector3.Distance(crate.transform.position, at);
            Debug.Log($"[WarspiteWaveTests] the shot: the crate moved {shot:0.00} m");
            Assert.Greater(shot, 0.2f, "a bullet pushes the crate it hits");

            GameObject other = GameObject.Find("Crate_K2");
            Vector3 at2 = other.transform.position;
            GlorifiedBody.Enemy.EnemyGrenade.Launch(at2 + new Vector3(0f, 1f, -2f), at2 + new Vector3(0f, 0f, -1f), 0.4f, 0.3f, 10f, 3f, null, "test_grenades");
            yield return new WaitForSeconds(2f);
            float blast = Vector3.Distance(other.transform.position, at2);
            Debug.Log($"[WarspiteWaveTests] the grenade: the crate moved {blast:0.00} m");
            Assert.Greater(blast, 0.5f, "an explosion pushes the crate near it");
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
