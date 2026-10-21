using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Cameras;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Enemy;
using GlorifiedBody.Level;
using GlorifiedBody.Level.Build;
using GlorifiedBody.Player;
using GlorifiedBody.Pooling;
using GlorifiedBody.Systems;
using GlorifiedBody.UI;

namespace GlorifiedBody.Editor
{
    /// <summary>
    /// Builds the step-1 menu loop from code, never from hand-edited YAML:
    /// Main_Menu.unity (managers + MainMenuUI) and TrainingRoom.unity (a greybox room, three fixed cameras with
    /// two camera zones, a placeholder Lucy with the tank controller and the lock-on, two target dummies, the
    /// gameplay systems and the overlay menus).
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

            CreatePowerAssets.CreateAll();
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
            // Walls 0.5 m thick: a run step at a low batch frame rate must not pass through them.
            EnsurePrimitive("Wall_North", PrimitiveType.Cube, new Vector3(0f, 1.5f, 5.15f), new Vector3(10.6f, 3f, 0.5f)).isStatic = true;
            EnsurePrimitive("Wall_South", PrimitiveType.Cube, new Vector3(0f, 1.5f, -5.15f), new Vector3(10.6f, 3f, 0.5f)).isStatic = true;
            EnsurePrimitive("Wall_East", PrimitiveType.Cube, new Vector3(5.15f, 1.5f, 0f), new Vector3(0.5f, 3f, 10.6f)).isStatic = true;
            EnsurePrimitive("Wall_West", PrimitiveType.Cube, new Vector3(-5.15f, 1.5f, 0f), new Vector3(0.5f, 3f, 10.6f)).isStatic = true;

