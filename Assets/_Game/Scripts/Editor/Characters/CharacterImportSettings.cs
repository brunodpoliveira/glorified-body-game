#if UNITY_EDITOR
using System;
using System.IO;
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
    /// its own avatar and no materials, every clip a loop played in place, and the base colour at 1024 with
    /// point sampling and no compression (the upscaled-PS1 look, ART_LESSONS §1 and §3). Numbers PROPOSED.
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
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = !travels;
            }
            importer.clipAnimations = clips;
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
            importer.textureType = TextureImporterType.Default;
            importer.maxTextureSize = TextureSize;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = false;
            importer.wrapMode = TextureWrapMode.Repeat;
        }
    }
}
#endif
