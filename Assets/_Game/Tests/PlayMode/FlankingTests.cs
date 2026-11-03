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
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// Flanking (2026-11-03, DEMO_PLAN 13.1). Bruno: "verify if the soldiers prefer attacking her from the flanks or behind
    /// (that'll make them look smarter by trying to flank/pinch her)"; "guards would try to face her head-on, soldiers and
    /// power armor flank her". In the AI TEST room (a rifle soldier, a shield guard, the power armour, a gas soldier; she
    /// stands still and faces north, at them): the three flankers claim three different sides of the fight, two of them
    /// opposite (the pinch); the guard claims none; the flankers pick few spots in her view, and the guard keeps to his own
    /// line. The knife's side run is in ArmedCivilianTests. Batch: tools/unity.sh test playmode FlankingTests.
    /// </summary>
    public class FlankingTests
    {
        private GameObject lucy;
        private SoldierBrain[] all;

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
            lucy.GetComponent<TankController>().Teleport(new Vector3(0f, 0f, -4f), 0f);
            all = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).OrderBy(s => s.name).ToArray();
            UnityEngine.Random.InitState(1103);
        }

        [UnityTest]
        public IEnumerator TheFlankers_TakeDifferentSides_TwoOfThemOpposite_TheGuardNone()
        {
            SoldierBrain guard = Brain("Guard_B");
            Assert.AreEqual(SoldierBrain.Approach.HeadOn, guard.Style, "a guard faces her head-on");
            foreach (string n in new[] { "Soldier_A", "Armour_C", "Gasser_D" })
                Assert.AreEqual(SoldierBrain.Approach.Flank, Brain(n).Style, n + " flanks");
            foreach (SoldierBrain b in all) b.Alert("test", callOthers: false);
            yield return WaitUntil(() => FlankBoard.Count >= 3, 12f);
            float[] slots = new[] { "Soldier_A", "Armour_C", "Gasser_D" }.Select(n => FlankBoard.SlotOf(Brain(n)) ?? float.NaN).ToArray();
            Debug.Log($"[FlankingTests] the sides: {string.Join(", ", slots.Select(x => x.ToString("0")))}");
            Assert.IsFalse(slots.Any(float.IsNaN), "every flanker claimed a side");
            Assert.AreEqual(3, slots.Distinct().Count(), "three different sides");
            bool opposite = slots.Any(a => slots.Any(b => Mathf.Abs(Mathf.DeltaAngle(a, b)) >= 170f));
            Assert.IsTrue(opposite, "two of them on opposite sides of her: the pinch");
            Assert.IsNull(FlankBoard.SlotOf(guard), "the guard claims no side");
        }

        [UnityTest]
        public IEnumerator TheFlankers_KeepOutOfHerView_TheGuardKeepsToHisLine()
        {
            foreach (SoldierBrain b in all) b.Alert("test", callOthers: false);
            SoldierBrain guard = Brain("Guard_B");
            float guardOffLine = 0f;
            int guardPicks = 0, lastPicks = 0;
            float t0 = Time.time;
            while (Time.time - t0 < 20f)
            {
                // She stands and faces north, at where they came from.
                lucy.transform.rotation = Quaternion.identity;
                if (guard.SpotsPicked != lastPicks)
                {
                    lastPicks = guard.SpotsPicked;
                    guardPicks++;
                    guardOffLine = Mathf.Max(guardOffLine, guard.LastSpotOffLine);
                }
                yield return null;
            }
            SoldierBrain[] flankers = all.Where(b => b.Style == SoldierBrain.Approach.Flank).ToArray();
            int picked = flankers.Sum(b => b.SpotsPicked), inView = flankers.Sum(b => b.SpotsInHerView);
            Debug.Log($"[FlankingTests] flankers: {inView} of {picked} spots in her view; the guard: {guardPicks} picks, at most {guardOffLine:0} degrees off his line");
            Assert.Greater(picked, 3, "the flankers moved");
            Assert.LessOrEqual(inView, picked / 3, "at most a third of the flankers' spots in her view (most on her flanks or behind)");
            Assert.Greater(guardPicks, 0, "the guard moved");
            Assert.LessOrEqual(guardOffLine, 31f, "the guard keeps to his line to her (head-on)");
        }

        private SoldierBrain Brain(string name)
        {
            SoldierBrain b = all.FirstOrDefault(s => s.name == name);
            Assert.IsNotNull(b, name);
            return b;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
