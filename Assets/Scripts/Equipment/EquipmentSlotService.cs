using System.Collections.Generic;
using IdleDefenseSurvival.Inventory;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Slot state + unlock logic. Owns unlock costs and free-slot defaults.
    /// </summary>
    public sealed class EquipmentSlotService
    {
        public static readonly long[] SlotUnlockCosts = { 0, 100, 250, 500, 1000, 2000, 5000, 10000, 25000, 50000, 100000 };

        private readonly IEquipmentRepository _repo;
        private readonly Dictionary<EquipmentType, EquipmentSlotData> _slotData = new();

        public EquipmentSlotService(IEquipmentRepository repo)
        {
            _repo = repo;
            foreach (EquipmentType slot in EquipmentTypeExtensions.GetAllTypes())
            {
                if (slot == EquipmentType.None) continue;
                _slotData[slot] = new EquipmentSlotData
                {
                    Slot = slot,
                    IsUnlocked = IsDefaultUnlocked(slot),
                    RequiredLevel = 1
                };
            }
        }

        private static bool IsDefaultUnlocked(EquipmentType slot) =>
            slot == EquipmentType.Hat || slot == EquipmentType.Armor || slot == EquipmentType.Pants;

        public IReadOnlyList<EquipmentSlotData> GetAllSlotData()
        {
            var list = new List<EquipmentSlotData>(_slotData.Count);
            foreach (EquipmentType slot in EquipmentTypeExtensions.GetAllTypes())
            {
                if (slot != EquipmentType.None && _slotData.TryGetValue(slot, out var data))
                    list.Add(data);
            }
            return list;
        }

        public bool IsUnlocked(EquipmentType slot) => _repo.IsSlotUnlocked(slot);
        public int UnlockedCount => _repo.UnlockedSlots.Count;

        public long GetUnlockCost(EquipmentType slot)
        {
            int index = slot.GetIndex();
            return index >= 0 && index < SlotUnlockCosts.Length ? SlotUnlockCosts[index] : long.MaxValue;
        }

        public EquipmentType? GetNextUnlockable()
        {
            foreach (EquipmentType slot in EquipmentTypeExtensions.GetAllTypes())
            {
                if (slot != EquipmentType.None && !_repo.IsSlotUnlocked(slot))
                    return slot;
            }
            return null;
        }

        public bool Unlock(EquipmentType slot)
        {
            if (_repo.IsSlotUnlocked(slot)) return true;

            long cost = GetUnlockCost(slot);
            if (!Economy.EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gem, cost, $"Unlock {slot} Slot"))
                return false;

            _repo.SetSlotUnlocked(slot, true);
            if (_slotData.TryGetValue(slot, out var data)) data.IsUnlocked = true;
            return true;
        }

        /// <summary>Resets to default free slots.</summary>
        public void ResetUnlocks()
        {
            var unlocked = new List<EquipmentType>(_repo.UnlockedSlots);
            foreach (var slot in unlocked) _repo.SetSlotUnlocked(slot, false);

            foreach (EquipmentType slot in EquipmentTypeExtensions.GetAllTypes())
            {
                if (slot == EquipmentType.None) continue;
                bool isDefault = IsDefaultUnlocked(slot);
                _repo.SetSlotUnlocked(slot, isDefault);
                if (_slotData.TryGetValue(slot, out var data)) data.IsUnlocked = isDefault;
            }
        }

        public void RestoreUnlocks(IEnumerable<EquipmentType> slots)
        {
            foreach (var slot in slots)
            {
                if (slot == EquipmentType.None) continue;
                _repo.SetSlotUnlocked(slot, true);
                if (_slotData.TryGetValue(slot, out var data)) data.IsUnlocked = true;
            }
        }

        public void BindItem(EquipmentType slot, InventoryItem item)
        {
            if (_slotData.TryGetValue(slot, out var data)) data.EquippedItem = item;
        }

        public EquipmentSlotData GetSlotData(EquipmentType slot)
        {
            _slotData.TryGetValue(slot, out var data);
            return data;
        }
    }
}