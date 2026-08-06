using System;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Validates if a recipe can be crafted.
    /// Extensible for future requirements (building level, events, etc.)
    /// </summary>
    public sealed class CraftValidator
    {
        private readonly CraftRecipeRepository _repository;
        private readonly IInventoryService _inventory;
        private readonly IEconomyService _economy;
        private readonly SaveManager _saveManager;

        public CraftValidator(CraftRecipeRepository repository, IInventoryService inventory, IEconomyService economy, SaveManager saveManager = null)
        {
            _repository = repository;
            _inventory = inventory;
            _economy = economy;
            _saveManager = saveManager ?? SaveManager.Instance;
        }

        // ============ Public API ============
        public ValidationResult CanCraft(string recipeId, int count = 1)
        {
            // 1. Check recipe exists
            if (!_repository.TryGetRecipe(recipeId, out var recipe))
            {
                return ValidationResult.Fail("Recipe not found");
            }

            // 2. Check recipe is unlocked
            if (!_repository.IsUnlocked(recipeId))
            {
                return ValidationResult.Fail("Recipe not unlocked");
            }

            // 3. Check crafting level requirement
            int playerCraftLevel = GetPlayerCraftLevel();
            if (playerCraftLevel < recipe.RequiredCraftingLevel)
            {
                return ValidationResult.Fail($"Requires crafting level {recipe.RequiredCraftingLevel} (current: {playerCraftLevel})");
            }

            // 4. Check tier requirement
            int currentTier = GetCurrentTier();
            if (currentTier < recipe.RequiredTier)
            {
                return ValidationResult.Fail($"Requires tier {recipe.RequiredTier} (current: {currentTier})");
            }

            // 5. Check prerequisite recipes
            if (recipe.RequiredRecipes != null)
            {
                foreach (var prereq in recipe.RequiredRecipes)
                {
                    if (!_repository.IsUnlocked(prereq.RecipeId))
                    {
                        return ValidationResult.Fail($"Requires recipe '{prereq.DisplayName}' to be unlocked first");
                    }
                }
            }

            // 6. Check quest requirements
            if (recipe.RequiredQuests != null && recipe.RequiredQuests.Length > 0)
            {
                foreach (var questId in recipe.RequiredQuests)
                {
                    if (!IsQuestCompleted(questId))
                    {
                        return ValidationResult.Fail($"Requires quest '{questId}' completion");
                    }
                }
            }

            // 7. Check ingredients (multiply by count)
            if (recipe.Ingredients != null)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    int required = ingredient.Count * count;
                    int have = _inventory.GetTotalQuantity(ingredient.ItemId);

                    // Check quality/level requirements if specified
                    if (ingredient.MinQuality > 0 || ingredient.MinLevel > 0 || ingredient.MinEnhance > 0)
                    {
                        have = CountQualifiedItems(ingredient.ItemId, ingredient.MinQuality, ingredient.MinLevel, ingredient.MinEnhance);
                    }

                    if (have < required)
                    {
                        var itemData = ItemDatabase.Instance?.GetItem(ingredient.ItemId);
                        return ValidationResult.Fail($"Not enough {itemData?.Name ?? ingredient.ItemId} (need {required}, have {have})");
                    }
                }
            }

            // 8. Check gold cost
            long totalGoldCost = recipe.GoldCost * count;
            if (totalGoldCost > 0 && !_economy.HasEnoughCurrency(CurrencyType.Gold, totalGoldCost))
            {
                return ValidationResult.Fail($"Not enough gold (need {totalGoldCost}, have {_economy.GetCurrency(CurrencyType.Gold)})");
            }

            // 9. Check gem cost
            long totalGemCost = recipe.GemCost * count;
            if (totalGemCost > 0 && !_economy.HasEnoughCurrency(CurrencyType.Gem, totalGemCost))
            {
                return ValidationResult.Fail($"Not enough gems (need {totalGemCost}, have {_economy.GetCurrency(CurrencyType.Gem)})");
            }

            // 10. Check additional currency costs
            if (recipe.AdditionalCosts != null)
            {
                foreach (var cost in recipe.AdditionalCosts)
                {
                    long totalCost = cost.Amount * count;
                    if (!_economy.HasEnoughCurrency(cost.Currency, totalCost))
                    {
                        return ValidationResult.Fail($"Not enough {cost.Currency} (need {totalCost}, have {_economy.GetCurrency(cost.Currency)})");
                    }
                }
            }

            // 11. Check special conditions (time of day, biome, weather, etc.)
            if (recipe.Conditions != null)
            {
                foreach (var condition in recipe.Conditions)
                {
                    if (!condition.Check(currentTier, GetCurrentWave(), playerCraftLevel, GetPlayerLuck()))
                    {
                        return ValidationResult.Fail($"Condition not met: {condition.Type}");
                    }
                }
            }

            // 12. Check inventory space for results
            if (!HasInventorySpaceForResults(recipe, count))
            {
                return ValidationResult.Fail("Not enough inventory space for results");
            }

            return ValidationResult.Success();
        }

        // ============ Helper Methods ============
        private int GetPlayerCraftLevel()
        {
            // TODO: Integrate with player progression system
            // For now, return a default or from SaveData
            return 1;
        }

        private int GetCurrentTier()
        {
            return _saveManager != null ? _saveManager.GetHighestUnlockedTier() : 1;
        }

        private int GetCurrentWave()
        {
            // Would integrate with WaveManager
            return 1;
        }

        private long GetPlayerLuck()
        {
            // TODO: Get from player stats
            return 0;
        }

        private bool IsQuestCompleted(string questId)
        {
            // TODO: Integrate with quest system
            return false;
        }

        private int CountQualifiedItems(string itemId, int minQuality, int minLevel, int minEnhance)
        {
            var items = _inventory.GetItemsById(itemId);
            int count = 0;
            foreach (var item in items)
            {
                bool qualityOk = minQuality <= 0 || item.GetRarity() >= (ItemRarity)minQuality;
                bool levelOk = minLevel <= 0 || item.Level >= minLevel;
                bool enhanceOk = minEnhance <= 0 || item.EnhanceLevel >= minEnhance;
                if (qualityOk && levelOk && enhanceOk)
                    count += item.Quantity;
            }
            return count;
        }

        private bool HasInventorySpaceForResults(CraftRecipeData recipe, int count)
        {
            // Estimate max possible results
            int maxItems = 0;
            if (recipe.PossibleResults != null)
            {
                foreach (var result in recipe.PossibleResults)
                {
                    maxItems = Math.Max(maxItems, result.MaxCount);
                }
            }
            if (recipe.GuaranteedResult != null)
            {
                maxItems += recipe.GuaranteedResult.MaxCount;
            }
            maxItems *= count;

            return _inventory.FreeSlots >= maxItems || _inventory.HasSpaceFor("", maxItems);
        }
    }

    /// <summary>
    /// Result of craft validation.
    /// </summary>
    public struct ValidationResult
    {
        public bool IsSuccess;
        public string Reason;

        public static ValidationResult Success() => new() { IsSuccess = true, Reason = string.Empty };
        public static ValidationResult Fail(string reason) => new() { IsSuccess = false, Reason = reason };
    }
}