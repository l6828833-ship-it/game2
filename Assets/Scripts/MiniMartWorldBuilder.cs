using System.Collections.Generic;
using UnityEngine;

namespace MiniMart
{
    /// <summary>
    /// Builds the farm, the store and every prop out of primitives at runtime, plus the
    /// shared material/primitive helpers and the day/night lighting pass.
    /// </summary>
    public partial class MiniMartGameManager
    {
        private static readonly ProductKind[] ExtraShelfProducts =
        {
            ProductKind.Watermelon, ProductKind.Tomato, ProductKind.Banana, ProductKind.Tomato,
            ProductKind.Tomato, ProductKind.Watermelon, ProductKind.Banana, ProductKind.Watermelon
        };

        /// <summary>Prop sizes in metres. The nest is wide and shallow, so its height stays low.</summary>
        private const float NestHeight = 0.34f;
        private const float ChickenHeight = 0.46f;
        private const float EggHeight = 0.18f;

        /// <summary>Tilled plot thickness. Two of them side by side make up one crop bed.</summary>
        private const float PlotHeight = 0.55f;

        /// <summary>
        /// Paddock bounds. The north rail sits at z = -7.2, clear of the store floor which starts at
        /// z = -6, so the fenced field never overlaps the shop or the farm.
        /// </summary>
        private const float PastureNorthZ = -7.2f;
        private const float PastureSouthZ = -13f;
        private const float PastureWestX = -19f;
        private const float PastureEastX = 13.5f;
        private const float PastureCenterX = (PastureWestX + PastureEastX) * 0.5f;
        private const float PastureCenterZ = (PastureNorthZ + PastureSouthZ) * 0.5f;
        private const float PastureWidth = PastureEastX - PastureWestX;
        private const float PastureDepth = PastureNorthZ - PastureSouthZ;

        /// <summary>Free floor space for purchased shelves, in build order.</summary>
        private static readonly Vector2[] ExtraShelfSlots =
        {
            new Vector2(6.4f, 1.7f), new Vector2(9.2f, 1.7f), new Vector2(12.0f, 1.7f), new Vector2(14.6f, 1.7f),
            new Vector2(12.0f, 4.6f), new Vector2(14.6f, 4.6f), new Vector2(6.4f, -1.2f), new Vector2(9.2f, -1.2f)
        };

        private readonly Dictionary<ProductKind, Color> productColors = new Dictionary<ProductKind, Color>();
        private readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
        private Light sun;
        private Camera sceneCamera;
        private Shader vertexColorShader;
        private bool vertexColorShaderChecked;

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
            productColors[ProductKind.Tomato] = new Color(0.91f, 0.24f, 0.20f);
            productColors[ProductKind.Watermelon] = new Color(0.36f, 0.66f, 0.28f);
            productColors[ProductKind.Banana] = new Color(0.98f, 0.82f, 0.24f);
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
            BuildPasture();
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
        }

        // ------------------------------------------------------------------- farm

