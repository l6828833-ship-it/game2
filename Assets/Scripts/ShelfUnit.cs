using System.Collections.Generic;
using UnityEngine;

namespace MiniMart
{
    /// <summary>
    /// A sellable store display. Normal products use the three-board shelf; eggs use the user's
    /// four-slot or six-slot table and sit only in its intended recessed socket positions.
    /// </summary>
    public class ShelfUnit : MonoBehaviour
    {
        private static readonly Vector3[] EggSlotsFour =
        {
            // Four diamond-pattern sockets on the table top surface. The table is 0.95m tall after
            // scaling, so y = 0.92 sits the eggs right on the surface. Positions are in table-local
            // space (the root is rotated so open side faces camera).
            new Vector3(-0.18f, 0.92f, -0.18f),
            new Vector3(0.18f, 0.92f, -0.18f),
            new Vector3(-0.18f, 0.92f, 0.18f),
            new Vector3(0.18f, 0.92f, 0.18f)
        };

        private static readonly Vector3[] EggSlotsSix =
        {
            new Vector3(-0.22f, 0.92f, -0.16f),
            new Vector3(0f, 0.92f, -0.16f),
            new Vector3(0.22f, 0.92f, -0.16f),
            new Vector3(-0.22f, 0.92f, 0.16f),
            new Vector3(0f, 0.92f, 0.16f),
            new Vector3(0.22f, 0.92f, 0.16f)
        };

        public ProductKind Product { get; private set; }
        public int Stock { get; private set; }
        public int Capacity => eggTable ? (eggTableUpgraded ? 6 : 4) : GameConfig.ShelfCapacity;
        public bool IsFull => Stock >= Capacity;
        public bool IsEmpty => Stock <= 0;
        public bool IsEggTable => eggTable;
        public bool CanUpgradeEggTable => eggTable && !eggTableUpgraded;

        private readonly List<GameObject> visuals = new List<GameObject>();
        private int unitsPerVisual = 1;
        private bool eggTable;
        private bool eggTableUpgraded;
        private Transform tableMesh;

        public void Initialise(ProductKind kind, int stock)
        {
            Product = kind;
            eggTable = false;
            eggTableUpgraded = false;
            Stock = Mathf.Clamp(stock, 0, Capacity);
            MiniMartGameManager game = MiniMartGameManager.Instance;
            Material wood = game.MaterialFor("ShelfWood", new Color(0.49f, 0.27f, 0.15f));

            GameObject back = game.CreatePrimitive(PrimitiveType.Cube, "Shelf_Back", transform.position,
                new Vector3(2.15f, 2.35f, 0.14f), wood, transform);
            back.transform.localPosition = new Vector3(0f, 1.2f, 0.22f);

            for (int row = 0; row < 3; row++)
            {
                GameObject board = game.CreatePrimitive(PrimitiveType.Cube, "Shelf_Board", transform.position,
                    new Vector3(2.3f, 0.1f, 0.65f), wood, transform);
                board.transform.localPosition = new Vector3(0f, 0.35f + row * 0.72f, 0f);
            }

            GameObject marker = game.CreateDecor(PrimitiveType.Cube, "Product_Label_" + kind, transform.position,
                new Vector3(1.45f, 0.2f, 0.07f), game.MaterialFor("Label_" + kind, game.ProductColor(kind)), transform);
            marker.transform.localPosition = new Vector3(0f, 2.15f, -0.37f);

            BuildProductPool();
            RebuildVisuals();
        }

        /// <summary>Creates the dedicated in-store egg table using the user-supplied table model.</summary>
        public void InitialiseEggTable(int stock, bool upgraded)
        {
            Product = ProductKind.Egg;
            eggTable = true;
            eggTableUpgraded = upgraded;
            Stock = Mathf.Clamp(stock, 0, Capacity);
            BuildEggTableMesh();
            BuildProductPool();
            RebuildVisuals();
        }

        /// <summary>Switches the table model and exposes the two extra egg sockets, without losing stock.</summary>
        public void UpgradeEggTable()
        {
            if (!CanUpgradeEggTable) return;
            eggTableUpgraded = true;
            ClearProductPool();
            if (tableMesh != null) Destroy(tableMesh.gameObject);
            BuildEggTableMesh();
            BuildProductPool();
            RebuildVisuals();
        }

        /// <summary>Shopper picks one item off the display.</summary>
        public bool TakeOne()
        {
            if (Stock <= 0) return false;
            Stock--;
            RebuildVisuals();
            return true;
        }

        /// <summary>A shopper who abandoned the queue puts their item back.</summary>
        public void ReturnOne()
        {
            if (IsFull) return;
            Stock++;
            RebuildVisuals();
        }

        /// <summary>Adds up to <paramref name="amount"/> units and returns how many actually fit.</summary>
        public int Restock(ProductKind kind, int amount)
        {
            if (kind != Product || amount <= 0) return 0;
            int placed = Mathf.Min(amount, Capacity - Stock);
            if (placed <= 0) return 0;
            Stock += placed;
            RebuildVisuals();
            return placed;
        }

        private void BuildEggTableMesh()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            Material wood = game.MaterialFor("EggTableWood", new Color(0.58f, 0.34f, 0.16f));

