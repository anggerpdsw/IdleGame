using System;

namespace IdleDefenseSurvival.Items.Decomposition
{
    /// <summary>
    /// Scaled decomposed snapshot entry (per-job total).
    /// Returned by <see cref="DecomposedRequirementAggregator.SumPerJob"/>.
    /// Vocabulary lock: Count (scaled) != Quantity (per-unit), per v3.2 §16.1.
    ///</summary>
    [Serializable]
    public struct DecomposedSnapshotEntry
    {
        public string ItemId;
        public int Count;

        public DecomposedSnapshotEntry(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }
}