        private void BuildFarm()
        {
            Material soil = MaterialFor("FarmSoil", new Color(0.48f, 0.25f, 0.12f));
            Material fence = MaterialFor("FarmFence", new Color(0.68f, 0.42f, 0.18f));
            // Two slim path pieces make a clean gate between shop and farm without covering the farm soil.
            CreatePrimitive(PrimitiveType.Cube, "Farm_Walkway", new Vector3(-14.1f, 0.06f, -2.2f), new Vector3(5.0f, 0.08f, 1.35f), MaterialFor("FarmPath", new Color(0.91f, 0.72f, 0.40f)));
            CreatePrimitive(PrimitiveType.Cube, "Farm_Gate_Path", new Vector3(-10.8f, 0.06f, -2.2f), new Vector3(1.4f, 0.08f, 2.5f), MaterialFor("FarmPath", new Color(0.91f, 0.72f, 0.40f)));
            CreatePrimitive(PrimitiveType.Cube, "Farm_Zone", new Vector3(-16f, 0.05f, 2.1f), new Vector3(8.5f, 0.1f, 9f), MaterialFor("FarmGrass", new Color(0.58f, 0.88f, 0.34f)));
            // Each bed grows what the shop actually sells, so every shelf has a source.
            BuildCropBed(new Vector3(-18f, 0f, 3.6f), "Tomato_Bed", soil, ProductKind.Tomato, new Color(0.91f, 0.24f, 0.20f));
            BuildCropBed(new Vector3(-14.2f, 0f, 3.6f), "Melon_Bed", soil, ProductKind.Watermelon, new Color(0.36f, 0.66f, 0.28f));
            BuildCropBed(new Vector3(-18f, 0f, 0.25f), "Banana_Bed", soil, ProductKind.Banana, new Color(0.98f, 0.82f, 0.24f));
            // Farm production is limited to the three supplied crop plots; no nest or loose collection point is spawned.
            BuildFenceLine(new Vector3(-20.3f, 0f, 2.1f), new Vector3(0f, 0f, 8.8f), fence, 6);
            BuildFenceLine(new Vector3(-16f, 0f, 6.5f), new Vector3(8.6f, 0f, 0f), fence, 6);
            CreatePrimitive(PrimitiveType.Cylinder, "Farm_Well", new Vector3(-11.2f, 0.48f, 3.4f), new Vector3(0.78f, 0.48f, 0.78f), MaterialFor("Well", new Color(0.48f, 0.62f, 0.66f)));
            CreatePrimitive(PrimitiveType.Sphere, "Farm_Water", new Vector3(-11.2f, 0.91f, 3.4f), new Vector3(0.62f, 0.12f, 0.62f), MaterialFor("Water", new Color(0.19f, 0.70f, 0.94f)));
        }

        private void BuildCropBed(Vector3 position, string label, Material soil, ProductKind product, Color fruitColor)
        {
            // A brown ground slab guarantees a readable cultivated patch even while the imported
            // soil asset is being processed by Unity. The new supplied soil model decorates the same patch.
            CreatePrimitive(PrimitiveType.Cube, label + "_Earth", position + new Vector3(0f, 0.12f, 0f),
                new Vector3(4.35f, 0.24f, 3.05f), soil);
            Material soilTint = MaterialFor("PlotSoil", new Color(0.55f, 0.36f, 0.20f));
            Transform plot = ModelKit.SpawnProp(null, "Props/FarmSoilPlot", soilTint, 0.42f, 3, Vector3.zero);
            if (plot != null)
            {
                plot.name = label + "_Soil_Plot";
                plot.position = position + new Vector3(0f, 0.20f, 0f);
            }

            // The producer creates exactly four real crop models on this earth plot and removes one
            // per interaction. No loose crate, nest, or separate collection marker is used.
            CreateFarmProducer(position, product, GameConfig.ProductLabel(product), fruitColor, 0f, 0.42f, false);
        }

        private void BuildChickenCoop(Vector3 position)
        {
            CreatePrimitive(PrimitiveType.Cube, "Chicken_Coop", position + new Vector3(0f, 0.55f, 0f), new Vector3(3f, 1.1f, 1.65f), MaterialFor("Coop", new Color(0.91f, 0.42f, 0.23f)));
            CreatePrimitive(PrimitiveType.Cylinder, "Chicken_Coop_Roof", position + new Vector3(0f, 1.25f, 0f), new Vector3(1.9f, 0.22f, 1.2f), MaterialFor("CoopRoof", new Color(0.31f, 0.58f, 0.84f)));

            // The nest is the harvest point: a straw bowl with the egg resting inside it.
            Vector3 nestSpot = position + new Vector3(1.55f, 0f, -1.15f);
            Transform nest = ModelKit.SpawnProp(null, ModelKit.NestModel, MaterialFor("NestStraw", new Color(0.83f, 0.66f, 0.34f)),
                NestHeight, 3, ModelKit.ZUpFix);
            if (nest != null)
            {
                nest.name = "Chicken_Nest";
                nest.position = nestSpot;
            }

            // The egg rests on the rim so it reads from the camera instead of hiding in the bowl.
            FarmProducer eggs = CreateFarmProducer(nestSpot, ProductKind.Egg, "Eggs", new Color(1f, 0.96f, 0.74f),
                EggHeight, nest != null ? NestHeight * 0.8f : 0.5f, nest == null);
            eggs.ReadySound = SfxKind.Cluck;

            // One hen, keeping to her nest. She sits just behind it, which from this camera angle
            // leaves the egg in clear view in front of her.
            BuildChicken(nestSpot + new Vector3(0.06f, 0f, 0.42f), eggs, "Hen");
        }

