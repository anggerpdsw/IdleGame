using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Inventory;
using UnityEngine;

namespace IdleDefenseSurvival.UI.Equipment
{
    /// <summary>Visual state of an equipment slot.</summary>
    public enum EquipmentSlotState
    {
        Locked = 0,
        Empty = 1,
        Occupied = 2
    }

    /// <summary>
    /// Data-only view model for an equipment slot.
    /// The view applies it without touching any service or database.
    /// </summary>
    public class EquipmentSlotViewData
    {
        public EquipmentSlotState State = EquipmentSlotState.Empty;

        public Sprite Icon;
        public bool ShowIcon;
        public Color BorderColor = Color.white;
        public bool ShowBorder;

        public string Level;
        public bool MaxLevel;

        public float Durability;
        public bool ShowDurability;
        public Color DurabilityColor = Color.white;

        public bool ShowSetBonusGlow;

        // Unlock (poin 10)
        public EquipmentSlotUnlockState UnlockState = EquipmentSlotUnlockState.Unlocked;
        public bool ShowUnlockButton;
        public long UnlockCost;
        public string UnlockLabel = string.Empty;

        /// <summary>Item being shown, if any. Passed through for tooltips / compare.</summary>
        public InventoryItem ReferenceItem;
    }
}