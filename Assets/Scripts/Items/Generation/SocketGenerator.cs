using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Items.Random;

namespace IdleDefenseSurvival.Items.Generation
{
    /// <summary>
    /// Generates equipment sockets.
    ///
    /// Socket rules:
    /// Common    : 0..1
    /// Rare      : 0..2
    /// Epic      : 1..3
    /// Legendary : 2..4
    /// Mythic    : 3..5
    /// Divine    : 4..6
    ///
    /// MaxSockets comes from EquipmentBaseData rarity configuration.
    /// </summary>
    public sealed class SocketGenerator
    {
        private readonly IRandomProvider _rng;
        private readonly SocketGeneratorConfig _config;

        public SocketGenerator(IRandomProvider rng, SocketGeneratorConfig config = null)
        {
            _rng = rng ?? new UnityRandomProvider();
            _config = config ?? SocketGeneratorConfig.Default;
        }

        /// <summary>
        /// Generates the actual sockets for an equipment item.
        /// maxSockets is the maximum socket count defined by rarity configuration.
        /// </summary>
        public SocketData[] GenerateSockets(int maxSockets, Rarity rarity, ItemGenerationContext context)
        {
            if (maxSockets <= 0) return Array.Empty<SocketData>();
            int minSockets = GetMinimumSockets(rarity);
            // Safety: minimum can never exceed configured maximum.
            minSockets = Math.Min(minSockets, maxSockets);
            // Random range is inclusive.
            int socketCount = _rng.NextInt(minSockets, maxSockets + 1);
            if (socketCount <= 0) return Array.Empty<SocketData>();
            var sockets = new SocketData[socketCount];
            for (int i = 0; i < socketCount; i++)
            {
                bool isUnlocked = IsSocketUnlocked(i, rarity, context);
                sockets[i] = new SocketData
                {
                    SocketIndex = i,
                    IsUnlocked = isUnlocked,
                    IsLocked = false,
                    GemId = null,
                    GemLevel = 1
                };
            }
            ApplyEventModifiers(sockets, context);
            return sockets;
        }

        /// <summary>
        /// Returns the minimum possible socket count for the rarity.
        /// Common    = 0
        /// Rare      = 0
        /// Epic      = 1
        /// Legendary = 2
        /// Mythic    = 3
        /// Divine    = 4
        /// </summary>
        private static int GetMinimumSockets(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.Common => 0,
                Rarity.Rare => 0,
                Rarity.Epic => 1,
                Rarity.Legendary => 2,
                Rarity.Mythic => 3,
                Rarity.Divine => 4,
                _ => 0,
            };
        }

        /// <summary>
        /// Determines whether a generated socket is unlocked.
        /// </summary>
        private bool IsSocketUnlocked(int index, Rarity rarity, ItemGenerationContext context)
        {
            // First socket is always unlocked.
            if (index == 0) return true;
            // Check rarity-based unlock.
            int unlockRarity = _config.SocketUnlockRarity.TryGetValue(index, out var rarityRequirement)
                ? rarityRequirement
                : index + 1;
            if ((int)rarity >= unlockRarity) return true;
            // Check enhance-based unlock.
            int unlockEnhance =
                _config.SocketUnlockEnhance.TryGetValue(index, out var enhanceRequirement)
                    ? enhanceRequirement
                    : 0;
            if (context.FixedEnhance.HasValue && context.FixedEnhance.Value >= unlockEnhance)
                return true;
            return false;
        }

        private void ApplyEventModifiers(SocketData[] sockets, ItemGenerationContext context)
        {
            if (context.EventModifiers == null) return;
            foreach (var modifier in context.EventModifiers)
            {
                if (modifier is ISocketModifier socketMod)
                    socketMod.ModifySockets(sockets, context);
            }
        }
    }

    /// <summary>
    /// Configuration for socket generation.
    /// </summary>
    [Serializable]
    public class SocketGeneratorConfig
    {
        public Dictionary<int, int> SocketUnlockRarity = new()
        {
            { 1, 2 }, // 2nd socket at Rare
            { 2, 4 }, // 3rd socket at Legendary
            { 3, 5 }, // 4th socket at Mythic
            { 4, 6 }, // 5th socket at Divine
        };

        public Dictionary<int, int> SocketUnlockEnhance = new()
        {
            { 1, 5 },  // 2nd socket at +5
            { 2, 10 }, // 3rd socket at +10
            { 3, 15 }, // 4th socket at +15
            { 4, 20 }, // 5th socket at +20
        };

        public static SocketGeneratorConfig Default
        {
            get { return new SocketGeneratorConfig(); }
        }
    }

    /// <summary>
    /// Interface for event modifiers that affect sockets.
    /// </summary>
    public interface ISocketModifier
    {
        void ModifySockets(SocketData[] sockets, ItemGenerationContext context);
    }
}