using UnityEngine;
using System.Collections.Generic;
using IdleDefenseSurvival.Data;

namespace IdleDefenseSurvival.Ultimate
{
    /// <summary>
    /// Static factory and registry for all ultimate handlers.
    /// Provides a centralized point to:
    /// 1. Register new ultimate handlers
    /// 2. Spawn ultimates by ID
    /// 3. Track active counts per ultimate
    /// 4. Get handler references
    /// </summary>
    public static class UltimateFactory
    {
        private static Dictionary<string, IUltimateHandler> _handlers = new();
        private static Dictionary<string, int> _activeCountMap = new();

        /// <summary>
        /// Register a handler for an ultimate.
        /// Call this during initialization (e.g., from UltimateManager.Awake).
        /// </summary>
        public static void RegisterHandler(string ultimateId, IUltimateHandler handler)
        {
            if (handler == null)
            {
                Debug.LogError($"[UltimateFactory] Cannot register null handler for '{ultimateId}'");
                return;
            }

            if (_handlers.ContainsKey(ultimateId))
            {
                Debug.LogWarning($"[UltimateFactory] Handler for '{ultimateId}' is being overridden");
            }

            _handlers[ultimateId] = handler;
            _activeCountMap[ultimateId] = 0;
        }

        /// <summary>
        /// Try to spawn an ultimate by ID.
        /// Returns true if spawn was successful, false otherwise.
        /// </summary>
        public static bool TrySpawn(string ultimateId, Player.Player player, Vector3 position, UltimateData ultimateData)
        {
            if (!_handlers.TryGetValue(ultimateId, out var handler))
            {
                Debug.LogError($"[UltimateFactory] No handler registered for ultimate '{ultimateId}'");
                return false;
            }

            if (ultimateData == null)
            {
                Debug.LogError($"[UltimateFactory] UltimateData is null for '{ultimateId}'");
                return false;
            }

            return handler.TrySpawn(player, position, ultimateData);
        }

        /// <summary>
        /// Notify factory when an instance is destroyed.
        /// Call this from the ultimate instance when it's being destroyed.
        /// </summary>
        public static void OnInstanceDestroyed(string ultimateId)
        {
            if (_activeCountMap.TryGetValue(ultimateId, out int count) && count > 0)
            {
                _activeCountMap[ultimateId] = count - 1;
            }
        }

        /// <summary>
        /// Get the current active count for an ultimate.
        /// </summary>
        public static int GetActiveCount(string ultimateId)
        {
            return _activeCountMap.TryGetValue(ultimateId, out int count) ? count : 0;
        }

        /// <summary>
        /// Increment active count for an ultimate.
        /// Call this from the handler when an instance is successfully spawned.
        /// </summary>
        public static void IncrementActiveCount(string ultimateId)
        {
            if (_activeCountMap.ContainsKey(ultimateId))
                _activeCountMap[ultimateId]++;
        }

        /// <summary>
        /// Get handler by ultimate ID.
        /// Returns null if handler not found.
        /// </summary>
        public static IUltimateHandler GetHandler(string ultimateId)
        {
            _handlers.TryGetValue(ultimateId, out var handler);
            return handler;
        }

        /// <summary>
        /// Get all registered ultimate IDs.
        /// Useful for UI, progression tracking, etc.
        /// </summary>
        public static IReadOnlyCollection<string> GetAllUltimateIds()
        {
            return _handlers.Keys;
        }

        /// <summary>
        /// Reset the factory (for testing or scene reloads).
        /// </summary>
        public static void Reset()
        {
            _handlers.Clear();
            _activeCountMap.Clear();
        }
    }
}
