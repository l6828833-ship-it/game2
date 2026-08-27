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
        /// <summary>Prop sizes in metres. The nest is wide and shallow, so its height stays low.</summary>
        private const float NestHeight = 0.34f;
        private const float ChickenHeight = 0.72f;
        private const float EggHeight = 0.18f;

        /// <summary>Tilled plot thickness. Two of them side by side make up one crop bed.</summary>
        private const float PlotHeight = 0.55f;

        // Ground plane. A Unity plane is ten units across at scale one, hence the 0.2 when the
        // extent below is a half width.
        private const float GroundCenterX = -2f;
        private const float GroundCenterZ = -2f;
        private const float GroundExtent = 62f;

        /// <summary>
        /// Nothing is planted inside this box. It covers the farm, the shop, the paddock and the
        /// paths between them with room to spare, so no tree can end up somewhere the player walks.
        /// </summary>
        private static readonly Rect PlayArea = Rect.MinMaxRect(-23f, -15.5f, 19f, 10.5f);

        /// <summary>How far past the play area trees are scattered. Beyond this the camera never sees them.</summary>
        private const float WoodsDepth = 24f;

        private const int TreeCount = 78;

        /// <summary>Trunk heights in metres, per species, matching the model order in ModelKit.</summary>
        private static readonly float[] TreeHeights = { 5.4f, 6.2f, 5.8f, 5.0f };

        /// <summary>
        /// Paddock bounds. The north rail sits at z = -7.2, clear of the store floor which starts at
        /// z = -6, so the fenced field never overlaps the shop or the farm.
        /// </summary>
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
            productColors[ProductKind.Egg] = Color.white;
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

            // Countryside well past the fences, so the map does not end in mid air at the edges.
            CreatePrimitive(PrimitiveType.Plane, "Pastel Grass", new Vector3(GroundCenterX, 0f, GroundCenterZ),
                new Vector3(GroundExtent * 0.2f, 1f, GroundExtent * 0.2f), MaterialFor("Grass", new Color(0.47f, 0.82f, 0.38f)));
            CreatePrimitive(PrimitiveType.Cube, "Market Floor", new Vector3(3f, 0.04f, 1f), new Vector3(26f, 0.12f, 14f), MaterialFor("Floor", new Color(0.96f, 0.82f, 0.57f)));
            BuildFarm();
            BuildWildlife();
            BuildWoods();
            BuildStoreShell();
            BuildProps();
            BuildShelves();
            BuildCheckout();
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
            BuildChickenNest(new Vector3(-14.2f, 0f, 0.15f));
            // Each farm source is a visible asset: soil plots for crops and one nest for eggs.
            BuildFenceLine(new Vector3(-20.3f, 0f, 2.1f), new Vector3(0f, 0f, 8.8f), fence, 6);
            BuildFenceLine(new Vector3(-16f, 0f, 6.5f), new Vector3(8.6f, 0f, 0f), fence, 6);
            CreatePrimitive(PrimitiveType.Cylinder, "Farm_Well", new Vector3(-11.2f, 0.48f, 3.4f), new Vector3(0.78f, 0.48f, 0.78f), MaterialFor("Well", new Color(0.48f, 0.62f, 0.66f)));
            CreatePrimitive(PrimitiveType.Sphere, "Farm_Water", new Vector3(-11.2f, 0.91f, 3.4f), new Vector3(0.62f, 0.12f, 0.62f), MaterialFor("Water", new Color(0.19f, 0.70f, 0.94f)));
        }

        private void BuildCropBed(Vector3 position, string label, Material soil, ProductKind product, Color fruitColor)
        {
            // This is the user's supplied farm-spot FBX. It is spawned without the runtime paint
            // helper and without a fallback cube, so the asset's own soil shape and imported material
            // remain visible instead of becoming a plain brown rectangle.
            SpawnFarmSoilPlot(position, label);

            // The producer creates exactly four real crop models on this earth plot and removes one
            // per interaction. No loose crate, nest, or separate collection marker is used.
            CreateFarmProducer(position, product, GameConfig.ProductLabel(product), fruitColor, 0f, 0.56f, false);
        }

        private Transform SpawnFarmSoilPlot(Vector3 position, string label)
        {
            GameObject asset = Resources.Load<GameObject>("Props/FarmSoilPlot");
            if (asset == null)
            {
                Debug.LogWarning("FarmSoilPlot.fbx is missing from Resources/Props.");
                return null;
            }

            GameObject pivot = new GameObject(label + "_Soil_Plot");
            GameObject model = Instantiate(asset, pivot.transform);
            model.name = "FarmSoilPlot_Mesh";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(ModelKit.ZUpFix);
            model.transform.localScale = Vector3.one;
            ModelKit.KeepOneLod(model, 0);
            ModelKit.SitOnGround(pivot.transform, model.transform, 0.56f);
            // Colour the actual FBX mesh, not an extra primitive beneath it. The Lit material keeps
            // the soil asset's edges, normals, and light/shadow detail visible from the game camera.
            ModelKit.Paint(model, MaterialFor("FarmSpotWarmSoil", new Color(0.48f, 0.25f, 0.11f)));
            ModelKit.StripColliders(model);
            pivot.transform.position = position;
            return pivot.transform;
        }

        private void BuildChickenNest(Vector3 nestSpot)
        {
            // The uploaded OBJ is the actual egg source. A warm straw colour matches the supplied
            // reference while keeping the woven shape visible under the game's directional light.
            Transform nest = ModelKit.SpawnProp(null, "Props/FarmNest",
                MaterialFor("NestStraw", new Color(0.78f, 0.51f, 0.20f)), 0.42f, 0, Vector3.zero);
            if (nest != null)
            {
                nest.name = "Chicken_Nest";
                nest.position = nestSpot;
            }

            // One white egg sits raised in the open front of the nest. After collection, the same
            // white egg appears here again after its short regrow timer; it is not a separate pickup point.
            Vector3 eggSpot = nestSpot + new Vector3(-0.14f, 0f, -0.10f);
            FarmProducer eggs = CreateFarmProducer(eggSpot, ProductKind.Egg, "Nest egg", Color.white,
                0.22f, nest != null ? 0.34f : 0.42f, false, 1, true);
            eggs.ReadySound = SfxKind.Cluck;

            // The chicken is perched on the rear rim of the nest and deliberately remains still so
            // the white egg stays readable in front of her.
            BuildChicken(nestSpot + new Vector3(0.12f, 0.24f, 0.20f), "Nest_Chicken");
        }

        /// <summary>
        /// Scatters the tree pack in a band around the play area. Sampling is seeded so the woods
        /// come out the same every run rather than rearranging themselves each time you press Play,
        /// and every candidate inside the play area is thrown away, so nothing lands on the farm,
        /// the shop floor, the paddock or the paths between them.
        /// </summary>
        private void BuildWoods()
        {
            Material bark = TexturedMaterial("Tree", ModelKit.TreeTexture);
            Rect outer = Rect.MinMaxRect(
                PlayArea.xMin - WoodsDepth, PlayArea.yMin - WoodsDepth,
                PlayArea.xMax + WoodsDepth, PlayArea.yMax + WoodsDepth);

            Random.State callerState = Random.state;
            Random.InitState(20260826);

            Transform woods = new GameObject("Woods").transform;
            int planted = 0;
            for (int attempt = 0; attempt < TreeCount * 12 && planted < TreeCount; attempt++)
            {
                Vector3 spot = new Vector3(Random.Range(outer.xMin, outer.xMax), 0f, Random.Range(outer.yMin, outer.yMax));
                if (PlayArea.Contains(new Vector2(spot.x, spot.z))) continue;

                int species = Random.Range(0, ModelKit.TreeModels.Length);
                float height = TreeHeights[species % TreeHeights.Length] * Random.Range(0.72f, 1.25f);
                Transform tree = ModelKit.SpawnProp(woods, ModelKit.TreeModels[species], bark, height, 0, Vector3.zero);
                if (tree == null) break; // pack missing, no point trying the rest

                tree.name = "Tree_" + planted;
                tree.position = spot;
                tree.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                planted++;
            }

            Random.state = callerState;
        }

        /// <summary>
        /// Animals scattered around the countryside. Same exclusion zone as the trees: nothing
        /// lands inside the play area, so they potter about among the woods instead of blocking
        /// the paths and the shop floor. Placement is seeded for consistency.
        /// </summary>
        private void BuildWildlife()
        {
            string[] species = { ModelKit.CowModel, ModelKit.SheepModel, ModelKit.SheepModel,
                ModelKit.PigModel, ModelKit.DuckModel, ModelKit.DuckModel };
            float[] heights = { 1.45f, 0.85f, 0.85f, 0.70f, 0.50f, 0.50f };
            float[] speeds = { 0.35f, 0.42f, 0.40f, 0.5f, 0.6f, 0.55f };
            bool[] grazes = { false, true, true, true, true, true };
            Color[] colors =
            {
                new Color(0.44f, 0.40f, 0.38f), new Color(0.96f, 0.95f, 0.90f),
                new Color(0.93f, 0.92f, 0.86f), new Color(0.96f, 0.62f, 0.66f),
                new Color(0.99f, 0.97f, 0.88f), new Color(0.98f, 0.94f, 0.80f)
            };

            Rect outer = Rect.MinMaxRect(
                PlayArea.xMin - WoodsDepth + 2f, PlayArea.yMin - WoodsDepth + 2f,
                PlayArea.xMax + WoodsDepth - 2f, PlayArea.yMax + WoodsDepth - 2f);

            Random.State callerState = Random.state;
            Random.InitState(20260827);

            for (int i = 0; i < species.Length; i++)
            {
                Vector3 home = Vector3.zero;
                for (int attempt = 0; attempt < 60; attempt++)
                {
                    float x = Random.Range(outer.xMin, outer.xMax);
                    float z = Random.Range(outer.yMin, outer.yMax);
                    if (PlayArea.Contains(new Vector2(x, z))) continue;
                    home = new Vector3(x, 0f, z);
                    break;
                }
                if (home == Vector3.zero) continue;

                string label = species[i].Substring(species[i].LastIndexOf('/') + 1) + "_" + i;
                GameObject root = new GameObject("Animal_" + label);
                root.transform.position = home;

                Transform body = ModelKit.SpawnProp(root.transform, species[i],
                    VertexColorMaterial("Hide_" + label, colors[i]), heights[i], 0, Vector3.zero);
                if (body == null) { Destroy(root); continue; }
                body.name = label + "_Body";

                float pw = 4f;
                Rect patch = new Rect(home.x - pw * 0.5f, home.z - pw * 0.5f, pw, pw);
                root.AddComponent<RoamingAnimal>().Initialise(body, patch, speeds[i], heights[i] * 0.035f, grazes[i]);
            }

            Random.state = callerState;
        }

        private void BuildChicken(Vector3 position, string name)
        {
            // The Easy Primitive Animals chicken is a multi-part primitive prefab, but its materials
            // use the built-in Standard shader which URP renders magenta. Instead of upgrading twelve
            // materials on disk, we instantiate it and repaint every renderer with URP materials
            // carrying the original colours.
            GameObject prefab = Resources.Load<GameObject>(ModelKit.ChickenPrefab);
            if (prefab != null)
            {
                GameObject hen = Instantiate(prefab);
                hen.name = name;
                hen.transform.position = position;
                hen.transform.rotation = Quaternion.Euler(0f, 160f, 0f);
                hen.transform.localScale = Vector3.one * 0.85f;
                RepaintChicken(hen);
                // The prefab is built from Unity primitives, which ship with colliders. Left in
                // place they form a wall around the nest that stops the player reaching the egg.
                ModelKit.StripColliders(hen);
                return;
            }

            // Fallback: primitive blobs.
            BuildToyChicken(position, name);
        }

        /// <summary>
        /// Maps the pack's built-in shader material names to URP colours so the chicken does not
        /// render magenta. The pack uses: White (body/wings), Orange (feet/beak), Dark Red (comb),
        /// Gold (legs), Dark Pink (wattle).
        /// </summary>
        private void RepaintChicken(GameObject hen)
        {
            Material white = MaterialFor("ChickenWhite", new Color(1f, 1f, 1f));
            Material orange = MaterialFor("ChickenOrange", new Color(1f, 0.47f, 0.12f));
            Material darkRed = MaterialFor("ChickenDarkRed", new Color(0.80f, 0.14f, 0.14f));
            Material gold = MaterialFor("ChickenGold", new Color(1f, 0.86f, 0f));
            Material darkPink = MaterialFor("ChickenDarkPink", new Color(0.85f, 0.32f, 0.42f));

            foreach (Renderer renderer in hen.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.sharedMaterial == null) { renderer.sharedMaterial = white; continue; }
                string matName = renderer.sharedMaterial.name;
                if (matName.Contains("White")) renderer.sharedMaterial = white;
                else if (matName.Contains("Orange")) renderer.sharedMaterial = orange;
                else if (matName.Contains("Dark Red")) renderer.sharedMaterial = darkRed;
                else if (matName.Contains("Gold")) renderer.sharedMaterial = gold;
                else if (matName.Contains("Dark Pink") || matName.Contains("Pink")) renderer.sharedMaterial = darkPink;
                else renderer.sharedMaterial = white;
            }
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

            // Dedicated egg furniture on the open right side of the shop, away from the customer lanes,
            // the coolers, the produce stand, and the till. It begins with four eggs in four sockets.
            CreateEggTable(new Vector3(7.0f, 0f, 1.65f));
        }

        private void CreateEggTable(Vector3 position)
        {
            GameObject root = new GameObject("Egg_Table");
            root.transform.position = position;
            // No rotation: the table lines up with the shop shelves, which are also unrotated.
            root.transform.rotation = Quaternion.identity;
            ShelfUnit table = root.AddComponent<ShelfUnit>();
            // Starts with two of its four recesses filled, leaving room to stock the rest.
            table.InitialiseEggTable(2, MiniMartGameManager.Instance.EggTableUpgraded);
            Shelves.Add(table);
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
            float modelHeight = 0f, float restHeight = 0.5f, bool showMarker = true,
            int itemCount = GameConfig.CarryCapacity, bool centreItems = false)
        {
            GameObject root = new GameObject("FarmHarvest_" + product);
            root.transform.position = position;
            FarmProducer producer = root.AddComponent<FarmProducer>();
            producer.Initialise(product, label, color, modelHeight, restHeight, showMarker, itemCount, centreItems);
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
        /// Material that samples a model's own texture. The tree pack ships a built in shader that
        /// URP cannot render, so its palette texture gets rebound to a URP material instead.
        /// </summary>
        public Material TexturedMaterial(string name, string texturePath)
        {
            string key = "Tex_" + name;
            if (materialCache.TryGetValue(key, out Material cached)) return cached;

            Texture2D texture = Resources.Load<Texture2D>(texturePath);
            if (texture == null) return MaterialFor(name, new Color(0.45f, 0.62f, 0.36f));

            Material material = MaterialFor(key, Color.white);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
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
