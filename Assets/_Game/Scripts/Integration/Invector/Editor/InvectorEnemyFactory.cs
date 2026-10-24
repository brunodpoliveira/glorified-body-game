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
            public DummyShooter.Weapon weapon = DummyShooter.Weapon.Pistol;
            public bool shield;
            public bool canMelee = true;
            /// <summary>Design §11.5: who runs. Guards at a fear of 3, soldiers and heavies never.</summary>
            public EnemyMorale.Rank rank = EnemyMorale.Rank.Soldier;
        }

        public static Spec SpecFor(string classId)
        {
            switch (classId)
            {
                case "guard": return new Spec { classId = classId, health = 60f, rank = EnemyMorale.Rank.Guard };
                case "power_armour": return new Spec { classId = classId, health = 250f, heavy = true, range = 9f, rank = EnemyMorale.Rank.Heavy };
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
            // A fighter turns fast (Bruno, 2026-10-24: "they don't look like they're in combat at all").
            SetFloat(aiSo, "freeSpeed.rotationSpeed", 16f);
            SetFloat(aiSo, "strafeSpeed.rotationSpeed", 16f);
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
            // The chest of a 1.8 m soldier is 1.2 m; a taller class (the 2.6 m power armour) has a higher chest.
            SetFloat(targetSo, "fallbackHeight", Body.ChestHeight * Mathf.Max(1f, entry.height / 1.8f));
            SetBool(targetSo, "heavy", spec.heavy);
            targetSo.ApplyModifiedPropertiesWithoutUndo();

            DummyShooter gun = root.AddComponent<DummyShooter>();
            var gunSo = new SerializedObject(gun);
            gunSo.FindProperty("enemyId").stringValue = spec.classId;
            SetFloat(gunSo, "range", spec.range);
            SetBool(gunSo, "requireLineOfSight", true);
            gunSo.FindProperty("weapon").enumValueIndex = (int)spec.weapon;
            SetBool(gunSo, "canMelee", spec.canMelee);
            gunSo.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<SoldierBrain>();
            root.AddComponent<InvectorEnemyBody>();
            // What a death does to the others (§11.4, §11.5), and the care of a dead colleague (not the heavies).
            EnemyMorale morale = root.AddComponent<EnemyMorale>();
            var moraleSo = new SerializedObject(morale);
            moraleSo.FindProperty("rank").enumValueIndex = (int)spec.rank;
            moraleSo.ApplyModifiedPropertiesWithoutUndo();
            if (spec.rank != EnemyMorale.Rank.Heavy) root.AddComponent<CorpseCare>();
            BuildGun(root, scale, spec.weapon);
            if (spec.shield) BuildShield(root, scale);
            // The ragdoll (Bruno, 2026-10-24): the lift, the throw and a full mk_warspite knock him down. A heavy
            // never falls (it only staggers), so it has none.
            if (!spec.heavy) BuildRagdoll(root, animator);
            return root;
        }

        // ── The ragdoll ───────────────────────────────────────────────

        public const float RagdollMass = 60f; // kg, PROPOSED

        /// <summary>
        /// The ragdoll from HumanoidRagdoll (the parts of Unity's ragdoll wizard, built by our editor code). Then
        /// every bone body is kinematic with its collider off (EnemyRagdoll turns them on when he goes limp), on the
        /// BodyPart layer (it hits the room and other ragdolls, not her and not the capsules).
        /// </summary>
        public static void BuildRagdoll(GameObject root, Animator animator)
        {
            GlorifiedBody.Editor.Characters.HumanoidRagdoll.Result built = GlorifiedBody.Editor.Characters.HumanoidRagdoll.Build(animator, RagdollMass);
            if (built.Error != "")
            {
                Debug.LogError($"[InvectorEnemyFactory] the ragdoll of {root.name}: {built.Error}");
                return;
            }
            Transform Bone(HumanBodyBones b) => animator.GetBoneTransform(b);
            Transform hips = Bone(HumanBodyBones.Hips);
            Rigidbody rootBody = root.GetComponent<Rigidbody>();
            var bones = new List<Rigidbody>();
            foreach (Rigidbody body in hips.GetComponentsInChildren<Rigidbody>(true))
            {
                // Only the builder's bodies: the hips and the bones with a joint (not a gun or a shield part).
                if (body == rootBody || (body.transform != hips && body.GetComponent<CharacterJoint>() == null)) continue;
                bones.Add(body);
            }
            // Parents before children: EnemyRagdoll puts the bones back in this order.
            bones.Sort((a, b) => Depth(a.transform).CompareTo(Depth(b.transform)));
            int bodyPart = LayerMask.NameToLayer("BodyPart");
            foreach (Rigidbody body in bones)
            {
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.None;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                Collider c = body.GetComponent<Collider>();
                if (c != null) c.enabled = false;
                if (body.TryGetComponent(out CharacterJoint joint)) joint.enableProjection = true;
                body.gameObject.layer = bodyPart;
            }
            EnemyRagdoll ragdoll = root.GetComponent<EnemyRagdoll>() ?? root.AddComponent<EnemyRagdoll>();
            ragdoll.Bind(bones.ToArray());
            EditorUtility.SetDirty(ragdoll);
            Debug.Log($"[InvectorEnemyFactory] ragdoll of {root.name}: {bones.Count} bones, {RagdollMass} kg.");
        }

        private static int Depth(Transform t)
        {
            int d = 0;
            for (Transform p = t.parent; p != null; p = p.parent) d++;
            return d;
        }

        // ── The gun and the shot effects ──────────────────────────────

        public const string GunPrefabPath = "Assets/Invector-FSMAIController/Prefabs/Weapons/AI_Handgun.prefab";
        public const string RiflePrefabPath = "Assets/Invector-FSMAIController/Prefabs/Weapons/AI_vAssaultRifle.prefab";
        public const string LauncherPrefabPath = "Assets/Invector-FSMAIController/Prefabs/Weapons/AI_vShotgun.prefab"; // the placeholder launcher
        public const string ShieldPrefabPath = "Assets/Invector-FSMAIController/Prefabs/Weapons/AI_vGreatShield.prefab";
        private const string MeleeAudio = "Assets/Invector-3rdPersonController/Melee Combat/Audio/";
        private const string ExplosionPath = "Assets/Invector-3rdPersonController/Shooter/Prefabs/Particles/Explosion/Explosion_Air.prefab";
        private const string SmokeGrenadePath = "Assets/Invector-3rdPersonController/Shooter/Scripts/ThrowSystem/Prefabs/Grenades/vSmokeGrenade.prefab";
        private const string RpgPath = "Assets/Invector-3rdPersonController/Shooter/3DModels/Weapons/vWeaponsNew/prefabs/vRPG.prefab";
        public const string GasSmokePath = "Assets/_Game/Effects/GasCloud_Smoke.prefab";
        public const string EffectsPath = "Assets/_Game/Resources/Effects/shot_effects.asset";
        private const string WeaponAudio = "Assets/Invector-3rdPersonController/Shooter/Audio/Weapons/";
        private const string ImpactPath = "Assets/Invector-3rdPersonController/Shooter/Scripts/DecalManager/BulletEffect/Resources/prefabs/impactConcrete.prefab";

        /// <summary>
        /// The vendor AI handgun without its scripts and colliders: the mesh, the flash and smoke particles, the
        /// light, the audio source. EnemyGun places it at the right hand every frame.
        /// </summary>
        private static void BuildGun(GameObject root, float rootScale, DummyShooter.Weapon weapon)
        {
            // The launchers (frag, gas, the power armour's frag and rockets) use the vendor shotgun as a placeholder.
            bool launcher = weapon == DummyShooter.Weapon.GrenadeLauncher || weapon == DummyShooter.Weapon.GasLauncher || weapon == DummyShooter.Weapon.ArmourLaunchers;
            string path = weapon == DummyShooter.Weapon.Rifle ? RiflePrefabPath : launcher ? LauncherPrefabPath : GunPrefabPath;
            // The vendor animator's weapon set: handgun 1, rifle 2, shotgun 3 (the vendor prefabs' upperBodyID).
            int animatorId = weapon == DummyShooter.Weapon.Rifle ? 2 : launcher ? 3 : 1;
            GameObject gun = StrippedVendorModel(path, "Gun", root, rootScale);
            if (gun == null) return;
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
            enemyGun.Bind(gun.transform, muzzle, flashes.ToArray(), smoke, light, source, animatorId, animatorId);
            EditorUtility.SetDirty(enemyGun);
        }

        /// <summary>A vendor weapon prefab, unpacked under the root, without its scripts and colliders.</summary>
        private static GameObject StrippedVendorModel(string path, string name, GameObject root, float rootScale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogError($"[InvectorEnemyFactory] no model at {path}."); return null; }
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = name;
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = Vector3.one / rootScale; // the root carries the model scale
            // Scripts first (several passes: some require others), then the colliders.
            for (int pass = 0; pass < 4; pass++)
            {
                foreach (MonoBehaviour script in model.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (script != null) Object.DestroyImmediate(script);
                }
            }
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(model);
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            return model;
        }

        /// <summary>
        /// The vendor great shield, made 1.0 m tall. Its face (the thinnest side of its bounds) turns to the
        /// soldier's front and its long side up; EnemyShield holds it in front of his left side.
        /// </summary>
        private static void BuildShield(GameObject root, float rootScale)
        {
            GameObject shield = StrippedVendorModel(ShieldPrefabPath, "Shield", root, rootScale);
            if (shield == null) return;
            SetLayer(shield, LayerMask.NameToLayer("Enemy"));
            // Measure the model upright at the origin, in its own axes.
            Quaternion keep = shield.transform.rotation;
            Vector3 keepPos = shield.transform.position;
            shield.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Bounds bounds = new Bounds();
            bool any = false;
            foreach (Renderer r in shield.GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            Vector3 size = any ? bounds.size : Vector3.one;
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            float[] extents = { size.x, size.y, size.z };
            int thin = 0, tall = 0;
            for (int i = 1; i < 3; i++)
            {
                if (extents[i] < extents[thin]) thin = i;
                if (extents[i] > extents[tall]) tall = i;
            }
            if (thin == tall) tall = (thin + 1) % 3;
            // The long side in the face plane, from the spread of the mesh points (the main axis of their
            // covariance): the box axes lie when a child of the model is turned.
            int u = (thin + 1) % 3, v = (thin + 2) % 3;
            var points = new List<Vector3>();
            foreach (MeshFilter mf in shield.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                foreach (Vector3 p in mf.sharedMesh.vertices) points.Add(mf.transform.TransformPoint(p));
            }
            Vector3 longSide = axes[tall];
            float length = extents[tall];
            if (points.Count > 8)
            {
                Vector3 mean = Vector3.zero;
                foreach (Vector3 p in points) mean += p;
                mean /= points.Count;
                double cuu = 0, cvv = 0, cuv = 0;
                foreach (Vector3 p in points)
                {
                    double du = p[u] - mean[u], dv = p[v] - mean[v];
                    cuu += du * du; cvv += dv * dv; cuv += du * dv;
                }
                float angle = 0.5f * Mathf.Atan2((float)(2 * cuv), (float)(cuu - cvv));
                longSide = (axes[u] * Mathf.Cos(angle) + axes[v] * Mathf.Sin(angle)).normalized;
                if (Vector3.Dot(longSide, axes[tall]) < 0f) longSide = -longSide;
                float min = float.MaxValue, max = float.MinValue;
                foreach (Vector3 p in points)
                {
                    float d = Vector3.Dot(p, longSide);
                    min = Mathf.Min(min, d);
                    max = Mathf.Max(max, d);
                }
                length = max - min;
            }
            // The model's thin axis to +z (its face forward), its long side to +y.
            Quaternion face = Quaternion.Inverse(Quaternion.LookRotation(axes[thin], longSide));
            float fit = length > 0.01f ? 1.0f / length : 1f;
            shield.transform.localScale *= fit;
            shield.transform.SetPositionAndRotation(keepPos, keep);
            Debug.Log($"[InvectorEnemyFactory] shield bounds {size}, thin axis {thin}, long side {longSide}, {points.Count} points, scale x{fit:0.00}.");

            EnemyShield guard = root.AddComponent<EnemyShield>();
            guard.Bind(shield.transform, face);
            EditorUtility.SetDirty(guard);
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
            fx.rifleFireClips = Clips(WeaponAudio, "NewWeaponSounds/MediumWeaponShot1.mp3", "NewWeaponSounds/MediumWeaponShot2.mp3", "NewWeaponSounds/MediumWeaponShot3.mp3", "Rifle@Fire.wav");
            fx.rifleAimClip = AssetDatabase.LoadAssetAtPath<AudioClip>(WeaponAudio + "NewWeaponSounds/MediumWeaponAim.mp3");
            fx.launcherAimClip = AssetDatabase.LoadAssetAtPath<AudioClip>(WeaponAudio + "NewWeaponSounds/MediumWeaponAimB.mp3");
            fx.meleeSwingClips = Clips(MeleeAudio, "slash_A.wav", "slash_B.wav", "slash_C.wav");
            fx.meleeHitClips = Clips(MeleeAudio, "PunchA.wav", "PunchB.wav", "Kick.wav");
            fx.blockClips = Clips(MeleeAudio, "Block 1.wav", "Block 2.wav");
            fx.explosionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionPath);
            if (fx.explosionPrefab == null) Debug.LogWarning($"[InvectorEnemyFactory] no explosion prefab at {ExplosionPath}.");
            fx.explosionClip = AssetDatabase.LoadAssetAtPath<AudioClip>(WeaponAudio + "Explosion_A.wav");
            fx.launcherFireClips = Clips(WeaponAudio, "PumpShotgun@FireA.mp3", "PumpShotgun@FireB.mp3");
            fx.rocketFireClips = Clips(WeaponAudio, "RPG@Fire.wav", "RPG@FireB.mp3");
            fx.gasClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Invector-3rdPersonController/Basic Locomotion/Audio/Others/grenade-explosion-sfx-medium-sized.flac");
            fx.gasPrefab = BuildGasSmoke();
            ReadRocketMesh(fx);
            if (fx.impactPrefab == null) Debug.LogWarning($"[InvectorEnemyFactory] no impact prefab at {ImpactPath}.");
            EditorUtility.SetDirty(fx);
            AssetDatabase.SaveAssets();
            return fx;
        }

        /// <summary>
        /// The smoke of the vendor smoke grenade, its scripts removed, saved as our prefab (it refers to the vendor
        /// particle materials, so it works only with the vendor folders present, like the soldiers).
        /// </summary>
        private static GameObject BuildGasSmoke()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SmokeGrenadePath);
            if (prefab == null) { Debug.LogWarning($"[InvectorEnemyFactory] no smoke grenade at {SmokeGrenadePath}."); return null; }
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(temp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Transform smoke = FindDeep(temp.transform, "SmokeEffect");
            if (smoke == null) { Object.DestroyImmediate(temp); Debug.LogWarning("[InvectorEnemyFactory] the smoke grenade has no SmokeEffect."); return null; }
            smoke.SetParent(null, false);
            smoke.gameObject.SetActive(true); // the vendor wakes it when its grenade bursts: ours plays at once
            smoke.localPosition = Vector3.zero;
            smoke.localRotation = Quaternion.identity;
            Object.DestroyImmediate(temp);
            for (int pass = 0; pass < 4; pass++)
            {
                foreach (MonoBehaviour script in smoke.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (script != null) Object.DestroyImmediate(script);
                }
            }
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(smoke.gameObject);
            foreach (Collider collider in smoke.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (AudioSource audio in smoke.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(audio);
            foreach (ParticleSystem ps in smoke.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.playOnAwake = true;
                main.loop = true;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy; // GasCloud scales it to its ring
            }
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Effects")) AssetDatabase.CreateFolder("Assets/_Game", "Effects");
            // The vendor smoke uses legacy built-in particle shaders: under URP they ignore a tint. Our own URP
            // particle material per smoke texture, green (git-ignored: it points at the vendor texture).
            var made = new Dictionary<Texture, Material>();
            foreach (ParticleSystemRenderer r in smoke.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                Texture tex = r.sharedMaterial != null ? r.sharedMaterial.mainTexture : null;
                if (tex == null) continue;
                if (!made.TryGetValue(tex, out Material gas))
                {
                    gas = GasMaterial(tex, $"Assets/_Game/Effects/GasSmoke_{made.Count}.mat");
                    made[tex] = gas;
                }
                if (gas != null) r.sharedMaterial = gas;
            }
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Effects")) AssetDatabase.CreateFolder("Assets/_Game", "Effects");
            smoke.name = "GasCloud_Smoke";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(smoke.gameObject, GasSmokePath);
            Object.DestroyImmediate(smoke.gameObject);
            return saved;
        }

        /// <summary>A URP particle material, unlit, alpha blended, green, with the smoke texture.</summary>
        private static Material GasMaterial(Texture texture, string path)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) { Debug.LogWarning("[InvectorEnemyFactory] no URP particle shader: the gas keeps the vendor smoke."); return null; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", new Color(0.75f, 1f, 0.35f, 0.6f)); // with the sheet's own alpha (up to 0.56)
            mat.SetFloat("_Surface", 1f); // transparent
            mat.SetFloat("_Blend", 0f);   // alpha
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>The vendor RPG's missile mesh for the rocket, its long side along +z, 0.6 m long.</summary>
        private static void ReadRocketMesh(ShotEffects fx)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RpgPath);
            Transform missile = prefab != null ? FindDeep(prefab.transform, "RPG_missel") : null;
            MeshFilter filter = missile != null ? missile.GetComponentInChildren<MeshFilter>(true) : null;
            MeshRenderer renderer = filter != null ? filter.GetComponent<MeshRenderer>() : null;
            if (filter == null || filter.sharedMesh == null)
            {
                Debug.LogWarning($"[InvectorEnemyFactory] no missile mesh in {RpgPath}: the rocket is a capsule.");
                fx.rocketMesh = null;
                return;
            }
            Vector3 size = filter.sharedMesh.bounds.size;
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            float[] extents = { size.x, size.y, size.z };
            int longAxis = 0;
            for (int i = 1; i < 3; i++) if (extents[i] > extents[longAxis]) longAxis = i;
            Vector3 up = longAxis == 1 ? Vector3.forward : Vector3.up;
            fx.rocketMesh = filter.sharedMesh;
            fx.rocketMaterial = renderer != null ? renderer.sharedMaterial : null;
            fx.rocketMeshRotation = Quaternion.Inverse(Quaternion.LookRotation(axes[longAxis], up));
            fx.rocketMeshScale = extents[longAxis] > 0.0001f ? 0.6f / extents[longAxis] : 1f;
            Debug.Log($"[InvectorEnemyFactory] rocket mesh {filter.sharedMesh.name} size {size}, long axis {longAxis}, scale x{fx.rocketMeshScale:0.00}.");
        }

        private static AudioClip[] Clips(string folder, params string[] names)
        {
            var clips = new List<AudioClip>();
            foreach (string name in names)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(folder + name);
                if (clip != null) clips.Add(clip);
                else Debug.LogWarning($"[InvectorEnemyFactory] no clip {folder}{name}.");
            }
            return clips.ToArray();
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
