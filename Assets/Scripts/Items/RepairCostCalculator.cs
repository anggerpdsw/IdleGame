using System;
using UnityEngine;
using IdleDefenseSurvival.Inventory;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Detailed repair cost breakdown for UI tooltip.
    /// </summary>
    [Serializable]
    public class RepairCostBreakdown
    {
        public string ItemId;
        public string InstanceId;
        public int DurabilityNeeded;
        public long BaseCostPerPoint;

        // Scaling factors
        public float LevelMultiplier;
        public float RarityMultiplier;
        public float EnhanceMultiplier;
        public float LimitBreakMultiplier;
        public float RefineMultiplier;
        public float TranscendMultiplier;
        public float EvolutionMultiplier;
        public float AwakeningMultiplier;
        public float MasterworkMultiplier;
        public float MissingPercentMultiplier;
        public float QualityMultiplier;
        public float TierMultiplier;
        public float StarMultiplier;
        public float CorruptionMultiplier;
        public float BrokenMultiplier;
        public float GlobalGrowth;

        // Thresholds
        public float FreeRepairThreshold;
        public float CurrentDurabilityPercent;
        public bool IsFree;

        public float TotalMultiplier => LevelMultiplier * RarityMultiplier * EnhanceMultiplier *
            LimitBreakMultiplier * RefineMultiplier * TranscendMultiplier *
            EvolutionMultiplier * AwakeningMultiplier * MasterworkMultiplier *
            MissingPercentMultiplier * QualityMultiplier * TierMultiplier *
            StarMultiplier * CorruptionMultiplier * BrokenMultiplier * GlobalGrowth;

        public long EstimatedCost => IsFree ? 0 : Mathf.RoundToInt(BaseCostPerPoint * DurabilityNeeded * TotalMultiplier);
    }

    /// <summary>
    /// Pure repair cost calculation: formula, free-repair threshold, cost breakdown.
    /// No payment, no events. UI reads derived data directly.
    /// </summary>
    public sealed class RepairCostCalculator
    {
        private readonly RepairConfig _config;

        public RepairCostCalculator(RepairConfig config) => _config = config;

        /// <summary>
        /// Checks if item qualifies for free repair.
        /// </summary>
        public bool IsFreeRepair(InventoryItem item)
        {
            if (item == null) return false;
            return item.GetDurabilityPercent() >= _config.FreeRepairThreshold;
        }

        /// <summary>
        /// Calculates repair cost for an item with enhanced formula.
        /// </summary>
        public long CalculateRepairCost(InventoryItem item, int durabilityPoints)
        {
            if (item == null || durabilityPoints <= 0) return 0;

            var itemData = ItemDatabase.Instance?.GetItem(item.ItemId) as EquipmentData;
            if (itemData == null) return durabilityPoints * _config.BaseRepairCostPerPoint;

            long baseCostPerPoint = itemData.RepairCostPerDurability > 0
                ? itemData.RepairCostPerDurability
                : _config.BaseRepairCostPerPoint;

            // ===== Scaling Factors =====

            // 1. Level scaling: +5% per level
            float levelMultiplier = 1f + (item.Level - 1) * 0.05f;

            // 2. ItemRarity scaling
            float rarityMultiplier = itemData.ItemRarity.GetDefaultUpgradeMultiplier();

            // 3. Enhance scaling: +10% per enhance level
            float enhanceMultiplier = 1f + item.EnhanceLevel * 0.1f;

            // 4. Limit Break scaling: +15% per limit break
            float limitBreakMultiplier = 1f + item.LimitBreakCount * 0.15f;

            // 5. Refine scaling: +8% per refine level
            float refineMultiplier = 1f + item.RefineLevel * 0.08f;

            // 6. Transcend scaling: +20% per transcend level
            float transcendMultiplier = 1f + item.TranscendLevel * 0.2f;

            // 7. Evolution scaling: +25% per evolution stage
            float evolutionMultiplier = 1f + item.EvolutionStage * 0.25f;

            // 8. Awakening scaling: +50% if awakened
            float awakeningMultiplier = item.IsAwakened ? 1.5f : 1f;

            // 9. Masterwork scaling: +30% if masterwork
            float masterworkMultiplier = item.IsMasterwork ? 1.3f : 1f;

            // 10. Durability missing % scaling: more missing = slightly higher per-point cost
            float missingPercent = 1f - item.GetDurabilityPercent();
            float missingMultiplier = 1f + missingPercent * 0.2f; // Up to +20% when fully broken

            // 11. Quality scaling (from item data)
            float qualityMultiplier = itemData.QualityMultiplier > 0 ? itemData.QualityMultiplier : 1f;

            // 12. Item Tier scaling
            float tierMultiplier = 1f + itemData.Tier * 0.1f;

            // 13. Star/Awakening/Transcend/Corruption scalings
            float starMultiplier = 1f + itemData.StarRating * 0.05f;
            float corruptionMultiplier = itemData.CorruptionTier * 0.1f + 1f;

            // 14. Broken state penalty: 2x cost if completely broken
            float brokenMultiplier = item.IsBroken ? 2f : 1f;

            // 15. Global RepairCostGrowth config
            float globalGrowth = _config.RepairCostGrowth;

            // ===== Total Multiplier =====
            float totalMultiplier = levelMultiplier *
                                   rarityMultiplier *
                                   enhanceMultiplier *
                                   limitBreakMultiplier *
                                   refineMultiplier *
                                   transcendMultiplier *
                                   evolutionMultiplier *
                                   awakeningMultiplier *
                                   masterworkMultiplier *
                                   missingMultiplier *
                                   qualityMultiplier *
                                   tierMultiplier *
                                   starMultiplier *
                                   corruptionMultiplier *
                                   brokenMultiplier *
                                   globalGrowth;

            float cost = baseCostPerPoint * durabilityPoints * totalMultiplier;
            return Mathf.RoundToInt(cost);
        }

        /// <summary>
        /// Gets total repair cost for a collection of items (accounts for free repair threshold).
        /// </summary>
        public long GetTotalRepairCost(System.Collections.Generic.IEnumerable<InventoryItem> items)
        {
            long total = 0;
            if (items == null) return 0;

            foreach (var item in items)
            {
                if (item == null || !item.IsEquippable()) continue;
                if (item.CurrentDurability >= item.MaxDurability) continue;

                int needed = item.MaxDurability - item.CurrentDurability;
                long cost = CalculateRepairCost(item, needed);

                // Apply free repair threshold
                if (IsFreeRepair(item))
                    cost = 0;

                total += cost;
            }
            return total;
        }

        /// <summary>
        /// Gets repair cost for a single item (accounts for free repair threshold).
        /// </summary>
        public long GetRepairCost(InventoryItem item)
        {
            if (item == null || item.CurrentDurability >= item.MaxDurability) return 0;

            int needed = item.MaxDurability - item.CurrentDurability;
            long cost = CalculateRepairCost(item, needed);

            if (IsFreeRepair(item)) cost = 0;
            return cost;
        }

        /// <summary>
        /// Gets detailed cost breakdown for UI tooltip.
        /// </summary>
        public RepairCostBreakdown GetCostBreakdown(InventoryItem item)
        {
            if (item == null) return null;

            var itemData = ItemDatabase.Instance?.GetItem(item.ItemId) as EquipmentData;
            if (itemData == null) return null;

            int needed = item.MaxDurability - item.CurrentDurability;
            if (needed <= 0) return null;

            long baseCostPerPoint = itemData.RepairCostPerDurability > 0
                ? itemData.RepairCostPerDurability
                : _config.BaseRepairCostPerPoint;

            return new RepairCostBreakdown
            {
                ItemId = item.ItemId,
                InstanceId = item.InstanceId,
                DurabilityNeeded = needed,
                BaseCostPerPoint = baseCostPerPoint,
                LevelMultiplier = 1f + (item.Level - 1) * 0.05f,
                RarityMultiplier = itemData.ItemRarity.GetDefaultUpgradeMultiplier(),
                EnhanceMultiplier = 1f + item.EnhanceLevel * 0.1f,
                LimitBreakMultiplier = 1f + item.LimitBreakCount * 0.15f,
                RefineMultiplier = 1f + item.RefineLevel * 0.08f,
                TranscendMultiplier = 1f + item.TranscendLevel * 0.2f,
                EvolutionMultiplier = 1f + item.EvolutionStage * 0.25f,
                AwakeningMultiplier = item.IsAwakened ? 1.5f : 1f,
                MasterworkMultiplier = item.IsMasterwork ? 1.3f : 1f,
                MissingPercentMultiplier = 1f + (1f - item.GetDurabilityPercent()) * 0.2f,
                QualityMultiplier = itemData.QualityMultiplier > 0 ? itemData.QualityMultiplier : 1f,
                TierMultiplier = 1f + itemData.Tier * 0.1f,
                StarMultiplier = 1f + itemData.StarRating * 0.05f,
                CorruptionMultiplier = itemData.CorruptionTier * 0.1f + 1f,
                BrokenMultiplier = item.IsBroken ? 2f : 1f,
                GlobalGrowth = _config.RepairCostGrowth,
                FreeRepairThreshold = _config.FreeRepairThreshold,
                CurrentDurabilityPercent = item.GetDurabilityPercent(),
                IsFree = IsFreeRepair(item)
            };
        }
    }
}
