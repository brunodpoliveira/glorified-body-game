#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Integration.Invector;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The FSM soldiers in the AI test room (Bruno, 2026-10-23): the Invector AI body and FSM, our SoldierBrain
    /// ("move or shoot, never both", Bruno 2026-10-21) and the telegraph gun. Lucy stands still and takes no
    /// damage. Batch: tools/unity.sh test playmode EnemyAiTests.
    /// </summary>
    public class EnemyAiTests
    {
        private const string MainMenu = "Main_Menu";
        private const string Room = "AiTest";

        private GameObject lucy;
        private SoldierBrain[] soldiers;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainMenu);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == MainMenu && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(Room);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Room && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            lucy = GameObject.FindGameObjectWithTag("Player");
            Assert.IsNotNull(lucy, "Lucy in the room");
            Health health = lucy.GetComponentInChildren<Health>();
            health.SetInvulnerable(true);
            soldiers = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).OrderBy(s => s.name).ToArray();
        }

        [UnityTest]
        public IEnumerator Soldiers_SeeLucy_MoveToASpot_StopAndShoot_NeverBoth()
        {
            Assert.AreEqual(3, soldiers.Length, "three FSM soldiers");
            var start = soldiers.ToDictionary(s => s, s => s.transform.position);
            float maxMoved = 0f;
            int shots = 0;
            float t0 = Time.time;
            float nextLog = 0f;
            while (Time.time - t0 < 24f)
            {
                foreach (SoldierBrain s in soldiers)
                {
                    if (s == null) continue;
                    maxMoved = Mathf.Max(maxMoved, Vector3.Distance(start[s], s.transform.position));
                }
                shots = soldiers.Sum(s => s != null ? s.Gun.ShotsFired : 0);
                if (Time.time >= nextLog)
                {
                    nextLog = Time.time + 2f;
                    foreach (SoldierBrain s in soldiers) Debug.Log("[EnemyAiTests] " + Line(s, t0));
                }
                yield return null;
            }

            foreach (SoldierBrain s in soldiers) Debug.Log("[EnemyAiTests] end " + Line(s, t0));
            Assert.IsTrue(soldiers.All(s => s.Body.HasTarget), "every soldier found Lucy");
            Assert.Greater(maxMoved, 1f, "a soldier walked to a firing spot");
            Assert.GreaterOrEqual(shots, 1, "a soldier shot with the telegraph");
            foreach (SoldierBrain s in soldiers)
            {
                Assert.AreEqual(0, s.MovedWhileShootingFrames, s.name + " never moved while it aimed or counted down");
            }
        }

        private static string Line(SoldierBrain s, float t0)
        {
            if (s == null) return "gone";
            var body = s.GetComponent<InvectorEnemyBody>();
            return $"{Time.time - t0:0.0}s {s.name} fsm={body?.FsmState} phase={s.Current} pos={s.transform.position} speed={body?.Speed:0.00} " +
                   $"target={s.Body.HasTarget} sees={s.Body.CanSeeTarget} gun={s.Gun.Current} shots={s.Gun.ShotsFired} moves={s.Moves} shoots={s.ShootPhases} " +
                   $"spot={s.Spot} movedWhileShooting={s.MovedWhileShootingFrames}";
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
