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
            Assert.AreEqual(4, powers.Powers.Count, "four powers loaded from Resources/Powers");
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

            float nalconBefore = stats.Nalcon;
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(blast.fullChargeTime + 0.3f); // the full circle fires by itself
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
            yield return new WaitForSeconds(tk.fullChargeTime + 0.2f); // the full circle fires by itself
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
        public IEnumerator OnePose_HerArmRisesToTheTarget_AndWithOrzabalFollowsTheFloater()
        {
            // DEMO_PLAN §1, CANON 2026-11-05: "she lifts one arm, the palm out. With mk_orzabal the arm follows the floater".
            PowerArm arm = powers.GetComponent<PowerArm>();
            Assert.IsNotNull(arm, "PsychicPowers adds it");
            Assert.AreEqual(0f, arm.Weight, "no power: her arms as the animation has them");
            powers.Select(PowerKind.Blast);
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(1f, arm.Weight, 0.01f, "the charge raises the arm");
            Debug.Log($"[PowersAndBarsTests] the arm on the charge: {arm.AimError:0.0} degrees off the target");
            Assert.Less(arm.AimError, 20f, "it points at the locked target");
            AssertStance(arm, "the charge");
            pad.SetHeld(InputManager.GameAction.Charge, false);
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(0f, arm.Weight, 0.01f, "the shot gone: the arm goes down");

            powers.Select(PowerKind.Telekinesis);
            PowerDefinition tk = powers.Selected;
            powers.FireAt(tk, 1f, dummyB);
            yield return new WaitForSeconds(tk.liftSeconds + 0.8f);
            Assert.IsNotNull(dummyB.GetComponent<TelekinesisLift>(), "it floats");
            Assert.AreEqual(1f, arm.Weight, 0.01f, "she holds it: the arm stays up");
            Assert.Less(Vector3.Distance(arm.AimPoint, lockOn.HeldThing.position), 0.01f, "it aims at the floater");
            Debug.Log($"[PowersAndBarsTests] the arm on the floater ({(arm.LeftArm ? "left" : "right")}): {arm.AimError:0.0} degrees off, aim yaw/pitch {arm.AimAngles}, held at {lockOn.HeldThing.position - powers.transform.position}");
            Assert.Less(arm.AimError, 25f, "the arm follows the floater");
            AssertStance(arm, "the floater");
        }

        /// <summary>Bruno, 2026-11-09: "her arm is obviously going through her head; she's still doing the rest of the pistol
        /// shot pose". The aim hand clear of the head and near the shoulder's height, the other hand down at her side.</summary>
        public static void AssertStance(PowerArm arm, string when)
        {
            float headGap = Vector3.Distance(arm.AimHand, arm.Head);
            float handOverShoulder = arm.AimHand.y - arm.AimShoulder.y;
            float restUnderShoulder = arm.AimShoulder.y - arm.RestHand.y;
            Debug.Log($"[PowerArm] {when}: hand to head {headGap:0.00} m, hand over the shoulder {handOverShoulder:0.00} m, the other hand under it {restUnderShoulder:0.00} m, elbow bend {arm.ElbowBend:0} degrees, reach {arm.Reach:0.00}");
            Assert.Less(arm.ElbowBend, 25f, when + ": the arm out, the elbow almost straight");
            Debug.Log($"[PowerArm] {when}: the fingers {Vector3.Angle(arm.Fingers, Vector3.up):0} degrees from straight up");
            Assert.Less(Vector3.Angle(arm.Fingers, Vector3.up), 30f, when + ": the hand bent up, the fingers to the sky");
            // Bruno, 2026-11-09: "she's meant to have palm facing forward".
            Assert.IsTrue(arm.KnowsPalm, "the model has the finger bones that tell where the palm faces");
            Debug.Log($"[PowerArm] {when}: the palm {arm.PalmError:0} degrees from the target");
            Assert.Less(arm.PalmError, 35f, when + ": the palm faces the target");
            Assert.Greater(arm.Reach, 0.85f, when + ": the hand far from the shoulder, not at her chin");
            Assert.Greater(headGap, 0.3f, when + ": the hand is clear of her head");
            Assert.Less(Mathf.Abs(handOverShoulder), 0.35f, when + ": the hand near the shoulder's height");
            Assert.Greater(restUnderShoulder, 0.4f, when + ": the other arm hangs at her side");
        }

        [UnityTest]
        public IEnumerator Telekinesis_ABulletOnHer_DropsTheFloater_Unhurt()
        {
            // Bruno, 2026-11-07: "she won't drop the guy even after being hit multiple times".
            powers.Select(PowerKind.Telekinesis);
            PowerDefinition tk = powers.Selected;
            Health target = dummyB.GetComponent<Health>();
            float hp0 = target.CurrentHealth;
            powers.FireAt(tk, 1f, dummyB);
            yield return new WaitForSeconds(tk.liftSeconds + 0.3f);
            TelekinesisLift lift = dummyB.GetComponent<TelekinesisLift>();
            Assert.IsNotNull(lift, "it floats");

            Transform her = powers.transform;
            Vector3 chest = her.position + Vector3.up * 1.2f;
            Vector3 origin = chest + her.right * 4f;
            EnemyProjectile.Launch(origin, chest - origin, 20f, 5f, dummyA.gameObject, "test_bullet");
            yield return WaitUntil(() => lift == null || lift.Ending == "released", 2f);
            Assert.IsTrue(lift == null || lift.Ending == "released", "a bullet on her breaks the hold");
            yield return WaitUntil(() => dummyB.GetComponent<TelekinesisLift>() == null, 2f);
            Assert.AreEqual(hp0, target.CurrentHealth, 0.01f, "it drops with no damage");
            Assert.IsNull(powers.HeldLift, "she holds nothing");
        }

        [UnityTest]
        public IEnumerator Telekinesis_TheShortsStart_DropsTheFloater_AtOnce()
        {
            // Bruno, 2026-10-26: a short that starts while she holds an enemy in the air drops it at once.
            powers.Select(PowerKind.Telekinesis);
            PowerDefinition tk = powers.Selected;
            float y0 = dummyB.transform.position.y;
            powers.FireAt(tk, 1f, dummyB);
            yield return new WaitForSeconds(tk.liftSeconds + 0.3f);
            TelekinesisLift lift = dummyB.GetComponent<TelekinesisLift>();
            Assert.IsNotNull(powers.HeldLift, "she holds Dummy_B in the air");

            stats.DebugSet(stats.Nalcon, PsychicStats.MaxShort);
            shortController.Begin();
            Assert.IsTrue(shortController.IsActive, "the short started");
            Assert.IsNull(powers.HeldLift, "she lets go at the start of the short");
            Assert.IsFalse(lift.CanLaunch, "the lift is released at once");
            // The lift names its ending when the world runs again (the frozen start of the short).
            yield return WaitUntil(() => lift == null || lift.Ending != "", ShortController.StartFreeze + 2f);
            Assert.IsTrue(lift != null && lift.Ending == "released", "a release, not a throw or a slam: " + (lift != null ? lift.Ending : "gone"));
            yield return WaitUntil(() => dummyB.GetComponent<TelekinesisLift>() == null, ShortController.StartFreeze + 3f);
            Assert.AreEqual(y0, dummyB.transform.position.y, 0.05f, "it dropped to the floor after the frozen start");
            shortController.End(byDrug: false);
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
            Assert.AreEqual(nalconBefore - PsychicPowers.LiftCost, stats.Nalcon, 0.5f, "the lift costs as on an enemy");
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
            Assert.AreEqual(hp0 - tk.baseDamage * tk.minCharge, target.CurrentHealth, 0.5f, "the crate hurts the dummy, by the least charge of a short press");
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
            Assert.AreEqual(100f - blast.nalconCost, stats.Nalcon, 0.01f, "the circle fills in half the time: it fired already");
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
            Assert.AreEqual(hpB - tk.baseDamage * tk.minCharge, b.CurrentHealth, 0.5f, "the hit dummy takes the damage, by the least charge of a short press");
            Assert.AreEqual(hpA - tk.baseDamage * tk.minCharge, a.CurrentHealth, 0.5f, "the thrown dummy takes the damage");
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
        public IEnumerator SecondWind_LeavesHerAt1Hp_Invulnerable_Once_AndOnlyThePillRearmsIt()
        {
            // Bruno, 2026-10-20: the first lethal hit leaves her at 1 HP and invulnerable for 2.5 s. Once. Bruno,
            // 2026-10-30: no drug gives HP; the second wind pill gives the second wind back.
            Assert.IsTrue(health.IsSecondWindEnabled, "the second wind is on for Lucy");
            Assert.IsTrue(health.IsSecondWindAvailable, "and unused");
            PlayerHud hud = UnityEngine.Object.FindFirstObjectByType<PlayerHud>();

            // Bruno, 2026-11-06: the lethal hit from full HP only leaves her at 1 HP, and the second wind stays.
            health.TakeDamage(health.MaxHealth * 2f);
            yield return null;
            Assert.IsFalse(health.IsDead, "no hit kills her from above 1 HP");
            Assert.AreEqual(1f, health.CurrentHealth, 0.01f, "at 1 HP");
            Assert.IsTrue(health.IsSecondWindAvailable, "the second wind stays");
            Assert.IsTrue(health.IsInvulnerable, "for 0.5 s no hit lands: a burst cannot finish her");
            health.TakeDamage(50f);
            Assert.AreEqual(1f, health.CurrentHealth, 0.01f, "the next round of the burst does nothing");
            Assert.IsFalse(health.IsSecondWindActive);
            yield return new WaitForSeconds(Health.LastHpInvulnerableSeconds + 0.1f);
            Assert.IsFalse(health.IsInvulnerable, "the window closed");

            health.TakeDamage(health.MaxHealth * 2f);
            yield return null;
            Assert.IsFalse(health.IsDead, "she survives the hit at 1 HP");
            Assert.IsFalse(health.IsSecondWindAvailable, "it took the second wind");
            Assert.AreEqual(1f, health.CurrentHealth, 0.01f, "at 1 HP");
            Assert.IsTrue(health.IsInvulnerable, "invulnerable");
            Assert.IsTrue(hud == null || hud.SecondWindShown, "the HUD says SECOND WIND");
            Assert.AreEqual(2.5f, health.SecondWindInvulnerabilityRemaining, 0.1f, "for 2.5 s");
            health.TakeDamage(50f);
            Assert.AreEqual(1f, health.CurrentHealth, 0.01f, "a hit in the window does nothing");

            yield return new WaitForSecondsRealtime(2.6f);
            Assert.IsFalse(health.IsInvulnerable, "the window closed");
            Assert.IsFalse(health.IsSecondWindAvailable, "the second wind is used up");

            stats.DebugSet(40f, 0f);
            drugs.DebugSet(DrugType.Nalcon, 1);
            yield return DrugPress.TakeFirst(pad); // the drug key opens the choice: it asks, then it takes
            Assert.AreEqual(PsychicStats.MaxNalcon, stats.Nalcon, 0.01f, "the MK capsule fills MK");
            Assert.AreEqual(1f, health.CurrentHealth, 0.01f, "and gives no HP");
            Assert.IsFalse(health.IsSecondWindAvailable, "and no second wind");

            drugs.DebugSet(DrugType.DFelon, 1);
            yield return DrugPress.TakeFirst(pad);
            Assert.AreEqual("drug:DFelon", powers.LastResult);
            Assert.IsTrue(health.IsSecondWindAvailable, "the second wind pill gives it back");
            Assert.AreEqual(1f, health.CurrentHealth, 0.01f, "and no HP");
            health.TakeDamage(health.MaxHealth * 2f);
            yield return null;
            Assert.IsFalse(health.IsDead, "the second wind holds again");
            yield return new WaitForSecondsRealtime(2.6f);
            health.TakeDamage(health.MaxHealth * 2f);
            yield return null;
            Assert.IsTrue(health.IsDead, "the next lethal hit kills her");
        }

        [UnityTest]
        public IEnumerator EachDrug_DoesOneThing_AndADrugWithNothingToDo_IsKept()
        {
            // Bruno, 2026-10-30: "they can either fill up mk (one pill does it) or decrease short (another pill does that)".
            health.SetHealth(50f);
            stats.DebugSet(20f, 60f);
            drugs.DebugSet(DrugType.Nalcon, 1);
            Assert.IsTrue(powers.UseDrug(DrugType.Nalcon));
            Assert.AreEqual(PsychicStats.MaxNalcon, stats.Nalcon, 0.01f, "the MK capsule: MK full");
            Assert.AreEqual(60f, stats.Short, 0.01f, "the SHORT bar as it was");
            Assert.AreEqual(50f, health.CurrentHealth, 0.01f, "no HP");

            drugs.DebugSet(DrugType.Red, 1);
            Assert.IsTrue(powers.UseDrug(DrugType.Red));
            Assert.AreEqual(60f - PsychicStats.MaxShort * PsychicPowers.ShortCapsuleShare, stats.Short, 0.01f, "the SHORT capsule: half the bar away");
            Assert.AreEqual(PsychicStats.MaxNalcon, stats.Nalcon, 0.01f, "MK as it was");
            Assert.AreEqual(50f, health.CurrentHealth, 0.01f, "no HP");

            // Nothing to do: refused, and the drug stays.
            drugs.DebugSet(DrugType.Nalcon, 1);
            Assert.IsFalse(powers.UseDrug(DrugType.Nalcon));
            Assert.AreEqual("mk_full", powers.LastResult);
            Assert.AreEqual(1, drugs.Count(DrugType.Nalcon), "kept");
            stats.DebugSet(100f, 0f);
            drugs.DebugSet(DrugType.Red, 1);
            Assert.IsFalse(powers.UseDrug(DrugType.Red));
            Assert.AreEqual("short_empty", powers.LastResult);
            Assert.AreEqual(1, drugs.Count(DrugType.Red), "kept");
            drugs.DebugSet(DrugType.DFelon, 1);
            Assert.IsFalse(powers.UseDrug(DrugType.DFelon));
            Assert.AreEqual("second_wind_ready", powers.LastResult);
            Assert.AreEqual(1, drugs.Count(DrugType.DFelon), "kept");

            // No stand-in: a wanted drug she does not carry is not replaced by another.
            drugs.DebugSet(DrugType.Nalcon, 0);
            stats.DebugSet(20f, 0f);
            Assert.IsFalse(powers.UseDrug(DrugType.Nalcon));
            Assert.AreEqual("drug_none", powers.LastResult);
            Assert.AreEqual(1, drugs.Count(DrugType.Red), "the SHORT capsule stays");
            yield return null;
        }

        [UnityTest]
        public IEnumerator NoHp_ComesBack_ByItself_InAnyOutfit()
        {
            // Bruno, 2026-10-30: "hp regen is to be disabled in all forms (includes the passive regen we have on lucy bare)".
            stats.RegenPaused = false;
            health.SetHealth(40f);
            yield return new WaitForSeconds(2f);
            Assert.AreEqual(40f, health.CurrentHealth, 0.001f, "the nude outfit: no HP back");
            stats.SetOutfit(Resources.Load<OutfitProfile>("Outfits/outfit_guard"));
            health.SetHealth(40f);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(40f, health.CurrentHealth, 0.001f, "the guard outfit: no HP back");
        }

        [UnityTest]
        public IEnumerator NoMkBar_ComesBack_ByItself_InAnyOutfit()
        {
            // Bruno, 2026-11-06: "that basically makes mk an ammo counter and the pills clips to restore the ammo".
            stats.RegenPaused = false;
            stats.DebugSet(50f, 0f);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(50f, stats.Nalcon, 0.01f, "the nude outfit: no MK back");
            stats.SetOutfit(Resources.Load<OutfitProfile>("Outfits/outfit_guard"));
            Assert.AreEqual(150f, health.MaxHealth, "the guard outfit has more HP");
            stats.DebugSet(50f, 0f);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(50f, stats.Nalcon, 0.01f, "the guard outfit: no MK back");
        }

        [UnityTest]
        public IEnumerator EachPower_SpendsItsOwnBar_AndItsCapsuleFillsOnlyThatBar()
        {
            // Bruno, 2026-11-06: "each power has its own separate mk bar (and a respective pill to restore said power bar)".
            stats.DebugSet(100f, 0f);
            powers.Select(PowerKind.Fire);
            Assert.AreEqual(100f, stats.Nalcon, 0.01f);
            Assert.IsTrue(powers.Release(1f), powers.LastResult);
            Assert.AreEqual(75f, stats.Mk(PowerKind.Fire), 0.01f, "mk_erebus spends its own bar");
            Assert.AreEqual(100f, stats.Mk(PowerKind.Blast), 0.01f, "the mk_warspite bar stays");
            Assert.AreEqual(100f, stats.Mk(PowerKind.Telekinesis), 0.01f, "the mk_orzabal bar stays");
            drugs.DebugSet(DrugType.Nalcon, 1);
            drugs.DebugSet(DrugType.NalconFire, 1);
            Assert.IsFalse(powers.UseDrug(DrugType.Nalcon), "the mk_warspite capsule on a full bar: it stays");
            Assert.AreEqual("mk_full", powers.LastResult);
            Assert.AreEqual(1, drugs.Count(DrugType.Nalcon));
            Assert.IsTrue(powers.UseDrug(DrugType.NalconFire));
            Assert.AreEqual(100f, stats.Mk(PowerKind.Fire), 0.01f, "its capsule fills its bar");
            Assert.AreEqual(0, drugs.Count(DrugType.NalconFire));

            // An empty bar stops only its own power.
            stats.DebugSetMk(PowerKind.Fire, 0f);
            Assert.IsFalse(powers.Release(1f));
            Assert.AreEqual("no_nalcon", powers.LastResult);
            powers.Select(PowerKind.Blast);
            Assert.IsTrue(powers.Release(1f), "mk_warspite still has its bar");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AFullCircle_FiresByItself_AndANewChargeNeedsANewPress()
        {
            // Bruno, 2026-11-06: "when the circle for the power is full the power is unleashed automatically".
            stats.DebugSet(100f, 0f);
            dummyA.GetComponent<Health>().SetMaxHealth(1000f);
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return WaitUntil(() => stats.Mk(PowerKind.Blast) < 100f, powers.Selected.fullChargeTime + 1f);
            Assert.AreEqual(80f, stats.Mk(PowerKind.Blast), 0.01f, "it fired with the key still down, at the full cost");
            Assert.AreEqual(0f, powers.Charge, 0.001f, "the circle is empty");
            Assert.IsFalse(tank.MovementLocked, "her feet are free");
            yield return new WaitForSeconds(powers.Selected.fullChargeTime * 0.7f);
            Assert.AreEqual(0f, powers.Charge, 0.001f, "the same press starts no new charge");
            Assert.AreEqual(80f, stats.Mk(PowerKind.Blast), 0.01f);
            pad.SetHeld(InputManager.GameAction.Charge, false);
            yield return null;
            yield return null;
            Assert.AreEqual(80f, stats.Mk(PowerKind.Blast), 0.01f, "the release fires nothing");
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return WaitUntil(() => powers.Charge > 0f, 1f);
            Assert.Greater(powers.Charge, 0f, "a new press starts a new charge");
            pad.SetHeld(InputManager.GameAction.Charge, false);
        }

        [UnityTest]
        public IEnumerator TheDefendKey_CancelsACharge_AndSpendsNoMk()
        {
            // Bruno, 2026-11-06: "i started charging but changed my mind"; "a press of the defend key during a charge cancels it. agreed".
            stats.DebugSet(100f, 0f);
            float hp = dummyA.GetComponent<Health>().CurrentHealth;
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return WaitUntil(() => powers.Charge > 0.3f, 2f);
            Assert.Greater(powers.Charge, 0.3f);
            pad.Tap(InputManager.GameAction.Defend);
            yield return null;
            yield return null;
            Assert.AreEqual(0f, powers.Charge, 0.001f, "the circle is gone");
            Assert.AreEqual("cancel", powers.LastResult);
            Assert.IsFalse(tank.MovementLocked, "her feet are free");
            pad.SetHeld(InputManager.GameAction.Charge, false);
            yield return null;
            yield return null;
            Assert.AreEqual(100f, stats.Mk(PowerKind.Blast), 0.01f, "no MK spent");
            Assert.AreEqual(hp, dummyA.GetComponent<Health>().CurrentHealth, 0.001f, "nothing fired");
            Assert.AreEqual(0f, stats.Short, 0.01f, "no SHORT");
        }

        [UnityTest]
        public IEnumerator Orzabal_APress_LiftsAtOnce_ForOneCost_OnAnEnemyOrACrate()
        {
            // Bruno, 2026-11-07: "lift should cost the same no matter what is being lifted"; "orzabal needs no charge to lift".
            powers.Select(PowerKind.Telekinesis);
            stats.DebugSet(100f, 0f);
            lockOn.SetTarget(dummyB);
            yield return null;
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return null;
            yield return null;
            Assert.AreEqual(dummyB.gameObject, powers.HeldLift != null ? powers.HeldLift.gameObject : null, "the press lifts it now");
            Assert.AreEqual(100f - PsychicPowers.LiftCost, stats.Mk(PowerKind.Telekinesis), 0.01f);
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(0f, powers.Charge, 0.001f, "the same key held fills no circle: it must rise first");
            pad.SetHeld(InputManager.GameAction.Charge, false);
            yield return null;
            powers.HeldLift.Release(tank.gameObject);
            yield return WaitUntil(() => dummyB.GetComponent<TelekinesisLift>() == null, 2f);

            LockOnTargetable crate = GameObject.Find("Prop_Crate_1").GetComponent<LockOnTargetable>();
            lockOn.SetTarget(crate);
            yield return null;
            pad.Tap(InputManager.GameAction.Charge);
            yield return null;
            yield return null;
            Assert.AreEqual(crate.gameObject, powers.HeldLift != null ? powers.HeldLift.gameObject : null, "the crate too");
            Assert.AreEqual(100f - 2f * PsychicPowers.LiftCost, stats.Mk(PowerKind.Telekinesis), 0.01f, "the same cost");
            powers.HeldLift.Release(tank.gameObject);

            // With nothing to lift the key fills no circle.
            yield return new WaitForSeconds(1.5f);
            lockOn.SetTarget(null);
            foreach (LockOnTargetable t in new[] { dummyA, dummyB }) t.gameObject.SetActive(false);
            yield return null;
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(0f, powers.Charge, 0.001f);
            pad.SetHeld(InputManager.GameAction.Charge, false);
        }

        [UnityTest]
        public IEnumerator Orzabal_SheTurnsToWhatSheLifts_AndAChargeOnItDoesNotSpinHer()
        {
            powers.Select(PowerKind.Telekinesis);
            stats.DebugSet(100f, 0f);
            Vector3 to = Vector3.ProjectOnPlane(dummyB.transform.position - tank.transform.position, Vector3.up);
            tank.Teleport(tank.transform.position, Quaternion.LookRotation(to).eulerAngles.y + 90f);
            lockOn.SetTarget(dummyB);
            yield return null;
            Vector3 flat = Vector3.ProjectOnPlane(dummyB.transform.position, Vector3.up);
            pad.Tap(InputManager.GameAction.Charge);
            yield return new WaitForSeconds(powers.Selected.liftSeconds + 0.2f);
            Assert.Less(Vector3.Distance(flat, Vector3.ProjectOnPlane(dummyB.transform.position, Vector3.up)), 0.2f, "it rises where it stood: it does not swing as she turns");
            to = Vector3.ProjectOnPlane(dummyB.transform.position - tank.transform.position, Vector3.up);
            Assert.Less(Vector3.Angle(tank.transform.forward, to), 5f, "she faces it");

            // The mouse takes it to one side, and the charge must not chase it: it turns with her.
            pad.Look = new Vector2(1f, 0f);
            yield return new WaitForSeconds(0.3f);
            pad.Look = Vector2.zero;
            float yaw = tank.transform.eulerAngles.y;
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(0.4f);
            Assert.Greater(powers.Charge, 0f, "the circle fills with a thing in the air");
            Assert.AreEqual(0f, Mathf.DeltaAngle(yaw, tank.transform.eulerAngles.y), 0.5f, "she does not turn");
            pad.Tap(InputManager.GameAction.Defend);
            yield return null;
            pad.SetHeld(InputManager.GameAction.Charge, false);
            yield return null;
            powers.HeldLift.Release(tank.gameObject);
        }

        [UnityTest]
        public IEnumerator AThrow_CostsHurtsAndFlies_ByItsCharge()
        {
            // Bruno, 2026-11-07: "a throw should cost relative to the power the player put into it", the thrown enemy takes more, and it flies faster.
            powers.Select(PowerKind.Telekinesis);
            PowerDefinition tk = powers.Selected;
            Health a = dummyA.GetComponent<Health>(), b = dummyB.GetComponent<Health>();
            float weakSpeed = 0f, strongSpeed = 0f;
            foreach (float charge in new[] { 0.25f, 0.75f })
            {
                stats.DebugSet(45f, 0f); // no boost, for the base numbers
                float hpA = a.CurrentHealth, hpB = b.CurrentHealth;
                Vector3 toB = Vector3.ProjectOnPlane(dummyB.transform.position - dummyA.transform.position, Vector3.up);
                lockOn.SetTarget(dummyA);
                yield return null;
                powers.FireAt(tk, 1f, dummyA);
                yield return new WaitForSeconds(tk.liftSeconds + 0.1f);
                tank.Teleport(tank.transform.position, Quaternion.LookRotation(Vector3.ProjectOnPlane(dummyB.transform.position - tank.transform.position, Vector3.up)).eulerAngles.y);
                yield return null;
                float mk = stats.Mk(PowerKind.Telekinesis);
                Assert.IsTrue(powers.Release(charge), powers.LastResult);
                Assert.AreEqual(mk - tk.nalconCost * charge, stats.Mk(PowerKind.Telekinesis), 0.01f, $"the cost at {charge}");
                Vector3 from = dummyA.transform.position;
                float t0 = Time.time;
                yield return null;
                yield return null;
                yield return null;
                float speed = Vector3.ProjectOnPlane(dummyA.transform.position - from, Vector3.up).magnitude / Mathf.Max(0.001f, Time.time - t0);
                if (charge < 0.5f) weakSpeed = speed; else strongSpeed = speed;
                yield return WaitUntil(() => dummyA.GetComponent<TelekinesisLift>() == null, 3f);
                Assert.AreEqual(hpB - tk.baseDamage * charge, b.CurrentHealth, 0.5f, $"the one it hits, at {charge}");
                Assert.AreEqual(hpA - tk.baseDamage * charge, a.CurrentHealth, 0.5f, $"the thrown one, at {charge}");
                Debug.Log($"[PowersAndBars] a throw at {charge}: {speed:0.0} m/s");
                dummyA.transform.position = dummyA.transform.position - toB.normalized * 3f;
                yield return null;
            }
            Assert.Greater(strongSpeed, weakSpeed * 1.2f, "more power, more speed");
        }

        [UnityTest]
        public IEnumerator TheWheel_SwitchesTheLock_BothWays_AndRaisesAThingInTheAir()
        {
            // Bruno, 2026-11-07: "an alternative input to scroll through locked targets more quickly".
            powers.Select(PowerKind.Blast);
            lockOn.SetTarget(dummyA);
            yield return null;
            pad.Scroll = 0.1f;
            yield return null;
            pad.Scroll = 0f;
            yield return null;
            LockOnTargetable next = lockOn.CurrentTarget;
            Assert.AreNotEqual(dummyA, next, "the wheel up switches");
            pad.Scroll = -0.1f;
            yield return null;
            pad.Scroll = 0f;
            yield return null;
            Assert.AreEqual(dummyA, lockOn.CurrentTarget, "the wheel down goes back");

            powers.Select(PowerKind.Telekinesis);
            stats.DebugSet(100f, 0f);
            pad.Tap(InputManager.GameAction.Charge);
            yield return new WaitForSeconds(powers.Selected.liftSeconds + 0.1f);
            Assert.IsNotNull(powers.HeldLift);
            float y = dummyA.transform.position.y;
            pad.Scroll = 0.1f;
            yield return null;
            pad.Scroll = 0f;
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(dummyA, lockOn.CurrentTarget, "with a thing in the air the wheel does not switch");
            Assert.Greater(dummyA.transform.position.y, y + 0.1f, "it raises the thing");

            // With another power selected the wheel is the switch again, and the thing stays where it is.
            powers.Select(PowerKind.Blast);
            yield return null;
            y = dummyA.transform.position.y;
            pad.Scroll = 0.1f;
            yield return null;
            pad.Scroll = 0f;
            yield return new WaitForSeconds(0.2f);
            Assert.AreNotEqual(dummyA, lockOn.CurrentTarget, "another power: the wheel switches");
            Assert.AreEqual(y, dummyA.transform.position.y, 0.05f, "and raises nothing");
            powers.HeldLift.Release(tank.gameObject);
        }

        [UnityTest]
        public IEnumerator TheDefendPress_KeepsAHeldEnemy_InTheAir()
        {
            // Bruno, 2026-11-07: "the player might gently drop an enemy by accident on a misclick".
            powers.Select(PowerKind.Telekinesis);
            stats.DebugSet(100f, 0f);
            lockOn.SetTarget(dummyB);
            Assert.IsTrue(powers.Release(1f), powers.LastResult);
            yield return new WaitForSeconds(powers.Selected.liftSeconds + 0.1f);
            TelekinesisLift held = powers.HeldLift;
            Assert.IsNotNull(held, "she holds it");
            pad.Tap(InputManager.GameAction.Defend);
            yield return new WaitForSeconds(0.5f);
            Assert.AreSame(held, powers.HeldLift, "the press does not drop an enemy");
            Assert.AreNotEqual("drop", powers.LastResult);
            held.Release(tank.gameObject);
        }

        [UnityTest]
        public IEnumerator TheDefendPress_DropsACrateOfTheRoom_Whole_OntoAnotherCrate()
        {
            // A crate of the rooms has no Rigidbody until she lifts it. Without one it fell through what lay under it.
            powers.Select(PowerKind.Telekinesis);
            stats.DebugSet(100f, 0f);
            GameObject crate = GameObject.Find("Prop_Crate_1"), under = GameObject.Find("Prop_Crate_2");
            tank.Teleport((crate.transform.position + under.transform.position) * 0.5f + Vector3.back * 1.5f, 0f);
            yield return null;
            lockOn.SetTarget(crate.GetComponent<LockOnTargetable>());
            Assert.IsTrue(powers.Release(1f), powers.LastResult);
            yield return new WaitForSeconds(powers.Selected.liftSeconds + 0.1f);
            float t0 = Time.time;
            while (Time.time - t0 < 3f && Vector3.ProjectOnPlane(crate.transform.position - under.transform.position, Vector3.up).magnitude > 0.08f)
            {
                powers.HeldLift.SteerBy(Vector3.ClampMagnitude(Vector3.ProjectOnPlane(under.transform.position - crate.transform.position, Vector3.up), 0.05f), 0f, powers.Selected.range);
                yield return null;
            }
            pad.Tap(InputManager.GameAction.Defend);
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(crate != null, "a drop does not break it");
            float edge = under.GetComponent<Collider>().bounds.size.y;
            Debug.Log($"[PowersAndBars] the crate lies at {crate.transform.position}, the one under it at {under.transform.position}");
            Assert.Greater(crate.transform.position.y, under.transform.position.y + edge * 0.7f, "it rests on the other crate");
        }

        [UnityTest]
        public IEnumerator TheMouse_MovesTheFloater_AndDoesNotTurnHer()
        {
            // Bruno, 2026-11-06: "the tk'd object should be able to be moved with a mouse".
            powers.Select(PowerKind.Telekinesis);
            stats.DebugSet(100f, 0f);
            lockOn.SetTarget(dummyB);
            Assert.IsTrue(powers.Release(1f), powers.LastResult);
            yield return new WaitForSeconds(powers.Selected.liftSeconds + 0.1f);
            Assert.IsNotNull(powers.HeldLift, "she holds it");
            Vector3 at = dummyB.transform.position;
            float heading = tank.transform.eulerAngles.y;
            Camera cam = Camera.main;
            Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
            pad.Look = new Vector2(1f, 0f);
            yield return new WaitForSeconds(0.3f);
            pad.Look = Vector2.zero;
            Assert.Greater(Vector3.Dot(dummyB.transform.position - at, right), 0.2f, "mouse right: it goes right on the screen");
            Assert.AreEqual(heading, tank.transform.eulerAngles.y, 0.5f, "the mouse does not turn her while she holds it");
            powers.HeldLift.Release(tank.gameObject);
        }

        [UnityTest]
        public IEnumerator TheHud_ShowsOnlyTheBarOfTheSelectedPower()
        {
            // Bruno, 2026-11-06: "only the bar of the power that it activated is visible".
            PlayerHud hud = UnityEngine.Object.FindFirstObjectByType<PlayerHud>();
            Assert.IsNotNull(hud);
            stats.DebugSet(100f, 0f);
            stats.DebugSetMk(PowerKind.Fire, 10f);
            powers.Select(PowerKind.Blast);
            yield return null;
            yield return null;
            Transform bars = hud.transform.Find("PlayerHudCanvas/Bars");
            Image[] fills = bars.GetComponentsInChildren<Image>(true);
            Image fill = System.Array.Find(fills, i => i.name == "WARSPITEFill");
            Assert.IsNotNull(fill, "one MK row");
            Assert.AreEqual(1f, fill.fillAmount, 0.01f);
            Assert.IsFalse(hud.LowNalconWarning);
            powers.Select(PowerKind.Fire);
            yield return null;
            yield return null;
            Assert.AreEqual(0.1f, fill.fillAmount, 0.01f, "the row shows the bar of the selected power");
            Assert.IsTrue(hud.LowNalconWarning, "its warning too");
            Text label = System.Array.Find(bars.GetComponentsInChildren<Text>(true), t => t.text == "EREBUS");
            Assert.IsNotNull(label, "with its name");
            Assert.IsNull(System.Array.Find(fills, i => i.name == "EREBUSFill"), "no second MK row");
        }

        [UnityTest]
        public IEnumerator TheBoost_ReadsTheBarOfTheSelectedPower_AndADrugGoesToTheSelectedPowerFirst()
        {
            stats.DebugSet(100f, 0f);
            stats.DebugSetMk(PowerKind.Fire, 10f);
            stats.DebugSetMk(PowerKind.Telekinesis, 40f);
            powers.Select(PowerKind.Blast);
            Assert.IsTrue(powers.IsBoosted, "mk_warspite over half");
            powers.Select(PowerKind.Fire);
            Assert.IsFalse(powers.IsBoosted, "mk_erebus under half");

            drugs.DebugSet(DrugType.NalconFire, 1);
            drugs.DebugSet(DrugType.NalconLift, 1);
            Assert.AreEqual(DrugType.NalconFire, powers.MostNeededDrug(), "the capsule of the selected power");
            powers.Select(PowerKind.Blast); // its bar is full, and she has no capsule for it
            Assert.AreEqual(DrugType.NalconFire, powers.MostNeededDrug(), "then the lowest bar");
            yield return null;
        }

        [UnityTest]
        public IEnumerator MkBars_StayInASave_AndAnOlderFileGivesEveryBarItsOneValue()
        {
            stats.DebugSet(100f, 30f);
            stats.DebugSetMk(PowerKind.Fire, 10f);
            stats.DebugSetMk(PowerKind.Telekinesis, 55f);
            drugs.DebugSet(DrugType.NalconLift, 1);
            var state = GlorifiedBody.Saving.SaveSystem.CapturePlayer(player);
            stats.DebugSet(1f, 0f);
            drugs.DebugSet(DrugType.NalconLift, 0);
            GlorifiedBody.Saving.SaveSystem.ApplyPlayer(player, state);
            Assert.AreEqual(100f, stats.Mk(PowerKind.Blast), 0.01f);
            Assert.AreEqual(10f, stats.Mk(PowerKind.Fire), 0.01f);
            Assert.AreEqual(55f, stats.Mk(PowerKind.Telekinesis), 0.01f);
            Assert.AreEqual(1, drugs.Count(DrugType.NalconLift));

            state.mk.Clear();
            state.nalcon = 70f;
            GlorifiedBody.Saving.SaveSystem.ApplyPlayer(player, state);
            Assert.AreEqual(70f, stats.Mk(PowerKind.Blast), 0.01f, "an older file has one MK");
            Assert.AreEqual(70f, stats.Mk(PowerKind.Fire), 0.01f);
            Assert.AreEqual(70f, stats.Mk(PowerKind.Telekinesis), 0.01f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UsingAPower_FillsTheShortBar_ByTheMkItSpends()
        {
            // Bruno, 2026-11-06: "the short bar should increase when the player uses a power".
            stats.SetOutfit(Resources.Load<OutfitProfile>("Outfits/outfit_guard")); // no drain
            stats.DebugSet(100f, 0f);
            dummyA.GetComponent<Health>().SetMaxHealth(1000f); // it stands through both blasts
            Assert.IsTrue(powers.Release(1f), powers.LastResult);
            Assert.AreEqual(20f * PsychicStats.ShortPerMkPoint, stats.Short, 0.01f, "a full mk_warspite");
            float before = stats.Short;
            Assert.IsTrue(powers.Release(0.5f), powers.LastResult);
            Assert.AreEqual(before + 10f * PsychicStats.ShortPerMkPoint, stats.Short, 0.01f, "half a charge, half the MK, half the SHORT");
            before = stats.Short;
            powers.Release(0.05f); // under the minimum charge: a fizzle spends nothing
            Assert.AreEqual(before, stats.Short, 0.01f, "no MK spent, no SHORT");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheShort_KillsHer_WhenHerHpRunsOut_AndTheGameOverScreenNamesIt()
        {
            // Bruno, 2026-11-06: "otherwise it'll straight up kill them. confirm we have a game over screen for shorting to death".
            health.SetSecondWindEnabled(false);
            health.SetInvulnerable(false);
            health.SetRuntimeInvulnerable(false);
            health.SetHealth(5f);
            DeathMenuUI.LastKillerId = "soldier_rifle"; // an older hit must not take the credit
            shortController.Begin();
            yield return WaitUntil(() => health.IsDead, ShortController.StartFreeze + 8f);
            Assert.IsTrue(health.IsDead, "the shocks took her last HP");
            Assert.AreEqual(ShortController.KillerId, DeathMenuUI.LastKillerId, "the short killed her");
            Assert.IsFalse(shortController.IsActive, "no shocks from a body");
            yield return WaitUntil(() => GameManager.Instance.IsGameOver, 5f);
            Assert.IsTrue(GameManager.Instance.IsGameOver, "game over");
            DeathMenuUI screen = UnityEngine.Object.FindAnyObjectByType<DeathMenuUI>();
            Assert.IsNotNull(screen);
            yield return WaitUntil(() => screen.Shown != null, 2f);
            Assert.IsNotNull(screen.Shown, "the death screen is up");
            Assert.AreEqual(ShortController.KillerId, screen.Shown.killerId, "the death screen has its own row for the short");
            StringAssert.Contains("SHORT", screen.Shown.hint);
            Assert.IsFalse(string.IsNullOrEmpty(screen.Shown.voiceText), "the writer's line");
            GameObject canvas = GameObject.Find("DeathMenuUICanvas");
            Assert.IsNotNull(canvas, "the screen shows");
            Assert.IsTrue(canvas.activeInHierarchy);
        }

        [UnityTest]
        public IEnumerator TheShort_TakesTheSecondWind_GoesOn_AndTakesNoMoreHp()
        {
            health.SetSecondWindEnabled(true);
            health.RearmSecondWind();
            health.SetInvulnerable(false);
            health.SetRuntimeInvulnerable(false);
            health.SetHealth(5f);
            stats.DebugSet(45f, PsychicStats.MaxShort);
            shortController.Begin();
            yield return WaitUntil(() => !health.IsSecondWindAvailable, ShortController.StartFreeze + 8f);
            Assert.IsFalse(health.IsSecondWindAvailable, "the shock took the second wind");
            yield return null;
            Assert.IsFalse(health.IsDead, "she lives");
            Assert.AreEqual(1f, health.CurrentHealth, 0.001f, "at 1 HP");
            // Bruno, 2026-11-06: "the short goes on as normal, but it doesn't detract health while using up the second wind".
            Assert.IsTrue(shortController.IsActive, "the short goes on");
            int shocks = shortController.ShocksFired;
            yield return new WaitForSeconds(3.5f);
            Assert.IsFalse(health.IsDead, "she still lives after the invulnerable frames");
            Assert.Greater(shortController.ShocksFired, shocks, "the shocks go on");
            Assert.AreEqual(1f, health.CurrentHealth, 0.001f, "they take no more HP");
            yield return WaitUntil(() => !shortController.IsActive, shortController.Duration + 1f);
            Assert.IsFalse(shortController.IsActive, "it ends by itself");
            Assert.AreEqual(1f, health.CurrentHealth, 0.001f);
            Assert.AreEqual(0f, stats.Short, 0.01f);

            // The second wind is gone: the next short kills her.
            stats.DebugSet(45f, PsychicStats.MaxShort);
            shortController.Begin();
            yield return WaitUntil(() => health.IsDead, ShortController.StartFreeze + 3f);
            Assert.IsTrue(health.IsDead, "no second wind: the short kills her");
        }

        [UnityTest]
        public IEnumerator TheShort_AtHighHp_StillLeavesHerAlive()
        {
            health.SetSecondWindEnabled(false);
            health.SetInvulnerable(false);
            health.SetRuntimeInvulnerable(false);
            health.SetHealth(health.MaxHealth);
            shortController.Begin();
            yield return new WaitForSeconds(shortController.Duration + 0.6f);
            Assert.IsFalse(health.IsDead, "half her HP is gone, no more");
            Assert.IsFalse(shortController.IsActive);
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
        public IEnumerator FullShort_StartsOnTheAttackPress_SlowWalk_AndTheShortCapsuleEndsIt()
        {
            // Bruno, 2026-10-20: with a full bar the short starts the moment she presses attack or defend. During
            // it she walks slowly: no run, no dodge.
            powers.Select(PowerKind.Blast);
            drugs.DebugSet(DrugType.Red, 1);
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

            float hpInShort = health.CurrentHealth;
            yield return DrugPress.TakeFirst(pad); // the drug key opens the choice: it asks, then it takes
            Assert.IsFalse(shortController.IsActive, "the SHORT capsule ends the short");
            Assert.AreEqual(hpInShort, health.CurrentHealth, 0.01f, "no HP back (Bruno, 2026-10-30: only feeding)");
            Assert.AreEqual(0f, stats.Short, 0.01f, "SHORT reset");
            Assert.AreEqual(0, drugs.Count(DrugType.Red), "one drug used");
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
            // Bruno, 2026-10-20: a full SHORT bar does not drain. Only a short or the SHORT capsule lowers it.
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
            Assert.AreEqual(PsychicStats.MaxShort * PsychicPowers.ShortCapsuleShare, stats.Short, 0.2f, "the SHORT capsule takes half the full bar away (the part-full bar drains a little in the frames of the press) (Bruno, 2026-10-30: \"decrease short\")");
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
