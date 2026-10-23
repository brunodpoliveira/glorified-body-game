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
using GlorifiedBody.Integration.Invector;
using GlorifiedBody.Player;
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
            var found = new System.Collections.Generic.HashSet<SoldierBrain>();
            float t0 = Time.time;
            float nextLog = 0f;
            while (Time.time - t0 < 24f)
            {
                foreach (SoldierBrain s in soldiers)
                {
                    if (s == null) continue;
                    maxMoved = Mathf.Max(maxMoved, Vector3.Distance(start[s], s.transform.position));
                    if (s.Body.HasTarget) found.Add(s);
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
            Assert.AreEqual(soldiers.Length, found.Count, "every soldier found Lucy");
            Assert.Greater(maxMoved, 1f, "a soldier walked to a firing spot");
            Assert.GreaterOrEqual(shots, 1, "a soldier shot with the telegraph");
            foreach (SoldierBrain s in soldiers)
            {
                Assert.AreEqual(0, s.MovedWhileShootingFrames, s.name + " never moved while it aimed or counted down");
            }
        }

        [UnityTest]
        public IEnumerator Lift_SuspendsTheSoldier_ThenItStandsAndFightsAgain()
        {
            SoldierBrain soldier = Find("Soldier_A");
            var body = soldier.GetComponent<InvectorEnemyBody>();
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            yield return WaitUntil(() => soldier.Engaged, 6f);
            powers.Select(PowerKind.Telekinesis);
            powers.FireAt(powers.Selected, 1f, soldier.GetComponent<LockOnTargetable>());
            yield return new WaitForSeconds(1.0f);
            float y = soldier.transform.position.y;
            int shots = soldier.Gun.ShotsFired;
            Debug.Log("[EnemyAiTests] lifted " + Line(soldier, 0f));
            Assert.Greater(y, 1f, "the soldier hangs in the air");
            Assert.IsTrue(body.Suspended && body.FsmStopped, "the body and its FSM are off while it hangs");
            yield return new WaitForSeconds(2.0f);
            Assert.Greater(soldier.transform.position.y, 1f, "it still hangs: the AI does not pull it down");
            Assert.AreEqual(shots, soldier.Gun.ShotsFired, "no shot from the air");

            TelekinesisLift lift = soldier.GetComponent<TelekinesisLift>();
            Assert.IsTrue(lift.Release(null), "dropped");
            yield return WaitUntil(() => soldier.GetComponent<TelekinesisLift>() == null, 2f);
            yield return new WaitForSeconds(0.5f);
            Assert.Less(soldier.transform.position.y, 0.2f, "back on the floor");
            Assert.IsFalse(body.Suspended || body.FsmStopped, "the body runs again");
            Vector3 at = soldier.transform.position;
            int phases = soldier.Moves + soldier.ShootPhases;
            yield return new WaitForSeconds(6f);
            Debug.Log("[EnemyAiTests] after the drop " + Line(soldier, 0f));
            Assert.Greater(soldier.Moves + soldier.ShootPhases, phases, "it fights again after the drop");
        }

        [UnityTest]
        public IEnumerator Blast_StaggersTheSoldier_ItStandsStillForTheStagger()
        {
            SoldierBrain soldier = Find("Soldier_A");
            var body = soldier.GetComponent<InvectorEnemyBody>();
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            yield return WaitUntil(() => soldier.Current == SoldierBrain.Phase.Move && body.IsMoving, 8f);
            powers.Select(PowerKind.Blast);
            powers.FireAt(powers.Selected, 0.3f, soldier.GetComponent<LockOnTargetable>(), 1f); // a weak blast: it lives
            yield return null;
            Assert.AreEqual(SoldierBrain.Phase.Hold, soldier.Current, "the stagger stops the soldier");
            yield return new WaitForSeconds(0.2f);
            Vector3 at = soldier.transform.position;
            yield return new WaitForSeconds(0.15f);
            Assert.Less(Vector3.Distance(at, soldier.transform.position), 0.06f, "it stands still in the stagger");
        }

        [UnityTest]
        public IEnumerator Death_KillsTheInvectorBody_AndTheCorpseStays()
        {
            SoldierBrain soldier = Find("Guard_B");
            var body = soldier.GetComponent<InvectorEnemyBody>();
            Health health = soldier.GetComponent<Health>();
            yield return WaitUntil(() => soldier.Engaged, 6f);
            health.TakeDamage(health.MaxHealth * 10f);
            yield return null;
            Assert.IsTrue(health.IsDead, "our Health is dead");
            Assert.IsTrue(body.IsDead, "the Invector body is dead too");
            Assert.AreEqual(SoldierBrain.Phase.Dead, soldier.Current);
            Assert.IsTrue(soldier.Gun.IsDead, "the gun is dead");
            Assert.IsFalse(soldier.GetComponent<LockOnTargetable>().IsAlive, "out of the lock");
            int shots = soldier.Gun.ShotsFired;
            yield return new WaitForSeconds(3f);
            Assert.IsTrue(soldier != null, "the corpse stays in the room");
            Assert.AreEqual(shots, soldier.Gun.ShotsFired, "a corpse does not shoot");
        }

        [UnityTest]
        public IEnumerator PowerArmour_IsHeavy_TheLiftRefusesIt()
        {
            SoldierBrain armour = Find("Armour_C");
            LockOnTargetable target = armour.GetComponent<LockOnTargetable>();
            Assert.IsTrue(target.IsHeavy, "the power armour is heavy (Bruno, 2026-10-23)");
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            LockOnController lockOn = lucy.GetComponent<LockOnController>();
            powers.Select(PowerKind.Telekinesis);
            lockOn.SetTarget(target);
            Assert.IsFalse(powers.Release(1f), "no lift");
            Assert.AreEqual("too_heavy", powers.LastResult);
            yield return new WaitForSeconds(0.5f);
            Assert.IsNull(armour.GetComponent<TelekinesisLift>(), "it stays on its feet");
        }

        [UnityTest]
        public IEnumerator EasyDifficulty_OnlyOneSoldierAimsAtATime()
        {
            DifficultyModuleDefinition previous = DifficultyManager.Instance.ActivePreset;
            DifficultyManager.Instance.SetPreset(DifficultyModuleDefinition.ModuleType.PresetEasy);
            Assert.AreEqual(1, ShooterScheduler.MaxSlots, "easy: one shooter at a time");
            int maxAiming = 0, shots = 0;
            float t0 = Time.time;
            while (Time.time - t0 < 18f)
            {
                maxAiming = Mathf.Max(maxAiming, soldiers.Count(s => s != null && s.Gun.HoldsSlot));
                yield return null;
            }
            shots = soldiers.Sum(s => s.Gun.ShotsFired);
            // Back to the player's own preset (ResetToDefaults would also clear the modifiers).
            DifficultyManager.Instance.SetPreset(previous != null ? previous.moduleType : DifficultyModuleDefinition.ModuleType.PresetNormal);
            Debug.Log($"[EnemyAiTests] easy: max aiming at once {maxAiming}, shots {shots}");
            Assert.AreEqual(1, maxAiming, "never two aims at once on easy");
            Assert.GreaterOrEqual(shots, 2, "they still shoot, one after the other");
        }

        private SoldierBrain Find(string name)
        {
            SoldierBrain soldier = soldiers.FirstOrDefault(s => s.name == name);
            Assert.IsNotNull(soldier, name);
            return soldier;
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
