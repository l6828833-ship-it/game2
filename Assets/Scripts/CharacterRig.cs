using System.Collections.Generic;
using UnityEngine;

namespace MiniMart
{
    /// <summary>
    /// Shared setup for the imported Mixamo characters. The player and every shopper are the same
    /// body and skeleton, so this handles the parts they both need: one visible LOD instead of the
    /// five Mixamo stacks on top of each other, a size that matches the game scale, a flat colour,
    /// no colliders, and an Animator so clips can actually be played.
    /// </summary>
    public static class CharacterRig
    {
        /// <summary>Every animated FBX ships the same mesh, so one of them is the body for everyone.</summary>
        public const string BodyModel = "Characters/FarmPlayerRun";

        public const string RunClip = "Characters/FarmPlayerRun";
        public const string IdleClip = "Characters/FarmPlayerIdle";
        public const string CarryIdleClip = "Characters/FarmPlayerCarryIdle";
        public const string CustomerWalkClip = "Characters/FarmCustomerWalk";

        private static readonly Dictionary<string, AnimationClip> ClipCache = new Dictionary<string, AnimationClip>();

        public class Rig
        {
            public GameObject Model;
            public Animator Animator;
            public Transform Pelvis;
            public float Scale;
        }

        /// <summary>
        /// Instantiates the shared body under <paramref name="parent"/>. Returns null when the model
        /// is missing so callers can fall back to primitives.
        /// </summary>
        public static Rig Build(Transform parent, Material material, float targetHeight, int preferredLod)
        {
            GameObject asset = Resources.Load<GameObject>(BodyModel);
            if (asset == null) return null;

            GameObject model = Object.Instantiate(asset, parent);
            model.name = "Character_Body";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            ModelKit.KeepOneLod(model, preferredLod);
            float scale = FitHeight(model.transform, targetHeight);
            ModelKit.Paint(model, material);
            ModelKit.StripColliders(model);

            // An FBX imported with Avatar Setup "No Avatar" carries clips but no Animator. Generic
            // clips bind by transform path, so adding one on the model root is enough.
            Animator animator = model.GetComponent<Animator>() ?? model.GetComponentInChildren<Animator>(true);
            if (animator == null) animator = model.AddComponent<Animator>();

            return new Rig
            {
                Model = model,
                Animator = animator,
                Pelvis = FindPelvis(model.transform),
                Scale = scale
            };
        }

        /// <summary>Longest real clip in an imported model, cached so shoppers do not reload it each spawn.</summary>
        public static AnimationClip LoadClip(string resourcePath)
        {
            if (ClipCache.TryGetValue(resourcePath, out AnimationClip cached) && cached != null) return cached;

            AnimationClip[] clips = Resources.LoadAll<AnimationClip>(resourcePath);
            AnimationClip best = null;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null || clip.name.StartsWith("__preview__")) continue;
                if (best == null || clip.length > best.length) best = clip;
            }
            ClipCache[resourcePath] = best;
            return best;
        }

        /// <summary>
        /// Scales the body to <paramref name="targetHeight"/> and drops it so the feet sit on the
        /// floor. Measured from mesh bounds rather than assuming an FBX import scale.
        /// </summary>
        public static float FitHeight(Transform model, float targetHeight)
        {
            if (!ModelKit.TryMeasure(model, model, out Bounds bounds) || bounds.size.y <= 0.0001f) return 1f;

            float scale = Mathf.Clamp(targetHeight / bounds.size.y, 0.005f, 20f);
            model.localScale = Vector3.one * scale;
            model.localPosition = new Vector3(0f, -bounds.min.y * scale, 0f);
            return scale;
        }

        /// <summary>The bone the clips translate. Mixamo calls it mixamorig:Hips.</summary>
        public static Transform FindPelvis(Transform modelRoot)
        {
            foreach (Transform bone in modelRoot.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name.IndexOf("Hips", System.StringComparison.OrdinalIgnoreCase) >= 0) return bone;
            }
            return null;
        }

        public static void Paint(GameObject root, Material material) => ModelKit.Paint(root, material);

        public static void StripColliders(GameObject root) => ModelKit.StripColliders(root);
    }
}
