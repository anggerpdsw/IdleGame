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

        // Main attributes — player-allocated stat points. Base 5 each, +5 points per level-up.
        public int constitution = 5;
        public int strength = 5;
        public int intelligence = 5;
        public int dexterity = 5;
        public int unspentStatPoints = GameConstants.STARTING_STAT_POINTS; // Level 1 start
    }
}
