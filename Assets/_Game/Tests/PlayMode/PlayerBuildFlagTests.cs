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
        public IEnumerator Start_LoadsTheDemo_AndAReleaseHasNoTrainingRoom()
        {
            GameBuild.Force = false;
            yield return Menu();
            // Bruno, 2026-11-02: "remove the training room, or set it to dev only".
            Assert.IsNull(Button("TRAINING ROOM"), "no training room in a release menu");
            UnityEngine.UI.Button start = Button("START");
            Assert.IsNotNull(start, "START in a release menu");
            start.onClick.Invoke();
            float t0 = Time.realtimeSinceStartup;
            while ((SceneManager.GetActiveScene().name != "Demo_W1" || !GameManager.Instance.IsPlaying) && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            Assert.AreEqual("Demo_W1", SceneManager.GetActiveScene().name, "START");
        }

        [UnityTest]
        public IEnumerator ADevBuild_TrainingRoom_LoadsTheTrainingRoom_FromTheTestColumn()
        {
            GameBuild.Force = true;
            yield return Menu();
            UnityEngine.UI.Button button = Button("TRAINING ROOM");
            Assert.IsNotNull(button, "TRAINING ROOM in a dev menu");
            Assert.AreEqual("TestColumn", button.transform.parent.name, "with the test rooms");
            button.onClick.Invoke();
            float t0 = Time.realtimeSinceStartup;
            while ((SceneManager.GetActiveScene().name != "TrainingRoom" || !GameManager.Instance.IsPlaying) && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            Assert.AreEqual("TrainingRoom", SceneManager.GetActiveScene().name);
        }

        [UnityTest]
        public IEnumerator TheDebugStatusLine_ShowsOnlyInDev()
        {
            foreach (bool dev in new[] { false, true })
            {
                GameBuild.Force = dev;
                yield return Menu();
                GameManager.Instance.LoadGameplay();
                float t0 = Time.realtimeSinceStartup;
                while ((SceneManager.GetActiveScene().name != "TrainingRoom" || !GameManager.Instance.IsPlaying) && Time.realtimeSinceStartup - t0 < 10f) yield return null;
                yield return null;
                yield return null;
                GameObject line = Find("DebugStatusCanvas");
                Assert.IsNotNull(line, "the status line exists in the training room");
                Assert.AreEqual(dev, line.activeInHierarchy, dev ? "a dev build shows it" : "a release hides it (the web build showed it, 2026-10-31)");
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

        /// <summary>The active button with this label, or null.</summary>
        private static UnityEngine.UI.Button Button(string label)
        {
            foreach (UnityEngine.UI.Button b in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            {
                UnityEngine.UI.Text text = b.GetComponentInChildren<UnityEngine.UI.Text>();
                if (text != null && text.text == label && b.gameObject.activeInHierarchy) return b;
            }
            return null;
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
