using UnityEngine;

namespace MiniMart
{
    /// <summary>
    /// Which imported mesh stands in for each product, and how big it should be in each place it
    /// turns up. Anything without an entry falls back to a coloured primitive, so new products can
    /// be added here one line at a time.
    ///
    /// The up axis is recorded per product rather than assumed: even from one supplier the OBJ
    /// exports arrive Y up and grounded while the FBX exports are Z up.
    /// </summary>
    public static class ProductVisuals
    {
        public readonly struct Visual
        {
            public readonly string Model;
            public readonly Vector3 UpFix;

            /// <summary>Height on a shop shelf, on a crop plant, and in a hand.</summary>
            public readonly float ShelfHeight;
            public readonly float CropHeight;
            public readonly float HandHeight;

            /// <summary>Cheap mesh for the many copies on a shelf, better one for close up work.</summary>
            public readonly int ShelfLod;
            public readonly int DetailLod;

            public Visual(string model, Vector3 upFix, float shelfHeight, float cropHeight, float handHeight,
                int shelfLod, int detailLod)
            {
                Model = model;
                UpFix = upFix;
                ShelfHeight = shelfHeight;
                CropHeight = cropHeight;
                HandHeight = handHeight;
                ShelfLod = shelfLod;
                DetailLod = detailLod;
            }
        }

        public static bool TryGet(ProductKind kind, out Visual visual)
        {
            switch (kind)
            {
                case ProductKind.Egg:
                    // User-supplied nest egg: five LODs, Z up.
                    visual = new Visual("Items/FarmNestEgg", ModelKit.ZUpFix, 0.14f, 0.15f, 0.16f, 4, 3);
                    return true;
                case ProductKind.Banana:
                    visual = new Visual("Items/Banana", ModelKit.ZUpFix, 0.28f, 0.42f, 0.34f, 4, 3);
                    return true;
                case ProductKind.Tomato:
                    // OBJ export: single mesh, already Y up and sitting on zero, so no LOD or fix.
                    visual = new Visual("Items/Tomato", Vector3.zero, 0.28f, 0.42f, 0.34f, 0, 0);
                    return true;
                case ProductKind.Watermelon:
                    visual = new Visual("Items/Watermelon", Vector3.zero, 0.16f, 0.14f, 0.20f, 0, 0);
                    return true;
                default:
                    visual = default;
                    return false;
            }
        }

        public static bool Has(ProductKind kind) => TryGet(kind, out _);
    }
}
