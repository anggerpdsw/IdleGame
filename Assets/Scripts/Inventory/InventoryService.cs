using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdleDefenseSurvival.Items;
using IdleDefenseSurvival.Equipment;
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
        public event Action OnInventorySorted;
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
        public string AddItem(string itemId, int quantity = 1, Dictionary<string, object> customData = null)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0) return string.Empty;

            var itemData = ItemDatabase.Instance?.GetItem(itemId);
            if (itemData == null)
            {
                Debug.LogWarning($"[InventoryService] Item not found: {itemId}");
                return string.Empty;
            }

            int maxStack = itemData.StackSize > 0 ? itemData.StackSize : MaxStackSize;

            // Try to stack with existing items first
            if (itemData.IsStackable)
            {
                foreach (var slot in _slots)
                {
                    if (!slot.IsEmpty && slot.Item.ItemId == itemId && !slot.Item.IsEquippable())
                    {
                        int canAdd = Math.Min(quantity, maxStack - slot.Item.Quantity);
                        if (canAdd > 0)
                        {
                            slot.Item.Quantity += canAdd;
                            quantity -= canAdd;
                            NotifyQuantityChanged(slot.Item, canAdd, slot.SlotIndex);

                            if (quantity <= 0) return slot.Item.InstanceId;
                        }
                    }
                }
            }

            // Find empty slot for remaining quantity
            while (quantity > 0)
            {
                int emptySlot = FindEmptySlot();
                if (emptySlot < 0)
                {
                    // Inventory full
                    if (!ExpandCapacity(1))
                    {
                        Debug.LogWarning("[InventoryService] Inventory full!");
                        break;
                    }
                    emptySlot = FindEmptySlot();
                }

                int addAmount = Math.Min(quantity, maxStack);
                var newItem = CreateInventoryItem(itemId, addAmount, customData);
                _slots[emptySlot].Item = newItem;
                quantity -= addAmount;

                NotifyAdded(newItem, emptySlot);

                if (quantity <= 0) return newItem.InstanceId;
            }

            return string.Empty;
        }

        public bool AddItemInstance(InventoryItem item)
        {
            if (item == null) return false;

            // Try stacking first
            if (item.IsStackable)
            {
                foreach (var slot in _slots)
                {
                    if (!slot.IsEmpty && slot.Item.ItemId == item.ItemId && !slot.Item.IsMaxStack)
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
                if (!slot.IsEmpty && slot.Item.InstanceId == instanceId)
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
            if (!to.IsEmpty && to.Item.ItemId == from.Item.ItemId && from.Item.IsStackable && to.Item.IsStackable)
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
            if (slotA < 0 || slotA >= _slots.Count || slotB < 0 || slotB >= _slots.Count || slotA == slotB)
                return false;

            var temp = _slots[slotA].Item;
            _slots[slotA].Item = _slots[slotB].Item;
            _slots[slotB].Item = temp;

            if (_slots[slotA].Item != null) _slots[slotA].Item.IsNew = false;
            if (_slots[slotB].Item != null) _slots[slotB].Item.IsNew = false;

            NotifySwapped(slotA, slotB);
            return true;
        }

        public InventoryItem SplitStack(string instanceId, int amount)
        {
            if (string.IsNullOrEmpty(instanceId) || amount <= 0) return null;

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEmpty && slot.Item.InstanceId == instanceId)
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
                .GroupBy(s => s.Item.ItemId)
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
            return _slots.FirstOrDefault(s => !s.IsEmpty && s.Item.InstanceId == instanceId)?.Item;
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

        public IReadOnlyList<InventoryItem> GetItemsByRarity(ItemRarity rarity)
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

        #region Sorting & Filtering
        public void Sort(InventorySortType sortType, bool ascending = true)
        {
            var items = _slots.Where(s => !s.IsEmpty).Select(s => s.Item).ToList();

            Func<InventoryItem, IComparable> selector = sortType switch
            {
                InventorySortType.Category => i => i.GetItemCategory().ToString(),
                InventorySortType.Rarity => i => (int)i.GetRarity(),
                InventorySortType.Level => i => i.Level,
                InventorySortType.Name => i => ItemDatabase.Instance?.GetItem(i.ItemId)?.Name ?? i.ItemId,
                InventorySortType.Value => i => ItemDatabase.Instance?.GetSellPrice(i.ItemId) ?? 0L,
                InventorySortType.Newest => i => -i.AcquiredTimestamp,
                InventorySortType.Quantity => i => i.Quantity,
                InventorySortType.EnhanceLevel => i => i.EnhanceLevel,
                InventorySortType.Durability => i => i.CurrentDurability,
                _ => i => i.InstanceId
            };

            items = ascending ? items.OrderBy(selector).ToList() : items.OrderByDescending(selector).ToList();

            // Rebuild slots
            ClearAllSlots();
            for (int i = 0; i < items.Count && i < _slots.Count; i++)
            {
                _slots[i].Item = items[i];
            }
            MarkAllSlotsDirty(DirtyType.All);
            OnInventorySorted?.Invoke();
        }

        public void SortByCategory() => Sort(InventorySortType.Category);
        public void SortByRarity() => Sort(InventorySortType.Rarity);
        public void SortByLevel() => Sort(InventorySortType.Level);
        public void SortByName() => Sort(InventorySortType.Name);
        public void SortByValue() => Sort(InventorySortType.Value);
        public void SortByNewest() => Sort(InventorySortType.Newest);

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
                if (slotIndex >= 0) MarkDirty(slotIndex, DirtyType.Favorite);
            }
        }

        public void SetLocked(string instanceId, bool locked)
        {
            var item = GetItem(instanceId);
            if (item != null)
            {
                item.IsLocked = locked;
                int slotIndex = _slots.FindIndex(s => s.Item == item);
                if (slotIndex >= 0) MarkDirty(slotIndex, DirtyType.Lock);
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
        #endregion

        #region Capacity Management
        public bool ExpandCapacity(int slots = 1)
        {
            if (Capacity >= _config.MaxCapacity) return false;

            int cost = GetExpansionCost();
            if (!EconomyManager.Instance.TrySpendCurrency(CurrencyType.Gold, cost, "Inventory Expansion"))
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
            {
                _slots.Add(new InventorySlot { SlotIndex = _slots.Count });
            }
            // Mark new slots as dirty
            for (int i = startIndex; i < _slots.Count; i++)
            {
                MarkDirty(i, DirtyType.Item);
            }

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
            var items = _slots.Where(s => !s.IsEmpty && filter.Matches(s.Item) && !s.Item.IsLocked && !s.Item.IsFavorite).Select(s => s.Item.InstanceId).ToList();
            return QuickSell(items);
        }

        public long QuickSellJunk()
        {
            var filter = new InventoryFilter
            {
                Rarities = new[] { ItemRarity.Common, ItemRarity.Uncommon },
                HideEquipped = true,
                HideMaxStack = false
            };
            return QuickSellByFilter(filter);
        }

        public long QuickSellAllExceptFavorites()
        {
            var items = _slots.Where(s => !s.IsEmpty && !s.Item.IsFavorite && !s.Item.IsLocked).Select(s => s.Item.InstanceId).ToList();
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
            ConsumeItem(item.InstanceId, 1);
            return true;
        }

        private bool OpenChest(InventoryItem item, ItemData data)
        {
            // Open chest and give rewards
            // This would use DropTable/LootGenerator
            ConsumeItem(item.InstanceId, 1);
            return true;
        }

        private bool UseSkillBook(InventoryItem item, ItemData data)
        {
            // Learn skill
            ConsumeItem(item.InstanceId, 1);
            return true;
        }

        private bool UseUpgradeStone(InventoryItem item, ItemData data)
        {
            // Apply upgrade stone
            ConsumeItem(item.InstanceId, 1);
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
                    DestroyItem(slot.Item.InstanceId, slot.Item.Quantity);
                    destroyed++;
                }
            }
            return destroyed;
        }
        #endregion

        #region Persistence
        public InventorySaveData GetSaveData()
        {
            var slotData = _slots
                .Where(s => !s.IsEmpty)
                .Select(s => new InventorySlotData { SlotIndex = s.SlotIndex, Item = s.Item })
                .ToArray();

            return new InventorySaveData
            {
                Config = _config,
                Slots = slotData,
                LastModifiedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        public void LoadFromSaveData(InventorySaveData data)
        {
            if (data == null) return;

            _config = data.Config ?? new InventoryConfig();
            CreateSlots(_config.BaseCapacity);

            if (data.Slots != null)
            {
                foreach (var slotData in data.Slots)
                {
                    if (slotData.SlotIndex >= 0 && slotData.SlotIndex < _slots.Count)
                    {
                        _slots[slotData.SlotIndex].Item = slotData.Item;
                    }
                }
            }

            MarkAllSlotsDirty(DirtyType.All);
            // Notify UI subscribers (CardCollection button state, InventoryUI, etc.)
            OnInventoryChanged?.Invoke(InventoryChangedEventArgs.CreateRemoved(string.Empty, string.Empty, 0, 0, null));
            FlushDirtySlots();
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
            // Remove null items
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                if (_slots[i].Item == null) continue;
                if (string.IsNullOrEmpty(_slots[i].Item.InstanceId))
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
        private InventoryItem CreateInventoryItem(string itemId, int quantity, Dictionary<string, object> customData)
        {
            var itemData = ItemDatabase.Instance?.GetItem(itemId);
            var equipmentData = itemData as EquipmentData;

            var item = new InventoryItem
            {
                InstanceId = Guid.NewGuid().ToString(),
                ItemId = itemId,
                Quantity = quantity,
                Level = equipmentData?.BaseLevel ?? 1,
                MaxDurability = equipmentData?.MaxDurability ?? 0,
                CurrentDurability = equipmentData?.MaxDurability ?? 0,
                AcquiredTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CustomData = customData
            };

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

        private void ClearAllSlots()
        {
            foreach (var slot in _slots)
            {
                slot.Item = null;
            }
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
        #endregion
    }
}