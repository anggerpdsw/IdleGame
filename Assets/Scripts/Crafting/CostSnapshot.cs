using System;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Scaled cost captured at snapshot build. Mirrors §10.1 CostSnapshot shape.
    /// Progression gated by DecomposedRequirementResolver.Compute + SumPerJob.
    ///</summary>
    [Serializable]
    public struct CostSnapshot
    {
        public IngredientCost[] Materials;
        public IngredientCost[] Catalysts;
        public IngredientCost[] Progression;   // scaled by SumPerJob; BLOCKED until wiring
        public CurrencySnapshot Currency;

        public CostSnapshot(
            IngredientCost[] materials,
            IngredientCost[] catalysts,
            IngredientCost[] progression,
            CurrencySnapshot currency)
        {
            Materials = materials ?? Array.Empty<IngredientCost>();
            Catalysts = catalysts ?? Array.Empty<IngredientCost>();
            Progression = progression ?? Array.Empty<IngredientCost>();
            Currency = currency;
        }
    }

    /// <summary>
    /// Per-resource scaled cost entry. Count = per-unit * jobCount.
    ///</summary>
    [Serializable]
    public struct IngredientCost
    {
        public string ItemId;
        public int Count;
    }
}
