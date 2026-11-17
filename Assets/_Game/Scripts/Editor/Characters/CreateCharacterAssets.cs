#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using GlorifiedBody.Characters;

namespace GlorifiedBody.Editor.Characters
{
    /// <summary>
    /// The nine T19 characters into game assets. From Assets/_Game/Characters/Models/<class>.fbx and
    /// <class>_basecolor (tools/characters.sh wrote them, plus <class>_normal and <class>_ms for a T49 class) it
    /// makes: one URP material per class (matte, or the T49 map set), one animator
    /// controller per class (its own idle, walk and run, the Lucy or the Enemy state set), one prefab per class
    /// (the model scaled to its height, the material, the animator) and the catalog in Resources. Then it
    /// rebuilds the scenes. Idempotent.
    /// Menu: GlorifiedBody > Build Characters. Batch: tools/unity.sh exec GlorifiedBody.Editor.Characters.CreateCharacterAssets.BuildAll
    /// The Invector clips (roll, aim, hit, death, fall) are placeholders for the poses the models lack. Without
    /// the Invector folder (a fresh clone) those states play the idle, and the log says so.
    /// </summary>
    public static class CreateCharacterAssets
    {
        public const string Root = "Assets/_Game/Characters";
        public const string ModelsFolder = Root + "/Models";
        public const string MaterialsFolder = Root + "/Materials";
        public const string PrefabsFolder = Root + "/Prefabs";
        public const string AnimFolder = Root + "/Anim";
        public const string CatalogPath = "Assets/_Game/Resources/Characters/catalog.asset";
        public const string InvectorFolder = "Assets/Invector-3rdPersonController";

        private struct ClassDef
        {
            public string id, display, outfit;
            public float height;
            public CharacterRole role;
            public ClassDef(string id, string display, float height, string outfit, CharacterRole role)
            { this.id = id; this.display = display; this.height = height; this.outfit = outfit; this.role = role; }
        }

        // Heights: Bruno, 2026-10-21 ("heights make sense for me"). The power armour is larger than a person (design §8.4).
        private static readonly ClassDef[] Classes =
        {
            new ClassDef("lucy_bare", "LUCY", 1.70f, "nude", CharacterRole.Player),
            new ClassDef("lucy_guard", "LUCY (GUARD)", 1.70f, "guard", CharacterRole.Player),
            new ClassDef("patient", "PATIENT", 1.68f, "", CharacterRole.Civilian),
            new ClassDef("nurse", "NURSE", 1.68f, "", CharacterRole.Civilian),
            new ClassDef("labworker", "LAB WORKER", 1.78f, "", CharacterRole.Enemy),
            new ClassDef("guard", "SECURITY GUARD", 1.80f, "", CharacterRole.Enemy),
            new ClassDef("soldier", "SOLDIER", 1.80f, "", CharacterRole.Enemy),
            new ClassDef("fireman", "FIREMAN", 1.80f, "", CharacterRole.Civilian),
            new ClassDef("power_armour", "POWER ARMOUR", 2.90f, "", CharacterRole.Enemy), // DEMO_PLAN §3.2: 2.90 m (the T49 model, round 183)
            // Round 179 (the T49 models; DEMO_PLAN §3.2): the night manager of the lodge in G19 and the navy guard of R3.
            new ClassDef("lodge_staff", "NIGHT MANAGER", 1.75f, "", CharacterRole.Civilian),
            new ClassDef("toll_guard", "TOLL GUARD", 1.82f, "", CharacterRole.Enemy),
        };

        public static IEnumerable<string> ClassIds { get { foreach (ClassDef c in Classes) yield return c.id; } }

