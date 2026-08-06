using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Items.Random;

namespace IdleDefenseSurvival.Items.Generation
{
    /// <summary>
    /// Generator for equipment enchantments.
    /// </summary>
    public sealed class EnchantmentGenerator
    {
        private readonly IRandomProvider _rng;
        private readonly EnchantmentGeneratorConfig _config;

        public EnchantmentGenerator(IRandomProvider rng, EnchantmentGeneratorConfig config = null)
        {
            _rng = rng ?? new UnityRandomProvider();
            _config = config ?? EnchantmentGeneratorConfig.Default;
        }

        /// <summary>
        /// Attempts to generate an enchantment for equipment.
        /// Returns null if no enchantment is generated.
        /// </summary>
        public EnchantmentInstanceData GenerateEnchantment(EquipmentData baseEquipment, ItemRarity rarity, int level, ItemGenerationContext context)
        {
            // Check chance
            float chance = GetEnchantmentChance(rarity, context);
            if (!_rng.Chance(chance)) return null;

            // Determine enchantment level (1-5 typically)
            int enchantLevel = DetermineEnchantLevel(rarity, level, context);

            var enchantment = new EnchantmentInstanceData
            {
                EnchantmentId = GenerateEnchantmentId(rarity),
                Level = enchantLevel,
                Experience = 0,
                AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            // Generate stat bonuses
            int statCount = GetEnchantmentStatCount(rarity, context);
            var allStats = Enum.GetValues(typeof(MainStat)).Cast<MainStat>()
                .Where(s => s != MainStat.None)
                .ToArray();

            enchantment.StatBonuses = new MainStatEntry[statCount];
            for (int i = 0; i < statCount; i++)
            {
                var stat = allStats[_rng.NextInt(allStats.Length)];
                float baseValue = GetRandomStatValue(stat, rarity, enchantLevel);

                enchantment.StatBonuses[i] = new MainStatEntry
                {
                    Stat = stat,
                    BaseValue = baseValue,
                    ValuePerLevel = baseValue * 0.1f,
                    ValuePerEnhance = baseValue * 0.05f,
                    Mode = _config.PercentStats.Contains(stat) ? SecondaryStatMode.Percent : SecondaryStatMode.Flat,
                    IsPercent = _config.PercentStats.Contains(stat)
                };
            }

            // Generate special effects
            if (_rng.Chance(_config.SpecialEffectChance))
            {
                enchantment.Effects = GenerateSpecialEffects(rarity, enchantLevel);
            }

            return enchantment;
        }

        private float GetEnchantmentChance(ItemRarity rarity, ItemGenerationContext context)
        {
            float baseChance = _config.BaseChancePerRarity.TryGetValue(rarity, out var c) ? c : 0f;

            // Luck modifier
            if (context.Luck > 0)
            {
                baseChance += context.Luck * 0.0001f; // 0.01% per luck
            }

            // Event modifiers
            if (context.EventModifiers != null)
            {
                foreach (var mod in context.EventModifiers)
                {
                    if (mod is IEnchantmentChanceModifier enchantMod)
                    {
                        baseChance += enchantMod.GetExtraChance(context);
                    }
                }
            }

            return Math.Clamp(baseChance, 0f, 1f);
        }

        private int DetermineEnchantLevel(ItemRarity rarity, int itemLevel, ItemGenerationContext context)
        {
            int maxLevel = _config.MaxLevelPerRarity.TryGetValue(rarity, out var m) ? m : 1;
            int baseLevel = Math.Min(maxLevel, 1 + itemLevel / 20);

            // Event modifiers can boost level
            if (context.EventModifiers != null)
            {
                foreach (var mod in context.EventModifiers)
                {
                    if (mod is IEnchantmentLevelModifier levelMod)
                    {
                        baseLevel += levelMod.GetLevelBoost(context);
                    }
                }
            }

            return Math.Clamp(baseLevel, 1, maxLevel);
        }

        private int GetEnchantmentStatCount(ItemRarity rarity, ItemGenerationContext context)
        {
            int baseCount = _config.StatCountPerRarity.TryGetValue(rarity, out var c) ? c : 1;

            // Higher item level = more stats
            baseCount += context.FixedLevel.GetValueOrDefault() / 30;

            return Math.Clamp(baseCount, 1, 5);
        }

        private string GenerateEnchantmentId(ItemRarity rarity)
        {
            return $"Enchant_{rarity}_{Guid.NewGuid().ToString("N")[..8]}";
        }

        private float GetRandomStatValue(MainStat stat, ItemRarity rarity, int enchantLevel)
        {
            float rarityMult = rarity.GetDefaultStatMultiplier();
            float levelMult = 1f + enchantLevel * 0.1f;
            float variance = _rng.Range(0.8f, 1.2f);

            float baseValue = _config.BaseValues.TryGetValue(stat, out var val) ? val : 1f;

            return baseValue * rarityMult * levelMult * variance;
        }

        private SpecialEffectEntry[] GenerateSpecialEffects(ItemRarity rarity, int level)
        {
            // Could generate special effects based on rarity/level
            // For now return empty
            return Array.Empty<SpecialEffectEntry>();
        }
    }

