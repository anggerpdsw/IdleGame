using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Data
{
    [Serializable]
    public class AccountData
    {
        public int level = 1;
        public long currentExp = 0;
        public long totalExp = 0;
        public int craftingLevel = 1; // Added
        public Dictionary<string, int> recipeMasteryLevels = new(); // Added
    }
}
