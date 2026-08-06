using System;
using System.Collections.Generic;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Items.Random;

namespace IdleDefenseSurvival.Items.Generation
{
    /// <summary>
    /// Generator for equipment sockets.
    /// Handles socket count, unlock conditions, and special socket types.
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
        /// Generates sockets for an equipment item.
        /// </summary>
        public SocketData[] GenerateSockets(EquipmentData baseEquipment, ItemRarity rarity, ItemGenerationContext context)
        {
            if (baseEquipment.MaxSockets <= 0) return Array.Empty<SocketData>();

            int socketCount = baseEquipment.MaxSockets;
            var sockets = new SocketData[socketCount];

            for (int i = 0; i < socketCount; i++)
            {
                bool isUnlocked = IsSocketUnlocked(i, baseEquipment, rarity, context);

                sockets[i] = new SocketData
                {
                    SocketIndex = i,
                    IsUnlocked = isUnlocked,
                    IsLocked = false,
                    GemId = null,
                    GemLevel = 1
                };
            }

            // Apply special socket modifiers from events
            ApplyEventModifiers(sockets, context);

            return sockets;
        }

        private bool IsSocketUnlocked(int index, EquipmentData baseEquipment, ItemRarity rarity, ItemGenerationContext context)
        {
            // First socket always unlocked
            if (index == 0) return true;

            // Check socket rules from SocketConfigData
            if (baseEquipment.MaxSockets > 0)
            {
                // Use rarity-based unlock
                int unlockRarity = _config.SocketUnlockRarity.TryGetValue(index, out var r) ? r : index + 1;
                if ((int)rarity >= unlockRarity) return true;

                // Use enhance-based unlock
                int unlockEnhance = _config.SocketUnlockEnhance.TryGetValue(index, out var e) ? e : 0;
                if (context.FixedEnhance.HasValue && context.FixedEnhance.Value >= unlockEnhance) return true;
            }

            return false;
        }

        private void ApplyEventModifiers(SocketData[] sockets, ItemGenerationContext context)
        {
            if (context.EventModifiers == null) return;

            foreach (var modifier in context.EventModifiers)
            {
                if (modifier is ISocketModifier socketMod)
                {
                    socketMod.ModifySockets(sockets, context);
                }
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
            { 1, 3 }, // 2nd socket at Rare
            { 2, 5 }, // 3rd socket at Legendary
            { 3, 7 }, // 4th socket at Ancient
            { 4, 8 }, // 5th socket at Divine
        };

        public Dictionary<int, int> SocketUnlockEnhance = new()
        {
            { 1, 5 },  // 2nd socket at +5
            { 2, 10 }, // 3rd socket at +10
            { 3, 15 }, // 4th socket at +15
            { 4, 20 }, // 5th socket at +20
        };

        public static SocketGeneratorConfig Default => new();
    }

    /// <summary>
    /// Interface for event modifiers that affect sockets.
    /// </summary>
    public interface ISocketModifier
    {
        void ModifySockets(SocketData[] sockets, ItemGenerationContext context);
    }
}