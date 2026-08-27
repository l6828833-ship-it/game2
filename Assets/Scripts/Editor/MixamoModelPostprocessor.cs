using UnityEditor;
using UnityEngine;

namespace MiniMart.EditorTools
{
    /// <summary>
    /// Import settings for the Mixamo character live in code rather than in a hand tweaked
    /// Inspector, so a fresh clone imports the model the same way this machine did.
    ///
    /// Only applied when the asset has no .meta yet, which leaves manual changes alone afterwards.
    /// </summary>
    public class MixamoModelPostprocessor : AssetPostprocessor
    {
        private const string AnimatedPlayerPrefix = "Assets/Resources/Characters/FarmPlayer";

        private static readonly string[] AnimatedPlayerAssets =
        {
            AnimatedPlayerPrefix + "Run.fbx",
            AnimatedPlayerPrefix + "Idle.fbx",
            AnimatedPlayerPrefix + "CarryIdle.fbx"
        };

        /// <summary>
        /// Safety net for the case where the model is imported before this script has compiled:
        /// on the next domain reload the rig is checked and reimported once if it is wrong.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void EnsureAnimatedPlayerRigs()
        {
            for (int i = 0; i < AnimatedPlayerAssets.Length; i++) EnsureAnimatedRig(AnimatedPlayerAssets[i]);
        }

        private static void EnsureAnimatedRig(string assetPath)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is ModelImporter importer)) return;

            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }
            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                changed = true;
            }
            if (!changed) return;

            importer.SaveAndReimport();
            Debug.Log("Reimported " + assetPath + " as a generic animated rig for the mini mart player.");
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(AnimatedPlayerPrefix) || !assetPath.EndsWith(".fbx")) return;
            if (!(assetImporter is ModelImporter importer) || !importer.importSettingsMissing) return;

            // Generic keeps the clip bound to the rig it shipped with: no avatar mapping to get wrong.
            importer.animationType = ModelImporterAnimationType.Generic;
            // Without an avatar Unity imports the clip but adds no Animator, so nothing can play it.
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;

            // The FBX points at a normal map that is not in the project, and the game paints the
            // character with its own flat material anyway.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;

            Debug.Log("Configured " + assetPath + " as a generic animated rig for the mini mart player.");
        }
    }
}
