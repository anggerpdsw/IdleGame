namespace IdleDefenseSurvival.Data
{
    public class VictoryData
    {
        public WaveState State;
        public int Tier;
        public int HighestWave;

        public long GoldEarned;
        public long MeatEarned;
        public long ExpEarned;

        public long BonusGold;
        public long BonusMeat;

        public long TotalGold => GoldEarned + BonusGold;
        public long TotalMeat => MeatEarned + BonusMeat;
    }
}