using UnityEngine;

namespace MiniMart
{
    /// <summary>
    /// A buyable pad on the shop front. Prices climb with each level and the beacon only
    /// glows while you can actually afford the next one.
    /// </summary>
    public class UpgradeStation : MonoBehaviour
    {
        public UpgradeType Type { get; private set; }

        private Transform beacon;
        private Renderer beaconRenderer;
        private Color tint;

        public void Initialise(UpgradeType type, Color color)
        {
            Type = type;
            tint = color;
            MiniMartGameManager game = MiniMartGameManager.Instance;
            string key = "Upgrade_" + type;

            GameObject platform = game.CreatePrimitive(PrimitiveType.Cylinder, key + "_Platform", transform.position,
                new Vector3(0.74f, 0.13f, 0.74f), game.MaterialFor(key, color), transform);
            platform.transform.localPosition = new Vector3(0f, 0.13f, 0f);

            GameObject orb = game.CreateDecor(PrimitiveType.Sphere, key + "_Beacon", transform.position,
                new Vector3(0.38f, 0.38f, 0.38f), game.MaterialFor(key, color), transform);
            orb.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            beacon = orb.transform;
            beaconRenderer = orb.GetComponent<Renderer>();
        }

        private void Update()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            if (game == null || beacon == null) return;

            bool maxed = game.IsUpgradeMaxed(Type);
            bool affordable = !maxed && game.Money >= game.PriceFor(Type);
            float lift = affordable ? Mathf.Sin(Time.time * 3.2f) * 0.09f : 0f;
            beacon.localPosition = new Vector3(0f, 0.62f + lift, 0f);

            if (beaconRenderer == null) return;
            string key = maxed ? "UpgradeMaxed" : affordable ? "Upgrade_" + Type : "UpgradeDim_" + Type;
            Color color = maxed ? new Color(0.55f, 0.58f, 0.62f) : affordable ? tint : tint * 0.45f;
            beaconRenderer.sharedMaterial = game.MaterialFor(key, color);
        }

        /// <summary>Line shown in the HUD while the player stands on the pad.</summary>
        public string PromptText()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            string name = GameConfig.UpgradeName(Type);
            int level = game.UpgradeLevel(Type);
            if (game.IsUpgradeMaxed(Type)) return name + " is fully upgraded (Lv " + level + ")";
            return "[E]  " + name + " Lv " + level + " to " + (level + 1) + "   $" + game.PriceFor(Type);
        }

        public void TryPurchase()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            if (game.IsUpgradeMaxed(Type))
            {
                game.Sfx.Play(SfxKind.Deny);
                game.UI.SetNotification(GameConfig.UpgradeName(Type) + " is already maxed out.", 1.8f);
                return;
            }
            if (!game.TrySpend(game.PriceFor(Type))) return;
            game.ApplyUpgrade(Type);
        }
    }
}
