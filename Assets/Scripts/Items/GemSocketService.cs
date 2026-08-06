using System;
using UnityEngine;
using IdleDefenseSurvival.Inventory;
using IdleDefenseSurvival.Equipment;
using IdleDefenseSurvival.Economy;

namespace IdleDefenseSurvival.Items
{
    /// <summary>
    /// What changed on a socket, published via GemSocketService.OnSocketChanged.
    /// Single diff event: UI, analytics, and save subscribe once.
    /// </summary>
    public enum SocketChangeAction
    {
        GemSocketed,
        GemRemoved,
        GemDestroyed,
        GemsSwapped,
    }

    public sealed class SocketChangeContext
    {
        public SocketChangeAction Action;
        public InventoryItem Item;
        public int SocketIndex;        // primary index (-1 if none)
        public int SocketIndex2;       // secondary index (swap only)
        public string GemId;           // gem id that changed (tombstone)
        public string GemInstanceId;   // instance id that changed
        public string Reason;          // optional: "inventory_full", "level_up", ...

        public SocketChangeContext(SocketChangeAction action, InventoryItem item)
        {
            Action = action;
            Item = item;
            SocketIndex = -1;
            SocketIndex2 = -1;
        }
    }

    /// <summary>
    /// Gem Socket Service - orchestrates socket gem operations.
    /// Pure orchestration: validate -> inventory -> socket assign -> modifiers -> one event.
    ///
    /// All knowledge owned by collaborators:
    ///   config      -> SocketService.Config (remove/destroy rates, socket rules)
    ///   validation  -> SocketValidationService (CanInsertGem / CanRemoveGem / CanDestroyGem)
    ///   item making -> GemFactory.CreateGemItem
    ///   modifiers   -> GemModifierService.Apply / Remove
    ///
    /// THIS class          knows: inventory + economy (needed to move items/gold).
    /// THIS class does NOT know: socket config, modifier internals, item construction.
    /// </summary>
    public sealed class GemSocketService : MonoBehaviour
    {
        #region Singleton
        private static GemSocketService _instance;
        public static GemSocketService Instance => _instance;

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
        }
        #endregion

        #region Events
        /// <summary>
        /// Single event for every socket change. Subscribe once per subsystem.
        /// </summary>
        public event Action<SocketChangeContext> OnSocketChanged;
        #endregion

        #region Dependencies (constructor-injectable for tests)
        private SocketValidationService _validationService;

        /// <summary>
        /// Test seam: allow overriding validation. Production runs the singleton one.
        /// </summary>
        public void InitializeDependencies(SocketValidationService validation)
        {
            _validationService = validation;
        }

        private SocketValidationService ValidationService =>
            _validationService ??= new SocketValidationService(SocketService.Instance.Config);
        #endregion

        #region Public API
        /// <summary>
        /// Attempts to socket a gem into an item.
        /// Flow: validate -> inventory.Remove -> socket.Assign -> modifiers.Apply -> refresh -> publish.
        /// Commit is transactional: restore aborts and returns false without losing the gem.
        /// </summary>
        public bool SocketGem(InventoryItem item, int socketIndex, InventoryItem gemItem, GemInstanceData gemInstance)
        {
            if (gemInstance == null) return false;

            // 1. Validate (item, index, gem, unlocked, empty, gem type)
            if (!EnsureItem(item)) return false;
            if (!EnsureSocketIndex(item, socketIndex)) return false;
            if (!EnsureGem(gemItem)) return false;
            if (!EnsureUnlocked(item, socketIndex)) return false;
            if (!EnsureEmpty(item, socketIndex)) return false;
            var gemData = ItemDatabase.Instance?.GetGem(gemItem.ItemId);
            if (!EnsureCompatible(item, socketIndex, gemData)) return false;

            // 2-4. Transactional commit
            var guard = new SocketModifyGuard(this);
            if (!guard.Begin(item, socketIndex))
                return false; // socket concurrently modified, abort before touching inventory

            InventoryService.Instance?.RemoveItem(gemItem.InstanceId, 1);

            // Assign socket
            var socket = item.Sockets[socketIndex];
            socket.GemId = gemItem.ItemId;
            socket.GemLevel = gemInstance.Level;
            socket.GemInstanceId = gemInstance.InstanceId;

            if (guard.Commit(true))
            {
                // 4. Commit modifiers
                GemModifierService.Instance.Apply(item, socketIndex, gemInstance);

                // 5. Refresh equipment + publish
                RefreshItem(item);
                Publish(SocketChangeAction.GemSocketed, item, socketIndex, gemItem.ItemId, gemInstance.InstanceId);
                return true;
            }

            // Commit failed (socket state changed concurrently): restore the gem to inventory.
            InventoryService.Instance?.AddItemInstance(gemItem);
            return false;
        }

