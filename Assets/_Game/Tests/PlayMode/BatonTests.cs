#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The overconfident guard (round 134, GAME_NOTES.md; Bruno, 2026-11-13: "he strikes"). He does not aim at a patient with no
    /// gun: he walks at her with the baton and strikes in reach, he runs after her when she runs, and when she attacks the
    /// baton falls, he draws his gun and fires. A guard of another personality keeps his gun. In the AI TEST room with only
    /// Guard_B (the shield and the pistol) awake. Batch: tools/unity.sh test playmode BatonTests.
    /// </summary>
    public class BatonTests
    {
        private GameObject lucy;
        private TankController tank;
        private SoldierBrain guard;
        private EnemyGun weapon;
        private readonly HashSet<string> said = new HashSet<string>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel("AiTest");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "AiTest" && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            tank = lucy.GetComponent<TankController>();
            tank.Teleport(new Vector3(-2f, 0f, -4f), 0f);
            foreach (SoldierBrain b in UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None))
                if (b.name != "Guard_B") b.gameObject.SetActive(false); // only the guard
            guard = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).First(b => b.name == "Guard_B");
            weapon = guard.GetComponent<EnemyGun>();
            said.Clear();
            UnityEngine.Random.InitState(1113);
            SoldierBrain.BatonEnabled = true; // off in batch runs: this class tests it
        }

        [TearDown]
        public void BatonOff() => SoldierBrain.BatonEnabled = !Application.isBatchMode;

        [UnityTest]
        public IEnumerator TheOverconfidentGuard_WalksAtHer_WithTheBaton_AndStrikes_NoShot()
        {
            BarkVoice.Of(guard.gameObject).SetPersonality(SoldierBrain.BatonPersonality);
            guard.Alert("sight", callOthers: false);
            Assert.IsTrue(guard.BatonOut, "an overconfident guard takes the baton at his alert");
            Assert.IsTrue(weapon.BatonShown, "the baton in his hand, the gun out of sight");
            Assert.IsTrue(guard.Gun.MeleeOnly, "the gun only strikes");
            // His speed over windows of 0.5 s (one frame can carry a snap of the vendor body).
            var windows = new List<float>();
            Vector3 last = guard.transform.position;
            float windowStart = Time.time;
            float t0 = Time.time;
            while (guard.Gun.MeleeStrikes == 0 && Time.time - t0 < 25f)
            {
                yield return null;
                Listen();
                if (Time.time - windowStart >= 0.5f)
                {
                    windows.Add(Flat(guard.transform.position, last) / (Time.time - windowStart));
                    last = guard.transform.position;
                    windowStart = Time.time;
                }
            }
            float fastest = windows.Count > 0 ? windows.Max() : 0f;
            Debug.Log("[BatonTests] his speed by 0.5 s: " + string.Join(" ", windows.Select(w => w.ToString("0.0"))));
            Debug.Log($"[BatonTests] strikes {guard.Gun.MeleeStrikes}, rounds {guard.Gun.RoundsFired}, his top walk speed {fastest:0.00} m/s, lines: {string.Join(" | ", said)}");
            Assert.Greater(guard.Gun.MeleeStrikes, 0, "he reached her and struck with the baton");
            Assert.AreEqual(0, guard.Gun.RoundsFired, "no shot at a patient with no gun");
            Assert.IsFalse(guard.Chasing, "she stood: no chase");
            Assert.Less(fastest, 2.4f, "a slow walk at her, not a run");
            Assert.IsTrue(said.Any(l => Rows(BarkSituation.Baton).Contains(l)), "an 'approaches with the baton' line");
        }

        [UnityTest]
        public IEnumerator SheRuns_HeChasesHer_SheStops_HeWalks()
        {
            BarkVoice.Of(guard.gameObject).SetPersonality(SoldierBrain.BatonPersonality);
            tank.Teleport(new Vector3(4f, 0f, -5.5f), 180f);
            guard.Alert("sight", callOthers: false);
            yield return WaitUntil(() => Flat(guard.transform.position, lucy.transform.position) < 5f, 20f);
            Assert.IsTrue(guard.BatonOut);
            // She runs west at her run speed for 3 s.
            float t0 = Time.time;
            while (Time.time - t0 < 3f)
            {
                Vector3 p = lucy.transform.position + Vector3.left * tank.RunSpeed * Time.deltaTime;
                if (p.x < -8f) break;
                tank.Teleport(p, 270f);
                yield return null;
                Listen();
            }
            Assert.IsTrue(guard.Chasing, "she ran: he runs after her");
            Assert.AreEqual(1, guard.Chases);
            // She stands: he walks again.
            yield return WaitUntil(() => { Listen(); return !guard.Chasing; }, 4f);
            Debug.Log($"[BatonTests] chases {guard.Chases}, lines: {string.Join(" | ", said)}");
            Assert.IsFalse(guard.Chasing, "she stopped: the chase ends");
            Assert.IsTrue(guard.BatonOut, "a run is no attack: the baton stays");
            Assert.IsTrue(said.Any(l => Rows(BarkSituation.Chases).Contains(l)), "a 'chases her' line");
        }

        [UnityTest]
        public IEnumerator SheAttacks_TheBatonFalls_HeDrawsAndFires()
        {
            BarkVoice.Of(guard.gameObject).SetPersonality(SoldierBrain.BatonPersonality);
            tank.Teleport(new Vector3(-3f, 0f, -5f), 45f);
            guard.Alert("sight", callOthers: false);
            Assert.IsTrue(guard.BatonOut);
            yield return new WaitForSeconds(1f);
            guard.GetComponent<Health>().TakeDamage(5f); // her hit
            Listen();
            Assert.IsFalse(guard.BatonOut, "she attacked: the baton goes");
            Assert.AreEqual("hurt", guard.DrewReason);
            Assert.IsFalse(guard.Gun.MeleeOnly, "the gun fires again");
            Assert.IsFalse(weapon.BatonShown, "the baton is on the floor");
            Assert.IsTrue(said.Any(l => Rows(BarkSituation.DrawsGun).Contains(l)), "a 'draws the gun' line");
            // She stays out of reach: he fires.
            float t0 = Time.time;
            while (guard.Gun.RoundsFired == 0 && Time.time - t0 < 20f)
            {
                if (Flat(guard.transform.position, lucy.transform.position) < 4f) tank.Teleport(new Vector3(-7f, 0f, -5.5f), 45f);
                yield return null;
            }
            Debug.Log($"[BatonTests] after the draw: rounds {guard.Gun.RoundsFired}, strikes {guard.Gun.MeleeStrikes}");
            Assert.Greater(guard.Gun.RoundsFired, 0, "he aims and fires");
        }

        [UnityTest]
        public IEnumerator AGuardOfAnotherPersonality_KeepsHisGun()
        {
            BarkVoice.Of(guard.gameObject).SetPersonality("professional");
            guard.Alert("sight", callOthers: false);
            yield return null;
            Assert.IsFalse(guard.BatonOut, "only the overconfident guard takes the baton");
            Assert.IsFalse(weapon.BatonShown);
            Assert.IsFalse(guard.Gun.MeleeOnly);
        }

        private void Listen()
        {
            Bark bark = guard.GetComponent<Bark>();
            if (bark != null && bark.Shown != "") said.Add(bark.Shown);
        }

        private static HashSet<string> Rows(string situation)
            => new HashSet<string>(BarkBook.RowsFor("guard", SoldierBrain.BatonPersonality, situation).Select(r => r.line));

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
