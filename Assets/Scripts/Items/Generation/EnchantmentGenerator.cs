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