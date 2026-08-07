using System.Collections.Generic;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Modifiers;
using IdleDefenseSurvival;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Backend contract for equipment state. Implemented by EquipmentService.
    /// Lets sub-services receive data + issue state changes without singleton access.
    /// </summary>
    public interface IEquipmentRepository
    {
        IReadOnlyDictionary<EquipmentType, InventoryItem> EquippedItems { get; }

        /// <summary>Build profile (All/Tank/Warrior/Mage/Assassin) steering auto-equip attribute weights.</summary>
        BuildProfile BuildProfile { get; }

        bool TryGetEquipped(EquipmentType slot, out InventoryItem item);
        void SetEquipped(EquipmentType slot, InventoryItem item);
        bool RemoveEquipped(EquipmentType slot, out InventoryItem item);

        bool IsSlotUnlocked(EquipmentType slot);
        void SetSlotUnlocked(EquipmentType slot, bool unlocked);
        IReadOnlyCollection<EquipmentType> UnlockedSlots { get; }

        void UpdateSetPieceCount(string setId, int newCount);
        int GetSetPieceCount(string setId);
        void ClearSetCounts();
        IEnumerable<string> ActiveSetIds { get; }
        IReadOnlyDictionary<string, int> SnapshotSetCounts();

        void AddActiveEffect(IEquipmentEffect effect);
        void RemoveActiveEffect(IEquipmentEffect effect);
        void ClearActiveEffects();
        IReadOnlyList<IEquipmentEffect> ActiveEffects { get; }
    }
}
