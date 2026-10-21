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
            stats.DebugSet(100f, 0f);

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
            stats.DebugSet(100f, 0f);

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
        public IEnumerator Telekinesis_Lifts_ThenSlams()
        {
            powers.Select(PowerKind.Telekinesis);
            PowerDefinition tk = powers.Selected;
            Health target = dummyB.GetComponent<Health>();
            float hp0 = target.CurrentHealth;
            float y0 = dummyB.transform.position.y;
            powers.FireAt(tk, 1f, dummyB);
            yield return new WaitForSeconds(tk.liftSeconds + 0.2f);
            Assert.Greater(dummyB.transform.position.y, y0 + tk.liftHeight * 0.8f, "lifted");
            Assert.AreEqual(hp0, target.CurrentHealth, "no damage while in the air");
            yield return new WaitForSeconds(tk.holdSeconds + 0.5f);
            Assert.AreEqual(y0, dummyB.transform.position.y, 0.05f, "back on the floor");
            Assert.AreEqual(hp0 - tk.baseDamage, target.CurrentHealth, 0.5f, "the slam damage");
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
            Assert.AreEqual(hp0 - fire.baseDamage * 0.6f, target.CurrentHealth, 0.01f, "a part charge is a heat hit");
            yield return new WaitForSeconds(BurningStatus.TickSeconds + 0.2f);
            Assert.AreEqual(hp0 - fire.baseDamage * 0.6f, target.CurrentHealth, 0.01f, "no burn ticks after a part charge");

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
            Assert.AreEqual(1.5f, tk.holdSeconds, 0.01f, "the hold is the launch window, 1.5 s");
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
            Assert.AreEqual("launch:Dummy_B", powers.LastResult, "the press throws it at the other dummy");
            Assert.IsFalse(powers.IsCharging, "the press does not start a charge");
            Assert.AreEqual(0f, powers.Charge, 0.001f, "no charge");

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
            yield return WaitUntil(() => dummyA.GetComponent<TelekinesisLift>() == null, 3f);
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
            Assert.AreEqual(hp0 - fire.baseDamage * 0.5f, target.CurrentHealth, 0.01f, "the heat hit");
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
            Assert.AreEqual(100f - PsychicPowers.CatchNalconCost, stats.Nalcon, 0.5f, "the catch costs nalcon (the nude outfit regenerates 6 per second, a frame or two passed)");
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
            pad.Tap(InputManager.GameAction.UseDrug);
            yield return null;
            yield return null;
            Assert.AreEqual(health.MaxHealth, health.CurrentHealth, 0.01f, "the drug refills her");
            Assert.IsFalse(health.IsSecondWindAvailable, "but the second wind stays used");

            health.TakeDamage(health.MaxHealth * 2f);
            yield return null;
            Assert.IsTrue(health.IsDead, "the next lethal hit kills her");
        }

        [UnityTest]
        public IEnumerator Nalcon_Recharges_Nude_NotGuard()
        {
            stats.DebugSet(50f, 0f);
            yield return new WaitForSeconds(1f);
            Assert.Greater(stats.Nalcon, 54f, "the nude outfit recharges nalcon");

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
        public IEnumerator FullShort_TurnsTheRelease_IntoAShort_AndADrugEndsIt()
        {
            powers.Select(PowerKind.Blast);
            drugs.DebugSet(DrugType.Nalcon, 1);
            stats.DebugSet(100f, 100f);
            Assert.IsTrue(stats.IsShortFull);
            float hpMax = health.MaxHealth;

            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(0.6f);
            pad.ReleaseAll();
            yield return null;
            yield return null;
            Assert.IsTrue(shortController.IsActive, "a full SHORT turns the release into a short");
            Assert.IsFalse(tank.MovementLocked, "she keeps her legs during a short (Bruno, 2026-10-20)");
            Vector3 before = player.transform.position;
            pad.SetHeld(InputManager.GameAction.Forward, true);
            yield return new WaitForSeconds(1.0f);
            pad.ReleaseAll();
            Assert.Greater(Vector3.Distance(before, player.transform.position), 0.8f, "she walked during the short");
            Assert.IsTrue(shortController.IsActive, "the short goes on while she walks");
            Assert.Less(health.CurrentHealth, hpMax - 5f, "the shocks hurt her");
            Assert.GreaterOrEqual(health.CurrentHealth, 1f, "the short never kills her");

            pad.Tap(InputManager.GameAction.UseDrug);
            yield return null;
            yield return null;
            Assert.IsFalse(shortController.IsActive, "the drug ends the short");
            Assert.AreEqual(hpMax, health.CurrentHealth, 0.01f, "HP refilled");
            Assert.AreEqual(PsychicStats.MaxNalcon, stats.Nalcon, 0.01f, "nalcon refilled");
            Assert.AreEqual(0f, stats.Short, 0.01f, "SHORT reset");
            Assert.AreEqual(0, drugs.Count(DrugType.Nalcon), "one drug used");
            Assert.IsFalse(tank.MovementLocked, "control is back");
        }

        [UnityTest]
        public IEnumerator FullShort_Holds_UntilAShortOrADrug()
        {
            // Bruno, 2026-10-20: a full SHORT bar does not drain. Only a short or a drug empties it.
            Assert.Greater(stats.Outfit.shortDecayPerSecond, 0f, "the nude outfit drains a part-full bar");
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
            pad.Tap(InputManager.GameAction.UseDrug);
            yield return null;
            yield return null;
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
            Assert.AreEqual(8f, shortController.Duration, 0.01f, "8 s (Bruno, 2026-10-20). The scene must carry the C# default.");
            shortController.Begin();
            Assert.IsTrue(shortController.IsActive);
            yield return new WaitForSeconds(shortController.Duration + 0.6f);
            Assert.IsFalse(shortController.IsActive, "the short ends on its own");
            Assert.AreEqual(hpMax * 0.5f, health.CurrentHealth, hpMax * 0.12f, "about half her HP is gone");
            Assert.AreEqual(0f, stats.Short, 0.01f, "SHORT reset");
        }

        [UnityTest]
        public IEnumerator DrugPickup_AddsToTheInventory_UpToTheCapacity()
        {
            Assert.AreEqual(0, drugs.Count(DrugType.Nalcon));
            GameObject pickup = GameObject.Find("Drug_Nalcon");
            Assert.IsNotNull(pickup, "a nalcon drug lies in the room");
            tank.Teleport(pickup.transform.position + Vector3.up * 0.6f, 0f);
            yield return WaitUntil(() => drugs.Count(DrugType.Nalcon) == 1, 2f);
            Assert.AreEqual(1, drugs.Count(DrugType.Nalcon), "picked up");
            yield return null;
            Assert.IsTrue(pickup == null, "the pickup is gone");
            Assert.AreEqual(1, drugs.Add(DrugType.Nalcon, 5), "the nude outfit holds two of each: one more fits");
            Assert.AreEqual(2, drugs.Count(DrugType.Nalcon));
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
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
