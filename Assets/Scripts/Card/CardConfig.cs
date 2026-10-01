using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Data
{
    [Serializable] public class CardProgressionConfig
    {
        public int StartingSlots;
        public int MaximumSlots;
        public int MaximumLevel;
        public CardRollCostConfig RollCosts;
        public CardPityThresholdConfig PityThresholds;
        public List<int> SlotExpansionCosts;
        public List<int> DuplicateRequirements;
    }

    [Serializable] public class CardRollCostConfig
    {
        public int Single;
        public int Ten;
        public int Hundred;
    }

    [Serializable] public class CardPityThresholdConfig
    {
        public int Epic;
        public int Legendary;
        public int Mythic;
    }
}