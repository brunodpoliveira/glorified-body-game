#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// What a death does to the others, in the AI test room (design §11.4 and §11.5; Bruno 2026-10-24: CPR and the
    /// drag of a body): the witnesses' shock, the fear by rank (a guard retreats and cowers, a soldier never runs),
    /// and a colleague who drags the body and does CPR while she hides. Lucy takes no damage.
    /// Batch: tools/unity.sh test playmode EnemyMoraleTests.
    /// </summary>
    public class EnemyMoraleTests
    {
        private GameObject lucy;
        private SoldierBrain[] soldiers;

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
            soldiers = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).OrderBy(s => s.name).ToArray();
            DebugTools.RunTelemetry.OnEvent -= LogEvent;
            DebugTools.RunTelemetry.OnEvent += LogEvent;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DebugTools.RunTelemetry.OnEvent -= LogEvent;
            yield return null;
        }

        private static void LogEvent(string name, string detail)
        {
            if (name.StartsWith("care") || name.StartsWith("soldier_shock") || name.StartsWith("soldier_retreat") || name.StartsWith("soldier_cower") || name.StartsWith("enemy_witness") || name.StartsWith("enemy_death"))
                Debug.Log($"[EnemyMoraleTests] event {name} {detail}");
        }

        [UnityTest]
        public IEnumerator GruesomeDeath_TheWitnessesSeeIt_AndAreShocked()
        {
            yield return WaitUntil(() => soldiers.All(s => s.Alerted), 8f);
            SoldierBrain victim = Find("Soldier_A");
            Kill(victim, DamageType.ShortShock);
            yield return null;
            yield return null;
            yield return null;
            Assert.IsTrue(victim.GetComponent<EnemyMorale>().DiedGruesome, "a death by the short is gruesome");
            var living = soldiers.Where(s => s != victim).ToArray();
            int witnesses = living.Count(s => s.GetComponent<EnemyMorale>().Witnessed > 0);
            Debug.Log("[EnemyMoraleTests] witnesses: " + string.Join(" ", living.Select(s => $"{s.name}:seen={s.GetComponent<EnemyMorale>().Witnessed}/fear={s.GetComponent<EnemyMorale>().Fear}/shocks={s.ShockCount}/phase={s.Current}")));
            Assert.GreaterOrEqual(witnesses, 1, "a colleague saw the death");
            foreach (SoldierBrain s in living.Where(s => s.GetComponent<EnemyMorale>().Witnessed > 0 && !s.Retreating))
            {
                Assert.GreaterOrEqual(s.ShockCount, 1, s.name + " is shocked");
                Assert.IsFalse(s.Gun.HoldsSlot, s.name + " lost its aim");
                Assert.IsTrue(s.Current == SoldierBrain.Phase.Hold, s.name + " stands still in the shock");
            }
            Assert.Greater(Bark.Count, 0, "a bark over a head");
        }

        [UnityTest]
        public IEnumerator Fear_TheGuardRetreatsAndCowers_TheSoldierNeverRuns()
        {
            yield return WaitUntil(() => soldiers.All(s => s.Alerted), 8f);
            SoldierBrain guard = Find("Guard_B");
            SoldierBrain soldier = Find("Gasser_D");
            SoldierBrain armour = Find("Armour_C");
            EnemyMorale corpseA = Find("Soldier_A").GetComponent<EnemyMorale>();
            Assert.AreEqual(EnemyMorale.Rank.Guard, guard.GetComponent<EnemyMorale>().RankOf);
            Assert.AreEqual(EnemyMorale.Rank.Soldier, soldier.GetComponent<EnemyMorale>().RankOf);
            Assert.AreEqual(EnemyMorale.Rank.Heavy, armour.GetComponent<EnemyMorale>().RankOf);

            // A gruesome death (+2) and a plain one (+1): the guard's limit is 3.
            guard.GetComponent<EnemyMorale>().See(corpseA, gruesome: true);
            Assert.IsFalse(guard.Retreating, "fear 2: shocked, not yet running");
            guard.GetComponent<EnemyMorale>().See(corpseA, gruesome: false);
            Assert.IsTrue(guard.Retreating, "fear 3: the guard runs");
            for (int i = 0; i < 6; i++)
            {
                soldier.GetComponent<EnemyMorale>().See(corpseA, gruesome: true);
                armour.GetComponent<EnemyMorale>().See(corpseA, gruesome: true);
            }
            Assert.IsFalse(soldier.Retreating, "a soldier never runs");
            Assert.IsFalse(armour.Retreating, "the power armour never runs");

            Vector3 start = guard.transform.position;
            float startGap = Flat(start, lucy.transform.position);
            int attacks = guard.Gun.ShotsFired;
            yield return WaitUntil(() => guard.Current == SoldierBrain.Phase.Cower, 10f);
            Debug.Log($"[EnemyMoraleTests] guard {start} -> {guard.transform.position}, gap {startGap:0.0} -> {Flat(guard.transform.position, lucy.transform.position):0.0} m, phase {guard.Current}");
            Assert.AreEqual(SoldierBrain.Phase.Cower, guard.Current, "it reached its spot and cowers");
            Assert.Greater(Flat(guard.transform.position, lucy.transform.position), startGap, "it ran away from her");
            yield return new WaitForSeconds(4f);
            Assert.AreEqual(attacks, guard.Gun.ShotsFired, "a cowering guard does not attack");
            Assert.AreEqual(SoldierBrain.Phase.Cower, guard.Current, "it stays down");
        }

        /// <summary>
        /// Bruno's report (2026-10-24): "the guard runs away but doesn't actually leave the place; he tries to
        /// retreat but runs in place". The fear reaches its limit while the guard aims.
        /// </summary>
        [UnityTest]
        public IEnumerator Fear_TheGuardRetreatsDuringHisAim_AndLeavesHisPlace()
        {
            yield return WaitUntil(() => soldiers.All(s => s.Alerted), 8f);
            SoldierBrain guard = Find("Guard_B");
            EnemyMorale corpseA = Find("Soldier_A").GetComponent<EnemyMorale>();
            yield return WaitUntil(() => guard.Gun.HoldsSlot && guard.Gun.Current == DummyShooter.State.Telegraph, 20f);
            Assert.IsTrue(guard.Gun.HoldsSlot, "the guard aims");
            yield return new WaitForSeconds(0.1f);
            guard.GetComponent<EnemyMorale>().See(corpseA, gruesome: true);
            guard.GetComponent<EnemyMorale>().See(corpseA, gruesome: false);
            Assert.IsTrue(guard.Retreating, "fear 3: the guard runs");
            Vector3 start = guard.transform.position;
            yield return new WaitForSeconds(2.5f);
            float moved = Flat(start, guard.transform.position);
            Debug.Log($"[EnemyMoraleTests] retreat during the aim: moved {moved:0.00} m in 2.5 s, phase {guard.Current}, kinematic {guard.GetComponent<Rigidbody>().isKinematic}");
            Assert.Greater(moved, 2f, "he left his place");
        }

        [UnityTest]
        public IEnumerator Care_ACollegueDragsTheBodyAway_ThenDoesCpr_AndStopsWhenHeSeesHer()
        {
            yield return WaitUntil(() => soldiers.All(s => s.Alerted), 8f);
            SoldierBrain victim = Find("Soldier_A");
            SoldierBrain medic = Find("Gasser_D");
            Kill(victim, DamageType.PsychicBlast);
            yield return new WaitForSeconds(2.2f); // the corpse lies down
            Vector3 home = lucy.transform.position;
            Hide();
            yield return new WaitForSeconds(0.2f);
            // The rule may already have sent a colleague (one without a line to her for 3 s). Follow that one, or
            // send the medic.
            CorpseCare care = UnityEngine.Object.FindObjectsByType<CorpseCare>(FindObjectsSortMode.None).FirstOrDefault(c => c.Patient == victim.transform);
            if (care == null)
            {
                yield return WaitUntil(() => medic.CanCare, 3f);
                care = medic.GetComponent<CorpseCare>();
                Assert.IsTrue(care.Begin(victim.transform), "the medic takes the body");
            }
            medic = care.GetComponent<SoldierBrain>();
            Debug.Log($"[EnemyMoraleTests] carer {medic.name}");
            Assert.AreSame(victim.transform, care.Patient, "his patient");
            Vector3 bodyStart = victim.transform.position;
            yield return WaitUntil(() => care.Current == CorpseCare.Step.Cpr || !care.Active, 20f);
            Debug.Log($"[EnemyMoraleTests] care: step {care.Current}, dragged {care.Dragged:0.0} m, body {bodyStart} -> {victim.transform.position}");
            Assert.AreEqual(CorpseCare.Step.Cpr, care.Current, "he dragged the body, then started CPR");
            Assert.Greater(care.Dragged, 1.5f, "the body moved with him");
            Assert.Greater(Flat(victim.transform.position, bodyStart), 1.5f, "the body is in a new place");
            int pumps = care.Compressions;
            yield return new WaitForSeconds(2f);
            Assert.GreaterOrEqual(care.Compressions - pumps, 3, "the chest compressions go on (110 a minute)");
            Assert.IsFalse(medic.Gun.HoldsSlot, "no aim during the care");

            // She comes back into view: he drops the body and fights.
            lucy.GetComponent<TankController>().Teleport(home, 0f);
            yield return WaitUntil(() => !care.Active, 2f);
            Assert.IsFalse(care.Active, "he saw her and stopped");
            Assert.AreNotEqual(SoldierBrain.Phase.Care, medic.Current, "back to the fight");
        }

        [UnityTest]
        public IEnumerator Care_StartsByItself_WhenSheHidesNearABody()
        {
            yield return WaitUntil(() => soldiers.All(s => s.Alerted), 8f);
            SoldierBrain victim = Find("Guard_B");
            Kill(victim, DamageType.PsychicBlast);
            yield return new WaitForSeconds(1.6f);
            Hide();
            yield return WaitUntil(() => soldiers.Any(s => s.GetComponent<CorpseCare>() != null && s.GetComponent<CorpseCare>().Active), 10f);
            SoldierBrain carer = soldiers.FirstOrDefault(s => s.GetComponent<CorpseCare>() != null && s.GetComponent<CorpseCare>().Active);
            Debug.Log($"[EnemyMoraleTests] carer: {(carer != null ? carer.name : "none")}");
            Assert.IsNotNull(carer, "a colleague came to tend the body while she hid");
            Assert.AreSame(victim.transform, carer.GetComponent<CorpseCare>().Patient);
            Assert.AreNotEqual("Armour_C", carer.name, "the power armour does not tend bodies");
        }

        // ── Helpers ───────────────────────────────────────────────────

        /// <summary>A kill as the game does it: the damage, then the hit event with its cause.</summary>
        private static void Kill(SoldierBrain s, DamageType cause)
        {
            Health h = s.GetComponent<Health>();
            h.TakeDamage(h.MaxHealth * 10f);
            HitEventBus.Raise(new HitInfo { Victim = h.gameObject, HitTransform = s.transform, HitPoint = s.transform.position + Vector3.up, DamageAmount = h.MaxHealth * 10f, DamageType = cause, WasFatal = true });
        }

        /// <summary>She hides: out of every line of sight (under the floor; she takes no damage).</summary>
        private void Hide()
        {
            CharacterController cc = lucy.GetComponent<CharacterController>();
            cc.enabled = false;
            lucy.transform.position = new Vector3(0f, -30f, -5f);
            Physics.SyncTransforms();
            lucy.GetComponent<TankController>().enabled = false;
        }

        private SoldierBrain Find(string name)
        {
            SoldierBrain s = soldiers.FirstOrDefault(x => x.name == name);
            Assert.IsNotNull(s, name);
            return s;
        }

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
