using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Auto-equip: chooses best candidate per empty slot by attribute-weighted score.
    /// Attributes drive ~80% of item value; combat stats ~20%. Rarity/sockets/passives
    /// factor in so a higher-rarity item (with a mechanic) outweighs raw numbers.
    /// Anti-power-creep: score is level-relative so a low-level legendary never
    /// auto-beats a high-level common in the same slot.
    /// </summary>
    public sealed class EquipmentAutoEquipService
    {
        private readonly IEquipmentRepository _repo;

        // Build weights for specialization stats (tuned to ~20% share).
        // Derived combat stats (AttackDamage, HealthPoint, CriticalDamage, ...) come
        // from Main Attribute (×80 weight below), so only SecondaryStat routes here.
        private static readonly Dictionary<SecondaryStat, float> StatWeights = new()
        {
            { SecondaryStat.LifeSteal, 1.3f },
            { SecondaryStat.MoveSpeed, 0.5f },
            { SecondaryStat.CooldownReduction, 0.8f },
            { SecondaryStat.BossDamage, 1.5f },
            { SecondaryStat.EliteDamage, 1.2f },
            { SecondaryStat.BounceChance, 1.0f },
            { SecondaryStat.BounceCount, 1.0f },
            { SecondaryStat.AttackRange, 0.8f },
            { SecondaryStat.MultiShootChance, 1.2f },
            { SecondaryStat.KnockbackChance, 0.8f },
            { SecondaryStat.GoldGain, 1.0f },
            { SecondaryStat.DropRate, 1.0f },
        };

        public EquipmentAutoEquipService(IEquipmentRepository repo)
        {
            _repo = repo;
        }

        /// <param name="canEquip">Orchestrator CanEquip(item, slot) — validation lives there.</param>
        /// <param name="equip">Orchestrator Equip(item, slot) — the actual transaction.</param>
        /// <returns>Count of items equipped.</returns>
        public int AutoEquipBest(System.Func<InventoryItem, EquipmentType, bool> canEquip,
            System.Func<Inventory.InventoryItem, EquipmentType, bool> equip)
        {
            var inventory = InventoryService.Instance;
            if (inventory == null) return 0;

            int equipped = 0;
            foreach (EquipmentType slot in EquipmentTypeExtensions.GetAllTypes())
            {
                if (slot == EquipmentType.None) continue;
                if (_repo.EquippedItems.ContainsKey(slot) || !_repo.IsSlotUnlocked(slot)) continue;

                var best = GetBestItemForSlot(slot, canEquip);
                if (best == null) continue;

                if (equip(best, slot)) equipped++;
            }
            return equipped;
        }

        public InventoryItem GetBestItemForSlot(EquipmentType slot,
            System.Func<InventoryItem, EquipmentType, bool> canEquip = null)
        {
            var inventory = InventoryService.Instance;
            if (inventory == null) return null;

            var candidates = inventory.GetEquipmentsByType(slot)
                .Where(i => !i.IsEquipped && (canEquip == null || canEquip(i, slot)))
                .ToList();

            if (candidates.Count == 0) return null;

            InventoryItem best = null;
            float bestScore = float.MinValue;

            foreach (var candidate in candidates)
            {
                float score = Evaluate(candidate, slot);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>Attribute-weighted score. Attr ×4 = 80/20 split; normalize by level.</summary>
        public float Evaluate(InventoryItem item, EquipmentType slot)
        {
            if (item == null) return float.MinValue;

            var db = ItemDatabase.Instance;
            var equipmentData = item.GetEquipmentData();
            bool hasPassive = equipmentData != null && RarityMechanicConfig.HasPassive(equipmentData.ItemRarity);
            var attrBonuses = EquipmentStatCalculator.GetItemAttributeBonuses(item);
            var setCounts = _repo.SnapshotSetCounts();

            // ~80% share via per-build attribute weights (Main Attribute -> derived
            // SkillTypes). BuildProfile decides which attribute is worth most —
            // BuildProfile.All keeps every attribute at ×1 (flat equivalence).
            var attrWeights = AttributeWeightsConfig.ForBuild(_repo.BuildProfile);
            float score = 0f;
            foreach (var (attr, value) in attrBonuses)
                score += value * attrWeights.WeightFor(attr);

            // ~20% share: combat stats via per-build weights.
            var bonuses = EquipmentStatCalculator.GetItemBonusesWithSet(item, db, setCounts);
            foreach (var (stat, value) in bonuses)
                score += value * StatWeights.GetValueOrDefault(stat, 0.3f);

            // Small terms so rarity won't dominate but mechanics still count.
            if (hasPassive)
                score += 1.0f;    // passive = mechanic bonus, not raw power
            int sockets = item.Sockets?.Length ?? 0;
            score += sockets * 0.5f; // sockets = gem opportunity; value lives in comparison UI

            // Level normalization: higher level = higher raw, don't blind-select highest rarity.
            int level = System.Math.Max(item.Level, 1);
            score /= (float)System.Math.Sqrt(level);

            return score;
        }
    }
}