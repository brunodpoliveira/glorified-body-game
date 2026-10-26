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
using GlorifiedBody.UI;

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
            DebugTools.RunTelemetry.OnEvent -= LogWeaponEvent;
            DebugTools.RunTelemetry.OnEvent += LogWeaponEvent;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DebugTools.RunTelemetry.OnEvent -= LogWeaponEvent;
            yield return null;
        }

        /// <summary>
        /// The music (2026-10-26): Ambient while no one hunts her, Tension while alerted soldiers hunt her (their guns
        /// off here, so no aim turns it to Combat), and their boots make steps as they run.
        /// </summary>
        [UnityTest]
        public IEnumerator HuntingSoldiers_TurnTheMusicToTension_AndTheirBootsSound()
        {
            GlorifiedBody.Audio.MusicDirector music = GlorifiedBody.Audio.MusicDirector.Instance;
            Assert.IsNotNull(music, "the music director");
            foreach (DummyShooter gun in UnityEngine.Object.FindObjectsByType<DummyShooter>(FindObjectsSortMode.None)) gun.enabled = false;
            yield return WaitUntil(() => music.State == GlorifiedBody.Audio.MusicState.Ambient, 4f);
            Assert.AreEqual(GlorifiedBody.Audio.MusicState.Ambient, music.State, "no one hunts her yet");
            foreach (SoldierBrain s in soldiers) s.Alert("test", callOthers: false);
            yield return WaitUntil(() => music.State == GlorifiedBody.Audio.MusicState.Tension, 3f);
            Assert.AreEqual(GlorifiedBody.Audio.MusicState.Tension, music.State, "they hunt her");
            Assert.AreEqual("tension", music.CurrentClip.name, "the placeholder tension track");
            int steps0 = soldiers.Sum(s => s.GetComponent<GlorifiedBody.Audio.Footsteps>()?.Steps ?? 0);
            yield return new WaitForSeconds(3f);
            int steps1 = soldiers.Sum(s => s.GetComponent<GlorifiedBody.Audio.Footsteps>()?.Steps ?? 0);
            Assert.Greater(steps1 - steps0, 2, "their boots as they move");
            Assert.AreEqual(GlorifiedBody.Audio.MusicState.Tension, music.State, "still hunting, no aim");
        }

        /// <summary>The weapon events in the log: the bursts, the catches, the hits.</summary>
        private static void LogWeaponEvent(string name, string detail)
        {
            if (name.StartsWith("rocket") || name.StartsWith("grenade") || name.StartsWith("gas") || name == "player_hit" || name.StartsWith("shield") || name == "enemy_melee")
                Debug.Log($"[EnemyAiTests] event {name} {detail}");
        }

        [UnityTest]
        public IEnumerator Soldiers_SeeLucy_MoveToASpot_StopAndShoot_NeverBoth()
        {
            Assert.AreEqual(4, soldiers.Length, "four FSM soldiers");
            var start = soldiers.ToDictionary(s => s, s => s.transform.position);
            float maxMoved = 0f;
            float maxSpeed = 0f;
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
                    var b = s.GetComponent<InvectorEnemyBody>();
                    if (b != null) maxSpeed = Mathf.Max(maxSpeed, b.Speed);
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
            Debug.Log($"[EnemyAiTests] the fastest soldier moved at {maxSpeed:0.00} m/s");
            Assert.AreEqual(soldiers.Length, found.Count, "every soldier found Lucy");
            Assert.Greater(maxMoved, 1f, "a soldier walked to a firing spot");
            Assert.GreaterOrEqual(shots, 1, "a soldier shot with the telegraph");
            // Bruno, 2026-10-24: "the enemies move too slowly ... set them up to do their running anims".
            Assert.Greater(maxSpeed, 2.5f, "the soldiers run, not walk");
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
            // Since 2026-10-24 he falls limp (EnemyRagdoll) and stands up again first (RagdollTests).
            yield return WaitUntil(() => !RagdollBodies.IsDown(soldier), 8f);
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
        public IEnumerator Difficulty100_OnlyOneSoldierAimsAtATime()
        {
            DifficultyManager.Instance.SetPreset(100);
            Assert.AreEqual(1, ShooterScheduler.MaxSlots, "100 %: one shooter at a time");
            int maxAiming = 0, shots = 0;
            float t0 = Time.time;
            while (Time.time - t0 < 18f)
            {
                maxAiming = Mathf.Max(maxAiming, soldiers.Count(s => s != null && s.Gun.HoldsSlot));
                yield return null;
            }
            shots = soldiers.Sum(s => s.Gun.ShotsFired);
            DifficultyManager.Instance.SetPreset(DifficultyManager.DefaultPreset);
            Debug.Log($"[EnemyAiTests] easy: max aiming at once {maxAiming}, shots {shots}");
            Assert.AreEqual(1, maxAiming, "never two aims at once at 100 %");
            Assert.GreaterOrEqual(shots, 2, "they still shoot, one after the other");
        }

        [UnityTest]
        public IEnumerator Alert_AllFight_AndTheyKeepHerAsTarget_OutOfSight()
        {
            // Bruno, 2026-10-23: "on project warspite, the enemies weren't this dumb".
            yield return WaitUntil(() => soldiers.All(s => s.Alerted), 6f);
            Debug.Log("[EnemyAiTests] alert: " + string.Join(" ", soldiers.Select(s => $"{s.name}={s.AlertReason}")));
            Assert.IsTrue(soldiers.All(s => s.Alerted), "every soldier is alerted");

            // She goes to the south-east corner, behind the pillar from most spots.
            lucy.GetComponent<TankController>().Teleport(new Vector3(8.2f, 0.1f, -6.5f), 0f);
            yield return new WaitForSeconds(6f);
            foreach (SoldierBrain s in soldiers)
            {
                Assert.IsTrue(s.Alerted, s.name + " stays alerted");
                Assert.IsTrue(s.Body.HasTarget, s.name + " keeps her as its target out of sight");
                Assert.Less(Vector3.Distance(s.TargetPosition, lucy.transform.position), 0.6f, s.name + " knows where she is");
            }
        }

        [UnityTest]
        public IEnumerator Gun_InTheHand_Fires_WithFlashAndSound_AMissHitsTheWall()
        {
            foreach (SoldierBrain s in soldiers)
            {
                EnemyGun gun = s.GetComponent<EnemyGun>();
                Assert.IsNotNull(gun, s.name + " carries a gun");
                Assert.IsNotNull(gun.Model, s.name + " gun model");
                Assert.IsNotNull(gun.Muzzle, s.name + " gun muzzle");
            }
            yield return new WaitForSeconds(0.5f);
            foreach (SoldierBrain s in soldiers)
            {
                Transform hand = s.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
                Assert.Less(Vector3.Distance(hand.position, s.GetComponent<EnemyGun>().Model.position), 0.3f, s.name + ": the gun is in the right hand");
            }

            // A telegraph, then she steps aside: the bullet flies to the locked point and on into the wall.
            // A bullet weapon (the grenade has no wall impact).
            Func<SoldierBrain, bool> bulletTelegraph = s => s.Gun.Current == DummyShooter.State.Telegraph
                && (s.Gun.CurrentAttack == DummyShooter.Attack.Shot || s.Gun.CurrentAttack == DummyShooter.Attack.Burst);
            yield return WaitUntil(() => soldiers.Any(bulletTelegraph), 15f);
            SoldierBrain shooter = soldiers.First(bulletTelegraph);
            EnemyGun shooterGun = shooter.GetComponent<EnemyGun>();
            int shots = shooterGun.Shots;
            Vector3 p = lucy.transform.position;
            lucy.GetComponent<TankController>().Teleport(p + Vector3.right * 2.5f, 0f);
            yield return WaitUntil(() => shooterGun.Shots > shots, 3f);
            Assert.Greater(shooterGun.Shots, shots, "the gun fired");
            Assert.GreaterOrEqual(shooterGun.AimClicks, 1, "the aim click played");
            Assert.Less(Time.time - shooterGun.LastFireTime, 1f, "the flash and the sound played on the shot");
            Assert.IsTrue(shooter.Gun.Muzzle == shooterGun.Muzzle.position, "the bullet leaves the gun's muzzle");
            yield return WaitUntil(() => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t => t.name.StartsWith("impactConcrete")), 3f);
            Assert.IsTrue(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t => t.name.StartsWith("impactConcrete")), "the missed bullet left an impact on the wall");
        }

        // ── Bruno, 2026-10-24: the lock marker, the timing, the weapons, the icons, the melee ──

        [UnityTest]
        public IEnumerator LockMarker_FloatsOverThePowerArmoursHead()
        {
            SoldierBrain armour = Find("Armour_C");
            LockOnController lockOn = lucy.GetComponent<LockOnController>();
            lockOn.SetTarget(armour.GetComponent<LockOnTargetable>());
            yield return null;
            yield return null;
            float top = armour.GetComponentsInChildren<SkinnedMeshRenderer>().Max(r => r.bounds.max.y);
            Debug.Log($"[EnemyAiTests] armour top {top:0.00} m, marker at {lockOn.MarkerPosition.y:0.00} m");
            Assert.IsTrue(lockOn.MarkerVisible, "the marker shows");
            Assert.Greater(top, 2.3f, "the power armour is tall");
            Assert.Greater(lockOn.MarkerPosition.y, top, "the marker floats over its head, not inside it");
            Assert.Less(Vector3.Distance(Flat(lockOn.MarkerPosition), Flat(armour.transform.position)), 0.3f, "over the armour itself");
        }

        [UnityTest]
        public IEnumerator Telegraph_FollowsTheDifficulty_200At08_100At15()
        {
            try
            {
                DifficultyManager.Instance.SetPreset(200);
                Assert.AreEqual(0.8f, DifficultyManager.Instance.AttackSeconds, 0.001f, "200 %: 0.8 s");
                float hard = -1f;
                yield return MeasureAttack(v => hard = v);
                DifficultyManager.Instance.SetPreset(100);
                Assert.AreEqual(1.5f, DifficultyManager.Instance.AttackSeconds, 0.001f, "100 %: 1.5 s");
                float easy = -1f;
                yield return MeasureAttack(v => easy = v);
                Debug.Log($"[EnemyAiTests] laser to attack: hard {hard:0.00} s, easy {easy:0.00} s");
                Assert.AreEqual(0.8f, hard, 0.08f, "200 %: 0.8 s from the laser to the attack");
                Assert.AreEqual(1.5f, easy, 0.08f, "100 %: 1.5 s from the laser to the attack");
                Assert.AreEqual(2f, DifficultyManager.Instance.RestSeconds, 0.001f, "2 s rest");
            }
            finally
            {
                DifficultyManager.Instance.SetPreset(DifficultyManager.DefaultPreset);
            }
        }

        /// <summary>The seconds from the first new aim to its attack, on any soldier.</summary>
        private IEnumerator MeasureAttack(Action<float> result)
        {
            // Wait for every aim of the old preset to end, then catch a new one at its first frame.
            yield return WaitUntil(() => soldiers.All(s => !s.Gun.HoldsSlot), 8f);
            DummyShooter gun = null;
            yield return WaitUntil(() => (gun = soldiers.Select(s => s.Gun).FirstOrDefault(g => g.Current == DummyShooter.State.Aiming)) != null, 15f);
            Assert.IsNotNull(gun, "a soldier aims");
            float t0 = Time.time - Time.deltaTime;
            int attacks = gun.ShotsFired;
            yield return WaitUntil(() => gun.ShotsFired > attacks || !gun.HoldsSlot, 4f);
            Assert.Greater(gun.ShotsFired, attacks, "the aim ended in an attack");
            result(Time.time - t0);
        }

        [UnityTest]
        public IEnumerator Rifle_FiresABurstOfThreeRounds_AsOneAttack()
        {
            SoldierBrain rifleman = Find("Soldier_A");
            Assert.AreEqual(DummyShooter.Weapon.Rifle, rifleman.Gun.WeaponKind);
            int attacks = rifleman.Gun.ShotsFired, rounds = rifleman.Gun.RoundsFired;
            yield return WaitUntil(() => rifleman.Gun.ShotsFired > attacks && rifleman.Gun.CurrentAttack == DummyShooter.Attack.Burst && !rifleman.Gun.HoldsSlot, 20f);
            Debug.Log($"[EnemyAiTests] burst: attacks {rifleman.Gun.ShotsFired - attacks}, rounds {rifleman.Gun.RoundsFired - rounds}");
            Assert.AreEqual(attacks + 1, rifleman.Gun.ShotsFired, "one attack");
            Assert.AreEqual(rounds + 3, rifleman.Gun.RoundsFired, "three rounds");
            Assert.AreEqual(0, rifleman.MovedWhileShootingFrames, "it stood still for the whole burst");
        }

        [UnityTest]
        public IEnumerator Grenade_ArcsToHerFeet_TheRingWarns_TheBurstHurtsHer()
        {
            SoldierBrain grenadier = Find("Armour_C");
            Assert.AreEqual(DummyShooter.Weapon.ArmourLaunchers, grenadier.Gun.WeaponKind);
            Health health = lucy.GetComponentInChildren<Health>();
            EnemyGrenade grenade = null;
            yield return WaitUntil(() => (grenade = Frag()) != null, 30f);
            Assert.IsNotNull(grenade, "the power armour launched a grenade");
            Assert.IsNotNull(grenade.Ring, "the red ring shows the blast zone");
            Assert.Less(Vector3.Distance(Flat(grenade.Target), Flat(lucy.transform.position)), 0.6f, "it lands at her feet");
            health.SetInvulnerable(false);
            float hp = health.CurrentHealth;
            int bursts = EnemyGrenade.Bursts;
            // This grenade: a gas grenade's burst also counts in EnemyGrenade.Bursts.
            yield return WaitUntil(() => grenade == null, 4f);
            yield return null;
            float after = health.CurrentHealth;
            health.SetInvulnerable(true);
            health.SetHealth(health.MaxHealth);
            Assert.Greater(EnemyGrenade.Bursts, bursts, "it burst");
            Assert.Less(after, hp, "the burst hurt her: she stood in the ring");
            Assert.IsTrue(grenade == null, "the grenade and its ring are gone");
        }

        [UnityTest]
        public IEnumerator Grenade_CaughtInTheAir_FliesBack_AndBurstsOnTheSoldiers()
        {
            SoldierBrain grenadier = Find("Armour_C");
            Health armourHealth = grenadier.GetComponent<Health>();
            // The catch takes the nearest grenade: a gas grenade near her would be the one (the run of 02:30).
            Health gasser = Find("Gasser_D").GetComponent<Health>();
            gasser.TakeDamage(gasser.MaxHealth * 10f);
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            powers.Select(PowerKind.Telekinesis);
            EnemyGrenade grenade = null;
            yield return WaitUntil(() => (grenade = Frag()) != null, 30f);
            Assert.IsNotNull(grenade, "a grenade in the air");
            yield return WaitUntil(() => grenade == null || Vector3.Distance(grenade.transform.position, lucy.transform.position + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight) < PsychicPowers.CatchRadius - 0.2f, 3f);
            stats.RegenPaused = true;
            stats.DebugSet(PsychicStats.MaxNalcon * 0.45f, 0f);
            float mk = stats.Nalcon;
            float hp = armourHealth.CurrentHealth;
            Assert.IsTrue(powers.TryCatchBullet(), "the defend press caught the grenade");
            Assert.AreEqual("catch_grenade", powers.LastResult);
            Assert.IsTrue(grenade.Returned, "it flies back");
            Assert.AreEqual(mk - PsychicPowers.GrenadeCatchNalconCost, stats.Nalcon, 0.5f, "the catch costs MK");
            int bursts = EnemyGrenade.Bursts;
            yield return WaitUntil(() => EnemyGrenade.Bursts > bursts, 3f);
            yield return null;
            Debug.Log($"[EnemyAiTests] returned grenade: armour HP {hp:0} -> {armourHealth.CurrentHealth:0}");
            stats.RegenPaused = false;
            Assert.Less(armourHealth.CurrentHealth, hp, "the returned grenade hurt the power armour");
        }

        [UnityTest]
        public IEnumerator Shield_BlocksTheFrontBlast_NotTheBack_AndTheLiftTearsItAway()
        {
            SoldierBrain guard = Find("Guard_B");
            EnemyShield shield = guard.GetComponent<EnemyShield>();
            Assert.IsNotNull(shield, "the guard carries a shield");
            Assert.IsNotNull(shield.Model, "the shield model");
            Health health = guard.GetComponent<Health>();
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            TankController tank = lucy.GetComponent<TankController>();
            LockOnTargetable target = guard.GetComponent<LockOnTargetable>();
            yield return WaitUntil(() => guard.Alerted, 6f);
            powers.Select(PowerKind.Blast);

            // In front of him: blocked, no damage.
            Vector3 front = guard.transform.position + guard.transform.forward * 4f;
            tank.Teleport(new Vector3(front.x, lucy.transform.position.y, front.z), 0f);
            float hp = health.CurrentHealth;
            powers.FireAt(powers.Selected, 0.3f, target, 1f);
            Assert.AreEqual("blocked", powers.LastResult, "the shield stops mk_warspite from the front");
            Assert.AreEqual(hp, health.CurrentHealth, "no damage through the shield");
            Assert.AreEqual(1, shield.BlockCount);

            // Behind him: the blast lands.
            Vector3 back = guard.transform.position - guard.transform.forward * 4f;
            tank.Teleport(new Vector3(back.x, lucy.transform.position.y, back.z), 0f);
            powers.FireAt(powers.Selected, 0.3f, target, 1f);
            Assert.Less(health.CurrentHealth, hp, "the back is open");
            yield return new WaitForSeconds(1.2f);

            // mk_orzabal still lifts him, and the shield falls away.
            powers.Select(PowerKind.Telekinesis);
            powers.FireAt(powers.Selected, 1f, target, 1f);
            yield return new WaitForSeconds(0.3f);
            Assert.IsNotNull(guard.GetComponent<TelekinesisLift>(), "the lift took him");
            Assert.IsTrue(shield.Dropped, "the lift tore the shield away");
            Assert.IsFalse(shield.Blocks(lucy.transform.position), "no guard without the shield");
        }

        [UnityTest]
        public IEnumerator Melee_WhenSheIsClose_TheSoldierStrikes_WithTheTelegraph()
        {
            SoldierBrain soldier = Find("Soldier_A");
            Health health = lucy.GetComponentInChildren<Health>();
            yield return WaitUntil(() => soldier.Alerted && !soldier.Gun.HoldsSlot, 10f);
            Vector3 close = soldier.transform.position + soldier.transform.forward * 1.2f;
            lucy.GetComponent<TankController>().Teleport(new Vector3(close.x, lucy.transform.position.y, close.z), 0f);
            yield return WaitUntil(() => soldier.Gun.HoldsSlot && soldier.Gun.CurrentAttack == DummyShooter.Attack.Melee, 6f);
            Assert.AreEqual(DummyShooter.Attack.Melee, soldier.Gun.CurrentAttack, "close to her, it picks a strike");
            PlayerHud hud = UnityEngine.Object.FindFirstObjectByType<PlayerHud>();
            yield return WaitUntil(() => hud != null && hud.ShownAttacks.Contains(DummyShooter.Attack.Melee), 1f);
            Assert.IsTrue(hud != null && hud.ShownAttacks.Contains(DummyShooter.Attack.Melee), "the HUD shows the melee icon");
            health.SetInvulnerable(false);
            float hp = health.CurrentHealth;
            int strikes = soldier.Gun.MeleeStrikes;
            yield return WaitUntil(() => soldier.Gun.MeleeStrikes > strikes, 3f);
            float after = health.CurrentHealth;
            health.SetInvulnerable(true);
            health.SetHealth(health.MaxHealth);
            Assert.Greater(soldier.Gun.MeleeStrikes, strikes, "it struck after the telegraph");
            Assert.Less(after, hp, "the strike hurt her");
            Assert.AreEqual(0, soldier.MovedWhileShootingFrames, "it stood still for the strike");
        }

        [UnityTest]
        public IEnumerator Hud_ShowsTheAttackIcon_OfEveryAim()
        {
            PlayerHud hud = UnityEngine.Object.FindFirstObjectByType<PlayerHud>();
            Assert.IsNotNull(hud, "the HUD");
            var seen = new System.Collections.Generic.HashSet<DummyShooter.Attack>();
            float t0 = Time.time;
            while (Time.time - t0 < 25f && seen.Count < 3)
            {
                foreach (SoldierBrain s in soldiers)
                {
                    if (s.Gun.Current != DummyShooter.State.Telegraph) continue;
                    Assert.Contains(s.Gun.CurrentAttack, hud.ShownAttacks, $"{s.name}: the HUD shows its {s.Gun.CurrentAttack} icon");
                    Assert.IsNotNull(PlayerHud.IconFor(hud, s.Gun.CurrentAttack));
                    seen.Add(s.Gun.CurrentAttack);
                }
                yield return null;
            }
            Debug.Log("[EnemyAiTests] icons seen: " + string.Join(",", seen));
            Assert.GreaterOrEqual(seen.Count, 2, "at least two kinds of attack showed their icon");
        }

        /// <summary>A frag grenade in the air, not gas.</summary>
        private static EnemyGrenade Frag() => UnityEngine.Object.FindObjectsByType<EnemyGrenade>(FindObjectsSortMode.None).FirstOrDefault(g => !g.IsGas && g.Catchable);

        [UnityTest]
        public IEnumerator PowerArmour_FiresFragGrenadesAndRockets_OneAfterTheOther()
        {
            SoldierBrain armour = Find("Armour_C");
            var kinds = new System.Collections.Generic.List<DummyShooter.Attack>();
            int attacks = armour.Gun.ShotsFired;
            float t0 = Time.time;
            while (Time.time - t0 < 40f && kinds.Count < 2)
            {
                if (armour.Gun.ShotsFired > attacks)
                {
                    attacks = armour.Gun.ShotsFired;
                    kinds.Add(armour.Gun.CurrentAttack);
                }
                yield return null;
            }
            Debug.Log("[EnemyAiTests] armour attacks: " + string.Join(",", kinds));
            Assert.AreEqual(2, kinds.Count, "two attacks");
            CollectionAssert.AreEquivalent(new[] { DummyShooter.Attack.Grenade, DummyShooter.Attack.Rocket }, kinds, "a frag grenade and a rocket");
        }

        [UnityTest]
        public IEnumerator Rocket_CaughtInFlight_FliesBack_AndBurstsOnTheArmour()
        {
            SoldierBrain armour = Find("Armour_C");
            Health armourHealth = armour.GetComponent<Health>();
            PsychicPowers powers = lucy.GetComponent<PsychicPowers>();
            PsychicStats stats = lucy.GetComponent<PsychicStats>();
            powers.Select(PowerKind.Telekinesis);
            Vector3 Chest() => lucy.transform.position + Vector3.up * GlorifiedBody.Characters.Body.ChestHeight;
            EnemyRocket rocket = null;
            yield return WaitUntil(() => (rocket = UnityEngine.Object.FindObjectsByType<EnemyRocket>(FindObjectsSortMode.None).FirstOrDefault(r => r.Catchable)) != null, 40f);
            Assert.IsNotNull(rocket, "the power armour fired a rocket");
            yield return WaitUntil(() => rocket == null || Vector3.Distance(rocket.transform.position, Chest()) < PsychicPowers.CatchRadius - 0.3f, 3f);
            Assert.IsTrue(rocket != null, "the rocket reached the catch radius");
            stats.RegenPaused = true;
            stats.DebugSet(PsychicStats.MaxNalcon * 0.45f, 0f);
            float mk = stats.Nalcon;
            float hp = armourHealth.CurrentHealth;
            Debug.Log($"[EnemyAiTests] rocket caught at {rocket.transform.position}, armour at {armour.transform.position}, her at {lucy.transform.position}");
            Assert.IsTrue(powers.TryCatchBullet(), "the defend press caught the rocket");
            Assert.AreEqual("catch_rocket", powers.LastResult);
            Assert.AreEqual(mk - PsychicPowers.RocketCatchNalconCost, stats.Nalcon, 0.5f, "a rocket costs 25 MK");
            int bursts = EnemyRocket.Bursts;
            yield return WaitUntil(() => EnemyRocket.Bursts > bursts, 3f);
            yield return null;
            stats.RegenPaused = false;
            Debug.Log($"[EnemyAiTests] returned rocket: armour HP {hp:0} -> {armourHealth.CurrentHealth:0}");
            Assert.Less(armourHealth.CurrentHealth, hp, "the returned rocket hurt the power armour");
        }

        [UnityTest]
        public IEnumerator Gas_ThePoisonCloudHurtsHer_NotTheMaskedSoldiers()
        {
            SoldierBrain gasser = Find("Gasser_D");
            Assert.AreEqual(DummyShooter.Weapon.GasLauncher, gasser.Gun.WeaponKind);
            Health health = lucy.GetComponentInChildren<Health>();
            int clouds = GasCloud.Clouds;
            EnemyGrenade gas = null;
            yield return WaitUntil(() => (gas = UnityEngine.Object.FindObjectsByType<EnemyGrenade>(FindObjectsSortMode.None).FirstOrDefault(g => g.IsGas)) != null, 30f);
            Assert.IsNotNull(gas, "the gas grenadier launched gas");
            yield return WaitUntil(() => GasCloud.Clouds > clouds, 4f);
            GasCloud cloud = UnityEngine.Object.FindFirstObjectByType<GasCloud>();
            Assert.IsNotNull(cloud, "the cloud");
            // She stands in the cloud for 1 s; a soldier stands in it too.
            health.SetInvulnerable(false);
            float hp = health.CurrentHealth;
            lucy.GetComponent<TankController>().Teleport(cloud.transform.position, 0f);
            SoldierBrain masked = soldiers.First(s => s != gasser && !s.Body.IsDead);
            Health maskedHealth = masked.GetComponent<Health>();
            float maskedHp = maskedHealth.CurrentHealth;
            yield return new WaitForSeconds(1.0f);
            float after = health.CurrentHealth;
            bool maskedInside = cloud != null && cloud.Inside(masked.transform.position);
            health.SetInvulnerable(true);
            health.SetHealth(health.MaxHealth);
            Debug.Log($"[EnemyAiTests] gas: her HP {hp:0.0} -> {after:0.0}, dealt {(cloud != null ? cloud.DamageDealt : -1):0.0}");
            Assert.Less(after, hp - 3f, "the poison hurt her (about 6 per second)");
            Assert.AreEqual(maskedHp, maskedHealth.CurrentHealth, "no soldier takes gas damage" + (maskedInside ? " (it stood inside)" : ""));
        }

        [UnityTest]
        public IEnumerator Gas_TheCloudIsVisible_AndGreen()
        {
            GasCloud cloud = GasCloud.Spawn(new Vector3(-6.5f, 0f, 3.5f), 2.5f, 4f, 0f, "test");
            yield return new WaitForSeconds(1.2f);
            int particles = cloud.GetComponentsInChildren<ParticleSystem>().Sum(ps => ps.particleCount);
            var go = new GameObject("GasCheckCamera");
            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            go.transform.position = cloud.transform.position + new Vector3(4.5f, 1.4f, -1.3f);
            go.transform.LookAt(cloud.transform.position + Vector3.up);
            var rt = new RenderTexture(320, 180, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(320, 180, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 320, 180), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            int green = tex.GetPixels().Count(c => c.g > 0.25f && c.g > c.r * 1.15f && c.g > c.b * 1.3f);
            UnityEngine.Object.Destroy(go);
            Debug.Log($"[EnemyAiTests] gas: {particles} particles, {green} green pixels of {320 * 180}");
            Assert.Greater(particles, 10, "the cloud emits smoke");
            Assert.Greater(green, 320 * 180 / 50, "the smoke shows green (2 percent of the frame or more)");
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

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
