using UnityEngine;

namespace MiniMart
{
    /// <summary>A crop plot or egg nest. Press E when the produce orb is up to collect a crate.</summary>
    public class FarmProducer : MonoBehaviour
    {
        public ProductKind Product { get; private set; }
        public string Label { get; private set; }
        public bool IsReady { get; private set; } = true;
        public float RegrowRemaining => Mathf.Max(0f, regrowTimer);

        private Transform harvestVisual;
        private Vector3 baseScale = Vector3.one;
        private float restHeight = 0.5f;
        private float regrowTimer;
        private float regrowDuration = 1f;

        public void Initialise(ProductKind kind, string label, Color color)
        {
            Product = kind;
            Label = label;
            MiniMartGameManager game = MiniMartGameManager.Instance;

            GameObject marker = game.CreateDecor(PrimitiveType.Cylinder, label + "_Marker", transform.position,
                new Vector3(0.68f, 0.08f, 0.68f), game.MaterialFor("FarmMarker", new Color(0.92f, 0.75f, 0.32f)), transform);
            marker.transform.localPosition = new Vector3(0f, 0.08f, 0f);

            GameObject produce = null;
            if (kind == ProductKind.Egg)
            {
                GameObject eggAsset = Resources.Load<GameObject>("Items/FarmEgg");
                if (eggAsset != null)
                {
                    produce = Instantiate(eggAsset, transform);
                    baseScale = Vector3.one * 0.26f;
                    restHeight = 0.16f;
                }
            }

            if (produce == null)
            {
                PrimitiveType shape = kind == ProductKind.Egg || kind == ProductKind.Apple ? PrimitiveType.Sphere : PrimitiveType.Cube;
                baseScale = kind == ProductKind.Egg ? new Vector3(0.38f, 0.50f, 0.38f) : new Vector3(0.46f, 0.46f, 0.46f);
                restHeight = 0.5f;
                produce = game.CreateDecor(shape, label + "_Ready", transform.position, baseScale,
                    game.MaterialFor("FarmOutput_" + kind, color), transform);
            }

            produce.name = label + "_Ready";
            produce.transform.localPosition = new Vector3(0f, restHeight, 0f);
            produce.transform.localScale = baseScale;
            Material output = game.MaterialFor("FarmOutput_" + kind, color);
            foreach (Renderer renderer in produce.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = output;
            foreach (Collider collider in produce.GetComponentsInChildren<Collider>(true)) Destroy(collider);
            harvestVisual = produce.transform;
        }

        private void Update()
        {
            if (IsReady)
            {
                // Gentle hover so ready plots read at a glance from the isometric camera.
                harvestVisual.localPosition = new Vector3(0f, restHeight + Mathf.Sin(Time.time * 2.2f) * 0.04f, 0f);
                return;
            }

            regrowTimer -= Time.deltaTime;
            float growth = Mathf.Clamp01(1f - regrowTimer / regrowDuration);
            harvestVisual.localScale = baseScale * Mathf.Lerp(0.08f, 1f, growth);
            if (regrowTimer > 0f) return;

            IsReady = true;
            harvestVisual.localScale = baseScale;
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
