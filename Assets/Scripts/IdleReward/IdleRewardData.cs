using System;

namespace IdleDefenseSurvival.Data
{
    /// <summary>
    /// Serializable container for idle reward persistence.
    /// All timestamps use UTC ticks to avoid timezone issues.
    /// </summary>
    [Serializable]
    public class IdleRewardData
    {
        public long lastClaimUtcTicks = DateTime.UtcNow.Ticks;

        public int maxDurationSeconds = 4 * 3600;

        public int minimumClaimSeconds = 600;

        public float rewardMultiplier = 1f;
    }
}
