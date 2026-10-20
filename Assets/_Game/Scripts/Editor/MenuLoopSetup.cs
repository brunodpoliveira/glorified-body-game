using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Level;
using GlorifiedBody.Level.Build;
using GlorifiedBody.Pooling;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// Builds the step-1 menu loop from code, never from hand-edited YAML:
    /// Main_Menu.unity (managers + MainMenuUI) and TrainingRoom.unity (a greybox floor, one fixed camera,
    /// a placeholder Lucy with Health, the gameplay systems and the overlay menus).
    /// Idempotent: an existing scene is opened and completed, so its GUID in Build Settings stays valid.
    /// Menu: GlorifiedBody > Build Menu Loop Scenes.
    /// Batch: tools/unity.sh exec GlorifiedBody.Editor.MenuLoopSetup.BuildMenuLoopScenes
    /// </summary>
    public static class MenuLoopSetup
    {
        public const string ScenesFolder = "Assets/_Game/Scenes";
        public const string MainMenuScenePath = ScenesFolder + "/Main_Menu.unity";
        public const string TrainingRoomScenePath = ScenesFolder + "/TrainingRoom.unity";
        public const string ResourcesFolder = "Assets/_Game/Resources";
        public const string DeathContentPath = ResourcesFolder + "/" + DeathMenuUI.ContentResourceName + ".asset";

        [MenuItem("GlorifiedBody/Build Menu Loop Scenes")]
        public static void BuildMenuLoopScenes()
        {
            Directory.CreateDirectory(ScenesFolder);
            Directory.CreateDirectory(ResourcesFolder);
            AssetDatabase.Refresh();

            EnsureDeathContent();
            BuildTrainingRoomScene();
            BuildMainMenuScene();
            CleanBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MenuLoopSetup] Menu loop scenes built: Main_Menu -> TrainingRoom wired.");
        }

        // ── Training room ─────────────────────────────────────────────

        private static void BuildTrainingRoomScene()
        {
            Scene scene = OpenOrCreate(TrainingRoomScenePath);

            Camera camera = EnsureCamera(new Vector3(0f, 7f, -9f), new Vector3(0f, 1f, 0f), 50f);
            EnsureLight();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.33f, 0.36f);

            GameObject floor = EnsurePrimitive("Floor", PrimitiveType.Plane, Vector3.zero, Vector3.one);
            floor.isStatic = true;
            EnsurePrimitive("Wall_North", PrimitiveType.Cube, new Vector3(0f, 1.5f, 5f), new Vector3(10f, 3f, 0.2f)).isStatic = true;
            EnsurePrimitive("Wall_South", PrimitiveType.Cube, new Vector3(0f, 1.5f, -5f), new Vector3(10f, 3f, 0.2f)).isStatic = true;
            EnsurePrimitive("Wall_East", PrimitiveType.Cube, new Vector3(5f, 1.5f, 0f), new Vector3(0.2f, 3f, 10f)).isStatic = true;
            EnsurePrimitive("Wall_West", PrimitiveType.Cube, new Vector3(-5f, 1.5f, 0f), new Vector3(0.2f, 3f, 10f)).isStatic = true;

            // Lucy placeholder: a capsule with Health. The step-2 controller replaces the capsule.
            GameObject player = EnsurePrimitive("Lucy_Placeholder", PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), Vector3.one);
            player.tag = "Player";
            Rigidbody body = EnsureComponent<Rigidbody>(player);
            body.isKinematic = true;
            body.useGravity = false;
            Health health = EnsureComponent<Health>(player);
            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("maxHealth").floatValue = 100f;
            healthSo.FindProperty("isPlayerHealth").boolValue = true;
            healthSo.FindProperty("destroyOnDeath").boolValue = false;
            healthSo.FindProperty("enableSecondWind").boolValue = false;
            healthSo.ApplyModifiedPropertiesWithoutUndo();

            // A kill volume for walking tests once the controller exists (step 2).
            GameObject killVolume = EnsurePrimitive("KillVolume", PrimitiveType.Cube, new Vector3(4f, 1f, 0f), new Vector3(1f, 2f, 1f));
            killVolume.GetComponent<Collider>().isTrigger = true;
            PlayerDeathTestVolume volume = EnsureComponent<PlayerDeathTestVolume>(killVolume);
            var volumeSo = new SerializedObject(volume);
            volumeSo.FindProperty("bypassSecondWind").boolValue = true;
            volumeSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject systems = EnsureRoot("[GameplaySystems]");
            EnsureComponent<GameplayInitializer>(systems);
            EnsureComponent<ImpactFrameController>(systems);
            EnsureComponent<PlayerDeathFlowController>(systems);
            EnsureComponent<PoolService>(systems);
            EnsureComponent<DebugKillKey>(systems);

            GameObject ui = EnsureRoot("[RuntimeUI]");
            EnsureComponent<PauseMenuUI>(ui);
            EnsureComponent<OptionsMenuUI>(ui);
            EnsureComponent<DifficultyMenuUI>(ui);
            EnsureComponent<DeathMenuUI>(ui);
            EnsureComponent<CreditsUI>(ui);
            EnsureComponent<LevelCompleteUI>(ui);

            GameObject shot = EnsureRoot("ShotPoint_fixed_camera");
            shot.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
            ShotPoint shotPoint = EnsureComponent<ShotPoint>(shot);
            shotPoint.shotName = "fixed_camera";
            shotPoint.fieldOfView = camera.fieldOfView;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MenuLoopSetup] {TrainingRoomScenePath} built.");
        }

        // ── Main menu ─────────────────────────────────────────────────

        private static void BuildMainMenuScene()
        {
            Scene scene = OpenOrCreate(MainMenuScenePath);

            EnsureCamera(new Vector3(0f, 1f, -10f), new Vector3(0f, 1f, 0f), 60f);
            EnsureLight();

            GameObject managers = EnsureRoot("[GameManagers]");
            EnsureComponent<GameManager>(managers);
            EnsureComponent<InputManager>(managers);
            EnsureComponent<DifficultyManager>(managers);
            EnsureComponent<SettingsManager>(managers);

            GameObject menuHost = EnsureRoot("[MainMenu]");
            EnsureComponent<MainMenuUI>(menuHost);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MenuLoopSetup] {MainMenuScenePath} built.");
        }

        // ── Death screen content ──────────────────────────────────────

        private static void EnsureDeathContent()
        {
            if (AssetDatabase.LoadAssetAtPath<DeathScreenContent>(DeathContentPath) != null) return;
            var content = ScriptableObject.CreateInstance<DeathScreenContent>();
            AssetDatabase.CreateAsset(content, DeathContentPath);
            Debug.Log($"[MenuLoopSetup] placeholder death screen content created at {DeathContentPath}.");
        }

        // ── Build settings ────────────────────────────────────────────

        private static void CleanBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.path) == null) continue;
                scenes.Add(entry);
            }

            EnsureBuildScene(scenes, MainMenuScenePath);
            EnsureBuildScene(scenes, TrainingRoomScenePath);

            // The main menu loads first.
            scenes.Sort((a, b) =>
            {
                int Rank(string path) => path == MainMenuScenePath ? 0 : path == TrainingRoomScenePath ? 1 : 2;
                return Rank(a.path).CompareTo(Rank(b.path));
            });

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureBuildScene(List<EditorBuildSettingsScene> scenes, string path)
        {
            foreach (EditorBuildSettingsScene entry in scenes)
            {
                if (entry.path == path)
                {
                    entry.enabled = true;
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static Scene OpenOrCreate(string path)
        {
            if (File.Exists(path))
            {
                return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            return scene;
        }

        private static Camera EnsureCamera(Vector3 position, Vector3 lookAt, float fieldOfView)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObj = new GameObject("Main Camera");
                cameraObj.tag = "MainCamera";
                camera = cameraObj.AddComponent<Camera>();
                cameraObj.AddComponent<AudioListener>();
                cameraObj.transform.position = position;
                cameraObj.transform.LookAt(lookAt);
                camera.fieldOfView = fieldOfView;
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.05f, 0.06f);
            return camera;
        }

        private static void EnsureLight()
        {
            if (Object.FindFirstObjectByType<Light>() != null) return;
            var lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static GameObject EnsureRoot(string name)
        {
            GameObject obj = GameObject.Find(name);
            return obj != null ? obj : new GameObject(name);
        }

        private static GameObject EnsurePrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale)
        {
            GameObject obj = GameObject.Find(name);
            if (obj == null)
            {
                obj = GameObject.CreatePrimitive(type);
                obj.name = name;
            }

            obj.transform.position = position;
            obj.transform.localScale = scale;
            return obj;
        }

        private static T EnsureComponent<T>(GameObject obj) where T : Component
        {
            T component = obj.GetComponent<T>();
            return component != null ? component : obj.AddComponent<T>();
        }
    }
}
