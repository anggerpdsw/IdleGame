using System;

namespace IdleDefenseSurvival.Data
{
    [Serializable]
    public class AccountData
    {
        public int level = 1;
        public long currentExp = 0;
        public long totalExp = 0;

        // Mission System
        public int maxMission = 1;

        // Crafting & Blacksmithing
        public int craftingLevel = 1;
        public long craftingCurrentExp = 0;
        public long craftingTotalExp = 0;
        public int blacksmithLevel = 1;
        public long blacksmithCurrentExp = 0;
        public long blacksmithTotalExp = 0;

        // Main attributes — player-allocated stat points. Base 5 each, +5 points per level-up.
        public int constitution = 5;
        public int strength = 5;
        public int intelligence = 5;
        public int dexterity = 5;
        public int unspentStatPoints = GameConstants.STARTING_STAT_POINTS; // Level 1 start
    }
}
