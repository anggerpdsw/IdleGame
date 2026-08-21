using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Inventory;
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
        public EnchantmentInstanceData GenerateEnchantment(EquipmentData baseEquipment, Rarity rarity, int level, ItemGenerationContext context)
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
            var allStats = Enum.GetValues(typeof(SecondaryStat)).Cast<SecondaryStat>()
                .Where(s => s != SecondaryStat.None)
                .ToArray();

            enchantment.StatBonuses = new CombatStatEntry[statCount];
            for (int i = 0; i < statCount; i++)
            {
                var stat = allStats[_rng.NextInt(allStats.Length)];
                float baseValue = GetRandomStatValue(stat, rarity, enchantLevel);

                enchantment.StatBonuses[i] = new CombatStatEntry
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

        private float GetEnchantmentChance(Rarity rarity, ItemGenerationContext context)
        {
            float baseChance = _config.BaseChancePerRarity.TryGetValue(rarity, out var c) ? c : 0f;

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

        private int DetermineEnchantLevel(Rarity rarity, int itemLevel, ItemGenerationContext context)
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

        private int GetEnchantmentStatCount(Rarity rarity, ItemGenerationContext context)
        {
            int baseCount = _config.StatCountPerRarity.TryGetValue(rarity, out var c) ? c : 1;

            // Higher item level = more stats
            baseCount += context.FixedLevel.GetValueOrDefault() / 30;

            return Math.Clamp(baseCount, 1, 5);
        }

        private string GenerateEnchantmentId(Rarity rarity)
        {
            return $"Enchant_{rarity}_{Guid.NewGuid().ToString("N")[..8]}";
        }

        private float GetRandomStatValue(SecondaryStat stat, Rarity rarity, int enchantLevel)
        {
            float rarityMult = rarity.GetDefaultStatMultiplier();
            float levelMult = 1f + enchantLevel * 0.1f;
            float variance = _rng.Range(0.8f, 1.2f);

            float baseValue = _config.BaseValues.TryGetValue(stat, out var val) ? val : 1f;

            return baseValue * rarityMult * levelMult * variance;
        }

        private SpecialEffectEntry[] GenerateSpecialEffects(Rarity rarity, int level)
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
        public Dictionary<Rarity, float> BaseChancePerRarity = new()
        {
            { Rarity.Common, 0f },
            { Rarity.Rare, 0.1f },
            { Rarity.Epic, 0.2f },
            { Rarity.Legendary, 0.35f },
            { Rarity.Mythic, 0.5f },
            { Rarity.Divine, 0.9f }
        };

        public Dictionary<Rarity, int> MaxLevelPerRarity = new()
        {
            { Rarity.Common, 1 },
            { Rarity.Rare, 3 },
            { Rarity.Epic, 4 },
            { Rarity.Legendary, 5 },
            { Rarity.Mythic, 5 },
            { Rarity.Divine, 5 }
        };

        public Dictionary<Rarity, int> StatCountPerRarity = new()
        {
            { Rarity.Common, 1 },
            { Rarity.Rare, 2 },
            { Rarity.Epic, 2 },
            { Rarity.Legendary, 3 },
            { Rarity.Mythic, 3 },
            { Rarity.Divine, 4 }
        };

        public Dictionary<SecondaryStat, float> BaseValues = new()
        {
            { SecondaryStat.AttackRange, 1f },
            { SecondaryStat.BounceChance, 5f },
            { SecondaryStat.BounceCount, 1f },
            { SecondaryStat.MultiShootChance, 5f },
            { SecondaryStat.KnockbackChance, 5f },
            { SecondaryStat.LifeSteal, 1f },
            { SecondaryStat.MoveSpeed, 0.5f },
            { SecondaryStat.CooldownReduction, 1f },
            { SecondaryStat.BossDamage, 1f },
            { SecondaryStat.EliteDamage, 1f },
            { SecondaryStat.HitRate, 1f },
            { SecondaryStat.Penetration, 1f },
            { SecondaryStat.DefenseBreak, 1f }
        };

        // Derived combat stats (CriticalChance, AttackSpeed, ...) come from Main Attribute.
        public HashSet<SecondaryStat> PercentStats = new()
        {
            SecondaryStat.BounceChance,
            SecondaryStat.MultiShootChance,
            SecondaryStat.KnockbackChance,
            SecondaryStat.LifeSteal,
            SecondaryStat.MoveSpeed,
            SecondaryStat.CooldownReduction,
            SecondaryStat.BossDamage,
            SecondaryStat.EliteDamage,
            SecondaryStat.HitRate
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