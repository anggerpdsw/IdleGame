using System;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Economy;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Upgrade service - handles equipment leveling, enhancement, limit break, refinement, awakening, transcendence, evolution, and masterwork.
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
        public event Action<InventoryItem> OnItemMaxedOut; // item (all upgrades maxed)
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
            _config.EnhanceDestroyRate = new float[] { 0, 0, 0, 0, 0, 0, 0, 0.05f, 0.1f, 0.15f, 0.2f, 0.25f, 0.3f, 0.35f, 0.4f };
            _config.LimitBreakCost = 10000;
            _config.LimitBreakGemCost = 100;
            _config.RefineBaseCost = 5000;
            _config.RefineCostGrowth = 1.3f;
            _config.AwakenCost = 50000;
            _config.AwakenGemCost = 500;
            _config.TranscendCost = 100000;
            _config.TranscendGemCost = 1000;
            _config.EvolutionCost = 500000;
            _config.EvolutionGemCost = 5000;
            _config.MasterworkCost = 1000000;
            _config.MasterworkGemCost = 10000;
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

            // Check if maxed
            if (item.Level >= maxLevel && IsFullyMaxed(item))
                OnItemMaxedOut?.Invoke(item);

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

        #region Enhancement (+1 to +20)
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
            return equipData?.MaxLevel ?? 20; // Using MaxLevel as max enhance for now
        }
        #endregion

        #region Limit Break
        public bool LimitBreak(InventoryItem item)
        {
            if (!CanLimitBreak(item, out string reason)) return false;

            long goldCost = _config.LimitBreakCost;
            long gemCost = _config.LimitBreakGemCost;

            if (!EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gold, goldCost, $"Limit Break {item.ItemId}"))
            {
                OnUpgradeFailed?.Invoke(item, ItemLevelType.LimitBreak, UpgradeFailReason.NotEnoughGold);
                return false;
            }

            if (gemCost > 0 && !EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gem, gemCost, $"Limit Break Gem {item.ItemId}"))
            {
                OnUpgradeFailed?.Invoke(item, ItemLevelType.LimitBreak, UpgradeFailReason.NotEnoughGem);
                return false;
            }

            OnUpgradeStarted?.Invoke(item, ItemLevelType.LimitBreak);

            item.LimitBreakCount++;
            OnItemUpgraded?.Invoke(item, ItemLevelType.LimitBreak, item.LimitBreakCount - 1, item.LimitBreakCount);
            OnUpgradeAttempted?.Invoke(item, ItemLevelType.LimitBreak, true);

            // Limit break increases max level
            if (ItemDatabase.Instance?.GetEquipment(item.ItemId) is EquipmentData equipData)
            {
                // Max level increases by 10 per limit break (configurable)
            }

            if (item.IsEquipped)
                EquipmentService.Instance?.ApplyItemStatModifiers(item, item.EquippedSlot, true);

            // Mark item dirty for UI update
            InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Upgrade);

            return true;
        }

        public bool CanLimitBreak(InventoryItem item, out string reason)
        {
            reason = string.Empty;
            if (item == null) { reason = "Item is null"; return false; }
            if (!item.IsEquippable()) { reason = "Item is not equipment"; return false; }

            int maxLimitBreak = GetMaxLimitBreak(item);
            if (item.LimitBreakCount >= maxLimitBreak) { reason = $"Already at max limit break ({maxLimitBreak})"; return false; }

            // Requires max level
            if (ItemDatabase.Instance?.GetEquipment(item.ItemId) is EquipmentData equipData && item.Level < equipData.MaxLevel) { reason = "Must be at max level"; return false; }

            return true;
        }

        public long GetLimitBreakGoldCost(InventoryItem item) => _config.LimitBreakCost;
        public long GetLimitBreakGemCost(InventoryItem item) => _config.LimitBreakGemCost;
        private int GetMaxLimitBreak(InventoryItem item) => 5; // Configurable
        #endregion

        #region Refine
        public bool Refine(InventoryItem item)
        {
            if (!CanRefine(item, out string reason)) return false;

            long cost = CalculateRefineCost(item.RefineLevel);
            if (!EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gold, cost, $"Refine {item.ItemId}"))
            {
                OnUpgradeFailed?.Invoke(item, ItemLevelType.Refine, UpgradeFailReason.NotEnoughGold);
                return false;
            }

            OnUpgradeStarted?.Invoke(item, ItemLevelType.Refine);

            item.RefineLevel++;
            OnItemUpgraded?.Invoke(item, ItemLevelType.Refine, item.RefineLevel - 1, item.RefineLevel);
            OnUpgradeAttempted?.Invoke(item, ItemLevelType.Refine, true);

            if (item.IsEquipped)
                EquipmentService.Instance?.ApplyItemStatModifiers(item, item.EquippedSlot, true);

            // Mark item dirty for UI update
            InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Upgrade);

            return true;
        }

        public bool CanRefine(InventoryItem item, out string reason)
        {
            reason = string.Empty;
            if (item == null) { reason = "Item is null"; return false; }
            if (!item.IsEquippable()) { reason = "Item is not equipment"; return false; }

            int maxRefine = 10; // Configurable
            if (item.RefineLevel >= maxRefine) { reason = $"Already at max refine ({maxRefine})"; return false; }

            return true;
        }

        public long GetRefineCost(InventoryItem item) => CalculateRefineCost(item.RefineLevel);
        private long CalculateRefineCost(int currentRefine)
        {
            float cost = _config.RefineBaseCost * Mathf.Pow(_config.RefineCostGrowth, currentRefine);
            return Mathf.RoundToInt(cost);
        }
        #endregion

        #region Awaken
        public bool Awaken(InventoryItem item)
        {
            if (!CanAwaken(item, out string reason)) return false;

            long goldCost = _config.AwakenCost;
            long gemCost = _config.AwakenGemCost;

            if (!EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gold, goldCost, $"Awaken {item.ItemId}"))
            {
                OnUpgradeFailed?.Invoke(item, ItemLevelType.Awaken, UpgradeFailReason.NotEnoughGold);
                return false;
            }

            if (gemCost > 0 && !EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gem, gemCost, $"Awaken Gem {item.ItemId}"))
            {
                OnUpgradeFailed?.Invoke(item, ItemLevelType.Awaken, UpgradeFailReason.NotEnoughGem);
                return false;
            }

            OnUpgradeStarted?.Invoke(item, ItemLevelType.Awaken);

            item.IsAwakened = true;
            OnItemUpgraded?.Invoke(item, ItemLevelType.Awaken, 0, 1);
            OnUpgradeAttempted?.Invoke(item, ItemLevelType.Awaken, true);

            if (item.IsEquipped)
                EquipmentService.Instance?.ApplyItemStatModifiers(item, item.EquippedSlot, true);

            // Mark item dirty for UI update
            InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Upgrade);

            return true;
        }

        public bool CanAwaken(InventoryItem item, out string reason)
        {
            reason = string.Empty;
            if (item == null) { reason = "Item is null"; return false; }
            if (!item.IsEquippable()) { reason = "Item is not equipment"; return false; }
            if (item.IsAwakened) { reason = "Already awakened"; return false; }

            // Requires max level, max enhance, and at least 1 limit break
            if (ItemDatabase.Instance?.GetEquipment(item.ItemId) is EquipmentData equipData && item.Level < equipData.MaxLevel) { reason = "Must be at max level"; return false; }
            if (item.EnhanceLevel < GetMaxEnhance(item)) { reason = "Must be at max enhance"; return false; }
            if (item.LimitBreakCount < 1) { reason = "Requires at least 1 limit break"; return false; }

            return true;
        }

        public long GetAwakenGoldCost(InventoryItem item) => _config.AwakenCost;
        public long GetAwakenGemCost(InventoryItem item) => _config.AwakenGemCost;
        #endregion

        #region Transcend
        public bool Transcend(InventoryItem item)
        {
            if (!CanTranscend(item, out string reason)) return false;

            long goldCost = _config.TranscendCost;
            long gemCost = _config.TranscendGemCost;

            if (!EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gold, goldCost, $"Transcend {item.ItemId}"))
            {
                OnUpgradeFailed?.Invoke(item, ItemLevelType.Transcend, UpgradeFailReason.NotEnoughGold);
                return false;
            }

            if (gemCost > 0 && !EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gem, gemCost, $"Transcend Gem {item.ItemId}"))
            {
                OnUpgradeFailed?.Invoke(item, ItemLevelType.Transcend, UpgradeFailReason.NotEnoughGem);
                return false;
            }

            OnUpgradeStarted?.Invoke(item, ItemLevelType.Transcend);

            item.TranscendLevel++;
            OnItemUpgraded?.Invoke(item, ItemLevelType.Transcend, item.TranscendLevel - 1, item.TranscendLevel);
            OnUpgradeAttempted?.Invoke(item, ItemLevelType.Transcend, true);

            if (item.IsEquipped)
                EquipmentService.Instance?.ApplyItemStatModifiers(item, item.EquippedSlot, true);

            // Mark item dirty for UI update
            InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Upgrade);

            return true;
        }

        public bool CanTranscend(InventoryItem item, out string reason)
        {
            reason = string.Empty;
            if (item == null) { reason = "Item is null"; return false; }
            if (!item.IsEquippable()) { reason = "Item is not equipment"; return false; }
            if (!item.IsAwakened) { reason = "Must be awakened first"; return false; }

            int maxTranscend = 3;
            if (item.TranscendLevel >= maxTranscend) { reason = $"Already at max transcend ({maxTranscend})"; return false; }

            return true;
        }

        public long GetTranscendGoldCost(InventoryItem item) => _config.TranscendCost;
        public long GetTranscendGemCost(InventoryItem item) => _config.TranscendGemCost;
        #endregion

        #region Evolution
        public bool Evolve(InventoryItem item)
        {
            if (!CanEvolve(item, out string reason)) return false;

            long goldCost = _config.EvolutionCost;
            long gemCost = _config.EvolutionGemCost;

            if (!EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gold, goldCost, $"Evolve {item.ItemId}"))
            {
                OnUpgradeFailed?.Invoke(item, ItemLevelType.Evolution, UpgradeFailReason.NotEnoughGold);
                return false;
            }

            if (gemCost > 0 && !EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gem, gemCost, $"Evolve Gem {item.ItemId}"))
            {
                OnUpgradeFailed?.Invoke(item, ItemLevelType.Evolution, UpgradeFailReason.NotEnoughGem);
                return false;
            }

            OnUpgradeStarted?.Invoke(item, ItemLevelType.Evolution);

            item.EvolutionStage++;
            OnItemUpgraded?.Invoke(item, ItemLevelType.Evolution, item.EvolutionStage - 1, item.EvolutionStage);
            OnUpgradeAttempted?.Invoke(item, ItemLevelType.Evolution, true);

            // Evolution changes item appearance and base stats significantly
            // This would swap to a new EquipmentData variant

            if (item.IsEquipped)
                EquipmentService.Instance?.ApplyItemStatModifiers(item, item.EquippedSlot, true);

            // Mark item dirty for UI update
            InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Upgrade);

            return true;
        }

        public bool CanEvolve(InventoryItem item, out string reason)
        {
            reason = string.Empty;
            if (item == null) { reason = "Item is null"; return false; }
            if (!item.IsEquippable()) { reason = "Item is not equipment"; return false; }
            if (!item.IsAwakened) { reason = "Must be awakened first"; return false; }
            if (item.TranscendLevel < 3) { reason = "Must be max transcend"; return false; }

            int maxEvolution = 4;
            if (item.EvolutionStage >= maxEvolution) { reason = $"Already at max evolution ({maxEvolution})"; return false; }

            return true;
        }

        public long GetEvolutionGoldCost(InventoryItem item) => _config.EvolutionCost;
        public long GetEvolutionGemCost(InventoryItem item) => _config.EvolutionGemCost;
        #endregion

        #region Masterwork
        public bool Masterwork(InventoryItem item)
        {
            if (!CanMasterwork(item, out string reason)) return false;

            long goldCost = _config.MasterworkCost;
            long gemCost = _config.MasterworkGemCost;

            if (!EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gold, goldCost, $"Masterwork {item.ItemId}"))
            {
                OnUpgradeFailed?.Invoke(item, ItemLevelType.Masterwork, UpgradeFailReason.NotEnoughGold);
                return false;
            }

            if (gemCost > 0 && !EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gem, gemCost, $"Masterwork Gem {item.ItemId}"))
            {
                OnUpgradeFailed?.Invoke(item, ItemLevelType.Masterwork, UpgradeFailReason.NotEnoughGem);
                return false;
            }

            OnUpgradeStarted?.Invoke(item, ItemLevelType.Masterwork);

            item.IsMasterwork = true;
            OnItemUpgraded?.Invoke(item, ItemLevelType.Masterwork, 0, 1);
            OnUpgradeAttempted?.Invoke(item, ItemLevelType.Masterwork, true);

            if (item.IsEquipped)
                EquipmentService.Instance?.ApplyItemStatModifiers(item, item.EquippedSlot, true);

            // Mark item dirty for UI update
            InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Upgrade);

            OnItemMaxedOut?.Invoke(item);
            return true;
        }

        public bool CanMasterwork(InventoryItem item, out string reason)
        {
            reason = string.Empty;
            if (item == null) { reason = "Item is null"; return false; }
            if (!item.IsEquippable()) { reason = "Item is not equipment"; return false; }
            if (item.IsMasterwork) { reason = "Already masterwork"; return false; }

            // Requires everything maxed
            if (!item.IsAwakened) { reason = "Must be awakened"; return false; }
            if (item.TranscendLevel < 3) { reason = "Must be max transcend"; return false; }
            if (item.EvolutionStage < 4) { reason = "Must be max evolution"; return false; }
            if (item.RefineLevel < 10) { reason = "Must be max refine"; return false; }
            if (item.LimitBreakCount < 5) { reason = "Must be max limit break"; return false; }
            if (item.EnhanceLevel < GetMaxEnhance(item)) { reason = "Must be max enhance"; return false; }

            if (ItemDatabase.Instance?.GetEquipment(item.ItemId) is EquipmentData equipData && item.Level < equipData.MaxLevel) { reason = "Must be max level"; return false; }

            return true;
        }

        public long GetMasterworkGoldCost(InventoryItem item) => _config.MasterworkCost;
        public long GetMasterworkGemCost(InventoryItem item) => _config.MasterworkGemCost;
        #endregion

        #region Helper
        private bool IsFullyMaxed(InventoryItem item)
        {
            return item.Level >= GetMaxLevel(item) &&
                   item.EnhanceLevel >= GetMaxEnhance(item) &&
                   item.LimitBreakCount >= GetMaxLimitBreak(item) &&
                   item.RefineLevel >= 10 &&
                   item.IsAwakened &&
                   item.TranscendLevel >= 3 &&
                   item.EvolutionStage >= 4 &&
                   item.IsMasterwork;
        }

        private int GetMaxLevel(InventoryItem item)
        {
            var equipData = ItemDatabase.Instance?.GetEquipment(item.ItemId) as EquipmentData;
            return equipData?.MaxLevel ?? 100;
        }

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

            public long LimitBreakCost;
            public long LimitBreakGemCost;

            public long RefineBaseCost;
            public float RefineCostGrowth;

            public long AwakenCost;
            public long AwakenGemCost;

            public long TranscendCost;
            public long TranscendGemCost;

            public long EvolutionCost;
            public long EvolutionGemCost;

            public long MasterworkCost;
            public long MasterworkGemCost;
        }
        #endregion
    }
}