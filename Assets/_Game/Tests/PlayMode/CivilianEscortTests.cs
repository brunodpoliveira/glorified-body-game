#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;
using GlorifiedBody.Player;
using GlorifiedBody.Saving;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>
    /// The escort (round 160, DEMO_PLAN §3.3 item 8): a soldier in a fight takes a living civilian near her out of danger,
    /// walking backward between her and the civilian, his free hand on its shoulder. In the KIT TEST room. Batch:
    /// tools/unity.sh test playmode CivilianEscortTests.
    /// </summary>
    public class CivilianEscortTests
    {
        private const string Room = "KitTest";
        private GameObject lucy;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SaveSystem.Folder = Path.Combine(Application.temporaryCachePath, "gb_escort_tests");
            if (Directory.Exists(SaveSystem.Folder)) Directory.Delete(SaveSystem.Folder, true);
            SceneManager.LoadScene("Main_Menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Main_Menu" && GameManager.Instance != null && GameManager.Instance.IsInMenu, 10f);
            GameManager.Instance.LoadLevel(Room);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == Room && GameManager.Instance.IsPlaying, 10f);
            yield return null;
            lucy = GameObject.FindGameObjectWithTag("Player");
            lucy.GetComponentInChildren<Health>().SetInvulnerable(true);
            Bark.ResetVoices();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SaveSystem.Folder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ASoldier_LeadsACivilianAwayFromHer_FacingHer_HisFreeHandOnIt()
        {
            Assert.IsNull(GameObject.Find("Guard_K3").GetComponent<CivilianEscort>(), "only the soldiers escort");
            GameObject lab = GameObject.Find("Lab_K4");
            GameObject soldier = GameObject.Find("Soldier_K1");
            CivilianEscort escort = soldier.GetComponent<CivilianEscort>();
            Assert.IsNotNull(escort, "a soldier can escort");
            lucy.GetComponent<TankController>().Teleport(lab.transform.position + new Vector3(0f, 0f, -5f), 0f);
            Assert.IsTrue(NavMesh.SamplePosition(lab.transform.position + new Vector3(3f, 0f, 0f), out NavMeshHit near, 1.5f, NavMesh.AllAreas));
            soldier.GetComponent<NavMeshAgent>().Warp(near.position); // within his reach of the civilian
            yield return null;
            foreach (SoldierBrain b in UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None)) b.Alert("test", false);
            float startGap = Flat(lab.transform.position, lucy.transform.position);
            yield return WaitUntil(() => escort.Escorts > 0 && escort.Civilian != null && escort.Civilian.gameObject == lab, 10f);
            Assert.AreEqual(lab, escort.Civilian != null ? escort.Civilian.gameObject : null, "he takes the lab worker near her");
            Assert.AreEqual(CivilianBrain.Phase.Care, lab.GetComponent<CivilianBrain>().Current, "the civilian goes with him");
            yield return WaitUntil(() => escort.Current == CivilianEscort.Step.Lead, 6f);
            Assert.AreEqual(CivilianEscort.Step.Lead, escort.Current, "he reaches it and leads it");
            yield return new WaitForSeconds(1.2f);
            if (escort.Current == CivilianEscort.Step.Lead)
            {
                EnemyGun gun = soldier.GetComponent<EnemyGun>();
                Animator a = soldier.GetComponent<Animator>();
                Vector3 hand = a.GetBoneTransform(HumanBodyBones.LeftHand).position;
                Assert.IsTrue(gun.FreeHandTo.HasValue, "his free hand goes to the civilian");
                float reach = Vector3.Distance(hand, gun.FreeHandTo.Value);
                Transform up = a.GetBoneTransform(HumanBodyBones.LeftUpperArm), low = a.GetBoneTransform(HumanBodyBones.LeftLowerArm), lh = a.GetBoneTransform(HumanBodyBones.LeftHand);
                float arm = Vector3.Distance(up.position, low.position) + Vector3.Distance(low.position, lh.position);
                Debug.Log($"[CivilianEscortTests] the arm: {arm:0.00} m long, the target {Vector3.Distance(up.position, gun.FreeHandTo.Value):0.00} m from the shoulder, the hand {Vector3.Distance(up.position, hand):0.00} m from it");
                float facing = Vector3.Angle(Vector3.ProjectOnPlane(soldier.transform.forward, Vector3.up), Vector3.ProjectOnPlane(lucy.transform.position - soldier.transform.position, Vector3.up));
                float between = Flat(soldier.transform.position, lucy.transform.position) - Flat(lab.transform.position, lucy.transform.position);
                Debug.Log($"[CivilianEscortTests] the lead: hand {reach:0.00} m from the shoulder, facing her within {facing:0} degrees, he is {-between:0.00} m nearer to her than the civilian, gun on his back {soldier.GetComponent<EnemyGun>().Model.parent != a.GetBoneTransform(HumanBodyBones.RightHand)}");
                Assert.Less(reach, 0.35f, "the hand on the shoulder");
                Assert.Less(facing, 60f, "he faces her");
                Assert.Less(between, 0.3f, "he stays between her and the civilian");
            }
            yield return WaitUntil(() => !escort.Active, 20f);
            float endGap = Flat(lab.transform.position, lucy.transform.position);
            Debug.Log($"[CivilianEscortTests] the escort: delivered {escort.Delivered}, the civilian from {startGap:0.0} m to {endGap:0.0} m from her, his lines {Bark.CountOf(soldier)}");
            Assert.IsFalse(escort.Active, "it ends");
            Assert.AreEqual(1, escort.Delivered, "the civilian is safe");
            Assert.Greater(endGap, startGap + 2.5f, "further from her");
            Assert.AreNotEqual(CivilianBrain.Phase.Care, lab.GetComponent<CivilianBrain>().Current, "the civilian is free again");
            Assert.AreNotEqual(SoldierBrain.Phase.Care, soldier.GetComponent<SoldierBrain>().Current, "he goes back to the fight");
        }

        [UnityTest]
        public IEnumerator AHurt_EndsTheEscort()
        {
            GameObject lab = GameObject.Find("Lab_K4");
            GameObject soldier = GameObject.Find("Soldier_K1");
            CivilianEscort escort = soldier.GetComponent<CivilianEscort>();
            lucy.GetComponent<TankController>().Teleport(lab.transform.position + new Vector3(0f, 0f, -5f), 0f);
            Assert.IsTrue(NavMesh.SamplePosition(lab.transform.position + new Vector3(3f, 0f, 0f), out NavMeshHit near, 1.5f, NavMesh.AllAreas));
            soldier.GetComponent<NavMeshAgent>().Warp(near.position);
            yield return null;
            foreach (SoldierBrain b in UnityEngine.Object.FindObjectsByType<SoldierBrain>(FindObjectsSortMode.None)) b.Alert("test", false);
            yield return WaitUntil(() => escort.Active, 10f);
            Assert.IsTrue(escort.Active);
            soldier.GetComponent<Health>().TakeDamage(5f);
            yield return null;
            Assert.IsFalse(escort.Active, "a hurt ends it");
            Assert.AreNotEqual(CivilianBrain.Phase.Care, lab.GetComponent<CivilianBrain>().Current, "the civilian is free again");
        }

        private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeoutSeconds) yield return null;
        }
    }
}
#endif
