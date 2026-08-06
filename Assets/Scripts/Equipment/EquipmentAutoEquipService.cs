using System.Linq;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Auto-equip: chooses best candidate per empty slot by composite stat score.
    /// </summary>
    public sealed class EquipmentAutoEquipService
    {
        private readonly IEquipmentRepository _repo;

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
                var bonuses = EquipmentStatCalculator.GetItemBonusesWithSet(candidate, ItemDatabase.Instance, _repo.SnapshotSetCounts());
                // Attributes are the core power source — weight them higher than raw combat stats.
                var attrBonuses = EquipmentStatCalculator.GetItemAttributeBonuses(candidate);
                float score = bonuses.Values.Sum() + attrBonuses.Values.Sum() * 2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }
    }
}