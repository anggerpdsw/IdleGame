using System.Collections.Generic;
using IdleDefenseSurvival;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Stats;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Slot state + unlock logic. Owns unlock costs and free-slot defaults.
    /// </summary>
    public sealed class EquipmentSlotService
    {
        public static readonly long[] SlotUnlockCosts = { 0, 20, 50, 80, 110, 140, 170, 200, 230, 260, 300 };

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
            int playerLevel = PlayerStatsManager.Instance?.GetStatInt(SkillType.HealthPoint) ?? 1;
            var list = new List<EquipmentSlotData>(_slotData.Count);
            foreach (EquipmentType slot in EquipmentTypeExtensions.GetAllTypes())
            {
                if (slot != EquipmentType.None && _slotData.TryGetValue(slot, out var data))
                {
                    data.UnlockState = ComputeUnlockState(data, playerLevel);
                    list.Add(data);
                }
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
            if (_slotData.TryGetValue(slot, out var data))
            {
                data.IsUnlocked = true;
                data.UnlockState = EquipmentSlotUnlockState.Unlocked;
            }
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

        /// <summary>
        /// Computes the current unlock gate for a slot.
        /// Priority: quest &gt; level &gt; gold. Called on unlock/reload; UI just reads it.
        /// </summary>
        public static EquipmentSlotUnlockState ComputeUnlockState(EquipmentSlotData data, int playerLevel)
        {
            if (data.IsUnlocked) return EquipmentSlotUnlockState.Unlocked;

            if (!string.IsNullOrEmpty(data.RequiredQuest)) return EquipmentSlotUnlockState.LockedByQuest;
            if (data.RequiredLevel > 1 && playerLevel < data.RequiredLevel) return EquipmentSlotUnlockState.LockedByLevel;
            return EquipmentSlotUnlockState.LockedByGold;
        }
    }
}