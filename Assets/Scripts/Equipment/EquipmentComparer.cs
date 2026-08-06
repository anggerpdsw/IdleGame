using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Equipment comparer - compares equipment items for upgrades, sorting, and evaluation.
    /// </summary>
    public static class EquipmentComparer
    {
        /// <summary>
        /// Compares two equipment items and returns a detailed comparison.
        /// </summary>
        public static EquipmentComparison Compare(InventoryItem current, InventoryItem candidate, EquipmentType slot)
        {
            var comparison = new EquipmentComparison
            {
                Slot = slot,
                CurrentItem = current,
                NewItem = candidate,
                StatComparisons = new Dictionary<MainStat, StatComparison>()
            };

            var currentBonuses = current != null ? GetTotalStatBonuses(current) : new Dictionary<MainStat, float>();
            var candidateBonuses = candidate != null ? GetTotalStatBonuses(candidate) : new Dictionary<MainStat, float>();

            // Compare all stats
            var allStats = new HashSet<MainStat>(currentBonuses.Keys);
            allStats.UnionWith(candidateBonuses.Keys);

            float totalImprovement = 0f;
            int upgradeCount = 0;
            int downgradeCount = 0;

            foreach (var stat in allStats)
            {
                float currentVal = currentBonuses.GetValueOrDefault(stat, 0);
                float candidateVal = candidateBonuses.GetValueOrDefault(stat, 0);
                float diff = candidateVal - currentVal;
                float percentChange = currentVal != 0 ? diff / currentVal * 100f : (candidateVal > 0 ? 100f : 0f);

                var statComp = new StatComparison
                {
                    Stat = stat,
                    CurrentValue = currentVal,
                    NewValue = candidateVal,
                    Difference = diff,
                    PercentChange = percentChange
                };

                comparison.StatComparisons[stat] = statComp;

                if (diff > 0.001f)
                {
                    totalImprovement += diff;
                    upgradeCount++;
                }
                else if (diff < -0.001f)
                {
                    downgradeCount++;
                }
            }

            comparison.TotalStatImprovement = (int)totalImprovement;
            comparison.IsUpgrade = totalImprovement > 0;
            comparison.UpgradeStatCount = upgradeCount;
            comparison.DowngradeStatCount = downgradeCount;

            // Compare special effects
            if (current != null)
            {
                var currentEffects = GetItemEffects(current);
                var candidateEffects = GetItemEffects(candidate);

                comparison.GainedEffects = candidateEffects.Except(currentEffects).ToArray();
                comparison.LostEffects = currentEffects.Except(candidateEffects).ToArray();
                comparison.KeptEffects = currentEffects.Intersect(candidateEffects).ToArray();
            }
            else
            {
                comparison.GainedEffects = GetItemEffects(candidate).ToArray();
                comparison.KeptEffects = Array.Empty<string>();
            }

            // Compare set bonuses
            string currentSetId = current?.GetSetId();
            string candidateSetId = candidate?.GetSetId();

            if (!string.IsNullOrEmpty(candidateSetId) && candidateSetId != currentSetId)
            {
                var setData = ItemDatabase.Instance?.GetSet(candidateSetId);
                if (setData != null)
                {
                    int newSetCount = EquipmentService.Instance?.GetSetPieceCount(candidateSetId) + 1 ?? 1;
                    foreach (var tier in setData.Tiers.Where(t => t.IsActive(newSetCount)))
                    {
                        comparison.GainedSetBonuses = comparison.GainedSetBonuses.Concat(new[] { $"{setData.SetName} {tier.TierName}" }).ToArray();
                    }
                }
            }

            if (!string.IsNullOrEmpty(currentSetId) && currentSetId != candidateSetId)
            {
                var setData = ItemDatabase.Instance?.GetSet(currentSetId);
                if (setData != null)
                {
                    int currentSetCount = EquipmentService.Instance?.GetSetPieceCount(currentSetId) ?? 0;
                    foreach (var tier in setData.Tiers.Where(t => t.IsActive(currentSetCount) && !t.IsActive(currentSetCount - 1)))
                    {
                        comparison.LostSetBonuses = comparison.LostSetBonuses.Concat(new[] { $"{setData.SetName} {tier.TierName}" }).ToArray();
                    }
                }
            }

            // Calculate overall score
            comparison.OverallScore = CalculateScore(candidateBonuses, GetItemEffects(candidate));
            if (current != null)
            {
                float currentScore = CalculateScore(currentBonuses, GetItemEffects(current));
                comparison.ScoreDifference = comparison.OverallScore - currentScore;
            }

            return comparison;
        }

        /// <summary>
        /// Gets the best equipment for a slot from a list of candidates.
        /// </summary>
        public static InventoryItem GetBestForSlot(EquipmentType slot, IEnumerable<InventoryItem> candidates, InventoryItem current = null)
        {
            var validCandidates = candidates
                .Where(c => c != null && c.IsEquippable() && c.GetEquipmentType() == slot)
                .ToList();

            if (validCandidates.Count == 0) return null;

            // Score each candidate
            var scored = validCandidates.Select(c => new
            {
                Item = c,
                Score = CalculateScore(GetTotalStatBonuses(c), GetItemEffects(c))
            }).OrderByDescending(x => x.Score).ToList();

            return scored.First().Item;
        }

        /// <summary>
        /// Ranks equipment items for a slot from best to worst.
        /// </summary>
        public static List<EquipmentComparison> RankForSlot(EquipmentType slot, IEnumerable<InventoryItem> candidates, InventoryItem current = null)
        {
            var validCandidates = candidates
                .Where(c => c != null && c.IsEquippable() && c.GetEquipmentType() == slot)
                .ToList();

            var comparisons = new List<EquipmentComparison>();

            foreach (var candidate in validCandidates)
            {
                var comp = Compare(current, candidate, slot);
                comparisons.Add(comp);
            }

            return comparisons.OrderByDescending(c => c.OverallScore).ToList();
        }

        /// <summary>
        /// Gets stat comparison summary text for UI.
        /// </summary>
        public static string GetComparisonText(EquipmentComparison comparison)
        {
            if (comparison == null) return "No comparison";

            var lines = new List<string>();

            if (comparison.CurrentItem == null)
            {
                lines.Add("Equipping new item:");
            }
            else if (comparison.NewItem == null)
            {
                lines.Add("Unequipping item:");
            }
            else
            {
                lines.Add(comparison.IsUpgrade ? "↑ UPGRADE" : "↓ DOWNGRADE");
            }

            // Stat changes
            var significantChanges = comparison.StatComparisons
                .Where(kvp => Math.Abs(kvp.Value.Difference) > 0.001f)
                .OrderByDescending(kvp => Math.Abs(kvp.Value.PercentChange))
                .Take(6);

            foreach (var kvp in significantChanges)
            {
                var comp = kvp.Value;
                string sign = comp.Difference > 0 ? "+" : "";
                string color = comp.Difference > 0 ? "<color=green>" : "<color=red>";
                lines.Add($"{color}{comp.Stat.GetShortName()}: {comp.CurrentValue:F1} → {comp.NewValue:F1} ({sign}{comp.Difference:F1})</color>");
            }

            // Effects
            if (comparison.GainedEffects.Length > 0)
            {
                lines.Add($"<color=cyan>Gained: {string.Join(", ", comparison.GainedEffects)}</color>");
            }
            if (comparison.LostEffects.Length > 0)
            {
                lines.Add($"<color=red>Lost: {string.Join(", ", comparison.LostEffects)}</color>");
            }
            if (comparison.GainedSetBonuses.Length > 0)
            {
                lines.Add($"<color=yellow>Set: {string.Join(", ", comparison.GainedSetBonuses)}</color>");
            }
            if (comparison.LostSetBonuses.Length > 0)
            {
                lines.Add($"<color=orange>Lost Set: {string.Join(", ", comparison.LostSetBonuses)}</color>");
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Calculates a composite score for an equipment item.
        /// </summary>
        public static float CalculateScore(Dictionary<MainStat, float> statBonuses, IEnumerable<string> effects)
        {
            float score = 0f;

            // Stat weights (adjustable based on game balance)
            var statWeights = new Dictionary<MainStat, float>
            {
                { MainStat.Attack, 1.0f },
                { MainStat.HP, 0.8f },
                { MainStat.Defense, 0.7f },
                { MainStat.CriticalRate, 1.5f },
                { MainStat.CriticalDamage, 1.2f },
                { MainStat.AttackSpeed, 1.0f },
                { MainStat.LifeSteal, 1.3f },
                { MainStat.DamageReduction, 1.0f },
                { MainStat.MoveSpeed, 0.5f },
                { MainStat.Range, 0.6f },
                { MainStat.CooldownReduction, 0.8f },
                { MainStat.FinalDamage, 2.0f },
                { MainStat.FinalDefense, 1.5f },
                { MainStat.FinalHP, 1.5f },
                { MainStat.BossDamage, 1.5f },
                { MainStat.EliteDamage, 1.2f },
                { MainStat.Dodge, 0.8f },
                { MainStat.BlockChance, 0.7f },
            };

            foreach (var kvp in statBonuses)
            {
                float weight = statWeights.GetValueOrDefault(kvp.Key, 0.3f);
                score += kvp.Value * weight;
            }

            // Effect bonuses
            if (effects != null)
            {
                foreach (var effect in effects)
                {
                    // Assign base value to effects
                    score += 10f; // Base effect value
                }
            }

            return score;
        }

        /// <summary>
        /// Gets total stat bonuses for an item (including gems, enchantments, etc.)
        /// </summary>
        public static Dictionary<MainStat, float> GetTotalStatBonuses(InventoryItem item)
        {
            var bonuses = new Dictionary<MainStat, float>();

            if (item == null) return bonuses;

            if (ItemDatabase.Instance?.GetItem(item.ItemId) is not EquipmentData itemData) return bonuses;

            // Main stats
            if (itemData.MainStats != null)
            {
                foreach (var statEntry in itemData.MainStats)
                {
                    float value = statEntry.GetValue(item.Level, item.EnhanceLevel);
                    if (bonuses.ContainsKey(statEntry.Stat))
                        bonuses[statEntry.Stat] += value;
                    else
                        bonuses[statEntry.Stat] = value;
                }
            }

            // Gem stats
            if (item.Sockets != null)
            {
                foreach (var socket in item.Sockets)
                {
                    if (socket.IsFilled)
                    {
                        var gemStats = GemService.Instance?.GetGemStats(socket.GemId, socket.GemLevel);
                        if (gemStats != null)
                        {
                            foreach (var statEntry in gemStats)
                            {
                                float value = statEntry.GetValue(socket.GemLevel, 0);
                                if (bonuses.ContainsKey(statEntry.Stat))
                                    bonuses[statEntry.Stat] += value;
                                else
                                    bonuses[statEntry.Stat] = value;
                            }
                        }
                    }
                }
            }

            // Enchantment stats
            if (item.Enchantment?.StatBonuses != null)
            {
                foreach (var statEntry in item.Enchantment.StatBonuses)
                {
                    float value = statEntry.GetValue(item.Enchantment.Level, 0);
                    if (bonuses.ContainsKey(statEntry.Stat))
                        bonuses[statEntry.Stat] += value;
                    else
                        bonuses[statEntry.Stat] = value;
                }
            }

            return bonuses;
        }

        /// <summary>
        /// Gets active effect names for an item.
        /// </summary>
        public static List<string> GetItemEffects(InventoryItem item)
        {
            var effects = new List<string>();

            if (item == null) return effects;

            var itemData = ItemDatabase.Instance?.GetItem(item.ItemId) as EquipmentData;
            if (itemData?.SpecialEffects != null)
            {
                foreach (var effectEntry in itemData.SpecialEffects)
                {
                    if (effectEntry.CanActivate(item.Level, item.EnhanceLevel))
                    {
                        effects.Add(effectEntry.EffectType.GetDisplayName());
                    }
                }
            }

            // Set bonuses
            string setId = item.GetSetId();
            if (!string.IsNullOrEmpty(setId))
            {
                var setData = ItemDatabase.Instance?.GetSet(setId);
                if (setData?.Tiers != null)
                {
                    int setCount = EquipmentService.Instance?.GetSetPieceCount(setId) + 1 ?? 1;
                    foreach (var tier in setData.Tiers.Where(t => t.IsActive(setCount)))
                    {
                        if (tier.SpecialEffects != null)
                        {
                            foreach (var effectEntry in tier.SpecialEffects)
                            {
                                effects.Add($"Set: {effectEntry.EffectType.GetDisplayName()}");
                            }
                        }
                    }
                }
            }

            return effects;
        }
    }
}