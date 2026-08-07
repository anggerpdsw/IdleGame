using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Items.Random;
using IdleDefenseSurvival.Equipment;

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
            var usedStats = new HashSet<SecondaryStat>();

            for (int i = 0; i < statCount && availableStats.Length > 0; i++)
            {
                // Pick a stat (avoid duplicates if configured)
                var stat = PickStat(availableStats, usedStats, rarity);
                if (stat == SecondaryStat.None) break;

                usedStats.Add(stat);
                var entry = CreateStatEntry(stat, rarity, context);
                results.Add(entry);
            }

            return results.ToArray();
        }

        private int GetStatCount(ItemRarity rarity, ItemGenerationContext context)
        {
            int baseCount = RarityMechanicConfig.GetSecondaryCount(rarity);

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

        private SecondaryStat[] GetAvailableStats(EquipmentData baseEquipment)
        {
            return baseEquipment.SecondaryStats
                .Where(s => s.Stat != SecondaryStat.None && s.Value > 0)
                .Select(s => s.Stat)
                .Distinct()
                .ToArray();
        }

        private SecondaryStat PickStat(SecondaryStat[] available, HashSet<SecondaryStat> used, ItemRarity rarity)
        {
            var candidates = available.Where(s => !used.Contains(s)).ToArray();
            if (candidates.Length == 0) return SecondaryStat.None;

            // Weight by rarity importance
            return _rng.Choice(candidates);
        }

        private MainStatEntry CreateStatEntry(SecondaryStat stat, ItemRarity rarity, ItemGenerationContext context)
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
        /// <summary>
        /// Secondary count per rarity moved to RarityMechanicConfig (single tuning point).
        /// Only specialization stats (SecondaryStat) are rolled here — derived stats like
        /// AttackDamage (STR), Health (CON), SkillDamage (INT), CriticalDamage (DEX) come
        /// from Main Attributes, not equipment secondaries.
        /// </summary>
        public Dictionary<SecondaryStat, float> BaseValues = new()
        {
            { SecondaryStat.AttackRange, 5f },
            { SecondaryStat.BounceChance, 5f },
            { SecondaryStat.BounceCount, 1f },
            { SecondaryStat.MultiShootChance, 5f },
            { SecondaryStat.MultiShootCount, 1f },
            { SecondaryStat.KnockbackChance, 5f },
            { SecondaryStat.StuntChance, 3f },
            { SecondaryStat.StuntDuration, 0.5f },
            { SecondaryStat.LifeSteal, 1f },
            { SecondaryStat.DamagePerRange, 1f },
            { SecondaryStat.CooldownReduction, 1f },
            { SecondaryStat.MoveSpeed, 0.5f },
            { SecondaryStat.BossDamage, 1f },
            { SecondaryStat.EliteDamage, 1f },
            { SecondaryStat.GoldGain, 1f },
            { SecondaryStat.DropRate, 1f },
            { SecondaryStat.InterestWave, 1f },
            { SecondaryStat.HitRate, 1f }
        };

        public float PerLevelMultiplier = 0.1f;
        public float PerEnhanceMultiplier = 0.2f;

        public HashSet<SecondaryStat> PercentStats = new()
        {
            SecondaryStat.BounceChance,
            SecondaryStat.MultiShootChance,
            SecondaryStat.KnockbackChance,
            SecondaryStat.StuntChance,
            SecondaryStat.LifeSteal,
            SecondaryStat.MoveSpeed,
            SecondaryStat.CooldownReduction,
            SecondaryStat.BossDamage,
            SecondaryStat.EliteDamage,
            SecondaryStat.GoldGain,
            SecondaryStat.DropRate,
            SecondaryStat.InterestWave,
            SecondaryStat.HitRate
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