using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MiniMart
{
    public enum ProductKind { Milk, Bread, Apple, Juice, Cereal, Chips, Water, Cookies }

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
        public readonly List<StorageBox> Storages = new List<StorageBox>();
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

        private readonly ProductKind[] starterProducts =
        {
            ProductKind.Milk, ProductKind.Bread, ProductKind.Apple, ProductKind.Juice, ProductKind.Cereal
        };

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
                spawnTimer = Mathf.Lerp(8f, 3.5f, Mathf.Clamp01((Customers.Count + save.customerUpgrade) / 10f));
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
        }

        private void BuildWorld()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.66f, 0.75f, 0.79f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.72f, 0.88f, 0.94f);
            RenderSettings.fogDensity = 0.008f;

            CreatePrimitive(PrimitiveType.Plane, "Pastel Grass", Vector3.zero, new Vector3(3.5f, 1f, 3.5f), MaterialFor("Grass", new Color(0.35f, 0.75f, 0.47f)));
            CreatePrimitive(PrimitiveType.Cube, "Store Floor", new Vector3(0f, 0.04f, 0f), new Vector3(15f, 0.12f, 11f), MaterialFor("Floor", new Color(0.92f, 0.87f, 0.76f)));
            BuildStoreShell();
            BuildProps();
            BuildShelves();
            BuildStorage();
            BuildCheckout();
            BuildUpgrades();
            BuildPlayer();
            BuildCamera();
            BuildLighting();

            CustomerSpawn = new GameObject("Customer_Entrance").transform;
            CustomerSpawn.position = new Vector3(-8f, 0f, -1.8f);
            CustomerExit = new GameObject("Customer_Exit").transform;
            CustomerExit.position = new Vector3(-9.5f, 0f, -1.8f);
            for (int i = 0; i < 3; i++) SpawnCustomer();
            spawnTimer = 3.5f;
        }

        private void BuildStoreShell()
        {
            Material wall = MaterialFor("Wall", new Color(0.96f, 0.74f, 0.60f));
            Material roof = MaterialFor("Roof", new Color(0.39f, 0.55f, 0.89f));
            CreatePrimitive(PrimitiveType.Cube, "Back Wall", new Vector3(0f, 2.3f, 5.35f), new Vector3(15f, 4.5f, 0.28f), wall);
            CreatePrimitive(PrimitiveType.Cube, "Left Wall", new Vector3(-7.35f, 2.3f, 2.4f), new Vector3(0.28f, 4.5f, 5.9f), wall);
            CreatePrimitive(PrimitiveType.Cube, "Right Wall", new Vector3(7.35f, 2.3f, 2.4f), new Vector3(0.28f, 4.5f, 5.9f), wall);
            CreatePrimitive(PrimitiveType.Cube, "Roof Trim", new Vector3(0f, 4.7f, 5.15f), new Vector3(15.5f, 0.35f, 0.65f), roof);
            CreatePrimitive(PrimitiveType.Cube, "Entry Sign", new Vector3(-5.5f, 3.25f, -0.4f), new Vector3(2.6f, 0.75f, 0.2f), MaterialFor("Sign", new Color(1f, 0.88f, 0.2f)));
            CreatePrimitive(PrimitiveType.Cylinder, "Plant Pot L", new Vector3(-6.2f, 0.35f, -3.2f), new Vector3(0.45f, 0.35f, 0.45f), MaterialFor("Pot", new Color(0.89f, 0.43f, 0.34f)));
            CreatePrimitive(PrimitiveType.Sphere, "Plant L", new Vector3(-6.2f, 0.95f, -3.2f), new Vector3(0.75f, 1f, 0.75f), MaterialFor("Plant", new Color(0.27f, 0.65f, 0.34f)));
            CreatePrimitive(PrimitiveType.Cylinder, "Plant Pot R", new Vector3(6.2f, 0.35f, -3.2f), new Vector3(0.45f, 0.35f, 0.45f), MaterialFor("Pot", new Color(0.89f, 0.43f, 0.34f)));
            CreatePrimitive(PrimitiveType.Sphere, "Plant R", new Vector3(6.2f, 0.95f, -3.2f), new Vector3(0.75f, 1f, 0.75f), MaterialFor("Plant", new Color(0.27f, 0.65f, 0.34f)));
        }

        private void BuildProps()
        {
            Material fridge = MaterialFor("Fridge", new Color(0.54f, 0.86f, 0.95f));
            CreatePrimitive(PrimitiveType.Cube, "Cooler", new Vector3(5.65f, 1.6f, 3.6f), new Vector3(1.6f, 3.1f, 1.15f), fridge);
            CreatePrimitive(PrimitiveType.Cube, "Cooler Window", new Vector3(5.65f, 1.75f, 2.99f), new Vector3(1.2f, 2.35f, 0.04f), MaterialFor("Glass", new Color(0.7f, 0.94f, 1f)));
            CreatePrimitive(PrimitiveType.Cube, "Welcome Mat", new Vector3(-5.45f, 0.12f, -2.75f), new Vector3(2.1f, 0.05f, 1.25f), MaterialFor("Mat", new Color(0.93f, 0.38f, 0.48f)));
        }

        private void BuildShelves()
        {
            Vector3[] positions =
            {
                new Vector3(-2.4f, 0f, 2.7f), new Vector3(0.4f, 0f, 2.7f), new Vector3(3.2f, 0f, 2.7f),
                new Vector3(-2.4f, 0f, 0.35f), new Vector3(0.4f, 0f, 0.35f)
            };
            for (int i = 0; i < positions.Length; i++) CreateShelf(positions[i], starterProducts[i], true);
            for (int i = 0; i < save.extraShelves; i++)
                CreateShelf(new Vector3(3.2f + (i % 2) * 2.8f, 0f, 0.35f - (i / 2) * 2.2f), ProductKind.Chips, true);
        }

        private void CreateShelf(Vector3 position, ProductKind product, bool stocked)
        {
            GameObject root = new GameObject("Shelf_" + product);
            root.transform.position = position;
            ShelfUnit shelf = root.AddComponent<ShelfUnit>();
            shelf.Initialise(product, stocked ? 12 : 0);
            Shelves.Add(shelf);
        }

        private void BuildStorage()
        {
            ProductKind[] types = { ProductKind.Milk, ProductKind.Bread, ProductKind.Apple, ProductKind.Juice, ProductKind.Cereal };
            for (int i = 0; i < types.Length; i++)
            {
                GameObject root = new GameObject("Storage_" + types[i]);
                root.transform.position = new Vector3(-5.55f + i * 1.15f, 0f, 3.95f);
                StorageBox storage = root.AddComponent<StorageBox>();
                storage.Initialise(types[i]);
                Storages.Add(storage);
            }
        }

        private void BuildCheckout()
        {
            GameObject root = new GameObject("Checkout");
            root.transform.position = new Vector3(4.9f, 0f, -1.9f);
            Checkout = root.AddComponent<CheckoutStation>();
            Checkout.Initialise();
        }

        private void BuildUpgrades()
        {
            CreateUpgrade("Upgrade_Shelf", new Vector3(2.3f, 0f, -3.35f), "SHELF +", 60, UpgradeType.ExtraShelf, new Color(0.37f, 0.84f, 0.89f));
            CreateUpgrade("Upgrade_Customers", new Vector3(4.6f, 0f, -3.35f), "BUSY +", 90, UpgradeType.Customers, new Color(0.96f, 0.57f, 0.82f));
            CreateUpgrade("Upgrade_Premium", new Vector3(6.4f, 0f, -3.35f), "SALE +", 120, UpgradeType.Premium, new Color(1f, 0.78f, 0.2f));
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
            root.transform.position = new Vector3(-5.2f, 0f, -1.2f);
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
            camera.orthographicSize = 9.2f;
            camera.backgroundColor = new Color(0.67f, 0.86f, 0.95f);
            camera.transform.position = new Vector3(-9f, 12f, -12f);
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
                CreateShelf(new Vector3(3.2f + ((save.extraShelves - 1) % 2) * 2.8f, 0f, 0.35f - ((save.extraShelves - 1) / 2) * 2.2f), ProductKind.Chips, true);
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

        public StorageBox FindStorage(ProductKind kind)
        {
            foreach (StorageBox box in Storages) if (box.Product == kind) return box;
            return null;
        }

        public void SpawnCustomer()
        {
            GameObject customer = new GameObject("Customer_" + (++customerSerial));
            customer.transform.position = CustomerSpawn.position + new Vector3(0f, 0f, UnityEngine.Random.Range(-0.4f, 0.4f));
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
        private float wobble;
        public ProductKind? Carrying => carrying;

        public void Initialise()
        {
            controller = gameObject.AddComponent<CharacterController>();
            controller.radius = 0.34f;
            controller.height = 1.25f;
            controller.center = new Vector3(0f, 0.63f, 0f);
            visual = new GameObject("Cute_Player_Visual").transform;
            visual.SetParent(transform);
            BuildToyCharacter(visual, new Color(0.20f, 0.56f, 0.97f), new Color(1f, 0.75f, 0.58f), "Player");
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
            Vector3 movement = new Vector3(input.x, 0f, input.y).normalized;
            controller.Move(movement * 4.2f * Time.deltaTime + Vector3.down * 2f * Time.deltaTime);
            if (movement.sqrMagnitude > 0.01f)
            {
                transform.forward = Vector3.Slerp(transform.forward, movement, 14f * Time.deltaTime);
                wobble += Time.deltaTime * 11f;
                visual.localPosition = new Vector3(0f, 0.05f + Mathf.Abs(Mathf.Sin(wobble)) * 0.08f, 0f);
            }
            else visual.localPosition = Vector3.Lerp(visual.localPosition, Vector3.zero, Time.deltaTime * 7f);
            UpdateCarryVisual();
        }

        private void Interact()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            StorageBox closestStorage = FindClosest(game.Storages, 2.1f);
            if (carrying == null && closestStorage != null)
            {
                carrying = closestStorage.Product;
                game.UI.SetNotification("Picked up " + carrying + " box. Find the matching shelf!", 2f);
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
            game.UI.SetNotification(carrying == null ? "Walk near a storage box or upgrade station and press E." : "Walk to a shelf and press E to stock it.", 1.6f);
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

        public static void BuildToyCharacter(Transform root, Color shirt, Color skin, string label)
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            GameObject body = game.CreatePrimitive(PrimitiveType.Capsule, label + "_Body", root.position + new Vector3(0f, 0.53f, 0f), new Vector3(0.48f, 0.6f, 0.38f), game.MaterialFor(label + "_Shirt", shirt), root);
            body.transform.localPosition = new Vector3(0f, 0.48f, 0f);
            GameObject head = game.CreatePrimitive(PrimitiveType.Sphere, label + "_Head", root.position + new Vector3(0f, 1.22f, 0f), new Vector3(0.72f, 0.68f, 0.68f), game.MaterialFor(label + "_Skin", skin), root);
            head.transform.localPosition = new Vector3(0f, 1.18f, 0f);
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject arm = game.CreatePrimitive(PrimitiveType.Capsule, label + "_Arm", root.position, new Vector3(0.16f, 0.36f, 0.16f), game.MaterialFor(label + "_Shirt", shirt), root);
                arm.transform.localPosition = new Vector3(i * 0.37f, 0.55f, 0f);
                arm.transform.localRotation = Quaternion.Euler(0f, 0f, i * -18f);
            }
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject foot = game.CreatePrimitive(PrimitiveType.Cube, label + "_Shoe", root.position, new Vector3(0.25f, 0.16f, 0.38f), game.MaterialFor(label + "_Shoes", new Color(0.20f, 0.25f, 0.34f)), root);
                foot.transform.localPosition = new Vector3(i * 0.17f, 0.12f, 0.05f);
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

    public class StorageBox : MonoBehaviour
    {
        public ProductKind Product { get; private set; }
        public void Initialise(ProductKind kind)
        {
            Product = kind;
            MiniMartGameManager game = MiniMartGameManager.Instance;
            GameObject box = game.CreatePrimitive(PrimitiveType.Cube, "Stock_Box_" + kind, transform.position + new Vector3(0f, 0.35f, 0f), new Vector3(0.85f, 0.68f, 0.72f), game.MaterialFor("Box", new Color(0.65f, 0.38f, 0.20f)), transform);
            box.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            GameObject stripe = game.CreatePrimitive(PrimitiveType.Cube, "Box_Stripe", transform.position, new Vector3(0.88f, 0.18f, 0.74f), game.MaterialFor("Stock_" + kind, game.ProductColor(kind)), transform);
            stripe.transform.localPosition = new Vector3(0f, 0.47f, -0.38f);
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
        public int BasketValue { get; private set; }

        public void Initialise(int serial)
        {
            Color[] shirts = { new Color(0.96f, 0.43f, 0.57f), new Color(0.45f, 0.73f, 0.91f), new Color(0.62f, 0.76f, 0.37f), new Color(0.82f, 0.55f, 0.92f) };
            Color[] skins = { new Color(1f, 0.76f, 0.58f), new Color(0.63f, 0.40f, 0.28f), new Color(0.44f, 0.25f, 0.16f), new Color(0.88f, 0.61f, 0.43f) };
            visual = new GameObject("CustomerToy").transform;
            visual.SetParent(transform);
            PlayerShopper.BuildToyCharacter(visual, shirts[serial % shirts.Length], skins[(serial + 1) % skins.Length], "Customer" + serial);
            target = new Vector3(-4.4f, 0f, -1.5f);
            state = CustomerState.Entering;
        }

        private void Update()
        {
            float speed = 1.7f;
            Vector3 delta = target - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.04f)
            {
                transform.position += delta.normalized * speed * Time.deltaTime;
                transform.forward = Vector3.Slerp(transform.forward, delta.normalized, 8f * Time.deltaTime);
                visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.time * 7f)) * 0.05f, 0f);
                return;
            }
            visual.localPosition = Vector3.zero;
            stateTimer -= Time.deltaTime;
            if (state == CustomerState.Entering)
            {
                targetShelf = MiniMartGameManager.Instance.FindShelf((ProductKind)UnityEngine.Random.Range(0, 5));
                if (targetShelf == null) { BeginLeaving(); return; }
                target = targetShelf.transform.position + new Vector3(0f, 0f, -1.0f);
                state = CustomerState.GoingToShelf;
            }
            else if (state == CustomerState.GoingToShelf)
            {
                if (targetShelf != null && targetShelf.TakeOne())
                {
                    hasItem = true;
                    BasketValue = MiniMartGameManager.Instance.GetSaleValue(targetShelf.Product);
                }
                MiniMartGameManager.Instance.Checkout.JoinQueue(this);
                state = CustomerState.Queuing;
            }
            else if (state == CustomerState.Leaving && stateTimer <= 0f) MiniMartGameManager.Instance.RemoveCustomer(this);
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
        private readonly Vector3 offset = new Vector3(-8.4f, 11.4f, -10.2f);
        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * 4f);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(55f, 45f, 0f), Time.deltaTime * 5f);
        }
    }

    public class MiniMartUI : MonoBehaviour
    {
        private MiniMartGameManager game;
        private TextMeshProUGUI moneyText;
        private TextMeshProUGUI carryText;
        private TextMeshProUGUI notificationText;
        private TextMeshProUGUI helperText;
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
            moneyText = CreateText("Money", new Vector2(28f, -24f), TextAlignmentOptions.Left, 32f, new Color(0.18f, 0.29f, 0.38f));
            carryText = CreateText("Carry", new Vector2(28f, -70f), TextAlignmentOptions.Left, 20f, new Color(0.18f, 0.29f, 0.38f));
            helperText = CreateText("Help", new Vector2(28f, 35f), TextAlignmentOptions.Left, 19f, Color.white);
            RectTransform helperRect = helperText.rectTransform;
            helperRect.anchorMin = new Vector2(0f, 0f);
            helperRect.anchorMax = new Vector2(0f, 0f);
            helperRect.pivot = new Vector2(0f, 0f);
            helperText.text = "WASD  Move      E  Interact      ESC  Pause";
            notificationText = CreateText("Notification", new Vector2(0f, 70f), TextAlignmentOptions.Center, 24f, Color.white);
            RectTransform noteRect = notificationText.rectTransform;
            noteRect.anchorMin = new Vector2(0.5f, 0f);
            noteRect.anchorMax = new Vector2(0.5f, 0f);
            noteRect.pivot = new Vector2(0.5f, 0f);
        }

        private TextMeshProUGUI CreateText(string name, Vector2 position, TextAlignmentOptions alignment, float size, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
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
