#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Automation;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Systems;

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
            Assert.IsTrue(tank.MovementLocked, "she loses control");
            yield return new WaitForSeconds(1.0f);
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
        public IEnumerator Short_EndsByItself_AtAboutHalfHp()
        {
            stats.SetOutfit(Resources.Load<OutfitProfile>("Outfits/outfit_guard"));
            stats.DebugSet(100f, 100f);
            float hpMax = health.MaxHealth;
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
