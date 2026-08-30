using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items.Random;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Equipment;

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
        /// Generates enchantment stat bonuses (always rolls, independent of enchant chance).
        /// Uses RarityMechanicConfig.StatBonusesRollRanges.
        /// </summary>
        public EnchantmentStatBonusEntry[] GenerateStatBonuses(Rarity rarity, int level, ItemGenerationContext context)
        {
            var (minRolls, maxRolls) = RarityMechanicConfig.GetStatBonusesRollRange(rarity);
            int rollCount = _rng.Range(minRolls, maxRolls + 1);

            int tierBonus = context?.Tier / 10 ?? 0;
            int eventBonus = 0;
            if (context?.EventModifiers != null)
            {
                foreach (var mod in context.EventModifiers)
                {
                    if (mod is IStatCountModifier statMod)
                        eventBonus += statMod.GetExtraStatCount(context);
                }
            }

            rollCount = Math.Max(0, rollCount + tierBonus + eventBonus);
            if (rollCount <= 0) return Array.Empty<EnchantmentStatBonusEntry>();

            var allStats = SecondaryStatRegistry.GetEnchantableStats();
            var aggregated = new Dictionary<SecondaryStat, float>();

            for (int i = 0; i < rollCount; i++)
            {
                var stat = _rng.Choice(allStats);
                var meta = SecondaryStatRegistry.Get(stat);
                float rarityMult = rarity.GetDefaultStatMultiplier();
                float levelMult = 1f + level * 0.1f;
                float variance = _rng.Range(0.8f, 1.2f);

                float templateBaseValue = meta.BaseValue;
                var progression = AttributeStatLoader.Instance?.GetSecondaryProgression(stat);
                float valuePerLevel = progression?.ValuePerLevel ?? 0f;

                float baseValue = templateBaseValue + valuePerLevel;
                float finalValue = baseValue * rarityMult * levelMult * variance;

                if (aggregated.TryGetValue(stat, out float existing))
                    aggregated[stat] = existing + finalValue;
                else
                    aggregated[stat] = finalValue;
            }

            var bonusEntries = new EnchantmentStatBonusEntry[aggregated.Count];
            int index = 0;
            foreach (var kvp in aggregated)
            {
                var meta = SecondaryStatRegistry.Get(kvp.Key);
                var progression = AttributeStatLoader.Instance?.GetSecondaryProgression(kvp.Key);
                float valuePerLevel = progression?.ValuePerLevel ?? 0f;

                bonusEntries[index++] = new EnchantmentStatBonusEntry
                {
                    Stat = kvp.Key,
                    BaseValue = kvp.Value,
                    ValuePerLevel = valuePerLevel,
                    Mode = meta.DefaultMode,
                    IsPercent = meta.IsPercentage
                };
            }

            return bonusEntries;
        }

        /// <summary>
        /// Attempts to generate an enchantment for equipment (effects only).
        /// Returns null if no enchantment is generated.
        /// Stat bonuses are generated separately via GenerateStatBonuses.
        /// </summary>
        public EnchantmentInstanceData GenerateEnchantment(EquipmentData baseEquipment, Rarity rarity, int level, ItemGenerationContext context)
        {
            // Check chance for enchantment (effects only)
            float chance = GetEnchantmentChance(rarity, context);
            if (!_rng.Chance(chance)) return null;

            int enchantLevel = DetermineEnchantLevel(rarity, level, context);

            var enchantment = new EnchantmentInstanceData
            {
                EnchantmentId = GenerateEnchantmentId(rarity),
                Level = enchantLevel,
                Experience = 0,
                AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            // StatBonuses are now generated separately via GenerateStatBonuses
            // Only populate Effects here (chance-based)
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