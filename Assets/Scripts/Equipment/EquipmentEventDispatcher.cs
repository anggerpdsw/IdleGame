using System;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Equipment event delegate. Equip/unequip/durability dispatch with slot + item.
    /// </summary>
    public delegate void EquipmentSlotItemChanged(EquipmentSlot slot, Inventory.InventoryItem item);

    /// <summary>
    /// Raises all equipment events. IEquipmentService re-raises these on its own
    /// public events; logic call sites stay event-free.
    /// </summary>
    public sealed class EquipmentEventDispatcher
    {
        public event Action<EquipmentChangedEventArgs> Changed;
        public event EquipmentSlotItemChanged ItemEquipped;
        public event EquipmentSlotItemChanged ItemUnequipped;
        public event Action<EquipmentSlot> SlotUnlocked;
        public event Action SetBonusChanged;
        public event Action<EquipmentSlot> DurabilityChanged;

        public void Equipped(EquipmentSlot slot, Inventory.InventoryItem item, string setId, int setCount)
        {
            ItemEquipped?.Invoke(slot, item);
            Changed?.Invoke(EquipmentChangedEventArgs.CreateEquipped(slot, item, setId, setCount));
        }

        public void Unequipped(EquipmentSlot slot, Inventory.InventoryItem item, string setId, int setCount)
        {
            ItemUnequipped?.Invoke(slot, item);
            Changed?.Invoke(EquipmentChangedEventArgs.CreateUnequipped(slot, item, setId, setCount));
        }

        public void Swapped(EquipmentSlot slotA, EquipmentSlot slotB, Inventory.InventoryItem itemA, Inventory.InventoryItem itemB)
        {
            Changed?.Invoke(EquipmentChangedEventArgs.CreateSwapped(slotA, slotB, itemA, itemB));
        }

        public void NotifySetBonusChanged(string setId, int previousCount, int newCount)
        {
            SetBonusChanged?.Invoke();
            Changed?.Invoke(EquipmentChangedEventArgs.CreateSetBonusChanged(setId, previousCount, newCount));
        }

        public void NotifySlotUnlocked(EquipmentSlot slot)
        {
            SlotUnlocked?.Invoke(slot);
            Changed?.Invoke(EquipmentChangedEventArgs.CreateSlotUnlocked(slot));
        }

        public void NotifyDurabilityChanged(EquipmentSlot slot)
        {
            DurabilityChanged?.Invoke(slot);
        }

        public void NotifyBroken(EquipmentSlot slot, Inventory.InventoryItem item, EquipmentChangeType type)
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