        /// <summary>
        /// The paddock, south of everything else. Livestock can only pick targets inside their own
        /// patch of it, so they can never wander onto the shop floor or through the crop beds: the
        /// store starts at z = -6 and the farm at z = -2.4, both north of the fence line.
        /// </summary>
        private void BuildPasture()
        {
            Material fence = MaterialFor("PastureFence", new Color(0.72f, 0.50f, 0.26f));
            CreatePrimitive(PrimitiveType.Cube, "Pasture_Ground", new Vector3(PastureCenterX, 0.05f, PastureCenterZ),
                new Vector3(PastureWidth, 0.1f, PastureDepth), MaterialFor("PastureGrass", new Color(0.52f, 0.84f, 0.36f)));

            // North rail is split so the player can walk in through a gate.
            BuildFenceLine(new Vector3(-10f, 0f, PastureNorthZ), new Vector3(18f, 0f, 0f), fence, 9);
            BuildFenceLine(new Vector3(7.5f, 0f, PastureNorthZ), new Vector3(12f, 0f, 0f), fence, 6);
            BuildFenceLine(new Vector3(PastureCenterX, 0f, PastureSouthZ), new Vector3(PastureWidth, 0f, 0f), fence, 14);
            BuildFenceLine(new Vector3(PastureWestX, 0f, PastureCenterZ), new Vector3(0f, 0f, PastureDepth), fence, 4);
            BuildFenceLine(new Vector3(PastureEastX, 0f, PastureCenterZ), new Vector3(0f, 0f, PastureDepth), fence, 4);

            CreatePrimitive(PrimitiveType.Cube, "Water_Trough", new Vector3(-16.5f, 0.22f, -8.6f),
                new Vector3(1.9f, 0.44f, 0.9f), MaterialFor("Trough", new Color(0.55f, 0.42f, 0.28f)));
            CreateDecor(PrimitiveType.Cube, "Water_Trough_Water", new Vector3(-16.5f, 0.42f, -8.6f),
                new Vector3(1.7f, 0.06f, 0.72f), MaterialFor("Water", new Color(0.19f, 0.70f, 0.94f)));

            // Each animal keeps to its own corner so they stay spread across the field.
            BuildPastureAnimal(ModelKit.CowModel, "Cow", new Vector3(-14.5f, 0f, -10.6f), 3.2f, 1.8f,
                1.45f, 0.35f, new Color(0.44f, 0.40f, 0.38f), false);
            BuildPastureAnimal(ModelKit.SheepModel, "Sheep_A", new Vector3(-7.5f, 0f, -9.4f), 2.6f, 1.6f,
                0.85f, 0.42f, new Color(0.96f, 0.95f, 0.90f), true);
            BuildPastureAnimal(ModelKit.SheepModel, "Sheep_B", new Vector3(-4.6f, 0f, -11.3f), 2.6f, 1.6f,
                0.85f, 0.42f, new Color(0.93f, 0.92f, 0.86f), true);
            BuildPastureAnimal(ModelKit.PigModel, "Pig", new Vector3(1.8f, 0f, -10.9f), 3f, 1.7f,
                0.70f, 0.5f, new Color(0.96f, 0.62f, 0.66f), true);
            BuildPastureAnimal(ModelKit.DuckModel, "Duck_A", new Vector3(8f, 0f, -9.2f), 2.4f, 1.5f,
                0.50f, 0.6f, new Color(0.99f, 0.97f, 0.88f), true);
            BuildPastureAnimal(ModelKit.DuckModel, "Duck_B", new Vector3(10.6f, 0f, -11f), 2.4f, 1.5f,
                0.50f, 0.6f, new Color(0.98f, 0.94f, 0.80f), true);
        }

        private void BuildPastureAnimal(string model, string name, Vector3 home, float patchWidth, float patchDepth,
            float height, float speed, Color color, bool grazes)
        {
            GameObject root = new GameObject("Pasture_" + name);
            root.transform.position = home;

            // Vertex colours give the cow its patches and the pig its snout, so no flat paint here.
            Transform body = ModelKit.SpawnProp(root.transform, model, VertexColorMaterial("Hide_" + name, color),
                height, 0, Vector3.zero);
            if (body == null)
            {
                Destroy(root);
                return;
            }
            body.name = name + "_Body";

            // Clamped so no patch can reach past the rails, whatever the home point is.
            Rect patch = Rect.MinMaxRect(
                Mathf.Max(PastureWestX + 0.8f, home.x - patchWidth * 0.5f),
                Mathf.Max(PastureSouthZ + 0.8f, home.z - patchDepth * 0.5f),
                Mathf.Min(PastureEastX - 0.8f, home.x + patchWidth * 0.5f),
                Mathf.Min(PastureNorthZ - 0.8f, home.z + patchDepth * 0.5f));

            root.AddComponent<RoamingAnimal>().Initialise(body, patch, speed, height * 0.035f, grazes);
        }

