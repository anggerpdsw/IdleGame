using System;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Immutable per-job execution state. Built ONCE at StartCraft.
    /// CompletionSeed generated at build time (I-21), BEFORE any resource mutation.
    /// All sub-snapshots are frozen; no live repository reads allowed.
    ///</summary>
    [Serializable]
    public sealed class CraftExecutionSnapshot
    {
        public RecipeSnapshot Recipe;
        public CostSnapshot Cost;
        public CraftContextSnapshot Context;
        public long? CompletionSeed;     // null = not generated (invalid after build); sentinel 0 BANNED
        public int CraftCount;

        public CraftExecutionSnapshot(
            RecipeSnapshot recipe,
            CostSnapshot cost,
            CraftContextSnapshot context,
            long? completionSeed,
            int craftCount)
        {
            Recipe = recipe;
            Cost = cost;
            Context = context;
            CompletionSeed = completionSeed;
            CraftCount = craftCount;
        }
    }
    // ponytail: DecomposedRequirementsSnapshot[] omitted at root — Progression lives under CostSnapshot.
}
