using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Rarity mechanics ladder — how many of each mechanic a rarity unlocks.
    /// Pure data; single place to re-tune rarity progression (socket/secondary/passive).
    /// Consumed by SocketGenerator, SecondaryStatGenerator.
    /// </summary>
    public static class RarityMechanicConfig
    {
        private static readonly Dictionary<Rarity, (int min, int max)> SecondaryRollRanges = new()
        {
            { Rarity.Common, (1, 2) },
            { Rarity.Rare, (2, 3) },
            { Rarity.Epic, (3, 4) },
            { Rarity.Legendary, (4, 5) },
            { Rarity.Mythic, (5, 6) },
            { Rarity.Divine, (25, 50) },
        };

        private static readonly Dictionary<Rarity, int> PassiveTiers = new()
        {
            { Rarity.Common, 0 },
            { Rarity.Rare, 1 },
            { Rarity.Epic, 2 },
            { Rarity.Legendary, 3 }, // minor
            { Rarity.Mythic, 4 },    // standard
            { Rarity.Divine, 6 },    // unique
        };

        public static (int min, int max) GetSecondaryRollRange(Rarity rarity) =>
            SecondaryRollRanges.TryGetValue(rarity, out var range) ? range : (0, 0);

        // Keep GetSecondaryCount for backward compat (returns max)
        public static int GetSecondaryCount(Rarity rarity) =>
            SecondaryRollRanges.TryGetValue(rarity, out var range) ? range.max : 0;

        public static int GetPassiveTier(Rarity rarity) =>
            PassiveTiers.TryGetValue(rarity, out var c) ? c : 0;

        /// <summary>Passives unlock from Legendary (tier > 0).</summary>
        public static bool HasPassive(Rarity rarity) => GetPassiveTier(rarity) > 0;
    }
}