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
        public Rarity Rarity;
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
        public static string GetDisplayName(this Rarity rarity) => rarity switch
        {
            Rarity.Common => "Common",
            Rarity.Rare => "Rare",
            Rarity.Epic => "Epic",
            Rarity.Legendary => "Legendary",
            Rarity.Mythic => "Mythic",
            Rarity.Divine => "Divine",
            _ => "Unknown"
        };

        /// <summary>
        /// Gets the default color for the rarity.
        /// </summary>
        public static Color GetDefaultColor(this Rarity rarity) => rarity switch
        {
            Rarity.Common => GameColors.commonGray,      // Gray
            Rarity.Rare => GameColors.rareBlue,          // Blue
            Rarity.Epic => GameColors.epicPurple,          // Purple
            Rarity.Legendary => GameColors.legendaryOrange,     // Orange
            Rarity.Mythic => GameColors.mythicPink,        // Pink/Magenta
            Rarity.Divine => GameColors.divineGold,        // Gold
            _ => Color.white
        };

        /// <summary>
        /// Default stat multiplier per rarity.
        /// </summary>
        public static float GetDefaultStatMultiplier(this Rarity rarity) => rarity switch
        {
            Rarity.Common => 1.0f,
            Rarity.Rare => 1.35f,
            Rarity.Epic => 1.6f,
            Rarity.Legendary => 2.0f,
            Rarity.Mythic => 2.5f,
            Rarity.Divine => 4.0f,
            _ => 1.0f
        };

        /// <summary>
        /// Default upgrade cost multiplier per rarity.
        /// </summary>
        public static float GetDefaultUpgradeMultiplier(this Rarity rarity) => rarity switch
        {
            Rarity.Common => 1.0f,
            Rarity.Rare => 1.5f,
            Rarity.Epic => 2.0f,
            Rarity.Legendary => 3.0f,
            Rarity.Mythic => 5.0f,
            Rarity.Divine => 12.0f,
            _ => 1.0f
        };

        /// <summary>
        /// Default drop rate per rarity (higher = more common).
        /// </summary>
        public static float GetDefaultDropRate(this Rarity rarity) => rarity switch
        {
            Rarity.Common => 100f,
            Rarity.Rare => 20f,
            Rarity.Epic => 5f,
            Rarity.Legendary => 1f,
            Rarity.Mythic => 0.2f,
            Rarity.Divine => 0.01f,
            _ => 100f
        };

        /// <summary>
        /// Default sell price multiplier per rarity.
        /// </summary>
        public static float GetDefaultSellMultiplier(this Rarity rarity) => rarity switch
        {
            Rarity.Common => 1f,
            Rarity.Rare => 2.5f,
            Rarity.Epic => 5f,
            Rarity.Legendary => 10f,
            Rarity.Mythic => 25f,
            Rarity.Divine => 100f,
            _ => 1f
        };

        /// <summary>
        /// Checks if the rarity is valid (not None).
        /// </summary>
        public static bool IsValid(this Rarity rarity) => rarity != Rarity.None;

        /// <summary>
        /// Gets all valid rarities (excludes None).
        /// </summary>
        public static Rarity[] GetAllRarities() =>
            (Rarity[])Enum.GetValues(typeof(Rarity));

        /// <summary>
        /// Compares two rarities. Returns positive if first is higher.
        /// </summary>
        public static int CompareRarity(Rarity a, Rarity b) => ((int)a).CompareTo((int)b);

        /// <summary>
        /// Returns true if this rarity is higher than the other.
        /// </summary>
        public static bool IsHigherThan(this Rarity a, Rarity b) => (int)a > (int)b;

        /// <summary>
        /// Returns true if this rarity is lower than the other.
        /// </summary>
        public static bool IsLowerThan(this Rarity a, Rarity b) => (int)a < (int)b;
    }
}