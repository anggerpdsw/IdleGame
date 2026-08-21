using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Items.Random;
using Newtonsoft.Json;
using UnityEngine;

namespace IdleDefenseSurvival.Items.Data
{
    
    /// <summary>
    /// Base equipment configuration loaded from dataBaseEquipment.json.
    /// Single source of truth for all equipment rarity-based base stats.
    /// Index 0 = Common, 1 = Rare, 2 = Epic, 3 = Legendary, 4 = Mythic, 5 = Divine.
    /// </summary>
    [Serializable]
    public sealed class EquipmentBaseData
    {
        public string Id = "equip_base";
        public string Name = "Base Equipment";
        public string Description = "Base template for all equipment. Used by crafting system to generate specific rarity equipment.";
        public ItemCategory Category = ItemCategory.Equipment;
        public int SellPrice = 20;
        public int StackSize = 1;
        public int BaseLevel = 1;
        // Rarity-indexed arrays:
        // 0 = Common
        // 1 = Rare
        // 2 = Epic
        // 3 = Legendary
        // 4 = Mythic
        // 5 = Divine
        public int[] MaxLevel = new int[] {10, 15, 20, 25, 30, 50};

        // Durability ranges (min/max per rarity)
        public int[] Durability = new int[] {100, 200, 300, 400, 500, 1000};
        public int[] DurabilityLossPerUse = new int[] {2, 4, 6, 10, 14, 20};
        public int[] RepairCostPerDurability = new int[] {10, 50, 100, 1000, 5000, 10000};
        public int[] Sockets = new int[] {1, 2, 3, 4, 5, 6};

        /// <summary>
        /// Validates that all rarity arrays contain exactly GameConstants.RARITY_COUNT elements.
        /// </summary>
        public bool Validate()
        {
            return MaxLevel?.Length == GameConstants.RARITY_COUNT
                && Durability?.Length == GameConstants.RARITY_COUNT
                && DurabilityLossPerUse?.Length == GameConstants.RARITY_COUNT
                && RepairCostPerDurability?.Length == GameConstants.RARITY_COUNT
                && Sockets?.Length == GameConstants.RARITY_COUNT;
        }

        /// <summary>
        /// Gets rarity-specific configuration with random value as a strongly typed struct.
        /// </summary>
        public EquipmentRarityConfig GetRarityConfig(Rarity rarity)
        {
            int index = GetRarityIndex(rarity);
            return new EquipmentRarityConfig
            {
                MaxLevel = GetMaxLevel(index),
                Durability = GetDurability(index),
                DurabilityLossPerUse = GetDurabilityLossPerUse(index),
                RepairCostPerDurability = GetRepairCostPerDurability(index),
                Sockets = GetSockets(index),
            };
        }

        private static int GetRarityIndex(Rarity rarity)
        {
            int index = (int)rarity - 1;
            if (index < 0) return 0;
            if (index >= GameConstants.RARITY_COUNT) return GameConstants.RARITY_COUNT - 1;
            return index;
        }

        private int GetMaxLevel(int index)
        {
            IRandomProvider _rng = new UnityRandomProvider();
            int minLevel = 1;
            if (index > 0) minLevel = MaxLevel[index - 1];
            return _rng.NextInt(minLevel, MaxLevel[index]);
        }
        private int GetDurability(int index)
        {
            IRandomProvider _rng = new UnityRandomProvider();
            int minDurability = Durability[index] / 2;
            return _rng.NextInt(minDurability, Durability[index] + 1);
        }
        private int GetDurabilityLossPerUse(int index)
        {
            IRandomProvider _rng = new UnityRandomProvider();
            int minLossPerUse = DurabilityLossPerUse[index] / 2;
            return _rng.NextInt(minLossPerUse, DurabilityLossPerUse[index] + 1);
        }
        private int GetRepairCostPerDurability(int index)
        {
            IRandomProvider _rng = new UnityRandomProvider();
            int minRepairCost = RepairCostPerDurability[index] / 2;
            return _rng.NextInt(minRepairCost, RepairCostPerDurability[index] + 1);
        }
        private int GetSockets(int index)
        {
            IRandomProvider _rng = new UnityRandomProvider();
            int minSocket = 0;
            if (index > 1) minSocket = Sockets[index - 2];
            return _rng.NextInt(minSocket, Sockets[index] + 1);
        }

    }

