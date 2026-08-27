using UnityEngine;

namespace MiniMart
{
    /// <summary>A crop plot or egg nest. Press E when the produce is up to collect a crate.</summary>
    public class FarmProducer : MonoBehaviour
    {
        public ProductKind Product { get; private set; }
        public string Label { get; private set; }
        public bool IsReady { get; private set; } = true;
        public float RegrowRemaining => Mathf.Max(0f, regrowTimer);

        /// <summary>Played when fresh produce appears. The nest uses it for a cluck.</summary>
        public SfxKind? ReadySound { get; set; }

        private Transform harvestVisual;
        private Vector3 baseScale = Vector3.one;
        private float restHeight = 0.5f;
        private float hover = 0.04f;
        private float regrowTimer;
        private float regrowDuration = 1f;

        /// <summary>
        /// The produce mesh comes from the product table, falling back to a coloured primitive for
        /// products without one. <paramref name="restHeight"/> is where it sits, which is what puts
        /// the egg on the nest rim rather than floating above a marker.
        /// </summary>
        public void Initialise(ProductKind kind, string label, Color color,
            float modelHeight = 0f, float restHeight = 0.5f, bool showMarker = true)
        {
            Product = kind;
            Label = label;
            this.restHeight = restHeight;
            MiniMartGameManager game = MiniMartGameManager.Instance;
            Material output = game.MaterialFor("FarmOutput_" + kind, color);

            if (showMarker)
            {
                GameObject marker = game.CreateDecor(PrimitiveType.Cylinder, label + "_Marker", transform.position,
                    new Vector3(0.68f, 0.08f, 0.68f), game.MaterialFor("FarmMarker", new Color(0.92f, 0.75f, 0.32f)), transform);
                marker.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            }

            Transform produce = null;
            if (ProductVisuals.TryGet(kind, out ProductVisuals.Visual visual))
            {
                float height = modelHeight > 0f ? modelHeight : visual.CropHeight;
                produce = ModelKit.SpawnProp(transform, visual.Model, output, height, visual.DetailLod, visual.UpFix);
                if (produce != null)
                {
                    // SpawnProp already sized and grounded the mesh inside the pivot, so the pivot
                    // itself is what gets moved and scaled from here on.
                    baseScale = produce.localScale;
                    hover = Mathf.Min(0.04f, height * 0.2f);
                }
            }

            if (produce == null)
            {
                PrimitiveType shape = kind == ProductKind.Egg || kind == ProductKind.Apple ? PrimitiveType.Sphere : PrimitiveType.Cube;
                baseScale = kind == ProductKind.Egg ? new Vector3(0.38f, 0.50f, 0.38f) : new Vector3(0.46f, 0.46f, 0.46f);
                produce = game.CreateDecor(shape, label + "_Ready", transform.position, baseScale, output, transform).transform;
                produce.localScale = baseScale;
            }

            produce.name = label + "_Ready";
            produce.localPosition = new Vector3(0f, this.restHeight, 0f);
            ModelKit.Paint(produce.gameObject, output);
            ModelKit.StripColliders(produce.gameObject);
            harvestVisual = produce;
        }

        private void Update()
        {
            if (harvestVisual == null) return;

            if (IsReady)
            {
                // Gentle hover so ready produce reads at a glance from the isometric camera.
                harvestVisual.localPosition = new Vector3(0f, restHeight + Mathf.Sin(Time.time * 2.2f) * hover, 0f);
                return;
            }

            regrowTimer -= Time.deltaTime;
            float growth = Mathf.Clamp01(1f - regrowTimer / regrowDuration);
            harvestVisual.localScale = baseScale * Mathf.Lerp(0.08f, 1f, growth);
            if (regrowTimer > 0f) return;

            IsReady = true;
            harvestVisual.localScale = baseScale;
            if (ReadySound.HasValue) MiniMartGameManager.Instance.Sfx.Play(ReadySound.Value);
        }

        public bool TryHarvest()
        {
            if (!IsReady)
            {
                MiniMartGameManager.Instance.Sfx.Play(SfxKind.Deny);
                MiniMartGameManager.Instance.UI.SetNotification(Label + " need " + Mathf.CeilToInt(RegrowRemaining) + "s more to grow.", 1.6f);
                return false;
            }
            IsReady = false;
            regrowDuration = Product == ProductKind.Egg ? 8f : 6f;
            regrowTimer = regrowDuration;
            harvestVisual.localScale = baseScale * 0.08f;
            return true;
        }
    }
}
