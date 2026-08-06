using System;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Equipment slot types - exactly 11 slots for the player.
    /// Each slot only accepts items of matching EquipmentType.
    /// </summary>
    public enum EquipmentSlot
    {
        None = 0,
        Hat = 1,
        Gloves = 2,
        Cape = 3,
        Armor = 4,
        Belt = 5,
        Pants = 6,
        Pendant = 7,
        Ring = 8,
        Earring = 9,
        Bracelet = 10,
        Shoes = 11,
    }

    /// <summary>
    /// Extension methods for EquipmentSlot.
    /// </summary>
    public static class EquipmentSlotExtensions
    {
        /// <summary>
        /// Gets all valid equipment slots (excludes None).
        /// </summary>
        public static EquipmentSlot[] GetAllSlots() =>
            (EquipmentSlot[])Enum.GetValues(typeof(EquipmentSlot));

        /// <summary>
        /// Gets the display name for the slot.
        /// </summary>
        public static string GetDisplayName(this EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.Hat => "Hat",
            EquipmentSlot.Gloves => "Gloves",
            EquipmentSlot.Cape => "Cape",
            EquipmentSlot.Armor => "Armor",
            EquipmentSlot.Belt => "Belt",
            EquipmentSlot.Pants => "Pants",
            EquipmentSlot.Pendant => "Pendant",
            EquipmentSlot.Ring => "Ring",
            EquipmentSlot.Earring => "Earring",
            EquipmentSlot.Bracelet => "Bracelet",
            EquipmentSlot.Shoes => "Shoes",
            _ => "Unknown"
        };

        /// <summary>
        /// Gets the slot index (0-10) for array access.
        /// </summary>
        public static int GetIndex(this EquipmentSlot slot) => (int)slot - 1;

        /// <summary>
        /// Checks if the slot is valid (not None).
        /// </summary>
        public static bool IsValid(this EquipmentSlot slot) => slot != EquipmentSlot.None;

        /// <summary>
        /// Total number of equipment slots.
        /// </summary>
        public const int SlotCount = 11;
    }
}