    /// <summary>
    /// Strongly typed rarity-specific equipment configuration with min/max ranges.
    /// Prevents array-index handling from being scattered throughout the codebase.
    /// </summary>
    [Serializable]
    public struct EquipmentRarityConfig
    {
        public int MaxLevel;
        // Durability range
        public int Durability;
        // Durability loss per use range
        public int DurabilityLossPerUse;
        // Repair cost per durability range
        public int RepairCostPerDurability;
        // Socket range
        public int Sockets;

        public bool IsValid =>
            MaxLevel > 0 &&
            Durability > 0 &&
            DurabilityLossPerUse >= 0 &&
            RepairCostPerDurability >= 0 &&
            Sockets >= 0;
    }

    /// <summary>
    /// Repository responsible for loading and providing EquipmentBaseData.
    /// </summary>
    public static class EquipmentBaseDataRepository
    {
        private const string RESOURCE_PATH = "Data/Crafting/Equipment/dataBaseEquipment";
        private static EquipmentBaseData _instance;
        private static bool _initialized;

        /// <summary>
        /// Gets the loaded equipment base configuration.
        /// Automatically loads the JSON on first access.
        /// </summary>
        public static EquipmentBaseData Instance
        {
            get
            {
                if (!_initialized) Load();
                return _instance;
            }
        }

        /// <summary>
        /// Loads equipment base configuration from JSON.
        /// </summary>
        public static void Load()
        {
            if (_initialized) return;
            var asset = Resources.Load<TextAsset>(RESOURCE_PATH);
            if (asset == null)
            {
                Debug.LogError(
                    $"[EquipmentBaseDataRepository] " +
                    $"'{RESOURCE_PATH}.json' not found in Resources. " +
                    $"Using hardcoded defaults."
                );
                _instance = CreateDefault();
                _initialized = true;
                return;
            }

            try
            {
                var items = JsonConvert.DeserializeObject<List<EquipmentBaseData>>(
                    asset.text, CreateJsonSettings());
                if (items == null || items.Count == 0)
                {
                    Debug.LogError(
                        "[EquipmentBaseDataRepository] " +
                        "JSON contains no EquipmentBaseData. Using defaults."
                    );
                    _instance = CreateDefault();
                }
                else
                {
                    var data = items[0];
                    if (data == null || !data.Validate())
                    {
                        Debug.LogError(
                            "[EquipmentBaseDataRepository] " +
                            "Invalid EquipmentBaseData. " +
                            $"All rarity arrays must contain exactly {GameConstants.RARITY_COUNT} elements " +
                            "(Common..Divine). Using defaults."
                        );
                        _instance = CreateDefault();
                    }
                    else
                    {
                        _instance = data;
                    }
                }
            }
            catch (JsonException e)
            {
                Debug.LogError(
                    $"[EquipmentBaseDataRepository] " +
                    $"JSON deserialization failed: {e.Message}. " +
                    $"Using defaults."
                );
                _instance = CreateDefault();
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[EquipmentBaseDataRepository] " +
                    $"Failed to load equipment data: {e.Message}. " +
                    $"Using defaults."
                );

                _instance = CreateDefault();
            }

            _initialized = true;
        }

        private static JsonSerializerSettings CreateJsonSettings()
        {
            return new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            };
        }

        /// <summary>
        /// Resets the repository so the JSON can be loaded again.
        /// Useful for development/testing.
        /// </summary>
        public static void Reload()
        {
            _initialized = false;
            _instance = null;
            Load();
        }

        private static EquipmentBaseData CreateDefault()
        {
            return new EquipmentBaseData
            {
                Id = "equip_base",
                Name = "Base Equipment",
                Description = "Base template for all equipment. Used by crafting system to generate specific rarity equipment.",
                Category = ItemCategory.Equipment,
                SellPrice = 20,
                StackSize = 1,
                BaseLevel = 1,
                MaxLevel = new int[] {10, 15, 20, 25, 30, 50},
                Durability = new int[] {100, 200, 300, 400, 500, 1000},
                DurabilityLossPerUse = new int[] {2, 4, 6, 10, 14, 20},
                RepairCostPerDurability = new int[] {10, 50, 100, 1000, 5000, 10000},
                Sockets = new int[] {1, 2, 3, 4, 5, 6}
            };
        }
    }
}