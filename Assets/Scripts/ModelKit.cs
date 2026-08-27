using UnityEngine;

namespace MiniMart
{
    /// <summary>
    /// Shared plumbing for the imported models. They all arrive from the same generator with five
    /// stacked LOD meshes, a normal map the project does not ship, and Z as their up axis, so every
    /// one of them needs the same handful of corrections.
    /// </summary>
    public static class ModelKit
    {
        /// <summary>
        /// The prop models are authored Z up while their FBX header claims Y up, so Unity imports
        /// them lying on their back. This rights them.
        /// </summary>
        public static readonly Vector3 ZUpFix = new Vector3(-90f, 0f, 0f);

        public const string NestModel = "Props/Nest";
        public const string ChickenModel = "Props/Chicken";
        public const string EggModel = "Items/Egg";
        public const string LegacyEggModel = "Items/FarmEgg";

        public const string CowModel = "Props/CowBlW";
        public const string SheepModel = "Props/SheepWhite";
        public const string PigModel = "Props/Pig";
        public const string DuckModel = "Props/DuckWhite";

        /// <summary>
        /// Spawns a static prop under <paramref name="parent"/>: one LOD, righted, scaled to
        /// <paramref name="targetHeight"/>, sitting on the ground with its footprint centred, and
        /// painted flat. Returns the pivot, or null when the model is missing.
        /// </summary>
        public static Transform SpawnProp(Transform parent, string resourcePath, Material material,
            float targetHeight, int preferredLod, Vector3 upFixEuler)
        {
            GameObject asset = Resources.Load<GameObject>(resourcePath);
            if (asset == null) return null;

            int slash = resourcePath.LastIndexOf('/');
            GameObject pivot = new GameObject(slash >= 0 ? resourcePath.Substring(slash + 1) : resourcePath);
            pivot.transform.SetParent(parent, false);

            GameObject model = Object.Instantiate(asset, pivot.transform);
            model.name = "Mesh";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(upFixEuler);
            model.transform.localScale = Vector3.one;

            KeepOneLod(model, preferredLod);
            SitOnGround(pivot.transform, model.transform, targetHeight);
            Paint(model, material);
            StripColliders(model);
            return pivot.transform;
        }

        /// <summary>
        /// Scales <paramref name="model"/> so it stands <paramref name="targetHeight"/> tall in
        /// <paramref name="space"/>, with its base at y = 0 and centred horizontally.
        /// </summary>
        public static float SitOnGround(Transform space, Transform model, float targetHeight)
        {
            if (!TryMeasure(space, model, out Bounds bounds) || bounds.size.y <= 0.0001f) return 1f;

            float scale = Mathf.Clamp(targetHeight / bounds.size.y, 0.0005f, 200f);
            model.localScale = Vector3.one * scale;
            model.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
            return scale;
        }

        /// <summary>Combined mesh bounds of <paramref name="root"/> expressed in <paramref name="space"/>.</summary>
        public static bool TryMeasure(Transform space, Transform root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool measured = false;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : null;
                if (mesh == null)
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    mesh = filter != null ? filter.sharedMesh : null;
                }
                if (mesh == null) continue;

                Bounds local = mesh.bounds;
                Matrix4x4 toSpace = space.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = new Vector3(
                        (corner & 1) == 0 ? local.min.x : local.max.x,
                        (corner & 2) == 0 ? local.min.y : local.max.y,
                        (corner & 4) == 0 ? local.min.z : local.max.z);
                    Vector3 inSpace = toSpace.MultiplyPoint3x4(point);
                    if (measured) bounds.Encapsulate(inSpace);
                    else { bounds = new Bounds(inSpace, Vector3.zero); measured = true; }
                }
            }
            return measured;
        }

        /// <summary>
        /// The models ship model_LOD0..LOD4 stacked in one hierarchy with an LODGroup. Keeping a
        /// single mesh avoids five overlapping copies and lets small props use a cheap one.
        /// </summary>
        public static void KeepOneLod(GameObject model, int preferredLod)
        {
            LODGroup group = model.GetComponent<LODGroup>();
            if (group != null) Object.Destroy(group);

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length <= 1) return;

            string wanted = "LOD" + Mathf.Clamp(preferredLod, 0, renderers.Length - 1);
            int keep = -1;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].name.EndsWith(wanted)) continue;
                keep = i;
                break;
            }
            if (keep < 0) return; // not the LOD naming convention, leave it alone

            for (int i = 0; i < renderers.Length; i++)
            {
                if (i != keep) Object.Destroy(renderers[i].gameObject);
            }
        }

        public static void Paint(GameObject root, Material material)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
        }

        public static void StripColliders(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) Object.Destroy(collider);
        }
    }
}
