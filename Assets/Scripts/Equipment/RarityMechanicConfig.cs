using System;
using System.Collections.Generic;

namespace IdleDefenseSurvival.Equipment
{
    /// <summary>
    /// Rarity mechanics ladder — how many of each mechanic a rarity unlocks.
    /// Pure data; single place to re-tune rarity progression (socket/secondary/passive counts).
    /// Consumed by SocketGenerator, StatRollService, AffixGenerator, EnchantmentGenerator.
    /// </summary>
    public static class RarityMechanicConfig
    {
        // Design ladder: Common 1 attr / Rare +1 socket / Epic +1 secondary / Legendary +1 secondary +passive /
        // Mythic +1 secondary +socket / Divine +1 secondary +socket.
        private static readonly Dictionary<Rarity, int> SecondaryCounts = new()
        {
            { Rarity.Common, 0 },
            { Rarity.Rare, 1 },
            { Rarity.Epic, 2 },
            { Rarity.Legendary, 3 },
            { Rarity.Mythic, 4 },
            { Rarity.Divine, 6 },
        };

        private static readonly Dictionary<Rarity, int> PassiveTiers = new()
        {
            { Rarity.Common, 0 },
            { Rarity.Rare, 0 },
            { Rarity.Epic, 0 },
            { Rarity.Legendary, 1 }, // minor
            { Rarity.Mythic, 2 },    // standard
            { Rarity.Divine, 4 },    // unique
        };

        public static int GetSecondaryCount(Rarity rarity) =>
            SecondaryCounts.TryGetValue(rarity, out var c) ? c : 0;

        public static int GetPassiveTier(Rarity rarity) =>
            PassiveTiers.TryGetValue(rarity, out var c) ? c : 0;

        /// <summary>Passives unlock from Legendary (tier &gt; 0).</summary>
        public static bool HasPassive(Rarity rarity) => GetPassiveTier(rarity) > 0;
    }
}