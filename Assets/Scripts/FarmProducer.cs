using UnityEngine;

namespace MiniMart
{
    /// <summary>A supplied farm plot that begins with four real crop items and replenishes after it is emptied.</summary>
    public class FarmProducer : MonoBehaviour
    {
        private const int PlotCapacity = GameConfig.CarryCapacity;

        public ProductKind Product { get; private set; }
        public string Label { get; private set; }
        public bool IsReady => availableCount > 0;
        public int AvailableCount => availableCount;
        public float RegrowRemaining => Mathf.Max(0f, regrowTimer);
        public SfxKind? ReadySound { get; set; }

        private readonly System.Collections.Generic.List<Transform> harvestVisuals = new System.Collections.Generic.List<Transform>();
        private int availableCount;
        private int sourceCapacity;
        private bool centreItems;
        private float restHeight;
        private float regrowTimer;
        private float regrowDuration = 6f;

        public void Initialise(ProductKind kind, string label, Color color,
            float modelHeight = 0f, float restHeight = 0.5f, bool showMarker = false,
            int itemCount = PlotCapacity, bool centreItems = false)
        {
            Product = kind;
            Label = label;
            this.restHeight = restHeight;
            sourceCapacity = Mathf.Clamp(itemCount, 1, PlotCapacity);
            this.centreItems = centreItems;
            availableCount = sourceCapacity;
            MiniMartGameManager game = MiniMartGameManager.Instance;
            Material output = game.MaterialFor("FarmOutput_" + kind, color);

            if (showMarker)
            {
                GameObject marker = game.CreateDecor(PrimitiveType.Cylinder, label + "_Marker", transform.position,
                    new Vector3(0.68f, 0.08f, 0.68f), game.MaterialFor("FarmMarker", new Color(0.92f, 0.75f, 0.32f)), transform);
                marker.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            }

            for (int index = 0; index < sourceCapacity; index++)
            {
                Transform produce = SpawnHarvestItem(game, output, modelHeight);
                int row = index / 2;
                int column = index % 2;
                produce.name = label + "_Harvest_" + (index + 1);
                produce.localPosition = centreItems
                    ? new Vector3(0f, this.restHeight, 0f)
                    : new Vector3(-0.50f + column * 1.00f, this.restHeight + row * 0.08f, -0.30f + row * 0.60f);
                produce.localRotation = Quaternion.Euler(0f, index * 48f, 0f);
                harvestVisuals.Add(produce);
            }
        }

        private Transform SpawnHarvestItem(MiniMartGameManager game, Material output, float modelHeight)
        {
            if (ProductVisuals.TryGet(Product, out ProductVisuals.Visual visual))
            {
                float height = modelHeight > 0f ? modelHeight : visual.CropHeight * 2.0f;
                // Tomatoes need a smaller farm presence than their shelf and carry models so all
                // four fit neatly on the supplied soil patch without dominating the player.
                if (Product == ProductKind.Tomato) height *= 0.55f;
                Transform item = ModelKit.SpawnProp(transform, visual.Model, output, height, visual.DetailLod, visual.UpFix);
                if (item != null)
                {
                    ModelKit.Paint(item.gameObject, output);
                    ModelKit.StripColliders(item.gameObject);
                    return item;
                }
            }

            PrimitiveType shape = Product == ProductKind.Banana ? PrimitiveType.Capsule : PrimitiveType.Sphere;
            GameObject fallback = game.CreateDecor(shape, Label + "_Harvest", transform.position,
                Product == ProductKind.Banana ? new Vector3(0.22f, 0.42f, 0.22f) : Vector3.one * 0.32f, output, transform);
            return fallback.transform;
        }

        private void Update()
        {
            if (availableCount > 0) return;
            regrowTimer -= Time.deltaTime;
            if (regrowTimer > 0f) return;

            availableCount = sourceCapacity;
            foreach (Transform item in harvestVisuals) if (item != null) item.gameObject.SetActive(true);
            if (ReadySound.HasValue) MiniMartGameManager.Instance.Sfx.Play(ReadySound.Value);
            MiniMartGameManager.Instance.UI.SetNotification(Label + " produced " + sourceCapacity + " fresh " + (sourceCapacity == 1 ? "item!" : "items!"), 1.4f);
        }

        public bool TryHarvest()
        {
            if (availableCount <= 0)
            {
                MiniMartGameManager.Instance.Sfx.Play(SfxKind.Deny);
                MiniMartGameManager.Instance.UI.SetNotification(Label + " need " + Mathf.CeilToInt(RegrowRemaining) + "s more to grow.", 1.6f);
                return false;
            }

            availableCount--;
            harvestVisuals[availableCount].gameObject.SetActive(false);
            if (availableCount == 0)
            {
                regrowDuration = 7f;
                regrowTimer = regrowDuration;
            }
            return true;
        }
    }
}
