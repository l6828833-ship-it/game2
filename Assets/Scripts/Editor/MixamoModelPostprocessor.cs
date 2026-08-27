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
        private const string AnimatedPlayerPath = "Assets/Resources/Characters/FarmPlayerRun";
        private const string AnimatedPlayerAsset = AnimatedPlayerPath + ".fbx";

        /// <summary>
        /// Safety net for the case where the model is imported before this script has compiled:
        /// on the next domain reload the rig is checked and reimported once if it is wrong.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void EnsureAnimatedPlayerRig()
        {
            if (!(AssetImporter.GetAtPath(AnimatedPlayerAsset) is ModelImporter importer)) return;

            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }
            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                changed = true;
            }
            if (!changed) return;

            importer.SaveAndReimport();
            Debug.Log("Reimported " + AnimatedPlayerAsset + " as a generic animated rig for the mini mart player.");
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(AnimatedPlayerPath)) return;
            if (!(assetImporter is ModelImporter importer) || !importer.importSettingsMissing) return;

            // Generic keeps the clip bound to the rig it shipped with: no avatar mapping to get wrong.
            importer.animationType = ModelImporterAnimationType.Generic;
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