    /// <summary>
    /// Configuration for enchantment generation.
    /// </summary>
    [Serializable]
    public class EnchantmentGeneratorConfig
    {
        public Dictionary<ItemRarity, float> BaseChancePerRarity = new()
        {
            { ItemRarity.Common, 0f },
            { ItemRarity.Uncommon, 0.05f },
            { ItemRarity.Rare, 0.1f },
            { ItemRarity.Epic, 0.2f },
            { ItemRarity.Legendary, 0.35f },
            { ItemRarity.Mythic, 0.5f },
            { ItemRarity.Ancient, 0.7f },
            { ItemRarity.Divine, 0.9f }
        };

        public Dictionary<ItemRarity, int> MaxLevelPerRarity = new()
        {
            { ItemRarity.Common, 1 },
            { ItemRarity.Uncommon, 2 },
            { ItemRarity.Rare, 3 },
            { ItemRarity.Epic, 4 },
            { ItemRarity.Legendary, 5 },
            { ItemRarity.Mythic, 5 },
            { ItemRarity.Ancient, 5 },
            { ItemRarity.Divine, 5 }
        };

        public Dictionary<ItemRarity, int> StatCountPerRarity = new()
        {
            { ItemRarity.Common, 1 },
            { ItemRarity.Uncommon, 1 },
            { ItemRarity.Rare, 2 },
            { ItemRarity.Epic, 2 },
            { ItemRarity.Legendary, 3 },
            { ItemRarity.Mythic, 3 },
            { ItemRarity.Ancient, 4 },
            { ItemRarity.Divine, 4 }
        };

        public Dictionary<MainStat, float> BaseValues = new()
        {
            { MainStat.Attack, 5f },
            { MainStat.HP, 50f },
            { MainStat.Defense, 3f },
            { MainStat.CriticalRate, 1f },
            { MainStat.CriticalDamage, 10f },
            { MainStat.AttackSpeed, 0.05f },
            { MainStat.LifeSteal, 1f },
            { MainStat.MoveSpeed, 0.5f },
            { MainStat.Range, 0.5f },
            { MainStat.DamageReduction, 1f },
            { MainStat.CooldownReduction, 1f }
        };

        public HashSet<MainStat> PercentStats = new()
        {
            MainStat.CriticalRate,
            MainStat.CriticalDamage,
            MainStat.AttackSpeed,
            MainStat.LifeSteal,
            MainStat.MoveSpeed,
            MainStat.DamageReduction,
            MainStat.CooldownReduction
        };

        public float SpecialEffectChance = 0.1f;

        public static EnchantmentGeneratorConfig Default => new();
    }

    public interface IEnchantmentChanceModifier
    {
        float GetExtraChance(ItemGenerationContext context);
    }

    public interface IEnchantmentLevelModifier
    {
        int GetLevelBoost(ItemGenerationContext context);
    }
}