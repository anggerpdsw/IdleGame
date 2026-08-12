namespace IdleDefenseSurvival
{
    public static class GameConstants
    {
        public const int CURRENT_SAVE_VERSION = 3; // v3 = flat Items[] save; category derived from ItemId; slot via SlotIndex
        public const int MAX_WAVE_PER_TIER = 350;
        public const int BASE_LEVEL = 8903;
        public const int STARTING_STAT_POINTS = 5;  // Level 1 start
        public const int POINTS_PER_LEVEL = 5;      // Bonus per level-up
        public const string DATE_FORMAT = "yyyy-MM-dd";
        public const int CARD_START_SLOT = 1;
        public const int CARD_MAX_LEVEL = 10;
        public const int CARD_MAX_SLOT = 19;
        public const int ROLL1X_GEM_COST = 20;
        public const int ROLL10X_GEM_COST = 190;
        public const int ROLL100X_GEM_COST = 1800;
        public static readonly int[] CARD_SLOT_EXPANSION_COSTS =
        {
            0,      // Slot 1
            50,     // Slot 2
            100,    // Slot 3
            200,    // Slot 4
            300,    // Slot 5
            400,    // Slot 6
            500,    // Slot 7
            600,    // Slot 8
            750,    // Slot 9
            1000,   // Slot 10
            1200,   // Slot 11
            1400,   // Slot 12
            1600,   // Slot 13
            1800,   // Slot 14
            2500,   // Slot 15
            3500,   // Slot 16
            4500,   // Slot 17
            5500,   // Slot 18
            7500    // Slot 19
        };
        
        // Pity thresholds (configurable via constants or JSON in the future)
        public const int PITY_EPIC_THRESHOLD = 51;
        public const int PITY_LEGENDARY_THRESHOLD = 153;
        public const int PITY_MYTHIC_THRESHOLD = 505;
        // SaveData keys for pity counters
        public const string KEY_PITY_EPIC = "PityEpic";
        public const string KEY_PITY_LEGENDARY = "PityLegendary";
        public const string KEY_PITY_MYTHIC = "PityMythic";

        // Daily Reward        
        public const int REWARD_COUNT = 7;
        public const int COOLDOWN_MINUTES = 5;

    }

}