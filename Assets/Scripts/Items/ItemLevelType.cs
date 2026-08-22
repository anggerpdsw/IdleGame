using System;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Equipment progression types - different ways to upgrade equipment.
    /// Each type affects stats differently and has different requirements.
    /// </summary>
    public enum ItemLevelType
    {
        None = 0,
        Level = 1,           // Basic level progression (1, 2, 3...)
        LimitBreak = 2,      // Limit break (breaks max level cap)
    }

    /// <summary>
    /// Why an upgrade attempt failed. UI uses this to show a precise failure message
    /// instead of guessing from a generic success=false.
    /// </summary>
    public enum UpgradeFailReason
    {
        None = 0,
        NotEnoughGold,      // Insufficient gold to pay the upgrade cost
        NotEnoughGem,       // Insufficient gems to pay the gem cost
        MaxLevel,           // This upgrade type is already at its max value
        RequirementNotMet,  // A prerequisite is missing (awaken first, max level first, etc.)
        Destroyed           // failed level up AND item was destroyed
    }

    /// <summary>
    /// Extension methods for ItemLevelType.
    /// </summary>
    public static class ItemLevelTypeExtensions
    {
        /// <summary>
        /// Gets the display name for the level type.
        /// </summary>
        public static string GetDisplayName(this ItemLevelType type) => type switch
        {
            ItemLevelType.Level => "Level",
            ItemLevelType.LimitBreak => "Limit Break",
            _ => "Unknown"
        };

        /// <summary>
        /// Gets the short display name for UI.
        /// </summary>
        public static string GetShortName(this ItemLevelType type) => type switch
        {
            ItemLevelType.Level => "Lv",
            ItemLevelType.LimitBreak => "LB",
            _ => "?"
        };

        /// <summary>
        /// Checks if this level type uses a numeric value (like Level 5, LimitBreak LB).
        /// </summary>
        public static bool UsesNumericValue(this ItemLevelType type) => type switch
        {
            ItemLevelType.Level => true,
            ItemLevelType.LimitBreak => true,
            _ => false
        };

        /// <summary>
        /// Gets the default max value for this level type.
        /// </summary>
        public static int GetDefaultMaxValue(this ItemLevelType type) => type switch
        {
            ItemLevelType.Level => 100,
            ItemLevelType.LimitBreak => 5,
            _ => 0
        };

        /// <summary>
        /// Checks if the type is valid (not None).
        /// </summary>
        public static bool IsValid(this ItemLevelType type) => type != ItemLevelType.None;

        /// <summary>
        /// Gets all valid level types (excludes None).
        /// </summary>
        public static ItemLevelType[] GetAllTypes() =>
            (ItemLevelType[])Enum.GetValues(typeof(ItemLevelType));
    }
}