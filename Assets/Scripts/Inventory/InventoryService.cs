using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Economy;

namespace IdleDefenseSurvival.Inventory
{
    /// <summary>
    /// Inventory service implementation - core inventory operations.
    /// Uses event-driven architecture with dirty flags for performance.
    /// </summary>
    public sealed class InventoryService : MonoBehaviour, IInventoryService
    {
        #region Singleton
        private static InventoryService _instance;
        public static InventoryService Instance => _instance;

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

        #region Events
        public event Action<InventoryChangedEventArgs> OnInventoryChanged;
        public event Action<InventoryItem> OnItemAdded;
        public event Action<InventoryItem> OnItemRemoved;
        public event Action<InventoryItem, int> OnItemQuantityChanged;
        public event Action<int> OnCapacityChanged;
        public event Action OnInventoryFiltered;

        /// <summary>
        /// Event fired when specific slots have dirty flags. More granular than OnInventoryChanged.
        /// Key = slot index, Value = dirty flags.
        /// </summary>
        public event Action<Dictionary<int, DirtyType>> OnSlotsDirty;
        #endregion

        #region Fields
        private InventoryConfig _config = new();
        private readonly List<InventorySlot> _slots = new();
        private InventoryFilter _currentFilter = new();
        private bool _isFiltered = false;
        private readonly List<int> _filteredIndices = new();

        // Dirty flags for optimization - granular per-slot tracking
        private readonly Dictionary<int, DirtyType> _dirtySlots = new();

        #endregion

        #region Properties
        public int Capacity => _config.Width * _config.Height;
        public int UsedSlots => _slots.Count(s => !s.IsEmpty);
        public int FreeSlots => Capacity - UsedSlots;
        public int MaxStackSize => 999;
        public InventoryConfig Config => _config;
        public IReadOnlyList<InventorySlot> Slots => _slots;
        public IReadOnlyList<InventoryItem> AllItems => _slots.Where(s => !s.IsEmpty).Select(s => s.Item).ToList();

        /// <summary>
        /// Gets all dirty slots with their flags.
        /// </summary>
        public IReadOnlyDictionary<int, DirtyType> DirtySlots => _dirtySlots;
        #endregion

        #region Initialization
        public void Initialize()
        {
            _config = new InventoryConfig();
            CreateSlots(_config.BaseCapacity);
        }

        private void CreateSlots(int count)
        {
            _slots.Clear();
            for (int i = 0; i < count; i++)
            {
                _slots.Add(new InventorySlot { SlotIndex = i });
            }
            MarkAllSlotsDirty(DirtyType.All);
        }
        #endregion

        #region Core Operations
        public string AddItem(string itemId, int quantity = 1)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0) return string.Empty;

            var itemData = ItemDatabase.Instance?.GetItem(itemId);
            if (itemData == null)
            {
                Debug.LogError($"[InventoryService] Item not found: {itemId}");
                return string.Empty;
            }

            // ---- Guard: equipment must not use this path ----
            if (itemData.Category == ItemCategory.Equipment)
            {
                Debug.LogError(
                    $"[InventoryService] AddItem(string) cannot be used for equipment '{itemId}'. " +
                    "Generated equipment must be added via AddItemInstance(InventoryItem) or AddGeneratedItem()."
                );
                return string.Empty;
            }
            // ------------------------------------------------

            int maxStack = itemData.StackSize > 0 ? itemData.StackSize : MaxStackSize;

            // Try to stack with existing items first
            if (itemData.IsStackable)
            {
                foreach (var slot in _slots)
                {
                    if (!slot.IsEmpty && slot.Item.ItemId == itemId && slot.Item.CanStackWith(slot.Item))
                    {
                        int canAdd = Math.Min(quantity, maxStack - slot.Item.Quantity);
                        if (canAdd > 0)
                        {
                            slot.Item.Quantity += canAdd;
                            quantity -= canAdd;
                            NotifyQuantityChanged(slot.Item, canAdd, slot.SlotIndex);

                            if (quantity <= 0) return slot.Item.GetStackKey() ?? slot.Item.ItemId;
                        }
                    }
                }
            }

            // Add new stacks for remaining quantity
            while (quantity > 0)
            {
                int emptySlot = FindEmptySlot();
                if (emptySlot < 0)
                {
                    if (!ExpandCapacity(1))
                    {
                        Debug.LogWarning("[InventoryService] Inventory full!");
                        break;
                    }
                    emptySlot = FindEmptySlot();
                }

                int addAmount = Math.Min(quantity, maxStack);
                var newItem = CreateInventoryItem(itemId, addAmount);
                _slots[emptySlot].Item = newItem;
                quantity -= addAmount;

                NotifyAdded(newItem, emptySlot);

                if (quantity <= 0) return newItem.GetStackKey() ?? newItem.InstanceId;
            }