        [MenuItem("GlorifiedBody/Content/Build Characters", priority = 20)]
        public static void BuildAll()
        {
            EnsureFolder("Assets/_Game", "Characters");
            EnsureFolder(Root, "Models");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Prefabs");
            EnsureFolder(Root, "Anim");
            EnsureFolder("Assets/_Game", "Resources");
            EnsureFolder("Assets/_Game/Resources", "Characters");

            int present = 0;
            foreach (ClassDef c in Classes)
            {
                string fbx = $"{ModelsFolder}/{c.id}.fbx";
                if (!File.Exists(fbx)) { Debug.LogWarning($"[CreateCharacterAssets] missing {fbx} (run tools/characters.sh)"); continue; }
                AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceUpdate);
                // A new model under an old name keeps the old reference pose in its .meta: map it again.
                CharacterImportSettings.RefreshStaleAvatar(fbx);
                // The maps of the class and of its parts (<class>_<part>_basecolor.png and the rest: Lucy's Crown).
                foreach (string tex in Directory.GetFiles(ModelsFolder, c.id + "_*"))
                {
                    string t = tex.Replace('\\', '/');
                    if (t.EndsWith(".png") || t.EndsWith(".jpg") || t.EndsWith(".jpeg")) AssetDatabase.ImportAsset(t, ImportAssetOptions.ForceUpdate);
                }
                present++;
                // The root motion decision per clip, from the measurement next to the FBX (CharacterImportSettings).
                ClipTravelFile measured = CharacterImportSettings.ReadClipTravel(fbx);
                Debug.Log(measured == null
                    ? $"[CreateCharacterAssets] {c.id}: no clips.json, the clip names decide the root motion"
                    : $"[CreateCharacterAssets] {c.id}: {measured.Describe()}");
            }

            Dictionary<string, AnimationClip> invector = LoadInvectorClips();
            // The first build had two shared controllers (Lucy, Enemy). The guard's idle on the 2.6 m power armour
            // left its feet 14 cm in the air (the Humanoid retarget), so every class plays its own clips now.
            foreach (string legacy in new[] { "Lucy", "Enemy" })
            {
                string legacyPath = $"{AnimFolder}/{legacy}.controller";
                if (File.Exists(legacyPath)) AssetDatabase.DeleteAsset(legacyPath);
            }

            CharacterCatalog catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.entries.Clear();
            foreach (ClassDef c in Classes)
            {
                string fbx = $"{ModelsFolder}/{c.id}.fbx";
                if (!File.Exists(fbx)) continue;
                Material material = BuildMaterial(c.id);
                AnimatorController controller = BuildController(c.id, c.id, invector, lucy: c.role == CharacterRole.Player, height: c.height);
                GameObject prefab = BuildPrefab(c, fbx, material, controller);
                catalog.entries.Add(new CharacterEntry
                {
                    id = c.id, displayName = c.display, prefab = prefab, height = c.height, outfitId = c.outfit, role = c.role
                });
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CreateCharacterAssets] {catalog.entries.Count} of {Classes.Length} classes built ({present} FBX present).");
            BuildPoseClips();

            MenuLoopSetup.BuildMenuLoopScenes();
        }

        public const string PoseClipsPath = "Assets/_Game/Resources/" + PoseClips.ResourcePath + ".asset";
        public const string FearClipsFbx = ModelsFolder + "/labworker_fear.fbx";
        public const string PainClipsFbx = ModelsFolder + "/lucy_pain.fbx";

