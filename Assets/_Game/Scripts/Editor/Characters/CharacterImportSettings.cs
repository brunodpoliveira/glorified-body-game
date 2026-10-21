#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GlorifiedBody.Editor.Characters
{
    /// <summary>
    /// The import settings of the T19 characters (Assets/_Game/Characters/Models): the FBX as a Humanoid with
    /// its own avatar and no materials, every clip a loop kept in place, and the base colour at 1024 with point
    /// sampling and no compression (the upscaled-PS1 look, ART_LESSONS §1 and §3). Numbers PROPOSED.
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
                clip.loopTime = true;
                clip.loopPose = false;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
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
