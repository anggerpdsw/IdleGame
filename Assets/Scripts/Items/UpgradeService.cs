using System;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Economy;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Upgrade service - handles equipment leveling and enhancement only.
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
            _config.EnhanceBaseCost = 500;
            _config.EnhanceCostGrowth = 1.5f;
            _config.EnhanceFailRate = new float[] { 0, 0, 0, 0, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 0.95f, 0.98f };
            _config.EnhanceDestroyRate = new float[] { 0, 0, 0, 0, 0, 0, 0, 0.05f, 0.07f, 0.11f, 0.13f, 0.17f, 0.23f, 0.29f, 0.37f };
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
            int maxLevel = equipData.MaxLevel;
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

            // Update socket unlocks
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

            if (item.Level >= equipData.MaxLevel) { reason = "Already at max level"; return false; }

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

        #region Enhancement (+1 to +15)
        /// <summary>
        /// Enhances equipment (risk-based upgrade with chance to fail/destroy).
        /// </summary>
        public bool Enhance(InventoryItem item, bool useProtectionScroll = false)
        {
            if (!CanEnhance(item, out string reason)) return false;

            int currentEnhance = item.EnhanceLevel;
            int maxEnhance = GetMaxEnhance(item);

            if (currentEnhance >= maxEnhance)
            {
                OnUpgradeFailed?.Invoke(item, ItemLevelType.Enhance, UpgradeFailReason.MaxLevel);
                return false;
            }

            // Calculate costs
            long goldCost = CalculateEnhanceCost(currentEnhance);
            long gemCost = CalculateEnhanceGemCost(currentEnhance);

            // Try spend gold
            if (!EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gold, goldCost, $"Enhance {item.ItemId} to +{currentEnhance + 1}"))
            {
                OnUpgradeFailed?.Invoke(item, ItemLevelType.Enhance, UpgradeFailReason.NotEnoughGold);
                return false;
            }

            OnUpgradeStarted?.Invoke(item, ItemLevelType.Enhance);

            // Try spend gems (optional, increases success rate)
            bool spentGems = false;
            if (gemCost > 0)
            {
                spentGems = EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gem, gemCost, $"Enhance Gem Cost {item.ItemId}");
            }

            // Calculate success rate
            float successRate = 1f - _config.EnhanceFailRate[currentEnhance];
            if (spentGems) successRate = Math.Min(1f, successRate + 0.2f); // Gems boost success
            if (useProtectionScroll) successRate = 1f; // Protection scroll guarantees success

            // Roll
            bool success = UnityEngine.Random.Range(0f, 1f) < successRate;

            if (success)
            {
                item.EnhanceLevel++;
                OnItemUpgraded?.Invoke(item, ItemLevelType.Enhance, currentEnhance, item.EnhanceLevel);
                OnUpgradeAttempted?.Invoke(item, ItemLevelType.Enhance, true);

                // Update socket unlocks
                SocketService.Instance?.UpdateSocketStates(item);
                GemService.Instance?.UpdateSocketUnlocks(item);

                // Refresh equipment modifiers
                if (item.IsEquipped)
                    EquipmentService.Instance?.ApplyItemStatModifiers(item, item.EquippedSlot, true);

                // Mark item dirty for UI update
                InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Upgrade | DirtyType.Socket);
            }
            else
            {
                // Check for destruction
                float destroyRate = _config.EnhanceDestroyRate[currentEnhance];
                if (UnityEngine.Random.Range(0f, 1f) < destroyRate && !useProtectionScroll)
                {
                    // Item destroyed!
                    DestroyItem(item);
                    OnUpgradeFailed?.Invoke(item, ItemLevelType.Enhance, UpgradeFailReason.Destroyed);
                    return false;
                }
                // Failed but not destroyed - enhance level drops (or stays same based on config)
                // For now, just fail without penalty beyond cost
                OnUpgradeFailed?.Invoke(item, ItemLevelType.Enhance, UpgradeFailReason.RNGFailed);
            }

            return success;
        }

        public bool CanEnhance(InventoryItem item, out string reason)
        {
            reason = string.Empty;
            if (item == null) { reason = "Item is null"; return false; }
            if (!item.IsEquippable()) { reason = "Item is not equipment"; return false; }

            if (ItemDatabase.Instance?.GetEquipment(item.ItemId) is not EquipmentData equipData) { reason = "Equipment data not found"; return false; }

            int maxEnhance = GetMaxEnhance(item);
            if (item.EnhanceLevel >= maxEnhance) { reason = $"Already at max enhance (+{maxEnhance})"; return false; }

            return true;
        }

        public long GetEnhanceCost(InventoryItem item) => CalculateEnhanceCost(item.EnhanceLevel);
        public long GetEnhanceGemCost(InventoryItem item) => CalculateEnhanceGemCost(item.EnhanceLevel);
        public float GetEnhanceSuccessRate(InventoryItem item)
        {
            if (item == null) return 0f;
            int enhance = item.EnhanceLevel;
            if (enhance < _config.EnhanceFailRate.Length)
                return 1f - _config.EnhanceFailRate[enhance];
            return 0.01f; // Very low at max
        }

        private long CalculateEnhanceCost(int currentEnhance)
        {
            float cost = _config.EnhanceBaseCost * Mathf.Pow(_config.EnhanceCostGrowth, currentEnhance);
            return Mathf.RoundToInt(cost);
        }

        private long CalculateEnhanceGemCost(int currentEnhance)
        {
            // Gem cost starts at +10
            if (currentEnhance < 10) return 0;
            return (currentEnhance - 9) * 10; // 10, 20, 30... gems per level
        }

        private int GetMaxEnhance(InventoryItem item)
        {
            var equipData = ItemDatabase.Instance?.GetEquipment(item.ItemId) as EquipmentData;
            return equipData?.MaxLevel ?? 15; // Cap at +15
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

            public long EnhanceBaseCost;
            public float EnhanceCostGrowth;
            public float[] EnhanceFailRate;
            public float[] EnhanceDestroyRate;
        }
        #endregion
    }
}