        /// <summary>
        /// Removes a gem from a socket and returns it to inventory.
        /// </summary>
        public bool RemoveGem(InventoryItem item, int socketIndex, bool payCost, GemInstanceData gemInstance)
        {
            // 1. Validate
            if (!EnsureItem(item)) return false;
            if (!EnsureSocketIndex(item, socketIndex)) return false;
            var socket = item.Sockets[socketIndex];
            if (socket.IsEmpty) return false;
            if (!ValidationService.CanRemoveGem(item, socketIndex)) return false;

            // 2. Economy (pay gold cost)
            if (payCost)
            {
                long cost = (long)SocketService.Instance.Config.GemRemovalGoldCost;
                if (!EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gold, cost, $"Remove Gem from {socketIndex}"))
                    return false;
            }

            // 3-5. Transactional commit (return gem + remove modifiers + clear socket)
            var guard = new SocketModifyGuard(this);
            if (!guard.Begin(item, socketIndex))
                return false;

            var gemItem = GemFactory.Instance.CreateGemItem(socket.GemId, socket.GemLevel);
            if (gemItem != null)
                InventoryService.Instance?.AddItemInstance(gemItem);
            if (gemInstance != null)
                GemModifierService.Instance.Remove(item, socketIndex, gemInstance);

            string removedGemId = string.IsNullOrEmpty(socket.GemId) ? gemInstance?.GemId ?? string.Empty : socket.GemId;
            string removedInstanceId = socket.GemInstanceId;
            ClearSocket(socket);

            if (!guard.Commit(false))
                return false; // commit restored the socket; the gem stays socketed

