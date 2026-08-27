using UnityEngine;

namespace MiniMart
{
    /// <summary>Everything the store can sell. Farm plots produce a subset of these.</summary>
    public enum ProductKind { Milk, Bread, Apple, Juice, Cereal, Chips, Water, Cookies, Egg, Tomato, Watermelon, Banana }

    public enum UpgradeType { ExtraShelf, Customers, Premium, Crate }

    /// <summary>Open = shoppers arrive. Closing = doors shut and the day gets totalled up.</summary>
    public enum DayPhase { Open, Closing }

    /// <summary>Tunable numbers in one place so the world builder, HUD and agents stay in sync.</summary>
    public static class GameConfig
    {
        public const string SaveKey = "TinyTownMiniMart_Save_v2";
        public const string LegacySaveKey = "TinyTownMiniMart_Save";

        public const int StartingMoney = 100;
        public const int ShelfCapacity = 15;
        public const int MaxUpgradeLevel = 8;

        public const float DayLength = 165f;
        public const float ClosingLength = 11f;
        public const float OpeningHour = 8f;
        public const float ClosingHour = 20f;

        public const float PlayerWalkSpeed = 4.6f;
        public const float PlayerSprintMultiplier = 1.55f;
        public const float InteractRange = 2.3f;

        /// <summary>Rent is charged when the store closes, so day one is a gentle warm up.</summary>
        public static int RentForDay(int day) => 25 + Mathf.Max(0, day - 1) * 15;

        /// <summary>How many units one trip from the farm puts on a shelf.</summary>
        public static int CrateSize(int crateLevel) => 5 + crateLevel * 2;

        public static int UpgradePrice(UpgradeType type, int level)
        {
            switch (type)
            {
                case UpgradeType.ExtraShelf: return 60 + level * 55;
                case UpgradeType.Customers: return 90 + level * 70;
                case UpgradeType.Premium: return 120 + level * 90;
                default: return 75 + level * 45;
            }
        }

        public static string UpgradeName(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.ExtraShelf: return "Extra shelf";
                case UpgradeType.Customers: return "Busier town";
                case UpgradeType.Premium: return "Premium prices";
                default: return "Bigger crates";
            }
        }

        public static string ProductLabel(ProductKind kind)
        {
            switch (kind)
            {
                case ProductKind.Apple: return "Apples";
                case ProductKind.Egg: return "Eggs";
                case ProductKind.Cookies: return "Cookies";
                case ProductKind.Chips: return "Chips";
                case ProductKind.Tomato: return "Tomatoes";
                case ProductKind.Banana: return "Bananas";
                case ProductKind.Watermelon: return "Watermelons";
                default: return kind.ToString();
            }
        }

        /// <summary>Base shelf price per product before the premium upgrade bonus.</summary>
        public static int BasePrice(ProductKind kind)
        {
            switch (kind)
            {
                case ProductKind.Water: return 2;
                case ProductKind.Apple: return 3;
                case ProductKind.Bread: return 4;
                case ProductKind.Chips: return 4;
                case ProductKind.Milk: return 5;
                case ProductKind.Cookies: return 5;
                case ProductKind.Juice: return 6;
                case ProductKind.Egg: return 6;
                case ProductKind.Tomato: return 4;
                case ProductKind.Banana: return 5;
                case ProductKind.Watermelon: return 9;
                default: return 7;
            }
        }
    }

    /// <summary>Serialised through JsonUtility into PlayerPrefs.</summary>
    [System.Serializable]
    public class StoreSave
    {
        public int version = 2;
        public int money = GameConfig.StartingMoney;
        public int extraShelves;
        public int customerUpgrade;
        public int premiumUpgrade;
        public int crateUpgrade;
        public int day = 1;
        public float reputation = 80f;
        public int lifetimeEarnings;
        public int lifetimeCustomers;
    }

    /// <summary>
    /// Boots the whole prototype without needing anything wired up in the scene, so the
    /// SampleScene can stay empty and the game still runs on Play.
    /// </summary>
    public static class MiniMartPrototype
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreatePrototype()
        {
            if (!Application.isPlaying || GameObject.Find("MiniMart_GameManager") != null) return;
            new GameObject("MiniMart_GameManager").AddComponent<MiniMartGameManager>();
        }
    }
}
