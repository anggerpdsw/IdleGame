using System;
using UnityEngine;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Configuration data for a rarity tier.
    /// </summary>
    [Serializable]
    public class ItemRarityConfig
    {
        public ItemRarity Rarity;
        public Color Color = Color.white;
        public float DropRate = 1f;
        public float SellMultiplier = 1f;
        public float UpgradeMultiplier = 1f;
        public float StatMultiplier = 1f;
        public Sprite BorderSprite;
        public GameObject GlowEffect;
        public GameObject ParticleEffect;
        public AudioClip ObtainSound;
    }

    /// <summary>
    /// Extension methods for ItemRarity.
    /// </summary>
    public static class RarityExtensions
    {
        /// <summary>
        /// Gets the display name for the rarity.
        /// </summary>
        public static string GetDisplayName(this ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => "Common",
            ItemRarity.Uncommon => "Uncommon",
            ItemRarity.Rare => "Rare",
            ItemRarity.Epic => "Epic",
            ItemRarity.Legendary => "Legendary",
            ItemRarity.Mythic => "Mythic",
            ItemRarity.Ancient => "Ancient",
            ItemRarity.Divine => "Divine",
            _ => "Unknown"
        };

        /// <summary>
        /// Gets the default color for the rarity.
        /// </summary>
        public static Color GetDefaultColor(this ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => GameColors.commonGray,      // Gray
            ItemRarity.Uncommon => GameColors.uncommonGreen,    // Green
            ItemRarity.Rare => GameColors.rareBlue,          // Blue
            ItemRarity.Epic => GameColors.epicPurple,          // Purple
            ItemRarity.Legendary => GameColors.legendaryOrange,     // Orange
            ItemRarity.Mythic => GameColors.mythicPink,        // Pink/Magenta
            ItemRarity.Ancient => GameColors.ancientPurple,       // Deep Purple
            ItemRarity.Divine => GameColors.divineGold,        // Gold
            _ => Color.white
        };

        /// <summary>
        /// Default stat multiplier per rarity.
        /// </summary>
        public static float GetDefaultStatMultiplier(this ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => 1.0f,
            ItemRarity.Uncommon => 1.15f,
            ItemRarity.Rare => 1.35f,
            ItemRarity.Epic => 1.6f,
            ItemRarity.Legendary => 2.0f,
            ItemRarity.Mythic => 2.5f,
            ItemRarity.Ancient => 3.2f,
            ItemRarity.Divine => 4.0f,
            _ => 1.0f
        };

        /// <summary>
        /// Default upgrade cost multiplier per rarity.
        /// </summary>
        public static float GetDefaultUpgradeMultiplier(this ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => 1.0f,
            ItemRarity.Uncommon => 1.2f,
            ItemRarity.Rare => 1.5f,
            ItemRarity.Epic => 2.0f,
            ItemRarity.Legendary => 3.0f,
            ItemRarity.Mythic => 5.0f,
            ItemRarity.Ancient => 8.0f,
            ItemRarity.Divine => 12.0f,
            _ => 1.0f
        };

        /// <summary>
        /// Default drop rate per rarity (higher = more common).
        /// </summary>
        public static float GetDefaultDropRate(this ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => 100f,
            ItemRarity.Uncommon => 50f,
            ItemRarity.Rare => 20f,
            ItemRarity.Epic => 5f,
            ItemRarity.Legendary => 1f,
            ItemRarity.Mythic => 0.2f,
            ItemRarity.Ancient => 0.05f,
            ItemRarity.Divine => 0.01f,
            _ => 100f
        };

        /// <summary>
        /// Default sell price multiplier per rarity.
        /// </summary>
        public static float GetDefaultSellMultiplier(this ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => 1f,
            ItemRarity.Uncommon => 1.5f,
            ItemRarity.Rare => 2.5f,
            ItemRarity.Epic => 5f,
            ItemRarity.Legendary => 10f,
            ItemRarity.Mythic => 25f,
            ItemRarity.Ancient => 50f,
            ItemRarity.Divine => 100f,
            _ => 1f
        };

        /// <summary>
        /// Checks if the rarity is valid (not None).
        /// </summary>
        public static bool IsValid(this ItemRarity rarity) => rarity != ItemRarity.None;

        /// <summary>
        /// Gets all valid rarities (excludes None).
        /// </summary>
        public static ItemRarity[] GetAllRarities() =>
            (ItemRarity[])Enum.GetValues(typeof(ItemRarity));

        /// <summary>
        /// Compares two rarities. Returns positive if first is higher.
        /// </summary>
        public static int CompareRarity(ItemRarity a, ItemRarity b) => ((int)a).CompareTo((int)b);

        /// <summary>
        /// Returns true if this rarity is higher than the other.
        /// </summary>
        public static bool IsHigherThan(this ItemRarity a, ItemRarity b) => (int)a > (int)b;

        /// <summary>
        /// Returns true if this rarity is lower than the other.
        /// </summary>
        public static bool IsLowerThan(this ItemRarity a, ItemRarity b) => (int)a < (int)b;
    }
}