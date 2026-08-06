using System.Collections.Generic;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Modifiers;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Backend contract for equipment state. Implemented by EquipmentService.
    /// Lets sub-services receive data + issue state changes without singleton access.
    /// </summary>
    public interface IEquipmentRepository
    {
        IReadOnlyDictionary<EquipmentSlot, InventoryItem> EquippedItems { get; }

        bool TryGetEquipped(EquipmentSlot slot, out InventoryItem item);
        void SetEquipped(EquipmentSlot slot, InventoryItem item);
        bool RemoveEquipped(EquipmentSlot slot, out InventoryItem item);

        bool IsSlotUnlocked(EquipmentSlot slot);
        void SetSlotUnlocked(EquipmentSlot slot, bool unlocked);
        IReadOnlyCollection<EquipmentSlot> UnlockedSlots { get; }

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
