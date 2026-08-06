using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Enemy;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Loot generator - creates loot drops from enemies, chests, and other sources.
    /// </summary>
    public sealed class LootGenerator : MonoBehaviour
    {
        #region Singleton
        private static LootGenerator _instance;
        public static LootGenerator Instance => _instance;

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
        public event Action<InventoryItem[]> OnLootGenerated;
        public event Action<InventoryItem, Vector3> OnItemDropped; // item, position
        #endregion

        #region Fields
        private readonly Dictionary<string, DropTableData> _dropTables = new();
        private readonly LootConfig _config = new();
        #endregion

        #region Initialization
        private void Initialize()
        {
            _config.BaseDropRate = 1f;
            _config.QualityBoostPerTier = 0.05f;
            _config.QualityBoostPerWave = 0.001f;
            _config.MaxItemsPerDrop = 5;
            _config.GoldDropChance = 1f;
            _config.MinGoldPerDrop = 1;
            _config.MaxGoldPerDrop = 100;

            LoadDropTables();
        }

        private void LoadDropTables()
        {
            // Load from Resources
            var jsonAsset = Resources.Load<TextAsset>("Data/dataDropTables");
            if (jsonAsset != null)
            {
                try
                {
                    var container = JsonConvert.DeserializeObject<DropTableContainer>(jsonAsset.text);
                    if (container?.Tables != null)
                    {
                        foreach (var table in container.Tables)
                        {
                            _dropTables[table.TableId] = table;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LootGenerator] Failed to load drop tables: {e.Message}");
                }
            }

            // Also load from enemy data
            LoadEnemyDropTables();
        }

        private void LoadEnemyDropTables()
        {
            // This would iterate through EnemyData and register their drop tables
        }
        #endregion

        #region Public API - Enemy Drops
        /// <summary>
        /// Generates loot from an enemy death.
        /// </summary>
        public InventoryItem[] GenerateEnemyLoot(EnemyAi enemy, int tier, int wave, float luckBonus = 0f)
        {
            if (enemy == null) return Array.Empty<InventoryItem>();

            var loot = new List<InventoryItem>();

            // Gold drop (guaranteed)
            if (enemy.GoldReward > 0)
            {
                long goldAmount = CalculateGoldDrop(enemy.GoldReward, tier, wave, luckBonus);
                if (goldAmount > 0)
                {
                    var goldItem = CreateCurrencyItem(CurrencyType.Gold, goldAmount);
                    loot.Add(goldItem);
                }
            }

            // Gem drop (with daily limit)
            if (enemy.GemReward > 0 && CanDropGem())
            {
                int gemAmount = CalculateGemDrop((int)enemy.GemReward, tier, wave, luckBonus);
                if (gemAmount > 0)
                {
                    var gemItem = CreateCurrencyItem(CurrencyType.Gem, gemAmount);
                    loot.Add(gemItem);
                }
            }

            // Meat drop
            if (enemy.MeatReward > 0)
            {
                long meatAmount = CalculateMeatDrop(enemy.MeatReward, tier, wave, luckBonus);
                if (meatAmount > 0)
                {
                    var meatItem = CreateCurrencyItem(CurrencyType.Meat, meatAmount);
                    loot.Add(meatItem);
                }
            }

            // Equipment/Item drops from drop table
            if (!string.IsNullOrEmpty(enemy.DropTableId))
            {
                var tableItems = GenerateFromDropTable(enemy.DropTableId, tier, wave, luckBonus);
                loot.AddRange(tableItems);
            }

            // Apply luck bonus to quality
            ApplyLuckToLoot(loot, luckBonus);

            var result = loot.ToArray();
            OnLootGenerated?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Generates loot from a chest.
        /// </summary>
        public InventoryItem[] GenerateChestLoot(string chestId, int tier, int wave, float luckBonus = 0f)
        {
            var loot = new List<InventoryItem>();

            var chestData = ItemDatabase.Instance?.GetItem(chestId);
            if (chestData?.DropTable != null)
            {
                var tableItems = GenerateFromDropTableData(chestData.DropTable, tier, wave, luckBonus);
                loot.AddRange(tableItems);
            }

            // Guaranteed gold
            long gold = CalculateGoldDrop(100, tier, wave, luckBonus);
            loot.Add(CreateCurrencyItem(CurrencyType.Gold, gold));

            var result = loot.ToArray();
            OnLootGenerated?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Generates loot from a drop table.
        /// </summary>
        public InventoryItem[] GenerateFromDropTable(string tableId, int tier, int wave, float luckBonus = 0f)
        {
            if (!_dropTables.TryGetValue(tableId, out var table)) return Array.Empty<InventoryItem>();
            return GenerateFromDropTableData(table, tier, wave, luckBonus);
        }

        /// <summary>
        /// Drops loot items at a world position (spawns physical items).
        /// </summary>
        public void DropLootAtPosition(InventoryItem[] items, Vector3 position)
        {
            if (items == null || items.Length == 0) return;

            foreach (var item in items)
            {
                OnItemDropped?.Invoke(item, position);
            }
        }
        #endregion

        #region Drop Table Generation
        private InventoryItem[] GenerateFromDropTableData(DropTableData table, int tier, int wave, float luckBonus)
        {
            var items = new List<InventoryItem>();

            if (table?.Entries == null || table.Entries.Length == 0) return items.ToArray();

            // Calculate quality modifier
            float qualityMod = 1f + tier * _config.QualityBoostPerTier + wave * _config.QualityBoostPerWave + luckBonus;

            foreach (var entry in table.Entries)
            {
                if (!ShouldDropEntry(entry, qualityMod)) continue;

                int count = UnityEngine.Random.Range(entry.MinCount, entry.MaxCount + 1);
                for (int i = 0; i < count; i++)
                {
                    var item = GenerateEntryItem(entry, tier, wave, qualityMod);
                    if (item != null) items.Add(item);
                }
            }

            // Limit max items
            if (items.Count > _config.MaxItemsPerDrop)
            {
                items = items.OrderByDescending(i => GetItemValue(i)).Take(_config.MaxItemsPerDrop).ToList();
            }

            return items.ToArray();
        }

        private bool ShouldDropEntry(DropEntry entry, float qualityMod)
        {
            if (entry == null) return false;

            float weight = entry.Weight * qualityMod;
            float roll = UnityEngine.Random.Range(0f, 100f); // Assuming weight is percentage-like
            return roll < weight;
        }

        private InventoryItem GenerateEntryItem(DropEntry entry, int tier, int wave, float qualityMod)
        {
            if (entry == null || string.IsNullOrEmpty(entry.ItemId)) return null;

            var itemData = ItemDatabase.Instance?.GetItem(entry.ItemId);
            if (itemData == null) return null;

            // Determine level
            int level = UnityEngine.Random.Range(entry.MinLevel, entry.MaxLevel + 1);

            // Determine rarity
            ItemRarity rarity = DetermineRarity(entry.MinRarity, entry.MaxRarity, qualityMod);

            // Generate based on category
            InventoryItem result = null;

            switch (itemData.Category)
            {
                case ItemCategory.Equipment:
                    result = ItemGenerator.Instance.GenerateEquipmentFromBase(itemData as EquipmentData, rarity, level, tier);
                    break;
                case ItemCategory.Gem:
                    {
                        var gemData = ItemDatabase.Instance?.GetGem(entry.ItemId);
                        result = gemData != null ? ItemGenerator.Instance.GenerateGemFromBase(gemData, rarity, level) : null;
                    }
                    break;
                case ItemCategory.Consumable:
                case ItemCategory.Material:
                case ItemCategory.UpgradeStone:
                case ItemCategory.SkillBook:
                    result = ItemGenerator.Instance.GenerateConsumable(itemData.Category, rarity, UnityEngine.Random.Range(entry.MinCount, entry.MaxCount + 1));
                    break;
                default:
                    result = new InventoryItem
                    {
                        InstanceId = Guid.NewGuid().ToString(),
                        ItemId = entry.ItemId,
                        Quantity = UnityEngine.Random.Range(entry.MinCount, entry.MaxCount + 1),
                        Level = level,
                        AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };
                    break;
            }

            return result;
        }

        private ItemRarity DetermineRarity(ItemRarity minRarity, ItemRarity maxRarity, float qualityMod)
        {
            int min = (int)minRarity;
            int max = (int)maxRarity;

            // Boost max rarity based on quality
            max = Mathf.Min(8, max + Mathf.RoundToInt(qualityMod * 2));

            int roll = UnityEngine.Random.Range(min, max + 1);
            return (ItemRarity)Math.Clamp(roll, min, max);
        }

        private void ApplyLuckToLoot(List<InventoryItem> loot, float luckBonus)
        {
            if (luckBonus <= 0) return;

            foreach (var item in loot)
            {
                // Chance to upgrade rarity
                if (UnityEngine.Random.Range(0f, 1f) < luckBonus * 0.1f)
                {
                    // This would require re-generating the item with higher rarity
                    // For now, just mark as lucky
                    item.CustomData ??= new Dictionary<string, object>();
                    item.CustomData["LuckyDrop"] = true;
                }
            }
        }

        private float GetItemValue(InventoryItem item)
        {
            var itemData = ItemDatabase.Instance?.GetItem(item.ItemId);
            if (itemData == null) return 0f;

            long sellPrice = itemData.SellPrice * item.Quantity;
            return sellPrice * (1f + item.Level * 0.1f) * itemData.ItemRarity.GetDefaultStatMultiplier();
        }
        #endregion

        #region Currency Calculation
        private long CalculateGoldDrop(long baseGold, int tier, int wave, float luckBonus)
        {
            float multiplier = 1f + tier * 0.1f + wave * 0.01f + luckBonus * 0.2f;
            long amount = Mathf.RoundToInt(baseGold * multiplier);
            return Math.Clamp(amount, _config.MinGoldPerDrop, _config.MaxGoldPerDrop * tier);
        }

        private int CalculateGemDrop(int baseGems, int tier, int wave, float luckBonus)
        {
            float multiplier = 1f + tier * 0.05f + wave * 0.005f + luckBonus * 0.3f;
            int amount = Mathf.RoundToInt(baseGems * multiplier);
            return Math.Min(amount, SaveManager.Instance?.GetRemainingDailyGems() ?? 20);
        }

        private long CalculateMeatDrop(long baseMeat, int tier, int wave, float luckBonus)
        {
            float multiplier = 1f + tier * 0.05f + wave * 0.01f + luckBonus * 0.1f;
            return Mathf.RoundToInt(baseMeat * multiplier);
        }

        private bool CanDropGem()
        {
            return SaveManager.Instance?.HasReachedDailyGemLimit() == false;
        }

        private InventoryItem CreateCurrencyItem(CurrencyType type, long amount)
        {
            string itemId = type switch
            {
                CurrencyType.Gold => "Currency_Gold",
                CurrencyType.Gem => "Currency_Gem",
                CurrencyType.Meat => "Currency_Meat",
                _ => "Currency_Gold"
            };

            return new InventoryItem
            {
                InstanceId = Guid.NewGuid().ToString(),
                ItemId = itemId,
                Quantity = (int)Math.Min(amount, int.MaxValue),
                Level = 1,
                AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }
        #endregion

        #region Config
        [Serializable]
        public class LootConfig
        {
            public float BaseDropRate;
            public float QualityBoostPerTier;
            public float QualityBoostPerWave;
            public int MaxItemsPerDrop;
            public float GoldDropChance;
            public long MinGoldPerDrop;
            public long MaxGoldPerDrop;
        }
        #endregion
    }
}