            // The normal table is the user's GLB model converted to FBX. Its original material is
            // deliberately preserved instead of receiving the flat brown paint used by old props.
            tableMesh = eggTableUpgraded
                ? ModelKit.SpawnProp(transform, "Props/EggTable6", wood, 0.95f, 0, Vector3.zero)
                : SpawnUserEggTable();
            if (tableMesh == null)
            {
                // The store remains playable if a model import has not completed yet.
                GameObject fallback = game.CreateDecor(PrimitiveType.Cube, "Egg_Table_Fallback", transform.position,
                    new Vector3(1.45f, 0.72f, 1.10f), wood, transform);
                fallback.transform.localPosition = new Vector3(0f, 0.36f, 0f);
                tableMesh = fallback.transform;
            }
            tableMesh.name = eggTableUpgraded ? "Egg_Table_6_Slots" : "Egg_Table_4_Slots";
        }

        /// <summary>Spawns the user's textured four-slot table in its native Y-up orientation.</summary>
        private Transform SpawnUserEggTable()
        {
            GameObject asset = Resources.Load<GameObject>("Props/UserEggTable4");
            if (asset == null) return null;

            GameObject pivot = new GameObject("User_Egg_Table_4_Slots");
            pivot.transform.SetParent(transform, false);
            GameObject model = Instantiate(asset, pivot.transform);
            model.name = "Mesh";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            ModelKit.KeepOneLod(model, 0);
            ModelKit.SitOnGround(pivot.transform, model.transform, 0.95f);
            ModelKit.StripColliders(model);
            return pivot.transform;
        }

        /// <summary>Creates each display object once and then shows only the stocked positions.</summary>
        private void BuildProductPool()
        {
            if (eggTable)
            {
                BuildEggTableProductPool();
                return;
            }

            Transform displayRoot = new GameObject("Products").transform;
            displayRoot.SetParent(transform, false);
            displayRoot.localPosition = Vector3.zero;

            MiniMartGameManager game = MiniMartGameManager.Instance;
            Material material = game.MaterialFor("Product_" + Product, game.ProductColor(Product));
            bool hasModel = ProductVisuals.TryGet(Product, out ProductVisuals.Visual visual);

            unitsPerVisual = hasModel ? 2 : 1;
            int columns = hasModel ? 4 : 5;
            int rows = hasModel ? 2 : 3;
            float pitch = hasModel ? 0.42f : 0.36f;
            float left = -pitch * (columns - 1) * 0.5f;

            PrimitiveType shape = Product == ProductKind.Apple || Product == ProductKind.Egg
                ? PrimitiveType.Sphere
                : PrimitiveType.Cube;

            for (int index = 0; index < columns * rows; index++)
            {
                int row = index / columns;
                int col = index % columns;
                Vector3 slot = new Vector3(left + col * pitch, 0.5f + row * 0.72f, -0.26f);

                GameObject product;
                if (hasModel)
                {
                    Transform pivot = ModelKit.SpawnProp(displayRoot, visual.Model, material,
                        visual.ShelfHeight, visual.ShelfLod, visual.UpFix);
                    if (pivot == null)
                    {
                        hasModel = false;
                        unitsPerVisual = 1;
                        continue;
                    }
                    pivot.localPosition = slot;
                    pivot.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    product = pivot.gameObject;
                }
                else
                {
                    product = game.CreateDecor(shape, Product + "_Item", transform.position,
                        new Vector3(0.22f, 0.28f, 0.2f), material, displayRoot);
                    product.transform.localPosition = new Vector3(slot.x, slot.y + 0.04f, -0.28f);
                }

                product.SetActive(false);
                visuals.Add(product);
            }
        }

        private void BuildEggTableProductPool()
        {
            Transform displayRoot = new GameObject("Egg_Slots").transform;
            displayRoot.SetParent(transform, false);
            displayRoot.localPosition = Vector3.zero;

            MiniMartGameManager game = MiniMartGameManager.Instance;
            Material eggMaterial = game.MaterialFor("Product_Egg", Color.white);
            ProductVisuals.TryGet(ProductKind.Egg, out ProductVisuals.Visual visual);
            Vector3[] slots = eggTableUpgraded ? EggSlotsSix : EggSlotsFour;
            unitsPerVisual = 1;

            for (int index = 0; index < slots.Length; index++)
            {
                Transform pivot = ModelKit.SpawnProp(displayRoot, visual.Model, eggMaterial,
                    visual.ShelfHeight, visual.DetailLod, visual.UpFix);
                if (pivot == null) break;
                pivot.name = "Egg_Slot_" + (index + 1);
                pivot.localPosition = slots[index];
                pivot.localRotation = Quaternion.Euler(0f, index * 37f, 0f);
                pivot.gameObject.SetActive(false);
                visuals.Add(pivot.gameObject);
            }
        }

        private void ClearProductPool()
        {
            for (int i = 0; i < visuals.Count; i++)
                if (visuals[i] != null) Destroy(visuals[i]);
            visuals.Clear();
        }

        private void RebuildVisuals()
        {
            int shown = eggTable ? Stock : Mathf.CeilToInt(Stock / (float)Mathf.Max(1, unitsPerVisual));
            for (int i = 0; i < visuals.Count; i++)
            {
                if (visuals[i] == null) continue;
                bool visible = i < shown;
                if (visuals[i].activeSelf != visible) visuals[i].SetActive(visible);
            }
        }
    }
}
