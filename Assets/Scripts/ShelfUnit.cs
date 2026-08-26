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
        private Transform statusLight;
        private Renderer statusRenderer;

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

            GameObject light = game.CreateDecor(PrimitiveType.Sphere, "Shelf_Status", transform.position,
                new Vector3(0.3f, 0.3f, 0.3f), StatusMaterial(), transform);
            light.transform.localPosition = new Vector3(0f, 2.62f, -0.2f);
            statusLight = light.transform;
            statusRenderer = light.GetComponent<Renderer>();

            BuildProductPool();
            RebuildVisuals();
        }

        private void Update()
        {
            if (statusLight == null) return;
            float bob = IsEmpty ? Mathf.Sin(Time.time * 6f) * 0.07f : Mathf.Sin(Time.time * 1.8f) * 0.03f;
            statusLight.localPosition = new Vector3(0f, 2.62f + bob, -0.2f);
            if (statusRenderer != null) statusRenderer.sharedMaterial = StatusMaterial();
        }

        private Material StatusMaterial()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            if (IsEmpty) return game.MaterialFor("ShelfStatusEmpty", new Color(0.95f, 0.22f, 0.24f));
            if (Stock <= 4) return game.MaterialFor("ShelfStatusLow", new Color(1f, 0.78f, 0.18f));
            return game.MaterialFor("ShelfStatusGood", new Color(0.34f, 0.85f, 0.42f));
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
        /// Every slot is created once and then just shown or hidden. The old version destroyed and
        /// rebuilt fifteen objects on every single sale.
        /// </summary>
        private void BuildProductPool()
        {
            Transform displayRoot = new GameObject("Products").transform;
            displayRoot.SetParent(transform, false);
            displayRoot.localPosition = Vector3.zero;

            MiniMartGameManager game = MiniMartGameManager.Instance;
            Material material = game.MaterialFor("Product_" + Product, game.ProductColor(Product));
            PrimitiveType shape = Product == ProductKind.Apple || Product == ProductKind.Egg
                ? PrimitiveType.Sphere
                : PrimitiveType.Cube;

            for (int index = 0; index < GameConfig.ShelfCapacity; index++)
            {
                int row = index / 5;
                int col = index % 5;
                GameObject product = game.CreateDecor(shape, Product + "_Item", transform.position,
                    new Vector3(0.22f, 0.28f, 0.2f), material, displayRoot);
                product.transform.localPosition = new Vector3(-0.72f + col * 0.36f, 0.54f + row * 0.72f, -0.28f);
                product.SetActive(false);
                visuals.Add(product);
            }
        }

        private void RebuildVisuals()
        {
            for (int i = 0; i < visuals.Count; i++)
            {
                if (visuals[i] == null) continue;
                bool shown = i < Stock;
                if (visuals[i].activeSelf != shown) visuals[i].SetActive(shown);
            }
        }
    }
}
