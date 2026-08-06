using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Data
{
    /// <summary>
    /// Stores progress for each tier, tracking the highest wave reached.
    /// </summary>
    [Serializable]
    public class WaveProgressData
    {
        /// <summary>
        /// The tier the player is currently playing.
        /// </summary>
        public int CurrentTier = 1;
        public List<TierProgress> Tiers = new();
    }

    /// <summary>
    /// Progress information for a single tier.
    /// </summary>
    [Serializable]
    public class TierProgress
    {
        public int Tier = 1;
        public int HighestWave;
        public bool Cleared;
        public int TotalRuns;
        public long TotalKills;
        public long HighestGoldEarned;
        public long HighestMeatEarned;
        public long HighestExpEarned;
    }
}
