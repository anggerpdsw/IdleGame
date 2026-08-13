using System.Collections.Generic;

namespace IdleDefenseSurvival.Items.Decomposition
{
    /// <summary>
    /// Pure C# aggregator. Scales per-unit decomposed requirements by craft count.
    /// No Unity dependency, no IO.
    ///</summary>
    public static class DecomposedRequirementAggregator
    {
        /// <summary>
        /// Aggregate per-unit requirements into a per-job scaled list.
        /// Mirrors the v2.5 <c>CraftIngredientSnapshot.From(ing, count</c> factory pattern:
        /// <c>Count = Quantity * jobCount</c>, computed once at capture, frozen.
        ///</summary>
        /// <param name="requirements">Per-unit requirements from resolver</param>
        /// <param name="jobCount">Craft batch multiplier (≥1</param>
        /// <returns>Scaled snapshot entries. Never null</returns>
        public static IReadOnlyList<DecomposedSnapshotEntry> SumPerJob(
            IReadOnlyList<DecomposedRequirement> requirements,
            int jobCount)
        {
            if (requirements == null || requirements.Count == 0)
                return System.Array.Empty<DecomposedSnapshotEntry>();

            if (jobCount < 1)
                jobCount = 1;

            var result = new DecomposedSnapshotEntry[requirements.Count];
            for (int i = 0; i < requirements.Count; i++)
            {
                var req = requirements[i];
                result[i] = new DecomposedSnapshotEntry(req.ItemId, req.Quantity * jobCount);
            }
            return result;
        }
    }
}
