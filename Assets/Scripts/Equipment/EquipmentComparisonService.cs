using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Stats;
using UnityEngine;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Compares an inventory item against the item currently equipped in its slot.
    /// Pure query — no state mutation.
    /// </summary>
    public sealed class EquipmentComparisonService
    {
        private readonly IEquipmentRepository _repo;

        public EquipmentComparisonService(IEquipmentRepository repo)
        {
            _repo = repo;
        }

        public EquipmentComparison CompareWithEquipped(InventoryItem item)
        {
            if (item == null || !item.IsEquippable()) return null;

            EquipmentType slot = item.GetEquipmentType();
            _repo.TryGetEquipped(slot, out var currentItem);

            var comparison = new EquipmentComparison
            {
                Slot = slot,
                CurrentItem = currentItem,
                NewItem = item,
                StatComparisons = new Dictionary<SecondaryStat, StatComparison>()
            };

            var currentBonuses = currentItem != null ? EquipmentStatCalculator.GetItemStatBonuses(currentItem) : new Dictionary<SecondaryStat, float>();
            var newBonuses = EquipmentStatCalculator.GetItemStatBonuses(item);

            var allStats = new HashSet<SecondaryStat>(currentBonuses.Keys);
            allStats.UnionWith(newBonuses.Keys);

            int totalImprovement = 0;
            foreach (var stat in allStats)
            {
                float current = currentBonuses.GetValueOrDefault(stat, 0);
                float @new = newBonuses.GetValueOrDefault(stat, 0);
                float diff = @new - current;

                comparison.StatComparisons[stat] = new StatComparison
                {
                    Stat = stat,
                    CurrentValue = current,
                    NewValue = @new,
                    Difference = diff,
                    PercentChange = current != 0 ? diff / current * 100f : (@new > 0 ? 100f : 0f)
                };

                if (diff > 0) totalImprovement += Mathf.RoundToInt(diff);
            }

            comparison.TotalStatImprovement = totalImprovement;
            comparison.IsUpgrade = totalImprovement > 0;

            // Compare effects
            if (currentItem != null)
            {
                var currentEffects = GetItemEffects(currentItem);
                var newEffects = GetItemEffects(item);

                comparison.GainedEffects = newEffects.Except(currentEffects).ToArray();
                comparison.LostEffects = currentEffects.Except(newEffects).ToArray();
            }
            else
            {
                comparison.GainedEffects = GetItemEffects(item).ToArray();
            }

            // Compare set bonuses
            string newSetId = item.GetSetId();
            string currentSetId = currentItem?.GetSetId();

            if (!string.IsNullOrEmpty(newSetId) && newSetId != currentSetId)
                comparison.GainedSetBonuses = new[] { newSetId };
            if (!string.IsNullOrEmpty(currentSetId) && currentSetId != newSetId)
                comparison.LostSetBonuses = new[] { currentSetId };

            return comparison;
        }

        private static IEnumerable<string> GetItemEffects(InventoryItem item)
        {
            var itemData = item.GetEquipmentData();
            if (itemData?.SpecialEffects == null) return Array.Empty<string>();

            return itemData.SpecialEffects
                .Where(e => e.CanActivate(item.Level))
                .Select(e => e.EffectType.GetDisplayName());
        }
    }
}