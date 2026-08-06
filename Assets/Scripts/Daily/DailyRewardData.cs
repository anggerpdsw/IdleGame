using System;
using IdleDefenseSurvival.Manager;

namespace IdleDefenseSurvival.Data
{
    public sealed class DailyRewardData
    {
        public DailyRewardData(RewardType type, long amount, string id = "")
        {
            Type = type;
            Amount = amount;
            Id = id;
        }

        public RewardType Type { get; }
        public long Amount { get; }
        public string Id { get; }
    }
    
    public sealed class DailyRewardProvider
    {
        public DailyRewardData GetReward(int rewardIndex)
        {
            return rewardIndex switch
            {
                0 => new DailyRewardData(RewardType.Gold, GetGoldReward()),
                1 => new DailyRewardData(RewardType.Gem, 11),
                2 => new DailyRewardData(RewardType.Meat, GetMeatReward()),
                3 => new DailyRewardData(RewardType.Item, 1, "CardRoll"),
                4 => new DailyRewardData(RewardType.Exp, GetExpReward()),
                5 => new DailyRewardData(RewardType.Item, 3, "UltimateStone"),
                6 => new DailyRewardData(RewardType.Item, 1, "SkinShard"),
                _ => new DailyRewardData(RewardType.Gold, 0)
            };
        }

        private long GetGoldReward()
        {
            var highestGold = SaveManager.Instance?.GetHighestGoldEarned() ?? 0L;
            return Math.Max(100000L, highestGold);
        }

        private long GetMeatReward()
        {
            var highestMeat = SaveManager.Instance?.GetHighestMeatEarned() ?? 0L;
            return Math.Max(1000L, highestMeat);
        }

        private long GetExpReward()
        {
            var highestExp = SaveManager.Instance?.GetHighestExpEarned() ?? 0L;
            return Math.Max(3000L, highestExp / 2);
        }
    }

}