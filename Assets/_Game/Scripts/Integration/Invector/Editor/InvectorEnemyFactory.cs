#if UNITY_EDITOR
using System.Collections.Generic;
using Invector.vCharacterController.AI;
using Invector.vCharacterController.AI.FSMBehaviour;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using GlorifiedBody.Characters;
using GlorifiedBody.Combat;
using GlorifiedBody.Core;
using GlorifiedBody.Enemy;

namespace GlorifiedBody.Integration.Invector.Build
{
    /// <summary>
    /// Builds the FSM soldiers from editor code (rule 6: Invector types stay in this namespace).
    ///
    /// The FSM asset (FSM_GB_Soldier) opens in Invector's FSM window: Idle (find the target) -> Engage (Move Or
    /// Shoot, our SoldierBrain) when the soldier is alerted (it saw Lucy, took a hit, heard an alert), back to Idle
    /// when it is not. The builder writes it again on every run.
    ///
    /// A soldier is the Invector AI body made the way Invector's "Create AI" window does it (vControlAI, its
    /// primary components, the FSM controller, the animator controller of the vendor shooter AI), on the T19
    /// model of its class. The model's own scale (1.8 for a 1.8 m figure) stays on the root, so the capsule and
    /// the agent take local sizes. Our side: Health (the real one, the corpse stays), LockOnTargetable (heavy for
    /// the power armour), DummyShooter (the telegraph gun), SoldierBrain, InvectorEnemyBody. Numbers PROPOSED.
    /// </summary>
    public static class InvectorEnemyFactory
    {
        public const string Folder = "Assets/_Game/AI";
        public const string FsmPath = Folder + "/FSM_GB_Soldier.asset";
        public const string AnimatorPath = "Assets/Invector-3rdPersonController/Shooter/Animator/Invector@ShooterMelee.controller";

        public sealed class Spec
        {
            public string classId = "soldier";
            public float health = 80f;
            public bool heavy;
            public float range = 10f;
            public float detectDistance = 12f;
        }

        public static Spec SpecFor(string classId)
        {
            switch (classId)
            {
                case "guard": return new Spec { classId = classId, health = 60f };
                case "power_armour": return new Spec { classId = classId, health = 250f, heavy = true, range = 9f };
                default: return new Spec { classId = classId };
            }
        }

        // ── The FSM ───────────────────────────────────────────────────

        public static vFSMBehaviour BuildSoldierFsm()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Game", "AI");
            AssetDatabase.DeleteAsset(FsmPath);
            var graph = ScriptableObject.CreateInstance<vFSMBehaviour>();
            graph.states = new List<vFSMState>();
            AssetDatabase.CreateAsset(graph, FsmPath);

            vFSMState entry = Node(graph, "Entry", new Vector2(40, 40), Color.green);
            entry.resetCurrentDestination = false;
            entry.useDecisions = false;
            entry.useActions = false;
            entry.canEditName = entry.canRemove = entry.canTranstTo = entry.canEditColor = entry.canSetAsDefault = false;
            entry.description = "This State Run Just in Start\n to init first state";
            vFSMState any = Node(graph, "AnyState", new Vector2(40, 140), Color.cyan);
            any.resetCurrentDestination = false;
            any.useActions = false;
            any.canEditName = any.canRemove = any.canTranstTo = any.canEditColor = any.canSetAsDefault = false;
            any.description = "This State Run after current state";

            vFSMState idle = Node(graph, "Idle", new Vector2(300, 40), new Color(0.5f, 0.7f, 1f));
            idle.description = "Unaware: looks for Lucy (sight). A hit or an alert also wakes it (SoldierBrain).";
            idle.actions.Add(Action<vAIFindTargetAction>(graph, "Find Target", vFSMComponentExecutionType.OnStateUpdate));

            vFSMState engage = Node(graph, "Engage", new Vector2(560, 40), new Color(1f, 0.45f, 0.35f));
            engage.description = "Alerted: move or shoot, never both (SoldierBrain). It knows where she is.";
            engage.actions.Add(Action<GbEngageAction>(graph, "Move Or Shoot", vFSMComponentExecutionType.OnStateUpdate | vFSMComponentExecutionType.OnStateExit));

            entry.defaultTransition = idle;
            Transition(idle, Decision<GbAlertedDecision>(graph, "Is Alerted"), engage, whenTrue: true);
            Transition(engage, Decision<GbAlertedDecision>(graph, "Is Alerted"), idle, whenTrue: false);

            graph.states.AddRange(new[] { entry, any, idle, engage });
            foreach (vFSMState state in graph.states)
            {
                state.parentGraph = graph;
                EditorUtility.SetDirty(state);
            }
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            return graph;
        }

        private static vFSMState Node(vFSMBehaviour graph, string name, Vector2 at, Color colour)
        {
            var state = ScriptableObject.CreateInstance<vFSMState>();
            state.name = name;
            state.nodeRect = new Rect(at.x, at.y, 180, 30);
            state.nodeColor = colour;
            state.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(state, graph);
            return state;
        }

