using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Items.Random;

namespace IdleDefenseSurvival.Items.Generation
{
    /// <summary>
    /// Service for rolling secondary stats on equipment.
    /// </summary>
    public sealed class StatRollService
    {
        private readonly IRandomProvider _rng;
        private readonly StatRollConfig _config;

        public StatRollService(IRandomProvider rng, StatRollConfig config = null)
        {
            _rng = rng ?? new UnityRandomProvider();
            _config = config ?? StatRollConfig.Default;
        }

        /// <summary>
        /// Rolls secondary stats for an equipment item.
        /// </summary>
        public MainStatEntry[] RollSecondaryStats(EquipmentData baseEquipment, ItemRarity rarity, ItemGenerationContext context)
        {
            if (baseEquipment.SecondaryStats == null || baseEquipment.SecondaryStats.Length == 0)
                return Array.Empty<MainStatEntry>();

            int statCount = GetStatCount(rarity, context);
            if (statCount <= 0) return Array.Empty<MainStatEntry>();

            var availableStats = GetAvailableStats(baseEquipment);
            if (availableStats.Length == 0) return Array.Empty<MainStatEntry>();

            var results = new List<MainStatEntry>();
            var usedStats = new HashSet<MainStat>();

            for (int i = 0; i < statCount && availableStats.Length > 0; i++)
            {
                // Pick a stat (avoid duplicates if configured)
                var stat = PickStat(availableStats, usedStats, rarity);
                if (stat == MainStat.None) break;

                usedStats.Add(stat);
                var entry = CreateStatEntry(stat, rarity, context);
                results.Add(entry);
            }

            return results.ToArray();
        }

        private int GetStatCount(ItemRarity rarity, ItemGenerationContext context)
        {
            int baseCount = _config.BaseCountPerRarity.TryGetValue(rarity, out var count) ? count : 0;

            // Tier bonus
            int tierBonus = context.Tier / 10;

            // Event modifiers
            int eventBonus = 0;
            if (context.EventModifiers != null)
            {
                foreach (var mod in context.EventModifiers)
                {
                    if (mod is IStatCountModifier statMod)
                    {
                        eventBonus += statMod.GetExtraStatCount(context);
                    }
                }
            }

            return Math.Max(0, baseCount + tierBonus + eventBonus);
        }

        private MainStat[] GetAvailableStats(EquipmentData baseEquipment)
        {
            return baseEquipment.SecondaryStats
                .Where(s => s.Stat != MainStat.None && s.Value > 0)
                .Select(s => s.Stat)
                .Distinct()
                .ToArray();
        }

        private MainStat PickStat(MainStat[] available, HashSet<MainStat> used, ItemRarity rarity)
        {
            var candidates = available.Where(s => !used.Contains(s)).ToArray();
            if (candidates.Length == 0) return MainStat.None;

            // Weight by rarity importance
            return _rng.Choice(candidates);
        }

        private MainStatEntry CreateStatEntry(MainStat stat, ItemRarity rarity, ItemGenerationContext context)
        {
            float rarityMult = rarity.GetDefaultStatMultiplier();
            float tierMult = 1f + context.Tier * 0.02f;

            float baseValue = _config.BaseValues.TryGetValue(stat, out var val) ? val : 1f;
            float variance = _rng.Range(0.8f, 1.2f);

            float finalValue = baseValue * rarityMult * tierMult * variance;

            // Per-level scaling
            float perLevel = finalValue * _config.PerLevelMultiplier;
            float perEnhance = finalValue * _config.PerEnhanceMultiplier;

            // Determine mode (flat vs percent)
            var mode = _config.PercentStats.Contains(stat) ? SecondaryStatMode.Percent : SecondaryStatMode.Flat;

            return new MainStatEntry
            {
                Stat = stat,
                BaseValue = finalValue,
                ValuePerLevel = perLevel,
                ValuePerEnhance = perEnhance,
                Mode = mode,
                IsPercent = mode == SecondaryStatMode.Percent
            };
        }
    }

    /// <summary>
    /// Configuration for stat rolling.
    /// </summary>
    [Serializable]
    public class StatRollConfig
    {
        public Dictionary<ItemRarity, int> BaseCountPerRarity = new()
        {
            { ItemRarity.Common, 0 },
            { ItemRarity.Uncommon, 1 },
            { ItemRarity.Rare, 2 },
            { ItemRarity.Epic, 3 },
            { ItemRarity.Legendary, 4 },
            { ItemRarity.Mythic, 5 },
            { ItemRarity.Ancient, 6 },
            { ItemRarity.Divine, 7 }
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
            { MainStat.ArmorPenetration, 1f },
            { MainStat.MagicResistance, 1f }
        };

        public float PerLevelMultiplier = 0.1f;
        public float PerEnhanceMultiplier = 0.2f;

        public HashSet<MainStat> PercentStats = new()
        {
            MainStat.CriticalRate,
            MainStat.CriticalDamage,
            MainStat.AttackSpeed,
            MainStat.LifeSteal,
            MainStat.MoveSpeed,
            MainStat.DamageReduction,
            MainStat.CooldownReduction,
            MainStat.ArmorPenetration,
            MainStat.MagicResistance
        };

        public static StatRollConfig Default => new();
    }

    /// <summary>
    /// Interface for modifiers that affect stat count.
    /// </summary>
    public interface IStatCountModifier
    {
        int GetExtraStatCount(ItemGenerationContext context);
    }
}