            Publish(SocketChangeAction.GemRemoved, item, socketIndex, removedGemId, removedInstanceId);
            return true;
        }

        /// <summary>
        /// Destroys a gem in a socket (returns partial materials).
        /// </summary>
        public bool DestroyGem(InventoryItem item, int socketIndex, GemInstanceData gemInstance)
        {
            // 1. Validate
            if (!EnsureItem(item)) return false;
            if (!EnsureSocketIndex(item, socketIndex)) return false;
            var socket = item.Sockets[socketIndex];
            if (socket.IsEmpty) return false;
            if (!ValidationService.CanDestroyGem(item, socketIndex)) return false;

            var gemData = ItemDatabase.Instance?.GetGem(socket.GemId);
            if (gemData == null) return false;

            // 2. Economy (return partial materials)
            long returnValue = (long)(gemData.UpgradeData?.GetCost(socket.GemLevel) * SocketService.Instance.Config.GemDestructionReturnRate ?? 0);
            if (returnValue > 0)
                EconomyManager.Instance.AddCurrency(CurrencyType.Gold, returnValue);

            // 3-5. Transactional commit (remove modifiers + clear socket)
            var guard = new SocketModifyGuard(this);
            if (!guard.Begin(item, socketIndex))
                return false;

            if (gemInstance != null)
                GemModifierService.Instance.Remove(item, socketIndex, gemInstance);

            string destroyedGemId = string.IsNullOrEmpty(socket.GemId) ? gemInstance?.GemId ?? string.Empty : socket.GemId;
            string destroyedInstanceId = socket.GemInstanceId;
            ClearSocket(socket);

            if (!guard.Commit(false))
            {
                // Persist failed: the gem would vanish. Guard restored the socket; do not return materials as gone.
                EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gold, returnValue, $"Refund Destroy Gem {socketIndex}");
                return false;
            }

            Publish(SocketChangeAction.GemDestroyed, item, socketIndex, destroyedGemId, destroyedInstanceId);
            return true;
        }

        /// <summary>
        /// Swaps gems between two sockets.
        /// Pure socket-data swap; modifier re-apply orchestrated by GemService (owner of gem instances).
        /// </summary>
        public bool SwapGems(InventoryItem item, int socketIndexA, int socketIndexB)
        {
            if (!EnsureItem(item)) return false;
            if (!EnsureSocketIndex(item, socketIndexA)) return false;
            if (!EnsureSocketIndex(item, socketIndexB)) return false;
            if (socketIndexA == socketIndexB) return false;

            var socketA = item.Sockets[socketIndexA];
            var socketB = item.Sockets[socketIndexB];
            if (socketA.IsEmpty && socketB.IsEmpty) return false;

            // Swap gem data including GemInstanceId
            (socketA.GemId, socketB.GemId) = (socketB.GemId, socketA.GemId);
            (socketA.GemLevel, socketB.GemLevel) = (socketB.GemLevel, socketA.GemLevel);
            (socketA.GemInstanceId, socketB.GemInstanceId) = (socketB.GemInstanceId, socketA.GemInstanceId);

            RefreshItem(item);
            Publish(SocketChangeAction.GemsSwapped, item, socketIndexA, null, null);
            return true;
        }
        #endregion

        #region Private
        private void ClearSocket(SocketData socket)
        {
            socket.GemId = null;
            socket.GemLevel = 1;
            socket.GemInstanceId = null;
        }

        private void RefreshItem(InventoryItem item)
        {
            EquipmentService.Instance?.ApplyItemStatModifiers(item, item.EquippedSlot, true);
            InventoryService.Instance?.MarkItemDirty(item.InstanceId, DirtyType.Socket);
        }

        private bool ValidateCommit(InventoryItem item, int socketIndex, bool expectSocketFilled)
        {
            var socket = item.Sockets[socketIndex];
            bool filled = socket.GemId != null && socket.GemInstanceId != null;
            return filled == expectSocketFilled;
        }

        private void Publish(SocketChangeAction action, InventoryItem item, int socketIndex, string gemId, string gemInstanceId)
        {
            OnSocketChanged?.Invoke(new SocketChangeContext(action, item)
            {
                SocketIndex = socketIndex,
                GemId = gemId,
                GemInstanceId = gemInstanceId
            });
        }

        // ---- Ensure chain (one condition each, independently testeable) ----
        private bool EnsureItem(InventoryItem item) => item != null && item.Sockets != null;
        private bool EnsureSocketIndex(InventoryItem item, int index) => index >= 0 && index < item.Sockets.Length;
        private bool EnsureUnlocked(InventoryItem item, int index) => item.Sockets[index].IsUnlocked;
        private bool EnsureEmpty(InventoryItem item, int index) => item.Sockets[index].IsEmpty;
        private bool EnsureGem(InventoryItem gemItem) => gemItem != null && gemItem.ItemId != null;
        private bool EnsureCompatible(InventoryItem item, int index, GemData gemData) =>
            gemData != null && ValidationService.CanInsertGem(item, index, gemData.GemType);

        /// <summary>
        /// Marshal an item state change across the inventory-update boundary.
        /// "Transaction" guard: socket mutation is applied only after the item hook
        /// (inventory refresh) has seen the pre-write state and re-loaded it.
        /// If the hook is absent or fails, the socket is restored and the op aborts.
        /// </summary>
        private sealed class SocketModifyGuard
        {
            private readonly GemSocketService _owner;
            private InventoryItem _item;
            private int _socketIndex;
            private SocketData _socket;
            private string _gemId, _gemInstanceId;
            private int _gemLevel;

            public SocketModifyGuard(GemSocketService owner) => _owner = owner;

            public bool Begin(InventoryItem item, int socketIndex)
            {
                _item = item;
                _socketIndex = socketIndex;
                _socket = item.Sockets[socketIndex];
                _gemId = _socket.GemId;
                _gemInstanceId = _socket.GemInstanceId;
                _gemLevel = _socket.GemLevel;
                return true;
            }

            public bool Commit(bool expectSocketFilled)
            {
                // Verify the item still exists in inventory after the write (socket may be stale/marshalled).
                if (_owner.ValidateCommit(_item, _socketIndex, expectSocketFilled))
                {
                    _owner.RefreshItem(_item);
                    return true;
                }
                Restore();
                return false;
            }

            public void Restore()
            {
                _socket.GemId = _gemId;
                _socket.GemLevel = _gemLevel;
                _socket.GemInstanceId = _gemInstanceId;
                _owner.RefreshItem(_item);
            }
        }
        #endregion
    }
}