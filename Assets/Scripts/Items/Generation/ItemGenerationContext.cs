using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Crafting;

namespace IdleDefenseSurvival.Items.Generation
{
    /// <summary>
    /// Immutable context for item generation.
    /// Uses class with private setters for builder pattern.
    /// </summary>
    public sealed class ItemGenerationContext
    {
        public ItemSource Source { get; set; } = ItemSource.Craft;
        public string RecipeId { get; set; }
        public int PlayerLevel { get; set; } = 1;
        public int CraftingMastery { get; set; } = 0;
        public int BlacksmithLevel { get; set; } = 0;
        public IReadOnlyList<EventCraftModifier> EventModifiers { get; set; }
        public long Luck { get; set; } = 0;
        public int? ForcedQuality { get; set; } = null;
        public int? FixedLevel { get; set; } = null;
        public int? FixedEnhance { get; set; } = null;
        public int? Seed { get; set; } = null;
        public int Tier { get; set; } = 1;
        public int Wave { get; set; } = 1;
        public float RarityBoost { get; set; } = 0f;
        public EquipmentType EquipmentType { get; set; } = EquipmentType.None;
        public GemType GemType { get; set; } = GemType.None;
        public ItemCategory Category { get; set; } = ItemCategory.None;
        public IReadOnlyDictionary<string, object> CustomData { get; set; }

        public ItemGenerationContext() { }

        /// <summary>
        /// Creates a context for crafting.
        /// </summary>
        public static ItemGenerationContext Craft(
            string recipeId,
            int playerLevel,
            int craftingMastery,
            int blacksmithLevel,
            long luck,
            IReadOnlyList<EventCraftModifier> eventModifiers = null,
            int? forcedQuality = null,
            int? fixedLevel = null,
            int? fixedEnhance = null,
            int? seed = null)
        {
            return new ItemGenerationContext
            {
                Source = ItemSource.Craft,
                RecipeId = recipeId,
                PlayerLevel = playerLevel,
                CraftingMastery = craftingMastery,
                BlacksmithLevel = blacksmithLevel,
                Luck = luck,
                EventModifiers = eventModifiers ?? Array.Empty<EventCraftModifier>(),
                ForcedQuality = forcedQuality,
                FixedLevel = fixedLevel,
                FixedEnhance = fixedEnhance,
                Seed = seed
            };
        }

        /// <summary>
        /// Creates a context for loot drops.
        /// </summary>
        public static ItemGenerationContext Drop(
            int tier,
            int wave,
            float rarityBoost = 0f,
            long luck = 0,
            int? seed = null)
        {
            return new ItemGenerationContext
            {
                Source = ItemSource.Drop,
                Tier = tier,
                Wave = wave,
                RarityBoost = rarityBoost,
                Luck = luck,
                Seed = seed
            };
        }

        /// <summary>
        /// Creates a context for reward generation.
        /// </summary>
        public static ItemGenerationContext Reward(
            int playerLevel,
            int tier,
            int wave,
            long luck = 0,
            int? seed = null)
        {
            return new ItemGenerationContext
            {
                Source = ItemSource.Reward,
                PlayerLevel = playerLevel,
                Tier = tier,
                Wave = wave,
                Luck = luck,
                Seed = seed
            };
        }

        /// <summary>
        /// Creates a context for specific equipment generation.
        /// </summary>
        public static ItemGenerationContext Equipment(
            EquipmentType equipmentType,
            Rarity rarity,
            int level,
            int tier = 1,
            int? seed = null)
        {
            return new ItemGenerationContext
            {
                Source = ItemSource.Craft,
                EquipmentType = equipmentType,
                Category = ItemCategory.Equipment,
                PlayerLevel = level,
                Tier = tier,
                ForcedQuality = (int)rarity,
                FixedLevel = level,
                Seed = seed
            };
        }

        /// <summary>
        /// Creates a context for specific gem generation.
        /// </summary>
        public static ItemGenerationContext Gem(
            GemType gemType,
            Rarity rarity,
            int level,
            int? seed = null)
        {
            return new ItemGenerationContext
            {
                Source = ItemSource.Craft,
                GemType = gemType,
                Category = ItemCategory.Gem,
                PlayerLevel = level,
                ForcedQuality = (int)rarity,
                FixedLevel = level,
                Seed = seed
            };
        }

        /// <summary>
        /// Creates a derived context with modifications.
        /// </summary>
        public ItemGenerationContext With(
            ItemSource? source = null,
            string recipeId = null,
            int? playerLevel = null,
            int? craftingMastery = null,
            int? blacksmithLevel = null,
            IReadOnlyList<EventCraftModifier> eventModifiers = null,
            long? luck = null,
            int? forcedQuality = null,
            int? fixedLevel = null,
            int? fixedEnhance = null,
            int? seed = null,
            int? tier = null,
            int? wave = null,
            float? rarityBoost = null,
            EquipmentType? equipmentType = null,
            GemType? gemType = null,
            ItemCategory? category = null,
            IReadOnlyDictionary<string, object> customData = null)
        {
            return new ItemGenerationContext
            {
                Source = source ?? Source,
                RecipeId = recipeId ?? RecipeId,
                PlayerLevel = playerLevel ?? PlayerLevel,
                CraftingMastery = craftingMastery ?? CraftingMastery,
                BlacksmithLevel = blacksmithLevel ?? BlacksmithLevel,
                EventModifiers = eventModifiers ?? EventModifiers,
                Luck = luck ?? Luck,
                ForcedQuality = forcedQuality ?? ForcedQuality,
                FixedLevel = fixedLevel ?? FixedLevel,
                FixedEnhance = fixedEnhance ?? FixedEnhance,
                Seed = seed ?? Seed,
                Tier = tier ?? Tier,
                Wave = wave ?? Wave,
                RarityBoost = rarityBoost ?? RarityBoost,
                EquipmentType = equipmentType ?? EquipmentType,
                GemType = gemType ?? GemType,
                Category = category ?? Category,
                CustomData = customData ?? CustomData
            };
        }
    }
}