            return string.Empty;
        }

        /// <summary>
        /// Adds a fully-generated equipment instance. Preserves all rarity-rolled fields.
        /// Returns true on success, false on failure (e.g., inventory full).
        /// </summary>
        public bool AddGeneratedItem(InventoryItem generatedItem)
        {
            if (generatedItem == null)
            {
                Debug.LogError("[InventoryService] AddGeneratedItem called with null item.");
                return false;
            }

            if (!generatedItem.IsEquippable())
            {
                Debug.LogError($"[InventoryService] AddGeneratedItem expects equipment, got '{generatedItem.ItemId}'.");
                return false;
            }

            // Directly forward to core instance-add path – no re-creation.
            return AddItemInstance(generatedItem);
        }

        public bool AddItemInstance(InventoryItem item)
        {
            if (item == null) return false;

            // Try stacking first (stackables of the same item; equipment never stacks)
            if (item.CanStackWith(item))
            {
                Debug.Log($"[InventoryService] CanStackWith item");
                foreach (var slot in _slots)
                {
                    if (!slot.IsEmpty && slot.Item.CanStackWith(item) && !slot.Item.IsMaxStack)
                    {
                        int canAdd = Math.Min(item.Quantity, slot.Item.GetMaxStackSize() - slot.Item.Quantity);
                        if (canAdd > 0)
                        {
                            slot.Item.Quantity += canAdd;
                            item.Quantity -= canAdd;
                            NotifyQuantityChanged(slot.Item, canAdd, slot.SlotIndex);
                            if (item.Quantity <= 0) return true;
                        }
                    }
                }
            }

            // Place remainder in empty slot
            if (item.Quantity > 0)
            {
                Debug.Log($"[InventoryService] item FindEmptySlot");
                int emptySlot = FindEmptySlot();
                if (emptySlot < 0 && !ExpandCapacity(1))
                {
                    emptySlot = FindEmptySlot();
                }
                if (emptySlot >= 0)
                {
                    _slots[emptySlot].Item = item;
                    NotifyAdded(item, emptySlot);
                    return true;
                }
            }
            return false;
        }

        public int RemoveItem(string instanceId, int quantity = 1)
        {
            if (string.IsNullOrEmpty(instanceId) || quantity <= 0) return 0;

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty && MatchKey(slot.Item, instanceId))
                {
                    int removed = Math.Min(quantity, slot.Item.Quantity);
                    slot.Item.Quantity -= removed;

                    if (slot.Item.Quantity <= 0)
                    {
                        var removedItem = slot.Item;
                        slot.Item = null;
                        NotifyRemoved(removedItem, i);
                    }
                    else
                    {
                        NotifyQuantityChanged(slot.Item, -removed, i);
                    }
                    return removed;
                }
            }
            return 0;
        }

        public int RemoveItemById(string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0) return 0;

            int totalRemoved = 0;
            for (int i = 0; i < _slots.Count && totalRemoved < quantity; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty && slot.Item.ItemId == itemId)
                {
                    int canRemove = Math.Min(quantity - totalRemoved, slot.Item.Quantity);
                    slot.Item.Quantity -= canRemove;
                    totalRemoved += canRemove;

                    if (slot.Item.Quantity <= 0)
                    {
                        var removedItem = slot.Item;
                        slot.Item = null;
                        NotifyRemoved(removedItem, i);
                    }
                    else
                    {
                        NotifyQuantityChanged(slot.Item, -canRemove, i);
                    }
                }
            }
            return totalRemoved;
        }

        public bool MoveItem(int fromSlot, int toSlot)
        {
            if (fromSlot < 0 || fromSlot >= _slots.Count || toSlot < 0 || toSlot >= _slots.Count || fromSlot == toSlot)
                return false;

            var from = _slots[fromSlot];
            var to = _slots[toSlot];

            if (from.IsEmpty) return false;

            // Try to stack
            if (!to.IsEmpty && from.Item.CanStackWith(to.Item))
            {
                int maxStack = to.Item.GetMaxStackSize();
                int canMove = Math.Min(from.Item.Quantity, maxStack - to.Item.Quantity);
                if (canMove > 0)
                {
                    to.Item.Quantity += canMove;
                    from.Item.Quantity -= canMove;
                    NotifyQuantityChanged(to.Item, canMove, toSlot);
                    NotifyQuantityChanged(from.Item, -canMove, fromSlot);

                    if (from.Item.Quantity <= 0)
                    {
                        var removed = from.Item;
                        from.Item = null;
                        NotifyRemoved(removed, fromSlot);
                    }
                    NotifyMoved(from.Item, fromSlot, toSlot);
                    return true;
                }
            }

            // Swap if target not empty
            if (!to.IsEmpty)
            {
                return SwapItems(fromSlot, toSlot);
            }

            // Move to empty slot
            to.Item = from.Item;
            from.Item = null;
            NotifyMoved(to.Item, fromSlot, toSlot);
            return true;
        }

        public bool SwapItems(int slotA, int slotB)
        {
            if (slotA < 0 || slotA >= _slots.Count ||
                slotB < 0 || slotB >= _slots.Count ||
                slotA == slotB)
                return false;
            (_slots[slotB].Item, _slots[slotA].Item) = (_slots[slotA].Item, _slots[slotB].Item);
            NotifySwapped(slotA, slotB);
            return true;
        }

        public InventoryItem SplitStack(string instanceId, int amount)
        {
            if (string.IsNullOrEmpty(instanceId) || amount <= 0) return null;

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty && MatchKey(slot.Item, instanceId))
                {
                    var splitItem = slot.Item.SplitStack(amount);
                    if (splitItem != null)
                    {
                        int emptySlot = FindEmptySlot();
                        if (emptySlot >= 0)
                        {
                            _slots[emptySlot].Item = splitItem;
                            NotifyAdded(splitItem, emptySlot);
                            NotifyQuantityChanged(slot.Item, -amount, i);
                            return splitItem;
                        }
                        else
                        {
                            // No space - merge back
                            slot.Item.Quantity += amount;
                        }
                    }
                    break;
                }
            }
            return null;
        }

        public int MergeStacks()
        {
            int merges = 0;
            var itemGroups = _slots
                .Where(s => !s.IsEmpty && s.Item.IsStackable)
                .GroupBy(s => s.Item.GetStackKey())
                .ToList();

            foreach (var group in itemGroups)
            {
                var items = group.Select(s => s.Item).OrderByDescending(i => i.Quantity).ToList();
                for (int i = 1; i < items.Count; i++)
                {
                    int maxStack = items[0].GetMaxStackSize();
                    int space = maxStack - items[0].Quantity;
                    if (space > 0)
                    {
                        int move = Math.Min(items[i].Quantity, space);
                        items[0].Quantity += move;
                        items[i].Quantity -= move;
                        merges++;

                        // Find slots and notify
                        int slot0 = _slots.FindIndex(s => s.Item == items[0]);
                        int slotI = _slots.FindIndex(s => s.Item == items[i]);
                        if (slot0 >= 0) NotifyQuantityChanged(items[0], move, slot0);
                        if (slotI >= 0) NotifyQuantityChanged(items[i], -move, slotI);

                        if (items[i].Quantity <= 0)
                        {
                            var slot = _slots.FirstOrDefault(s => s.Item == items[i]);
                            if (slot != null)
                            {
                                NotifyRemoved(items[i], slot.SlotIndex);
                                slot.Item = null;
                            }
                        }
                    }
                }
            }
            return merges;
        }
        #endregion

        #region Query Operations
        public InventoryItem GetItem(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return null;
            return _slots.FirstOrDefault(s => !s.IsEmpty && MatchKey(s.Item, instanceId))?.Item;
        }

        /// <summary>
        /// Key match for identity APIs: equipment by InstanceId, stackables by StackKey
        /// (ItemId or ItemId~StackId). Callers pass the item's GetStackKey() when InstanceId is null.
        /// </summary>
        private static bool MatchKey(InventoryItem item, string key)
        {
            if (string.IsNullOrEmpty(key) || item == null) return false;
            if (item.InstanceId == key) return true; // equipment
            return item.GetStackKey() == key;        // stackables (ItemId / ItemId~StackId)
        }

        public InventoryItem GetItemAtSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count) return null;
            return _slots[slotIndex].Item;
        }

        public IReadOnlyList<InventoryItem> GetItemsById(string itemId)
        {
            return _slots.Where(s => !s.IsEmpty && s.Item.ItemId == itemId).Select(s => s.Item).ToList();
        }

        public IReadOnlyList<InventoryItem> GetItemsByCategory(ItemCategory category)
        {
            return _slots.Where(s => !s.IsEmpty && s.Item.GetItemCategory() == category).Select(s => s.Item).ToList();
        }

        public IReadOnlyList<InventoryItem> GetItemsByRarity(Rarity rarity)
        {
            return _slots.Where(s => !s.IsEmpty && s.Item.GetRarity() == rarity).Select(s => s.Item).ToList();
        }

        public IReadOnlyList<InventoryItem> GetEquipments()
        {
            return _slots.Where(s => !s.IsEmpty && s.Item.IsEquippable()).Select(s => s.Item).ToList();
        }

        public IReadOnlyList<InventoryItem> GetEquipmentsByType(EquipmentType type)
        {
            return _slots.Where(s => !s.IsEmpty && s.Item.GetEquipmentType() == type).Select(s => s.Item).ToList();
        }

        public IReadOnlyList<InventoryItem> GetMergeableStacks()
        {
            return _slots.Where(s => !s.IsEmpty && s.Item.IsStackable && !s.Item.IsMaxStack).Select(s => s.Item).ToList();
        }

        public int GetTotalQuantity(string itemId)
        {
            return _slots.Where(s => !s.IsEmpty && s.Item.ItemId == itemId).Sum(s => s.Item.Quantity);
        }

        public bool HasSpaceFor(string itemId, int quantity = 1)
        {
            var itemData = ItemDatabase.Instance?.GetItem(itemId);
            if (itemData == null) return false;

            int maxStack = itemData.StackSize > 0 ? itemData.StackSize : MaxStackSize;
            int existingSpace = _slots.Where(s => !s.IsEmpty && s.Item.ItemId == itemId && s.Item.IsStackable)
                .Sum(s => maxStack - s.Item.Quantity);
            int emptySlots = FreeSlots * maxStack;

            return (existingSpace + emptySlots) >= quantity;
        }

        public bool HasItem(string itemId, int quantity = 1)
        {
            return GetTotalQuantity(itemId) >= quantity;
        }
        #endregion

        #region Filtering
        public void SetFilter(InventoryFilter filter)
        {
            _currentFilter = filter ?? new InventoryFilter();
            _isFiltered = !_currentFilter.IsEmpty;
            RebuildFilteredIndices();
            OnInventoryFiltered?.Invoke();
        }

        public void ClearFilter()
        {
            _currentFilter = new InventoryFilter();
            _isFiltered = false;
            _filteredIndices.Clear();
            OnInventoryFiltered?.Invoke();
        }

        public InventoryFilter GetCurrentFilter() => _currentFilter;
        #endregion

        #region Item Flags
        public void SetFavorite(string instanceId, bool favorite)
        {
            var item = GetItem(instanceId);
            if (item != null)
            {
                item.IsFavorite = favorite;
                int slotIndex = _slots.FindIndex(s => s.Item == item);
                if (slotIndex >= 0)
                {
                    MarkDirty(slotIndex, DirtyType.Favorite);
                    FlushDirtySlots();
                    OnInventoryChanged?.Invoke(InventoryChangedEventArgs.CreateRemoved(instanceId, item.ItemId, slotIndex, 0, item));
                }
            }
        }

        public void SetLocked(string instanceId, bool locked)
        {
            var item = GetItem(instanceId);
            if (item != null)
            {
                item.IsLocked = locked;
                int slotIndex = _slots.FindIndex(s => s.Item == item);
                if (slotIndex >= 0)
                {
                    MarkDirty(slotIndex, DirtyType.Lock);
                    FlushDirtySlots();
                    OnInventoryChanged?.Invoke(InventoryChangedEventArgs.CreateRemoved(instanceId, item.ItemId, slotIndex, 0, item));
                }
            }
        }

        public void MarkAsSeen(string instanceId)
        {
            var item = GetItem(instanceId);
            if (item != null)
            {
                item.IsNew = false;
                int slotIndex = _slots.FindIndex(s => s.Item == item);
                if (slotIndex >= 0) MarkDirty(slotIndex, DirtyType.Item | DirtyType.Tooltip);
            }
        }
        public bool MarkAsSeenAtSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count) return false;
            var item = _slots[slotIndex].Item;
            if (item == null || !item.IsNew) return false;
            item.IsNew = false;
            MarkDirty(slotIndex, DirtyType.Item | DirtyType.Tooltip);
            MarkSaveDirty();
            FlushDirtySlots();
            return true;
        }
        #endregion

        #region Capacity Management
        public bool ExpandCapacity(int slots = 1)
        {
            if (Capacity >= _config.MaxCapacity) return false;

            int cost = GetExpansionCost();
            if (!EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gem, cost, "Inventory Expansion"))
                return false;

            int oldCapacity = Capacity;
            int newSlots = Math.Min(slots, _config.MaxCapacity - Capacity);
            int newWidth = _config.Width;
            int newHeight = _config.Height + (newSlots + _config.Width - 1) / _config.Width;

            _config.Width = newWidth;
            _config.Height = newHeight;

            int slotsToAdd = Capacity - oldCapacity;
            int startIndex = _slots.Count;
            for (int i = 0; i < slotsToAdd; i++)
                _slots.Add(new InventorySlot { SlotIndex = _slots.Count });
            // Mark new slots as dirty
            for (int i = startIndex; i < _slots.Count; i++)
                MarkDirty(i, DirtyType.Item);

            OnCapacityChanged?.Invoke(Capacity);
            return true;
        }

        public int GetExpansionCost() => _config.GetExpansionCost((Capacity - _config.BaseCapacity) / _config.SlotsPerExpansion);
        public int GetMaxCapacity() => _config.MaxCapacity;
        #endregion

        #region Quick Actions
        public long QuickSell(IEnumerable<string> instanceIds)
        {
            long total = 0;
            foreach (var id in instanceIds)
            {
                var item = GetItem(id);
                if (item != null && !item.IsLocked && !item.IsFavorite)
                {
                    long price = ItemDatabase.Instance?.GetSellPrice(item.ItemId) ?? 0;
                    total += price * item.Quantity;
                    RemoveItem(id, item.Quantity);
                }
            }
            return total;
        }

        public long QuickSellByFilter(InventoryFilter filter)
        {
            var items = _slots.Where(s => !s.IsEmpty && filter.Matches(s.Item) && !s.Item.IsLocked && !s.Item.IsFavorite)
                .Select(s => s.Item.GetStackKey() ?? s.Item.InstanceId).ToList();
            return QuickSell(items);
        }

        public long QuickSellJunk()
        {
            var filter = new InventoryFilter
            {
                Rarities = new[] { Rarity.Common },
                HideEquipped = true,
                HideMaxStack = false
            };
            return QuickSellByFilter(filter);
        }

        public long QuickSellAllExceptFavorites()
        {
            var items = _slots.Where(s => !s.IsEmpty && !s.Item.IsFavorite && !s.Item.IsLocked)
                .Select(s => s.Item.GetStackKey() ?? s.Item.InstanceId).ToList();
            return QuickSell(items);
        }
        #endregion

        #region Consumption & Usage
        public bool ConsumeItem(string instanceId, int quantity = 1)
        {
            return RemoveItem(instanceId, quantity) > 0;
        }

        public bool UseItem(string instanceId)
        {
            var item = GetItem(instanceId);
            if (item == null) return false;

            var itemData = ItemDatabase.Instance?.GetItem(item.ItemId);
            if (itemData == null) return false;

            // Handle different item categories
            return itemData.Category switch
            {
                ItemCategory.Consumable => UseConsumable(item, itemData),
                ItemCategory.Chest => OpenChest(item, itemData),
                ItemCategory.SkillBook => UseSkillBook(item, itemData),
                ItemCategory.UpgradeStone => UseUpgradeStone(item, itemData),
                _ => false,
            };

        }

        private bool UseConsumable(InventoryItem item, ItemData data)
        {
            // Apply consumable effects
            // This would integrate with player stats, healing, buffs, etc.
            ConsumeItem(item.GetStackKey() ?? item.InstanceId, 1);
            return true;
        }

        private bool OpenChest(InventoryItem item, ItemData data)
        {
            // Open chest and give rewards
            // This would use DropTable/LootGenerator
            ConsumeItem(item.GetStackKey() ?? item.InstanceId, 1);
            return true;
        }

        private bool UseSkillBook(InventoryItem item, ItemData data)
        {
            // Learn skill
            ConsumeItem(item.GetStackKey() ?? item.InstanceId, 1);
            return true;
        }

        private bool UseUpgradeStone(InventoryItem item, ItemData data)
        {
            // Apply upgrade stone
            ConsumeItem(item.GetStackKey() ?? item.InstanceId, 1);
            return true;
        }
        #endregion

        #region Destruction
        public bool DestroyItem(string instanceId, int quantity = 1)
        {
            return RemoveItem(instanceId, quantity) > 0;
        }

        public int DestroyItemsByFilter(InventoryFilter filter)
        {
            int destroyed = 0;
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty && filter.Matches(slot.Item) && !slot.Item.IsLocked)
                {
                    DestroyItem(slot.Item.GetStackKey() ?? slot.Item.InstanceId, slot.Item.Quantity);
                    destroyed++;
                }
            }
            return destroyed;
        }
        #endregion

        #region Persistence
        public InventorySaveData GetSaveData()
        {
            var items = _slots
                .Where(s => !s.IsEmpty)
                .OrderBy(s => s.SlotIndex)
                .Select(s => ToSaveItem(s.SlotIndex, s.Item))
                .ToArray();

            // Socketed gems live outside the stack; GemService owns them (GemInstanceId-keyed).
            var socketedGems = GemService.Instance?.GetSocketedGemsSaveData()
                ?? Array.Empty<GemInstanceData>();

            return new InventorySaveData
            {
                Capacity = Capacity, // Current expanded capacity
                LastModifiedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Items = items,
                SocketedGems = socketedGems
            };
        }

        /// <summary>
        /// Persists equipment with full rarity-rolled state:
        /// MaxDurability, DurabilityLossPerUse, RepairCostPerDurability, MaxSockets,
        /// EquipmentType, and Main/Secondary attributes.
        /// Stackables persist identity + quantity + flags only.
        /// </summary>
        private static InventoryItemData ToSaveItem(int slotIndex, InventoryItem item)
        {
            var data = new InventoryItemData
            {
                ItemId = item.ItemId,
                Quantity = item.Quantity,
                SlotIndex = slotIndex,
                IsFavorite = item.IsFavorite,
                IsLocked = item.IsLocked,
                IsNew = item.IsNew,
                AcquiredTimestamp = item.AcquiredTimestamp
            };

            if (item.IsEquippable())
            {
                // Unique instance: identity + full state including rarity-rolled config.
                data.InstanceId = item.InstanceId;
                data.Level = item.Level;
                data.MaxLevel = item.MaxLevel;
                data.MaxDurability = item.MaxDurability;
                data.CurrentDurability = item.CurrentDurability;
                data.DurabilityLossPerUse = item.DurabilityLossPerUse;
                data.RepairCostPerDurability = item.RepairCostPerDurability;
                data.MaxSockets = item.MaxSockets;
                data.EquipmentType = item.EquipmentType;
                data.Enchantment = item.Enchantment?.Clone();
                data.Sockets = item.Sockets?.Select(s => s?.Clone()).ToArray();
                data.AttributeData = item.AttributeData;
            }
            else
            {
                // Stackable: identity + quantity + flags. StackId ('a'..'z') keeps distinct stacks of the
                // same item separate; KeyId = ItemId + StackId is the stable stack key across saves.
                data.InstanceId = null;
                data.StackId = item.StackId;
                data.KeyId = item.GetStackKey();
            }
            return data;
        }

        public void LoadFromSaveData(InventorySaveData data)
        {
            if (data == null) return;

            // Config is loaded from dataInventory.json - we only restore capacity.
            // Handle legacy Config field from v3 saves (Width * Height preserves expansions)
            int capacity = data.Capacity;
            if (capacity == 0 && data.LegacyConfig != null)
            {
                capacity = data.LegacyConfig.Width * data.LegacyConfig.Height;
            }
            if (capacity == 0)
            {
                capacity = 48; // Default BaseCapacity
            }

            // Sync Height so Capacity (Width * Height) matches restored slot count
            _config.Height = (capacity + _config.Width - 1) / _config.Width;
            CreateSlots(capacity);

            if (data.Items != null)
            {
                foreach (var saveItem in data.Items)
                {
                    if (saveItem == null || string.IsNullOrEmpty(saveItem.ItemId)) continue;
                    
                    var item = RestoreItem(saveItem);
                    if (item == null) continue;
                    int targetSlot = saveItem.SlotIndex >= 0 && saveItem.SlotIndex < _slots.Count
                        ? saveItem.SlotIndex
                        : FindEmptySlot();
                    if (targetSlot >= 0) 
                    {
                        _slots[targetSlot].Item = item;
                        _slots[targetSlot].SlotIndex = targetSlot;
                    }
                }
            }

            MarkAllSlotsDirty(DirtyType.All);
            // Notify UI subscribers (CardCollection button state, InventoryUI, etc.)
            OnInventoryChanged?.Invoke(InventoryChangedEventArgs.CreateRemoved(string.Empty, string.Empty, 0, 0, null));
            FlushDirtySlots();
        }

        /// <summary>
        /// Rebuilds a runtime InventoryItem. Stackables get hardening defaults;
        /// equipment restores full state from persisted InventoryItemData (no DB lookup).
        /// Socketed gems get GemInstanceId bound so GemService can rehydrate the instance.
        /// </summary>
        private static InventoryItem RestoreItem(InventoryItemData data)
        {
            // Stackable: must exist in ItemDatabase
            if (ItemDatabase.Instance == null || !ItemDatabase.Instance.IsValidItemId(data.ItemId))
            {
                Debug.LogWarning($"[InventoryService] Dropping orphan '{data.ItemId}' (qty={data.Quantity})");
                return null; // drop, or push to _graveyard list for recovery
            }
            
            // Equipment identity comes from persisted EquipmentType (crafted items like "cotton_hat"
            // are NOT in ItemDatabase - only the "equip_base" template is)
            bool isEquipment = data.EquipmentType.HasValue;

            var item = new InventoryItem
            {
                ItemId = data.ItemId,
                Quantity = data.Quantity,
                IsFavorite = data.IsFavorite,
                IsLocked = data.IsLocked,
                IsNew = data.IsNew,
                AcquiredTimestamp = data.AcquiredTimestamp,
                EquipmentType = data.EquipmentType ?? EquipmentType.None
            };

            if (isEquipment)
            {
                // ---- Equipment: restore full state from saved data (no DB lookup) ----
                item.InstanceId = data.InstanceId;
                item.Level = data.Level ?? 1;
                item.MaxLevel = data.MaxLevel ?? 20;
                item.Enchantment = data.Enchantment;

                // ponytail: use persisted durability instead of DB template (crafted items lack DB entry)
                item.MaxDurability = data.MaxDurability ?? 100;
                item.CurrentDurability = data.CurrentDurability ?? item.MaxDurability;
                item.DurabilityLossPerUse = data.DurabilityLossPerUse ?? 1;
                item.RepairCostPerDurability = data.RepairCostPerDurability ?? 5;

                item.MaxSockets = data.MaxSockets ?? 0;
                if (data.Sockets != null)
                {
                    var sockets = data.Sockets.Where(s => s != null && s.IsUnlocked).ToArray();
                    for (int i = 0; i < sockets.Length; i++)
                    {
                        sockets[i].SocketIndex = i;
                    }
                    item.Sockets = sockets;
                }

                item.AttributeData = data.AttributeData;

                if (string.IsNullOrEmpty(item.InstanceId))
                    item.InstanceId = Guid.NewGuid().ToString();
            }
            else
            {
                // ---- Stackable: no instance identity, StackKey is the stack's handle ----
                item.InstanceId = null;
                item.StackId = data.StackId;
            }

            return item;
        }

        public void Reset()
        {
            foreach (var slot in _slots)
            {
                slot.Item = null;
            }
            MarkAllSlotsDirty(DirtyType.All);
            OnInventoryChanged?.Invoke(InventoryChangedEventArgs.CreateRemoved(string.Empty, string.Empty, 0, 0, null));
            FlushDirtySlots();
        }
        #endregion

        #region Utility
        public void ValidateIntegrity()
        {
            // Identity rule: equipment (unique instances) must carry an InstanceId;
            // stackables key on StackKey and may have null InstanceId.
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                if (_slots[i].Item == null) continue;
                if (_slots[i].Item.IsEquippable() && string.IsNullOrEmpty(_slots[i].Item.InstanceId))
                {
                    _slots[i].Item = null;
                }
            }

            // Fix stack counts
            MergeStacks();
            MarkAllSlotsDirty(DirtyType.All);
        }

        public int CleanupEmptySlots()
        {
            int moves = 0;
            int writeIndex = 0;

            for (int readIndex = 0; readIndex < _slots.Count; readIndex++)
            {
                if (!_slots[readIndex].IsEmpty)
                {
                    if (readIndex != writeIndex)
                    {
                        _slots[writeIndex].Item = _slots[readIndex].Item;
                        _slots[readIndex].Item = null;
                        moves++;
                    }
                    writeIndex++;
                }
            }
            MarkAllSlotsDirty(DirtyType.All);
            return moves;
        }
        #endregion

        #region Helper Methods
        private InventoryItem CreateInventoryItem(string itemId, int quantity)
        {
            var itemData = ItemDatabase.Instance?.GetItem(itemId);
            var equipmentData = itemData as EquipmentData;

            var item = new InventoryItem
            {
                ItemId = itemId,
                Quantity = quantity,
                Level = equipmentData?.BaseLevel ?? 1,
                MaxDurability = equipmentData?.MaxDurability ?? 0,
                CurrentDurability = equipmentData?.MaxDurability ?? 0,
                AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };

            // Identity rule: only unique instances (equipment) carry an InstanceId.
            // Stackables have no instance identity — their stack key is ItemId (+ StackId for splits).
            if (itemData != null && itemData.Category == ItemCategory.Equipment)
                item.InstanceId = Guid.NewGuid().ToString();

            // Initialize sockets if equipment
            if (equipmentData != null && equipmentData.MaxSockets > 0)
            {
                item.Sockets = new SocketData[equipmentData.MaxSockets];
                for (int i = 0; i < equipmentData.MaxSockets; i++)
                {
                    item.Sockets[i] = new SocketData
                    {
                        SocketIndex = i,
                        IsUnlocked = i == 0 // First socket unlocked by default
                    };
                }
            }

            return item;
        }

        private int FindEmptySlot()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsEmpty) return i;
            }
            return -1;
        }

        private void RebuildFilteredIndices()
        {
            _filteredIndices.Clear();
            if (!_isFiltered) return;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].IsEmpty && _currentFilter.Matches(_slots[i].Item))
                {
                    _filteredIndices.Add(i);
                }
            }
        }

        private void NotifyAdded(InventoryItem item, int slotIndex)
        {
            item.IsNew = true;
            MarkDirty(slotIndex, DirtyType.Item | DirtyType.Tooltip);
            MarkSaveDirty();
            OnItemAdded?.Invoke(item);
            OnInventoryChanged?.Invoke(InventoryChangedEventArgs.CreateAdded(item.InstanceId, item.ItemId, slotIndex, item.Quantity, item));
            if (item.IsEquippable())
            {
                // tambahkan data equipment baru hasil crafting ke database item runtime?
            }
        }

        private void NotifyRemoved(InventoryItem item, int slotIndex)
        {
            MarkDirty(slotIndex, DirtyType.Item);
            MarkSaveDirty();
            OnItemRemoved?.Invoke(item);
            OnInventoryChanged?.Invoke(InventoryChangedEventArgs.CreateRemoved(item.InstanceId, item.ItemId, slotIndex, item.Quantity, item));
        }

        private void NotifyQuantityChanged(InventoryItem item, int delta, int slotIndex)
        {
            MarkDirty(slotIndex, DirtyType.Stack);
            MarkSaveDirty();
            OnItemQuantityChanged?.Invoke(item, delta);
            OnInventoryChanged?.Invoke(InventoryChangedEventArgs.CreateQuantityChanged(item.InstanceId, item.ItemId, slotIndex, delta, item));
        }

        private void NotifyMoved(InventoryItem item, int fromSlot, int toSlot)
        {
            MarkDirty(fromSlot, DirtyType.Item);
            MarkDirty(toSlot, DirtyType.Item);
            MarkSaveDirty();
            OnInventoryChanged?.Invoke(InventoryChangedEventArgs.CreateMoved(item.InstanceId, item.ItemId, fromSlot, toSlot, item));
        }

        private void NotifySwapped(int slotA, int slotB)
        {
            MarkDirty(slotA, DirtyType.Item);
            MarkDirty(slotB, DirtyType.Item);
            MarkSaveDirty();
            var itemA = _slots[slotA].Item;
            var itemB = _slots[slotB].Item;
            OnInventoryChanged?.Invoke(new InventoryChangedEventArgs
            {
                ChangeType = InventoryChangeType.Swapped,
                InstanceId = itemA?.InstanceId,
                ItemId = itemA?.ItemId,
                SlotIndex = slotA,
                QuantityChange = slotB,
                Item = itemA
            });
        }
        #endregion

        /// <summary>
        /// Marks the persistent inventory data dirty so autosave triggers promptly.
        /// </summary>
        private void MarkSaveDirty()
        {
            if (Manager.SaveManager.Instance != null)
                Manager.SaveManager.Instance.MarkInventoryDirty();
        }

        #region Dirty Tracking Helpers
        /// <summary>
        /// Marks a slot as dirty with specific flags.
        /// </summary>
        private void MarkDirty(int slotIndex, DirtyType dirtyType)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count) return;
            if (!_dirtySlots.TryAdd(slotIndex, dirtyType))
            {
                _dirtySlots[slotIndex] |= dirtyType;
            }
        }

        /// <summary>
        /// Marks all slots as dirty with the given flag.
        /// </summary>
        private void MarkAllSlotsDirty(DirtyType dirtyType)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                MarkDirty(i, dirtyType);
            }
        }

        /// <summary>
        /// Clears all dirty flags.
        /// </summary>
        private void ClearAllDirty()
        {
            _dirtySlots.Clear();
        }

        /// <summary>
        /// Flushes dirty slots to UI subscribers.
        /// Call this after a batch of operations to notify UI of all changes at once.
        /// </summary>
        public void FlushDirtySlots()
        {
            if (_dirtySlots.Count > 0)
            {
                var snapshot = new Dictionary<int, DirtyType>(_dirtySlots);
                OnSlotsDirty?.Invoke(snapshot);
                ClearAllDirty();
            }
        }

        /// <summary>
        /// Marks an item dirty by instance ID. Useful for cross-service notifications (e.g., durability changes from DurabilityService).
        /// </summary>
        public void MarkItemDirty(string instanceId, DirtyType dirtyType)
        {
            if (string.IsNullOrEmpty(instanceId)) return;
            int slotIndex = _slots.FindIndex(s => !s.IsEmpty && s.Item.InstanceId == instanceId);
            if (slotIndex >= 0)
                MarkDirty(slotIndex, dirtyType);
        }

        /// <summary>
        /// Marks an item dirty by instance ID with multiple flags.
        /// </summary>
        public void MarkItemDirty(string instanceId, params DirtyType[] dirtyTypes)
        {
            if (string.IsNullOrEmpty(instanceId) || dirtyTypes == null || dirtyTypes.Length == 0) return;
            int slotIndex = _slots.FindIndex(s => !s.IsEmpty && s.Item.InstanceId == instanceId);
            if (slotIndex >= 0)
            {
                DirtyType combined = DirtyType.None;
                foreach (var dt in dirtyTypes)
                    combined |= dt;
                MarkDirty(slotIndex, combined);
            }
        }

        /// <summary>
        /// Applies a reward item without idempotency persistence (session-only).
        /// Returns ApplyResult indicating success or failure.
        /// </summary>
        public ApplyResult ApplyReward(InventoryItem item, string rewardOperationId)
        {
            if (item == null || string.IsNullOrEmpty(rewardOperationId))
                return ApplyResult.Failure;

            // Attempt to add the item
            bool added = AddItemInstance(item);
            if (!added) return ApplyResult.Failure;

            Debug.Log($"[InventoryService] ApplyReward item moved to inventory");
            return ApplyResult.Success;
        }

        /// <summary>
        /// Checks if a reward operation has already been applied (session-only, no persistence).
        /// </summary>
        public bool HasAppliedOperation(string rewardOperationId)
        {
            // No persistent tracking - always returns false
            return false;
        }
        #endregion
    }
}