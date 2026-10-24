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
using GlorifiedBody.Gore;
using GlorifiedBody.Gore.ThatIndividual;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The gore in a gameplay room (Bruno, 2026-10-24: "gore urp implementation"): the GoreManager and the That
    /// Individual driver start by themselves, a hit bleeds, a gruesome kill bursts the head on Full gore only, and
    /// the Gore option Off stops the blood. The test puts the player's Gore option back after it.
    /// Batch: tools/unity.sh test playmode GoreTests.
    /// </summary>
    public class GoreTests
    {
        private SoldierBrain[] soldiers;
        private int savedLevel;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            savedLevel = SettingsManager.Instance.GoreLevel;
            SettingsManager.Instance.GoreLevel = 2;
            GameManager.Instance.LoadLevel("AiTest");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "AiTest" && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            GameObject.FindGameObjectWithTag("Player").GetComponentInChildren<Health>().SetInvulnerable(true);
            soldiers = UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None).OrderBy(s => s.name).ToArray();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (SettingsManager.Instance != null) SettingsManager.Instance.GoreLevel = savedLevel;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Gore_StartsInTheRoom_WithoutItsDebugKeys()
        {
            yield return null;
            Assert.IsNotNull(GoreManager.Instance, "the GoreManager is in the room");
            Assert.IsNotNull(ThatIndividualGoreDriver.Instance, "the That Individual driver is in the room");
            Assert.AreEqual(GoreTier.Full, GoreManager.Instance.CurrentTier, "the Gore option Full");
            Assert.IsNull(UnityEngine.Object.FindFirstObjectByType<GoreDebugControls>(), "no gore debug keys (F7 to F12 are ours)");
            Assert.IsNull(UnityEngine.Object.FindFirstObjectByType<DismembermentDebugControls>(), "no dismemberment debug keys");
        }

        [UnityTest]
        public IEnumerator Gore_AHitBleeds_TheOptionOffStopsIt()
        {
            ThatIndividualGoreDriver driver = ThatIndividualGoreDriver.Instance;
            Assert.IsNotNull(driver);
            SoldierBrain s = soldiers.First();
            int played = driver.HitsPlayed;
            Hit(s, DamageType.PsychicBlast, 10f, fatal: false);
            yield return null;
            Assert.Greater(driver.HitsPlayed, played, "the blast hit played the vendor blood");
            Assert.Greater(driver.LiveEffects, 0, "a spray is live");

            SettingsManager.Instance.GoreLevel = 0;
            yield return null;
            Assert.AreEqual(GoreTier.Off, GoreManager.Instance.CurrentTier, "the option reached the GoreManager");
            played = driver.HitsPlayed;
            Hit(s, DamageType.PsychicBlast, 10f, fatal: false);
            yield return null;
            Assert.AreEqual(played, driver.HitsPlayed, "Gore Off: no blood");
        }

        [UnityTest]
        public IEnumerator Gore_AGruesomeKill_BurstsTheHead_OnFullOnly()
        {
            SoldierBrain first = soldiers.First(x => x.name == "Soldier_A");
            SoldierBrain second = soldiers.First(x => x.name == "Gasser_D");
            Transform head = first.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
            int bursts = GruesomeDeathFx.HeadsBurst;
            float size = head.TransformVector(Vector3.up).magnitude; // the bone's world size (lossyScale lies on this rig)
            Kill(first, DamageType.ShortShock);
            yield return null;
            yield return null; // after the animator's pass of the next frame
            Debug.Log($"[GoreTests] head {head.name} local={head.localScale.x:0.000} size {size:0.000} -> {head.TransformVector(Vector3.up).magnitude:0.000}");
            Assert.Greater(GruesomeDeathFx.HeadsBurst, bursts, "the head burst");
            Assert.Less(head.TransformVector(Vector3.up).magnitude, size * 0.05f, "the head is gone (the neck is the stump)");
            Assert.AreEqual(GoreHitWeight.FatalHeavy, ThatIndividualGoreRules.Classify(DamageType.ShortShock, 12f, true, 60f), "a death by the short is the full burst");
            Assert.AreEqual(GoreHitWeight.None, ThatIndividualGoreRules.Classify(DamageType.PsychicFire, 3f, false, 60f), "a burn tick does not bleed");

            SettingsManager.Instance.GoreLevel = 1;
            yield return null;
            Transform head2 = second.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
            float scale = head2.TransformVector(Vector3.up).magnitude;
            bursts = GruesomeDeathFx.HeadsBurst;
            Kill(second, DamageType.ShortShock);
            yield return null;
            Assert.AreEqual(bursts, GruesomeDeathFx.HeadsBurst, "Low gore: no head burst");
            Assert.AreEqual(scale, head2.TransformVector(Vector3.up).magnitude, scale * 0.01f, "the head stays");
        }

        private static void Hit(SoldierBrain s, DamageType type, float amount, bool fatal)
        {
            Health h = s.GetComponent<Health>();
            h.TakeDamage(amount);
            HitEventBus.Raise(new HitInfo { Victim = h.gameObject, HitTransform = s.transform, HitPoint = s.transform.position + Vector3.up * 1.2f, HitNormal = -s.transform.forward, DamageAmount = amount, DamageType = type, WasFatal = fatal || h.IsDead });
        }

        private static void Kill(SoldierBrain s, DamageType type)
        {
            Health h = s.GetComponent<Health>();
            Hit(s, type, h.MaxHealth * 10f, fatal: true);
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