            // Lucy placeholder: a capsule with Health, the tank controller and the lock-on. The CharacterController
            // is her collider, so the primitive's capsule collider and any rigidbody go.
            GameObject player = EnsurePrimitive("Lucy_Placeholder", PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), Vector3.one);
            player.tag = "Player";
            RemoveComponent<Rigidbody>(player);
            RemoveComponent<CapsuleCollider>(player);
            CharacterController controller = EnsureComponent<CharacterController>(player);
            controller.height = 2f;
            controller.radius = 0.4f;
            controller.center = Vector3.zero;
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;
            controller.minMoveDistance = 0f;
            EnsureComponent<TankController>(player);
            EnsureComponent<LockOnController>(player);
            EnsureComponent<PsychicStats>(player);
            EnsureComponent<DrugInventory>(player);
            // The builder opens the existing scene, so a kept component keeps its old serialized numbers and a new
            // C# default never lands (the short stayed at 4 s after the change to 8 s, 2026-10-20). ShortController
            // holds only tunings and nothing in the scene points at it, so it is made fresh on every build.
            RemoveComponent<ShortController>(player);
            EnsureComponent<ShortController>(player);
            EnsureComponent<PsychicPowers>(player);
            GameObject nose = EnsureChildPrimitive(player, "Nose", PrimitiveType.Cube, new Vector3(0f, 0.6f, 0.5f), Vector3.one * 0.2f);
            RemoveComponent<BoxCollider>(nose);
            Health health = EnsureComponent<Health>(player);
            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("maxHealth").floatValue = 100f;
            healthSo.FindProperty("isPlayerHealth").boolValue = true;
            healthSo.FindProperty("destroyOnDeath").boolValue = false;
            // The second wind (Bruno, 2026-10-20): the first lethal hit leaves her at 1 HP, invulnerable 2.5 s, once.
            healthSo.FindProperty("enableSecondWind").boolValue = true;
            healthSo.FindProperty("secondWindInvulnerabilityDuration").floatValue = 2.5f;
            healthSo.ApplyModifiedPropertiesWithoutUndo();

            // A kill volume for walking tests once the controller exists (step 2).
            GameObject killVolume = EnsurePrimitive("KillVolume", PrimitiveType.Cube, new Vector3(4f, 1f, 0f), new Vector3(1f, 2f, 1f));
            killVolume.GetComponent<Collider>().isTrigger = true;
            PlayerDeathTestVolume volume = EnsureComponent<PlayerDeathTestVolume>(killVolume);
            var volumeSo = new SerializedObject(volume);
            volumeSo.FindProperty("bypassSecondWind").boolValue = true;
            volumeSo.ApplyModifiedPropertiesWithoutUndo();

            // Two target dummies for the lock-on. Step 3 replaces them with the Invector dummy enemy.
            GameObject dummyA = EnsurePrimitive("Dummy_A", PrimitiveType.Capsule, new Vector3(3f, 1f, 2f), Vector3.one);
            SetupDummy(dummyA);
            EnsureComponent<DummyShooter>(dummyA); // the one that shoots, with the telegraph
            SetupDummy(EnsurePrimitive("Dummy_B", PrimitiveType.Capsule, new Vector3(-2f, 1f, 4f), Vector3.one));

            // Three drugs on the floor, one per power.
            EnsurePickup("Drug_Nalcon", DrugType.Nalcon, new Vector3(-3f, 0.4f, 0f));
            EnsurePickup("Drug_Red", DrugType.Red, new Vector3(-3.5f, 0.4f, 1.2f));
            EnsurePickup("Drug_DFelon", DrugType.DFelon, new Vector3(-2.5f, 0.4f, -1.2f));

            // Fixed cameras: the wide default, plus two corner cameras with their zones.
            // Cam_SouthWest sits in the south-east corner and looks west along the south wall (Bruno, 2026-10-20:
            // "no camera angle cuts the dummy target"). From its zone both dummies are outside the frame at 16:9
            // and at 4:3, so the HUD edge arrow shows there, and Lucy stays in the frame over the whole zone.
            GameObject camerasRoot = EnsureRoot("[Cameras]");
            FixedCameraDirector director = EnsureComponent<FixedCameraDirector>(camerasRoot);
            FixedCamera wide = EnsureFixedCamera(camerasRoot, "Cam_Wide", camera.transform.position, new Vector3(0f, 1f, 0f), 50f);
            FixedCamera northEast = EnsureFixedCamera(camerasRoot, "Cam_NorthEast", new Vector3(4.3f, 3.4f, 4.3f), new Vector3(0f, 1f, 0f), 55f);
            FixedCamera southWest = EnsureFixedCamera(camerasRoot, "Cam_SouthWest", new Vector3(4.6f, 2.8f, -4.6f), new Vector3(-3.0f, 0.9f, -3.4f), 50f);
            director.SetDefaultCamera(wide);
            EnsureCameraZone(camerasRoot, "Zone_NorthEast", new Vector3(3f, 1.5f, 3f), new Vector3(4f, 3f, 4f), northEast, 1);
            EnsureCameraZone(camerasRoot, "Zone_SouthWest", new Vector3(-3f, 1.5f, -3f), new Vector3(4f, 3f, 4f), southWest, 1);

            GameObject systems = EnsureRoot("[GameplaySystems]");
            EnsureComponent<GameplayInitializer>(systems);
            EnsureComponent<ImpactFrameController>(systems);
            EnsureComponent<PlayerDeathFlowController>(systems);
            EnsureComponent<PoolService>(systems);
            EnsureComponent<DebugKillKey>(systems);
            EnsureComponent<DebugCheatKeys>(systems);
            EnsureComponent<DebugStatusLine>(systems);

            GameObject ui = EnsureRoot("[RuntimeUI]");
            EnsureComponent<PauseMenuUI>(ui);
            EnsureComponent<OptionsMenuUI>(ui);
            EnsureComponent<DifficultyMenuUI>(ui);
            EnsureComponent<DeathMenuUI>(ui);
            EnsureComponent<CreditsUI>(ui);
            EnsureComponent<LevelCompleteUI>(ui);
            EnsureComponent<PlayerHud>(ui);

            // One ShotPoint per fixed camera, so `tools/unity.sh shots TrainingRoom` renders every view.
            GameObject oldShot = GameObject.Find("ShotPoint_fixed_camera");
            if (oldShot != null) Object.DestroyImmediate(oldShot);
            foreach (FixedCamera fixedCamera in new[] { wide, northEast, southWest })
            {
                GameObject shot = EnsureRoot("ShotPoint_" + fixedCamera.cameraName);
                shot.transform.SetPositionAndRotation(fixedCamera.transform.position, fixedCamera.transform.rotation);
                ShotPoint shotPoint = EnsureComponent<ShotPoint>(shot);
                shotPoint.shotName = fixedCamera.cameraName;
                shotPoint.fieldOfView = fixedCamera.fieldOfView;
            }

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
            var content = AssetDatabase.LoadAssetAtPath<DeathScreenContent>(DeathContentPath);
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<DeathScreenContent>();
                AssetDatabase.CreateAsset(content, DeathContentPath);
                Debug.Log($"[MenuLoopSetup] placeholder death screen content created at {DeathContentPath}.");
            }

            // One entry per enemy, placeholder words until the writer delivers them.
            if (content.Find("dummy_shooter") == content.fallback)
            {
                content.entries.Add(new DeathScreenContent.Entry
                {
                    killerId = "dummy_shooter",
                    hint = "PLACEHOLDER HINT: step back when the circle closes, then charge."
                });
                EditorUtility.SetDirty(content);
            }
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

        private static void RemoveComponent<T>(GameObject obj) where T : Component
        {
            T component = obj.GetComponent<T>();
            if (component != null) Object.DestroyImmediate(component);
        }

        private static GameObject EnsureChild(GameObject parent, string name)
        {
            Transform child = parent.transform.Find(name);
            if (child != null) return child.gameObject;
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            return obj;
        }

        private static GameObject EnsureChildPrimitive(GameObject parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale)
        {
            Transform child = parent.transform.Find(name);
            GameObject obj = child != null ? child.gameObject : GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent.transform, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localScale = localScale;
            return obj;
        }

        private static FixedCamera EnsureFixedCamera(GameObject root, string name, Vector3 position, Vector3 lookAt, float fieldOfView)
        {
            GameObject obj = EnsureChild(root, name);
            obj.transform.position = position;
            obj.transform.LookAt(lookAt);
            FixedCamera fixedCamera = EnsureComponent<FixedCamera>(obj);
            fixedCamera.cameraName = name;
            fixedCamera.fieldOfView = fieldOfView;
            return fixedCamera;
        }

        private static void EnsureCameraZone(GameObject root, string name, Vector3 center, Vector3 size, FixedCamera fixedCamera, int priority)
        {
            GameObject obj = EnsureChild(root, name);
            obj.transform.position = center;
            BoxCollider box = EnsureComponent<BoxCollider>(obj);
            box.isTrigger = true;
            box.size = size;
            box.center = Vector3.zero;
            CameraZone zone = EnsureComponent<CameraZone>(obj);
            zone.fixedCamera = fixedCamera;
            zone.priority = priority;
        }

        private static void EnsurePickup(string name, DrugType type, Vector3 position)
        {
            GameObject obj = EnsurePrimitive(name, PrimitiveType.Capsule, position, Vector3.one * 0.3f);
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null) collider.isTrigger = true;
            DrugPickup pickup = EnsureComponent<DrugPickup>(obj);
            pickup.drugType = type;
            pickup.amount = 1;
        }

        private static void SetupDummy(GameObject dummy)
        {
            dummy.tag = "Enemy";
            Health health = EnsureComponent<Health>(dummy);
            var so = new SerializedObject(health);
            so.FindProperty("maxHealth").floatValue = 50f;
            so.ApplyModifiedPropertiesWithoutUndo();
            LockOnTargetable target = EnsureComponent<LockOnTargetable>(dummy);
            var targetSo = new SerializedObject(target);
            targetSo.FindProperty("fallbackHeight").floatValue = 0.6f;
            targetSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