        private static T Action<T>(vFSMBehaviour graph, string name, vFSMComponentExecutionType when) where T : vStateAction
        {
            T action = ScriptableObject.CreateInstance<T>();
            action.name = name;
            action.executionType = when;
            action.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(action, graph);
            return action;
        }

        private static T Decision<T>(vFSMBehaviour graph, string name) where T : vStateDecision
        {
            T decision = ScriptableObject.CreateInstance<T>();
            decision.name = name;
            decision.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(decision, graph);
            return decision;
        }

        private static void Transition(vFSMState from, vStateDecision decision, vFSMState to, bool whenTrue)
        {
            var transition = new vStateTransition(decision) { parentState = from, trueState = to };
            transition.decisions[0].trueValue = whenTrue;
            from.transitions.Add(transition);
        }

        // ── A soldier ─────────────────────────────────────────────────

        /// <summary>A soldier of the class at the point. A present object of the same name is replaced.</summary>
        public static GameObject BuildSoldier(string name, Spec spec, Vector3 position, float yaw, CharacterCatalog catalog, vFSMBehaviour fsm)
        {
            GameObject old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);

            CharacterEntry entry = catalog != null ? catalog.Find(spec.classId) : null;
            if (entry == null || entry.prefab == null)
            {
                Debug.LogError($"[InvectorEnemyFactory] no prefab for the class '{spec.classId}'.");
                return null;
            }

