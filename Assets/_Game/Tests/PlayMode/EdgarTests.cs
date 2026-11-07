#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Automation;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// mk_edgar (Bruno, 2026-11-06), in the EDGAR TEST room (EdgarTestSetup.BuildEdgarTest): who it takes, the full circle, the cost, the
    /// kneel, the follower, the puzzle. Batch: tools/unity.sh test playmode EdgarTests.
    /// </summary>
    public class EdgarTests
    {
        private const string Room = "EdgarTest";

        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad pad;
            private void Update() => pad?.Commit();
        }

        private VirtualPad pad;
        private PadDriver driver;
        private GameObject lucy;
        private Health herHealth;
        private PsychicStats stats;
        private PsychicPowers powers;
        private LockOnController lockOn;
        private TankController tank;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            StoryFlags.Set(new string[0]);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(Room);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Room && GameManager.Instance.IsPlaying, 20f);
            yield return null;
            Assert.AreEqual(Room, SceneManager.GetActiveScene().name, "EdgarTestSetup.BuildEdgarTest");
            lucy = GameObject.FindGameObjectWithTag("Player");
            herHealth = lucy.GetComponentInChildren<Health>();
            herHealth.SetInvulnerable(true);
            stats = lucy.GetComponent<PsychicStats>();
            powers = lucy.GetComponent<PsychicPowers>();
            lockOn = lucy.GetComponent<LockOnController>();
            tank = lucy.GetComponent<TankController>();
            pad = new VirtualPad();
            driver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            driver.pad = pad;
            GameInput.SetBotSource(pad);
            yield return WaitUntil(() => PsychicPowers.EdgarUnlocked, 2f);
            Assert.IsTrue(PsychicPowers.EdgarUnlocked, "the room unlocks it");
            powers.Select(PowerKind.Domination);
            stats.DebugSet(100f, 0f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            pad.ReleaseAll();
            GameInput.ClearBotSource(pad);
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            yield return null;
        }

        /// <summary>Only these figures stay, and she stands in reach of the first with it locked.</summary>
        private IEnumerator Only(params string[] names)
        {
            foreach (LockOnTargetable t in UnityEngine.Object.FindObjectsByType<LockOnTargetable>(FindObjectsSortMode.None))
                if (!t.IsProp && !names.Contains(t.name)) t.gameObject.SetActive(false);
            GameObject first = GameObject.Find(names[0]);
            Assert.IsNotNull(first, names[0] + ": InvectorEnemyFactory.BuildEnemyPrefabs");
            Stand(first, 3f);
            yield return null;
            lockOn.SetTarget(first.GetComponent<LockOnTargetable>());
            yield return null;
        }

        private void Stand(GameObject figure, float metres)
        {
            Vector3 at = figure.transform.position + Vector3.back * metres;
            at.y = 0f;
            tank.Teleport(at, 0f);
        }

        private IEnumerator FullCircle()
        {
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(powers.Selected.fullChargeTime + 0.3f);
            pad.SetHeld(InputManager.GameAction.Charge, false);
            yield return null;
            yield return null;
        }

        private static IDominable Mind(GameObject figure) => figure.GetComponent<IDominable>();

        [UnityTest]
        public IEnumerator Locked_ThePowerStaysHidden_UntilTheFlag()
        {
            powers.Select(PowerKind.Blast);
            StoryFlags.Drop(PsychicPowers.EdgarFlag);
            pad.Tap(InputManager.GameAction.PowerDomination);
            yield return null;
            yield return null;
            Assert.AreEqual(PowerKind.Blast, powers.Selected.kind, "key 4 does nothing before the story gives it");
            StoryFlags.Raise(PsychicPowers.EdgarFlag);
            pad.Tap(InputManager.GameAction.PowerDomination);
            yield return null;
            yield return null;
            Assert.AreEqual(PowerKind.Domination, powers.Selected.kind, "key 4 selects it");
        }

        [UnityTest]
        public IEnumerator BeforeTheBeat_TheCapsuleIsUnknown_AndCannotBeUsed()
        {
            // Bruno, 2026-11-06: "marked as \"unknow capsule\" and cannot be used until this story beat".
            ItemDefinition capsule = Resources.Load<ItemDefinition>("Items/item_nalconmind");
            Assert.IsNotNull(capsule);
            powers.Select(PowerKind.Blast);
            DrugInventory drugs = lucy.GetComponent<DrugInventory>();
            drugs.DebugSet(DrugType.NalconMind, 1);
            stats.DebugSetMk(PowerKind.Domination, 10f);
            StoryFlags.Drop(PsychicPowers.EdgarFlag);
            Assert.AreEqual("UNKNOWN CAPSULE", capsule.ShownName);
            Assert.IsFalse(powers.UseDrug(DrugType.NalconMind), "it cannot be used");
            Assert.AreEqual("unknown", powers.LastResult);
            Assert.AreEqual(1, drugs.Count(DrugType.NalconMind), "she keeps it");
            StoryFlags.Raise(PsychicPowers.EdgarFlag);
            Assert.AreEqual("EDGAR CAPSULE", capsule.ShownName, "the name after the beat");
            Assert.IsTrue(powers.UseDrug(DrugType.NalconMind));
            Assert.AreEqual(100f, stats.Mk(PowerKind.Domination), 0.01f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ACivilian_FullHp_IsTakenByAFullCircle_ForAHugeCost()
        {
            yield return Only("Worker_C");
            GameObject worker = GameObject.Find("Worker_C");
            yield return FullCircle();
            Assert.IsTrue(Mind(worker).IsDominated, $"a civilian at full HP: {powers.LastResult}");
            Assert.AreEqual(25f, stats.Mk(PowerKind.Domination), 0.01f, "75 of the 100");
            Assert.AreEqual(100f, stats.Mk(PowerKind.Blast), 0.01f, "only its own bar");
            Assert.AreSame(worker.GetComponent<Dominated>(), Domination.Current);
            Assert.AreEqual(75f * PsychicStats.ShortPerMkPoint, stats.Short, 1.5f, "the SHORT rises with the MK spent");
        }

        [UnityTest]
        public IEnumerator AGuard_FullHp_Refuses_AtNoCost_AndWithDamage_IsTaken()
        {
            yield return Only("Guard_B");
            GameObject guard = GameObject.Find("Guard_B");
            yield return FullCircle();
            Assert.IsFalse(Mind(guard).IsDominated, "a guard at full HP");
            Assert.AreEqual(100f, stats.Mk(PowerKind.Domination), 0.01f, "no cost");
            Assert.AreEqual("dom_not_hurt", powers.LastResult);
            guard.GetComponent<Health>().TakeDamage(5f);
            yield return null;
            yield return FullCircle();
            Assert.IsTrue(Mind(guard).IsDominated, $"any damage: {powers.LastResult}");
        }

        [UnityTest]
        public IEnumerator ASoldier_FullHp_Refuses_AndWithDamage_IsTaken_AndLeavesHisTeam()
        {
            yield return Only("Soldier_A");
            GameObject soldier = GameObject.Find("Soldier_A");
            SoldierBrain brain = soldier.GetComponent<SoldierBrain>();
            yield return FullCircle();
            Assert.IsFalse(brain.IsDominated);
            soldier.GetComponent<Health>().TakeDamage(5f);
            yield return null;
            yield return FullCircle();
            Assert.IsTrue(brain.IsDominated, $"{powers.LastResult}");
            Assert.IsNull(brain.Team, "he leaves the fireteam");
            Assert.IsFalse(soldier.GetComponent<LockOnTargetable>().Selectable, "she cannot lock him now");
        }

        [UnityTest]
        public IEnumerator ABoss_NeverIsTaken_WithAnyDamage()
        {
            yield return Only("Boss_D");
            GameObject boss = GameObject.Find("Boss_D");
            boss.GetComponent<Health>().TakeDamage(50f);
            yield return null;
            yield return FullCircle();
            Assert.IsFalse(Mind(boss).IsDominated);
            Assert.AreEqual("dom_boss", powers.LastResult);
            Assert.AreEqual(100f, stats.Mk(PowerKind.Domination), 0.01f);
        }

        [UnityTest]
        public IEnumerator OutOfRange_Refuses()
        {
            yield return Only("Worker_C");
            GameObject worker = GameObject.Find("Worker_C");
            Stand(worker, powers.Selected.range + 2f);
            yield return null;
            lockOn.SetTarget(worker.GetComponent<LockOnTargetable>());
            yield return FullCircle();
            Assert.IsFalse(Mind(worker).IsDominated);
            Assert.AreEqual("dom_far", powers.LastResult);
        }

        [UnityTest]
        public IEnumerator HeKneels_DuringTheCircle_APartCircleStuns_AndCostsByTheCharge_ACancelCostsNothing()
        {
            yield return Only("Guard_B");
            GameObject guard = GameObject.Find("Guard_B");
            SoldierBrain brain = guard.GetComponent<SoldierBrain>();
            guard.GetComponent<Health>().TakeDamage(5f);
            yield return null;
            Vector3 at = guard.transform.position;
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(powers.Selected.fullChargeTime * 0.5f);
            Assert.Greater(powers.Charge, 0.2f);
            Assert.AreEqual(SoldierBrain.Phase.Hold, brain.Current, "he holds still");
            KneelPose kneel = guard.GetComponentInChildren<Animator>().GetComponent<KneelPose>();
            Assert.IsNotNull(kneel, "the pose");
            Assert.IsTrue(kneel.On, "on his knees");
            Assert.Less(Vector3.Distance(at, guard.transform.position), 0.6f, "he stays");
            float charge = powers.Charge;
            pad.SetHeld(InputManager.GameAction.Charge, false);
            yield return WaitUntil(() => !kneel.On, 0.5f);
            Assert.IsFalse(brain.IsDominated, "a part circle takes nothing");
            Assert.AreEqual("dom_stun", powers.LastResult, "it stuns him (Bruno, 2026-11-07)");
            Assert.AreEqual(100f - powers.Selected.nalconCost * charge, stats.Mk(PowerKind.Domination), 4f, "and costs by the charge");
            Assert.IsFalse(kneel.On, "he gets up");
            float mk = stats.Mk(PowerKind.Domination);

            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(powers.Selected.fullChargeTime * 0.4f);
            pad.Tap(InputManager.GameAction.Defend);
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(0f, powers.Charge, 0.001f, "the defend key cancels");
            Assert.IsFalse(kneel.On);
            Assert.AreEqual(mk, stats.Mk(PowerKind.Domination), 0.01f);
            pad.SetHeld(InputManager.GameAction.Charge, false);
        }

        [UnityTest]
        public IEnumerator OnAFloater_ItDoesNothing_ButItCosts()
        {
            // Bruno, 2026-11-07: "mind control does nothing, but it can be used".
            yield return Only("Worker_C");
            GameObject worker = GameObject.Find("Worker_C");
            powers.Select(PowerKind.Telekinesis);
            stats.DebugSet(100f, 0f);
            pad.Tap(InputManager.GameAction.Charge);
            yield return new WaitForSeconds(powers.Selected.liftSeconds + 0.5f);
            Assert.AreEqual(worker, powers.HeldLift != null ? powers.HeldLift.gameObject : null, "she holds him");

            powers.Select(PowerKind.Domination);
            yield return FullCircle();
            yield return WaitUntil(() => powers.LastResult == "dom_nothing", 0.5f);
            Assert.AreEqual("dom_nothing", powers.LastResult);
            Assert.IsFalse(Mind(worker).IsDominated, "no mind");
            Assert.AreEqual(100f - powers.Selected.nalconCost, stats.Mk(PowerKind.Domination), 0.01f, "a full circle's cost");
            Assert.IsNotNull(powers.HeldLift, "he stays in the air");
            KneelPose kneel = worker.GetComponentInChildren<Animator>().GetComponent<KneelPose>();
            Assert.IsTrue(kneel == null || !kneel.On, "no kneel");
            powers.HeldLift.Release(lucy);
        }

        [UnityTest]
        public IEnumerator APartCircle_StunsAGuardAtFullHp_ThoughSheCannotTakeHim()
        {
            yield return Only("Guard_B");
            GameObject guard = GameObject.Find("Guard_B");
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(powers.Selected.fullChargeTime * 0.5f);
            Assert.Greater(powers.Charge, 0.2f, "the circle goes on");
            Assert.AreEqual("dom_not_hurt", powers.LastResult, "the HUD says why she cannot take him");
            pad.SetHeld(InputManager.GameAction.Charge, false);
            yield return WaitUntil(() => powers.LastResult == "dom_stun", 0.5f);
            Assert.AreEqual("dom_stun", powers.LastResult);
            Assert.AreEqual(DummyShooter.State.Staggered, guard.GetComponent<DummyShooter>().Current, "it breaks his aim, as mk_erebus does");
            Assert.Less(stats.Mk(PowerKind.Domination), 100f, "it costs");
            Assert.IsFalse(Mind(guard).IsDominated);
        }

        [UnityTest]
        public IEnumerator AHitOnHer_BreaksTheCircle()
        {
            yield return Only("Worker_C");
            GameObject worker = GameObject.Find("Worker_C");
            herHealth.SetInvulnerable(false);
            herHealth.SetHealth(herHealth.MaxHealth);
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(powers.Selected.fullChargeTime * 0.4f);
            Assert.Greater(powers.Charge, 0f);
            herHealth.TakeDamage(5f);
            yield return null;
            yield return null;
            Assert.AreEqual(0f, powers.Charge, 0.001f);
            Assert.AreEqual("dom_broken", powers.LastResult);
            yield return new WaitForSeconds(powers.Selected.fullChargeTime);
            Assert.IsFalse(Mind(worker).IsDominated);
            Assert.AreEqual(100f, stats.Mk(PowerKind.Domination), 0.01f, "no cost");
        }

        [UnityTest]
        public IEnumerator TheBar_PaysForOne_AndTheCapsuleFillsItForTheNext()
        {
            yield return Only("Worker_C", "Guard_B");
            GameObject worker = GameObject.Find("Worker_C"), guard = GameObject.Find("Guard_B");
            yield return FullCircle();
            Assert.IsTrue(Mind(worker).IsDominated);
            guard.GetComponent<Health>().TakeDamage(5f);
            Stand(guard, 3f);
            yield return null;
            lockOn.SetTarget(guard.GetComponent<LockOnTargetable>());
            yield return null;
            yield return FullCircle();
            Assert.IsFalse(Mind(guard).IsDominated, "25 MK is not enough");
            Assert.AreEqual("no_nalcon", powers.LastResult);

            DrugInventory drugs = lucy.GetComponent<DrugInventory>();
            drugs.DebugSet(DrugType.NalconMind, 1);
            Assert.IsTrue(powers.UseDrug(DrugType.NalconMind), "its own capsule");
            Assert.AreEqual(100f, stats.Mk(PowerKind.Domination), 0.01f);
            yield return null;
            yield return FullCircle();
            Assert.IsTrue(Mind(guard).IsDominated, $"{powers.LastResult}");
            Assert.IsFalse(Mind(worker).IsDominated, "one at a time: the old one goes");
            Assert.AreSame(guard.GetComponent<Dominated>(), Domination.Current);
        }

        [UnityTest]
        public IEnumerator TheDominated_Follows_FightsHisColleagues_AndTheyFightHim_AndHerBulletsStaySafe()
        {
            yield return Only("Guard_B", "Soldier_A");
            GameObject guard = GameObject.Find("Guard_B"), soldier = GameObject.Find("Soldier_A");
            SoldierBrain guardBrain = guard.GetComponent<SoldierBrain>(), soldierBrain = soldier.GetComponent<SoldierBrain>();
            soldier.SetActive(false);
            guard.GetComponent<Health>().TakeDamage(5f);
            yield return null;
            yield return FullCircle();
            Assert.IsTrue(guardBrain.IsDominated);

            // No enemy: he comes along.
            tank.Teleport(new Vector3(-9f, 0f, -6f), 0f);
            yield return WaitUntil(() => Vector3.Distance(guard.transform.position, lucy.transform.position) < 4f, 12f);
            Assert.Less(Vector3.Distance(guard.transform.position, lucy.transform.position), 4f, "he follows her");
            Assert.AreEqual(0, guardBrain.Gun.ShotsFired, "he has nobody to fire at");

            // A soldier: they fight.
            soldier.SetActive(true);
            soldierBrain.Alert("test", callOthers: false);
            Health soldierHealth = soldier.GetComponent<Health>(), guardHealth = guard.GetComponent<Health>();
            float s0 = soldierHealth.CurrentHealth, g0 = guardHealth.CurrentHealth;
            herHealth.SetInvulnerable(false);
            herHealth.SetHealth(herHealth.MaxHealth);
            float her0 = herHealth.CurrentHealth;
            stats.AddShort(-100f);
            tank.Teleport(new Vector3(-11f, 0f, -7.5f), 0f);
            yield return WaitUntil(() => soldierHealth.CurrentHealth < s0 && guardHealth.CurrentHealth < g0, 45f);
            Debug.Log($"[EdgarTests] the fight: soldier {soldierHealth.CurrentHealth:0}/{s0:0}, guard {guardHealth.CurrentHealth:0}/{g0:0}, guard shots {guardBrain.Gun.ShotsFired}, soldier shots {soldierBrain.Gun.ShotsFired}, her {herHealth.CurrentHealth:0}/{her0:0}");
            Assert.Less(soldierHealth.CurrentHealth, s0, "his bullets hurt the soldier");
            Assert.Less(guardHealth.CurrentHealth, g0, "the soldier's bullets hurt him");
        }

        [UnityTest]
        public IEnumerator ThePuzzle_AGuardAtFullHp_BehindTheRail_SentToTheSwitch_OpensTheGate()
        {
            yield return Only("Guard_Far");
            GameObject guard = GameObject.Find("Guard_Far");
            GameObject gate = GameObject.Find("Gate_Leaf");
            Assert.IsTrue(gate.activeInHierarchy, "the gate is shut");
            Assert.AreEqual(guard.GetComponent<Health>().MaxHealth, guard.GetComponent<Health>().CurrentHealth, 0.01f, "full HP");
            tank.Teleport(new Vector3(guard.transform.position.x, 0f, 1.5f), 0f); // at the rail
            yield return null;
            lockOn.SetTarget(guard.GetComponent<LockOnTargetable>());
            yield return null;
            yield return FullCircle();
            Assert.IsTrue(Mind(guard).IsDominated, $"a puzzle figure at full HP: {powers.LastResult}");
            pad.Tap(InputManager.GameAction.Defend);
            yield return null;
            yield return null;
            Assert.AreEqual("dom_sent", powers.LastResult, "the defend key sends him");
            yield return WaitUntil(() => StoryFlags.Has(EdgarTestSetup_SwitchFlag), 15f);
            Assert.IsTrue(StoryFlags.Has(EdgarTestSetup_SwitchFlag), $"he reached the switch; he stands at {guard.transform.position}");
            yield return null;
            Assert.IsFalse(gate.activeInHierarchy, "the gate opens");
        }

        private const string EdgarTestSetup_SwitchFlag = "edgar_test_switch"; // editor code

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
