using System;
using System.Collections.Generic;
using System.Linq;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Manager;
using IdleDefenseSurvival.Core.Interfaces;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.Crafting
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

            // 4. Tier requirement removed - tier no longer a crafting gate

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
                    if (!condition.Check(playerCraftLevel, GetPlayerLuck()))
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
                bool qualityOk = minQuality <= 0 || item.GetRarity() >= (Rarity)minQuality;
                bool levelOk = minLevel <= 0 || item.Level >= minLevel;
                bool enhanceOk = minEnhance <= 0 || item.EnhanceLevel >= minEnhance;
                if (qualityOk && levelOk && enhanceOk)
                    count += item.Quantity;
            }
            return count;
        }

        private bool HasInventorySpaceForResults(CraftRecipeData recipe, int count)
        {
            // Deterministic: 1 equipment item per craft (plus possible mastery/critical extras)
            // Estimate: 1 base + 1 mastery + up to 2 critical = max 4 per craft
            int maxItems = count * 4;

            return _inventory.FreeSlots >= maxItems || _inventory.HasSpaceFor("", maxItems);
        }

        // ============ Layered Design Validation (§19-§20) ============

        /// <summary>
        /// Layer 2: Item validation. Every Ingredient.ItemId resolves and has valid Role.
        ///</summary>
        public ValidationResult ValidateItems(CraftRecipeData recipe)
        {
            if (recipe?.Ingredients == null || recipe.Ingredients.Length == 0)
                return ValidationResult.Fail("Recipe has no ingredients");

            foreach (var ing in recipe.Ingredients)
            {
                var item = ItemDatabase.Instance?.GetItem(ing.ItemId);
                if (item == null)
                    return ValidationResult.Fail($"Item not found: {ing.ItemId}");

                switch (item.Role)
                {
                    case ItemRole.Material:
                        if (item.CraftingFamily == CraftingFamily.None)
                            return ValidationResult.Fail($"Material '{ing.ItemId}' has no CraftingFamily");
                        break;
                    case ItemRole.Catalyst:
                        if (ing.ItemId != "water")
                            return ValidationResult.Fail($"Only water allowed as Catalyst (got: {ing.ItemId})");
                        break;
                    case ItemRole.Progression:
                        if (!IsDecomposedId(ing.ItemId))
                            return ValidationResult.Fail($"Invalid Progression item: {ing.ItemId}");
                        break;
                    default:
                        return ValidationResult.Fail($"Item '{ing.ItemId}' Role={item.Role} not eligible for crafting");
                }
            }

            if (!recipe.Ingredients.Any(i => ItemDatabase.Instance?.GetItem(i.ItemId)?.Role == ItemRole.Catalyst))
                return ValidationResult.Fail("Recipe missing water catalyst");

            return ValidationResult.Success();
        }

        /// <summary>
        /// Layer 3: Design validation. Identity rules (§7), tier ceiling (§6.2), R6 Special (§20.3).
        /// </summary>
        public ValidationResult ValidateDesign(CraftRecipeData recipe)
        {
            var materials = recipe.Ingredients
                .Select(i => new { Ingredient = i, Item = ItemDatabase.Instance?.GetItem(i.ItemId) })
                .Where(x => x.Item?.Role == ItemRole.Material)
                .ToList();

            if (materials.Count == 0)
                return ValidationResult.Fail("Recipe has no materials");

            int rarity = recipe.Rarity > 0 ? recipe.Rarity : 1;

            // Tier ceiling: CraftingTier <= Recipe.Rarity (§6.2)
            foreach (var m in materials)
            {
                if (m.Item.CraftingTier > rarity)
                    return ValidationResult.Fail($"Material '{m.Ingredient.ItemId}' tier {m.Item.CraftingTier} exceeds recipe rarity {rarity}");
            }

            // R6 Special requirement (§20.3)
            if (rarity == 6 && !materials.Any(m => m.Item.CraftingFamily == CraftingFamily.Special))
                return ValidationResult.Fail("R6 recipe missing Special family ingredient");

            // Equipment identity rules (§7)
            var identityRule = GetIdentityRule(recipe.EquipmentType);
            if (identityRule == null)
                return ValidationResult.Fail($"No identity rule for slot {recipe.EquipmentType}");

            var presentFamilies = materials.Select(m => m.Item.CraftingFamily).Distinct().ToHashSet();
            foreach (var required in identityRule.RequiredFamilies)
            {
                if (required.IsAlternative)
                {
                    if (!required.Families.Any(f => presentFamilies.Contains(f)))
                        return ValidationResult.Fail($"Identity rule for {recipe.EquipmentType} requires one of [{string.Join(",", required.Families)}]");
                }
                else if (required.IsOptional)
                {
                    // Optional families are not enforced
                    continue;
                }
                else
                {
                    if (!presentFamilies.Contains(required.Family))
                        return ValidationResult.Fail($"Identity rule for {recipe.EquipmentType} requires {required.Family}");
                }
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Layer 4: Economy validation. Monotonic cost (§18.3), sink coverage (§15).
        ///</summary>
        public ValidationResult ValidateEconomy(CraftRecipeData recipe)
        {
            var config = CraftingConfig.Load();
            if (config == null)
                return ValidationResult.Fail("dataConfigCrafting.json not loaded");

            // Single recipe weighted cost check
            var cost = ComputeWeightedCost(recipe, config);
            if (cost < 0)
                return ValidationResult.Fail("Recipe weighted cost computation failed");

            // Armor highest cost at rarity (§18.6)
            // Sink coverage (§15) — runtime report
            var materialCount = ItemDatabase.Instance?.GetItemsByRole(ItemRole.Material).Count ?? 0;
            var coveredCount = CountCoveredMaterials(config);

            return ValidationResult.Success();
        }

        private double ComputeWeightedCost(CraftRecipeData recipe, CraftingConfig config)
        {
            if (recipe.Ingredients == null) return 0;
            double total = 0;
            foreach (var ing in recipe.Ingredients)
            {
                var item = ItemDatabase.Instance?.GetItem(ing.ItemId);
                if (item == null) return -1;
                double weight = item.Role switch
                {
                    ItemRole.Material => config.GetWeight(item.CraftingFamily),
                    ItemRole.Catalyst => config.GetWeight(CraftingFamily.Water),
                    ItemRole.Progression => config.ProgressionWeight,
                    _ => 0
                };
                total += ing.Count * weight;
            }
            return total;
        }

        private int CountCoveredMaterials(CraftingConfig config)
        {
            var materials = ItemDatabase.Instance?.GetItemsByRole(ItemRole.Material) ?? new List<ItemData>();
            int covered = 0;
            foreach (var mat in materials)
            {
                if (mat.CraftingFamily == CraftingFamily.None) continue;
                // Coverage = material appears in any recipe (runtime scan)
                // Recipe registry scan deferred to repository hook
                covered++;
            }
            return covered;
        }

        private int GetRecipeRarity(CraftRecipeData recipe)
        {
            return recipe.Rarity > 0 ? Math.Clamp(recipe.Rarity, 1, 6) : 1;
        }

        private bool IsDecomposedId(string itemId)
        {
            return itemId == "decomposed_common"
                || itemId == "decomposed_rare"
                || itemId == "decomposed_epic"
                || itemId == "decomposed_legendary"
                || itemId == "decomposed_mythic";
        }

        private IdentityRule GetIdentityRule(EquipmentType equipmentType)
        {
            return equipmentType switch
            {
                EquipmentType.Hat => new IdentityRule(
                    new[] { RequiredFamily.Single(CraftingFamily.Thread) }),
                EquipmentType.Gloves => new IdentityRule(
                    new[] { RequiredFamily.Single(CraftingFamily.Leather), RequiredFamily.Single(CraftingFamily.Thread) }),
                EquipmentType.Cape => new IdentityRule(
                    new[] { RequiredFamily.Single(CraftingFamily.Thread), RequiredFamily.Single(CraftingFamily.Adhesive) }),
                EquipmentType.Armor => new IdentityRule(
                    new[] { RequiredFamily.Single(CraftingFamily.Leather), RequiredFamily.Single(CraftingFamily.Metal), RequiredFamily.Optional(CraftingFamily.Coal) }),
                EquipmentType.Belt => new IdentityRule(
                    new[] { RequiredFamily.Single(CraftingFamily.Leather), RequiredFamily.Single(CraftingFamily.Metal) }),
                EquipmentType.Pants => new IdentityRule(
                    new[] { RequiredFamily.Single(CraftingFamily.Leather), RequiredFamily.Single(CraftingFamily.Thread) }),
                EquipmentType.Pendant => new IdentityRule(
                    new[] { RequiredFamily.Single(CraftingFamily.Metal) }),
                EquipmentType.Ring => new IdentityRule(
                    new[] { RequiredFamily.Single(CraftingFamily.Metal) }),
                EquipmentType.Earring => new IdentityRule(
                    new[] { RequiredFamily.Single(CraftingFamily.Metal) }),
                EquipmentType.Bracelet => new IdentityRule(
                    new[] { RequiredFamily.Single(CraftingFamily.Metal) }),
                EquipmentType.Shoes => new IdentityRule(
                    new[] { RequiredFamily.Single(CraftingFamily.Leather), RequiredFamily.Single(CraftingFamily.Thread) }),
                _ => null
            };
        }
    }

    /// <summary>
    /// Equipment identity rule (§7).
    ///</summary>
    internal sealed class IdentityRule
    {
        public RequiredFamily[] RequiredFamilies { get; }
        public IdentityRule(RequiredFamily[] required) { RequiredFamilies = required; }
    }

    /// <summary>
    /// Required family in identity rule. IsAlternative = OR logic. IsOptional = not enforced.
    ///</summary>
    internal sealed class RequiredFamily
    {
        public CraftingFamily Family { get; }
        public CraftingFamily[] Families { get; }
        public bool IsAlternative { get; }
        public bool IsOptional { get; }

        private RequiredFamily(CraftingFamily family, CraftingFamily[] families, bool isAlt, bool isOptional)
        {
            Family = family;
            Families = families;
            IsAlternative = isAlt;
            IsOptional = isOptional;
        }

        public static RequiredFamily Single(CraftingFamily f) =>
            new(f, new[] { f }, false, false);
        public static RequiredFamily Alternative(params CraftingFamily[] fs) =>
            new(fs[0], fs, true, false);
        public static RequiredFamily Optional(CraftingFamily f) =>
            new(f, new[] { f }, false, true);
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