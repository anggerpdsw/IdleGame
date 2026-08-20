using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Durability damage + repair. Broken detection returns true so the
    /// orchestrator can run the unequip transaction in one place.
    /// </summary>
    public sealed class EquipmentDurabilityService
    {
        private readonly IEquipmentRepository _repo;
        private readonly EquipmentEventDispatcher _events;

        public EquipmentDurabilityService(IEquipmentRepository repo, EquipmentEventDispatcher events)
        {
            _repo = repo;
            _events = events;
        }

        /// <summary>Returns true when the item broke and should be unequipped.</summary>
        public bool DamageDurability(EquipmentType slot, int amount)
        {
            if (!_repo.TryGetEquipped(slot, out var item)) return false;

            item.DamageDurability(amount);
            _events.NotifyDurabilityChanged(slot);

            if (item.IsBroken)
                _events.NotifyBroken(slot, item, EquipmentChangeType.Broken);

            return item.IsBroken;
        }

        public long RepairSlot(EquipmentType slot)
        {
            if (!_repo.TryGetEquipped(slot, out var item)) return 0;

            var itemData = ItemDatabase.Instance?.GetItem(item.EquipmentTemplateId) as EquipmentData;
            if (itemData == null) return 0;

            int needed = item.MaxDurability - item.CurrentDurability;
            if (needed <= 0) return 0;

            long costPerPoint = itemData.RepairCostPerDurability > 0 ? itemData.RepairCostPerDurability : 10;
            long totalCost = needed * costPerPoint;

            if (!EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gold, totalCost, $"Repair {slot}"))
                return 0;

            item.Repair(needed);
            _events.NotifyDurabilityChanged(slot);
            return totalCost;
        }

        public long RepairAll()
        {
            long total = 0;
            foreach (var slot in _repo.EquippedItems.Keys)
                total += RepairSlot(slot);
            return total;
        }

        public long GetTotalRepairCost()
        {
            long total = 0;
            foreach (var (slot, item) in _repo.EquippedItems)
            {
                var itemData = ItemDatabase.Instance?.GetItem(item.ItemId) as EquipmentData;
                if (itemData == null) continue;

                int needed = item.MaxDurability - item.CurrentDurability;
                if (needed <= 0) continue;
                long costPerPoint = itemData.RepairCostPerDurability > 0 ? itemData.RepairCostPerDurability : 10;
                total += needed * costPerPoint;
            }
            return total;
        }
    }
}