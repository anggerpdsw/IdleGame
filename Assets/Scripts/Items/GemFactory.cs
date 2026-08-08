using System;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Gem Factory - handles gem instance creation.
    /// </summary>
    public sealed class GemFactory : MonoBehaviour
    {
        #region Singleton
        private static GemFactory _instance;
        public static GemFactory Instance => _instance;

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

        #region Public API
        /// <summary>
        /// Creates a new gem instance for socketing.
        /// </summary>
        public GemInstanceData CreateGemInstance(string gemId, int level)
        {
            var gemData = ItemDatabase.Instance?.GetGem(gemId);
            if (gemData == null) return null;

            return new GemInstanceData
            {
                InstanceId = Guid.NewGuid().ToString(),
                GemId = gemId,
                Level = level,
                Experience = 0,
                Stats = GemStatService.Instance.GenerateGemStats(gemData, level),
                AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        /// <summary>
        /// Creates an InventoryItem for a gem (for inventory storage).
        /// </summary>
        public InventoryItem CreateGemItem(string gemId, int level)
        {
            return new InventoryItem
            {
                ItemId = gemId,
                Quantity = 1,
                Level = level,
                AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        /// <summary>
        /// Creates a gem instance from an existing InventoryItem (when socketing).
        /// </summary>
        public GemInstanceData CreateGemInstanceFromItem(InventoryItem gemItem)
        {
            if (gemItem == null || !gemItem.IsGem()) return null;
            return CreateGemInstance(gemItem.ItemId, gemItem.Level);
        }

        /// <summary>
        /// Creates a gem instance from socket data (when loading save).
        /// </summary>
        public GemInstanceData CreateGemInstanceFromSocket(string gemId, int level, int experience = 0)
        {
            var gemData = ItemDatabase.Instance?.GetGem(gemId);
            if (gemData == null) return null;

            return new GemInstanceData
            {
                InstanceId = Guid.NewGuid().ToString(),
                GemId = gemId,
                Level = level,
                Experience = experience,
                Stats = GemStatService.Instance.GenerateGemStats(gemData, level),
                AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }
        #endregion
    }
}