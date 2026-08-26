using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MiniMart
{
    public enum ProductKind { Milk, Bread, Apple, Juice, Cereal, Chips, Water, Cookies, Egg }

    [Serializable]
    public class StoreSave
    {
        public int money = 100;
        public int extraShelves;
        public int customerUpgrade;
        public int premiumUpgrade;
    }

    public static class MiniMartPrototype
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreatePrototype()
        {
            if (!Application.isPlaying || GameObject.Find("MiniMart_GameManager") != null) return;
            new GameObject("MiniMart_GameManager").AddComponent<MiniMartGameManager>();
        }
    }

    public class MiniMartGameManager : MonoBehaviour
    {
        public static MiniMartGameManager Instance { get; private set; }
        public int Money { get; private set; }
        public PlayerShopper Player { get; private set; }
        public readonly List<ShelfUnit> Shelves = new List<ShelfUnit>();
        public readonly List<FarmProducer> FarmProducers = new List<FarmProducer>();
        public readonly List<CustomerAgent> Customers = new List<CustomerAgent>();
        public CheckoutStation Checkout { get; private set; }
        public MiniMartUI UI { get; private set; }
        public Transform CustomerSpawn { get; private set; }
        public Transform CustomerExit { get; private set; }

        private readonly Dictionary<ProductKind, Color> productColors = new Dictionary<ProductKind, Color>();
        private readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
        private StoreSave save;
        private float spawnTimer;
        private int customerSerial;
        private bool gamePaused;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitialisePalette();
            LoadSave();
            BuildWorld();
            UI = MiniMartUI.Create(this);
            UI.SetNotification("Welcome to Tiny Town Mini Mart! Stock shelves, serve shoppers, grow your store.", 5f);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                gamePaused = !gamePaused;
                Time.timeScale = gamePaused ? 0f : 1f;
                UI.SetNotification(gamePaused ? "Game paused" : "Back to work!", 1.5f);
            }

            if (gamePaused) return;
            spawnTimer -= Time.deltaTime;
            int maximumCustomers = 4 + save.customerUpgrade * 2;
            if (spawnTimer <= 0f && Customers.Count < maximumCustomers)
            {
                SpawnCustomer();
                float busyness = Mathf.Clamp01((Customers.Count + save.customerUpgrade) / 10f);
                spawnTimer = UnityEngine.Random.Range(Mathf.Lerp(5.5f, 2.6f, busyness), Mathf.Lerp(9f, 4.2f, busyness));
            }
            UI.Refresh();
        }

        private void InitialisePalette()
        {
            productColors[ProductKind.Milk] = new Color(0.92f, 0.97f, 1f);
            productColors[ProductKind.Bread] = new Color(0.96f, 0.65f, 0.28f);
            productColors[ProductKind.Apple] = new Color(0.95f, 0.23f, 0.26f);
            productColors[ProductKind.Juice] = new Color(1f, 0.56f, 0.18f);
            productColors[ProductKind.Cereal] = new Color(0.62f, 0.30f, 0.9f);
            productColors[ProductKind.Chips] = new Color(1f, 0.82f, 0.16f);
            productColors[ProductKind.Water] = new Color(0.22f, 0.72f, 1f);
            productColors[ProductKind.Cookies] = new Color(0.53f, 0.28f, 0.12f);
            productColors[ProductKind.Egg] = new Color(1f, 0.96f, 0.74f);
        }

        private void BuildWorld()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.66f, 0.75f, 0.79f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.72f, 0.88f, 0.94f);
            RenderSettings.fogDensity = 0.008f;

            CreatePrimitive(PrimitiveType.Plane, "Pastel Grass", Vector3.zero, new Vector3(5.5f, 1f, 5.5f), MaterialFor("Grass", new Color(0.47f, 0.82f, 0.38f)));
            CreatePrimitive(PrimitiveType.Cube, "Market Floor", new Vector3(3f, 0.04f, 1f), new Vector3(26f, 0.12f, 14f), MaterialFor("Floor", new Color(0.96f, 0.82f, 0.57f)));
            BuildFarm();
            BuildStoreShell();
            BuildProps();
            BuildShelves();
            BuildCheckout();
            BuildUpgrades();
            BuildPlayer();
            BuildCamera();
            BuildLighting();

            CustomerSpawn = new GameObject("Customer_Entrance").transform;
            CustomerSpawn.position = new Vector3(-10.5f, 0f, -2.1f);
            CustomerExit = new GameObject("Customer_Exit").transform;
            CustomerExit.position = new Vector3(-13.5f, 0f, -2.1f);
            for (int i = 0; i < 2; i++) SpawnCustomer();
            spawnTimer = UnityEngine.Random.Range(4.5f, 7f);
        }

        private void BuildFarm()
        {
            Material soil = MaterialFor("FarmSoil", new Color(0.48f, 0.25f, 0.12f));
            Material fence = MaterialFor("FarmFence", new Color(0.68f, 0.42f, 0.18f));
            CreatePrimitive(PrimitiveType.Cube, "Farm_Path", new Vector3(-12f, 0.06f, -2.2f), new Vector3(8.5f, 0.08f, 2.2f), MaterialFor("FarmPath", new Color(0.91f, 0.72f, 0.40f)));
            CreatePrimitive(PrimitiveType.Cube, "Farm_Zone", new Vector3(-16f, 0.05f, 2.1f), new Vector3(8.5f, 0.1f, 9f), MaterialFor("FarmGrass", new Color(0.58f, 0.88f, 0.34f)));
            BuildCropBed(new Vector3(-18f, 0f, 3.6f), "Tomato_Plot", soil, ProductKind.Apple, new Color(0.93f, 0.22f, 0.25f));
            BuildCropBed(new Vector3(-14.2f, 0f, 3.6f), "Carrot_Plot", soil, ProductKind.Juice, new Color(1f, 0.48f, 0.12f));
            BuildCropBed(new Vector3(-18f, 0f, 0.25f), "Corn_Plot", soil, ProductKind.Cereal, new Color(1f, 0.78f, 0.15f));
            BuildChickenCoop(new Vector3(-14.2f, 0f, 0.25f));
            BuildFenceLine(new Vector3(-20.3f, 0f, 2.1f), new Vector3(0f, 0f, 8.8f), fence, 6);
            BuildFenceLine(new Vector3(-16f, 0f, 6.5f), new Vector3(8.6f, 0f, 0f), fence, 6);
            CreatePrimitive(PrimitiveType.Cylinder, "Farm_Well", new Vector3(-11.2f, 0.48f, 3.4f), new Vector3(0.78f, 0.48f, 0.78f), MaterialFor("Well", new Color(0.48f, 0.62f, 0.66f)));
            CreatePrimitive(PrimitiveType.Sphere, "Farm_Water", new Vector3(-11.2f, 0.91f, 3.4f), new Vector3(0.62f, 0.12f, 0.62f), MaterialFor("Water", new Color(0.19f, 0.70f, 0.94f)));
        }

        private void BuildCropBed(Vector3 position, string label, Material soil, ProductKind product, Color fruitColor)
        {
            CreatePrimitive(PrimitiveType.Cube, label + "_Soil", position + new Vector3(0f, 0.12f, 0f), new Vector3(3f, 0.24f, 1.65f), soil);
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < 4; col++)
            {
                Vector3 p = position + new Vector3(-1.05f + col * 0.7f, 0.33f, -0.38f + row * 0.76f);
                CreatePrimitive(PrimitiveType.Cylinder, label + "_Stem", p, new Vector3(0.09f, 0.36f, 0.09f), MaterialFor("CropLeaf", new Color(0.19f, 0.58f, 0.18f)));
                CreatePrimitive(PrimitiveType.Sphere, label + "_Produce", p + new Vector3(0.16f, 0.28f, 0f), new Vector3(0.24f, 0.24f, 0.24f), MaterialFor(label + "_Fruit", fruitColor));
            }
            CreateFarmProducer(position + new Vector3(0f, 0f, -1.35f), product, label + " Harvest", fruitColor);
        }

        private void BuildChickenCoop(Vector3 position)
        {
            CreatePrimitive(PrimitiveType.Cube, "Chicken_Coop", position + new Vector3(0f, 0.55f, 0f), new Vector3(3f, 1.1f, 1.65f), MaterialFor("Coop", new Color(0.91f, 0.42f, 0.23f)));
            CreatePrimitive(PrimitiveType.Cylinder, "Chicken_Coop_Roof", position + new Vector3(0f, 1.25f, 0f), new Vector3(1.9f, 0.22f, 1.2f), MaterialFor("CoopRoof", new Color(0.31f, 0.58f, 0.84f)));
            BuildToyChicken(position + new Vector3(-1.4f, 0f, -1.15f), "Chicken_A");
            BuildToyChicken(position + new Vector3(-0.5f, 0f, -1.35f), "Chicken_B");
            BuildToyChicken(position + new Vector3(0.45f, 0f, -1.15f), "Chicken_C");
            CreateFarmProducer(position + new Vector3(1.55f, 0f, -1.15f), ProductKind.Egg, "Egg Nest", new Color(1f, 0.96f, 0.74f));
        }

        private void BuildToyChicken(Vector3 position, string label)
        {
            CreatePrimitive(PrimitiveType.Sphere, label + "_Body", position + new Vector3(0f, 0.36f, 0f), new Vector3(0.48f, 0.42f, 0.55f), MaterialFor("ChickenWhite", new Color(0.97f, 0.96f, 0.88f)));
            CreatePrimitive(PrimitiveType.Sphere, label + "_Head", position + new Vector3(0.28f, 0.64f, 0.1f), new Vector3(0.25f, 0.25f, 0.25f), MaterialFor("ChickenWhite", new Color(0.97f, 0.96f, 0.88f)));
            CreatePrimitive(PrimitiveType.Sphere, label + "_Beak", position + new Vector3(0.5f, 0.61f, 0.1f), new Vector3(0.16f, 0.10f, 0.10f), MaterialFor("ChickenBeak", new Color(1f, 0.63f, 0.12f)));
        }

        private void BuildFenceLine(Vector3 center, Vector3 dimensions, Material material, int segments)
        {
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments - 0.5f;
                Vector3 p = center + new Vector3(dimensions.x * t, 0.52f, dimensions.z * t);
                CreatePrimitive(PrimitiveType.Cylinder, "FencePost", p, new Vector3(0.10f, 0.52f, 0.10f), material);
            }
            CreatePrimitive(PrimitiveType.Cube, "FenceRail", center + new Vector3(0f, 0.45f, 0f), new Vector3(Mathf.Max(0.12f, dimensions.x), 0.12f, Mathf.Max(0.12f, dimensions.z)), material);
        }

        private void BuildStoreShell()
        {
            Material wall = MaterialFor("Wall", new Color(0.98f, 0.69f, 0.42f));
            Material roof = MaterialFor("Roof", new Color(0.26f, 0.60f, 0.91f));
            CreatePrimitive(PrimitiveType.Cube, "Back Wall", new Vector3(3f, 2.45f, 7.85f), new Vector3(26f, 4.8f, 0.28f), wall);
            CreatePrimitive(PrimitiveType.Cube, "Left Wall", new Vector3(-9.85f, 2.45f, 3.5f), new Vector3(0.28f, 4.8f, 8.8f), wall);
            CreatePrimitive(PrimitiveType.Cube, "Right Wall", new Vector3(15.85f, 2.45f, 3.5f), new Vector3(0.28f, 4.8f, 8.8f), wall);
            CreatePrimitive(PrimitiveType.Cube, "Roof Trim", new Vector3(3f, 4.95f, 7.65f), new Vector3(26.5f, 0.35f, 0.65f), roof);
            CreatePrimitive(PrimitiveType.Cube, "Market Sign", new Vector3(3f, 3.4f, -5.7f), new Vector3(5.4f, 0.8f, 0.2f), MaterialFor("Sign", new Color(1f, 0.88f, 0.2f)));
            CreatePrimitive(PrimitiveType.Cylinder, "Plant Pot L", new Vector3(-7.8f, 0.35f, -4.5f), new Vector3(0.45f, 0.35f, 0.45f), MaterialFor("Pot", new Color(0.89f, 0.43f, 0.34f)));
            CreatePrimitive(PrimitiveType.Sphere, "Plant L", new Vector3(-7.8f, 0.95f, -4.5f), new Vector3(0.75f, 1f, 0.75f), MaterialFor("Plant", new Color(0.27f, 0.65f, 0.34f)));
            CreatePrimitive(PrimitiveType.Cylinder, "Plant Pot R", new Vector3(13.8f, 0.35f, -4.5f), new Vector3(0.45f, 0.35f, 0.45f), MaterialFor("Pot", new Color(0.89f, 0.43f, 0.34f)));
            CreatePrimitive(PrimitiveType.Sphere, "Plant R", new Vector3(13.8f, 0.95f, -4.5f), new Vector3(0.75f, 1f, 0.75f), MaterialFor("Plant", new Color(0.27f, 0.65f, 0.34f)));
        }

        private void BuildProps()
        {
            Material fridge = MaterialFor("Fridge", new Color(0.54f, 0.86f, 0.95f));
            CreatePrimitive(PrimitiveType.Cube, "Cooler_A", new Vector3(12.8f, 1.6f, 5.7f), new Vector3(1.6f, 3.1f, 1.15f), fridge);
            CreatePrimitive(PrimitiveType.Cube, "Cooler_B", new Vector3(10.7f, 1.6f, 5.7f), new Vector3(1.6f, 3.1f, 1.15f), fridge);
            CreatePrimitive(PrimitiveType.Cube, "Cooler_Window_A", new Vector3(12.8f, 1.75f, 5.09f), new Vector3(1.2f, 2.35f, 0.04f), MaterialFor("Glass", new Color(0.7f, 0.94f, 1f)));
            CreatePrimitive(PrimitiveType.Cube, "Cooler_Window_B", new Vector3(10.7f, 1.75f, 5.09f), new Vector3(1.2f, 2.35f, 0.04f), MaterialFor("Glass", new Color(0.7f, 0.94f, 1f)));
            CreatePrimitive(PrimitiveType.Cube, "Welcome Mat", new Vector3(-7.4f, 0.12f, -4.65f), new Vector3(3.1f, 0.05f, 1.25f), MaterialFor("Mat", new Color(0.93f, 0.38f, 0.48f)));
            CreatePrimitive(PrimitiveType.Cube, "Produce_Display", new Vector3(7.7f, 0.65f, 4.9f), new Vector3(2.6f, 1.1f, 1.45f), MaterialFor("Display", new Color(0.62f, 0.36f, 0.14f)));
            for (int i = 0; i < 6; i++)
                CreatePrimitive(PrimitiveType.Sphere, "Produce_Basket", new Vector3(6.9f + (i % 3) * 0.75f, 1.25f, 4.55f + (i / 3) * 0.55f), new Vector3(0.34f, 0.34f, 0.34f), MaterialFor("Produce_" + i, i % 2 == 0 ? new Color(0.94f, 0.21f, 0.23f) : new Color(1f, 0.65f, 0.15f)));
        }

        private void BuildShelves()
        {
            Vector3[] positions =
            {
                new Vector3(-5.5f, 0f, 4.6f), new Vector3(-2.5f, 0f, 4.6f), new Vector3(0.5f, 0f, 4.6f), new Vector3(3.5f, 0f, 4.6f),
                new Vector3(-5.5f, 0f, 1.7f), new Vector3(-2.5f, 0f, 1.7f), new Vector3(0.5f, 0f, 1.7f), new Vector3(3.5f, 0f, 1.7f)
            };
            ProductKind[] products = { ProductKind.Apple, ProductKind.Juice, ProductKind.Cereal, ProductKind.Egg, ProductKind.Milk, ProductKind.Bread, ProductKind.Chips, ProductKind.Water };
            for (int i = 0; i < positions.Length; i++) CreateShelf(positions[i], products[i], true);
            for (int i = 0; i < save.extraShelves; i++)
                CreateShelf(new Vector3(6.4f + (i % 2) * 2.8f, 0f, 1.7f - (i / 2) * 2.9f), ProductKind.Chips, true);
        }

        private void CreateShelf(Vector3 position, ProductKind product, bool stocked)
        {
            GameObject root = new GameObject("Shelf_" + product);
            root.transform.position = position;
            ShelfUnit shelf = root.AddComponent<ShelfUnit>();
            shelf.Initialise(product, stocked ? 12 : 0);
            Shelves.Add(shelf);
        }

        private void CreateFarmProducer(Vector3 position, ProductKind product, string label, Color color)
        {
            GameObject root = new GameObject("FarmHarvest_" + product);
            root.transform.position = position;
            FarmProducer producer = root.AddComponent<FarmProducer>();
            producer.Initialise(product, label, color);
            FarmProducers.Add(producer);
        }

        private void BuildCheckout()
        {
            GameObject root = new GameObject("Checkout");
            root.transform.position = new Vector3(11.4f, 0f, -2.9f);
            Checkout = root.AddComponent<CheckoutStation>();
            Checkout.Initialise();
        }

        private void BuildUpgrades()
        {
            CreateUpgrade("Upgrade_Shelf", new Vector3(4.0f, 0f, -4.55f), "SHELF +", 60, UpgradeType.ExtraShelf, new Color(0.37f, 0.84f, 0.89f));
            CreateUpgrade("Upgrade_Customers", new Vector3(6.5f, 0f, -4.55f), "BUSY +", 90, UpgradeType.Customers, new Color(0.96f, 0.57f, 0.82f));
            CreateUpgrade("Upgrade_Premium", new Vector3(9.0f, 0f, -4.55f), "SALE +", 120, UpgradeType.Premium, new Color(1f, 0.78f, 0.2f));
        }

        private void CreateUpgrade(string name, Vector3 position, string label, int price, UpgradeType kind, Color color)
        {
            GameObject root = new GameObject(name);
            root.transform.position = position;
            UpgradeStation upgrade = root.AddComponent<UpgradeStation>();
            upgrade.Initialise(label, price, kind, color);
        }

        private void BuildPlayer()
        {
            GameObject root = new GameObject("Tiny_Mart_Manager");
            root.transform.position = new Vector3(-11.1f, 0f, -3.2f);
            Player = root.AddComponent<PlayerShopper>();
            Player.Initialise();
        }

        private void BuildCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cam = new GameObject("Main Camera");
                cam.tag = "MainCamera";
                camera = cam.AddComponent<Camera>();
                cam.AddComponent<AudioListener>();
            }
            camera.orthographic = true;
            camera.orthographicSize = 14.5f;
            camera.backgroundColor = new Color(0.65f, 0.88f, 0.98f);
            camera.transform.position = new Vector3(-18f, 20f, -19f);
            camera.transform.rotation = Quaternion.Euler(55f, 45f, 0f);
            CameraFollower follow = camera.GetComponent<CameraFollower>() ?? camera.gameObject.AddComponent<CameraFollower>();
            follow.target = Player.transform;
        }

        private void BuildLighting()
        {
            Light sun = FindAnyObjectByType<Light>();
            if (sun == null)
            {
                GameObject lightGo = new GameObject("Sun");
                sun = lightGo.AddComponent<Light>();
            }
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.94f, 0.79f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        public Material MaterialFor(string name, Color color)
        {
            if (materialCache.TryGetValue(name, out Material material)) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader) { name = "M_" + name, color = color };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
            materialCache[name] = material;
            return material;
        }

        public Color ProductColor(ProductKind kind) => productColors[kind];

        public GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            if (parent != null) go.transform.SetParent(parent, true);
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return go;
        }

        public void AddMoney(int amount)
        {
            Money += amount;
            Save();
            UI.SetNotification("+$" + amount + "  Sale complete!", 1.5f);
        }

        public bool TrySpend(int amount)
        {
            if (Money < amount) { UI.SetNotification("Need $" + amount + " to unlock this.", 1.8f); return false; }
            Money -= amount;
            Save();
            return true;
        }

        public void ApplyUpgrade(UpgradeType type)
        {
            if (type == UpgradeType.ExtraShelf)
            {
                save.extraShelves++;
                CreateShelf(new Vector3(6.4f + ((save.extraShelves - 1) % 2) * 2.8f, 0f, 1.7f - ((save.extraShelves - 1) / 2) * 2.9f), ProductKind.Chips, true);
                UI.SetNotification("New shelf unlocked!", 2f);
            }
            else if (type == UpgradeType.Customers)
            {
                save.customerUpgrade++;
                UI.SetNotification("Your store is getting busier!", 2f);
            }
            else
            {
                save.premiumUpgrade++;
                UI.SetNotification("Premium sale bonus unlocked!", 2f);
            }
            Save();
        }

        public int GetSaleValue(ProductKind kind)
        {
            int baseValue = kind == ProductKind.Juice || kind == ProductKind.Cereal ? 6 : 4;
            return baseValue + save.premiumUpgrade * 2;
        }

        public ShelfUnit FindShelf(ProductKind kind)
        {
            ShelfUnit best = null;
            foreach (ShelfUnit shelf in Shelves)
            {
                if (shelf.Product == kind && shelf.Stock > 0) return shelf;
                if (best == null && shelf.Stock > 0) best = shelf;
            }
            return best;
        }

        public void SpawnCustomer()
        {
            GameObject customer = new GameObject("Customer_" + (++customerSerial));
            customer.transform.position = CustomerSpawn.position + new Vector3(UnityEngine.Random.Range(-0.45f, 0.25f), 0f, UnityEngine.Random.Range(-0.8f, 0.8f));
            CustomerAgent agent = customer.AddComponent<CustomerAgent>();
            agent.Initialise(customerSerial);
            Customers.Add(agent);
        }

        public void RemoveCustomer(CustomerAgent customer)
        {
            Customers.Remove(customer);
            Destroy(customer.gameObject);
        }

        public void Save()
        {
            save.money = Money;
            PlayerPrefs.SetString("TinyTownMiniMart_Save", JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }

        private void LoadSave()
        {
            if (PlayerPrefs.HasKey("TinyTownMiniMart_Save"))
            {
                save = JsonUtility.FromJson<StoreSave>(PlayerPrefs.GetString("TinyTownMiniMart_Save"));
                Money = Mathf.Max(0, save.money);
            }
            else
            {
                save = new StoreSave { money = 100 };
                Money = 100;
            }
        }
    }

    public class PlayerShopper : MonoBehaviour
    {
        private CharacterController controller;
        private Transform visual;
        private ProductKind? carrying;
        private Vector3 moveVelocity;
        private float walkPhase;
        public ProductKind? Carrying => carrying;

        public void Initialise()
        {
            controller = gameObject.AddComponent<CharacterController>();
            controller.radius = 0.34f;
            controller.height = 1.25f;
            controller.center = new Vector3(0f, 0.63f, 0f);
            visual = new GameObject("Yellow_Farm_Player").transform;
            visual.SetParent(transform, false);
            GameObject playerAsset = Resources.Load<GameObject>("Characters/FarmPlayer");
            if (playerAsset != null)
            {
                GameObject importedPlayer = Instantiate(playerAsset, visual);
                importedPlayer.name = "Farm_Player_Asset";
                importedPlayer.transform.localPosition = new Vector3(0f, 0f, 0f);
                importedPlayer.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                importedPlayer.transform.localScale = Vector3.one * 0.78f;
                Material yellow = MiniMartGameManager.Instance.MaterialFor("PlayerYellow", new Color(1f, 0.82f, 0.10f));
                foreach (Renderer renderer in importedPlayer.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = yellow;
            }
            else
            {
                BuildToyCharacter(visual, new Color(1f, 0.82f, 0.10f), new Color(1f, 0.82f, 0.10f), "Player");
            }
        }

        private void Update()
        {
            Vector2 input = Vector2.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed) input.x -= 1f;
                if (Keyboard.current.dKey.isPressed) input.x += 1f;
                if (Keyboard.current.sKey.isPressed) input.y -= 1f;
                if (Keyboard.current.wKey.isPressed) input.y += 1f;
                if (Keyboard.current.eKey.wasPressedThisFrame) Interact();
            }
            Vector3 inputDirection = new Vector3(input.x, 0f, input.y).normalized;
            Vector3 desiredVelocity = inputDirection * 4.6f;
            moveVelocity = Vector3.Lerp(moveVelocity, desiredVelocity, 1f - Mathf.Exp(-12f * Time.deltaTime));
            controller.Move(moveVelocity * Time.deltaTime + Vector3.down * 2f * Time.deltaTime);
            if (inputDirection.sqrMagnitude > 0.01f)
            {
                transform.forward = Vector3.Slerp(transform.forward, inputDirection, 12f * Time.deltaTime);
                walkPhase += Time.deltaTime * 8.5f;
                visual.localPosition = Vector3.Lerp(visual.localPosition, new Vector3(0f, 0.02f, 0f), Time.deltaTime * 9f);
                visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(0f, 0f, Mathf.Sin(walkPhase) * 2.2f), Time.deltaTime * 10f);
            }
            else
            {
                visual.localPosition = Vector3.Lerp(visual.localPosition, Vector3.zero, Time.deltaTime * 9f);
                visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.identity, Time.deltaTime * 9f);
            }
            UpdateCarryVisual();
        }

        private void Interact()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            FarmProducer closestHarvest = FindClosest(game.FarmProducers, 2.1f);
            if (carrying == null && closestHarvest != null)
            {
                if (closestHarvest.TryHarvest())
                {
                    carrying = closestHarvest.Product;
                    game.UI.SetNotification("Harvested " + carrying + ". Take it to the matching shelf!", 2f);
                }
                return;
            }

            ShelfUnit closestShelf = FindClosest(game.Shelves, 2.1f);
            if (carrying != null && closestShelf != null)
            {
                closestShelf.Restock(carrying.Value, 5);
                carrying = null;
                game.UI.SetNotification("Shelf stocked!", 1.3f);
                return;
            }

            UpgradeStation closestUpgrade = FindClosest(FindObjectsByType<UpgradeStation>(), 2.1f);
            if (closestUpgrade != null) { closestUpgrade.TryPurchase(); return; }
            if (Vector3.Distance(transform.position, game.Checkout.transform.position) < 2.2f)
            {
                game.UI.SetNotification("Checkout is handling the queue automatically.", 1.5f);
                return;
            }
            game.UI.SetNotification(carrying == null ? "Press E at a crop plot, egg nest, or upgrade station." : "Walk to the matching shelf and press E to stock it.", 1.6f);
        }

        private void UpdateCarryVisual()
        {
            Transform carry = transform.Find("Carry_Box");
            if (carrying == null)
            {
                if (carry != null) carry.gameObject.SetActive(false);
                return;
            }
            if (carry == null)
            {
                carry = MiniMartGameManager.Instance.CreatePrimitive(PrimitiveType.Cube, "Carry_Box", transform.position, Vector3.one, MiniMartGameManager.Instance.MaterialFor("Carry", MiniMartGameManager.Instance.ProductColor(carrying.Value)), transform).transform;
                carry.localPosition = new Vector3(0f, 0.75f, 0.48f);
                carry.localScale = new Vector3(0.42f, 0.36f, 0.34f);
            }
            carry.gameObject.SetActive(true);
            carry.GetComponent<Renderer>().sharedMaterial = MiniMartGameManager.Instance.MaterialFor("Carry_" + carrying.Value, MiniMartGameManager.Instance.ProductColor(carrying.Value));
        }

        private T FindClosest<T>(IEnumerable<T> candidates, float radius) where T : Component
        {
            T closest = null;
            float best = radius;
            foreach (T item in candidates)
            {
                if (item == null) continue;
                float distance = Vector3.Distance(transform.position, item.transform.position);
                if (distance < best) { best = distance; closest = item; }
            }
            return closest;
        }

        public static void BuildToyCharacter(Transform root, Color shirt, Color headColor, string label)
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            GameObject body = game.CreatePrimitive(PrimitiveType.Capsule, label + "_BlobBody", root.position, new Vector3(0.52f, 0.48f, 0.44f), game.MaterialFor(label + "_Body", shirt), root);
            body.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            GameObject head = game.CreatePrimitive(PrimitiveType.Sphere, label + "_BigHead", root.position, new Vector3(0.66f, 0.61f, 0.61f), game.MaterialFor(label + "_Head", headColor), root);
            head.transform.localPosition = new Vector3(0f, 0.92f, 0.02f);
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject foot = game.CreatePrimitive(PrimitiveType.Sphere, label + "_Foot", root.position, new Vector3(0.22f, 0.18f, 0.29f), game.MaterialFor(label + "_Feet", shirt * 0.72f), root);
                foot.transform.localPosition = new Vector3(i * 0.18f, 0.12f, 0.07f);
            }
            GameObject face = game.CreatePrimitive(PrimitiveType.Sphere, label + "_Face", root.position, new Vector3(0.25f, 0.14f, 0.05f), game.MaterialFor(label + "_Face", new Color(0.12f, 0.18f, 0.25f)), root);
            face.transform.localPosition = new Vector3(0f, 0.92f, 0.31f);
            if (label.StartsWith("Customer") && int.TryParse(label.Replace("Customer", string.Empty), out int customerIndex) && customerIndex % 2 == 0)
            {
                GameObject cap = game.CreatePrimitive(PrimitiveType.Cylinder, label + "_Cap", root.position, new Vector3(0.42f, 0.10f, 0.42f), game.MaterialFor(label + "_Cap", shirt * 0.8f), root);
                cap.transform.localPosition = new Vector3(0f, 1.26f, 0.02f);
            }
        }
    }

    public class ShelfUnit : MonoBehaviour
    {
        public ProductKind Product { get; private set; }
        public int Stock { get; private set; }
        private readonly List<GameObject> visuals = new List<GameObject>();
        private Transform displayRoot;

        public void Initialise(ProductKind kind, int stock)
        {
            Product = kind;
            Stock = stock;
            MiniMartGameManager game = MiniMartGameManager.Instance;
            GameObject back = game.CreatePrimitive(PrimitiveType.Cube, "Shelf_Back", transform.position + new Vector3(0f, 1.2f, 0.22f), new Vector3(2.15f, 2.35f, 0.14f), game.MaterialFor("ShelfWood", new Color(0.49f, 0.27f, 0.15f)), transform);
            back.transform.localPosition = new Vector3(0f, 1.2f, 0.22f);
            for (int row = 0; row < 3; row++)
            {
                GameObject board = game.CreatePrimitive(PrimitiveType.Cube, "Shelf_Board", transform.position, new Vector3(2.3f, 0.1f, 0.65f), game.MaterialFor("ShelfWood", new Color(0.49f, 0.27f, 0.15f)), transform);
                board.transform.localPosition = new Vector3(0f, 0.35f + row * 0.72f, 0f);
            }
            GameObject marker = game.CreatePrimitive(PrimitiveType.Cube, "Product_Label_" + kind, transform.position, new Vector3(1.45f, 0.2f, 0.07f), game.MaterialFor("Label_" + kind, game.ProductColor(kind)), transform);
            marker.transform.localPosition = new Vector3(0f, 2.15f, -0.37f);
            displayRoot = new GameObject("Products").transform;
            displayRoot.SetParent(transform);
            RebuildVisuals();
        }

        public bool TakeOne()
        {
            if (Stock <= 0) return false;
            Stock--;
            RebuildVisuals();
            return true;
        }

        public void Restock(ProductKind kind, int amount)
        {
            if (kind != Product)
            {
                MiniMartGameManager.Instance.UI.SetNotification("This shelf is for " + Product + ".", 1.5f);
                return;
            }
            Stock = Mathf.Min(15, Stock + amount);
            RebuildVisuals();
        }

        private void RebuildVisuals()
        {
            foreach (GameObject item in visuals) if (item != null) Destroy(item);
            visuals.Clear();
            int shown = Mathf.Min(15, Stock);
            MiniMartGameManager game = MiniMartGameManager.Instance;
            for (int index = 0; index < shown; index++)
            {
                int row = index / 5;
                int col = index % 5;
                PrimitiveType shape = Product == ProductKind.Apple ? PrimitiveType.Sphere : PrimitiveType.Cube;
                GameObject product = game.CreatePrimitive(shape, Product + "_Item", transform.position, new Vector3(0.22f, 0.28f, 0.2f), game.MaterialFor("Product_" + Product, game.ProductColor(Product)), displayRoot);
                product.transform.localPosition = new Vector3(-0.72f + col * 0.36f, 0.54f + row * 0.72f, -0.28f);
                visuals.Add(product);
            }
        }
    }

    public class FarmProducer : MonoBehaviour
    {
        public ProductKind Product { get; private set; }
        private string label;
        private GameObject harvestVisual;
        private float regrowTimer;
        private bool ready = true;

        public void Initialise(ProductKind kind, string producerLabel, Color color)
        {
            Product = kind;
            label = producerLabel;
            MiniMartGameManager game = MiniMartGameManager.Instance;
            GameObject baseRing = game.CreatePrimitive(PrimitiveType.Cylinder, producerLabel + "_Marker", transform.position + new Vector3(0f, 0.08f, 0f), new Vector3(0.68f, 0.08f, 0.68f), game.MaterialFor("FarmMarker_" + kind, new Color(0.92f, 0.75f, 0.32f)), transform);
            baseRing.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            PrimitiveType shape = kind == ProductKind.Egg || kind == ProductKind.Apple ? PrimitiveType.Sphere : PrimitiveType.Cube;
            harvestVisual = game.CreatePrimitive(shape, producerLabel + "_Ready", transform.position, kind == ProductKind.Egg ? new Vector3(0.38f, 0.50f, 0.38f) : new Vector3(0.46f, 0.46f, 0.46f), game.MaterialFor("FarmOutput_" + kind, color), transform);
            harvestVisual.transform.localPosition = new Vector3(0f, 0.50f, 0f);
        }

        public bool TryHarvest()
        {
            if (!ready)
            {
                MiniMartGameManager.Instance.UI.SetNotification(label + " is growing. Come back soon!", 1.4f);
                return false;
            }
            ready = false;
            regrowTimer = Product == ProductKind.Egg ? 7f : 5f;
            harvestVisual.SetActive(false);
            return true;
        }

        private void Update()
        {
            if (ready) return;
            regrowTimer -= Time.deltaTime;
            if (regrowTimer > 0f) return;
            ready = true;
            harvestVisual.SetActive(true);
            MiniMartGameManager.Instance.UI.SetNotification(label + " is ready!", 1.2f);
        }
    }

    public enum UpgradeType { ExtraShelf, Customers, Premium }

    public class UpgradeStation : MonoBehaviour
    {
        private string label;
        private int price;
        private UpgradeType type;
        public void Initialise(string stationLabel, int stationPrice, UpgradeType stationType, Color color)
        {
            label = stationLabel;
            price = stationPrice;
            type = stationType;
            MiniMartGameManager game = MiniMartGameManager.Instance;
            GameObject platform = game.CreatePrimitive(PrimitiveType.Cylinder, label + "_Platform", transform.position + new Vector3(0f, 0.13f, 0f), new Vector3(0.74f, 0.13f, 0.74f), game.MaterialFor(label, color), transform);
            platform.transform.localPosition = new Vector3(0f, 0.13f, 0f);
            GameObject beacon = game.CreatePrimitive(PrimitiveType.Sphere, label + "_Beacon", transform.position + new Vector3(0f, 0.62f, 0f), new Vector3(0.38f, 0.38f, 0.38f), game.MaterialFor(label, color), transform);
            beacon.transform.localPosition = new Vector3(0f, 0.62f, 0f);
        }
        public void TryPurchase()
        {
            if (MiniMartGameManager.Instance.TrySpend(price)) MiniMartGameManager.Instance.ApplyUpgrade(type);
        }
    }

    public class CheckoutStation : MonoBehaviour
    {
        private readonly List<CustomerAgent> queue = new List<CustomerAgent>();
        private float paymentTimer;
        public Vector3 CounterPosition => transform.position + new Vector3(0f, 0f, 0.2f);

        public void Initialise()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            GameObject counter = game.CreatePrimitive(PrimitiveType.Cube, "Checkout_Counter", transform.position + new Vector3(0f, 0.72f, 0f), new Vector3(2.35f, 1.35f, 0.85f), game.MaterialFor("Checkout", new Color(0.93f, 0.46f, 0.40f)), transform);
            counter.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            GameObject till = game.CreatePrimitive(PrimitiveType.Cube, "Cash_Register", transform.position, new Vector3(0.48f, 0.3f, 0.35f), game.MaterialFor("Till", new Color(0.36f, 0.36f, 0.49f)), transform);
            till.transform.localPosition = new Vector3(0.35f, 1.52f, -0.1f);
        }

        private void Update()
        {
            for (int i = 0; i < queue.Count; i++) if (queue[i] != null) queue[i].SetQueueTarget(QueueSpot(i));
            if (queue.Count == 0) return;
            CustomerAgent first = queue[0];
            if (first == null) { queue.RemoveAt(0); return; }
            if (Vector3.Distance(first.transform.position, CounterPosition) < 0.55f)
            {
                paymentTimer += Time.deltaTime;
                if (paymentTimer > 1.5f)
                {
                    paymentTimer = 0f;
                    queue.RemoveAt(0);
                    MiniMartGameManager.Instance.AddMoney(first.BasketValue);
                    first.FinishShopping();
                }
            }
        }

        public void JoinQueue(CustomerAgent customer)
        {
            if (!queue.Contains(customer)) queue.Add(customer);
        }
        public Vector3 QueueSpot(int index) => index == 0
            ? CounterPosition
            : transform.position + new Vector3(-1.35f - (index - 1) * 0.8f, 0f, -0.1f);
    }

    public enum CustomerState { Entering, Browsing, GoingToShelf, Queuing, Leaving }

    public class CustomerAgent : MonoBehaviour
    {
        private CustomerState state;
        private Vector3 target;
        private float stateTimer;
        private ShelfUnit targetShelf;
        private Transform visual;
        private bool hasItem;
        private float walkSpeed;
        private float walkPhase;
        private Vector3 movementVelocity;
        public int BasketValue { get; private set; }

        public void Initialise(int serial)
        {
            Color[] shirts = { new Color(0.96f, 0.43f, 0.57f), new Color(0.45f, 0.73f, 0.91f), new Color(0.62f, 0.76f, 0.37f), new Color(0.82f, 0.55f, 0.92f), new Color(1f, 0.72f, 0.18f), new Color(0.29f, 0.78f, 0.70f) };
            Color[] skins = { new Color(1f, 0.76f, 0.58f), new Color(0.63f, 0.40f, 0.28f), new Color(0.44f, 0.25f, 0.16f), new Color(0.88f, 0.61f, 0.43f), new Color(0.80f, 0.52f, 0.35f) };
            visual = new GameObject("CustomerToy").transform;
            visual.SetParent(transform, false);
            PlayerShopper.BuildToyCharacter(visual, shirts[serial % shirts.Length], skins[(serial * 3 + 1) % skins.Length], "Customer" + serial);
            walkSpeed = UnityEngine.Random.Range(1.45f, 2.05f);
            walkPhase = UnityEngine.Random.Range(0f, 6.28f);
            target = new Vector3(-7.6f, 0f, -3.1f + UnityEngine.Random.Range(-0.6f, 0.6f));
            stateTimer = UnityEngine.Random.Range(0.45f, 1.35f);
            state = CustomerState.Entering;
        }

        private void Update()
        {
            Vector3 delta = target - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.04f)
            {
                Vector3 desiredVelocity = delta.normalized * walkSpeed;
                movementVelocity = Vector3.Lerp(movementVelocity, desiredVelocity, 1f - Mathf.Exp(-7f * Time.deltaTime));
                transform.position += movementVelocity * Time.deltaTime;
                transform.forward = Vector3.Slerp(transform.forward, movementVelocity.normalized, 9f * Time.deltaTime);
                walkPhase += Time.deltaTime * 7f;
                visual.localPosition = Vector3.Lerp(visual.localPosition, new Vector3(0f, 0.018f, 0f), Time.deltaTime * 10f);
                visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(0f, 0f, Mathf.Sin(walkPhase) * 2f), Time.deltaTime * 9f);
                return;
            }
            movementVelocity = Vector3.Lerp(movementVelocity, Vector3.zero, Time.deltaTime * 8f);
            visual.localPosition = Vector3.Lerp(visual.localPosition, Vector3.zero, Time.deltaTime * 8f);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.identity, Time.deltaTime * 8f);
            stateTimer -= Time.deltaTime;
            if (state == CustomerState.Entering)
            {
                if (stateTimer > 0f) return;
                state = CustomerState.Browsing;
                stateTimer = UnityEngine.Random.Range(0.35f, 1.15f);
                return;
            }
            if (state == CustomerState.Browsing)
            {
                if (stateTimer > 0f) return;
                MiniMartGameManager game = MiniMartGameManager.Instance;
                for (int attempt = 0; attempt < game.Shelves.Count; attempt++)
                {
                    ShelfUnit candidate = game.Shelves[UnityEngine.Random.Range(0, game.Shelves.Count)];
                    if (candidate.Stock > 0) { targetShelf = candidate; break; }
                }
                if (targetShelf == null) { BeginLeaving(); return; }
                target = targetShelf.transform.position + new Vector3(UnityEngine.Random.Range(-0.38f, 0.38f), 0f, -1.05f);
                state = CustomerState.GoingToShelf;
                return;
            }
            if (state == CustomerState.GoingToShelf)
            {
                if (targetShelf != null && targetShelf.TakeOne())
                {
                    hasItem = true;
                    BasketValue = MiniMartGameManager.Instance.GetSaleValue(targetShelf.Product);
                }
                MiniMartGameManager.Instance.Checkout.JoinQueue(this);
                state = CustomerState.Queuing;
                return;
            }
            if (state == CustomerState.Leaving && stateTimer <= 0f) MiniMartGameManager.Instance.RemoveCustomer(this);
        }

        public void SetQueueTarget(Vector3 queueTarget)
        {
            if (state == CustomerState.Queuing) target = queueTarget;
        }

        public void FinishShopping()
        {
            if (!hasItem) BasketValue = 0;
            BeginLeaving();
        }

        private void BeginLeaving()
        {
            state = CustomerState.Leaving;
            target = MiniMartGameManager.Instance.CustomerExit.position;
            stateTimer = 1.5f;
        }
    }

    public class CameraFollower : MonoBehaviour
    {
        public Transform target;
        private readonly Vector3 offset = new Vector3(-13.5f, 18f, -15.5f);
        private readonly Vector3 framingOffset = new Vector3(6f, 0f, 2f);
        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = target.position + framingOffset + offset;
            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * 4f);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(55f, 45f, 0f), Time.deltaTime * 5f);
        }
    }

    public class MiniMartUI : MonoBehaviour
    {
        private MiniMartGameManager game;
        private Text moneyText;
        private Text carryText;
        private Text notificationText;
        private Text helperText;
        private float notificationUntil;

        public static MiniMartUI Create(MiniMartGameManager manager)
        {
            GameObject root = new GameObject("MiniMart_UI");
            MiniMartUI ui = root.AddComponent<MiniMartUI>();
            ui.game = manager;
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280f, 720f);
            root.AddComponent<GraphicRaycaster>();
            ui.Build();
            return ui;
        }

        private void Build()
        {
            moneyText = CreateText("Money", new Vector2(28f, -24f), TextAnchor.UpperLeft, 32, new Color(0.18f, 0.29f, 0.38f));
            carryText = CreateText("Carry", new Vector2(28f, -70f), TextAnchor.UpperLeft, 20, new Color(0.18f, 0.29f, 0.38f));
            helperText = CreateText("Help", new Vector2(28f, 35f), TextAnchor.LowerLeft, 19, Color.white);
            RectTransform helperRect = helperText.rectTransform;
            helperRect.anchorMin = new Vector2(0f, 0f);
            helperRect.anchorMax = new Vector2(0f, 0f);
            helperRect.pivot = new Vector2(0f, 0f);
            helperText.text = "WASD  Move      E  Interact      ESC  Pause";
            notificationText = CreateText("Notification", new Vector2(0f, 70f), TextAnchor.LowerCenter, 24, Color.white);
            RectTransform noteRect = notificationText.rectTransform;
            noteRect.anchorMin = new Vector2(0.5f, 0f);
            noteRect.anchorMax = new Vector2(0.5f, 0f);
            noteRect.pivot = new Vector2(0.5f, 0f);
        }

        private Text CreateText(string name, Vector2 position, TextAnchor alignment, int size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(transform, false);
            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(920f, 80f);
            return text;
        }

        public void Refresh()
        {
            if (moneyText == null) return;
            moneyText.text = "$ " + game.Money;
            carryText.text = game.Player.Carrying == null ? "Hands free — visit storage boxes" : "Carrying: " + game.Player.Carrying + "  (E near matching shelf)";
            if (Time.unscaledTime > notificationUntil) notificationText.text = string.Empty;
        }

        public void SetNotification(string message, float duration)
        {
            if (notificationText == null) return;
            notificationText.text = message;
            notificationUntil = Time.unscaledTime + duration;
        }
    }
}
