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
        Enhance = 2,         // Enhancement (+1, +2, +3... up to +15/+20)
        LimitBreak = 3,      // Limit break (breaks max level cap)
        Refine = 4,          // Refinement (improves stat quality)
        Awaken = 5,          // Awakening (unlocks new effects)
        Transcend = 6,       // Transcendence (major power spike)
        Evolution = 7,       // Evolution (changes equipment tier/form)
        Masterwork = 8,      // Masterwork (perfect stats, cosmetic)
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
        RNGFailed,          // Enhance attempt rolled a failure
        Destroyed           // Enhance failed AND item was destroyed
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
            ItemLevelType.Enhance => "Enhance",
            ItemLevelType.LimitBreak => "Limit Break",
            ItemLevelType.Refine => "Refine",
            ItemLevelType.Awaken => "Awaken",
            ItemLevelType.Transcend => "Transcend",
            ItemLevelType.Evolution => "Evolution",
            ItemLevelType.Masterwork => "Masterwork",
            _ => "Unknown"
        };

        /// <summary>
        /// Gets the short display name for UI.
        /// </summary>
        public static string GetShortName(this ItemLevelType type) => type switch
        {
            ItemLevelType.Level => "Lv",
            ItemLevelType.Enhance => "+",
            ItemLevelType.LimitBreak => "LB",
            ItemLevelType.Refine => "Ref",
            ItemLevelType.Awaken => "Awk",
            ItemLevelType.Transcend => "Trn",
            ItemLevelType.Evolution => "Evo",
            ItemLevelType.Masterwork => "MW",
            _ => "?"
        };

        /// <summary>
        /// Checks if this level type uses a numeric value (like Level 5, Enhance +10).
        /// </summary>
        public static bool UsesNumericValue(this ItemLevelType type) => type switch
        {
            ItemLevelType.Level => true,
            ItemLevelType.Enhance => true,
            ItemLevelType.LimitBreak => true,
            ItemLevelType.Refine => true,
            ItemLevelType.Awaken => false, // Usually boolean (awakened/not)
            ItemLevelType.Transcend => true,
            ItemLevelType.Evolution => true,
            ItemLevelType.Masterwork => false, // Usually boolean
            _ => false
        };

        /// <summary>
        /// Gets the default max value for this level type.
        /// </summary>
        public static int GetDefaultMaxValue(this ItemLevelType type) => type switch
        {
            ItemLevelType.Level => 100,
            ItemLevelType.Enhance => 20,
            ItemLevelType.LimitBreak => 5,
            ItemLevelType.Refine => 10,
            ItemLevelType.Awaken => 1,
            ItemLevelType.Transcend => 3,
            ItemLevelType.Evolution => 4,
            ItemLevelType.Masterwork => 1,
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