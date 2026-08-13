using System;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Currency cost captured at snapshot build. Scaled by CraftCount.
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
