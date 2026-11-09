#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Automation;
using GlorifiedBody.Boss;
using GlorifiedBody.Cameras;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Player;
using GlorifiedBody.Rooms;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The brain fight (DEMO_PLAN §14.1, CANON round 10) in the BRAIN TEST room: the eye watches her and the Crown HUD goes,
    /// its aim starts far and follows her slower than her walk, a full lock charges a strike that she escapes by running or
    /// cancels by damage (a stagger), its life on a bar of its own, standing still slows her, a grabber pins her and a hit frees
    /// her, mk_orzabal rips a grabber out and throws it at a node, the work lights go out as the nodes die, its death opens
    /// the exit, and it is a boss (no lift, no short, no mk_edgar).
    /// Batch: tools/unity.sh test playmode "BrainFightTests"
    /// </summary>
    public class BrainFightTests
    {
        private const string Scene = "BrainTest";
        private const string DeadFlag = "brain_test_dead";

        private VirtualPad pad;
        private PadDriver driver;
        private TankController tank;
        private PsychicPowers powers;
        private PsychicStats stats;
        private LockOnController lockOn;
        private Health health;
        private BrainFight fight;
        private Room sector;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            StoryFlags.Drop(DeadFlag);
            StoryFlags.Drop(PlayerHudFlags.CrownGone);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(Scene);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Scene && GameManager.Instance.IsPlaying, 10f);
            Assert.AreEqual(Scene, SceneManager.GetActiveScene().name, "the room is in the build (BrainTestSetup.BuildBrainTest)");
            yield return null;
            GameObject lucy = GameObject.FindGameObjectWithTag("Player");
            tank = lucy.GetComponent<TankController>();
            powers = lucy.GetComponent<PsychicPowers>();
            stats = lucy.GetComponent<PsychicStats>();
            lockOn = lucy.GetComponent<LockOnController>();
            health = lucy.GetComponentInChildren<Health>();
            fight = BrainFight.Instance;
            sector = Room.Of("B1");
            Assert.IsNotNull(fight, "the brain");
            fight.firstGrab = 999f; // the tests call the grabbers
            pad = new VirtualPad();
            GameInput.SetBotSource(pad);
            driver = new GameObject("[PadDriver]").AddComponent<PadDriver>();
            driver.Pad = pad;
            foreach (PowerKind k in new[] { PowerKind.Blast, PowerKind.Fire, PowerKind.Telekinesis }) stats.FillMk(k);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameInput.ClearBotSource(pad);
            if (driver != null) UnityEngine.Object.Destroy(driver.gameObject);
            StoryFlags.Drop(DeadFlag);
            StoryFlags.Drop(PlayerHudFlags.CrownGone);
            yield return null;
        }

        private IEnumerator Enter(Vector3 at)
        {
            tank.Teleport(at, 0f);
            yield return WaitUntil(() => fight.Current == BrainFight.Phase.Fighting, 3f);
        }

        private Vector3 Middle => sector.Centre;

        [UnityTest]
        public IEnumerator TheFight_Begins_WhenSheEnters_TheEyeWatches_TheMkBarsAndTheChargeStay()
        {
            Assert.AreEqual(BrainFight.Phase.Waiting, fight.Current);
            yield return Enter(Middle);
            Assert.AreEqual(BrainFight.Phase.Fighting, fight.Current, "her first step into the sector");
            yield return null;
            Assert.AreSame(fight.eye, FixedCameraDirector.Instance.Current, "the camera is the brain's eye");
            Assert.IsTrue(StoryFlags.Has(PlayerHudFlags.CrownGone), "the Crown HUD goes");
            BrainHud hud = fight.GetComponent<BrainHud>();
            yield return null;
            StringAssert.StartsWith("SUBJECT", hud.VitalsShown, "the brain reads her vitals");
            // Bruno, 2026-11-08: "have the mk bars present and the circle charge ui".
            PlayerHud crown = UnityEngine.Object.FindFirstObjectByType<PlayerHud>();
            Assert.IsTrue(crown.Shown, "the MK bars and the charge circle stay in the fight");
            Assert.IsFalse(crown.HpShown, "her HP is the brain's reading (SUBJECT)");
            // "the target should be where the camera is at".
            Vector3 body = fight.nodes.Single().transform.position;
            Assert.Less(Vector3.Distance(body, fight.eye.transform.position), 1.5f, "the brain's body is at its eye");

            // The charge on the brain: the brain is behind the camera, so the circle shows over her.
            yield return WaitUntil(() => lockOn.CurrentTarget != null, 2f);
            powers.Select(PowerKind.Blast);
            pad.SetHeld(InputManager.GameAction.Charge, true);
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(crown.ChargeShown, "the charge circle shows");
            Assert.IsTrue(crown.CanvasPoint(tank.transform.position + Vector3.up * 1.2f, out Vector2 her), "she is on screen");
            Assert.Less(Vector2.Distance(crown.ChargeAt, her), 5f, "over her, as the brain is off the screen");
            pad.SetHeld(InputManager.GameAction.Charge, false);
            Assert.AreEqual(6, fight.LightsOn, "every work light on");
        }

        [UnityTest]
        public IEnumerator TheAim_StartsFar_FollowsHerSlowerThanHerWalk_AndSheOutrunsIt()
        {
            // Bruno, 2026-11-08: "the brain starts unlocked, then it tracks the player at some speed that they can
            // outrun/dodge".
            yield return Enter(Middle);
            Assert.AreEqual(BrainFight.AimState.Tracking, fight.Aiming);
            Assert.AreEqual(0f, fight.LockProgress, "it starts unlocked");
            float d0 = Flat(fight.Aim - tank.transform.position);
            Assert.Greater(d0, 3f, "far from her");
            Assert.Less(fight.trackSpeed, tank.WalkSpeed, "slower than her walk");
            yield return new WaitForSeconds(1f);
            float d1 = Flat(fight.Aim - tank.transform.position);
            Assert.AreEqual(d0 - fight.trackSpeed, d1, 0.4f, "it follows her at its speed");

            // Caught: the lock fills. She runs 3 m on: it drains, and nothing fires.
            tank.Teleport(fight.Aim, 0f);
            yield return WaitUntil(() => fight.LockProgress > 0.4f, 2f);
            Assert.Greater(fight.LockProgress, 0.4f, "in the ring the lock fills");
            tank.Teleport(fight.Aim + Vector3.right * 3f, 0f);
            yield return WaitUntil(() => fight.LockProgress <= 0f, 2f);
            Assert.AreEqual(0f, fight.LockProgress, "out of it, it drains");
            Assert.AreEqual(0, fight.Strikes, "no strike");
        }

        [UnityTest]
        public IEnumerator AFullLock_ChargesAStrike_ThatHitsHer_OrMissesWhenSheRunsOut()
        {
            fight.lockSeconds = 0.3f;
            fight.chargeSeconds = 0.8f;
            fight.restSeconds = 0.2f;
            yield return Enter(Middle);
            float hp0 = health.CurrentHealth;
            tank.Teleport(fight.Aim, 0f);
            yield return WaitUntil(() => fight.Aiming == BrainFight.AimState.Charging, 2f);
            Assert.AreEqual(BrainFight.AimState.Charging, fight.Aiming, "full lock: the charge");
            Assert.GreaterOrEqual(fight.RingProgress, 0f, "the HUD frame closes with the charge");
            yield return WaitUntil(() => fight.Strikes >= 1, 3f);
            Assert.AreEqual(1, fight.StrikeHits, "she stood in it: the strike hits her");
            Assert.Less(health.CurrentHealth, hp0);
            Assert.AreEqual(BrainFight.KillerId, DeathMenuUI.LastKillerId, "its death screen if it kills her");

            float hp1 = health.CurrentHealth;
            yield return WaitUntil(() => fight.Aiming == BrainFight.AimState.Charging, 3f);
            tank.Teleport(fight.Aim + Vector3.right * 3.5f, 0f); // she runs: the aim cannot follow in time
            yield return WaitUntil(() => fight.Strikes >= 2, 3f);
            Assert.AreEqual(1, fight.StrikeHits, "it fires at its aim, and she is not there");
            Assert.AreEqual(hp1, health.CurrentHealth, 0.01f);
        }

        [UnityTest]
        public IEnumerator DamageInTheCharge_StaggersIt_AndCancelsTheStrike_ThenTheAimStartsFarAgain()
        {
            // "making enough damage staggers the brain to cancel the attack".
            fight.lockSeconds = 0.2f;
            fight.chargeSeconds = 5f;
            fight.staggerSeconds = 0.6f;
            yield return Enter(Middle);
            tank.Teleport(fight.Aim, 0f);
            yield return WaitUntil(() => fight.Aiming == BrainFight.AimState.Charging, 2f);
            BrainNode node = fight.nodes[0];
            float hp0 = node.Health.CurrentHealth;
            powers.Select(PowerKind.Blast);
            powers.FireAt(powers.Selected, 1f, node.GetComponent<LockOnTargetable>());
            yield return null;
            Assert.Less(node.Health.CurrentHealth, hp0, "the blast hurts it");
            Assert.GreaterOrEqual(fight.ChargeDamage, fight.staggerDamage, "a full blast is enough");
            Assert.AreEqual(BrainFight.AimState.Staggered, fight.Aiming, "it reels");
            Assert.AreEqual(1, fight.Staggers);
            Assert.Less(fight.RingProgress, 0f, "no charge now");
            yield return new WaitForSeconds(fight.staggerSeconds + 0.2f);
            Assert.AreEqual(0, fight.Strikes, "the strike never came");
            Assert.AreEqual(BrainFight.AimState.Tracking, fight.Aiming);
            Assert.Greater(Flat(fight.Aim - tank.transform.position), 3f, "the aim starts far again, unlocked");
        }

        [UnityTest]
        public IEnumerator ItsLife_IsOnABarOfItsOwn()
        {
            // "it also needs hp for the boss".
            fight.trackSpeed = 0f;
            yield return Enter(Middle);
            yield return null;
            BrainHud hud = fight.GetComponent<BrainHud>();
            Assert.AreEqual(1f, hud.LifeShown, 0.01f, "full at the start");
            foreach (BrainNode n in fight.nodes) n.Health.TakeDamage(n.Health.MaxHealth * 0.25f);
            yield return null;
            Assert.AreEqual(fight.LifeShare, hud.LifeShown, 0.01f, "the bar follows its nodes");
            Assert.AreEqual(0.75f, hud.LifeShown, 0.02f);
        }

        [UnityTest]
        public IEnumerator TheLock_GoesToTheBrain_ByDefault_AndBackToItAfterAGrabber()
        {
            // Bruno, 2026-11-08: "lucy should aim at the brain as default; it should by default lock on onto the brain".
            fight.trackSpeed = 0f;
            yield return Enter(Middle + new Vector3(-7f, 0f, -4f)); // the far corner from the brain
            LockOnTargetable brain = fight.nodes.Single().GetComponent<LockOnTargetable>();
            Assert.IsTrue(brain.IsPreferred);
            yield return WaitUntil(() => lockOn.CurrentTarget != null, 2f);
            Assert.AreSame(brain, lockOn.CurrentTarget, "from the far corner, the lock is on the brain");

            Grabber g = fight.Grab();
            yield return WaitUntil(() => g == null || g.Holding, 3f);
            yield return null;
            Assert.AreSame(brain, lockOn.CurrentTarget, "a grabber does not take the lock");
            Assert.IsTrue(lockOn.SwitchTarget(), "the switch reaches the grabber");
            Assert.AreSame(g.GetComponent<LockOnTargetable>(), lockOn.CurrentTarget);
            g.Retract("test");
            yield return WaitUntil(() => g == null, 3f);
            yield return null;
            yield return null;
            Assert.AreSame(brain, lockOn.CurrentTarget, "the grabber gone: back on the brain");
        }

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

        [UnityTest]
        public IEnumerator StandingStill_SlowsHer_AndMovingFreesHer()
        {
            fight.trackSpeed = 0f; // no strike in this test: the aim stays in its corner
            fight.slowSeconds = 0.8f;
            yield return Enter(Middle);
            yield return new WaitForSeconds(fight.stillSeconds + 0.3f);
            Assert.IsTrue(fight.Slowing && tank.SlowWalk, "the floor is brain: still, she slows");
            Vector3 p = Middle;
            float t0 = Time.time;
            while (Time.time - t0 < fight.slowSeconds + 0.4f)
            {
                p += Vector3.right * 0.05f;
                tank.Teleport(p, 90f);
                yield return null;
            }
            Assert.IsFalse(fight.Slowing, "on the move she is free again");
            Assert.IsFalse(tank.SlowWalk);
        }

        [UnityTest]
        public IEnumerator AGrabber_PinsHer_AndABlastFreesHer()
        {
            fight.trackSpeed = 0f; // the aim stays in its corner
            yield return Enter(Middle);
            Grabber g = fight.Grab();
            yield return WaitUntil(() => g != null && g.Holding, 3f);
            Assert.IsTrue(g.Holding, "the grabber reaches her and holds");
            Assert.IsTrue(tank.MovementLocked, "she cannot move");
            powers.Select(PowerKind.Blast);
            powers.FireAt(powers.Selected, 0.5f, g.GetComponent<LockOnTargetable>());
            yield return null;
            Assert.IsTrue(g == null || g.Current == Grabber.State.Retracting, "a blast frees her");
            Assert.IsFalse(tank.MovementLocked, "she moves again");
        }

        [UnityTest]
        public IEnumerator ItsOwnTrack_PlaysFromTheFirstFrame_HitsDoNotChangeIt_AndItEndsAtItsDeath()
        {
            // Bruno, 2026-11-08: "there should be an unique song playing as soon as the fight starts; as it is it keeps on
            // the nomral explortation song until i attack, then it switches to combat ost for a while".
            fight.trackSpeed = 0f;
            var music = GlorifiedBody.Audio.MusicDirector.Instance;
            yield return Enter(Middle);
            yield return WaitUntil(() => music.State == GlorifiedBody.Audio.MusicState.Score, 2f);
            Assert.AreEqual(GlorifiedBody.Audio.MusicState.Score, music.State, "its own track at once");
            Assert.AreEqual("boss", music.CurrentClip != null ? music.CurrentClip.name : "", "the boss track (placeholder)");
            Assert.IsTrue(GlorifiedBody.Audio.TapePlayer.InFight, "it counts as a fight: a tape waits");
            powers.Select(PowerKind.Blast);
            powers.FireAt(powers.Selected, 1f, fight.nodes[0].GetComponent<LockOnTargetable>());
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(GlorifiedBody.Audio.MusicState.Score, music.State, "a hit does not turn it into the combat track");
            fight.nodes[0].Health.TakeDamage(fight.nodes[0].Health.MaxHealth);
            yield return WaitUntil(() => fight.Current == BrainFight.Phase.Dead, 6f);
            yield return WaitUntil(() => music.State != GlorifiedBody.Audio.MusicState.Score, 3f);
            Assert.AreNotEqual(GlorifiedBody.Audio.MusicState.Score, music.State, "at its death the room decides again");
        }

        [UnityTest]
        public IEnumerator AGrabber_SheStepsAside_ItMisses()
        {
            // Bruno, 2026-11-08: "the grabbers are still impossible to dogde".
            fight.trackSpeed = 0f;
            yield return Enter(Middle);
            Grabber g = fight.Grab();
            Assert.AreEqual(Grabber.State.Winding, g.Current, "first the tell at the mouth");
            yield return WaitUntil(() => g.Current == Grabber.State.Reaching, 2f);
            Assert.AreEqual(Grabber.State.Reaching, g.Current, "then the lunge");
            tank.Teleport(tank.transform.position + tank.transform.right * 2.5f, tank.transform.eulerAngles.y); // a side step in the lunge
            yield return WaitUntil(() => g == null || g.Current == Grabber.State.Retracting || g.Holding, 4f);
            Assert.IsFalse(g != null && g.Holding, "she stepped aside: it does not pin her");
            Assert.AreEqual("missed", g != null ? g.Ending : "missed");
            Assert.IsFalse(tank.MovementLocked, "she is free");
        }

        [UnityTest]
        public IEnumerator AGrabber_LetsGo_AfterItsTime()
        {
            fight.trackSpeed = 0f; // the aim stays in its corner
            fight.pinSeconds = 0.6f;
            yield return Enter(Middle);
            Grabber g = fight.Grab();
            yield return WaitUntil(() => g != null && g.Holding, 3f);
            yield return new WaitForSeconds(0.8f);
            Assert.IsTrue(g == null || g.Current == Grabber.State.Retracting);
            Assert.IsFalse(tank.MovementLocked);
        }

        [UnityTest]
        public IEnumerator Orzabal_RipsAGrabberOut_AndThrowsItAtANode()
        {
            fight.trackSpeed = 0f; // the aim stays in its corner
            yield return Enter(Middle);
            Grabber g = fight.Grab();
            yield return WaitUntil(() => g != null && g.Holding, 3f);
            powers.Select(PowerKind.Telekinesis);
            lockOn.SetTarget(g.GetComponent<LockOnTargetable>());
            yield return null;
            pad.Tap(InputManager.GameAction.Charge); // a press lifts at once
            yield return WaitUntil(() => g == null || g.Current == Grabber.State.Ripped, 2f);
            Assert.AreEqual(Grabber.State.Ripped, g.Current, "mk_orzabal tears it out of the wall");
            Assert.IsFalse(tank.MovementLocked, "she is free");
            yield return new WaitForSeconds(0.6f); // it rises
            BrainNode node = fight.nodes.OrderBy(n => Vector3.Distance(n.transform.position, tank.transform.position)).First();
            float hp0 = node.Health.CurrentHealth;
            lockOn.SetTarget(node.GetComponent<LockOnTargetable>());
            yield return null;
            Assert.IsTrue(powers.TryLaunch(1f), "she throws it at the node: " + powers.LastResult);
            yield return WaitUntil(() => node.Health.CurrentHealth < hp0, 3f);
            Assert.Less(node.Health.CurrentHealth, hp0, "the thrown grabber hurts the brain");
        }

        [UnityTest]
        public IEnumerator HerPowers_HurtTheNodes_TheLightsGoOut_AndItsDeathOpensTheExit()
        {
            fight.trackSpeed = 0f; // the aim stays in its corner
            yield return Enter(Middle);
            BrainNode first = fight.nodes[0];
            float hp0 = first.Health.CurrentHealth;
            powers.Select(PowerKind.Blast);
            powers.FireAt(powers.Selected, 1f, first.GetComponent<LockOnTargetable>());
            yield return null;
            Assert.Less(first.Health.CurrentHealth, hp0, "a blast hurts a node");

            foreach (BrainNode n in fight.nodes) n.Health.TakeDamage(n.Health.MaxHealth * 0.5f);
            yield return null;
            yield return null;
            Assert.AreEqual(3, fight.LightsOn, "half its life: half the work lights");
            GameObject leaf = fight.exitLeaf;
            Assert.IsTrue(leaf.activeSelf, "the exit is shut while it lives");
            foreach (BrainNode n in fight.nodes) n.Health.TakeDamage(n.Health.MaxHealth);
            yield return null;
            yield return null;
            Assert.AreEqual(BrainFight.Phase.Dying, fight.Current, "the last node: it dies");
            Assert.AreEqual(6, fight.LightsOn, "every light flares at once");
            Assert.IsTrue(StoryFlags.Has(DeadFlag));
            yield return WaitUntil(() => fight.Current == BrainFight.Phase.Dead, 6f);
            Assert.AreEqual(0, fight.LightsOn, "then they go out one by one");
            Assert.IsFalse(leaf.activeSelf, "the exit opens");
        }

        [UnityTest]
        public IEnumerator ItIsABoss_TooHeavyToLift_NoShort_NoMkEdgar()
        {
            fight.trackSpeed = 0f; // the aim stays in its corner
            yield return Enter(Middle);
            foreach (BrainNode n in fight.nodes)
            {
                LockOnTargetable t = n.GetComponent<LockOnTargetable>();
                Assert.IsTrue(t.IsHeavy && t.IsShockImmune, n.name + ": a boss part (the short skips it, mk_edgar refuses it)");
            }
            BrainNode node = fight.nodes.OrderBy(n => Vector3.Distance(n.transform.position, tank.transform.position)).First();
            tank.Teleport(node.transform.position + (Middle - node.transform.position).normalized * 3f, 0f);
            yield return null;
            powers.Select(PowerKind.Telekinesis);
            lockOn.SetTarget(node.GetComponent<LockOnTargetable>());
            yield return null;
            float mk = stats.Mk(PowerKind.Telekinesis);
            pad.Tap(InputManager.GameAction.Charge);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual("too_heavy", powers.LastResult, "the brain is not lifted");
            Assert.AreEqual(mk, stats.Mk(PowerKind.Telekinesis), 0.01f, "and it costs nothing");
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }

        [DefaultExecutionOrder(-1000)]
        private sealed class PadDriver : MonoBehaviour
        {
            public VirtualPad Pad;
            private void Update() => Pad?.Commit();
        }
    }
}
#endif
