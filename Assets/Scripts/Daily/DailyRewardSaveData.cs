using System;

namespace IdleDefenseSurvival.Data
{
    [Serializable]
    public class DailyRewardSaveData
    {
        public int currentRewardIndex = 0;
        public long nextUnlockUtcTicks = 0;
        public bool completedToday = false;
        public string lastResetDate = "";
        public int claimedToday = 0;

        public DailyRewardSaveData Clone()
        {
            return new DailyRewardSaveData
            {
                currentRewardIndex = currentRewardIndex,
                nextUnlockUtcTicks = nextUnlockUtcTicks,
                completedToday = completedToday,
                lastResetDate = lastResetDate,
                claimedToday = claimedToday
            };
        }
    }
}
