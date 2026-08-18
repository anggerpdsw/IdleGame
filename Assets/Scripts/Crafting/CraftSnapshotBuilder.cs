using System;
using IdleDefenseSurvival.Items.Decomposition;
using IdleDefenseSurvival.Items.Random;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Pure static factory for <see cref="CraftExecutionSnapshot"/>.
    /// Single source of truth for snapshot building — used by both CraftQueueService and CraftService.
    /// Stateless, deterministic given identical (recipe, count, rng, ingredientsSnapshot) inputs.
    ///</summary>
    public static class CraftSnapshotBuilder
    {
        /// <summary>
        /// Build an immutable <see cref="CraftExecutionSnapshot"/> from recipe + job parameters.
        ///</summary>
        /// <param name="recipe">Recipe source — pure data input, not mutated</param>
        /// <param name="count">Craft batch multiplier (≥1</param>
        /// <param name="rng">RNG provider — injected for reproducibility/testing. Sentinel seed 0 banned (I-21</param>
        /// <param name="ingredientsSnapshot">Pre-captured per-unit ingredients snapshot (built by caller via CraftIngredientSnapshot.From). Null → empty</param>
        /// <returns>Frozen snapshot. Caller stores and reuses; no live repository reads occur</returns>
        public static CraftExecutionSnapshot Build(
            CraftRecipeData recipe,
            int count,
            IRandomProvider rng,
            CraftIngredientSnapshot[] ingredientsSnapshot)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (count < 1) count = 1;
            if (ingredientsSnapshot == null) ingredientsSnapshot = Array.Empty<CraftIngredientSnapshot>();

            // RecipeSnapshot — preserves IngredientsSnapshot per P0-A refund/audit contract.
            var recipeSnapshot = new RecipeSnapshot(
                recipe.RecipeId,
                recipe.RecipeVersion,
                recipe.EquipmentType.ToString(),
                (int)recipe.Rarity,
                ingredientsSnapshot);

            // CostSnapshot.Materials — pass-through from ingredientsSnapshot.
            var materialsCosts = ingredientsSnapshot.Length > 0
                ? Array.ConvertAll(ingredientsSnapshot, s => new IngredientCost { ItemId = s.ItemId, Count = s.Count })
                : Array.Empty<IngredientCost>();

            // CostSnapshot.Progression — decomposed requirements scaled by SumPerJob.
            // Catalysts intentionally empty (no catalyst resolver exists yet).
            var decomposedReqs = DecomposedRequirementResolver.Compute(recipe.Rarity);
            var decomposedCosts = DecomposedRequirementAggregator.SumPerJob(decomposedReqs, count);
            var progressionCosts = new IngredientCost[decomposedCosts.Count];
            for (int pi = 0; pi < decomposedCosts.Count; pi++)
                progressionCosts[pi] = new IngredientCost { ItemId = decomposedCosts[pi].ItemId, Count = decomposedCosts[pi].Count };

            // CostSnapshot.Currency — delegated to pure CraftCostResolver (behavior-preserving).
            // See CraftCostResolverTests for regression invariant: resolver output == previous inline formula.
            var currencySnap = CraftCostResolver.ComputeCurrencyCost(recipe, count);

            var costSnap = new CostSnapshot(materialsCosts, Array.Empty<IngredientCost>(), progressionCosts, currencySnap);

            // ContextSnapshot — default empty. P0-D territory: CraftContextBuilder will populate.
            var contextSnap = new CraftContextSnapshot();

            // Seed — sentinel 0 banned (I-21). Lower bound 1 guarantees positive long value.
            var seed = (long)rng.NextInt(1, int.MaxValue);

            return new CraftExecutionSnapshot(recipeSnapshot, costSnap, contextSnap, seed, count);
        }
    }
}
