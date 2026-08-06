using System;
using IdleDefenseSurvival.Inventory;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Socket validation service - handles gem type validation logic.
    /// Separated from SocketService to follow single responsibility principle.
    /// Allows adding complex validation rules without touching socket mechanics.
    /// </summary>
    public sealed class SocketValidationService
    {
        private readonly IReadOnlySocketConfig _socketConfig;

        public SocketValidationService(IReadOnlySocketConfig socketConfig)
        {
            _socketConfig = socketConfig;
        }

        /// <summary>
        /// Checks if a gem type can be inserted into a specific socket on an item.
        /// </summary>
        public bool CanInsertGem(InventoryItem item, int socketIndex, GemType gemType)
        {
            if (item?.Sockets == null || socketIndex < 0 || socketIndex >= item.Sockets.Length)
                return false;

            // Check if socket is unlocked
            if (!item.Sockets[socketIndex].IsUnlocked)
                return false;

            // Check if socket is locked (prevents modification)
            if (item.Sockets[socketIndex].IsLocked)
                return false;

            // Check socket rule
            if (!_socketConfig.SocketRules[socketIndex].CanInsertGem(gemType))
                return false;

            // Additional checks can be added here:
            // - Item rarity requirements
            // - Player level requirements
            // - Item type restrictions
            // - Set bonus requirements
            // - Event restrictions

            return true;
        }

        /// <summary>
        /// Gets all allowed gem types for a socket on an item.
        /// </summary>
        public GemType[] GetAllowedGemTypes(InventoryItem item, int socketIndex)
        {
            if (item?.Sockets == null || socketIndex < 0 || socketIndex >= item.Sockets.Length)
                return Array.Empty<GemType>();

            var rule = _socketConfig.SocketRules[socketIndex];
            if (rule.AllowAnyGem)
            {
                // Return all gem types (would need GemType enum values)
                return Enum.GetValues(typeof(GemType)) as GemType[];
            }

            return rule.AllowedGemTypes ?? Array.Empty<GemType>();
        }

        /// <summary>
        /// Checks if a gem can be removed from a socket.
        /// </summary>
        public bool CanRemoveGem(InventoryItem item, int socketIndex)
        {
            if (item?.Sockets == null || socketIndex < 0 || socketIndex >= item.Sockets.Length)
                return false;

            if (!_socketConfig.CanRemoveGems)
                return false;

            if (item.Sockets[socketIndex].IsLocked)
                return false;

            return true;
        }

        /// <summary>
        /// Checks if a gem can be destroyed for materials.
        /// </summary>
        public bool CanDestroyGem(InventoryItem item, int socketIndex)
        {
            if (item?.Sockets == null || socketIndex < 0 || socketIndex >= item.Sockets.Length)
                return false;

            if (!_socketConfig.CanDestroyGems)
                return false;

            return true;
        }

        /// <summary>
        /// Validates full socket state for an item.
        /// Returns list of validation issues if any.
        /// </summary>
        public System.Collections.Generic.List<string> ValidateItemSockets(InventoryItem item)
        {
            var issues = new System.Collections.Generic.List<string>();

            if (item?.Sockets == null)
            {
                issues.Add("Item has no sockets");
                return issues;
            }

            for (int i = 0; i < item.Sockets.Length; i++)
            {
                var socket = item.Sockets[i];
                if (socket.IsEmpty) continue;

                if (!CanInsertGem(item, i, (GemType)System.Enum.Parse(typeof(GemType), socket.GemId)))
                {
                    issues.Add($"Socket {i}: Gem {socket.GemId} is not valid for this socket");
                }
            }

            return issues;
        }
    }
}