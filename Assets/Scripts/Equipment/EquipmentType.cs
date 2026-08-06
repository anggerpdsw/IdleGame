using System;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Extension methods for EquipmentType.
    /// </summary>
    public static class EquipmentTypeExtensions
    {
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
        /// Gets the slot index (0-10) for array access.
        /// </summary>
        public static int GetIndex(this EquipmentType type) => (int)type - 1;

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