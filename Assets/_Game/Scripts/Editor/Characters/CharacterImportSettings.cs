#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GlorifiedBody.Editor.Characters
{
    /// <summary>The measured travel of one clip (tools/characters_clips.py): the hips over one cycle, in source units.</summary>
    [Serializable]
    public sealed class ClipTravel
    {
        public string name;
        public float duration;
        public int keys;
        public float travel;
        public float reach;
        public bool travels;
        /// <summary>The planted foot's speed against the hips, source units a second (0 in an older file).</summary>
        public float ground;
    }

    /// <summary>The file next to an FBX, <c>&lt;class&gt;.clips.json</c>, written by tools/characters.sh.</summary>
    [Serializable]
    public sealed class ClipTravelFile
    {
        public string source;
        public string hips;
        public string[] chain;
        public float travelMin;
        public ClipTravel[] clips;

        public ClipTravel Find(string clipName)
        {
            if (clips == null) return null;
            foreach (ClipTravel clip in clips)
            {
                if (string.Equals(clip.name, clipName, StringComparison.OrdinalIgnoreCase)) return clip;
            }
            return null;
        }

        /// <summary>One line for a log: "idle stays, walk travels 1.53, run travels 2.95".</summary>
        public string Describe()
        {
            var text = new StringBuilder();
            if (clips == null) return "no clips";
            foreach (ClipTravel clip in clips)
            {
                if (text.Length > 0) text.Append(", ");
                text.Append(clip.name).Append(clip.travels ? " travels " + clip.travel.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : " stays");
            }
            return text.ToString();
        }
    }

    /// <summary>
    /// The import settings of the T19 characters (Assets/_Game/Characters/Models): the FBX as a Humanoid with
    /// its own avatar and no materials, every clip a loop played in place, and the maps (a T19 base colour at
    /// 1024, a T49 base colour, normal and metallic-smoothness set at 2048) with point sampling and no compression (the upscaled-PS1 look, ART_LESSONS §1 and §3). Numbers PROPOSED.
    /// A clip that travels in the source (the T19 walk and run: 1.5 m and 3.0 m per cycle at 1 m scale, the
    /// hips move) gives its travel to the root motion, which the Animator does not apply (no root motion on the
    /// prefab), so the body stays over the root. Baked into the pose, the model ran ahead of the controller and
    /// snapped back at each loop (Bruno, 2026-10-21). A clip that stays (the idle) keeps its root in the pose,
    /// with its sway. The decision comes from the measurement next to the FBX (<c>&lt;class&gt;.clips.json</c>,
    /// tools/characters_clips.py), not from the clip name. Without the file, the names decide (walk, run) and
    /// the import logs a warning. The EditMode test CharacterImportTests checks every FBX against its file.
    /// </summary>
    public sealed class CharacterImportSettings : AssetPostprocessor
    {
        public const string Folder = "Assets/_Game/Characters/Models/";
        public const int TextureSize = 1024;
        public const int MapSetTextureSize = 2048;

        private bool Ours => assetPath.StartsWith(Folder);

        /// <summary>The measurement file next to an FBX: "Models/guard.fbx" gives "Models/guard.clips.json".</summary>
        public static string ClipsJsonPath(string fbxPath) => Path.ChangeExtension(fbxPath, ".clips.json");

        /// <summary>Reads the measurement next to an FBX. Null when the file is absent or unreadable.</summary>
        public static ClipTravelFile ReadClipTravel(string fbxPath)
        {
            string path = ClipsJsonPath(fbxPath);
            if (!File.Exists(path)) return null;
            try
            {
                ClipTravelFile file = JsonUtility.FromJson<ClipTravelFile>(File.ReadAllText(path));
                return file != null && file.clips != null ? file : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CharacterImportSettings] {path}: unreadable ({e.Message}).");
                return null;
            }
        }

        private void OnPreprocessModel()
        {
            if (!Ours) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = true; // SeveredLimb reads the bone weights and the triangles (about 100 KB a class)
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        private void OnPreprocessAnimation()
        {
            if (!Ours) return;
            var importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            ClipTravelFile measured = ReadClipTravel(assetPath);
            if (measured == null)
            {
                Debug.LogWarning($"[CharacterImportSettings] {assetPath}: no {Path.GetFileName(ClipsJsonPath(assetPath))} next to the FBX (tools/characters.sh writes it). The clip names decide the root motion: walk and run travel.");
            }
            foreach (ModelImporterClipAnimation clip in clips)
            {
                // Blender names a take "<armature>|<action>". The game wants "idle", "walk", "run".
                int bar = clip.takeName.LastIndexOf('|');
                clip.name = bar >= 0 ? clip.takeName.Substring(bar + 1) : clip.takeName;
                ClipTravel info = measured?.Find(clip.name);
                if (measured != null && info == null)
                {
                    Debug.LogWarning($"[CharacterImportSettings] {assetPath}: the clip '{clip.name}' is not in the measurement. Its name decides the root motion.");
                }
                bool travels = info != null ? info.travels : Travels(clip.name);
                clip.loopTime = true;
                clip.loopPose = false;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                // A clip that travels: the XZ motion of the body goes to the root motion (not baked), and the
                // body stays centred over the root (the centre of mass, not the original offset).
                clip.lockRootPositionXZ = !travels;
                // A rig that stands turned in its own file (TurnedRigs) takes its root turn from the body, so the body faces the
                // root's forward. Every other class keeps the turn of its file.
                clip.keepOriginalOrientation = !TurnedRigs.Contains(Path.GetFileNameWithoutExtension(assetPath));
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = !travels;
                clip.heightOffset = Lift(Path.GetFileNameWithoutExtension(assetPath));
            }
            importer.clipAnimations = clips;
        }

        /// <summary>
        /// The classes whose rig stands turned against its file's forward: with the body orientation, Unity turns each clip
        /// so the body faces the root (the walk can still step off the body). None now. The round 183 power armour stood
        /// 14 degrees off +X and was on the list; the round 185 file is rigged again straight (the mesh turned 13 degrees
        /// before the rig) and faces forward with the file's turn.
        /// </summary>
        public static readonly string[] TurnedRigs = { };

        /// <summary>
        /// The classes whose clips stand under the floor: the clip import's root height offset ("level") for each clip of the
        /// class. A negative value lifts the body (Unity subtracts it), in the units of the Humanoid body, not of the mesh.
        /// The round 185 power armour: in the file the lowest vertex of the idle is 0.7 to 1.3 cm under the ground (1 m
        /// model), the walk and the run go to 2.3 cm under it. In the game the idle stood 4.2 cm in the floor at 2.90 m (the
        /// bot limit is 3.5 cm); with -0.01 it stands 2.4 cm in it (the bot lucy, 2026-11-16). With +0.01 the PlayMode
        /// CharacterTests feet check failed (the bounds 0.189 m under the root, limit 0.174). PROPOSED: a fix upstream (the clips on the ground in the file) removes this entry.
        /// </summary>
        public static readonly (string id, float offset)[] Lifts = { ("power_armour", -0.01f) };

        /// <summary>The root height offset of the clips of a class (0 for a class that is not in Lifts).</summary>
        public static float Lift(string id)
        {
            foreach ((string liftId, float offset) in Lifts) if (liftId == id) return offset;
            return 0f;
        }

        /// <summary>The fallback without a measurement: the clips named walk and run travel.</summary>
        public static bool Travels(string clipName)
        {
            string name = clipName.ToLowerInvariant();
            return name == "walk" || name == "run";
        }

        private void OnPreprocessTexture()
        {
            if (!Ours) return;
            var importer = (TextureImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            bool normal = name.EndsWith("_normal");
            bool data = normal || name.EndsWith("_ms");
            // A T19 class: the base colour at 1024. A T49 class (a <class>_normal.png next to it): the map set at 2048
            // (round 24: "the helmet maps at 2048"; round 175: "2048 maps").
            string stem = name.EndsWith("_basecolor") ? name.Substring(0, name.Length - "_basecolor".Length) : name;
            bool mapSet = data || File.Exists(Path.Combine(Path.GetDirectoryName(assetPath), stem + "_normal.png"));
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.maxTextureSize = mapSet ? MapSetTextureSize : TextureSize;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = true;
            // A T19 base colour (1024) stays uncompressed (the PS1 look). A T49 map set (2048, three maps for each
            // material) is compressed at high quality: uncompressed it would take about 21 MB a map on a 2 GB card.
            importer.textureCompression = mapSet ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = !data; // the normal and the metallic-smoothness maps are data, not colour
            importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Repeat;
        }

        /// <summary>
        /// True when the reference pose saved in the importer (humanDescription.skeleton) no longer matches the
        /// bones of the FBX on disk: a new model under the old file name (T49 Lucy over the T19 Lucy, 2026-11-16).
        /// Unity keeps the old reference pose on a reimport, and the avatar then bends the new body with the old
        /// proportions.
        /// </summary>
        public static bool AvatarIsStale(string fbxPath, out string reason)
        {
            reason = "";
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (importer == null || model == null) return false;
            SkeletonBone[] skeleton = importer.humanDescription.skeleton;
            if (skeleton == null || skeleton.Length == 0) return false;
            // The names, not the positions: a T19 reference pose sits a millimetre off its own FBX (the import
            // rounds), and a new model from another source brings other node names (the mesh, the helpers).
            var saved = new System.Collections.Generic.HashSet<string>();
            for (int i = 1; i < skeleton.Length; i++) saved.Add(skeleton[i].name); // [0] is the root object ("<class>(Clone)")
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t == model.transform) continue;
                if (!saved.Remove(t.name)) { reason = $"a new node {t.name}"; return true; }
            }
            if (saved.Count > 0) { reason = $"no node {saved.First()}"; return true; }
            return false;
        }

        /// <summary>
        /// Clears the saved bone map and reference pose of an FBX and imports it again: Unity maps the Humanoid
        /// from the new bones (the importer's auto mapping). The muscle settings stay. True when it reset.
        /// </summary>
        public static bool RefreshStaleAvatar(string fbxPath)
        {
            if (!AvatarIsStale(fbxPath, out string reason)) return false;
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            HumanDescription description = importer.humanDescription;
            description.human = new HumanBone[0];
            description.skeleton = new SkeletonBone[0];
            importer.humanDescription = description;
            importer.autoGenerateAvatarMappingIfUnspecified = true;
            importer.SaveAndReimport();
            Avatar avatar = AssetDatabase.LoadAssetAtPath<Avatar>(fbxPath);
            Debug.Log($"[CharacterImportSettings] {fbxPath}: the saved avatar did not fit the bones ({reason}); mapped again: " +
                      $"valid={(avatar != null && avatar.isValid)} human={(avatar != null && avatar.isHuman)} " +
                      $"bones={importer.humanDescription.human.Length}");
            return true;
        }
    }
}
#endif
