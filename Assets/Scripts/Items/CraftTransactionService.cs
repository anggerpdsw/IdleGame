using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Core;
using IdleDefenseSurvival.Items.Decomposition;
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
        private readonly CraftTransactionJournal _journal;
        private readonly ISaveService _saveService;

        // Pending transaction state
        private readonly List<ReservedMaterial> _reservedMaterials = new();
        private readonly Dictionary<CurrencyType, long> _reservedCurrency = new();
        private bool _committed = false;
        private bool _rolledBack = false;
        private Guid _journalEntryId = Guid.Empty; // P0-C: journal entry tracking

        public CraftTransactionService(
            IInventoryService inventory,
            IEconomyService economy,
            CraftTransactionJournal journal,
            ISaveService saveService)
        {
            _inventory = inventory;
            _economy = economy;
            _journal = journal;
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

            // P0-B step 10: reserve decomposed requirements
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
                    _reservedMaterials.Add(new ReservedMaterial { ItemId = entry.ItemId, Count = entry.Count, MinQuality = 0, MinLevel = 0, MinEnhance = 0 });
                }
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

        /// <summary>
        /// Canonical P0-C entry point. Same reservation semantics as the 2-param legacy overload,
        /// plus journal append (Prepared) + durable checkpoint AFTER successful reservation.
        /// Old 2-param overload retained for the legacy caller until 5C-1 replaces it.
        ///</summary>
        public TransactionResult BeginTransaction(
            string jobId,
            CraftRecipeData recipe,
            CraftExecutionSnapshot snapshot,
            int count = 1)
        {
            if (string.IsNullOrEmpty(jobId))
                return TransactionResult.Fail("jobId required");
            if (recipe == null)
                return TransactionResult.Fail("recipe required");
            if (snapshot == null)
                return TransactionResult.Fail("snapshot required");

            Reset();

            // Validate first
            var validation = ValidateRecipe(recipe, count);
            if (!validation.IsSuccess)
            {
                return TransactionResult.Fail(validation.Reason);
            }

            // Reserve materials (P0-B verbatim)
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

                // P0-B step 10: reserve decomposed requirements
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
                        _reservedMaterials.Add(new ReservedMaterial { ItemId = entry.ItemId, Count = entry.Count, MinQuality = 0, MinLevel = 0, MinEnhance = 0 });
                    }
                }
            }

            // Reserve currency (P0-B verbatim)
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

            // P0-C: Append journal entry (Prepared) + durable checkpoint AFTER successful reservation
            var ops = BuildOperations(snapshot);
            _journalEntryId = _journal.AppendEntry(jobId, snapshot, ops);
            _saveService.PersistCurrentStateDurably();

            return TransactionResult.Success();
        }

        public TransactionResult Commit()
        {
            if (_committed || _rolledBack)
                return TransactionResult.Fail("Transaction already completed");

            // Actually consume reserved materials
            foreach (var reserved in _reservedMaterials)
            {
                int removed = _inventory.RemoveItemById(reserved.ItemId, reserved.Count);
                if (removed < reserved.Count)
                {
                    // This shouldn't happen if reservation worked, but handle gracefully
                    Rollback();
                    return TransactionResult.Fail($"Failed to consume {reserved.ItemId}: only {removed}/{reserved.Count} removed");
                }
            }

            // Actually spend reserved currency
            foreach (var kvp in _reservedCurrency)
            {
                bool spent = _economy.TrySpendCurrency(kvp.Key, kvp.Value, "Craft commit");
                if (!spent)
                {
                    Rollback();
                    return TransactionResult.Fail($"Failed to spend {kvp.Key}: {kvp.Value}");
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

                    if (ingredient.MinQuality > 0 || ingredient.MinLevel > 0 || ingredient.MinEnhance > 0)
                    {
                        available = CountQualifiedItems(ingredient.ItemId, ingredient.MinQuality, ingredient.MinLevel, ingredient.MinEnhance);
                    }

                    if (available < required)
                    {
                        return ValidationResult.Fail($"Not enough {ingredient.ItemId}: need {required}, have {available}");
                    }
                }

            // P0-B step 10: validate decomposed requirements (R2-R6 only)
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
            // In a more complex system, we could "mark" items as reserved.
            int available = _inventory.GetTotalQuantity(itemId);

            if (ingredient.MinQuality > 0 || ingredient.MinLevel > 0 || ingredient.MinEnhance > 0)
            {
                available = CountQualifiedItems(itemId, ingredient.MinQuality, ingredient.MinLevel, ingredient.MinEnhance);
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
                MinEnhance = ingredient.MinEnhance
            });

            return ReserveResult.Success();
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

        private void Reset()
        {
            _reservedMaterials.Clear();
            _reservedCurrency.Clear();
            _committed = false;
            _rolledBack = false;
            _journalEntryId = Guid.Empty;
        }

        // ============ P0-C: Operation Builders ============

        private static CraftJournalOperation[] BuildOperations(CraftExecutionSnapshot snapshot)
        {
            var ops = new List<CraftJournalOperation>();
            var cost = snapshot.Cost;

            foreach (var m in cost.Materials ?? Array.Empty<IngredientCost>())
                ops.Add(MakeOp(ResourceKind.Material, m.ItemId, m.Count));

            foreach (var c in cost.Catalysts ?? Array.Empty<IngredientCost>())
                ops.Add(MakeOp(ResourceKind.Catalyst, c.ItemId, c.Count));

            foreach (var p in cost.Progression ?? Array.Empty<IngredientCost>())
                ops.Add(MakeOp(ResourceKind.Progression, p.ItemId, p.Count));

            if (cost.Currency.GoldSnapshot > 0)
                ops.Add(MakeOp(ResourceKind.Currency, "Gold", cost.Currency.GoldSnapshot));
            if (cost.Currency.GemSnapshot > 0)
                ops.Add(MakeOp(ResourceKind.Currency, "Gem", cost.Currency.GemSnapshot));
            foreach (var ac in cost.Currency.AdditionalCosts ?? Array.Empty<CostEntry>())
                ops.Add(MakeOp(ResourceKind.Currency, ac.CurrencyId, ac.Amount));

            return ops.ToArray();
        }

        private static CraftJournalOperation MakeOp(ResourceKind kind, string resourceId, long quantity)
        {
            ValidateQuantity(resourceId, kind, quantity);
            return new CraftJournalOperation
            {
                CraftTransactionOperationId = Guid.NewGuid(),
                ResourceType = kind,
                ResourceId = resourceId,
                Quantity = (int)quantity,
                State = OperationState.Pending
            };
        }

        private static void ValidateQuantity(string resourceId, ResourceKind kind, long value)
        {
            if (value < 0)
                throw new InvalidOperationException($"Negative quantity for {kind}:{resourceId} = {value}");
            if (value > int.MaxValue)
                throw new InvalidOperationException($"Quantity overflow int for {kind}:{resourceId} = {value}");
        }

        // ============ Internal Classes ============
        private class ReservedMaterial
        {
            public string ItemId;
            public int Count;
            public int MinQuality;
            public int MinLevel;
            public int MinEnhance;
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