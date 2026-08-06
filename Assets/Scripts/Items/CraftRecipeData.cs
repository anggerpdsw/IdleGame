using System;
using UnityEngine;
using IdleDefenseSurvival.Equipment;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Enhanced craft recipe data with support for multiple result types,
    /// crafting conditions, and progression integration.
    /// </summary>
    [Serializable]
    public class CraftRecipeData
    {
        // ============ Identity ============
        public string RecipeId;
        public string DisplayName;
        [TextArea] public string Description;
        public ItemCategory Category = ItemCategory.None;

        // ============ Requirements ============
        public int RequiredCraftingLevel = 1;
        public string[] RequiredQuests;
        public int RequiredTier = 1;
        public CraftRecipeData[] RequiredRecipes; // Prerequisite recipes

        // ============ Costs ============
        public CraftIngredient[] Ingredients;
        public long GoldCost = 0;
        public long GemCost = 0;
        public CurrencyCost[] AdditionalCosts; // Other currencies

        // ============ Timing ============
        public float BaseCraftTime = 0f; // Seconds (0 = instant)
        public float TimePerAdditionalUnit = 0f; // Extra time per unit beyond first

        // ============ Results ============
        public CraftResult[] PossibleResults; // For RNG crafting
        public CraftResult GuaranteedResult;  // Always granted (in addition to RNG)
        public bool Deterministic => PossibleResults == null || PossibleResults.Length == 0;

        // ============ Experience ============
        public long BaseExpReward = 0;
        public long ExpPerAdditionalUnit = 0;

        // ============ Success & Quality ============
        public float BaseSuccessRate = 100f; // Percentage
        public float SuccessRatePerLevel = 0f; // Bonus per crafting level
        public QualityChance[] QualityChances; // Chance for higher quality results

        // ============ Conditions ============
        public CraftCondition[] Conditions; // Special conditions (time of day, biome, etc.)

        // ============ Unlocking ============
        public bool AutoUnlock = false; // Unlocked automatically when requirements met
        public UnlockSource UnlockSource = UnlockSource.None;
        public string UnlockParameter; // Quest ID, tier, etc.

        // ============ Refund Policy ============
        public RecipeRefundPolicy RefundPolicy = RecipeRefundPolicy.ProgressBased;

        // ============ Visual/Audio ============
        public Sprite RecipeIcon;
        public AudioClip CraftStartSound;
        public AudioClip CraftCompleteSound;
        public AudioClip CraftFailSound;

        // ============ Tags ============
        public string[] Tags; // For filtering/searching

        // ============ Validation ============
        public bool IsValid() =>
            !string.IsNullOrEmpty(RecipeId) &&
            !string.IsNullOrEmpty(DisplayName) &&
            (Ingredients == null || Ingredients.Length > 0) &&
            (PossibleResults != null && PossibleResults.Length > 0 || GuaranteedResult != null);
    }

    /// <summary>
    /// Source of recipe unlock.
    /// </summary>
    public enum UnlockSource
    {
        None = 0,
        Default = 1,         // Unlocked by default
        CraftingLevel = 2,   // Unlocked at crafting level
        Quest = 3,           // Unlocked by quest completion
        Tier = 4,            // Unlocked at wave tier
        Discovery = 5,       // Unlocked by discovering item
        Purchase = 6,        // Purchased from shop
        Event = 7,           // Limited-time event
        VIP = 8,             // VIP perk
        Manual = 9,
    }

    /// <summary>
    /// Ingredient for crafting.
    /// </summary>
    [Serializable]
    public class CraftIngredient
    {
        public string ItemId;
        public int Count = 1;
        public bool Consumed = true;           // Whether ingredient is consumed
        public bool CanSubstitute = false;     // Can use alternative items
        public string[] SubstituteItemIds;     // Alternative item IDs
        public int MinQuality = 0;             // Minimum quality required (0 = any)
        public int MinLevel = 0;               // Minimum item level required
        public int MinEnhance = 0;             // Minimum enhance level required
        public bool ReturnOnFail = false;      // Return ingredient if craft fails
    }

    /// <summary>
    /// Result of crafting.
    /// </summary>
    [Serializable]
    public class CraftResult
    {
        public string ItemId;
        public int MinCount = 1;
        public int MaxCount = 1;
        public float Weight = 1f;              // For weighted random selection
        public int MinQuality = 0;             // Minimum quality of result
        public int MaxQuality = 0;             // Maximum quality of result
        public int FixedLevel = 0;             // Fixed level (0 = use recipe level)
        public int FixedEnhance = 0;           // Fixed enhance level
        public bool IsMainResult = false;      // Primary result (for display)
    }

    /// <summary>
    /// Additional currency cost for crafting.
    /// </summary>
    [Serializable]
    public class CurrencyCost
    {
        public CurrencyType Currency;
        public long Amount;
    }

    /// <summary>
    /// Quality chance for RNG quality results.
    /// </summary>
    [Serializable]
    public class QualityChance
    {
        public int QualityLevel;       // Quality tier (1 = Common, 2 = Uncommon, etc.)
        public float BaseChance = 0f;  // Base percentage chance
        public float ChancePerLevel = 0f; // Bonus per crafting level
        public float ChancePerLuck = 0f;  // Bonus per luck stat
    }

    /// <summary>
    /// Condition for crafting availability.
    /// </summary>
    [Serializable]
    public class CraftCondition
    {
        public ConditionType Type;
        public string Parameter;
        public float MinValue;
        public float MaxValue = float.MaxValue;
        public bool Invert = false;

        public bool Check(float value)
        {
            bool result = value >= MinValue && value <= MaxValue;
            return Invert ? !result : result;
        }

        public bool Check(int tier, int wave, int craftingLevel, long luck)
        {
            float value = Type switch
            {
                ConditionType.Tier => tier,
                ConditionType.Wave => wave,
                ConditionType.CraftingLevel => craftingLevel,
                ConditionType.Luck => luck,
                ConditionType.TimeOfDay => GetTimeOfDay(),
                ConditionType.Biome => GetCurrentBiome(),
                ConditionType.Weather => GetCurrentWeather(),
                _ => 0f
            };
            return Check(value);
        }

        private float GetTimeOfDay()
        {
            // 0-24 hour format
            var now = DateTime.Now;
            return now.Hour + now.Minute / 60f;
        }

        private float GetCurrentBiome()
        {
            // Would integrate with world/biome system
            return 0f;
        }

        private float GetCurrentWeather()
        {
            // Would integrate with weather system
            return 0f;
        }
    }

    public enum ConditionType
    {
        None = 0,
        Tier = 1,
        Wave = 2,
        CraftingLevel = 3,
        Luck = 4,
        TimeOfDay = 5,
        Biome = 6,
        Weather = 7,
    }

    /// <summary>
    /// Crafting station data - for station-based crafting (future expansion).
    /// </summary>
    [Serializable]
    public class CraftStationData
    {
        public string StationId;
        public string StationName;
        public EquipmentType StationType; // Which equipment slot this station uses
        public CraftRecipeData[] Recipes; // Recipes exclusive to this station
        public float SpeedMultiplier = 1f;
        public float SuccessRateBonus = 0f;
        public float QualityBonus = 0f;
        public int RequiredLevel = 1;
        public long UpgradeCost = 0;
    }

    /// <summary>
    /// Category for organizing craft recipes.
    /// </summary>
    public enum CraftCategory
    {
        None = 0,
        Weapon = 1,
        Armor = 2,
        Accessory = 3,
        Consumable = 4,
        Material = 5,
        Gem = 6,
        Special = 7,
    }

    /// <summary>
    /// Refund policy for this specific recipe.
    /// </summary>
    public enum RecipeRefundPolicy
    {
        Default = 0,           // Use global policy
        None = 1,              // No refund ever
        Full = 2,              // Always full refund
        ProgressBased = 3,     // Refund based on progress
        HalfAfterHalf = 4,     // Full before 50%, half after 50%, none after 90%
    }
}