using System;

namespace IdleDefenseSurvival.Items.Decomposition
{
    /// <summary>
    /// Per-unit decomposed requirement for a single rarity tier.
    /// Returned by <see cref="DecomposedRequirementResolver.Compute"/>.
    ///</summary>
    [Serializable]
    public struct DecomposedRequirement
    {
        public string ItemId;
        public int Quantity;

        public DecomposedRequirement(string itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }
    }
}