        private void BuildChicken(Vector3 position, FarmProducer nest, string name)
        {
            GameObject root = new GameObject(name);
            root.transform.position = position;

            // The FarmAnimals hen is Y up with forward +Z already, and her markings are vertex colours.
            Transform body = ModelKit.SpawnProp(root.transform, ModelKit.ChickenModel,
                VertexColorMaterial("Hen", new Color(0.96f, 0.93f, 0.86f)), ChickenHeight, 0, Vector3.zero);
            if (body == null)
            {
                // No imported hen: fall back to the primitive bird so the coop is not empty.
                BuildToyChicken(position, name);
                Destroy(root);
                return;
            }

            body.name = "Hen_Body";
            // Barely a patch at all: she shifts about on the nest rather than wandering off.
            Rect patch = new Rect(position.x - 0.16f, position.z - 0.16f, 0.32f, 0.32f);
            root.AddComponent<RoamingAnimal>().Initialise(body, patch, 0.22f, ChickenHeight * 0.05f, true, nest);
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

        // ------------------------------------------------------------------ store

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
            // Only the four farm products, doubled up. A shelf of something the farm cannot grow
            // would empty out and stay empty, since harvesting is the only way to restock.
            ProductKind[] backRow = { ProductKind.Tomato, ProductKind.Watermelon, ProductKind.Banana, ProductKind.Tomato };
            ProductKind[] frontRow = { ProductKind.Banana, ProductKind.Tomato, ProductKind.Watermelon, ProductKind.Banana };
            for (int i = 0; i < StoreLayout.ShelfColumns.Length; i++)
            {
                CreateShelf(new Vector3(StoreLayout.ShelfColumns[i], 0f, StoreLayout.BackRowZ), backRow[i], GameConfig.ShelfCapacity - 3);
                CreateShelf(new Vector3(StoreLayout.ShelfColumns[i], 0f, StoreLayout.FrontRowZ), frontRow[i], GameConfig.ShelfCapacity - 3);
            }
            for (int i = 0; i < save.extraShelves; i++) CreateExtraShelf(i);
        }

        /// <summary>
        /// Extra shelves fill hand picked slots on the right hand side of the shop so they never
        /// land on the coolers, the produce display, the till or the upgrade pads.
        /// </summary>
        private void CreateExtraShelf(int index)
        {
            Vector2 slot = ExtraShelfSlots[index % ExtraShelfSlots.Length];
            CreateShelf(new Vector3(slot.x, 0f, slot.y), ExtraShelfProducts[index % ExtraShelfProducts.Length], GameConfig.ShelfCapacity - 3);
        }

        private void CreateShelf(Vector3 position, ProductKind product, int stock)
        {
            GameObject root = new GameObject("Shelf_" + product);
            root.transform.position = position;
            ShelfUnit shelf = root.AddComponent<ShelfUnit>();
            shelf.Initialise(product, stock);
            Shelves.Add(shelf);
        }

        private FarmProducer CreateFarmProducer(Vector3 position, ProductKind product, string label, Color color,
            float modelHeight = 0f, float restHeight = 0.5f, bool showMarker = true)
        {
            GameObject root = new GameObject("FarmHarvest_" + product);
            root.transform.position = position;
            FarmProducer producer = root.AddComponent<FarmProducer>();
            producer.Initialise(product, label, color, modelHeight, restHeight, showMarker);
            FarmProducers.Add(producer);
            return producer;
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
            CreateUpgrade(new Vector3(1.5f, 0f, -4.55f), UpgradeType.Crate, new Color(0.55f, 0.85f, 0.45f));
            CreateUpgrade(new Vector3(4.0f, 0f, -4.55f), UpgradeType.ExtraShelf, new Color(0.37f, 0.84f, 0.89f));
            CreateUpgrade(new Vector3(6.5f, 0f, -4.55f), UpgradeType.Customers, new Color(0.96f, 0.57f, 0.82f));
            CreateUpgrade(new Vector3(9.0f, 0f, -4.55f), UpgradeType.Premium, new Color(1f, 0.78f, 0.2f));
        }

