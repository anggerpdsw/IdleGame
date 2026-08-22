using System;
using System.Collections.Generic;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Stats;
using System.Linq;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Gem Service Facade - unified entry point for all gem operations.
    /// Delegates to specialized sub-services.
    /// </summary>
    public sealed class GemService : MonoBehaviour
    {
        #region Singleton
        private static GemService _instance;
        public static GemService Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => _instance = null;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        #endregion

        #region Events (forwarded from sub-services)
        /// <summary>
        /// Single socket-change event, forwarded from GemSocketService.
        /// UI/analytics/save subscribe once.
        /// </summary>
        public event Action<SocketChangeContext> OnSocketChanged;
        public event Action<string, int, int> OnGemUpgraded;
        public event Action<string, int> OnGemExperienceChanged;
        #endregion

        #region Fields
        // Runtime gem instances keyed by GemInstanceData.InstanceId
        private readonly Dictionary<string, GemInstanceData> _socketedGems = new();
        #endregion

        #region Initialization
        private void Initialize()
        {
            // Subscribe to sub-service events
            GemSocketService.Instance.OnSocketChanged += ctx => OnSocketChanged?.Invoke(ctx);
            GemUpgradeService.Instance.OnGemUpgraded += (id, from, to) => OnGemUpgraded?.Invoke(id, from, to);
            GemExperienceService.Instance.OnGemExperienceChanged += (id, exp) => OnGemExperienceChanged?.Invoke(id, exp);
        }
        #endregion

        #region Public API - Socketing
        /// <summary>
        /// Attempts to socket a gem into an item.
        /// All validation delegated to GemSocketService; this only creates the runtime instance.
        /// </summary>
        public bool SocketGem(InventoryItem item, int socketIndex, InventoryItem gemItem)
        {
            if (item?.Sockets == null || socketIndex < 0 || socketIndex >= item.Sockets.Length) return false;

            // Create runtime gem instance (validates gem ItemId resolves to a GemData)
            var gemInstance = GemFactory.Instance.CreateGemInstance(gemItem?.ItemId, gemItem?.Level ?? 1);
            if (gemInstance == null) return false;

            // Store using the instance's own InstanceId as key
            _socketedGems[gemInstance.InstanceId] = gemInstance;

            // Delegate to socket service (validates item/gem/unlocked/empty/gem-type)
            bool success = GemSocketService.Instance.SocketGem(item, socketIndex, gemItem, gemInstance);

            if (!success)
            {
                _socketedGems.Remove(gemInstance.InstanceId);
            }

            return success;
        }

        /// <summary>
        /// Removes a gem from a socket and returns it to inventory.
        /// </summary>
        public bool RemoveGem(InventoryItem item, int socketIndex, bool payCost = true)
        {
            var gemInstance = GetSocketedGemInstance(item, socketIndex);
            if (gemInstance == null) return false;

            bool success = GemSocketService.Instance.RemoveGem(item, socketIndex, payCost, gemInstance);

            if (success)
            {
                _socketedGems.Remove(gemInstance.InstanceId);
            }

            return success;
        }

        /// <summary>
        /// Destroys a gem in a socket (returns partial materials).
        /// </summary>
        public bool DestroyGem(InventoryItem item, int socketIndex)
        {
            var gemInstance = GetSocketedGemInstance(item, socketIndex);
            if (gemInstance == null) return false;

            bool success = GemSocketService.Instance.DestroyGem(item, socketIndex, gemInstance);

            if (success)
            {
                _socketedGems.Remove(gemInstance.InstanceId);
            }

            return success;
        }

        /// <summary>
        /// Swaps gems between two sockets.
        /// Runtime instances stay with their InstanceId; modifiers re-applied per index (id-stable, idempotent).
        /// </summary>
        public bool SwapGems(InventoryItem item, int socketIndexA, int socketIndexB)
        {
            bool success = GemSocketService.Instance.SwapGems(item, socketIndexA, socketIndexB);
            if (!success) return false;

            // Re-apply modifiers for both sockets (stable GemInstanceId ids -> replace, no residue)
            ReapplyGemModifier(item, socketIndexA);
            ReapplyGemModifier(item, socketIndexB);
            return true;
        }
        #endregion

        #region Public API - Upgrading
        /// <summary>
        /// Upgrades a socketed gem.
        /// </summary>
        public bool UpgradeGem(InventoryItem item, int socketIndex, int targetLevel = -1)
        {
            var gemInstance = GetSocketedGemInstance(item, socketIndex);
            if (gemInstance == null) return false;

            return GemUpgradeService.Instance.UpgradeGem(item, socketIndex, targetLevel, gemInstance);
        }

        /// <summary>
        /// Gets the cost to upgrade a gem to target level.
        /// </summary>
        public long GetUpgradeCost(InventoryItem item, int socketIndex, int targetLevel)
        {
            return GemUpgradeService.Instance.GetUpgradeCost(item, socketIndex, targetLevel);
        }

        /// <summary>
        /// Gets the maximum level for a gem.
        /// </summary>
        public int GetMaxLevel(InventoryItem item, int socketIndex)
        {
            return GemUpgradeService.Instance.GetMaxLevel(item, socketIndex);
        }

        /// <summary>
        /// Checks if a gem can be upgraded further.
        /// </summary>
        public bool CanUpgrade(InventoryItem item, int socketIndex)
        {
            return GemUpgradeService.Instance.CanUpgrade(item, socketIndex);
        }

        /// <summary>
        /// Gets the current level of a socketed gem.
        /// </summary>
        public int GetCurrentLevel(InventoryItem item, int socketIndex)
        {
            return GemUpgradeService.Instance.GetCurrentLevel(item, socketIndex);
        }
        #endregion

        #region Public API - Experience
        /// <summary>
        /// Adds experience to a socketed gem.
        /// </summary>
        public bool AddGemExperience(InventoryItem item, int socketIndex, int experience)
        {
            var gemInstance = GetSocketedGemInstance(item, socketIndex);
            if (gemInstance == null) return false;

            return GemExperienceService.Instance.AddGemExperience(item, socketIndex, experience, gemInstance);
        }

        /// <summary>
        /// Gets the experience required for next level.
        /// </summary>
        public int GetExperienceForNextLevel(InventoryItem item, int socketIndex)
        {
            return GemExperienceService.Instance.GetExperienceForNextLevel(item, socketIndex);
        }

        /// <summary>
        /// Gets the current experience of a socketed gem.
        /// </summary>
        public int GetCurrentExperience(InventoryItem item, int socketIndex)
        {
            var gemInstance = GetSocketedGemInstance(item, socketIndex);
            return GemExperienceService.Instance.GetCurrentExperience(gemInstance);
        }

        /// <summary>
        /// Gets the experience required for a specific level.
        /// </summary>
        public int GetExperienceForLevel(string gemId, int level)
        {
            return GemExperienceService.Instance.GetExperienceForLevel(gemId, level);
        }
        #endregion

        #region Public API - Stats
        /// <summary>
        /// Gets stat bonuses from all socketed gems on an item.
        /// </summary>
        public Dictionary<SecondaryStat, float> GetItemGemBonuses(InventoryItem item)
        {
            return GemStatService.Instance.GetItemGemBonuses(item);
        }

        /// <summary>
        /// Gets stat bonuses from a specific socketed gem.
        /// </summary>
        public CombatStatEntry[] GetGemStats(string gemId, int level)
        {
            return GemStatService.Instance.GetGemStats(gemId, level);
        }

        /// <summary>
        /// Generates gem stats for a given gem data and level.
        /// </summary>
        public CombatStatEntry[] GenerateGemStats(GemData gemData, int level)
        {
            return GemStatService.Instance.GenerateGemStats(gemData, level);
        }

        /// <summary>
        /// Gets the total stat value for a specific stat from all socketed gems.
        /// </summary>
        public float GetTotalStatValue(InventoryItem item, SecondaryStat stat)
        {
            return GemStatService.Instance.GetTotalStatValue(item, stat);
        }
        #endregion

        #region Public API - Socket Management
        /// <summary>
        /// Checks if a socket is unlocked for an item. Delegated to SocketService.
        /// </summary>
        public bool IsSocketUnlocked(InventoryItem item, int socketIndex)
        {
            return SocketService.Instance.IsSocketUnlocked(item, socketIndex);
        }

        /// <summary>
        /// Gets the level required to unlock a socket. Delegated to SocketService.
        /// </summary>
        public int GetSocketUnlockRequirement(int socketIndex)
        {
            return SocketService.Instance.GetUnlockRequirement(socketIndex);
        }

        /// <summary>
        /// Updates socket unlock states based on item's level. Delegated to SocketService.
        /// </summary>
        public void UpdateSocketUnlocks(InventoryItem item)
        {
            SocketService.Instance.UpdateSocketStates(item);
        }

        /// <summary>
        /// Gets all unlocked socket indices for an item. Delegated to SocketService.
        /// </summary>
        public int[] GetUnlockedSockets(InventoryItem item)
        {
            return SocketService.Instance.GetUnlockedSocketIndices(item);
        }

        /// <summary>
        /// Checks if a gem type is allowed in a socket. Delegated to SocketService.
        /// </summary>
        public bool IsGemTypeAllowed(int socketIndex, GemType gemType)
        {
            return SocketService.Instance.Config.SocketRules[socketIndex].CanInsertGem(gemType);
        }
        #endregion

        #region Public API - Query
        /// <summary>
        /// Checks if a gem is socketed at the given index.
        /// </summary>
        public bool IsGemSocketed(InventoryItem item, int socketIndex)
        {
            if (item?.Sockets == null || socketIndex < 0 || socketIndex >= item.Sockets.Length)
                return false;
            return !item.Sockets[socketIndex].IsEmpty;
        }

        /// <summary>
        /// Gets the runtime gem instance data for a socketed gem.
        /// </summary>
        public GemInstanceData GetSocketedGemData(InventoryItem item, int socketIndex)
        {
            return GetSocketedGemInstance(item, socketIndex);
        }

        /// <summary>
        /// Gets the total number of socketed gems on an item.
        /// </summary>
        public int GetTotalSocketedGems(InventoryItem item)
        {
            if (item?.Sockets == null) return 0;
            return item.Sockets.Count(s => !s.IsEmpty);
        }

        /// <summary>
        /// Checks if an item has an empty unlocked socket.
        /// </summary>
        public bool HasEmptySocket(InventoryItem item)
        {
            if (item?.Sockets == null) return false;
            foreach (var socket in item.Sockets)
            {
                if (socket.IsUnlocked && socket.IsEmpty)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// All live GemInstances (socketed gems), keyed by GemInstanceId.
        /// Persisted separately from the stack inventory (SaveData.SocketedGems) so
        /// level/experience survive restarts — socket.GemInstanceId alone is just a reference.
        /// </summary>
        public GemInstanceData[] GetSocketedGemsSaveData() => _socketedGems.Values.ToArray();

        /// <summary>
        /// Restores the runtime GemInstance registry before Rehydrate. Stable InstanceIds
        /// keep modifier keys (Gem:{id}) valid across saves.
        /// </summary>
        public void LoadSocketedGems(IEnumerable<GemInstanceData> instances)
        {
            _socketedGems.Clear();
            if (instances == null) return;
            foreach (var instance in instances)
            {
                if (instance != null && !string.IsNullOrEmpty(instance.InstanceId))
                    _socketedGems[instance.InstanceId] = instance;
            }
        }

        /// <summary>
        /// Rehydrates socketed GemInstanceData from persisted SocketData references.
        /// Called after InventoryService.LoadFromSaveData. Gem instances keep stable
        /// GemInstanceId so modifiers can be re-applied (GemModifierService is id-stable).
        /// </summary>
        public void RestoreSocketedGems(IEnumerable<InventoryItem> items)
        {
            if (items == null) return;

            foreach (var item in items)
            {
                if (item?.Sockets == null) continue;

                foreach (var socket in item.Sockets)
                {
                    if (socket == null || socket.IsEmpty) continue;

                    // LoadSocketedGems (SaveManager) restored the registry first; the socket's
                    // GemInstanceId reference wins. Only fall back to a fresh instance when the
                    // id was lost (pre-SocketedGems saves).
                    if (!_socketedGems.TryGetValue(socket.GemInstanceId, out var instance))
                    {
                        instance = GemFactory.Instance?.CreateGemInstanceFromSocket(socket.GemId, socket.GemLevel);
                        if (instance == null) continue;

                        socket.GemInstanceId = instance.InstanceId;
                        _socketedGems[instance.InstanceId] = instance;
                    }

                    // Re-apply modifiers with the restored instance (id-stable replace).
                    GemModifierService.Instance?.Apply(item, socket.SocketIndex, instance);
                }
            }
        }

        /// <summary>
        /// Counts how many gems of a specific type are socketed.
        /// </summary>
        public int CountGemType(InventoryItem item, GemType gemType)
        {
            if (item?.Sockets == null) return 0;

            int count = 0;
            foreach (var socket in item.Sockets)
            {
                if (!socket.IsEmpty)
                {
                    var gemData = ItemDatabase.Instance?.GetGem(socket.GemId);
                    if (gemData?.GemType == gemType)
                        count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Finds a gem instance by gem ID.
        /// </summary>
        public GemInstanceData FindGemById(InventoryItem item, string gemId)
        {
            if (item?.Sockets == null) return null;

            foreach (var socket in item.Sockets)
            {
                if (!socket.IsEmpty && socket.GemId == gemId)
                {
                    return GetSocketedGemInstance(item, socket.SocketIndex);
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the highest gem level among all socketed gems.
        /// </summary>
        public int GetHighestGemLevel(InventoryItem item)
        {
            if (item?.Sockets == null) return 0;

            int maxLevel = 0;
            foreach (var socket in item.Sockets)
            {
                if (!socket.IsEmpty && socket.GemLevel > maxLevel)
                    maxLevel = socket.GemLevel;
            }
            return maxLevel;
        }

        /// <summary>
        /// Gets the total gem power (sum of all gem levels).
        /// </summary>
        public int GetTotalGemPower(InventoryItem item)
        {
            if (item?.Sockets == null) return 0;

            int total = 0;
            foreach (var socket in item.Sockets)
            {
                if (!socket.IsEmpty)
                    total += socket.GemLevel;
            }
            return total;
        }
        #endregion

        #region Internal Helpers
        /// <summary>
        /// Re-applies the modifier for one socket's gem (id-stable: replace, no residue).
        /// </summary>
        private void ReapplyGemModifier(InventoryItem item, int socketIndex)
        {
            if (item?.Sockets == null || socketIndex < 0 || socketIndex >= item.Sockets.Length) return;
            var socket = item.Sockets[socketIndex];
            if (!socket.IsEmpty && GetSocketedGemInstance(item, socketIndex) is GemInstanceData gem)
                GemModifierService.Instance.Apply(item, socketIndex, gem);
        }

        /// <summary>
        /// Gets the GemInstanceData for a socketed gem.
        /// Uses the GemInstanceId stored in SocketData for O(1) lookup.
        /// </summary>
        private GemInstanceData GetSocketedGemInstance(InventoryItem item, int socketIndex)
        {
            if (item?.Sockets == null || socketIndex < 0 || socketIndex >= item.Sockets.Length)
                return null;

            var socket = item.Sockets[socketIndex];
            if (socket.IsEmpty || string.IsNullOrEmpty(socket.GemInstanceId)) return null;

            // Direct O(1) lookup by InstanceId stored in SocketData
            _socketedGems.TryGetValue(socket.GemInstanceId, out var instance);
            return instance;
        }
        #endregion
    }
}