using System;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Equipment type - matches EquipmentSlot exactly.
    /// Each equipment item has one EquipmentType that determines which slot it fits in.
    /// </summary>
    public enum EquipmentType
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
    /// Extension methods for EquipmentType.
    /// </summary>
    public static class EquipmentTypeExtensions
    {
        /// <summary>
        /// Converts EquipmentType to the corresponding EquipmentSlot.
        /// </summary>
        public static EquipmentSlot ToSlot(this EquipmentType type) => (EquipmentType)type switch
        {
            EquipmentType.Hat => EquipmentSlot.Hat,
            EquipmentType.Gloves => EquipmentSlot.Gloves,
            EquipmentType.Cape => EquipmentSlot.Cape,
            EquipmentType.Armor => EquipmentSlot.Armor,
            EquipmentType.Belt => EquipmentSlot.Belt,
            EquipmentType.Pants => EquipmentSlot.Pants,
            EquipmentType.Pendant => EquipmentSlot.Pendant,
            EquipmentType.Ring => EquipmentSlot.Ring,
            EquipmentType.Earring => EquipmentSlot.Earring,
            EquipmentType.Bracelet => EquipmentSlot.Bracelet,
            EquipmentType.Shoes => EquipmentSlot.Shoes,
            _ => EquipmentSlot.None
        };

        /// <summary>
        /// Converts EquipmentSlot to the corresponding EquipmentType.
        /// </summary>
        public static EquipmentType ToType(this EquipmentSlot slot) => (EquipmentSlot)slot switch
        {
            EquipmentSlot.Hat => EquipmentType.Hat,
            EquipmentSlot.Gloves => EquipmentType.Gloves,
            EquipmentSlot.Cape => EquipmentType.Cape,
            EquipmentSlot.Armor => EquipmentType.Armor,
            EquipmentSlot.Belt => EquipmentType.Belt,
            EquipmentSlot.Pants => EquipmentType.Pants,
            EquipmentSlot.Pendant => EquipmentType.Pendant,
            EquipmentSlot.Ring => EquipmentType.Ring,
            EquipmentSlot.Earring => EquipmentType.Earring,
            EquipmentSlot.Bracelet => EquipmentType.Bracelet,
            EquipmentSlot.Shoes => EquipmentType.Shoes,
            _ => EquipmentType.None
        };

        /// <summary>
        /// Gets the display name for the equipment type.
        /// </summary>
        public static string GetDisplayName(this EquipmentType type) => type switch
        {
            EquipmentType.Hat => "Hat",
            EquipmentType.Gloves => "Gloves",
            EquipmentType.Cape => "Cape",
            EquipmentType.Armor => "Armor",
            EquipmentType.Belt => "Belt",
            EquipmentType.Pants => "Pants",
            EquipmentType.Pendant => "Pendant",
            EquipmentType.Ring => "Ring",
            EquipmentType.Earring => "Earring",
            EquipmentType.Bracelet => "Bracelet",
            EquipmentType.Shoes => "Shoes",
            _ => "Unknown"
        };

        /// <summary>
        /// Checks if the type is valid (not None).
        /// </summary>
        public static bool IsValid(this EquipmentType type) => type != EquipmentType.None;

        /// <summary>
        /// Gets all valid equipment types (excludes None).
        /// </summary>
        public static EquipmentType[] GetAllTypes() =>
            (EquipmentType[])Enum.GetValues(typeof(EquipmentType));
    }
}