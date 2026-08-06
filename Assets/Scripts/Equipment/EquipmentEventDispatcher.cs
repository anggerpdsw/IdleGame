using System;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Equipment event delegate. Equip/unequip/durability dispatch with slot + item.
    /// </summary>
    public delegate void EquipmentTypeItemChanged(EquipmentType slot, Inventory.InventoryItem item);

    /// <summary>
    /// Raises all equipment events. IEquipmentService re-raises these on its own
    /// public events; logic call sites stay event-free.
    /// </summary>
    public sealed class EquipmentEventDispatcher
    {
        public event Action<EquipmentChangedEventArgs> Changed;
        public event EquipmentTypeItemChanged ItemEquipped;
        public event EquipmentTypeItemChanged ItemUnequipped;
        public event Action<EquipmentType> SlotUnlocked;
        public event Action SetBonusChanged;
        public event Action<EquipmentType> DurabilityChanged;

        public void Equipped(EquipmentType slot, Inventory.InventoryItem item, string setId, int setCount)
        {
            ItemEquipped?.Invoke(slot, item);
            Changed?.Invoke(EquipmentChangedEventArgs.CreateEquipped(slot, item, setId, setCount));
        }

        public void Unequipped(EquipmentType slot, Inventory.InventoryItem item, string setId, int setCount)
        {
            ItemUnequipped?.Invoke(slot, item);
            Changed?.Invoke(EquipmentChangedEventArgs.CreateUnequipped(slot, item, setId, setCount));
        }

        public void Swapped(EquipmentType slotA, EquipmentType slotB, Inventory.InventoryItem itemA, Inventory.InventoryItem itemB)
        {
            Changed?.Invoke(EquipmentChangedEventArgs.CreateSwapped(slotA, slotB, itemA, itemB));
        }

        public void NotifySetBonusChanged(string setId, int previousCount, int newCount)
        {
            SetBonusChanged?.Invoke();
            Changed?.Invoke(EquipmentChangedEventArgs.CreateSetBonusChanged(setId, previousCount, newCount));
        }

        public void NotifySlotUnlocked(EquipmentType slot)
        {
            SlotUnlocked?.Invoke(slot);
            Changed?.Invoke(EquipmentChangedEventArgs.CreateSlotUnlocked(slot));
        }

        public void NotifyDurabilityChanged(EquipmentType slot)
        {
            DurabilityChanged?.Invoke(slot);
        }

        public void NotifyBroken(EquipmentType slot, Inventory.InventoryItem item, EquipmentChangeType type)
        {
            Changed?.Invoke(new EquipmentChangedEventArgs
            {
                ChangeType = type,
                Slot = slot,
                PreviousItem = item
            });
        }

        public void ItemDirty(Inventory.InventoryItem item, Inventory.DirtyType dirtyType)
        {
            // Standalone; other equipment events also mark dirty via InventoryService.
        }
    }
}