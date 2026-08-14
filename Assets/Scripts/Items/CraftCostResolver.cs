using System;
using System.Linq;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Pure cost resolver for craft currency computation.
    /// Extracts the deterministic cost calculation from CraftSnapshotBuilder.Build()
    /// (lines 55-58) so it can be used by both the transaction path and a
    /// read-only preview API without invoking the impure snapshot builder
    /// (which generates a CompletionSeed via RNG).
    /// </summary>
    public static class CraftCostResolver
    {
        /// <summary>
        /// Compute the currency cost snapshot for a recipe and count.
        /// Mirrors the exact formula in CraftSnapshotBuilder.Build():
        ///   Gold = recipe.GoldCost * count
        ///   Gem = recipe.GemCost * count
        ///   AdditionalCosts preserved verbatim scaled by count
        /// Deterministic; no RNG, no singleton access, no side effects.
        /// </summary>
        public static CurrencySnapshot ComputeCurrencyCost(CraftRecipeData recipe, int count)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (count < 1) count = 1;

            var additionalCosts = recipe.AdditionalCosts != null
                ? Array.ConvertAll(recipe.AdditionalCosts, c => new CostEntry
                {
                    CurrencyId = c.Currency.ToString(),
                    Amount = c.Amount * count
                })
                : Array.Empty<CostEntry>();

            return new CurrencySnapshot(recipe.GoldCost * count, recipe.GemCost * count, additionalCosts);
        }
    }
}
