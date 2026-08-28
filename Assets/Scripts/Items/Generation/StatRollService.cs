using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Stats;
using IdleDefenseSurvival.Items.Random;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival;

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
        public CombatStatEntry[] RollSecondaryStats(EquipmentData baseEquipment, Rarity rarity, ItemGenerationContext context)
        {
            if (baseEquipment.SecondaryStats == null || baseEquipment.SecondaryStats.Length == 0)
                return Array.Empty<CombatStatEntry>();

            int statCount = GetStatCount(rarity, context);
            if (statCount <= 0) return Array.Empty<CombatStatEntry>();

            var availableStats = GetAvailableStats(baseEquipment);
            if (availableStats.Length == 0) return Array.Empty<CombatStatEntry>();

            var results = new List<CombatStatEntry>();
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

        private int GetStatCount(Rarity rarity, ItemGenerationContext context)
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

        private SecondaryStat PickStat(SecondaryStat[] available, HashSet<SecondaryStat> used, Rarity rarity)
        {
            var candidates = available.Where(s => !used.Contains(s)).ToArray();
            if (candidates.Length == 0) return SecondaryStat.None;

            // Weight by rarity importance
            return _rng.Choice(candidates);
        }

        private CombatStatEntry CreateStatEntry(SecondaryStat stat, Rarity rarity, ItemGenerationContext context)
        {
            var meta = SecondaryStatRegistry.Get(stat);
            float rarityMult = rarity.GetDefaultStatMultiplier();
            float tierMult = 1f + context.Tier * 0.02f;

            float baseValue = meta.BaseValue;
            float variance = _rng.Range(0.8f, 1.2f);

            float finalValue = baseValue * rarityMult * tierMult * variance;

            // Per-level scaling from SOT: dataSkillTypeValuePerLevel.json
            var progression = AttributeStatLoader.Instance?.GetSecondaryProgression(stat);
            float perLevel = progression?.ValuePerLevel ?? (finalValue * _config.PerLevelMultiplier);

            return new CombatStatEntry
            {
                Stat = stat,
                BaseValue = finalValue,
                ValuePerLevel = perLevel,
                Mode = meta.DefaultMode,
                IsPercent = meta.IsPercentage
            };
        }
    }

    /// <summary>
    /// Configuration for stat rolling.
    /// </summary>
    [Serializable]
    public class StatRollConfig
    {
        public float PerLevelMultiplier = 0.1f;

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