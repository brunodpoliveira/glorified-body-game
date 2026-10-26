#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Story;
using GlorifiedBody.Subtitles;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// The SCRIPT TEST room (2026-10-26, the design sweep's ranks 2 and 4): the scientist's ultimatum of the story
    /// (design §9.3, Bruno's words, placeholder for the writer) as a scripted sequence, a gate a story flag opens, a
    /// trigger that plays the test cutscene, a save machine. No vendor data: in git. ScriptTests drive it.
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.ScriptTestSetup.BuildScriptTest
    /// </summary>
    public static class ScriptTestSetup
    {
        public const string SceneName = "ScriptTest";
        public const string ScenePath = MenuLoopSetup.ScenesFolder + "/" + SceneName + ".unity";
        public const string CutscenesFolder = "Assets/_Game/Resources/Cutscenes";
        public const string TestCutscenePath = CutscenesFolder + "/cutscene_test.asset";
        public const string UltimatumFlag = "ultimatum_done";

        [MenuItem("GlorifiedBody/Build Script Test (sequence, gate, cutscene)")]
        public static void BuildScriptTest()
        {
            CutsceneAsset cutscene = BuildTestCutscene();
            Scene scene = MenuLoopSetup.OpenOrCreate(ScenePath);
            CharacterCatalog catalog = MenuLoopSetup.LoadCatalog(); // after the scene opens (it unloads the prefab links)

            GameObject room = MenuLoopSetup.EnsureRoot("[Room]");
            Piece(room, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(14f, 0.1f, 10f));
            Piece(room, "Wall_North", new Vector3(0f, 1.5f, 5.25f), new Vector3(15f, 3f, 0.5f));
            Piece(room, "Wall_South", new Vector3(0f, 1.5f, -5.25f), new Vector3(15f, 3f, 0.5f));
            Piece(room, "Wall_East", new Vector3(7.25f, 1.5f, 0f), new Vector3(0.5f, 3f, 10f));
            Piece(room, "Wall_West", new Vector3(-7.25f, 1.5f, 0f), new Vector3(0.5f, 3f, 10f));
            MenuLoopSetup.EnsureLight();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.31f, 0.34f);

            MenuLoopSetup.EnsureLucy(catalog, new Vector3(0f, 0f, -3.5f), 0f);
            GameObject scientist = MenuLoopSetup.EnsureDummy("Scientist", new Vector3(0f, 0f, 2.5f), catalog, "labworker");
            scientist.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            MenuLoopSetup.EnsureComponent<Speaker>(scientist).DisplayName = "SCIENTIST";
            GameObject collar = MenuLoopSetup.EnsureChild(scientist, "Collar");
            collar.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            MenuLoopSetup.EnsureComponent<Speaker>(collar).DisplayName = "COMPUTER";

            // The gate: a barrier that the end of the ultimatum takes away (FlagGate shows it while the flag is down).
            GameObject gate = MenuLoopSetup.EnsureRoot("[Gate]");
            FlagGate flagGate = MenuLoopSetup.EnsureComponent<FlagGate>(gate);
            flagGate.flag = UltimatumFlag;
            flagGate.showWhenRaised = false;
            Piece(gate, "Gate_Barrier", new Vector3(5f, 1.5f, 0f), new Vector3(0.5f, 3f, 4f));

            // The ultimatum (design §9.3): the scientist asks his collar, the computer answers, he repeats, then the
            // PA. Words from Bruno's round 8; the writer's lines and the voices come later (the ids name the files).
            ScriptedSequence ultimatum = Sequence("Seq_Ultimatum", UltimatumFlag,
                ScriptStep.LockHer(),
                ScriptStep.Say(scientist, "scientist.computer_subject_has_escaped", "Computer, subject has escaped containment. Request advice."),
                ScriptStep.Say(collar, "computer.with_a_firm_tone", "With a firm tone of voice, demand subject lie down on the ground, hands behind their back."),
                ScriptStep.Say(scientist, "scientist.lie_down_on_the_ground", "Lie down on the ground. Hands behind your back."),
                ScriptStep.Wait(0.8f),
                ScriptStep.Say(scientist, "scientist.subject_did_not_comply", "Subject did not comply."),
                ScriptStep.Say(collar, "computer.approach_and_repeat", "Approach and repeat ultimatum in even firmer tone of voice and add at the end: or else."),
                ScriptStep.FreeHer(),
                ScriptStep.SetShort(90f),
                ScriptStep.PA("pa.code_silver", "Attention. Code silver. Non-security personnel, shelter in place."));
            Trigger("Trigger_Ultimatum", new Vector3(0f, 1f, -1.2f), new Vector3(6f, 2f, 1f), ultimatum);

            ScriptedSequence film = Sequence("Seq_Cutscene", "", ScriptStep.Play(cutscene));
            Trigger("Trigger_Cutscene", new Vector3(-5f, 1f, 3f), new Vector3(2f, 2f, 2f), film);

            SaveAssets.SavePoint("Save_Script", new Vector3(4f, 0f, -3.8f), 0f);

            Camera camera = MenuLoopSetup.EnsureCamera(new Vector3(0f, 6f, -9f), new Vector3(0f, 0f, 0.5f), 55f);
            GameObject camerasRoot = MenuLoopSetup.EnsureRoot("[Cameras]");
            FixedCameraDirector director = MenuLoopSetup.EnsureComponent<FixedCameraDirector>(camerasRoot);
            director.SetDefaultCamera(MenuLoopSetup.EnsureFixedCamera(camerasRoot, "Cam_Wide", camera.transform.position, new Vector3(0f, 0f, 0.5f), 55f));
            MenuLoopSetup.EnsureGameplaySystems();
            MenuLoopSetup.EnsureRuntimeUi();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            MenuLoopSetup.CleanBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[ScriptTestSetup] built ScriptTest and the test cutscene.");
        }

        /// <summary>The test cutscene: two stills with a pan, one video, three lines (one has a clip: test.voice_check).</summary>
        public static CutsceneAsset BuildTestCutscene()
        {
            Directory.CreateDirectory(CutscenesFolder);
            AssetDatabase.Refresh();
            var asset = AssetDatabase.LoadAssetAtPath<CutsceneAsset>(TestCutscenePath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CutsceneAsset>();
                AssetDatabase.CreateAsset(asset, TestCutscenePath);
            }
            asset.cutsceneId = "test";
            asset.music = null;
            asset.shots = new[]
            {
                new CutsceneAsset.Shot
                {
                    still = AssetDatabase.LoadAssetAtPath<Texture2D>(TutorialSetup.ScanImagePath), seconds = 2.5f,
                    speaker = "STEVENS", lineId = "stevens.test_can_you_hear_me", text = "Lucy. Can you hear me? We do not have much time.",
                },
                new CutsceneAsset.Shot
                {
                    still = AssetDatabase.LoadAssetAtPath<Texture2D>(LookTestSetup.TexturesFolder + "/gear_emblem.png"), seconds = 1.5f,
                    panFrom = new Vector3(0.1f, 0.1f, 1.3f), panTo = new Vector3(0f, 0f, 1f),
                    speaker = "LUCY", lineId = "test.voice_check", text = "Who are you?",
                },
                new CutsceneAsset.Shot
                {
                    video = AssetDatabase.LoadAssetAtPath<VideoClip>(TutorialSetup.ScanVideoPath), seconds = 1f,
                    speaker = "STEVENS", lineId = "stevens.test_go_now", text = "Go. Now.",
                },
            };
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static ScriptedSequence Sequence(string name, string doneFlag, params ScriptStep[] steps)
        {
            GameObject obj = GameObject.Find(name) ?? new GameObject(name);
            ScriptedSequence sequence = MenuLoopSetup.EnsureComponent<ScriptedSequence>(obj);
            sequence.doneFlag = doneFlag;
            sequence.steps = new System.Collections.Generic.List<ScriptStep>(steps);
            EditorUtility.SetDirty(sequence);
            return sequence;
        }

        private static void Trigger(string name, Vector3 position, Vector3 size, ScriptedSequence sequence)
        {
            GameObject obj = GameObject.Find(name) ?? new GameObject(name);
            obj.transform.position = position;
            BoxCollider box = MenuLoopSetup.EnsureComponent<BoxCollider>(obj);
            box.isTrigger = true;
            box.size = size;
            obj.layer = LayerMask.NameToLayer("Ignore Raycast");
            MenuLoopSetup.EnsureComponent<ScriptTrigger>(obj).sequence = sequence;
        }

        private static void Piece(GameObject root, string name, Vector3 position, Vector3 scale)
        {
            GameObject obj = MenuLoopSetup.EnsureChildPrimitive(root, name, PrimitiveType.Cube, position, scale);
            obj.isStatic = true;
        }
    }
}
#endif
