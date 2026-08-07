using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Gem Stat Service - handles stat calculation from gems.
    /// </summary>
    public sealed class GemStatService : MonoBehaviour
    {
        #region Singleton
        private static GemStatService _instance;
        public static GemStatService Instance => _instance;

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
        /// Gets stat bonuses from all socketed gems on an item.
        /// </summary>
        public Dictionary<SecondaryStat, float> GetItemGemBonuses(InventoryItem item)
        {
            var bonuses = new Dictionary<SecondaryStat, float>();

            if (item?.Sockets == null) return bonuses;

            foreach (var socket in item.Sockets)
            {
                if (socket.IsEmpty) continue;
                var stats = GetGemStats(socket.GemId, socket.GemLevel);
                foreach (var statEntry in stats)
                {
                    float value = statEntry.GetValue(socket.GemLevel, 0);
                    bonuses[statEntry.Stat] = bonuses.TryGetValue(statEntry.Stat, out var current) ? current + value : value;
                }
            }

            return bonuses;
        }

        /// <summary>
        /// Gets stat bonuses from a specific socketed gem.
        /// </summary>
        public CombatStatEntry[] GetGemStats(string gemId, int level)
        {
            var gemData = ItemDatabase.Instance?.GetGem(gemId);
            if (gemData == null) return Array.Empty<CombatStatEntry>();

            return GenerateGemStats(gemData, level);
        }

        /// <summary>
        /// Generates gem stats for a given gem data and level.
        /// </summary>
        public CombatStatEntry[] GenerateGemStats(GemData gemData, int level)
        {
            var stats = new List<CombatStatEntry>();

            // Base stats (guaranteed)
            if (gemData.BaseStats != null)
            {
                foreach (var stat in gemData.BaseStats)
                {
                    stats.Add(new CombatStatEntry
                    {
                        Stat = stat.Stat,
                        BaseValue = stat.BaseValue,
                        ValuePerLevel = stat.ValuePerLevel,
                        Mode = stat.Mode
                    });
                }
            }

            return stats.ToArray();
        }

        /// <summary>
        /// Gets the total stat value for a specific stat from all socketed gems.
        /// </summary>
        public float GetTotalStatValue(InventoryItem item, SecondaryStat stat)
        {
            if (item?.Sockets == null) return 0f;

            return GetItemGemBonuses(item).TryGetValue(stat, out var total) ? total : 0f;
        }
        #endregion
    }
}