using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Items.Random;

namespace IdleDefenseSurvival.Items.Generation
{
    /// <summary>
    /// Data-driven loot generator using loot tables.
    /// Replaces hardcoded drop percentages with configurable weights.
    /// </summary>
    public sealed class LootGenerator
    {
        private readonly IRandomProvider _rng;
        private readonly EquipmentGenerator _equipmentGen;
        private readonly GemGenerator _gemGen;
        private readonly ConsumableGenerator _consumableGen;
        private readonly LootGeneratorConfig _config;

        public LootGenerator(IRandomProvider rng, EquipmentGenerator equipmentGen, GemGenerator gemGen, ConsumableGenerator consumableGen, LootGeneratorConfig config = null)
        {
            _rng = rng;
            _equipmentGen = equipmentGen;
            _gemGen = gemGen;
            _consumableGen = consumableGen;
            _config = config ?? LootGeneratorConfig.Default;
        }

        /// <summary>
        /// Generates loot drops for a tier/wave using the configured loot table.
        /// </summary>
        public InventoryItem[] GenerateLoot(int tier, int wave, int itemCount, float rarityBoost = 0f, int? seed = null)
        {
            var items = new List<InventoryItem>();
            var context = ItemGenerationContext.Drop(tier, wave, rarityBoost, seed);

            for (int i = 0; i < itemCount; i++)
            {
                var lootType = RollLootType(context);
                InventoryItem item = lootType switch
                {
                    LootType.Equipment => GenerateLootEquipment(context),
                    LootType.Gem => GenerateLootGem(context),
                    LootType.Consumable => GenerateLootConsumable(context),
                    LootType.Material => GenerateLootMaterial(context),
                    _ => null
                };

                if (item != null) items.Add(item);
            }

            return items.ToArray();
        }

        private LootType RollLootType(ItemGenerationContext context)
        {
            var table = _config.GetTableForTier(context.Tier);
            float totalWeight = table.Entries.Sum(e => e.Weight);
            float roll = _rng.NextFloat() * totalWeight;
            float accum = 0f;

            foreach (var entry in table.Entries)
            {
                accum += entry.Weight;
                if (roll <= accum) return entry.Type;
            }

            return LootType.Equipment;
        }

        private InventoryItem GenerateLootEquipment(ItemGenerationContext context)
        {
            var types = Enum.GetValues(typeof(EquipmentType)).Cast<EquipmentType>()
                .Where(t => t != EquipmentType.None)
                .ToArray();

            var type = _rng.Choice(types);
            return _equipmentGen.GenerateRandom(type, context.Tier, context.Wave, context.RarityBoost, context.Seed);
        }

        private InventoryItem GenerateLootGem(ItemGenerationContext context)
        {
            var types = Enum.GetValues(typeof(GemType)).Cast<GemType>()
                .Where(t => t != GemType.None)
                .ToArray();

            var type = _rng.Choice(types);
            return _gemGen.GenerateRandom(type, context.Tier, context.Wave, context.RarityBoost, context.Seed);
        }

        private InventoryItem GenerateLootConsumable(ItemGenerationContext context)
        {
            return _consumableGen.GenerateRandom(ItemCategory.Consumable, context.Tier, context.Wave, context.RarityBoost, context.Seed);
        }

        private InventoryItem GenerateLootMaterial(ItemGenerationContext context)
        {
            return _consumableGen.GenerateRandom(ItemCategory.Material, context.Tier, context.Wave, context.RarityBoost, context.Seed);
        }
    }

    /// <summary>
    /// Types of loot that can be generated.
    /// </summary>
    public enum LootType
    {
        Equipment = 0,
        Gem = 1,
        Consumable = 2,
        Material = 3
    }

    /// <summary>
    /// Configuration for loot generation - data driven!
    /// </summary>
    [Serializable]
    public class LootGeneratorConfig
    {
        public LootTable[] Tables;

        public LootTable GetTableForTier(int tier)
        {
            var table = Tables?.FirstOrDefault(t => tier >= t.MinTier && tier <= t.MaxTier);
            return table ?? DefaultTable;
        }

        private static readonly LootTable DefaultTable = new()
        {
            MinTier = 1,
            MaxTier = int.MaxValue,
            Entries = new[]
            {
                new LootTableEntry { Type = LootType.Equipment, Weight = 60f },
                new LootTableEntry { Type = LootType.Gem, Weight = 20f },
                new LootTableEntry { Type = LootType.Consumable, Weight = 10f },
                new LootTableEntry { Type = LootType.Material, Weight = 10f }
            }
        };

        public static LootGeneratorConfig Default => new()
        {
            Tables = new[]
            {
                new LootTable { MinTier = 1, MaxTier = 5, Entries = new[] {
                    new LootTableEntry { Type = LootType.Equipment, Weight = 50f },
                    new LootTableEntry { Type = LootType.Gem, Weight = 15f },
                    new LootTableEntry { Type = LootType.Consumable, Weight = 20f },
                    new LootTableEntry { Type = LootType.Material, Weight = 15f }
                }},
                new LootTable { MinTier = 6, MaxTier = 15, Entries = new[] {
                    new LootTableEntry { Type = LootType.Equipment, Weight = 60f },
                    new LootTableEntry { Type = LootType.Gem, Weight = 20f },
                    new LootTableEntry { Type = LootType.Consumable, Weight = 10f },
                    new LootTableEntry { Type = LootType.Material, Weight = 10f }
                }},
                new LootTable { MinTier = 16, MaxTier = 50, Entries = new[] {
                    new LootTableEntry { Type = LootType.Equipment, Weight = 65f },
                    new LootTableEntry { Type = LootType.Gem, Weight = 20f },
                    new LootTableEntry { Type = LootType.Consumable, Weight = 8f },
                    new LootTableEntry { Type = LootType.Material, Weight = 7f }
                }},
                new LootTable { MinTier = 51, MaxTier = int.MaxValue, Entries = new[] {
                    new LootTableEntry { Type = LootType.Equipment, Weight = 70f },
                    new LootTableEntry { Type = LootType.Gem, Weight = 15f },
                    new LootTableEntry { Type = LootType.Consumable, Weight = 8f },
                    new LootTableEntry { Type = LootType.Material, Weight = 7f }
                }}
            }
        };
    }

    [Serializable]
    public class LootTable
    {
        public int MinTier;
        public int MaxTier;
        public LootTableEntry[] Entries;
    }

    [Serializable]
    public class LootTableEntry
    {
        public LootType Type;
        public float Weight;
    }
}