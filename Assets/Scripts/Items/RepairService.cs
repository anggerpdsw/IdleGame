using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Repair modes for batch repair operations.
    /// </summary>
    public enum RepairMode
    {
        Equipped = 0,           // Only currently equipped items
        InventoryEquipment = 1, // All equipment in inventory (not equipped)
        AllEquipment = 2,       // Both equipped and inventory equipment
        BrokenOnly = 3,         // Only items with 0 durability
        Selected = 4,           // Only specific items provided
    }

    /// <summary>
    /// Repair configuration.
    /// </summary>
    [Serializable]
    public class RepairConfig
    {
        [Tooltip("Base repair cost per durability point")]
        public long BaseRepairCostPerPoint = 10;

        [Tooltip("Global repair cost multiplier (affects all items)")]
        public float RepairCostGrowth = 1.1f;

        [Tooltip("Free repair threshold (0-1). Items above this durability % are free to repair.")]
        [Range(0f, 1f)]
        public float FreeRepairThreshold = 0.9f;

        [Tooltip("Auto-repair enabled")]
        public bool AutoRepairEnabled = false;

        [Tooltip("Auto-repair triggers when durability drops below this %")]
        [Range(0f, 1f)]
        public float AutoRepairThreshold = 0.3f;

        [Tooltip("Auto-repair only in safe zones (town, menu, after wave)")]
        public bool AutoRepairSafeZonesOnly = true;

        [Tooltip("Auto-repair cooldown in seconds")]
        public float AutoRepairCooldown = 5f;

        [Tooltip("Maximum auto-repair cost per trigger")]
        public long AutoRepairMaxCost = 10000;

        [Tooltip("Enable repair kit support")]
        public bool EnableRepairKits = true;

        [Tooltip("Enable gem repair as last resort")]
        public bool EnableGemRepair = true;
    }

    /// <summary>
    /// Repair service facade - orchestrates cost calculation, transactions and auto-repair.
    /// Domain events only (started / itemRepaired / completed / failed).
    /// Durability mutations fire DurabilityService.OnDurabilityChanged; UI re-derives costs.
    /// </summary>
    public sealed class RepairService : MonoBehaviour
    {
        #region Singleton
        private static RepairService _instance;
        public static RepairService Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => _instance = null;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        #endregion

        #region Events
        /// <summary>Fired when repair process starts.</summary>
        public event Action<RepairMode, int> OnRepairStarted;

        /// <summary>Fired when a single item is repaired.</summary>
        public event Action<InventoryItem, int, long, bool> OnItemRepaired;

        /// <summary>Fired when repair batch completes successfully.</summary>
        public event Action<RepairResult> OnRepairCompleted;

        /// <summary>Fired when repair fails (atomic rollback).</summary>
        public event Action<RepairMode, string> OnRepairFailed;
        #endregion

        #region Fields
        private readonly RepairConfig _config = new();
        private RepairCostCalculator _costCalculator;
        private RepairTransactionService _transactionService;
        private AutoRepairService _autoRepairService;
        #endregion

        #region Initialization
        private void Initialize()
        {
            _config.BaseRepairCostPerPoint = 10;
            _config.RepairCostGrowth = 1.1f;
            _config.FreeRepairThreshold = 0.9f;
            _config.AutoRepairEnabled = false;
            _config.AutoRepairThreshold = 0.3f;
            _config.AutoRepairSafeZonesOnly = true;
            _config.AutoRepairCooldown = 5f;
            _config.AutoRepairMaxCost = 10000;
            _config.EnableRepairKits = true;
            _config.EnableGemRepair = true;

            // Initialize repair cost providers
            RepairCostProviderRegistry.Initialize();

            // Create sub-services
            _costCalculator = new RepairCostCalculator(_config);
            _transactionService = new RepairTransactionService(_config, _costCalculator);
            _autoRepairService = new AutoRepairService(_config, _costCalculator, this);

            // Forward transaction events
            _transactionService.OnRepairStarted += (mode, count) => OnRepairStarted?.Invoke(mode, count);
            _transactionService.OnItemRepaired += (item, amount, cost, wasFree) => OnItemRepaired?.Invoke(item, amount, cost, wasFree);
            _transactionService.OnRepairCompleted += result => OnRepairCompleted?.Invoke(result);
            _transactionService.OnRepairFailed += (mode, reason) => OnRepairFailed?.Invoke(mode, reason);

            // Subscribe to durability changes (auto-repair trigger)
            DurabilityService.Instance.OnDurabilityChanged += OnDurabilityServiceChanged;

            // Subscribe to scene changes for safe zone detection
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            _autoRepairService.OnSceneLoaded(scene.name);
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            if (DurabilityService.Instance != null)
                DurabilityService.Instance.OnDurabilityChanged -= OnDurabilityServiceChanged;
        }

        private void OnDurabilityServiceChanged(InventoryItem item, int oldValue, int newValue, DurabilityService.DurabilityChangeReason reason)
        {
            // Check auto-repair on durability damage
            if (reason == DurabilityService.DurabilityChangeReason.CombatDamage && _config.AutoRepairEnabled)
            {
                _autoRepairService.TryAutoRepair(item);
            }
        }
        #endregion

        #region Public API - Batch Repair (Atomic)
        /// <summary>
        /// Repairs a collection of items atomically.
        /// Calculates total cost first, then pays once, then repairs all.
        /// </summary>
        public RepairResult RepairItems(IEnumerable<InventoryItem> items, RepairMode mode = RepairMode.Selected)
            => _transactionService.RepairItems(items, mode);

        /// <summary>
        /// Repairs all items matching the specified mode.
        /// </summary>
        public RepairResult RepairAll(RepairMode mode)
        {
            IEnumerable<InventoryItem> items = mode switch
            {
                RepairMode.Equipped => GetEquippedItems(),
                RepairMode.InventoryEquipment => GetInventoryEquipment(),
                RepairMode.AllEquipment => GetAllEquipment(),
                RepairMode.BrokenOnly => GetBrokenItems(),
                RepairMode.Selected => throw new ArgumentException("Use RepairItems for Selected mode"),
                _ => Enumerable.Empty<InventoryItem>()
            };

            return RepairItems(items, mode);
        }

        /// <summary>
        /// Repairs a single item (convenience method).
        /// </summary>
        public RepairResult RepairItem(InventoryItem item) => _transactionService.RepairItem(item);

        /// <summary>
        /// Repairs an item by a specific amount.
        /// </summary>
        public RepairResult RepairItemByAmount(InventoryItem item, int amount) => _transactionService.RepairItemByAmount(item, amount);
        #endregion

        #region Cost Queries (Derived Data - UI computes on demand)
        public long CalculateRepairCost(InventoryItem item, int durabilityPoints) => _costCalculator.CalculateRepairCost(item, durabilityPoints);
        public long GetTotalRepairCost(IEnumerable<InventoryItem> items) => _costCalculator.GetTotalRepairCost(items);
        public long GetRepairCost(InventoryItem item) => _costCalculator.GetRepairCost(item);
        public bool IsFreeRepair(InventoryItem item) => _costCalculator.IsFreeRepair(item);
        public RepairCostBreakdown GetCostBreakdown(InventoryItem item) => _costCalculator.GetCostBreakdown(item);
        #endregion

        #region Auto-Repair (Event-driven, Safe Zone Aware)
        public void SetAutoRepair(bool enabled) => _autoRepairService.SetEnabled(enabled);
        public void SetSafeZone(bool isSafe) => _autoRepairService.SetSafeZone(isSafe);
        public void CheckAutoRepairAll() => _autoRepairService.CheckAllEquipped();
        #endregion

        #region Item Collection Helpers
        private IEnumerable<InventoryItem> GetEquippedItems()
        {
            var equipment = EquipmentService.Instance;
            return equipment?.EquippedItems.Values ?? Enumerable.Empty<InventoryItem>();
        }

        private IEnumerable<InventoryItem> GetInventoryEquipment()
        {
            var inventory = InventoryService.Instance;
            return inventory?.GetEquipments().Where(i => !i.IsEquipped) ?? Enumerable.Empty<InventoryItem>();
        }

        private IEnumerable<InventoryItem> GetAllEquipment()
        {
            return GetEquippedItems().Concat(GetInventoryEquipment());
        }

        private IEnumerable<InventoryItem> GetBrokenItems()
        {
            return GetAllEquipment().Where(i => i.IsBroken);
        }
        #endregion
    }
}
