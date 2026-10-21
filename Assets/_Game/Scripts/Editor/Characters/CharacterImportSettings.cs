#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GlorifiedBody.Editor.Characters
{
    /// <summary>
    /// The import settings of the T19 characters (Assets/_Game/Characters/Models): the FBX as a Humanoid with
    /// its own avatar and no materials, every clip a loop played in place, and the base colour at 1024 with
    /// point sampling and no compression (the upscaled-PS1 look, ART_LESSONS §1 and §3). Numbers PROPOSED.
    /// The walk and the run travel in the source (1.5 m and 3.0 m per cycle at 1 m scale, the hips move). The
    /// import takes that travel out as root motion, which the Animator does not apply (no root motion on the
    /// prefab), so the body stays over the root. Baked into the pose, the model ran ahead of the controller and
    /// snapped back at each loop (Bruno, 2026-10-21). The idle does not travel: its sway stays in the pose.
    /// </summary>
    public sealed class CharacterImportSettings : AssetPostprocessor
    {
        public const string Folder = "Assets/_Game/Characters/Models/";
        public const int TextureSize = 1024;

        private bool Ours => assetPath.StartsWith(Folder);

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
            importer.isReadable = false;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        private void OnPreprocessAnimation()
        {
            if (!Ours) return;
            var importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                // Blender names a take "<armature>|<action>". The game wants "idle", "walk", "run".
                int bar = clip.takeName.LastIndexOf('|');
                clip.name = bar >= 0 ? clip.takeName.Substring(bar + 1) : clip.takeName;
                bool travels = Travels(clip.name);
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

        /// <summary>The clips whose source moves the body across the floor: the walk and the run.</summary>
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
