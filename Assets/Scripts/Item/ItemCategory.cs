using System;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Item category classification for filtering, sorting, and UI grouping.
    /// </summary>
    public enum ItemCategory
    {
        None = 0,
        Equipment = 1,
        Consumable = 2,
        Material = 3,
        Quest = 4,
        Currency = 5,
        Key = 6,
        Chest = 7,
        UpgradeStone = 8,
        SkillBook = 9,
        Rune = 10,
        Gem = 11,
        Skin = 12,
        Pet = 13,
        Artifact = 14,
    }

    /// <summary>
    /// Extension methods for ItemCategory.
    /// </summary>
    public static class ItemCategoryExtensions
    {
        /// <summary>
        /// Gets the display name for the category.
        /// </summary>
        public static string GetDisplayName(this ItemCategory category) => category switch
        {
            ItemCategory.Equipment => "Equipment",
            ItemCategory.Consumable => "Consumable",
            ItemCategory.Material => "Material",
            ItemCategory.Quest => "Quest",
            ItemCategory.Currency => "Currency",
            ItemCategory.Key => "Key",
            ItemCategory.Chest => "Chest",
            ItemCategory.UpgradeStone => "Upgrade Stone",
            ItemCategory.SkillBook => "Skill Book",
            ItemCategory.Rune => "Rune",
            ItemCategory.Gem => "Gem",
            ItemCategory.Skin => "Skin",
            ItemCategory.Pet => "Pet",
            ItemCategory.Artifact => "Artifact",
            _ => "Unknown"
        };

        /// <summary>
        /// Checks if the category is stackable by default.
        /// </summary>
        public static bool IsStackableByDefault(this ItemCategory category) => category switch
        {
            ItemCategory.Consumable => true,
            ItemCategory.Material => true,
            ItemCategory.Currency => true,
            ItemCategory.UpgradeStone => true,
            ItemCategory.Rune => true,
            ItemCategory.Gem => true,
            ItemCategory.Equipment => false,
            ItemCategory.Quest => false,
            ItemCategory.Key => false,
            ItemCategory.Chest => false,
            ItemCategory.SkillBook => false,
            ItemCategory.Skin => false,
            ItemCategory.Pet => false,
            ItemCategory.Artifact => false,
            _ => false
        };

        /// <summary>
        /// Checks if the category can be equipped.
        /// </summary>
        public static bool IsEquippable(this ItemCategory category) => category == ItemCategory.Equipment;

        /// <summary>
        /// Checks if the category can be consumed/used.
        /// </summary>
        public static bool IsConsumable(this ItemCategory category) => category switch
        {
            ItemCategory.Consumable => true,
            ItemCategory.Chest => true,
            ItemCategory.SkillBook => true,
            ItemCategory.UpgradeStone => true,
            _ => false
        };

        /// <summary>
        /// Gets all valid item categories (excludes None).
        /// </summary>
        public static ItemCategory[] GetAllCategories() =>
            (ItemCategory[])Enum.GetValues(typeof(ItemCategory));
    }
}