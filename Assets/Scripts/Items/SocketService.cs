using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Economy;
using IdleDefenseSurvival.Items;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// Socket service - manages socket unlocking, adding, and locking operations.
    /// Configuration is loaded from dataConfigSocket.json at startup.
    /// Gem validation moved to SocketValidationService.
    /// </summary>
    public sealed class SocketService : MonoBehaviour
    {
        #region Singleton
        private static SocketService _instance;
        public static SocketService Instance => _instance;

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
            LoadConfig();
        }
        #endregion

        #region Events
        /// <summary>
        /// Fired when any socket state changes on an item.
        /// UI should refresh from item.Sockets directly.
        /// </summary>
        public event Action<InventoryItem, int> OnSocketChanged; // item, socketIndex
        #endregion

        #region Fields
        private SocketConfigData _config;
        private const int MAX_TOTAL_SOCKETS = 6; // Base 4 + 2 additional
        #endregion

        #region Initialization
        private void LoadConfig()
        {
            TextAsset json = Resources.Load<TextAsset>("Data/Gems/dataConfigSocket");
            if (json != null)
            {
                _config = JsonConvert.DeserializeObject<SocketConfigData>(json.text);
            }
            else
            {
                _config = CreateDefaultConfig();
                Debug.LogWarning("[SocketService] dataConfigSocket.json not found, using defaults");
            }

            // Ensure SocketRules array has at least MaxSocketsPerItem entries
            EnsureSocketRulesCapacity();
        }

        private SocketConfigData CreateDefaultConfig()
        {
            var config = new SocketConfigData
            {
                MaxSocketsPerItem = 6,
                CanAddSockets = true,
                AddSocketCost = new SocketCurrencyCost { CurrencyType = CurrencyType.Gold, Amount = 10000 },
                MaxAdditionalSockets = 2,
                SocketRules = new SocketRule[]
                {
                    new() { SocketIndex = 0, UnlockLevel = 0, AllowAnyGem = true },
                    new() { SocketIndex = 1, UnlockLevel = 5, AllowAnyGem = true },
                    new() { SocketIndex = 2, UnlockLevel = 10, AllowAnyGem = true },
                    new() { SocketIndex = 3, UnlockLevel = 15, AllowAnyGem = true },
                    new() { SocketIndex = 4, UnlockLevel = 20, AllowAnyGem = true },
                    new() { SocketIndex = 5, UnlockLevel = 25, AllowAnyGem = true },
                    new() { SocketIndex = 6, UnlockLevel = 30, AllowAnyGem = true },
                }
            };
            return config;
        }

        private void EnsureSocketRulesCapacity()
        {
            int required = _config.MaxSocketsPerItem + _config.MaxAdditionalSockets;
            if (_config.SocketRules == null || _config.SocketRules.Length < required)
            {
                Array.Resize(ref _config.SocketRules, required);
                for (int i = 0; i < _config.SocketRules.Length; i++)
                {
                    if (_config.SocketRules[i] == null)
                    {
                        _config.SocketRules[i] = new SocketRule
                        {
                            SocketIndex = i,
                            UnlockLevel = i * 5,
                            AllowAnyGem = true
                        };
                    }
                }
            }
        }
        #endregion

        #region Socket Unlocking
        /// <summary>
        /// Checks if a socket is unlocked based on item's level.
        /// </summary>
        public bool IsSocketUnlocked(InventoryItem item, int socketIndex)
        {
            if (item?.Sockets == null || socketIndex < 0 || socketIndex >= item.Sockets.Length)
                return false;

            return _config.IsSocketUnlocked(socketIndex, item.Level);
        }

        /// <summary>
        /// Updates all socket unlock states for an item based on its level.
        /// </summary>
        public void UpdateSocketStates(InventoryItem item)
        {
            if (item?.Sockets == null) return;

            bool changed = false;
            for (int i = 0; i < item.Sockets.Length; i++)
            {
                bool shouldUnlock = _config.IsSocketUnlocked(i, item.Level);
                if (item.Sockets[i].IsUnlocked != shouldUnlock)
                {
                    item.Sockets[i].IsUnlocked = shouldUnlock;
                    changed = true;

                    if (shouldUnlock)
                        OnSocketChanged?.Invoke(item, i);
                }
            }

            if (changed)
            {
                InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Socket);
            }
        }

        /// <summary>
        /// Gets the level required to unlock a specific socket index.
        /// </summary>
        public int GetUnlockRequirement(int socketIndex)
        {
            return _config.GetUnlockRequirement(socketIndex);
        }

        /// <summary>
        /// Gets all currently unlocked socket indices.
        /// </summary>
        public int[] GetUnlockedSocketIndices(InventoryItem item)
        {
            if (item?.Sockets == null) return Array.Empty<int>();

            var list = new List<int>();
            for (int i = 0; i < item.Sockets.Length; i++)
            {
                if (item.Sockets[i].IsUnlocked)
                    list.Add(i);
            }
            return list.ToArray();
        }

        /// <summary>
        /// Gets the next socket that will be unlocked and at what level.
        /// </summary>
        public (int socketIndex, int requiredLevel) GetNextUnlock(InventoryItem item)
        {
            if (item?.Sockets == null) return (-1, -1);

            for (int i = 0; i < item.Sockets.Length; i++)
            {
                if (!item.Sockets[i].IsUnlocked)
                {
                    int required = GetUnlockRequirement(i);
                    return (i, required);
                }
            }
            return (-1, -1); // All unlocked
        }
        #endregion

        #region Socket Addition (Beyond Base Max)
        /// <summary>
        /// Checks if an item can have an additional socket added.
        /// </summary>
        public bool CanAddSocket(InventoryItem item)
        {
            if (item == null) return false;
            if (!_config.CanAddSockets) return false;

            int currentSockets = item.Sockets?.Length ?? 0;
            int baseMax = ItemDatabase.Instance?.GetMaxSockets(item.ItemId) ?? 0;
            int additionalSockets = currentSockets - baseMax;

            return additionalSockets < _config.MaxAdditionalSockets;
        }

        /// <summary>
        /// Adds an additional socket to an item (beyond its base max sockets).
        /// Costs currency and has a limit.
        /// </summary>
        public bool AddSocket(InventoryItem item)
        {
            if (!CanAddSocket(item)) return false;

            int currentSockets = item.Sockets?.Length ?? 0;

            // Spend currency
            if (!EconomyManager.Instance.TrySpendCurrency(_config.AddSocketCost.CurrencyType, _config.AddSocketCost.Amount, "Add Socket"))
                return false;

            // Use fixed-size array with count
            var newSockets = new SocketData[MAX_TOTAL_SOCKETS];
            if (item.Sockets != null)
                Array.Copy(item.Sockets, newSockets, currentSockets);

            // Initialize new socket
            var newSocket = new SocketData
            {
                SocketIndex = currentSockets,
                IsUnlocked = false,
                IsLocked = false
            };
            newSockets[currentSockets] = newSocket;

            item.Sockets = newSockets;

            OnSocketChanged?.Invoke(item, currentSockets);
            InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Socket);
            return true;
        }

        /// <summary>
        /// Gets the cost to add a socket.
        /// </summary>
        public SocketCurrencyCost GetAddSocketCost() => _config.AddSocketCost;

        /// <summary>
        /// Gets remaining additional sockets that can be added.
        /// </summary>
        public int GetRemainingAdditionalSockets(InventoryItem item)
        {
            if (item == null) return 0;
            int currentSockets = item.Sockets?.Length ?? 0;
            int baseMax = ItemDatabase.Instance?.GetMaxSockets(item.ItemId) ?? 0;
            int additionalSockets = currentSockets - baseMax;
            return Math.Max(0, _config.MaxAdditionalSockets - additionalSockets);
        }
        #endregion

        #region Socket Locking (Prevents Gem Removal)
        /// <summary>
        /// Locks a socket to prevent gem removal.
        /// </summary>
        public void LockSocket(InventoryItem item, int socketIndex)
        {
            if (item?.Sockets == null || socketIndex < 0 || socketIndex >= item.Sockets.Length) return;
            item.Sockets[socketIndex].IsLocked = true;
            OnSocketChanged?.Invoke(item, socketIndex);
            InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Socket);
        }

        /// <summary>
        /// Unlocks a socket to allow gem removal.
        /// </summary>
        public void UnlockSocket(InventoryItem item, int socketIndex)
        {
            if (item?.Sockets == null || socketIndex < 0 || socketIndex >= item.Sockets.Length) return;
            item.Sockets[socketIndex].IsLocked = false;
            OnSocketChanged?.Invoke(item, socketIndex);
            InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Socket);
        }

        /// <summary>
        /// Checks if a socket is locked (prevents gem removal).
        /// </summary>
        public bool IsSocketLocked(InventoryItem item, int socketIndex)
        {
            if (item?.Sockets == null || socketIndex < 0 || socketIndex >= item.Sockets.Length) return false;
            return item.Sockets[socketIndex].IsLocked;
        }
        #endregion

        #region Configuration Access (Read-Only)
        public SocketConfigData Config => _config;
        #endregion
    }
}