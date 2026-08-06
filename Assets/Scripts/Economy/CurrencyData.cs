using System;

namespace IdleDefenseSurvival.Economy
{
    /// <summary>
    /// Serializable data structure for the three currency types.
    /// Used for save/load and UI display.
    /// </summary>
    [Serializable]
    public class CurrencyData
    {
        public long gold;
        public long gem;
        public long meat;

        public CurrencyData()
        {
            gold = 0;
            gem = 0;
            meat = 0;
        }

        public CurrencyData(long gold, long gem, long meat)
        {
            this.gold = gold;
            this.gem = gem;
            this.meat = meat;
        }

        /// <summary>
        /// Check if we have enough of a specific currency.
        /// </summary>
        public bool HasEnough(CurrencyType type, long amount)
        {
            return type switch
            {
                CurrencyType.Gold => gold >= amount,
                CurrencyType.Gem => gem >= amount,
                CurrencyType.Meat => meat >= amount,
                _ => false
            };
        }

        /// <summary>
        /// Get the current amount of a specific currency.
        /// </summary>
        public long Get(CurrencyType type)
        {
            return type switch
            {
                CurrencyType.Gold => gold,
                CurrencyType.Gem => gem,
                CurrencyType.Meat => meat,
                _ => 0
            };
        }

        /// <summary>
        /// Set a specific currency amount.
        /// </summary>
        public void Set(CurrencyType type, long amount)
        {
            switch (type)
            {
                case CurrencyType.Gold:
                    gold = amount;
                    break;
                case CurrencyType.Gem:
                    gem = amount;
                    break;
                case CurrencyType.Meat:
                    meat = amount;
                    break;
            }
        }
    }

}
