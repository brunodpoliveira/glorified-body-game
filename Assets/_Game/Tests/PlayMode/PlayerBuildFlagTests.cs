#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GlorifiedBody.Systems;

namespace GlorifiedBody.Tests.PlayMode
{
    /// <summary>The dev flag (2026-10-31, rank 16): a release build hides the test rooms of the main menu; dev shows them. START loads the demo.</summary>
    public class PlayerBuildFlagTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameBuild.Force = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator AReleaseBuild_HidesTheTestRooms()
        {
            GameBuild.Force = false;
            yield return Menu();
            GameObject column = Find("TestColumn");
            Assert.IsTrue(column == null || !column.activeInHierarchy, "no test rooms in a release");
            Assert.IsFalse(GameBuild.Dev);
        }

        [UnityTest]
        public IEnumerator ADevBuild_ShowsThem()
        {
            GameBuild.Force = true;
            yield return Menu();
            GameObject column = Find("TestColumn");
            Assert.IsNotNull(column);
            Assert.IsTrue(column.activeInHierarchy, "the test rooms in a dev build");
        }

        [UnityTest]
        public IEnumerator Start_LoadsTheDemo_TrainingRoom_LoadsTheTrainingRoom()
        {
            GameBuild.Force = false;
            foreach ((string label, string scene) in new[] { ("START", "Demo_W1"), ("TRAINING ROOM", "TrainingRoom") })
            {
                yield return Menu();
                UnityEngine.UI.Button button = null;
                foreach (UnityEngine.UI.Button b in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
                {
                    UnityEngine.UI.Text text = b.GetComponentInChildren<UnityEngine.UI.Text>();
                    if (text != null && text.text == label && b.gameObject.activeInHierarchy) button = b;
                }
                Assert.IsNotNull(button, label + " in a release menu");
                button.onClick.Invoke();
                float t0 = Time.realtimeSinceStartup;
                while ((SceneManager.GetActiveScene().name != scene || !GameManager.Instance.IsPlaying) && Time.realtimeSinceStartup - t0 < 10f) yield return null;
                Assert.AreEqual(scene, SceneManager.GetActiveScene().name, label);
            }
        }

        private static IEnumerator Menu()
        {
            SceneManager.LoadScene("Main_Menu");
            float t0 = Time.realtimeSinceStartup;
            while ((SceneManager.GetActiveScene().name != "Main_Menu" || GameManager.Instance == null || !GameManager.Instance.IsInMenu) && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            yield return null;
            yield return null;
        }

        private static GameObject Find(string name)
        {
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name && t.gameObject.scene.IsValid()) return t.gameObject;
            return null;
        }
    }
}
#endif
