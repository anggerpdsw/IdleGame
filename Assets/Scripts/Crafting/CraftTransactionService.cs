using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Crafting;
using IdleDefenseSurvival.Core.Interfaces;
using IdleDefenseSurvival.Items.Decomposition;

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

            // Reserve decomposed requirements (always, independent of regular ingredients)
            var decomposedReqsReserve = DecomposedRequirementResolver.Compute(recipe.Rarity);
            if (decomposedReqsReserve.Count > 0)
            {
                var decomposedScaledReserve = DecomposedRequirementAggregator.SumPerJob(decomposedReqsReserve, count);
                for (int dr = 0; dr < decomposedScaledReserve.Count; dr++)
                {
                    var entry = decomposedScaledReserve[dr];
                    int have = _inventory.GetTotalQuantity(entry.ItemId);
                    if (have < entry.Count)
                    {
                        Rollback();
                        return TransactionResult.Fail($"Failed to reserve {entry.ItemId}: need {entry.Count}, have {have}");
                    }
                    _reservedMaterials.Add(new ReservedMaterial { ItemId = entry.ItemId, Count = entry.Count, MinQuality = 0, MinLevel = 0 });
                }
            }

            // Reserve currency
            if (recipe.GoldCost > 0)
            {
                long cost = recipe.GoldCost * count;
                if (!_economy.HasEnoughCurrency(CurrencyType.Gold, cost))
                {
                    Rollback();
                    return TransactionResult.Fail($"Insufficient gold: need {cost}");
                }
                _reservedCurrency[CurrencyType.Gold] = cost;
            }

            if (recipe.GemCost > 0)
            {
                long cost = recipe.GemCost * count;
                if (!_economy.HasEnoughCurrency(CurrencyType.Gem, cost))
                {
                    Rollback();
                    return TransactionResult.Fail($"Insufficient gems: need {cost}");
                }
                _reservedCurrency[CurrencyType.Gem] = cost;
            }

            if (recipe.AdditionalCosts != null)
            {
                foreach (var cost in recipe.AdditionalCosts)
                {
                    long totalCost = cost.Amount * count;
                    if (!_economy.HasEnoughCurrency(cost.Currency, totalCost))
                    {
                        Rollback();
                        return TransactionResult.Fail($"Insufficient {cost.Currency}: need {totalCost}");
                    }
                    _reservedCurrency[cost.Currency] = totalCost;
                }
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

            // Validate decomposed requirements (R2-R6 only) - ALWAYS, independent of regular ingredients
            var decomposedReqsValidate = DecomposedRequirementResolver.Compute(recipe.Rarity);
            if (decomposedReqsValidate.Count > 0)
            {
                var decomposedScaledValidate = DecomposedRequirementAggregator.SumPerJob(decomposedReqsValidate, count);
                for (int dv = 0; dv < decomposedScaledValidate.Count; dv++)
                {
                    int need = decomposedScaledValidate[dv].Count;
                    int have = _inventory.GetTotalQuantity(decomposedScaledValidate[dv].ItemId);
                    if (have < need)
                        return ValidationResult.Fail($"Not enough {decomposedScaledValidate[dv].ItemId}: need {need}, have {have}");
                }
            }

            // Check currency
            if (recipe.GoldCost > 0)
            {
                long cost = recipe.GoldCost * count;
                if (!_economy.HasEnoughCurrency(CurrencyType.Gold, cost))
                    return ValidationResult.Fail($"Not enough gold: need {cost}");
            }

            if (recipe.GemCost > 0)
            {
                long cost = recipe.GemCost * count;
                if (!_economy.HasEnoughCurrency(CurrencyType.Gem, cost))
                    return ValidationResult.Fail($"Not enough gems: need {cost}");
            }

            if (recipe.AdditionalCosts != null)
            {
                foreach (var cost in recipe.AdditionalCosts)
                {
                    long totalCost = cost.Amount * count;
                    if (!_economy.HasEnoughCurrency(cost.Currency, totalCost))
                        return ValidationResult.Fail($"Not enough {cost.Currency}: need {totalCost}");
                }
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