        private void CreateUpgrade(Vector3 position, UpgradeType kind, Color color)
        {
            GameObject root = new GameObject("Upgrade_" + kind);
            root.transform.position = position;
            UpgradeStation upgrade = root.AddComponent<UpgradeStation>();
            upgrade.Initialise(kind, color);
            Upgrades.Add(upgrade);
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
            sceneCamera = Camera.main;
            if (sceneCamera == null)
            {
                GameObject cam = new GameObject("Main Camera");
                cam.tag = "MainCamera";
                sceneCamera = cam.AddComponent<Camera>();
            }
            if (FindAnyObjectByType<AudioListener>() == null) sceneCamera.gameObject.AddComponent<AudioListener>();
            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = 14.5f;
            sceneCamera.backgroundColor = new Color(0.65f, 0.88f, 0.98f);
            sceneCamera.transform.position = new Vector3(-18f, 20f, -19f);
            sceneCamera.transform.rotation = Quaternion.Euler(55f, 45f, 0f);
            CameraFollower follow = sceneCamera.GetComponent<CameraFollower>() ?? sceneCamera.gameObject.AddComponent<CameraFollower>();
            follow.target = Player.transform;
        }

        private void BuildLighting()
        {
            sun = FindAnyObjectByType<Light>();
            if (sun == null) sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.94f, 0.79f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        /// <summary>Swings the sun across the sky and warms everything up towards closing time.</summary>
        private void UpdateSunlight(float timeOfDay)
        {
            float warmth = Mathf.Clamp01(Mathf.Abs(timeOfDay - 0.5f) * 2f);
            Color sky = Color.Lerp(new Color(0.65f, 0.88f, 0.98f), new Color(0.99f, 0.71f, 0.52f), warmth);
            if (sun != null)
            {
                float elevation = Mathf.Lerp(16f, 74f, Mathf.Sin(timeOfDay * Mathf.PI));
                sun.transform.rotation = Quaternion.Euler(elevation, -55f + timeOfDay * 110f, 0f);
                sun.color = Color.Lerp(new Color(1f, 0.96f, 0.87f), new Color(1f, 0.71f, 0.44f), warmth);
                sun.intensity = Mathf.Lerp(1.35f, 0.8f, warmth);
            }
            if (sceneCamera != null) sceneCamera.backgroundColor = sky;
            RenderSettings.ambientLight = Color.Lerp(new Color(0.66f, 0.75f, 0.79f), new Color(0.47f, 0.46f, 0.56f), warmth * 0.85f);
            RenderSettings.fogColor = sky;
        }

        // --------------------------------------------------------------- utilities

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

        /// <summary>
        /// Material that keeps a model's own vertex colours: the farm animals carry their markings
        /// there rather than in a texture. Falls back to a flat colour if the shader is unavailable,
        /// so a shader problem costs the markings rather than turning everything magenta.
        /// </summary>
        public Material VertexColorMaterial(string name, Color fallback)
        {
            string key = "VC_" + name;
            if (materialCache.TryGetValue(key, out Material cached)) return cached;

            if (vertexColorShader == null && !vertexColorShaderChecked)
            {
                vertexColorShaderChecked = true;
                vertexColorShader = Resources.Load<Shader>("Shaders/VertexColorLit");
                if (vertexColorShader != null && !vertexColorShader.isSupported)
                {
                    Debug.LogWarning("MiniMart/VertexColorLit did not compile; animals fall back to flat colours.");
                    vertexColorShader = null;
                }
            }
            if (vertexColorShader == null) return MaterialFor(name, fallback);

            Material material = new Material(vertexColorShader) { name = "M_" + key };
            if (material.HasProperty("_Tint")) material.SetColor("_Tint", Color.white);
            materialCache[key] = material;
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

        /// <summary>Decoration that shoppers and the player should walk through, not bump into.</summary>
        public GameObject CreateDecor(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        {
            GameObject go = CreatePrimitive(type, name, position, scale, material, parent);
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return go;
        }
    }
}
