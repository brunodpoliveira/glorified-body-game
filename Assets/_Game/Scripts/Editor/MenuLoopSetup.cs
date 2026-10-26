using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GlorifiedBody.Cameras;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.DebugTools;
using GlorifiedBody.Editor.Characters;
using GlorifiedBody.Enemy;
using GlorifiedBody.Interaction;
using GlorifiedBody.Level;
using GlorifiedBody.Level.Build;
using GlorifiedBody.Player;
using GlorifiedBody.Rendering;
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
            ItemAssets.CreateAll();
            EnsureDeathContent();
            BuildTrainingRoomScene();
            BuildMainMenuScene();
            CleanBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MenuLoopSetup] Menu loop scenes built: Main_Menu -> TrainingRoom wired.");
        }

        // ── Training room ─────────────────────────────────────────────

        internal static void BuildTrainingRoomScene()
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

            CharacterCatalog catalog = LoadCatalog();
            EnsureLucy(catalog, Vector3.zero, 0f);

            // The step 1 kill volume (a dark 1 x 2 m box at (4, 1, 0) with a PlayerDeathTestVolume) is gone: Bruno
            // walked into it and died at once (2026-10-21). The bot and the tests kill through Health. The builder
            // opens the existing scene, so the removal is explicit.
            RemoveObject("KillVolume");

            // Two target dummies for the lock-on, with the guard and the soldier models. The FSM AI comes later.
            GameObject dummyA = EnsureDummy("Dummy_A", new Vector3(3f, 0f, 2f), catalog, "guard");
            EnsureComponent<DummyShooter>(dummyA); // the one that shoots, with the telegraph
            EnsureDummy("Dummy_B", new Vector3(-2f, 0f, 4f), catalog, "soldier");

            // The cast along the north wall, facing the room (Bruno, 2026-10-21: all of them in the room now, to
            // test the materials). Visual only: no Health, no collider, the idle on each.
            string[] castIds = { "power_armour", "lucy_guard", "patient", "nurse", "labworker", "fireman" };
            float[] castX = { -4.4f, -3.2f, -0.4f, 0.8f, 2.0f, 3.2f };
            for (int i = 0; i < castIds.Length; i++) EnsureCastFigure(catalog, castIds[i], new Vector3(castX[i], 0f, 4.4f), 180f);

            // Two crates for mk_orzabal (Bruno, 2026-10-22): select it, the target switch reaches them, lift, throw.
            // Both in the wide camera's frame (at z -2.6 the south wall hid the second one).
            PropAssets.Crate("Prop_Crate_1", new Vector3(1.8f, 0f, -1.2f), 15f);
            PropAssets.Crate("Prop_Crate_2", new Vector3(-1.0f, 0f, -1.4f), -20f);

            // Three drugs on the floor, one per power.
            EnsurePickup("Drug_Nalcon", DrugType.Nalcon, new Vector3(-3f, 0.4f, 0f));
            EnsurePickup("Drug_Red", DrugType.Red, new Vector3(-3.5f, 0.4f, 1.2f));
            EnsurePickup("Drug_DFelon", DrugType.DFelon, new Vector3(-2.5f, 0.4f, -1.2f));

            // A save machine and a checkpoint (Bruno, 2026-10-25: saves "only in save rooms", checkpoints "only at
            // specific save points"). The machine stands on the east wall in the wide camera's frame, its screen to
            // the room; the checkpoint is a 1.6 m box in the south-west corner.
            SaveAssets.SavePoint("SavePoint_Training", new Vector3(4.55f, 0f, -2.0f), -90f);
            SaveAssets.Checkpoint("Checkpoint_Training", new Vector3(-4.0f, 0f, -4.0f), 45f, new Vector3(1.6f, 2.5f, 1.6f));

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
            // The zones test her root, which stands on the floor now, so they reach below it.
            EnsureCameraZone(camerasRoot, "Zone_NorthEast", new Vector3(3f, 1f, 3f), new Vector3(4f, 3f, 4f), northEast, 1);
            EnsureCameraZone(camerasRoot, "Zone_SouthWest", new Vector3(-3f, 1f, -3f), new Vector3(4f, 3f, 4f), southWest, 1);

            EnsureGameplaySystems();
            EnsureRuntimeUi();
            EnsureShotPoints(new[] { wide, northEast, southWest });

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MenuLoopSetup] {TrainingRoomScenePath} built.");
        }

        internal static CharacterCatalog LoadCatalog()
        {
            CharacterCatalog catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CreateCharacterAssets.CatalogPath);
            if (catalog == null) Debug.LogWarning("[MenuLoopSetup] no character catalog: run CreateCharacterAssets.BuildAll first. The room gets no models.");
            return catalog;
        }

        /// <summary>
        /// Lucy: the T19 model (lucy_bare) under a root that stands on its feet (y 0), with Health, the tank
        /// controller and the lock-on. The CharacterController is her collider. The capsule placeholder of
        /// step 2 migrates: same object, same components, the primitive mesh and the nose marker go.
        /// </summary>
        internal static GameObject EnsureLucy(CharacterCatalog catalog, Vector3 position, float yaw)
        {
            GameObject player = GameObject.Find("Lucy");
            if (player == null) player = GameObject.Find("Lucy_Placeholder");
            if (player == null) player = new GameObject("Lucy");
            player.name = "Lucy";
            player.tag = "Player";
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            player.transform.localScale = Vector3.one;
            RemoveComponent<Rigidbody>(player);
            RemoveComponent<CapsuleCollider>(player);
            RemoveComponent<MeshRenderer>(player);
            RemoveComponent<MeshFilter>(player);
            Transform nose = player.transform.Find("Nose");
            if (nose != null) Object.DestroyImmediate(nose.gameObject);
            float lucyHeight = catalog != null && catalog.Find("lucy_bare") != null ? catalog.Find("lucy_bare").height : 1.7f;
            CharacterController controller = EnsureComponent<CharacterController>(player);
            controller.height = lucyHeight;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, lucyHeight * 0.5f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;
            controller.minMoveDistance = 0f;
            TankController tankController = EnsureComponent<TankController>(player);
            // The kept component keeps its old numbers, so the dodge tunings land here (Bruno, 2026-10-21).
            var tankSo = new SerializedObject(tankController);
            tankSo.FindProperty("doubleTapWindow").floatValue = TankController.DefaultDoubleTapWindow;
            tankSo.FindProperty("dodgeAimWindow").floatValue = TankController.DefaultDodgeAimWindow;
            tankSo.FindProperty("dodgeBufferTime").floatValue = TankController.DefaultDodgeBufferTime;
            tankSo.ApplyModifiedPropertiesWithoutUndo();
            EnsureComponent<LockOnController>(player);
            EnsureComponent<PsychicStats>(player);
            EnsureComponent<DrugInventory>(player);
            // The builder opens the existing scene, so a kept component keeps its old serialized numbers and a new
            // C# default never lands (the short stayed at 4 s after the change to 8 s, 2026-10-20). ShortController
            // holds only tunings and nothing in the scene points at it, so it is made fresh on every build.
            RemoveComponent<ShortController>(player);
            EnsureComponent<ShortController>(player);
            EnsureComponent<PsychicPowers>(player);
            // The Interact and Scan keys (Bruno, 2026-10-22): the item inventory sized by the outfit, the pickup
            // with its confirmation, the psychic scan.
            EnsureComponent<ItemInventory>(player);
            EnsureComponent<PsychicScanner>(player);
            EnsureComponent<Interactor>(player);
            EnsureCharacterModel(player, catalog, "lucy_bare");
            EnsureComponent<LucyAnimator>(player);
            Health health = EnsureComponent<Health>(player);
            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("maxHealth").floatValue = 100f;
            healthSo.FindProperty("isPlayerHealth").boolValue = true;
            healthSo.FindProperty("destroyOnDeath").boolValue = false;
            // The second wind (Bruno, 2026-10-20): the first lethal hit leaves her at 1 HP, invulnerable 2.5 s, once.
            healthSo.FindProperty("enableSecondWind").boolValue = true;
            healthSo.FindProperty("secondWindInvulnerabilityDuration").floatValue = 2.5f;
            healthSo.ApplyModifiedPropertiesWithoutUndo();
            SetLayer(player, "Player"); // the stencil mark of the look: the dither skips the characters
            return player;
        }

        /// <summary>The systems every playable room needs, on "[GameplaySystems]".</summary>
        internal static void EnsureGameplaySystems()
        {
            GameObject systems = EnsureRoot("[GameplaySystems]");
            EnsureComponent<GameplayInitializer>(systems);
            EnsureComponent<ImpactFrameController>(systems);
            EnsureComponent<PlayerDeathFlowController>(systems);
            EnsureComponent<PoolService>(systems);
            EnsureComponent<DebugKillKey>(systems);
            EnsureComponent<DebugCheatKeys>(systems);
            EnsureComponent<DebugStatusLine>(systems);
            EnsureComponent<LookQualityApplier>(systems); // the LOOK setting (Authentic or Clear) on the room's camera
        }

        /// <summary>The overlay menus and the HUD, on "[RuntimeUI]".</summary>
        internal static void EnsureRuntimeUi()
        {
            GameObject ui = EnsureRoot("[RuntimeUI]");
            EnsureComponent<PauseMenuUI>(ui);
            EnsureComponent<OptionsMenuUI>(ui);
            EnsureComponent<DifficultyMenuUI>(ui);
            EnsureComponent<DeathMenuUI>(ui);
            EnsureComponent<CreditsUI>(ui);
            EnsureComponent<LevelCompleteUI>(ui);
            EnsureComponent<PlayerHud>(ui);
            EnsureComponent<InteractionUI>(ui);
            EnsureComponent<InventoryUI>(ui);
        }

        /// <summary>One ShotPoint per fixed camera, so `tools/unity.sh shots &lt;scene&gt;` renders every view.</summary>
        internal static void EnsureShotPoints(FixedCamera[] cameras)
        {
            GameObject oldShot = GameObject.Find("ShotPoint_fixed_camera");
            if (oldShot != null) Object.DestroyImmediate(oldShot);
            foreach (FixedCamera fixedCamera in cameras)
            {
                GameObject shot = EnsureRoot("ShotPoint_" + fixedCamera.cameraName);
                shot.transform.SetPositionAndRotation(fixedCamera.transform.position, fixedCamera.transform.rotation);
                ShotPoint shotPoint = EnsureComponent<ShotPoint>(shot);
                shotPoint.shotName = fixedCamera.cameraName;
                shotPoint.fieldOfView = fixedCamera.fieldOfView;
            }
        }

        // ── Main menu ─────────────────────────────────────────────────

        internal static void BuildMainMenuScene()
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

        internal static void EnsureDeathContent()
        {
            // The writer's rows and one placeholder for each killer id of the test rooms (DeathScreenSetup).
            DeathScreenSetup.Build();
        }

        // ── Build settings ────────────────────────────────────────────

        internal static void CleanBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.path) == null) continue;
                scenes.Add(entry);
            }

            EnsureBuildScene(scenes, MainMenuScenePath);
            EnsureBuildScene(scenes, TrainingRoomScenePath);
            if (File.Exists(LookTestSetup.ScenePath)) EnsureBuildScene(scenes, LookTestSetup.ScenePath);
            if (File.Exists(LookTestSetup.Scene2Path)) EnsureBuildScene(scenes, LookTestSetup.Scene2Path);
            if (File.Exists(ExteriorLookSetup.ScenePath)) EnsureBuildScene(scenes, ExteriorLookSetup.ScenePath);
            if (File.Exists(TravelTestSetup.PathA)) EnsureBuildScene(scenes, TravelTestSetup.PathA);
            if (File.Exists(TravelTestSetup.PathB)) EnsureBuildScene(scenes, TravelTestSetup.PathB);
            if (File.Exists(ScriptTestSetup.ScenePath)) EnsureBuildScene(scenes, ScriptTestSetup.ScenePath);
            if (File.Exists(KitTestSetup.ScenePath)) EnsureBuildScene(scenes, KitTestSetup.ScenePath);
            if (File.Exists(TutorialSetup.ScenePath)) EnsureBuildScene(scenes, TutorialSetup.ScenePath);
            if (File.Exists(AiTestSetup.ScenePath)) EnsureBuildScene(scenes, AiTestSetup.ScenePath);
            if (File.Exists(Destruction.CoverTestSetup.ScenePath)) EnsureBuildScene(scenes, Destruction.CoverTestSetup.ScenePath);
            if (File.Exists(CivilianTestSetup.ScenePath)) EnsureBuildScene(scenes, CivilianTestSetup.ScenePath);

            // The main menu loads first.
            scenes.Sort((a, b) =>
            {
                int Rank(string path) => path == MainMenuScenePath ? 0 : path == TrainingRoomScenePath ? 1 : 2;
                return Rank(a.path).CompareTo(Rank(b.path));
            });

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        internal static void EnsureBuildScene(List<EditorBuildSettingsScene> scenes, string path)
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

        internal static Scene OpenOrCreate(string path)
        {
            if (File.Exists(path))
            {
                return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            return scene;
        }

        internal static Camera EnsureCamera(Vector3 position, Vector3 lookAt, float fieldOfView)
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

        internal static void EnsureLight()
        {
            if (Object.FindFirstObjectByType<Light>() != null) return;
            var lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        internal static GameObject EnsureRoot(string name)
        {
            GameObject obj = GameObject.Find(name);
            return obj != null ? obj : new GameObject(name);
        }

        internal static GameObject EnsurePrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale)
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

        internal static T EnsureComponent<T>(GameObject obj) where T : Component
        {
            T component = obj.GetComponent<T>();
            return component != null ? component : obj.AddComponent<T>();
        }

        internal static void RemoveComponent<T>(GameObject obj) where T : Component
        {
            T component = obj.GetComponent<T>();
            if (component != null) Object.DestroyImmediate(component);
        }

        internal static void RemoveObject(string name)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null) Object.DestroyImmediate(obj);
        }

        /// <summary>Every transform under the root on the named layer: Player for Lucy, Enemy for the figures (the stencil mark of the look, the physics matrix allows every pair).</summary>
        internal static void SetLayer(GameObject root, string layerName)
        {
            if (root == null) return;
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0) return;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        internal static GameObject EnsureChild(GameObject parent, string name)
        {
            Transform child = parent.transform.Find(name);
            if (child != null) return child.gameObject;
            var obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);
            return obj;
        }

        internal static GameObject EnsureChildPrimitive(GameObject parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale)
        {
            Transform child = parent.transform.Find(name);
            GameObject obj = child != null ? child.gameObject : GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent.transform, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localScale = localScale;
            return obj;
        }

        internal static FixedCamera EnsureFixedCamera(GameObject root, string name, Vector3 position, Vector3 lookAt, float fieldOfView)
        {
            GameObject obj = EnsureChild(root, name);
            obj.transform.position = position;
            obj.transform.LookAt(lookAt);
            FixedCamera fixedCamera = EnsureComponent<FixedCamera>(obj);
            fixedCamera.cameraName = name;
            fixedCamera.fieldOfView = fieldOfView;
            return fixedCamera;
        }

        internal static void EnsureCameraZone(GameObject root, string name, Vector3 center, Vector3 size, FixedCamera fixedCamera, int priority)
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

        /// <summary>
        /// A drug on the floor: a grabbable (Bruno, 2026-10-22: pills are picked up with the Interact key after a
        /// confirmation, not by walking over them), drawn unlit so it stands out, one slot when taken.
        /// </summary>
        internal static void EnsurePickup(string name, DrugType type, Vector3 position)
        {
            ItemAssets.Pickup(null, name, ItemAssets.Drug(type), PrimitiveType.Capsule, position, Vector3.one * 0.3f, Quaternion.Euler(0f, 0f, 90f));
        }

        /// <summary>
        /// A dummy enemy: a root on its feet with a capsule collider the size of the class, Health (50), the
        /// lock-on target at chest height, the class model and its animator. The capsule placeholder migrates.
        /// </summary>
        internal static GameObject EnsureDummy(string name, Vector3 position, CharacterCatalog catalog, string classId)
        {
            GameObject dummy = GameObject.Find(name);
            if (dummy == null) dummy = new GameObject(name);
            dummy.tag = "Enemy";
            dummy.transform.position = position;
            dummy.transform.rotation = Quaternion.LookRotation(new Vector3(-position.x, 0f, -position.z).normalized, Vector3.up);
            dummy.transform.localScale = Vector3.one;
            RemoveComponent<Rigidbody>(dummy);
            RemoveComponent<MeshRenderer>(dummy);
            RemoveComponent<MeshFilter>(dummy);
            CharacterEntry entry = catalog != null ? catalog.Find(classId) : null;
            float height = entry != null ? entry.height : 1.8f;
            CapsuleCollider capsule = EnsureComponent<CapsuleCollider>(dummy);
            capsule.isTrigger = false;
            capsule.height = height;
            capsule.radius = 0.3f;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);

            Health health = EnsureComponent<Health>(dummy);
            var so = new SerializedObject(health);
            so.FindProperty("maxHealth").floatValue = 50f;
            so.ApplyModifiedPropertiesWithoutUndo();
            LockOnTargetable target = EnsureComponent<LockOnTargetable>(dummy);
            var targetSo = new SerializedObject(target);
            targetSo.FindProperty("fallbackHeight").floatValue = Body.ChestHeight;
            targetSo.ApplyModifiedPropertiesWithoutUndo();

            EnsureCharacterModel(dummy, catalog, classId);
            EnsureComponent<EnemyAnimator>(dummy);
            SetLayer(dummy, "Enemy");
            return dummy;
        }

        /// <summary>A figure of the cast with no game role: the model and its idle, for the look.</summary>
        internal static GameObject EnsureCastFigure(CharacterCatalog catalog, string classId, Vector3 position, float yaw)
        {
            string name = "Cast_" + classId;
            GameObject figure = GameObject.Find(name);
            if (figure == null) figure = new GameObject(name);
            figure.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            EnsureCharacterModel(figure, catalog, classId);
            SetLayer(figure, "Enemy");
            return figure;
        }

        /// <summary>The class prefab as the child "Model" of a root. A present child is replaced.</summary>
        internal static GameObject EnsureCharacterModel(GameObject root, CharacterCatalog catalog, string classId)
        {
            Transform old = root.transform.Find("Model");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            CharacterEntry entry = catalog != null ? catalog.Find(classId) : null;
            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"[MenuLoopSetup] no prefab for the class '{classId}': {root.name} has no model.");
                return null;
            }
            var model = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            return model;
        }
    }
}
