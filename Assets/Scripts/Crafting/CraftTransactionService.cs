using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Crafting;
using IdleDefenseSurvival.Core.Interfaces;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Handles atomic transactions for crafting - reserve, commit, or rollback.
    /// Ensures consistency: either all resources are consumed or none are.
    /// </summary>
    public sealed class CraftTransactionService
    {
        private readonly IInventoryService _inventory;
        private readonly IEconomyService _economy;
        private readonly ISaveService _saveService;

        // Pending transaction state
        private readonly List<ReservedMaterial> _reservedMaterials = new();
        private readonly Dictionary<CurrencyType, long> _reservedCurrency = new();
        private bool _committed = false;
        private bool _rolledBack = false;

        public CraftTransactionService(
            IInventoryService inventory,
            IEconomyService economy,
            ISaveService saveService)
        {
            _inventory = inventory;
            _economy = economy;
            _saveService = saveService;
        }

        // ============ Public API ============
        public TransactionResult BeginTransaction(CraftRecipeData recipe, int count = 1)
        {
            Reset();

            // Validate first
            var validation = ValidateRecipe(recipe, count);
            if (!validation.IsSuccess)
            {
                return TransactionResult.Fail(validation.Reason);
            }

            // Reserve materials
            if (recipe.Ingredients != null)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    if (!ingredient.Consumed) continue;

                    int required = ingredient.Count * count;
                    var reserved = ReserveMaterial(ingredient.ItemId, required, ingredient);
                    if (!reserved.IsSuccess)
                    {
                        Rollback();
                        return TransactionResult.Fail($"Failed to reserve {ingredient.ItemId}: {reserved.Reason}");
                    }
                }
            }

            // ponytail: decomposed requirements removed — already injected into recipe.Ingredients by CraftRecipeRepository.cs:111-158
            // Potion recipes get potion_hp_1/potion_mp_1; equipment recipes get decomposed_* materials.
            // Double-injection caused validation failure when player had hidden ingredient but not decomposed material.

            // Reserve currency via resolver (includes base gold/meat scaled by rarity)
            var costSnapshot = CraftCostResolver.ComputeCurrencyCost(recipe, count);

            if (costSnapshot.GoldSnapshot > 0)
            {
                if (!_economy.HasEnoughCurrency(CurrencyType.Gold, costSnapshot.GoldSnapshot))
                {
                    Rollback();
                    return TransactionResult.Fail($"Insufficient gold: need {costSnapshot.GoldSnapshot}");
                }
                _reservedCurrency[CurrencyType.Gold] = costSnapshot.GoldSnapshot;
            }

            if (costSnapshot.GemSnapshot > 0)
            {
                if (!_economy.HasEnoughCurrency(CurrencyType.Gem, costSnapshot.GemSnapshot))
                {
                    Rollback();
                    return TransactionResult.Fail($"Insufficient gems: need {costSnapshot.GemSnapshot}");
                }
                _reservedCurrency[CurrencyType.Gem] = costSnapshot.GemSnapshot;
            }

            foreach (var entry in costSnapshot.AdditionalCosts)
            {
                if (!Enum.TryParse<CurrencyType>(entry.CurrencyId, out var currencyType))
                    continue;
                if (entry.Amount <= 0) continue;
                if (!_economy.HasEnoughCurrency(currencyType, entry.Amount))
                {
                    Rollback();
                    return TransactionResult.Fail($"Insufficient {currencyType}: need {entry.Amount}");
                }
                _reservedCurrency[currencyType] = entry.Amount;
            }

            return TransactionResult.Success();
        }

        
        public TransactionResult Commit()
        {
            if (_committed || _rolledBack)
                return TransactionResult.Fail("Transaction already completed");

            // Consume materials
            foreach (var material in _reservedMaterials)
            {
                int removed = _inventory.RemoveItemById(material.ItemId, material.Count);
                if (removed != material.Count)
                {
                    throw new InvalidOperationException(
                        $"Remove failed: {material.ItemId} " +
                        $"got {removed}/{material.Count}");
                }
            }

            // Spend currency
            foreach (var kvp in _reservedCurrency)
            {
                if (!_economy.TrySpendCurrency(kvp.Key, kvp.Value, "Craft commit"))
                {
                    throw new InvalidOperationException(
                        $"Spend failed: {kvp.Key} " +
                        $"amount={kvp.Value}");
                }
            }

            _committed = true;
            return TransactionResult.Success();
        }

        public void Rollback()
        {
            if (_committed || _rolledBack) return;

            // Release material reservations (just clear the list - materials were never actually removed)
            _reservedMaterials.Clear();
            _reservedCurrency.Clear();
            _rolledBack = true;
        }

        // ============ Private Methods ============
        private ValidationResult ValidateRecipe(CraftRecipeData recipe, int count)
        {
            // Check ingredients availability
            if (recipe.Ingredients != null)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    if (!ingredient.Consumed) continue;

                    int required = ingredient.Count * count;
                    int available = _inventory.GetTotalQuantity(ingredient.ItemId);

                    if (ingredient.MinQuality > 0 || ingredient.MinLevel > 0)
                    {
                        available = CountQualifiedItems(ingredient.ItemId, ingredient.MinQuality, ingredient.MinLevel);
                    }

                    if (available < required)
                    {
                        return ValidationResult.Fail($"Not enough {ingredient.ItemId}: need {required}, have {available}");
                    }
                }
            }

            // ponytail: decomposed validation removed — recipe.Ingredients already complete after repository injection

            // Check currency via resolver (matches reservation logic)
            var costSnapshot = CraftCostResolver.ComputeCurrencyCost(recipe, count);

            if (costSnapshot.GoldSnapshot > 0)
            {
                if (!_economy.HasEnoughCurrency(CurrencyType.Gold, costSnapshot.GoldSnapshot))
                    return ValidationResult.Fail($"Not enough gold: need {costSnapshot.GoldSnapshot}");
            }

            if (costSnapshot.GemSnapshot > 0)
            {
                if (!_economy.HasEnoughCurrency(CurrencyType.Gem, costSnapshot.GemSnapshot))
                    return ValidationResult.Fail($"Not enough gems: need {costSnapshot.GemSnapshot}");
            }

            foreach (var entry in costSnapshot.AdditionalCosts)
            {
                if (!System.Enum.TryParse<CurrencyType>(entry.CurrencyId, out var currencyType))
                    continue;

                if (entry.Amount <= 0) continue;

                if (!_economy.HasEnoughCurrency(currencyType, entry.Amount))
                    return ValidationResult.Fail($"Not enough {currencyType}: need {entry.Amount}");
            }

            return ValidationResult.Success();
        }

        private ReserveResult ReserveMaterial(string itemId, int count, CraftIngredient ingredient)
        {
            // For now, just validate availability. Actual removal happens on Commit.
            int available = _inventory.GetTotalQuantity(itemId);

            if (ingredient.MinQuality > 0 || ingredient.MinLevel > 0)
            {
                available = CountQualifiedItems(itemId, ingredient.MinQuality, ingredient.MinLevel);
            }

            if (available < count)
            {
                return ReserveResult.Fail($"Not enough {itemId}: need {count}, have {available}");
            }

            _reservedMaterials.Add(new ReservedMaterial
            {
                ItemId = itemId,
                Count = count,
                MinQuality = ingredient.MinQuality,
                MinLevel = ingredient.MinLevel,
            });

            return ReserveResult.Success();
        }

        private int CountQualifiedItems(string itemId, int minQuality, int minLevel)
        {
            var items = _inventory.GetItemsById(itemId);
            int count = 0;
            foreach (var item in items)
            {
                bool qualityOk = minQuality <= 0 || item.GetRarity() >= (Rarity)minQuality;
                bool levelOk = minLevel <= 0 || item.Level >= minLevel;
                if (qualityOk && levelOk)
                    count += item.Quantity;
            }
            return count;
        }

        private void Reset()
        {
            _reservedMaterials.Clear();
            _reservedCurrency.Clear();
            _committed = false;
            _rolledBack = false;
        }

        // ============ Internal Classes ============
        private class ReservedMaterial
        {
            public string ItemId;
            public int Count;
            public int MinQuality;
            public int MinLevel;
        }

        private struct ReserveResult
        {
            public bool IsSuccess;
            public string Reason;

            public static ReserveResult Success() => new() { IsSuccess = true, Reason = string.Empty };
            public static ReserveResult Fail(string reason) => new() { IsSuccess = false, Reason = reason };
        }
    }

    /// <summary>
    /// Result of a craft transaction.
    /// </summary>
    public struct TransactionResult
    {
        public bool IsSuccess;
        public string Reason;

        public static TransactionResult Success() => new() { IsSuccess = true, Reason = string.Empty };
        public static TransactionResult Fail(string reason) => new() { IsSuccess = false, Reason = reason };
    }
}