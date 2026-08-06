using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items.Generation;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Generates final crafted items with proper levels, stats, and quality.
    /// Uses ItemGenerator for shared item generation logic.
    /// </summary>
    public sealed class CraftRewardService
    {
        private readonly ItemGenerator _itemGenerator;

        public CraftRewardService(ItemGenerator itemGenerator)
        {
            _itemGenerator = itemGenerator;
        }

        // ============ Public API ============
        public InventoryItem[] GenerateRewards(CraftRollResult rollResult, CraftRecipeData recipe, CraftContext context)
        {
            if (!rollResult.Success || rollResult.Entries.Count == 0)
                return Array.Empty<InventoryItem>();

            var items = new List<InventoryItem>();

            foreach (var entry in rollResult.Entries)
            {
                for (int i = 0; i < entry.Count; i++)
                {
                    var item = GenerateSingleItem(entry, recipe, context);
                    if (item != null)
                        items.Add(item);
                }
            }

            return items.ToArray();
        }

        private InventoryItem GenerateSingleItem(CraftResultEntry entry, CraftRecipeData recipe, CraftContext context)
        {
            // Build generation context
            var genContext = new ItemGenerationContext
            {
                Source = ItemSource.Craft,
                RecipeId = recipe.RecipeId,
                PlayerLevel = context.CraftingLevel, // Use crafting level as proxy
                CraftingMastery = context.GetMasteryLevel(recipe.RecipeId),
                BlacksmithLevel = context.BlacksmithLevel,
                EventModifiers = (IReadOnlyList<EventCraftModifier>)context.ActiveEventModifiers,
                Luck = context.Luck,
                ForcedQuality = entry.Quality > 0 ? entry.Quality : -1,
                FixedLevel = entry.FixedLevel > 0 ? entry.FixedLevel : -1,
                FixedEnhance = entry.FixedEnhance > 0 ? entry.FixedEnhance : -1
            };

            return _itemGenerator.GenerateItem(entry.ItemId, genContext);
        }
    }
}