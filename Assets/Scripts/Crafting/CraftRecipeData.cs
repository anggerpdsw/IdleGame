using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Items.Generation;
using UnityEngine;

namespace IdleDefenseSurvival.Crafting
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
        /// <summary>
        /// Broad item category.
        /// Example: Equipment, Consumable, Material.
        /// </summary>
        public ItemCategory Category = ItemCategory.None;
        /// <summary>
        /// Specific equipment slot/type.
        /// Only relevant when Category represents equipment.
        /// Example: Hat, Weapon, Armor.
        /// </summary>
        public EquipmentType EquipmentType = EquipmentType.None; // Which equipment slot this recipe crafts
        public bool IsEquipment => EquipmentType != EquipmentType.None;
        public int Rarity = 1; // 1=Common, 2=Rare, 3=Epic, 4=Legendary, 5=Mythic, 6=Divine
        public int RecipeVersion = 1; // v3.5 §8.1 — defaults to 1 when JSON omits the field

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
        // NOTE: Deterministic equipment crafting - no RNG result arrays.
        // Reward generated from recipe metadata (Rarity, RequiredTier, EquipmentType).

        // ============ Experience ============
        public long BaseExpReward = 0;
        public long ExpPerAdditionalUnit = 0;

        // ============ Success & Quality ============
        public float BaseSuccessRate = 100f; // Percentage
        public float SuccessRatePerLevel = 0f; // Bonus per crafting level

        // ============ Conditions ============
        public CraftCondition[] Conditions; // Special conditions (time of day, biome, etc.)

        // ============ Unlocking ============
        public bool AutoUnlock = true; // Unlocked automatically when requirements met
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
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(RecipeId)) return false;
            if (string.IsNullOrEmpty(DisplayName)) return false;
            if (Ingredients == null || Ingredients.Length == 0) return false;
            // Deterministic equipment always produces an item via recipe metadata.
            return true;
        }

        public static CraftRecipeData FromGeneration(CraftRecipeData source)
        {
            if (source == null) return null;
            return new CraftRecipeData
            {
                // Identity
                RecipeId = source.RecipeId,
                DisplayName = source.DisplayName,
                Description = source.Description,
                Category = source.Category,
                EquipmentType = source.EquipmentType,
                Rarity = source.Rarity,
                RecipeVersion = source.RecipeVersion,

                // Requirements
                RequiredCraftingLevel = source.RequiredCraftingLevel,
                RequiredQuests = source.RequiredQuests ?? Array.Empty<string>(),
                RequiredTier = source.RequiredTier,
                RequiredRecipes = source.RequiredRecipes ?? Array.Empty<CraftRecipeData>(),

                // Costs
                Ingredients = source.Ingredients,
                GoldCost = source.GoldCost,
                GemCost = source.GemCost,
                AdditionalCosts = source.AdditionalCosts ?? Array.Empty<CurrencyCost>(),

                // Timing
                BaseCraftTime = source.BaseCraftTime,
                TimePerAdditionalUnit = source.TimePerAdditionalUnit,

                // Experience
                BaseExpReward = source.BaseExpReward,
                ExpPerAdditionalUnit = source.ExpPerAdditionalUnit,

                // Success
                BaseSuccessRate = source.BaseSuccessRate,
                SuccessRatePerLevel = source.SuccessRatePerLevel,

                // Conditions
                Conditions = source.Conditions ?? Array.Empty<CraftCondition>(),

                // Unlock
                AutoUnlock = source.AutoUnlock,
                UnlockSource = source.UnlockSource,
                UnlockParameter = source.UnlockParameter,

                // Refund
                RefundPolicy = source.RefundPolicy,

                // Visual / Audio
                RecipeIcon = source.RecipeIcon,
                CraftStartSound = source.CraftStartSound,
                CraftCompleteSound = source.CraftCompleteSound,
                CraftFailSound = source.CraftFailSound,

                // Tags
                Tags = source.Tags ?? Array.Empty<string>()
            };
        }

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
    
    /// <summary>
    /// Save data for CraftRecipeRepository.
    /// </summary>
    [Serializable]
    public class CraftRecipeRepositorySaveData
    {
        public List<string> UnlockedRecipeIds = new();
        public List<string> KnownRecipeIds = new();
    }

    /// <summary>
    /// JSON wrapper for recipe files (root object with SchemaVersion + Recipes array).
    /// Matches the shape of Assets/Resources/Data/Crafting/dataRecipe*.json files.
    ///</summary>
    [Serializable]
    public class RecipeFile
    {
        public int SchemaVersion = 1;
        public List<CraftRecipeData> Recipes = new();
    }
}