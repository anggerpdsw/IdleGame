using System;

namespace IdleDefenseSurvival.Data
{
    [Serializable]
    public class RewardData
    {
        public RewardType Type;

        // Untuk Item/Equipment/Hero
        public string Id;

        public long Amount;

        public RewardData()
        {
        }

        public RewardData(RewardType type, long amount)
        {
            Type = type;
            Amount = amount;
        }

        public RewardData(RewardType type, string id, long amount)
        {
            Type = type;
            Id = id;
            Amount = amount;
        }
    }
}