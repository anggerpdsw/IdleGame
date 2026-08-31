using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Items.Random;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Items.Generation
{
    /// <summary>
    /// Generates SecondaryStats directly for crafting.
    /// Rolls from RarityMechanicConfig.SecondaryRollRanges and merges duplicates.
    /// Single source of truth for ValuePerLevel: dataSOTValuePerLevel.json via AttributeStatLoader.
    /// </summary>
    public sealed class SecondaryStatGenerator
    {
        private readonly IRandomProvider _rng;

        public SecondaryStatGenerator(IRandomProvider rng = null)
        {
            _rng = rng ?? new UnityRandomProvider();
        }

        /// <summary>
        /// Rolls secondary stats for crafting based on rarity.
        /// Merges duplicate stats by summing their BaseValues.
        /// </summary>
        public CombatStatEntry[] Generate(Rarity rarity, ItemGenerationContext context = null)
        {
            SecondaryStatResolver.Initialize();

            var validStats = SecondaryStatRegistry.GetRollableStats();
            if (validStats.Length == 0) return Array.Empty<CombatStatEntry>();

            // Get roll range from RarityMechanicConfig
            var (minRolls, maxRolls) = RarityMechanicConfig.GetSecondaryRollRange(rarity);
            if (maxRolls <= 0) return Array.Empty<CombatStatEntry>();

            int rollCount = _rng.Range(minRolls, maxRolls + 1);

            // Add tier bonus
            int tierBonus = context?.Tier / 10 ?? 0;

            // Add event modifiers
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
            if (rollCount <= 0) return Array.Empty<CombatStatEntry>();

            // Roll and aggregate duplicates
            var aggregated = new Dictionary<SecondaryStat, float>();

            for (int i = 0; i < rollCount; i++)
            {
                var stat = _rng.Choice(validStats);
                var meta = SecondaryStatRegistry.Get(stat);

                float rarityMult = rarity.GetDefaultStatMultiplier();
                float tierMult = 1f + (context?.Tier ?? 0) * 0.02f;
                float variance = _rng.Range(0.8f, 1.2f);

                float baseValue = meta.BaseValue * rarityMult * tierMult * variance;

                if (aggregated.TryGetValue(stat, out float existing))
                    aggregated[stat] = existing + baseValue;
                else
                    aggregated[stat] = baseValue;
            }

            // Convert to CombatStatEntry with proper template BaseValue + level progression
            // Use registry BaseValue (template) + Level * ValuePerLevel (from SOT)
            // GetValue(level) = BaseValue + ValuePerLevel * (level - 1)
            // We want final = templateBaseValue + Level * ValuePerLevel
            // So BaseValue = templateBaseValue + ValuePerLevel
            var results = new CombatStatEntry[aggregated.Count];
            int index = 0;
            foreach (var kvp in aggregated)
            {
                var meta = SecondaryStatRegistry.Get(kvp.Key);
                var progression = AttributeStatLoader.Instance?.GetSecondaryProgression(kvp.Key);

                float templateBaseValue = meta.BaseValue;
                float valuePerLevel = progression?.ValuePerLevel ?? 0f;

                // BaseValue stores the value at level 1: templateBaseValue + ValuePerLevel
                // GetValue(level) = BaseValue + ValuePerLevel * (level - 1) = templateBaseValue + Level * ValuePerLevel
                float baseValue = templateBaseValue + valuePerLevel;

                results[index++] = new CombatStatEntry
                {
                    Stat = kvp.Key,
                    BaseValue = baseValue,
                    ValuePerLevel = valuePerLevel,
                };
            }

            return results;
        }
    }
}