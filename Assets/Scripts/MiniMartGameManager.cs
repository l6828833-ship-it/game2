using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MiniMart
{
    /// <summary>
    /// Owns the store economy, the day cycle and every spawned agent.
    /// World construction lives in the partial half (MiniMartWorldBuilder.cs).
    /// </summary>
    public partial class MiniMartGameManager : MonoBehaviour
    {
        public static MiniMartGameManager Instance { get; private set; }

        public int Money { get; private set; }
        public bool EggTableUpgraded => save.eggTableUpgraded;
        public int Day => save.day;
        public float Reputation => save.reputation;
        public DayPhase Phase { get; private set; }
        public bool IsPaused => paused;
        public int CrateSize => GameConfig.CarryCapacity;
        public int RentDue => GameConfig.RentForDay(save.day);
        public float DayProgress => Mathf.Clamp01(dayTimer / GameConfig.DayLength);
        public float ClockHour => Mathf.Lerp(GameConfig.OpeningHour, GameConfig.ClosingHour, DayProgress);

        public int EarnedToday { get; private set; }

        public int ServedToday { get; private set; }
        public int LostToday { get; private set; }

        public PlayerShopper Player { get; private set; }
        public CheckoutStation Checkout { get; private set; }
        public MiniMartUI UI { get; private set; }
        public MiniMartAudio Sfx { get; private set; }
        public Transform CustomerSpawn { get; private set; }
        public Transform CustomerExit { get; private set; }

        public readonly List<ShelfUnit> Shelves = new List<ShelfUnit>();
        public readonly List<FarmProducer> FarmProducers = new List<FarmProducer>();

        public readonly List<CustomerAgent> Customers = new List<CustomerAgent>();

        private StoreSave save;
        private float dayTimer;
        private float closingTimer;
        private float spawnTimer;
        private int customerSerial;
        private bool paused;
        private float resetArmedUntil;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Time.timeScale = 1f;
            LoadSave();
            InitialisePalette();
            Sfx = MiniMartAudio.Create(transform);
            BuildWorld();
            UI = MiniMartUI.Create(this);
            BuildHudMoneyIcon();
            Phase = DayPhase.Open;
            spawnTimer = 3f;
            UI.SetNotification("Day " + save.day + ": harvest on the farm, stock the shelves, keep the queue moving.", 5.5f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (UI == null) return;
            HandleGlobalKeys();
            if (paused) { UI.Refresh(); return; }
            TickDay();
            TickSpawning();
            UI.Refresh();
        }

        // ------------------------------------------------------------------ input

        private void HandleGlobalKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                paused = !paused;
                Time.timeScale = paused ? 0f : 1f;
                UI.SetPaused(paused);
            }

            if (!keyboard.f5Key.wasPressedThisFrame) return;
            if (Time.unscaledTime < resetArmedUntil)
            {
                resetArmedUntil = 0f;
                ResetSave();
                return;
            }
            resetArmedUntil = Time.unscaledTime + 3f;
            UI.SetNotification("Press F5 again to wipe your save and start over.", 3f);
        }

        // -------------------------------------------------------------- day cycle

        private void TickDay()
        {
            if (Phase == DayPhase.Open)
            {
                dayTimer += Time.deltaTime;
                UpdateSunlight(DayProgress);
                if (dayTimer >= GameConfig.DayLength) CloseStore();
                return;
            }

            closingTimer -= Time.deltaTime;
            UpdateSunlight(1f);
            if (closingTimer <= 0f) OpenNextDay();
        }

        private void CloseStore()
        {
            Phase = DayPhase.Closing;
            closingTimer = GameConfig.ClosingLength;

            // Serve anyone already holding an item, then total the day up.
            for (int i = Customers.Count - 1; i >= 0; i--)
                if (Customers[i] != null) Customers[i].SendHome();

            int rent = RentDue;
            int paid = Mathf.Min(Money, rent);
            Money -= paid;
            bool shortfall = paid < rent;
            if (shortfall) AdjustReputation(-10f);

            save.lifetimeEarnings += EarnedToday;
            Sfx.Play(SfxKind.DayEnd);
            UI.ShowDaySummary(save.day, EarnedToday, ServedToday, LostToday, rent, paid, Money, shortfall);
            Save();
        }

        private void OpenNextDay()
        {
            save.day++;
            Phase = DayPhase.Open;
            dayTimer = 0f;
            EarnedToday = 0;
            ServedToday = 0;
            LostToday = 0;
            spawnTimer = 2.5f;
            UI.HideDaySummary();
            UI.SetNotification("Day " + save.day + " is open. Tonight's rent is $" + RentDue + ".", 4f);
            Save();
        }

        // --------------------------------------------------------------- shoppers

        private void TickSpawning()
        {
            if (Phase != DayPhase.Open) return;
            spawnTimer -= Time.deltaTime;
            int cap = GameConfig.MaxShoppers;
            if (spawnTimer > 0f || Customers.Count >= cap) return;

            if (TotalStock() <= 0)
            {
                spawnTimer = 2.5f; // nothing to sell yet, give the player a moment
                return;
            }

            SpawnCustomer();
            // Reputation alone drives the door now that there is nothing to buy to speed it up.
            float busyness = Mathf.Clamp01(save.reputation / 100f);
            spawnTimer = Random.Range(Mathf.Lerp(6.5f, 2.4f, busyness), Mathf.Lerp(10.5f, 4.2f, busyness));
        }

        public void SpawnCustomer()
        {
            GameObject customer = new GameObject("Customer_" + (++customerSerial));
            customer.transform.position = CustomerSpawn.position
                + new Vector3(Random.Range(-0.45f, 0.25f), 0f, Random.Range(-0.8f, 0.8f));
            CustomerAgent agent = customer.AddComponent<CustomerAgent>();
            agent.Initialise(customerSerial);
            Customers.Add(agent);
        }

        public void RemoveCustomer(CustomerAgent customer)
        {
            Customers.Remove(customer);
            if (Checkout != null) Checkout.LeaveQueue(customer);
            Destroy(customer.gameObject);
        }

        // ---------------------------------------------------------------- economy

        public void AddMoney(int amount)
        {
            if (amount <= 0) return;
            Money += amount;
            EarnedToday += amount;
            Save();
        }

        /// <summary>Called when the player walks over a money drop on the counter.</summary>
        public void CollectMoney(int amount)
        {
            if (amount <= 0) return;
            Money += amount;
            Sfx.Play(SfxKind.Sale);
            UI.SetNotification("+$" + amount + " collected!", 1.2f);
            Save();
        }

        /// <summary>Expands only the egg table from four fixed sockets to six fixed sockets.</summary>
        public bool TryUpgradeEggTable(ShelfUnit table)
        {
            if (table == null || !table.CanUpgradeEggTable || save.eggTableUpgraded) return false;
            if (Money < GameConfig.EggTableUpgradeCost)
            {
                Sfx.Play(SfxKind.Deny);
                UI.SetNotification("Need $" + GameConfig.EggTableUpgradeCost + " to expand the egg table.", 2f);
                return false;
            }

            Money -= GameConfig.EggTableUpgradeCost;
            save.eggTableUpgraded = true;
            table.UpgradeEggTable();
            Sfx.Play(SfxKind.Sale);
            UI.SetNotification("Egg table upgraded: 6 egg places.", 2.2f);
            Save();
            return true;
        }

        public void CompleteSale(CustomerAgent customer)
        {
            int value = customer.BasketValue;
            if (value <= 0) return;

            int tip = 0;
            if (save.reputation > 70f && Random.value < (save.reputation - 70f) / 55f) tip = Random.Range(1, 4);
            int total = value + tip;

            // Money lands on the counter as a collectible rather than going straight into the balance.
            EarnedToday += total;
            ServedToday++;
            save.lifetimeCustomers++;
            AdjustReputation(1.2f);
            SpawnMoneyDrop(total);
            UI.SetNotification(tip > 0
                ? "$" + value + " + $" + tip + " tip on the counter!"
                : "$" + total + " on the counter!", 1.6f);
            Save();
        }

        /// <summary>
        /// Places a small spinning money model in front of the camera, rendered on a layer the
        /// main camera does not see, with a dedicated camera that draws into the HUD area. This
        /// approach avoids using RawImage which needs a RenderTexture and is heavier than needed.
        ///
        /// Instead, we put the model in world space well above the play area, on the default layer
        /// so the main camera's depth sort still works, and let it spin. The isometric camera at
        /// y = 20 looks down at 55°, so anything above y = 40 is never in view for gameplay but
        /// stays in the frustum. We parent it to the camera so it stays in the corner.
        /// </summary>
        private void BuildHudMoneyIcon()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Transform icon = ModelKit.SpawnProp(cam.transform, ModelKit.MoneyModel,
                TexturedMaterial("MoneyIcon", ModelKit.MoneyTexture), 0.7f, 0, Vector3.zero);
            if (icon == null)
            {
                // Fallback: a yellow sphere.
                GameObject sphere = CreateDecor(PrimitiveType.Sphere, "HUD_Money", cam.transform.position,
                    new Vector3(0.4f, 0.4f, 0.4f), MaterialFor("CoinGold", new Color(1f, 0.84f, 0.12f)), cam.transform);
                sphere.transform.localPosition = new Vector3(-5.8f, 3.8f, 12f);
                icon = sphere.transform;
            }
            else
            {
                icon.localPosition = new Vector3(-5.8f, 3.8f, 12f);
            }
            icon.name = "HUD_Money_Icon";
            icon.gameObject.AddComponent<SpinY>();
        }
        private void SpawnMoneyDrop(int amount)
        {
            if (Checkout == null) return;
            Vector3 spot = Checkout.CounterPosition + new Vector3(Random.Range(-0.6f, 0.6f), 1.5f, Random.Range(-0.3f, 0.3f));
            GameObject root = new GameObject("MoneyDrop_$" + amount);
            root.transform.position = spot;

            Transform model = ModelKit.SpawnProp(root.transform, ModelKit.MoneyModel,
                TexturedMaterial("MoneyIcon", ModelKit.MoneyTexture), 0.28f, 0, Vector3.zero);
            if (model == null)
            {
                // Fallback: a yellow sphere if the model is missing.
                model = CreateDecor(PrimitiveType.Sphere, "Coin", spot, new Vector3(0.22f, 0.22f, 0.22f),
                    MaterialFor("CoinGold", new Color(1f, 0.84f, 0.12f)), root.transform).transform;
                model.localPosition = Vector3.zero;
            }

            MoneyDrop drop = root.AddComponent<MoneyDrop>();
            drop.Initialise(amount, model);
        }

        /// <summary>A shopper gave up. Costs reputation and shows up in the day summary.</summary>
        public void ReportUnhappyCustomer(string message, float reputationCost)
        {
            LostToday++;
            AdjustReputation(-reputationCost);
            Sfx.Play(SfxKind.Unhappy);
            if (!string.IsNullOrEmpty(message)) UI.SetNotification(message, 2.2f);
        }

        public void AdjustReputation(float delta)
        {
            save.reputation = Mathf.Clamp(save.reputation + delta, 0f, 100f);
        }

        public int GetSaleValue(ProductKind kind) => GameConfig.BasePrice(kind);

        // ----------------------------------------------------------------- shelves

        public int TotalStock()
        {
            int total = 0;
            for (int i = 0; i < Shelves.Count; i++) if (Shelves[i] != null) total += Shelves[i].Stock;
            return total;
        }

        /// <summary>A stocked shelf, preferring the requested product so shoppers spread out.</summary>
        public ShelfUnit FindShelf(ProductKind kind)
        {
            ShelfUnit fallback = null;
            for (int i = 0; i < Shelves.Count; i++)
            {
                ShelfUnit shelf = Shelves[i];
                if (shelf == null || shelf.Stock <= 0) continue;
                if (shelf.Product == kind) return shelf;
                if (fallback == null) fallback = shelf;
            }
            return fallback;
        }

        /// <summary>Random stocked shelf, used by shoppers picking something to buy.</summary>
        public ShelfUnit PickStockedShelf()
        {
            int stocked = 0;
            for (int i = 0; i < Shelves.Count; i++) if (Shelves[i] != null && Shelves[i].Stock > 0) stocked++;
            if (stocked == 0) return null;
            int target = Random.Range(0, stocked);
            for (int i = 0; i < Shelves.Count; i++)
            {
                ShelfUnit shelf = Shelves[i];
                if (shelf == null || shelf.Stock <= 0) continue;
                if (target-- == 0) return shelf;
            }
            return null;
        }

        /// <summary>Closest shelf that still has room for the given product, else any matching shelf.</summary>
        public ShelfUnit FindRestockTarget(ProductKind kind, Vector3 from)
        {
            ShelfUnit best = null;
            ShelfUnit anyMatch = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Shelves.Count; i++)
            {
                ShelfUnit shelf = Shelves[i];
                if (shelf == null || shelf.Product != kind) continue;
                if (anyMatch == null) anyMatch = shelf;
                if (shelf.IsFull) continue;
                float distance = (shelf.transform.position - from).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = shelf;
            }
            return best ?? anyMatch;
        }

        /// <summary>Product names of every empty shelf, for the restock hint in the HUD.</summary>
        public string EmptyShelfSummary()
        {
            List<string> names = new List<string>();
            for (int i = 0; i < Shelves.Count; i++)
            {
                ShelfUnit shelf = Shelves[i];
                if (shelf == null || shelf.Stock > 0) continue;
                string label = GameConfig.ProductLabel(shelf.Product);
                if (!names.Contains(label)) names.Add(label);
            }
            return names.Count == 0 ? string.Empty : string.Join(", ", names);
        }

        // -------------------------------------------------------------------- save

        public void Save()
        {
            save.money = Money;
            PlayerPrefs.SetString(GameConfig.SaveKey, JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }

        private void LoadSave()
        {
            string json = null;
            if (PlayerPrefs.HasKey(GameConfig.SaveKey)) json = PlayerPrefs.GetString(GameConfig.SaveKey);
            else if (PlayerPrefs.HasKey(GameConfig.LegacySaveKey)) json = PlayerPrefs.GetString(GameConfig.LegacySaveKey);

            save = string.IsNullOrEmpty(json) ? new StoreSave() : JsonUtility.FromJson<StoreSave>(json);
            if (save == null) save = new StoreSave();
            save.day = Mathf.Max(1, save.day);
            save.reputation = Mathf.Clamp(save.reputation <= 0f ? 80f : save.reputation, 1f, 100f);
            save.version = 3;
            Money = Mathf.Max(0, save.money);
        }

        /// <summary>Wipes progress and rebuilds the scene from scratch.</summary>
        public void ResetSave()
        {
            PlayerPrefs.DeleteKey(GameConfig.SaveKey);
            PlayerPrefs.DeleteKey(GameConfig.LegacySaveKey);
            PlayerPrefs.Save();
            paused = false;
            Time.timeScale = 1f;
            Instance = null;
            if (UI != null) Destroy(UI.gameObject);
            Destroy(gameObject);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
