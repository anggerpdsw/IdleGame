using System;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Economy;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Upgrade service - handles equipment leveling only.
    /// </summary>
    public sealed class UpgradeService : MonoBehaviour
    {
        #region Singleton
        private static UpgradeService _instance;
        public static UpgradeService Instance => _instance;

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
        public event Action<InventoryItem, ItemLevelType> OnUpgradeStarted; // item, type - fired BEFORE currency is spent
        public event Action<InventoryItem, ItemLevelType, int, int> OnItemUpgraded; // item, type, fromLevel, toLevel
        public event Action<InventoryItem, ItemLevelType, bool> OnUpgradeAttempted; // item, type, success
        public event Action<InventoryItem, ItemLevelType, UpgradeFailReason> OnUpgradeFailed; // item, type, reason
        #endregion

        #region Fields
        private readonly UpgradeConfig _config = new();
        #endregion

        #region Initialization
        private void Initialize()
        {
            _config.BaseUpgradeCost = 100;
            _config.UpgradeCostGrowth = 1.2f;
        }
        #endregion

        #region Level Upgrade (Basic Level)
        /// <summary>
        /// Upgrades item level (basic leveling).
        /// </summary>
        public bool UpgradeLevel(InventoryItem item, int targetLevel = -1)
        {
            if (!CanUpgradeLevel(item, out string reason)) return false;

            if (ItemDatabase.Instance?.GetEquipment(item.ItemId) is not EquipmentData equipData) return false;

            int currentLevel = item.Level;
            int maxLevel = Math.Min(equipData.MaxLevel, item.MaxLevel);
            int newLevel = targetLevel > 0 ? Math.Min(targetLevel, maxLevel) : Math.Min(currentLevel + 1, maxLevel);

            if (newLevel <= currentLevel)
            {
                OnUpgradeFailed?.Invoke(item, ItemLevelType.Level, UpgradeFailReason.MaxLevel);
                return false;
            }

            // Calculate cost
            long cost = CalculateLevelUpgradeCost(currentLevel, newLevel);
            if (!EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gold, cost, $"Level Up {item.ItemId} to {newLevel}"))
            {
                OnUpgradeFailed?.Invoke(item, ItemLevelType.Level, UpgradeFailReason.NotEnoughGold);
                return false;
            }

            // Apply upgrade
            OnUpgradeStarted?.Invoke(item, ItemLevelType.Level);
            item.Level = newLevel;
            OnItemUpgraded?.Invoke(item, ItemLevelType.Level, currentLevel, newLevel);
            OnUpgradeAttempted?.Invoke(item, ItemLevelType.Level, true);

            // Update socket unlocks (based on Level)
            SocketService.Instance?.UpdateSocketStates(item);
            GemService.Instance?.UpdateSocketUnlocks(item);

            // Refresh equipment modifiers if equipped
            if (item.IsEquipped)
                EquipmentService.Instance?.ApplyItemStatModifiers(item, item.EquippedSlot, true);

            // Mark item dirty for UI update
            InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Upgrade);

            return true;
        }

        public bool CanUpgradeLevel(InventoryItem item, out string reason)
        {
            reason = string.Empty;
            if (item == null) { reason = "Item is null"; return false; }
            if (!item.IsEquippable()) { reason = "Item is not equipment"; return false; }

            if (ItemDatabase.Instance?.GetEquipment(item.ItemId) is not EquipmentData equipData) { reason = "Equipment data not found"; return false; }

            int maxLevel = Math.Min(equipData.MaxLevel, item.MaxLevel);
            if (item.Level >= maxLevel) { reason = "Already at max level"; return false; }

            return true;
        }

        public long GetLevelUpgradeCost(InventoryItem item, int targetLevel = -1)
        {
            if (item == null) return -1;
            int currentLevel = item.Level;
            int newLevel = targetLevel > 0 ? targetLevel : currentLevel + 1;
            return CalculateLevelUpgradeCost(currentLevel, newLevel);
        }

        private long CalculateLevelUpgradeCost(int fromLevel, int toLevel)
        {
            long total = 0;
            for (int lvl = fromLevel; lvl < toLevel; lvl++)
            {
                float cost = _config.BaseUpgradeCost * Mathf.Pow(_config.UpgradeCostGrowth, lvl);
                total += Mathf.RoundToInt(cost);
            }
            return total;
        }
        #endregion

        #region Helper
        private void DestroyItem(InventoryItem item)
        {
            // Remove from inventory
            if (item.IsEquipped)
            {
                EquipmentService.Instance?.Unequip(item.EquippedSlot);
            }
            InventoryService.Instance?.RemoveItem(item.InstanceId, item.Quantity);
        }
        #endregion

        #region Config
        [Serializable]
        public class UpgradeConfig
        {
            public long BaseUpgradeCost;
            public float UpgradeCostGrowth;
        }
        #endregion
    }
}