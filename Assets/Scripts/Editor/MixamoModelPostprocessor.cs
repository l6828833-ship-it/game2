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
        private const string CharacterFolder = "Assets/Resources/Characters/";

        /// <summary>Folders whose FBX models are static props: no rig, no animation.</summary>
        private static readonly string[] PropFolders =
        {
            "Assets/Resources/Props/",
            "Assets/Resources/Items/"
        };

        private static readonly string[] AnimatedPlayerAssets =
        {
            CharacterFolder + "FarmPlayerRun.fbx",
            CharacterFolder + "FarmPlayerIdle.fbx",
            CharacterFolder + "FarmPlayerCarryIdle.fbx",
            CharacterFolder + "FarmCustomerWalk.fbx"
        };

        private static bool IsProp(string path)
        {
            for (int i = 0; i < PropFolders.Length; i++)
            {
                if (path.StartsWith(PropFolders[i])) return true;
            }
            return false;
        }

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
            bool character = assetPath.StartsWith(CharacterFolder);
            if (!assetPath.EndsWith(".fbx") || (!character && !IsProp(assetPath))) return;
            if (!(assetImporter is ModelImporter importer) || !importer.importSettingsMissing) return;

            // Every one of these models references a normal map the project does not ship, and the
            // game paints them with its own flat materials anyway.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;

            if (!character)
            {
                // Props are static meshes: no rig, no clips.
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                Debug.Log("Configured " + assetPath + " as a static mini mart prop.");
                return;
            }

            // Generic keeps the clip bound to the rig it shipped with: no avatar mapping to get wrong.
            importer.animationType = ModelImporterAnimationType.Generic;
            // Without an avatar Unity imports the clip but adds no Animator, so nothing can play it.
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;

            Debug.Log("Configured " + assetPath + " as a generic animated rig for the mini mart player.");
        }
    }
}
