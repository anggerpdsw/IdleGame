using System;

namespace IdleDefenseSurvival.Crafting
{
    /// <summary>
    /// Currency cost captured at snapshot build. Scaled by CraftCount.
    /// Meat injected into AdditionalCosts by CraftCostResolver.
    ///</summary>
    [Serializable]
    public struct CurrencySnapshot
    {
        public long GoldSnapshot;
        public long GemSnapshot;
        public CostEntry[] AdditionalCosts;

        public CurrencySnapshot(long gold, long gem, CostEntry[] additionalCosts = null)
        {
            GoldSnapshot = gold;
            GemSnapshot = gem;
            AdditionalCosts = additionalCosts ?? Array.Empty<CostEntry>();
        }

        /// <summary>
        /// Extract meat amount from AdditionalCosts. Returns 0 if not present.
        /// </summary>
        public long MeatSnapshot
        {
            get
            {
                if (AdditionalCosts == null) return 0;
                foreach (var entry in AdditionalCosts)
                {
                    if (string.Equals(entry.CurrencyId, CurrencyType.Meat.ToString(), StringComparison.OrdinalIgnoreCase))
                        return entry.Amount;
                }
                return 0;
            }
        }
    }

    /// <summary>
    /// Non-standard currency cost (meat, exp, special tokens).
    ///</summary>
    [Serializable]
    public struct CostEntry
    {
        public string CurrencyId;   // "meat", "exp", etc.
        public long Amount;
    }
}
