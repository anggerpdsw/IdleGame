using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Inventory;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Equipment save state export + hard reset. LoadFromSaveData lives on the
    /// orchestrator because re-equipping must flow through EquipInternal
    /// (slots, set counts, effects, modifiers stay in sync).
    /// </summary>
    public sealed class EquipmentPersistenceService
    {
        private readonly IEquipmentRepository _repo;
        private readonly EquipmentSlotService _slots;
        private readonly EquipmentEventDispatcher _events;

        public EquipmentPersistenceService(IEquipmentRepository repo, EquipmentSlotService slots, EquipmentEventDispatcher events)
        {
            _repo = repo;
            _slots = slots;
            _events = events;
        }

        public EquipmentSaveData GetSaveData()
        {
            var equippedData = _repo.EquippedItems
                .Select(kvp => new EquipmentInstanceIdData { Slot = kvp.Key, InstanceId = kvp.Value?.InstanceId })
                .Where(d => !string.IsNullOrEmpty(d.InstanceId))
                .ToArray();

            var unlockedData = _repo.UnlockedSlots
                .Select(s => new UnlockedSlotData { Slot = s, IsUnlocked = true })
                .ToArray();

            return new EquipmentSaveData
            {
                EquippedItems = equippedData,
                UnlockedSlots = unlockedData,
                LastModifiedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        /// <summary>Hard reset: unequip everything, clear sets/effects, default slots.</summary>
        public void Reset()
        {
            // Orchestrator calls UnequipAll before Reset; defensive reopen here.
            foreach (var slot in new List<EquipmentType>(_repo.EquippedItems.Keys))
                _repo.RemoveEquipped(slot, out _);

            _repo.ClearSetCounts();
            _repo.ClearActiveEffects();
            _slots.ResetUnlocks();
        }
    }
}