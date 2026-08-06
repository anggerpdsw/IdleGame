using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Data
{
    [Serializable]
    public class UpgradeData
    {
        public Dictionary<string, int> skillLevels = new();
    }
}
