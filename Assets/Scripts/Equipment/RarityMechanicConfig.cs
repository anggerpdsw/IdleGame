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
        private static readonly Dictionary<ItemRarity, int> SecondaryCounts = new()
        {
            { ItemRarity.Common, 0 },
            { ItemRarity.Rare, 1 },
            { ItemRarity.Epic, 2 },
            { ItemRarity.Legendary, 3 },
            { ItemRarity.Mythic, 4 },
            { ItemRarity.Divine, 6 },
        };

        private static readonly Dictionary<ItemRarity, int> SocketCounts = new()
        {
            { ItemRarity.Common, 0 },
            { ItemRarity.Rare, 1 },
            { ItemRarity.Epic, 1 },
            { ItemRarity.Legendary, 2 },
            { ItemRarity.Mythic, 3 },
            { ItemRarity.Divine, 3 },
        };

        private static readonly Dictionary<ItemRarity, int> PassiveTiers = new()
        {
            { ItemRarity.Common, 0 },
            { ItemRarity.Rare, 0 },
            { ItemRarity.Epic, 0 },
            { ItemRarity.Legendary, 1 }, // minor
            { ItemRarity.Mythic, 2 },    // standard
            { ItemRarity.Divine, 4 },    // unique
        };

        public static int GetSecondaryCount(ItemRarity rarity) =>
            SecondaryCounts.TryGetValue(rarity, out var c) ? c : 0;

        public static int GetSocketCount(ItemRarity rarity) =>
            SocketCounts.TryGetValue(rarity, out var c) ? c : 0;

        public static int GetPassiveTier(ItemRarity rarity) =>
            PassiveTiers.TryGetValue(rarity, out var c) ? c : 0;

        /// <summary>Passives unlock from Legendary (tier &gt; 0).</summary>
        public static bool HasPassive(ItemRarity rarity) => GetPassiveTier(rarity) > 0;
    }
}