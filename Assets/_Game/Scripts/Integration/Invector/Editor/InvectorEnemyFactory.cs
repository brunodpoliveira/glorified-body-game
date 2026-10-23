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
    /// Shoot, our SoldierBrain) when the soldier sees Lucy -> Search (walk to where she was) when it loses her
    /// -> Engage again when it sees her, or Idle at the spot. The builder writes it again on every run.
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
            idle.description = "Looks for Lucy.";
            idle.actions.Add(Action<vAIFindTargetAction>(graph, "Find Target", vFSMComponentExecutionType.OnStateUpdate));

            vFSMState engage = Node(graph, "Engage", new Vector2(560, 40), new Color(1f, 0.45f, 0.35f));
            engage.description = "Move or shoot, never both (SoldierBrain).";
            engage.actions.Add(Action<vAIFindTargetAction>(graph, "Find Target", vFSMComponentExecutionType.OnStateUpdate));
            engage.actions.Add(Action<GbEngageAction>(graph, "Move Or Shoot", vFSMComponentExecutionType.OnStateUpdate | vFSMComponentExecutionType.OnStateExit));

            vFSMState search = Node(graph, "Search", new Vector2(560, 200), new Color(1f, 0.85f, 0.4f));
            search.description = "Walks to where Lucy was last seen.";
            search.actions.Add(Action<vAIFindTargetAction>(graph, "Find Target", vFSMComponentExecutionType.OnStateUpdate));
            search.actions.Add(Action<vGoToTarget>(graph, "Chase Target", vFSMComponentExecutionType.OnStateUpdate | vFSMComponentExecutionType.OnStateEnter));

            entry.defaultTransition = idle;
            Transition(idle, Decision<AICanSeeTargetDecision>(graph, "Can See Target"), engage);
            Transition(engage, Decision<vTargetLost>(graph, "Target Lost"), search);
            Transition(search, Decision<AICanSeeTargetDecision>(graph, "Can See Target"), engage);
            Transition(search, Decision<vAIIsInDestination>(graph, "Is In Destination"), idle);

            graph.states.AddRange(new[] { entry, any, idle, engage, search });
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

        private static void Transition(vFSMState from, vStateDecision decision, vFSMState to)
        {
            var transition = new vStateTransition(decision) { parentState = from, trueState = to };
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
            SetBool(aiSo, "removeComponentsAfterDie", true);
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
            return root;
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
