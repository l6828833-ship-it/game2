using System.Collections.Generic;
using UnityEngine;

namespace MiniMart
{
    /// <summary>A single product shelf. Shows its stock as items on the boards plus a status light.</summary>
    public class ShelfUnit : MonoBehaviour
    {
        public ProductKind Product { get; private set; }
        public int Stock { get; private set; }
        public bool IsFull => Stock >= GameConfig.ShelfCapacity;
        public bool IsEmpty => Stock <= 0;

        private readonly List<GameObject> visuals = new List<GameObject>();
        private int unitsPerVisual = 1;

        public void Initialise(ProductKind kind, int stock)
        {
            Product = kind;
            Stock = Mathf.Clamp(stock, 0, GameConfig.ShelfCapacity);
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

        /// <summary>Shopper picks one item off the shelf.</summary>
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
            int placed = Mathf.Min(amount, GameConfig.ShelfCapacity - Stock);
            if (placed <= 0) return 0;
            Stock += placed;
            RebuildVisuals();
            return placed;
        }

        /// <summary>
        /// Every slot is created once and then just shown or hidden, rather than destroying and
        /// rebuilding the row on each sale.
        ///
        /// Products with a real mesh are stocked at half density: those models run to fifteen
        /// thousand triangles each, and fifteen of them per shelf across eight shelves is a lot of
        /// geometry for something a few pixels tall. One crate stands for two units instead.
        /// </summary>
        private void BuildProductPool()
        {
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
                    // A little turn each so a row of them does not look stamped out.
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

        private void RebuildVisuals()
        {
            int shown = Mathf.CeilToInt(Stock / (float)Mathf.Max(1, unitsPerVisual));
            for (int i = 0; i < visuals.Count; i++)
            {
                if (visuals[i] == null) continue;
                bool visible = i < shown;
                if (visuals[i].activeSelf != visible) visuals[i].SetActive(visible);
            }
        }
    }
}
