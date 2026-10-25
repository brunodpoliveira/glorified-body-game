#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using GlorifiedBody.Automation;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// Build order step 3: the circle charge and the three powers, the bars, the short, the drugs.
    /// The shooter is off here (DummyShooterTests covers it). Batch: tools/unity.sh test playmode.
    /// </summary>
    public class PowersAndBarsTests
    {
        private const string MainMenu = "Main_Menu";
        private const string Room = "TrainingRoom";

        [DefaultExecutionOrder(-1000)]
        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad pad;
            private void Update() => pad?.Commit();
        }

        private VirtualPad pad;
        private PadDriver driver;
        private GameObject player;
        private TankController tank;
        private LockOnController lockOn;
        private Health health;
        private PsychicStats stats;
        private PsychicPowers powers;
        private DrugInventory drugs;
        private ShortController shortController;
        private LockOnTargetable dummyA;
        private LockOnTargetable dummyB;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainMenu);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == MainMenu && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadGameplay();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Room && GameManager.Instance.IsPlaying, 10f);
            yield return null;

            foreach (DummyShooter shooter in UnityEngine.Object.FindObjectsByType<DummyShooter>(FindObjectsSortMode.None)) shooter.enabled = false;

            player = GameObject.FindGameObjectWithTag("Player");
            tank = player.GetComponent<TankController>();
            lockOn = player.GetComponent<LockOnController>();
            health = player.GetComponentInChildren<Health>();
            stats = player.GetComponent<PsychicStats>();
            powers = player.GetComponent<PsychicPowers>();
            drugs = player.GetComponent<DrugInventory>();
            shortController = player.GetComponent<ShortController>();
            Assert.IsNotNull(stats, "PsychicStats");
            Assert.IsNotNull(powers, "PsychicPowers");
            Assert.AreEqual(3, powers.Powers.Count, "three powers loaded from Resources/Powers");
            dummyA = GameObject.Find("Dummy_A").GetComponent<LockOnTargetable>();
            dummyB = GameObject.Find("Dummy_B").GetComponent<LockOnTargetable>();

            pad = new VirtualPad();
            driver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            driver.pad = pad;
            GameInput.SetBotSource(pad);
            tank.Teleport(new Vector3(0f, 1f, 0f), 0f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSeconds(0.2f);
            yield return WaitUntil(() => lockOn.HasTarget, 1.5f);
            Assert.AreEqual("Dummy_A", lockOn.TargetName, "the nearest dummy is the target");
            // The base numbers: under half a bar, no MK boost, and no regen to cross the line during a test.
            stats.RegenPaused = true;
            stats.DebugSet(45f, 0f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (pad != null)
            {
                pad.ReleaseAll();
                GameInput.ClearBotSource(pad);
            }
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullCharge_FiresBlast_FullDamage_FullCost()
        {
            powers.Select(PowerKind.Blast);
            PowerDefinition blast = powers.Selected;
            Health target = dummyA.GetComponent<Health>();
            float hp0 = target.CurrentHealth;
            stats.DebugSet(45f, 0f); // under the MK boost line

            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(blast.fullChargeTime + 0.3f);
            Assert.AreEqual(1f, powers.Charge, 0.01f, "the circle is full");
            float nalconBefore = stats.Nalcon;
            pad.ReleaseAll();
            yield return null;
            yield return null;
            yield return null;

            Assert.AreEqual("fired", powers.LastResult);
            Assert.AreEqual(hp0 - blast.baseDamage, target.CurrentHealth, 0.5f, "full damage");
            Assert.AreEqual(nalconBefore - blast.nalconCost, stats.Nalcon, 1.0f, "full cost (regen tolerance)");
            Assert.AreEqual(0f, powers.Charge, "the circle resets");
        }

        [UnityTest]
        public IEnumerator HalfCharge_HalfDamage_HalfCost()
        {
            powers.Select(PowerKind.Blast);
            PowerDefinition blast = powers.Selected;
            Health target = dummyA.GetComponent<Health>();
            float hp0 = target.CurrentHealth;
            stats.DebugSet(45f, 0f); // under the MK boost line

            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(blast.fullChargeTime * 0.5f);
            float charge = powers.Charge;
            float nalconBefore = stats.Nalcon;
            pad.ReleaseAll();
            yield return null;
            yield return null;

            Assert.That(charge, Is.InRange(0.35f, 0.65f), "about half a circle");
            Assert.AreEqual(hp0 - blast.baseDamage * charge, target.CurrentHealth, 0.5f, "damage scales with the charge");
            Assert.AreEqual(nalconBefore - blast.nalconCost * charge, stats.Nalcon, 1.0f, "cost scales with the charge");
        }

        [UnityTest]
        public IEnumerator Tap_FizzlesBelowMinCharge_NoCost()
        {
            powers.Select(PowerKind.Blast);
            stats.DebugSet(100f, 0f);
            Health target = dummyA.GetComponent<Health>();
            float hp0 = target.CurrentHealth;
            pad.Tap(InputManager.GameAction.Charge);
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual("fizzle", powers.LastResult);
            Assert.AreEqual(hp0, target.CurrentHealth, "no damage");
            Assert.GreaterOrEqual(stats.Nalcon, 99f, "no cost");
        }

        [UnityTest]
        public IEnumerator NoNalcon_NoFire()
        {
            powers.Select(PowerKind.Blast);
            stats.SetOutfit(Resources.Load<OutfitProfile>("Outfits/outfit_guard"));
            stats.DebugSet(5f, 0f);
            Health target = dummyA.GetComponent<Health>();
            float hp0 = target.CurrentHealth;
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(powers.Selected.fullChargeTime + 0.2f);
            pad.ReleaseAll();
            yield return null;
            yield return null;
            Assert.AreEqual("no_nalcon", powers.LastResult);
            Assert.AreEqual(hp0, target.CurrentHealth, "no damage without nalcon");
        }

        [UnityTest]
        public IEnumerator Fire_Burns_OverTime()
        {
            powers.Select(PowerKind.Fire);
            PowerDefinition fire = powers.Selected;
            Health target = dummyB.GetComponent<Health>();
            float hp0 = target.CurrentHealth;
            powers.FireAt(fire, 1f, dummyB);
            yield return null;
            Assert.IsNotNull(target.GetComponent<BurningStatus>(), "the target burns");
            Assert.AreEqual(hp0 - fire.baseDamage * 0.5f, target.CurrentHealth, 0.5f, "the initial hit");
            yield return new WaitForSeconds(2.1f);
            float expected = hp0 - fire.baseDamage * 0.5f - fire.burnPerSecond * 2f;
            Assert.AreEqual(expected, target.CurrentHealth, fire.burnPerSecond * 0.6f, "the burn ticks for 2 s");
        }

        [UnityTest]
        public IEnumerator Telekinesis_Holds_UntilAFullChargeSlamsIt()
        {
            // Bruno, 2026-10-22: the floater stays up until a throw, a slam (a full charge on it) or another enemy's hit.
            powers.Select(PowerKind.Telekinesis);
            PowerDefinition tk = powers.Selected;
            Health target = dummyB.GetComponent<Health>();
            float hp0 = target.CurrentHealth;
            float y0 = dummyB.transform.position.y;
            powers.FireAt(tk, 1f, dummyB);
            yield return new WaitForSeconds(tk.liftSeconds + tk.holdSeconds + 1.5f);
            Assert.Greater(dummyB.transform.position.y, y0 + tk.liftHeight * 0.8f, "still up after the old 1.5 s window");
            Assert.IsNotNull(powers.HeldLift, "she still holds it");
            Assert.AreEqual(hp0, target.CurrentHealth, "no damage while in the air");

            float nalconBefore = stats.Nalcon;
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(tk.fullChargeTime + 0.2f);
            Assert.AreEqual(1f, powers.Charge, 0.01f, "a full circle");
            pad.ReleaseAll();
            yield return null;
            yield return null;
            Assert.AreEqual("slam", powers.LastResult, "a full charge on the floater slams it");
            yield return WaitUntil(() => dummyB.GetComponent<TelekinesisLift>() == null, 2f);
            Assert.AreEqual(y0, dummyB.transform.position.y, 0.05f, "back on the floor");
            Assert.AreEqual(hp0 - tk.baseDamage, target.CurrentHealth, 0.5f, "the slam damage");
            Assert.AreEqual(nalconBefore - tk.nalconCost, stats.Nalcon, 0.5f, "the slam costs a full charge");
        }

        [UnityTest]
        public IEnumerator Telekinesis_AnotherEnemysBullet_DropsTheFloater_Unhurt()
        {
            powers.Select(PowerKind.Telekinesis);
            PowerDefinition tk = powers.Selected;
            Health target = dummyB.GetComponent<Health>();
            float hp0 = target.CurrentHealth;
            float y0 = dummyB.transform.position.y;
            powers.FireAt(tk, 1f, dummyB);
            yield return new WaitForSeconds(tk.liftSeconds + 0.3f);
            TelekinesisLift lift = dummyB.GetComponent<TelekinesisLift>();
            Assert.IsNotNull(lift, "it floats");

            Vector3 origin = dummyA.transform.position + Vector3.up * 1.3f;
            Vector3 aim = dummyB.transform.position + Vector3.up * 1.0f;
            EnemyProjectile.Launch(origin, aim - origin, 20f, 15f, dummyA.gameObject, "test_bullet");
            yield return WaitUntil(() => lift == null || lift.Ending == "released", 2f);
            Assert.IsTrue(lift == null || lift.Ending == "released", "the enemy's bullet releases it");
            yield return WaitUntil(() => dummyB.GetComponent<TelekinesisLift>() == null, 2f);
            Assert.AreEqual(y0, dummyB.transform.position.y, 0.05f, "it dropped to the floor");
            Assert.AreEqual(hp0, target.CurrentHealth, 0.01f, "released with no damage");
            Assert.IsNull(powers.HeldLift, "she holds nothing");
        }

        [UnityTest]
        public IEnumerator Props_TheAutoLockSkipsThem_TheSwitchReachesThemOnlyWithMkOrzabal()
        {
            // Bruno, 2026-10-22: mk_orzabal lifts the enemy directly; the props are a choice, not the default.
            LockOnTargetable crate = GameObject.Find("Prop_Crate_1").GetComponent<LockOnTargetable>();
            Assert.IsTrue(crate.IsProp, "the crate is a prop");
            Material material = crate.GetComponent<Renderer>().sharedMaterial;
            Assert.AreEqual("Universal Render Pipeline/Unlit", material.shader.name, "a throwable object is unlit (Bruno, 2026-10-22)");
            Assert.IsNotNull(material.GetTexture("_BaseMap"), "the crate face texture");
            Assert.Less(Vector3.Distance(player.transform.position, crate.GetLockPoint()),
                Vector3.Distance(player.transform.position, dummyA.GetLockPoint()), "the crate is nearer than the dummy");
            Assert.AreEqual("Dummy_A", lockOn.TargetName, "the auto-lock took the dummy, not the nearer crate");

            powers.Select(PowerKind.Blast);
            for (int i = 0; i < 4; i++)
            {
                pad.Tap(InputManager.GameAction.SwitchTarget);
                yield return null;
                yield return null;
                Assert.IsFalse(lockOn.CurrentTarget != null && lockOn.CurrentTarget.IsProp, "mk_warspite never locks a prop: " + lockOn.TargetName);
            }

            powers.Select(PowerKind.Telekinesis);
            bool reached = false;
            for (int i = 0; i < 6 && !reached; i++)
            {
                pad.Tap(InputManager.GameAction.SwitchTarget);
                yield return null;
                yield return null;
                reached = lockOn.CurrentTarget != null && lockOn.CurrentTarget.IsProp;
            }
            Assert.IsTrue(reached, "with mk_orzabal the switch reaches a crate");

            powers.Select(PowerKind.Blast);
            yield return null;
            yield return null;
            Assert.IsFalse(lockOn.CurrentTarget != null && lockOn.CurrentTarget.IsProp, "another power drops the prop lock");
            Assert.IsTrue(lockOn.HasTarget, "the auto-lock takes an enemy again");
        }

        [UnityTest]
        public IEnumerator Props_LiftAndThrow_TheCrateHurtsTheEnemy_Breaks_AndTheLockGoesBack()
        {
            powers.Select(PowerKind.Telekinesis);
            PowerDefinition tk = powers.Selected;
            LockOnTargetable crate = GameObject.Find("Prop_Crate_1").GetComponent<LockOnTargetable>();
            float y0 = crate.transform.position.y;
            Health target = dummyA.GetComponent<Health>();
            float hp0 = target.CurrentHealth;

            lockOn.SetTarget(crate);
            float nalconBefore = stats.Nalcon;
            Assert.IsTrue(powers.Release(1f), "the lift on the crate");
            Assert.AreEqual(nalconBefore - tk.nalconCost, stats.Nalcon, 0.5f, "the lift costs as on an enemy");
            yield return new WaitForSeconds(tk.liftSeconds + 0.3f);
            Assert.IsNotNull(powers.HeldLift, "she holds the crate");
            Assert.Greater(crate.transform.position.y, y0 + tk.liftHeight * 0.8f, "the crate is up");

            pad.Tap(InputManager.GameAction.Charge); // a short press throws
            yield return null;
            yield return null;
            yield return null;
            StringAssert.StartsWith("launch_prop:Dummy_A", powers.LastResult, "the crate flies at the dummy");
            yield return WaitUntil(() => crate == null, 3f);
            Assert.IsTrue(crate == null, "a thrown crate breaks: single use (Bruno, 2026-10-23)");
            Assert.AreEqual(hp0 - tk.baseDamage, target.CurrentHealth, 0.5f, "the crate hurts the dummy");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<PropPiece>(), "the pieces of the crate");
            Assert.IsTrue(lockOn.HasTarget && !lockOn.CurrentTarget.IsProp, "the auto-lock takes an enemy again: " + lockOn.TargetName);
        }

        [UnityTest]
        public IEnumerator Telekinesis_RefusesAHeavyTarget_AtNoCost_AndTheHudSaysSo()
        {
            // Bruno, 2026-10-22: "maybe the power armor can't be lifted" (PROPOSED; the power armour arrives with the FSM AI).
            dummyB.SetHeavy(true);
            powers.Select(PowerKind.Telekinesis);
            lockOn.SetTarget(dummyB);
            Health target = dummyB.GetComponent<Health>();
            float hp0 = target.CurrentHealth;
            float nalconBefore = stats.Nalcon;
            Assert.IsFalse(powers.Release(1f), "no lift");
            Assert.AreEqual("too_heavy", powers.LastResult);
            Assert.AreEqual(nalconBefore, stats.Nalcon, 0.01f, "no cost");
            Assert.IsNull(dummyB.GetComponent<TelekinesisLift>(), "it stays on the floor");
            Assert.AreEqual(hp0, target.CurrentHealth, 0.01f, "no damage");
            yield return null;
            PlayerHud hud = UnityEngine.Object.FindFirstObjectByType<PlayerHud>();
            Assert.AreEqual("TOO HEAVY", hud.NoticeShown, "the HUD says why");
            yield return new WaitForSeconds(PlayerHud.NoticeSeconds + 0.2f);
            Assert.AreEqual("", hud.NoticeShown, "the notice goes");

            powers.Select(PowerKind.Blast);
            lockOn.SetTarget(dummyB);
            Assert.IsTrue(powers.Release(1f), "the other powers still hit it");
            dummyB.SetHeavy(false);
        }

        [UnityTest]
        public IEnumerator Short_ShocksOneEnemyAtATime_TheNearestFirst_ThenTheNext()
        {
            // Bruno, 2026-10-22 (design §11.4): one shock, one enemy, the others watch. Before, every shock hit
            // every enemy in the radius for the same damage (his report of 2026-10-23).
            Health a = dummyA.GetComponent<Health>();
            Health b = dummyB.GetComponent<Health>();
            Assert.Less(Vector3.Distance(player.transform.position, a.transform.position),
                Vector3.Distance(player.transform.position, b.transform.position), "Dummy_A is the nearer one");
            Assert.Less(Vector3.Distance(player.transform.position, b.transform.position), 6f, "both in the shock radius");
            float hpA = a.CurrentHealth, hpB = b.CurrentHealth;

            shortController.Begin();
            yield return new WaitForSeconds(1.0f);
            Assert.AreEqual(a, shortController.Victim, "the shocks hit the nearest enemy");
            Assert.Less(a.CurrentHealth, hpA, "Dummy_A takes the shocks");
            Assert.AreEqual(hpB, b.CurrentHealth, 0.01f, "Dummy_B only watches");

            yield return WaitUntil(() => a == null || a.IsDead, 4f);
            Assert.IsTrue(a == null || a.IsDead, "the shocks kill Dummy_A");
            yield return new WaitForSeconds(1.0f);
            Assert.AreEqual(b, shortController.Victim, "then the shocks move to the next enemy");
            Assert.Less(b.CurrentHealth, hpB, "Dummy_B takes the shocks now");
            shortController.End(byDrug: false);
        }

        [UnityTest]
        public IEnumerator ShortStart_FreezesTheWorld_2_5s_LucyHoldsHerHead_AndScreams_ThenTheShocks()
        {
            // Bruno, 2026-10-25: "it should be 2.5 s; the world freezes as the main character plays an anim of pain on
            // the head; the player char will also play a scream sound. the shock is their power going out of their
            // control".
            Assert.AreEqual(2.5f, ShortController.StartFreeze, 0.001f, "2.5 s");
            int before = shortController.StartPains;
            int screams = shortController.Screams;
            shortController.Begin();
            yield return null;
            Assert.AreEqual(before + 1, shortController.StartPains, "one start, one pain");
            Assert.AreEqual(screams + 1, shortController.Screams, "she screams");
            Assert.AreEqual(0f, Time.timeScale, 0.001f, "the world freezes at the start");
            var pose = player.GetComponentInChildren<GlorifiedBody.Characters.PoseOverride>();
            var clips = GlorifiedBody.Characters.PoseClips.Load();
            Assert.IsNotNull(pose, "Lucy has the pose player");
            Assert.IsTrue(pose.Playing, "the pain plays during the freeze");
            Assert.AreEqual(clips.lucyPain, pose.Current, "the head pain clip (frustrated_02)");
            yield return new WaitForSecondsRealtime(2.0f);
            Assert.AreEqual(0f, Time.timeScale, 0.001f, "still frozen at 2 s");
            Assert.IsTrue(pose.Playing, "the pain goes on through the freeze (unscaled time)");
            Assert.That(pose.Time, Is.InRange(GlorifiedBody.Characters.PoseClips.PainFrom + 1.6, GlorifiedBody.Characters.PoseClips.PainFrom + 2.5), "the head-holding part of the clip");
            Assert.AreEqual(0, shortController.ShocksFired, "no shock during the pain");
            Assert.IsTrue(shortController.InPain);
            float t0 = Time.realtimeSinceStartup;
            while (Time.timeScale < 0.999f && Time.realtimeSinceStartup - t0 < 3f) yield return null;
            Debug.Log($"[PowersAndBarsTests] the freeze lasted {Time.realtimeSinceStartup - t0 + 2.0f:0.00} s");
            Assert.AreEqual(1f, Time.timeScale, 0.001f, "the world runs again");
            Assert.IsFalse(pose.Playing, "the pain clip ended: the controller has her back");
            yield return new WaitForSeconds(1f);
            Assert.Greater(shortController.ShocksFired, 0, "then the power goes out of control");
            Assert.AreEqual(before + 1, shortController.StartPains, "the shocks freeze nothing");
            Assert.AreEqual(1f, Time.timeScale, 0.001f, "no freeze at a shock");
            shortController.End(byDrug: false);
        }

        [UnityTest]
        public IEnumerator Short_EachShock_ArcsToTheVictim_Zaps_AndStunsIt()
        {
            // Bruno, 2026-10-25: "a shock sfx from the player going onto the enemy/entity it is damaging (as of now it
            // is not clear who the shock is damaging)"; "the shock should also stun the victim".
            DummyShooter gun = dummyA.GetComponent<DummyShooter>();
            shortController.Begin();
            yield return new WaitForSecondsRealtime(ShortController.StartFreeze + 0.3f); // the start's freeze, then 0.2 s back
            yield return new WaitForSeconds(0.3f); // Dummy_A has 50 HP: 12 a shock, so the checks stay under 4 shocks
            Assert.AreEqual(dummyA.GetComponent<Health>(), shortController.Victim, "Dummy_A is the victim");
            Assert.Greater(shortController.Arcs, 0, "an arc for each shock");
            Assert.Greater(shortController.Zaps, 0, "a zap for each shock");
            Assert.AreEqual(DummyShooter.State.Staggered, gun.Current, "the victim is stunned");
            int arcs = shortController.Arcs;
            yield return new WaitForSeconds(shortController.StunSeconds - 0.15f);
            Assert.Greater(shortController.Arcs, arcs, "the next shock");
            Assert.AreEqual(DummyShooter.State.Staggered, gun.Current, "the stun holds from shock to shock");
            shortController.End(byDrug: false);
        }

        [UnityTest]
        public IEnumerator Short_PassesAShockImmuneTarget()
        {
            // Bruno, 2026-10-23: the bosses are heavy and immune to the shocks.
            dummyA.SetShockImmune(true);
            Health a = dummyA.GetComponent<Health>();
            Health b = dummyB.GetComponent<Health>();
            float hpA = a.CurrentHealth, hpB = b.CurrentHealth;
            shortController.Begin();
            yield return new WaitForSeconds(1.0f);
            Assert.AreEqual(hpA, a.CurrentHealth, 0.01f, "the immune dummy takes no shock");
            Assert.AreEqual(b, shortController.Victim, "the shocks go to the next enemy");
            Assert.Less(b.CurrentHealth, hpB, "Dummy_B takes the shocks");
            shortController.End(byDrug: false);
            dummyA.SetShockImmune(false);
        }

        [UnityTest]
        public IEnumerator MkBoost_AtHalfABarOrMore_DoublesTheDamage_AndTheChargeSpeed()
        {
            powers.Select(PowerKind.Blast);
            PowerDefinition blast = powers.Selected;
            Health target = dummyA.GetComponent<Health>();
            PlayerHud hud = UnityEngine.Object.FindFirstObjectByType<PlayerHud>();

            stats.DebugSet(45f, 0f);
            yield return null;
            Assert.IsFalse(powers.IsBoosted, "under half a bar: no boost");
            Assert.IsFalse(hud.BoostShown, "no x2 on the HUD");

            stats.DebugSet(100f, 0f);
            yield return null;
            Assert.IsTrue(powers.IsBoosted, "a full bar: the boost");
            Assert.IsTrue(hud.BoostShown, "x2 on the HUD");
            float hp0 = target.CurrentHealth;
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(blast.fullChargeTime * 0.5f + 0.15f);
            Assert.AreEqual(1f, powers.Charge, 0.01f, "the circle fills in half the time");
            pad.ReleaseAll();
            yield return null;
            yield return null;
            Assert.AreEqual("fired", powers.LastResult);
            Assert.AreEqual(2f, powers.LastScale, 0.01f, "the boost doubles the damage");
            float dealt = hp0 - target.CurrentHealth;
            Assert.AreEqual(Mathf.Min(hp0, blast.baseDamage * 2f), dealt, 0.5f, "2x damage (up to the HP it had)");
        }

        [UnityTest]
        public IEnumerator Fire_Ignites_OnlyAtFullCharge_WithAVisibleBurn()
        {
            // Bruno, 2026-10-20: only a full charge ignites. A part charge is a heat hit, no burn (PROPOSED).
            powers.Select(PowerKind.Fire);
            PowerDefinition fire = powers.Selected;
            Health target = dummyB.GetComponent<Health>();
            float hp0 = target.CurrentHealth;

            powers.FireAt(fire, 0.6f, dummyB);
            yield return null;
            BurningStatus burn = dummyB.GetComponent<BurningStatus>();
            Assert.IsTrue(burn == null || !burn.IsBurning, "a part charge does not ignite");
            Assert.AreEqual(hp0, target.CurrentHealth, 0.01f, "a part charge does no damage (Bruno, 2026-10-22: it only stuns)");
            yield return new WaitForSeconds(BurningStatus.TickSeconds + 0.2f);
            Assert.AreEqual(hp0, target.CurrentHealth, 0.01f, "no burn ticks after a part charge");

            float hp1 = target.CurrentHealth;
            powers.FireAt(fire, 1f, dummyB);
            yield return null;
            burn = dummyB.GetComponent<BurningStatus>();
            Assert.IsNotNull(burn, "a full charge ignites");
            Assert.IsTrue(burn.IsBurning, "the burn runs");
            Assert.IsTrue(burn.IsTinted, "the placeholder look: the victim turns orange");
            Assert.AreEqual(hp1 - fire.baseDamage * 0.5f, target.CurrentHealth, 0.01f, "the full-charge hit");
            yield return new WaitForSeconds(1.1f);
            Assert.Less(target.CurrentHealth, hp1 - fire.baseDamage * 0.5f - fire.burnPerSecond * 0.9f, "the burn ticks");
            Assert.IsTrue(GameObject.Find("BurnFlame") != null, "flames rise off the victim");
            yield return new WaitForSeconds(fire.burnSeconds);
            Assert.IsFalse(burn.IsBurning, "the burn ends");
            Assert.IsFalse(burn.IsTinted, "the tint goes away");
        }

        [UnityTest]
        public IEnumerator Telekinesis_SecondPress_ThrowsTheHeldEnemy_IntoTheOther()
        {
            // Bruno, 2026-10-20: in the air, the next attack press throws it into another enemy. Both take the damage.
            powers.Select(PowerKind.Telekinesis);
            PowerDefinition tk = powers.Selected;
            Health a = dummyA.GetComponent<Health>();
            Health b = dummyB.GetComponent<Health>();
            float hpA = a.CurrentHealth;
            float hpB = b.CurrentHealth;
            Vector3 floorA = dummyA.transform.position;

            powers.FireAt(tk, 1f, dummyA);
            yield return new WaitForSeconds(tk.liftSeconds + 0.1f);
            Assert.IsNotNull(powers.HeldLift, "she holds Dummy_A in the air");

            pad.Tap(InputManager.GameAction.Charge);
            yield return null;
            yield return null;
            Assert.AreEqual("launch:Dummy_B", powers.LastResult, "a short press throws it at the other dummy");
            Assert.IsFalse(powers.IsCharging, "the press ended");
            Assert.AreEqual(0f, powers.Charge, 0.001f, "the circle reset");

            yield return WaitUntil(() => dummyA.GetComponent<TelekinesisLift>() == null, 3f);
            Assert.IsTrue(dummyA.GetComponent<TelekinesisLift>() == null, "the throw ended");
            Assert.AreEqual(hpB - tk.baseDamage, b.CurrentHealth, 0.5f, "the hit dummy takes the damage");
            Assert.AreEqual(hpA - tk.baseDamage, a.CurrentHealth, 0.5f, "the thrown dummy takes the damage");
            Assert.Less(Vector3.Distance(dummyA.transform.position, dummyB.transform.position), 2f, "it landed at the other dummy");
            Assert.AreEqual(floorA.y, dummyA.transform.position.y, 0.05f, "back on the floor");
            Assert.Greater(Vector3.Distance(dummyA.transform.position, floorA), 1f, "it flew");
        }

        [UnityTest]
        public IEnumerator Telekinesis_HeldEnemy_TurnsWithHer()
        {
            // Bruno, 2026-10-20: the floated enemy turns with her, so she aims the throw with the tank turn.
            powers.Select(PowerKind.Telekinesis);
            PowerDefinition tk = powers.Selected;
            powers.FireAt(tk, 1f, dummyA);
            yield return new WaitForSeconds(tk.liftSeconds + 0.1f);
            Assert.IsNotNull(powers.HeldLift, "she holds Dummy_A in the air");
            Vector3 before = Vector3.ProjectOnPlane(dummyA.transform.position - player.transform.position, Vector3.up);
            float yaw0 = player.transform.eulerAngles.y;

            pad.SetHeld(InputManager.GameAction.TurnRight, true);
            yield return new WaitForSeconds(0.4f);
            pad.ReleaseAll();
            yield return null;
            float turned = Mathf.DeltaAngle(yaw0, player.transform.eulerAngles.y);
            Assert.Greater(turned, 30f, "she turned right");
            Vector3 after = Vector3.ProjectOnPlane(dummyA.transform.position - player.transform.position, Vector3.up);
            float swung = Vector3.SignedAngle(before, after, Vector3.up);
            Assert.AreEqual(turned, swung, 8f, "the held enemy swung around her by the same angle");
            Assert.AreEqual(before.magnitude, after.magnitude, 0.2f, "at the same distance");
            Assert.IsNotNull(powers.HeldLift, "still in the air");
        }

        [UnityTest]
        public IEnumerator Fire_PartCharge_FlashesTheShooter()
        {
            // Bruno, 2026-10-20, option c: a part charge of FIRE breaks the shooter's aim, no burn.
            powers.Select(PowerKind.Fire);
            PowerDefinition fire = powers.Selected;
            Assert.Greater(fire.staggerSeconds, 0f, "the flash time is set on the asset");
            DummyShooter shooter = dummyA.GetComponent<DummyShooter>();
            Health target = dummyA.GetComponent<Health>();
            float hp0 = target.CurrentHealth;
            powers.FireAt(fire, 0.5f, dummyA);
            yield return null;
            Assert.AreEqual(DummyShooter.State.Staggered, shooter.Current, "the flash breaks its aim");
            Assert.AreEqual(hp0, target.CurrentHealth, 0.01f, "a part charge does no damage, it only stuns");
            BurningStatus burn = dummyA.GetComponent<BurningStatus>();
            Assert.IsTrue(burn == null || !burn.IsBurning, "no burn");
        }

        [UnityTest]
        public IEnumerator Telekinesis_CatchesABullet_AndSendsItBack()
        {
            // Bruno, 2026-10-20: the defensive telekinesis, first form. A bullet near her goes back at the shooter.
            powers.Select(PowerKind.Telekinesis);
            stats.DebugSet(100f, 0f);
            DummyShooter shooter = dummyA.GetComponent<DummyShooter>();
            Health shooterHealth = dummyA.GetComponent<Health>();
            float hp0 = shooterHealth.CurrentHealth;
            shooter.enabled = true;
            yield return WaitUntil(() => shooter.ShotsFired >= 1, 4f);
            Assert.AreEqual(1, shooter.ShotsFired, "it fired at her");

            // RMB presses, a release between them, until the bullet is inside the catch radius (RMB defends).
            // A press also counts for the next 0.2 s, so an early press still catches.
            float until = Time.time + 1.5f;
            while (Time.time < until && powers.LastResult != "catch")
            {
                pad.Tap(InputManager.GameAction.Defend);
                yield return null;
                yield return null;
            }
            Assert.AreEqual("catch", powers.LastResult, "RMB caught the bullet inside the catch radius");
            Assert.AreEqual(100f - PsychicPowers.CatchNalconCost, stats.Nalcon, 0.5f, "the catch costs nalcon (the nude outfit regenerates 2 per second, a frame or two passed)");
            Assert.AreEqual(health.MaxHealth, health.CurrentHealth, 0.01f, "the bullet never hit her");

            yield return WaitUntil(() => shooterHealth.CurrentHealth < hp0, 2f);
            Assert.AreEqual(hp0 - 30f, shooterHealth.CurrentHealth, 0.5f, "the shooter takes twice the bullet damage");
            Assert.AreEqual(DummyShooter.State.Staggered, shooter.Current, "and loses its aim");
            shooter.enabled = false;
        }

        [UnityTest]
        public IEnumerator Hud_Warns_LowHp_LowNalcon_HighShort_WithIconsAndBeeps()
        {
            // Bruno, 2026-10-20: a warning per bar, visible and audible. Low HP, low nalcon, high SHORT.
            PlayerHud hud = UnityEngine.Object.FindFirstObjectByType<PlayerHud>();
            Assert.IsNotNull(hud, "the HUD exists");
            yield return null;
            Assert.IsFalse(hud.LowHpWarning || hud.LowNalconWarning || hud.HighShortWarning, "no warning at full bars");
            int beeps0 = hud.WarningBeeps;

            health.TakeDamage(health.MaxHealth * 0.8f);
            stats.DebugSet(PsychicStats.MaxNalcon * 0.2f, PsychicStats.MaxShort * 0.8f);
            yield return null;
            Assert.IsTrue(hud.LowHpWarning, "HP at 20 percent: the low HP warning");
            Assert.IsTrue(hud.LowNalconWarning, "nalcon at 20 percent: the low nalcon warning");
            Assert.IsTrue(hud.HighShortWarning, "SHORT at 80 percent: the high SHORT warning");
            Assert.IsTrue(hud.transform.Find("PlayerHudCanvas/Bars/HpWarning").gameObject.activeSelf, "the heart badge shows");
            Assert.IsTrue(hud.transform.Find("PlayerHudCanvas/Bars/NalconWarning").gameObject.activeSelf, "the drop badge shows");
            Assert.IsTrue(hud.transform.Find("PlayerHudCanvas/Bars/ShortWarning").gameObject.activeSelf, "the bolt badge shows");
            Assert.AreEqual("! LOW", hud.transform.Find("PlayerHudCanvas/Bars/HpWarning/Text").GetComponent<Text>().text, "the word next to the icon");
            Assert.AreEqual("! HIGH", hud.transform.Find("PlayerHudCanvas/Bars/ShortWarning/Text").GetComponent<Text>().text, "high for SHORT");
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.AreEqual(beeps0 + 9, hud.WarningBeeps, "three beeps per warning when it first triggers, three warnings");
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.AreEqual(beeps0 + 9, hud.WarningBeeps, "no more beeps while the warnings stay on");

            stats.RefillAll();
            yield return null;
            Assert.IsFalse(hud.LowHpWarning || hud.LowNalconWarning || hud.HighShortWarning, "all clear after a refill");
            stats.DebugSet(PsychicStats.MaxNalcon * 0.1f, 0f);
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(beeps0 + 12, hud.WarningBeeps, "a warning that clears and triggers again beeps again");
        }

        [UnityTest]
        public IEnumerator SecondWind_LeavesHerAt1Hp_Invulnerable_Once_AndADrugDoesNotRearmIt()
        {
            // Bruno, 2026-10-20: the first lethal hit leaves her at 1 HP and invulnerable for 2.5 s. Once. A drug
            // refills her but does not give the second wind back (the pill does, later).
            Assert.IsTrue(health.IsSecondWindEnabled, "the second wind is on for Lucy");
            Assert.IsTrue(health.IsSecondWindAvailable, "and unused");
            PlayerHud hud = UnityEngine.Object.FindFirstObjectByType<PlayerHud>();

            health.TakeDamage(health.MaxHealth * 2f);
            yield return null;
            Assert.IsFalse(health.IsDead, "she survives the first lethal hit");
            Assert.AreEqual(1f, health.CurrentHealth, 0.01f, "at 1 HP");
            Assert.IsTrue(health.IsInvulnerable, "invulnerable");
            Assert.IsTrue(hud == null || hud.SecondWindShown, "the HUD says SECOND WIND");
            Assert.AreEqual(2.5f, health.SecondWindInvulnerabilityRemaining, 0.1f, "for 2.5 s");
            health.TakeDamage(50f);
            Assert.AreEqual(1f, health.CurrentHealth, 0.01f, "a hit in the window does nothing");

            yield return new WaitForSecondsRealtime(2.6f);
            Assert.IsFalse(health.IsInvulnerable, "the window closed");
            Assert.IsFalse(health.IsSecondWindAvailable, "the second wind is used up");

            drugs.DebugSet(DrugType.Nalcon, 1);
            yield return DrugPress.TakeFirst(pad); // the drug key opens the choice: it asks, then it takes
            Assert.AreEqual(health.MaxHealth, health.CurrentHealth, 0.01f, "the drug refills her");
            Assert.IsFalse(health.IsSecondWindAvailable, "but the second wind stays used");

            health.TakeDamage(health.MaxHealth * 2f);
            yield return null;
            Assert.IsTrue(health.IsDead, "the next lethal hit kills her");
        }

        [UnityTest]
        public IEnumerator Nalcon_Recharges_Nude_NotGuard()
        {
            // Bruno, 2026-10-20: 6 per second was too quick. 2 per second (PROPOSED): a full bar in 50 s.
            Assert.AreEqual(2f, stats.Outfit.nalconRegenPerSecond, 0.01f, "the nude outfit recharges 2 nalcon per second (the asset must carry the new number)");
            stats.RegenPaused = false;
            stats.DebugSet(50f, 0f);
            yield return new WaitForSeconds(1f);
            Assert.That(stats.Nalcon, Is.InRange(51.5f, 52.7f), "the nude outfit recharges nalcon, slowly");

            stats.SetOutfit(Resources.Load<OutfitProfile>("Outfits/outfit_guard"));
            Assert.AreEqual(150f, health.MaxHealth, "the guard outfit has more HP");
            stats.DebugSet(50f, 0f);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(50f, stats.Nalcon, 0.01f, "the guard outfit has no passive recharge");
        }

        [UnityTest]
        public IEnumerator Short_Rises_WithKills_AndDamageTaken()
        {
            stats.SetOutfit(Resources.Load<OutfitProfile>("Outfits/outfit_guard"));
            stats.DebugSet(100f, 0f);
            stats.NotifyKill("test");
            Assert.AreEqual(25f, stats.Short, 0.01f, "25 per kill");
            health.TakeDamage(10f);
            yield return null;
            Assert.AreEqual(31f, stats.Short, 0.01f, "0.6 per HP of damage taken");
        }

        [UnityTest]
        public IEnumerator FullShort_StartsOnTheAttackPress_SlowWalk_AndADrugEndsIt()
        {
            // Bruno, 2026-10-20: with a full bar the short starts the moment she presses attack or defend. During
            // it she walks slowly: no run, no dodge.
            powers.Select(PowerKind.Blast);
            drugs.DebugSet(DrugType.Nalcon, 1);
            stats.DebugSet(100f, 100f);
            Assert.IsTrue(stats.IsShortFull);
            float hpMax = health.MaxHealth;

            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return null;
            yield return null;
            Assert.IsTrue(shortController.IsActive, "the attack press starts the short at once, before any release");
            Assert.AreEqual("short", powers.LastResult);
            Assert.AreEqual(0f, powers.Charge, 0.001f, "that press starts no charge");
            pad.ReleaseAll();
            // The start freezes the game for a moment (Bruno, 2026-10-25): the release counts when it runs again.
            float frozenAt = Time.realtimeSinceStartup;
            while (Time.timeScale < 0.999f && Time.realtimeSinceStartup - frozenAt < 4f) yield return null;
            yield return null;
            yield return null;
            Assert.IsTrue(shortController.IsActive, "the release changes nothing");
            Assert.IsFalse(tank.MovementLocked, "she keeps her legs during a short");
            Assert.IsTrue(tank.SlowWalk, "a slow walk during the short");

            Vector3 before = player.transform.position;
            pad.SetHeld(InputManager.GameAction.Forward, true);
            pad.SetHeld(InputManager.GameAction.Run, true);
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(tank.IsRunning, "no run during the short, Shift or not");
            yield return new WaitForSeconds(0.5f);
            pad.ReleaseAll();
            yield return null;
            float walked = Vector3.Distance(before, player.transform.position);
            Assert.That(walked, Is.InRange(0.4f, tank.WalkSpeed * 0.85f), $"she walked, slower than the normal walk ({walked:0.00} m in 1 s)");
            pad.Tap(InputManager.GameAction.Dodge);
            yield return null;
            yield return null;
            Assert.IsFalse(tank.IsDodging, "no dodge during the short");
            Assert.IsTrue(shortController.IsActive, "the short goes on while she walks");
            Assert.Less(health.CurrentHealth, hpMax - 2f, "the shocks hurt her");
            Assert.GreaterOrEqual(health.CurrentHealth, 1f, "the short never kills her");

            yield return DrugPress.TakeFirst(pad); // the drug key opens the choice: it asks, then it takes
            Assert.IsFalse(shortController.IsActive, "the drug ends the short");
            Assert.AreEqual(hpMax, health.CurrentHealth, 0.01f, "HP refilled");
            Assert.AreEqual(PsychicStats.MaxNalcon, stats.Nalcon, 0.01f, "nalcon refilled");
            Assert.AreEqual(0f, stats.Short, 0.01f, "SHORT reset");
            Assert.AreEqual(0, drugs.Count(DrugType.Nalcon), "one drug used");
            Assert.IsFalse(tank.MovementLocked, "control is back");
            Assert.IsFalse(tank.SlowWalk, "the normal walk is back");
        }

        [UnityTest]
        public IEnumerator FullShort_StartsOnTheDefendPress_Too()
        {
            powers.Select(PowerKind.Telekinesis);
            stats.DebugSet(100f, 100f);
            pad.Tap(InputManager.GameAction.Defend);
            yield return null;
            yield return null;
            Assert.IsTrue(shortController.IsActive, "the defend press starts the short at once (Bruno, 2026-10-20)");
            Assert.AreEqual("short", powers.LastResult, "and it is no catch");
            shortController.End(byDrug: false);
            yield return null;
            Assert.IsFalse(shortController.IsActive);
            Assert.IsFalse(tank.SlowWalk, "the normal walk is back");
        }

        [UnityTest]
        public IEnumerator FullShort_TheBarFillsDuringACharge_TheShortStartsAtOnce()
        {
            // Bruno, 2026-10-25: "shock should trigger the moment player presses attack; as of now, the player can
            // charge fully and only when they release the button the shock is triggered". A hit during a held charge
            // fills the bar: the short starts then, not at the release.
            powers.Select(PowerKind.Blast);
            stats.DebugSet(100f, 90f);
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(powers.Charge, 0f, "she charges");
            Assert.IsFalse(shortController.IsActive, "no short yet");
            stats.AddShort(20f); // the hit that fills the bar
            yield return null;
            yield return null;
            Assert.IsTrue(shortController.IsActive, "the short starts while she still holds the key");
            Assert.AreEqual(0f, powers.Charge, 0.001f, "the charge is gone");
            pad.ReleaseAll();
            yield return null;
            yield return null;
            Assert.IsTrue(shortController.IsActive, "the release changes nothing");
            Assert.AreEqual("short", powers.LastResult, "the release fires no power");
            shortController.End(byDrug: false);
        }

        [UnityTest]
        public IEnumerator FullShort_Holds_UntilAShortOrADrug()
        {
            // Bruno, 2026-10-20: a full SHORT bar does not drain. Only a short or a drug empties it.
            Assert.Greater(stats.Outfit.shortDecayPerSecond, 0f, "the nude outfit drains a part-full bar");
            stats.RegenPaused = false; // the drain runs with the regen (the setup pauses both)
            stats.DebugSet(100f, 90f);
            yield return new WaitForSeconds(1f);
            Assert.Less(stats.Short, 89.5f, "a part-full bar drains");

            stats.DebugSet(100f, PsychicStats.MaxShort);
            yield return new WaitForSeconds(2f);
            Assert.IsTrue(stats.IsShortFull, "a full bar holds");
            Assert.AreEqual(PsychicStats.MaxShort, stats.Short, 0.001f, "not one point lost");
            health.TakeDamage(5f);
            yield return null;
            Assert.AreEqual(PsychicStats.MaxShort, stats.Short, 0.001f, "damage keeps it full");

            drugs.DebugSet(DrugType.Red, 1);
            yield return DrugPress.TakeFirst(pad); // the drug key opens the choice: it asks, then it takes
            Assert.AreEqual(0f, stats.Short, 0.01f, "a drug empties the full bar, short or not");
            Assert.IsFalse(shortController.IsActive, "no short ran");

            stats.DebugSet(100f, PsychicStats.MaxShort);
            shortController.Begin();
            yield return new WaitForSeconds(shortController.Duration + 0.6f);
            Assert.IsFalse(shortController.IsActive, "the short ended");
            Assert.AreEqual(0f, stats.Short, 0.01f, "the short empties the bar");
        }

        [UnityTest]
        public IEnumerator Short_EndsByItself_AtAboutHalfHp()
        {
            stats.SetOutfit(Resources.Load<OutfitProfile>("Outfits/outfit_guard"));
            stats.DebugSet(100f, 100f);
            float hpMax = health.MaxHealth;
            Assert.AreEqual(16f, shortController.Duration, 0.01f, "16 s (Bruno, 2026-10-20). The scene must carry the C# default.");
            shortController.Begin();
            Assert.IsTrue(shortController.IsActive);
            yield return new WaitForSeconds(shortController.Duration + 0.6f);
            Assert.IsFalse(shortController.IsActive, "the short ends on its own");
            Assert.AreEqual(hpMax * 0.5f, health.CurrentHealth, hpMax * 0.12f, "about half her HP is gone");
            Assert.AreEqual(0f, stats.Short, 0.01f, "SHORT reset");
        }

        [UnityTest]
        public IEnumerator DrugPickup_AsksFirst_AndTakesOneSlotEach_UpToTheOutfitSlots()
        {
            Assert.AreEqual(0, drugs.Count(DrugType.Nalcon));
            GameObject pickup = GameObject.Find("Drug_Nalcon");
            Assert.IsNotNull(pickup, "a nalcon drug lies in the room");
            Vector3 point = pickup.transform.position;
            tank.Teleport(new Vector3(point.x, 0f, point.z - 0.8f), 0f);
            yield return null;
            yield return null;
            Assert.AreEqual(0, drugs.Count(DrugType.Nalcon), "walking up to it takes nothing (Bruno, 2026-10-22: grabbables ask first)");
            pad.Tap(InputManager.GameAction.Interact);
            yield return null;
            yield return null;
            yield return null;
            pad.Tap(InputManager.GameAction.Interact);
            yield return WaitUntil(() => drugs.Count(DrugType.Nalcon) == 1, 2f);
            Assert.AreEqual(1, drugs.Count(DrugType.Nalcon), "the second press takes it");
            yield return null;
            Assert.IsTrue(pickup == null, "the pickup is gone");
            var items = tank.GetComponent<GlorifiedBody.Interaction.ItemInventory>();
            Assert.AreEqual(1, items.UsedSlots, "one slot");
            Assert.AreEqual(items.Capacity - 1, drugs.Add(DrugType.Red, 10), "the drugs fill the free slots, no more");
            Assert.IsTrue(items.IsFull);
        }

        [UnityTest]
        public IEnumerator DebugCheatKeys_StartTheShort_FillTheBar_AndRefill()
        {
            DebugCheatKeys cheats = UnityEngine.Object.FindFirstObjectByType<DebugCheatKeys>();
            Assert.IsNotNull(cheats, "DebugCheatKeys sits on [GameplaySystems]");
            Assert.IsFalse(shortController.IsActive, "no short at the start");

            Assert.IsTrue(cheats.StartShort(), "F6 starts the short");
            yield return null;
            Assert.IsTrue(shortController.IsActive, "the short is active at once");
            Assert.IsFalse(cheats.StartShort(), "a second F6 during a short does nothing");
            shortController.End(false);
            yield return null;
            Assert.IsFalse(shortController.IsActive, "the short ended");

            stats.DebugSet(stats.Nalcon, 0f);
            Assert.IsTrue(cheats.FillShort(), "F5 fills the bar");
            Assert.IsTrue(stats.IsShortFull, "the SHORT bar is full");

            drugs.DebugSet(DrugType.Red, 0);
            Assert.IsTrue(cheats.Refill(), "F7 refills");
            Assert.AreEqual(2, drugs.Count(DrugType.Red), "two of each drug");
            Assert.AreEqual(2, drugs.Count(DrugType.Nalcon), "two of each drug");

            // Bruno, 2026-10-20: the low-nalcon warning by hand. F8 takes 25, F9 gives 25.
            stats.DebugSet(100f, stats.Short);
            Assert.IsTrue(cheats.RemoveNalcon(), "F8 takes nalcon");
            Assert.AreEqual(75f, stats.Nalcon, 0.01f, "25 less");
            cheats.RemoveNalcon();
            cheats.RemoveNalcon();
            cheats.RemoveNalcon();
            Assert.AreEqual(0f, stats.Nalcon, 0.01f, "never under 0");
            Assert.IsTrue(cheats.AddNalcon(), "F9 gives nalcon");
            Assert.AreEqual(25f, stats.Nalcon, 0.01f, "25 more");
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