            // The model with its Animator becomes the root (vAIMotor reads the Animator on its own object).
            var wrapper = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab);
            PrefabUtility.UnpackPrefabInstance(wrapper, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Animator animator = wrapper.GetComponentInChildren<Animator>();
            GameObject root = animator.gameObject;
            if (root != wrapper)
            {
                root.transform.SetParent(null, true);
                Object.DestroyImmediate(wrapper);
            }
            root.name = name;
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorPath);
            if (animator.runtimeAnimatorController == null) Debug.LogError($"[InvectorEnemyFactory] no animator controller at {AnimatorPath}.");
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // The Invector body, as vCreateAIWindow.Create builds it.
            var ai = root.AddComponent<vControlAI>();
            ai.CreatePrimaryComponents();
            Object.DestroyImmediate(ai);
            var fsmController = root.AddComponent<vFSMBehaviourController>();
            fsmController.fsmBehaviour = fsm;
            ai = root.AddComponent<vControlAI>();
            ai.CreateSecondaryComponents();
            root.tag = "Enemy";
            SetLayer(root, LayerMask.NameToLayer("Enemy"));

            // Sizes: the root carries the model scale, so local = world / scale.
            float scale = Mathf.Max(0.01f, root.transform.lossyScale.y);
            var capsule = root.GetComponent<CapsuleCollider>();
            capsule.height = entry.height / scale;
            capsule.radius = (spec.heavy ? 0.45f : 0.3f) / scale;
            capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
            var agent = root.GetComponent<NavMeshAgent>();
            agent.radius = capsule.radius;
            agent.height = capsule.height;
            agent.baseOffset = 0f;

            ai.SetDetectionLayer(LayerMask.GetMask("Player"));
            ai.SetDetectionTags(new List<string> { "Player" });
            ai.SetObstaclesLayer(LayerMask.GetMask("Default"));
            ai.SetLineOfSight(180f, 1f, spec.detectDistance, 4f);
            var aiSo = new SerializedObject(ai);
            SetFloat(aiSo, "_maxHealth", 100000f);
            SetFloat(aiSo, "_currentHealth", 100000f);
            // Off: Invector's cleanup destroys every MonoBehaviour on the object (our Health too).
            // InvectorEnemyBody lays the corpse down itself.
            SetBool(aiSo, "removeComponentsAfterDie", false);
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            // Our side.
            Health health = root.AddComponent<Health>();
            var healthSo = new SerializedObject(health);
            SetFloat(healthSo, "maxHealth", spec.health);
            SetBool(healthSo, "destroyOnDeath", false);
            healthSo.ApplyModifiedPropertiesWithoutUndo();

            LockOnTargetable target = root.AddComponent<LockOnTargetable>();
            var targetSo = new SerializedObject(target);
            SetFloat(targetSo, "fallbackHeight", Body.ChestHeight);
            SetBool(targetSo, "heavy", spec.heavy);
            targetSo.ApplyModifiedPropertiesWithoutUndo();

            DummyShooter gun = root.AddComponent<DummyShooter>();
            var gunSo = new SerializedObject(gun);
            gunSo.FindProperty("enemyId").stringValue = spec.classId;
            SetFloat(gunSo, "range", spec.range);
            SetBool(gunSo, "requireLineOfSight", true);
            gunSo.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<SoldierBrain>();
            root.AddComponent<InvectorEnemyBody>();
            BuildGun(root, scale);
            return root;
        }

        // ── The gun and the shot effects ──────────────────────────────

        public const string GunPrefabPath = "Assets/Invector-FSMAIController/Prefabs/Weapons/AI_Handgun.prefab";
        public const string EffectsPath = "Assets/_Game/Resources/Effects/shot_effects.asset";
        private const string WeaponAudio = "Assets/Invector-3rdPersonController/Shooter/Audio/Weapons/";
        private const string ImpactPath = "Assets/Invector-3rdPersonController/Shooter/Scripts/DecalManager/BulletEffect/Resources/prefabs/impactConcrete.prefab";

        /// <summary>
        /// The vendor AI handgun without its scripts and colliders: the mesh, the flash and smoke particles, the
        /// light, the audio source. EnemyGun places it at the right hand every frame.
        /// </summary>
        private static void BuildGun(GameObject root, float rootScale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GunPrefabPath);
            if (prefab == null) { Debug.LogError($"[InvectorEnemyFactory] no gun at {GunPrefabPath}."); return; }
            var gun = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(gun, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            gun.name = "Gun";
            gun.transform.SetParent(root.transform, false);
            gun.transform.localScale = Vector3.one / rootScale; // the root carries the model scale
            // Scripts first (several passes: some require others), then the colliders.
            for (int pass = 0; pass < 4; pass++)
            {
                foreach (MonoBehaviour script in gun.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (script != null) Object.DestroyImmediate(script);
                }
            }
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gun);
            foreach (Collider collider in gun.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (string unused in new[] { "AimReference", "leftHandIK" })
            {
                Transform t = FindDeep(gun.transform, unused);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            SetLayer(gun, LayerMask.NameToLayer("Enemy"));

            var flashes = new List<ParticleSystem>();
            ParticleSystem smoke = null;
            foreach (ParticleSystem ps in gun.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.playOnAwake = false;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                bool top = ps.transform.parent == null || ps.transform.parent.GetComponent<ParticleSystem>() == null;
                if (!top) continue;
                if (ps.name.StartsWith("Smoke")) smoke = ps;
                else if (ps.name.StartsWith("Fire")) flashes.Add(ps);
            }
            Light light = gun.GetComponentInChildren<Light>(true);
            if (light != null) light.enabled = false;
            AudioSource source = gun.GetComponentInChildren<AudioSource>(true);
            if (source == null) source = gun.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.minDistance = 1.5f;
            source.maxDistance = 40f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            Transform muzzle = FindDeep(gun.transform, "muzzle");
            if (muzzle == null) Debug.LogWarning("[InvectorEnemyFactory] the gun has no muzzle: the shot leaves the chest.");

            EnemyGun enemyGun = root.AddComponent<EnemyGun>();
            enemyGun.Bind(gun.transform, muzzle, flashes.ToArray(), smoke, light, source);
            EditorUtility.SetDirty(enemyGun);
        }

        /// <summary>The fire and aim sounds and the impact, from the vendor's shooter assets.</summary>
        public static ShotEffects EnsureShotEffects()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources/Effects")) AssetDatabase.CreateFolder("Assets/_Game/Resources", "Effects");
            var fx = AssetDatabase.LoadAssetAtPath<ShotEffects>(EffectsPath);
            if (fx == null)
            {
                fx = ScriptableObject.CreateInstance<ShotEffects>();
                AssetDatabase.CreateAsset(fx, EffectsPath);
            }
            var clips = new List<AudioClip>();
            foreach (string name in new[] { "Handgun@Fire.wav", "NewWeaponSounds/LightWeaponShot.mp3", "NewWeaponSounds/LightWeaponShot2.mp3", "NewWeaponSounds/LightWeaponShot3.mp3" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(WeaponAudio + name);
                if (clip != null) clips.Add(clip);
                else Debug.LogWarning($"[InvectorEnemyFactory] no clip {name}.");
            }
            fx.fireClips = clips.ToArray();
            fx.aimClip = AssetDatabase.LoadAssetAtPath<AudioClip>(WeaponAudio + "NewWeaponSounds/LightWeaponAim.mp3");
            fx.impactPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPath);
            if (fx.impactPrefab == null) Debug.LogWarning($"[InvectorEnemyFactory] no impact prefab at {ImpactPath}.");
            EditorUtility.SetDirty(fx);
            AssetDatabase.SaveAssets();
            return fx;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        private static void SetFloat(SerializedObject so, string property, float value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p == null) { Debug.LogWarning($"[InvectorEnemyFactory] no property {property} on {so.targetObject.GetType().Name}."); return; }
            if (p.propertyType == SerializedPropertyType.Integer) p.intValue = Mathf.RoundToInt(value);
            else p.floatValue = value;
        }

        private static void SetBool(SerializedObject so, string property, bool value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p == null) { Debug.LogWarning($"[InvectorEnemyFactory] no property {property} on {so.targetObject.GetType().Name}."); return; }
            p.boolValue = value;
        }

        private static void SetLayer(GameObject root, int layer)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }
}
#endif
