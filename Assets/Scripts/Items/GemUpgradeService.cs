using System;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Gem Upgrade Service - handles gem upgrading logic.
    /// </summary>
    public sealed class GemUpgradeService : MonoBehaviour
    {
        #region Singleton
        private static GemUpgradeService _instance;
        public static GemUpgradeService Instance => _instance;

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
        }
        #endregion

        #region Events
        public event Action<string, int, int> OnGemUpgraded; // gemId, fromLevel, toLevel
        #endregion

        #region Public API
        /// <summary>
        /// Upgrades a socketed gem.
        /// </summary>
        public bool UpgradeGem(InventoryItem item, int socketIndex, int targetLevel, GemInstanceData gemInstance)
        {
            if (item == null || socketIndex < 0 || socketIndex >= item.Sockets.Length) return false;

            var socket = item.Sockets[socketIndex];
            if (socket.IsEmpty) return false;

            var gemData = ItemDatabase.Instance?.GetGem(socket.GemId);
            if (gemData == null) return false;

            int currentLevel = socket.GemLevel;
            int maxLevel = gemData.MaxLevel;
            int newLevel = targetLevel > 0 ? Math.Min(targetLevel, maxLevel) : Math.Min(currentLevel + 1, maxLevel);

            if (newLevel <= currentLevel) return false;

            // Calculate cost
            long cost = gemData.GetUpgradeCost(currentLevel, newLevel);
            if (!EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gold, cost, $"Upgrade Gem {socket.GemId} to +{newLevel}"))
                return false;

            // Update level
            socket.GemLevel = newLevel;

            // Update runtime instance
            if (gemInstance != null)
            {
                gemInstance.Level = newLevel;
                gemInstance.Stats = GemStatService.Instance.GenerateGemStats(gemData, newLevel);

                // Re-apply modifiers (idempotent - replaces same ids)
                GemModifierService.Instance.Apply(item, socketIndex, gemInstance);
            }

            OnGemUpgraded?.Invoke(socket.GemId, currentLevel, newLevel);
            EquipmentService.Instance?.ApplyItemStatModifiers(item, item.EquippedSlot, true);

            // Mark item dirty for UI update
            InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Socket | DirtyType.Upgrade);

            return true;
        }

        /// <summary>
        /// Gets the cost to upgrade a gem to target level.
        /// </summary>
        public long GetUpgradeCost(InventoryItem item, int socketIndex, int targetLevel)
        {
            if (item == null || socketIndex < 0 || socketIndex >= item.Sockets.Length) return -1;

            var socket = item.Sockets[socketIndex];
            if (socket.IsEmpty) return -1;

            var gemData = ItemDatabase.Instance?.GetGem(socket.GemId);
            if (gemData == null) return -1;

            return gemData.GetUpgradeCost(socket.GemLevel, targetLevel);
        }

        /// <summary>
        /// Gets the maximum level for a gem.
        /// </summary>
        public int GetMaxLevel(InventoryItem item, int socketIndex)
        {
            if (item == null || socketIndex < 0 || socketIndex >= item.Sockets.Length) return 1;

            var socket = item.Sockets[socketIndex];
            if (socket.IsEmpty) return 1;

            var gemData = ItemDatabase.Instance?.GetGem(socket.GemId);
            return gemData?.MaxLevel ?? 1;
        }

        /// <summary>
        /// Checks if a gem can be upgraded further.
        /// </summary>
        public bool CanUpgrade(InventoryItem item, int socketIndex)
        {
            int maxLevel = GetMaxLevel(item, socketIndex);
            int currentLevel = GetCurrentLevel(item, socketIndex);
            return currentLevel < maxLevel;
        }

        /// <summary>
        /// Gets the current level of a socketed gem.
        /// </summary>
        public int GetCurrentLevel(InventoryItem item, int socketIndex)
        {
            if (item == null || socketIndex < 0 || socketIndex >= item.Sockets.Length) return 1;
            var socket = item.Sockets[socketIndex];
            return socket.IsEmpty ? 1 : socket.GemLevel;
        }
        #endregion
    }
}