        /// <summary>
        /// The clips of PoseOverride (Resources/Characters/pose_clips): the RFab fear presets from
        /// labworker_fear.fbx (afraid, scared_01, scared_02) and the presets on Lucy from lucy_pain.fbx
        /// (frustrated_02 for the head pain at the start of the short, hurt for the sit). Both files come from
        /// tools/characters_export.py on the 3D Studio file with the motions.
        /// </summary>
        [MenuItem("GlorifiedBody/Content/Build Pose Clips", priority = 21)]
        public static void BuildPoseClips()
        {
            EnsureFolder("Assets/_Game/Resources", "Characters");
            if (File.Exists(FearClipsFbx)) AssetDatabase.ImportAsset(FearClipsFbx, ImportAssetOptions.ForceUpdate);
            Dictionary<string, AnimationClip> fear = File.Exists(FearClipsFbx) ? ClipsOf(FearClipsFbx) : new Dictionary<string, AnimationClip>();
            if (File.Exists(PainClipsFbx)) AssetDatabase.ImportAsset(PainClipsFbx, ImportAssetOptions.ForceUpdate);
            Dictionary<string, AnimationClip> pain = File.Exists(PainClipsFbx) ? ClipsOf(PainClipsFbx) : new Dictionary<string, AnimationClip>();
            var clips = AssetDatabase.LoadAssetAtPath<PoseClips>(PoseClipsPath);
            if (clips == null)
            {
                clips = ScriptableObject.CreateInstance<PoseClips>();
                AssetDatabase.CreateAsset(clips, PoseClipsPath);
            }
            fear.TryGetValue("afraid", out clips.afraid);
            fear.TryGetValue("scared_01", out clips.scared01);
            fear.TryGetValue("scared_02", out clips.scared02);
            pain.TryGetValue("frustrated_02", out clips.lucyPain);
            pain.TryGetValue("hurt", out clips.sit);
            if (clips.afraid == null || clips.scared01 == null || clips.scared02 == null)
                Debug.LogWarning($"[CreateCharacterAssets] {FearClipsFbx}: a fear clip is missing ({string.Join(", ", fear.Keys)}).");
            if (clips.lucyPain == null || clips.sit == null)
                Debug.LogWarning($"[CreateCharacterAssets] {PainClipsFbx}: a clip is missing ({string.Join(", ", pain.Keys)}).");
            EditorUtility.SetDirty(clips);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CreateCharacterAssets] pose clips: afraid={clips.afraid != null} scared_01={clips.scared01 != null} scared_02={clips.scared02 != null} pain={(clips.lucyPain != null ? clips.lucyPain.name : "none")} sit={(clips.sit != null ? clips.sit.name : "none")}");
        }

        private static string TexturePath(string id)
        {
            foreach (string ext in new[] { ".png", ".jpg", ".jpeg" })
            {
                string path = $"{ModelsFolder}/{id}_basecolor{ext}";
                if (File.Exists(path)) return path;
            }
            return null;
        }

        /// <summary>An extra map of a T49 class (tools/characters_basecolor.py --maps): normal or ms. Null when absent.</summary>
        private static string MapPath(string id, string kind)
        {
            string path = $"{ModelsFolder}/{id}_{kind}.png";
            return File.Exists(path) ? path : null;
        }

        private static Material BuildMaterial(string id)
        {
            string path = $"{MaterialsFolder}/{id}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            string texPath = TexturePath(id);
            Texture2D texture = texPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) : null;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            string normalPath = MapPath(id, "normal"), msPath = MapPath(id, "ms");
            Texture2D normal = normalPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) : null;
            Texture2D ms = msPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(msPath) : null;
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.SetTexture("_MetallicGlossMap", ms);
            material.SetFloat("_SmoothnessTextureChannel", 0f); // the smoothness from the alpha of the metallic map
            SetKeyword(material, "_NORMALMAP", normal != null);
            SetKeyword(material, "_METALLICSPECGLOSSMAP", ms != null);
            if (ms != null)
            {
                // A T49 map set (rounds 23, 24, 175, 178): the gloss by material is in the map. Lucy's skin is matte
                // (smoothness 0.25), the gold veins metal 0.9 at smoothness 0.65; the cloth of the T27 bake is capped
                // at 0.25 (ART_LESSONS §3 item 8), the hard parts keep their gloss; the Crown has its own material. _Smoothness multiplies the alpha, so 1 keeps the map as painted. A metal needs the
                // highlights and the reflections, or the gold goes black.
                material.SetFloat("_Smoothness", 1f);
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_SpecularHighlights", 1f);
                material.SetFloat("_EnvironmentReflections", 1f);
                SetKeyword(material, "_SPECULARHIGHLIGHTS_OFF", false);
                SetKeyword(material, "_ENVIRONMENTREFLECTIONS_OFF", false);
            }
            else
            {
                // Matte, the detail in the texture (ART_LESSONS §1.2): no gloss, no reflections, no specular.
                material.SetFloat("_Smoothness", 0f);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_SpecularHighlights", 0f);
                material.SetFloat("_EnvironmentReflections", 0f);
                SetKeyword(material, "_SPECULARHIGHLIGHTS_OFF", true);
                SetKeyword(material, "_ENVIRONMENTREFLECTIONS_OFF", true);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>A mesh name as a file name: letters, digits and _ (the same rule as characters_basecolor.part_name).</summary>
        public static string PartName(string name)
        {
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            return new string(chars);
        }

        private static void SetKeyword(Material material, string keyword, bool on)
        {
            if (on) material.EnableKeyword(keyword); else material.DisableKeyword(keyword);
        }

        /// <summary>
        /// Bruno, round 179 ("Speed up the clips"): the walk and the run of every T49 class (a class with the map set)
        /// play at the pace that fits the game's speed for the class, so the planted foot keeps its place on the floor.
        /// The pace = the speed / the speed of the clip's planted foot in play at x1. Round 185 (Bruno: "From the bot
        /// reading"): that speed comes from the bot lucy (PacePath, tools/characters_pace.py). A class that is not in that
        /// file uses the source measure: clips.json "ground" (tools/characters_clips.py) x the class height; in play it
        /// differs by up to 10 %. Lucy: the TankController defaults (walk, run, back). The figures: the Invector AI walk
        /// and run (ClassPace). The speeds stay as they are. A T19 class, or a clip without a measured ground speed, plays at 1.
        /// </summary>
        private static void ApplyPace(BlendTree tree, AnimatorState walkBack, string id, float height, bool lucy)
        {
            if (MapPath(id, "normal") == null) return; // a T19 class
            ClipTravelFile measured = CharacterImportSettings.ReadClipTravel($"{ModelsFolder}/{id}.fbx");
            PlaySpeed inPlay = ReadPaceFromBot()?.Find(id);
            float walkSpeed, runSpeed, backSpeed;
            if (lucy)
            {
                var probe = new GameObject("[Pace]");
                var tank = probe.AddComponent<GlorifiedBody.Player.TankController>();
                var so = new SerializedObject(tank);
                walkSpeed = so.FindProperty("walkSpeed").floatValue;
                runSpeed = so.FindProperty("runSpeed").floatValue;
                backSpeed = so.FindProperty("backSpeed").floatValue;
                Object.DestroyImmediate(probe);
            }
            else
            {
                walkSpeed = ClassPace.FigureWalkSpeed;
                runSpeed = ClassPace.FigureRunSpeed;
                backSpeed = walkSpeed;
            }
            float Pace(string clip, float speed)
            {
                float bot = inPlay == null ? 0f : clip == "walk" ? inPlay.walk : inPlay.run;
                if (bot > 0.05f) return speed / bot;
                ClipTravel info = measured?.Find(clip);
                return info != null && info.ground > 0.05f ? speed / (info.ground * height) : 1f;
            }
            float walkPace = Pace("walk", walkSpeed), runPace = Pace("run", runSpeed), backPace = Pace("walk", backSpeed);
            ChildMotion[] children = tree.children;
            for (int i = 0; i < children.Length; i++)
            {
                if (Mathf.Approximately(children[i].threshold, 0.5f)) children[i].timeScale = walkPace;
                else if (Mathf.Approximately(children[i].threshold, 1f)) children[i].timeScale = runPace;
            }
            tree.children = children;
            walkBack.speed = -backPace;
            Debug.Log($"[CreateCharacterAssets] {id}: the pace of the clips ({(inPlay != null ? "the bot reading" : "the source measure")}): walk x{walkPace:0.00} ({walkSpeed} m/s), run x{runPace:0.00} ({runSpeed} m/s), back x{backPace:0.00} ({backSpeed} m/s)");
        }

        /// <summary>The speeds of the planted foot in play at x1 (m/s), from the bot lucy: tools/characters_pace.py writes it.</summary>
        public const string PacePath = "Assets/_Game/Characters/pace_from_bot.json";

        [System.Serializable]
        public sealed class PlaySpeed
        {
            public string id;
            public float walk;
            public float run;
        }

        [System.Serializable]
        public sealed class PlaySpeedFile
        {
            public PlaySpeed[] classes;

            public PlaySpeed Find(string id)
            {
                if (classes == null) return null;
                foreach (PlaySpeed c in classes) if (c.id == id) return c;
                return null;
            }
        }

        /// <summary>The bot readings (PacePath), or null when the file is absent.</summary>
        public static PlaySpeedFile ReadPaceFromBot()
        {
            return File.Exists(PacePath) ? JsonUtility.FromJson<PlaySpeedFile>(File.ReadAllText(PacePath)) : null;
        }

        /// <summary>The pace of a clip in a class controller: the timeScale of its blend child (walk 0.5, run 1). 1 when absent.</summary>
        public static float PaceOf(AnimatorController controller, float threshold)
        {
            foreach (ChildAnimatorState s in controller.layers[0].stateMachine.states)
            {
                if (s.state.motion is BlendTree tree)
                {
                    foreach (ChildMotion c in tree.children) if (Mathf.Approximately(c.threshold, threshold)) return c.timeScale;
                }
            }
            return 1f;
        }

        /// <summary>The clips inside an FBX of ours, by their names (idle, walk, run after the import settings).</summary>
        private static Dictionary<string, AnimationClip> ClipsOf(string fbxPath)
        {
            var clips = new Dictionary<string, AnimationClip>();
            foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) clips[clip.name.ToLowerInvariant()] = clip;
            }
            return clips;
        }

        /// <summary>The Invector humanoid clips by name, from every FBX under the vendor folder. Empty when absent.</summary>
        private static Dictionary<string, AnimationClip> LoadInvectorClips()
        {
            var clips = new Dictionary<string, AnimationClip>();
            if (!AssetDatabase.IsValidFolder(InvectorFolder)) return clips;
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { InvectorFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview") && !clips.ContainsKey(clip.name)) clips[clip.name] = clip;
                }
            }
            return clips;
        }

        private static AnimationClip Pick(Dictionary<string, AnimationClip> invector, AnimationClip fallback, string use, params string[] names)
        {
            foreach (string name in names)
            {
                if (invector.TryGetValue(name, out AnimationClip clip)) return clip;
            }
            Debug.LogWarning($"[CreateCharacterAssets] no Invector clip for {use} ({string.Join(", ", names)}): the idle stands in");
            return fallback;
        }

        /// <summary>
        /// Locomotion (a 1D blend on Speed: idle 0, walk 0.5, run 1) plus the single-pose states. The clips idle,
        /// walk and run come from the class FBX; the poses from Invector (placeholders, PROPOSED).
        /// </summary>
        private static AnimatorController BuildController(string name, string clipsFrom, Dictionary<string, AnimationClip> invector, bool lucy, float height)
        {
            string path = $"{AnimFolder}/{name}.controller";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Back", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Dodge", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Charging", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Aiming", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Stagger", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Airborne", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);

            Dictionary<string, AnimationClip> own = ClipsOf($"{ModelsFolder}/{clipsFrom}.fbx");
            own.TryGetValue("idle", out AnimationClip idle);
            own.TryGetValue("walk", out AnimationClip walk);
            own.TryGetValue("run", out AnimationClip run);
            if (idle == null) Debug.LogWarning($"[CreateCharacterAssets] {clipsFrom}.fbx has no idle clip");
            walk = walk != null ? walk : idle;
            run = run != null ? run : walk;

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            if (idle != null) tree.AddChild(idle, 0f);
            if (walk != null) tree.AddChild(walk, 0.5f);
            if (run != null) tree.AddChild(run, 1f);
            sm.defaultState = locomotion;

            AnimatorState walkBack = sm.AddState("WalkBack");
            walkBack.motion = walk;
            walkBack.speed = -1f;
            ApplyPace(tree, walkBack, clipsFrom, height, lucy);
            AnimatorState dodge = sm.AddState("Dodge");
            dodge.motion = Pick(invector, idle, "the dodge", "Roll");
            // The dash lasts 0.35 s. The roll clip plays at the pace that ends it in about half a second, so the
            // legs are free again when the dash ends (and a charge right after it shows at once).
            if (dodge.motion != null && dodge.motion != idle) dodge.speed = Mathf.Max(1f, dodge.motion.averageDuration / 0.5f);
            AnimatorState pose = sm.AddState(lucy ? "Charge" : "Aim");
            pose.motion = Pick(invector, idle, lucy ? "the charge stance" : "the aim", "Aiming@Pistol", "Idle@Pistol");
            AnimatorState stagger = sm.AddState("Stagger");
            stagger.motion = Pick(invector, idle, "the stagger", "Hit Small Front", "Hit Big Front");
            AnimatorState airborne = sm.AddState("Airborne");
            airborne.motion = Pick(invector, idle, "the lift", "Falling", "Fall");
            AnimatorState dead = sm.AddState("Dead");
            dead.motion = Pick(invector, idle, "the death", "Death");

            AnimatorStateTransition t;
            t = locomotion.AddTransition(walkBack); t.hasExitTime = false; t.duration = 0.1f;
            t.AddCondition(AnimatorConditionMode.If, 0f, "Back"); t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t = walkBack.AddTransition(locomotion); t.hasExitTime = false; t.duration = 0.1f; t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Back");
            t = sm.AddAnyStateTransition(dodge); t.hasExitTime = false; t.duration = 0.05f; t.canTransitionToSelf = false; t.AddCondition(AnimatorConditionMode.If, 0f, "Dodge");
            t = dodge.AddTransition(locomotion); t.hasExitTime = true; t.exitTime = 0.8f; t.duration = 0.1f;
            t = locomotion.AddTransition(pose); t.hasExitTime = false; t.duration = 0.1f; t.AddCondition(AnimatorConditionMode.If, 0f, lucy ? "Charging" : "Aiming");
            t = pose.AddTransition(locomotion); t.hasExitTime = false; t.duration = 0.1f; t.AddCondition(AnimatorConditionMode.IfNot, 0f, lucy ? "Charging" : "Aiming");
            t = sm.AddAnyStateTransition(stagger); t.hasExitTime = false; t.duration = 0.05f; t.canTransitionToSelf = false; t.AddCondition(AnimatorConditionMode.If, 0f, "Stagger");
            t = stagger.AddTransition(locomotion); t.hasExitTime = true; t.exitTime = 0.9f; t.duration = 0.1f;
            t = sm.AddAnyStateTransition(airborne); t.hasExitTime = false; t.duration = 0.1f; t.canTransitionToSelf = false; t.AddCondition(AnimatorConditionMode.If, 0f, "Airborne");
            t = airborne.AddTransition(locomotion); t.hasExitTime = false; t.duration = 0.1f; t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Airborne");
            t = sm.AddAnyStateTransition(dead); t.hasExitTime = false; t.duration = 0.1f; t.canTransitionToSelf = false; t.AddCondition(AnimatorConditionMode.If, 0f, "Dead");

            if (lucy)
            {
                // The feed (Bruno, 2026-10-30: HP only from feeding on a body): she crouches at the body (the vendor clip).
                controller.AddParameter("Feeding", AnimatorControllerParameterType.Bool);
                AnimatorState feed = sm.AddState("Feed");
                feed.motion = Pick(invector, idle, "the feed", "Crouch_Idle");
                t = sm.AddAnyStateTransition(feed); t.hasExitTime = false; t.duration = 0.25f; t.canTransitionToSelf = false; t.AddCondition(AnimatorConditionMode.If, 0f, "Feeding");
                t = feed.AddTransition(locomotion); t.hasExitTime = false; t.duration = 0.3f; t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Feeding");
                AddHitLayer(controller, invector);
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        public const string UpperBodyMaskPath = AnimFolder + "/UpperBody.mask";

        /// <summary>
        /// Lucy's flinch (Bruno, 2026-10-27: "hit on lucy"): the vendor's small hit from the side of the shot, on a
        /// layer over the upper body only. The legs keep the walk and the tank keeps the control. "Hit" starts it,
        /// "HitDirection" picks the side (the vendor's HitAngle: 0 the front, 90 the right, -90 the left, 180 the
        /// back). The None state has no motion: the base layer shows through.
        /// </summary>
        private static void AddHitLayer(AnimatorController controller, Dictionary<string, AnimationClip> invector)
        {
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HitDirection", AnimatorControllerParameterType.Int);
            var sm = new AnimatorStateMachine { name = "Hit", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(sm, controller);
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "Hit", defaultWeight = 1f, avatarMask = UpperBodyMask(), blendingMode = AnimatorLayerBlendingMode.Override, stateMachine = sm
            });
            AnimatorState none = sm.AddState("None");
            sm.defaultState = none;
            foreach ((string side, int direction) in new[] { ("Front", 0), ("Right", 90), ("Left", -90), ("Back", 180) })
            {
                if (!invector.TryGetValue($"Hit Small {side}", out AnimationClip clip))
                {
                    Debug.LogWarning($"[CreateCharacterAssets] no Invector clip Hit Small {side}: no flinch from that side");
                    continue;
                }
                AnimatorState state = sm.AddState($"Hit_{side}");
                state.motion = clip;
                AnimatorStateTransition t = sm.AddAnyStateTransition(state);
                t.hasExitTime = false; t.duration = 0.08f; t.canTransitionToSelf = true;
                t.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
                t.AddCondition(AnimatorConditionMode.Equals, direction, "HitDirection");
                t = state.AddTransition(none);
                t.hasExitTime = true; t.exitTime = 0.85f; t.duration = 0.2f;
            }
        }

        /// <summary>The spine, the head and the arms (no root, no legs): kept, so its GUID holds between builds.</summary>
        private static AvatarMask UpperBodyMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            bool created = mask == null;
            if (created) mask = new AvatarMask();
            for (AvatarMaskBodyPart part = 0; part < AvatarMaskBodyPart.LastBodyPart; part++)
            {
                bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head
                             || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                             || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers
                             || part == AvatarMaskBodyPart.LeftHandIK || part == AvatarMaskBodyPart.RightHandIK;
                mask.SetHumanoidBodyPartActive(part, upper);
            }
            if (created) AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            else EditorUtility.SetDirty(mask);
            return mask;
        }

        /// <summary>Root (the class id) > Model (the FBX instance at height / 1 m, the material, the animator).</summary>
        private static GameObject BuildPrefab(ClassDef c, string fbxPath, Material material, AnimatorController controller)
        {
            string path = $"{PrefabsFolder}/{c.id}.prefab";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var root = new GameObject(c.id);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "Model";
            instance.transform.SetParent(root.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * (c.height / Body.ModelSourceHeight);

            Animator animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                // A part with its own maps (tools/characters_basecolor.py --maps: <class>_<part>) has its own material.
                string partId = c.id + "_" + PartName(renderer.name);
                renderer.sharedMaterial = TexturePath(partId) != null ? BuildMaterial(partId) : material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            }

            // Every model carries its character layer itself: Lucy on Player, every other figure on Enemy (the plate
            // draws the character layers over the room). The scenes put the layer on each node of the instance as an
            // override; a new model brings a node the old overrides do not name (the T49 meshes, 2026-11-16), and
            // that node keeps the prefab's layer.
            int layer = LayerMask.NameToLayer(c.role == CharacterRole.Player ? "Player" : "Enemy");
            if (layer >= 0) foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
